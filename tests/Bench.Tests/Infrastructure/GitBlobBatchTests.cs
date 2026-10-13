using System.Text;
using Bench.Domain;
using Bench.Infrastructure.Git;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>Review finding (performance, E2 code round): the hygiene scan ran one <c>git show</c> per staged file — a process launch each, so a
/// push of a few hundred files took minutes. The blobs are read through ONE <c>git cat-file --batch</c>, in the order asked, as BYTES (a blob
/// is a payload a text decode would change), each handed to the caller with its place in the list as it arrives; an object git does not have
/// refuses the whole read. The ids come from <c>git diff --raw -z --no-abbrev</c> (the index, a range) or <c>git ls-tree -r -z</c> (a tree),
/// parsed here — a rename is read under its NEW name, a submodule's gitlink is not a blob and is skipped.</summary>
public sealed class GitBlobBatchTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private readonly DatedGitRepo _repo = new(TestContext.Current.CancellationToken);

    public void Dispose() => _repo.Dispose();

    [Fact]
    public void A_raw_diff_lists_each_changed_path_with_the_blob_it_will_have_a_rename_under_its_new_name_and_no_gitlink()
    {
        var a = new string('a', 40);
        var b = new string('b', 40);
        var c = new string('c', 40);
        var d = new string('d', 40);
        var zero = new string('0', 40);
        var text = $":100644 100644 {a} {b} M\0src/a.cs\0:000000 100644 {zero} {c} A\0src/b.cs\0:100644 100644 {d} {d} R100\0old.cs\0new.cs\0:160000 160000 {a} {b} M\0sub\0";

        var listed = GitBlobListing.RawDiff(text).Ok();

        listed.Should().Equal(new GitBlobRef("src/a.cs", b), new GitBlobRef("src/b.cs", c), new GitBlobRef("new.cs", d));
        GitBlobListing.RawDiff(string.Empty).Ok().Should().BeEmpty();
        GitBlobListing.RawDiff(":100644 100644 x\0src/a.cs\0").Reason().Should().Contain("raw diff");
    }

    [Fact]
    public void An_ls_tree_lists_every_blob_with_its_id_and_skips_a_commit_entry()
    {
        var a = new string('a', 40);
        var b = new string('b', 40);
        var c = new string('c', 40);
        var text = $"100644 blob {a}\tREADME.md\0160000 commit {b}\tsub\0120000 blob {c}\tdocs/link\0";

        GitBlobListing.LsTree(text).Ok().Should().Equal(new GitBlobRef("README.md", a), new GitBlobRef("docs/link", c));
        GitBlobListing.LsTree("100644 blob\tREADME.md\0").Reason().Should().Contain("ls-tree");
    }

    [Fact]
    public async Task The_blobs_come_back_in_the_order_asked_byte_for_byte_each_with_its_place_and_a_missing_object_refuses_the_read()
    {
        byte[] binary = [0x00, 0x01, 0xFF, 0xFE, .. Encoding.Unicode.GetBytes("wide"), 0x00, 0x0A, 0x0D];
        await _repo.InitAsync(("text.txt", "hello\n"), ("init", "2026-05-01T10:00:00+00:00"));
        await File.WriteAllBytesAsync(Path.Combine(_repo.Root, "blob.bin"), binary, Ct);
        await _repo.GitAsync("add", "-A");
        await _repo.GitAsync("commit", "-q", "-m", "bin", "--date", "2026-05-02T10:00:00+00:00");
        var textId = (await _repo.GitAsync("rev-parse", "HEAD:text.txt")).Trim();
        var binaryId = (await _repo.GitAsync("rev-parse", "HEAD:blob.bin")).Trim();

        var read = await GitBlobBatch.ReadAsync(_repo.Root, [textId, binaryId, textId], Timeout, (at, bytes) => (at, bytes.ToArray()), Ct);

        var blobs = read.Ok();
        blobs.Select(b => b.at).Should().Equal(0, 1, 2);
        blobs[0].Item2.Should().Equal(Encoding.UTF8.GetBytes("hello\n"));
        blobs[1].Item2.Should().Equal(binary, "a blob is a payload — not a line of text");
        blobs[2].Item2.Should().Equal(blobs[0].Item2);
        (await GitBlobBatch.ReadAsync(_repo.Root, [], Timeout, (at, _) => at, Ct)).Ok().Should().BeEmpty();

        var missing = await GitBlobBatch.ReadAsync(_repo.Root, [textId, new string('0', 40)], Timeout, (at, _) => at, Ct);

        missing.Reason().Should().Contain("object 2 of 2").And.Contain("missing");
    }

    /// <summary>The reason the launch writes stdin WHILE it reads stdout: five hundred requests of a 20 KB blob are 20 KB of input against 10 MB of
    /// answers, and a child whose stdout pipe fills before its stdin is fully written has both sides waiting on a full pipe — that would arrive here
    /// as a timeout, not as the sum of the sizes.</summary>
    [Fact]
    public async Task A_long_request_list_whose_answers_outgrow_the_pipe_does_not_deadlock()
    {
        await _repo.InitAsync(("big.txt", new string('x', 20_000) + "\n"), ("init", "2026-05-01T10:00:00+00:00"));
        var id = (await _repo.GitAsync("rev-parse", "HEAD:big.txt")).Trim();
        var ids = Enumerable.Repeat(id, 500).ToList();

        var read = await GitBlobBatch.ReadAsync(_repo.Root, ids, Timeout, (_, bytes) => bytes.Length, Ct);

        read.Ok().Sum().Should().Be(500 * 20_001);
    }
}
