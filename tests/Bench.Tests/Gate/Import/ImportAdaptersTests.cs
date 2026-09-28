using Bench.Application.Gate;
using Bench.Domain;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Git;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Infrastructure.GateStoreFixtures;

namespace Bench.Tests.Gate.Import;

/// <summary>The import's adapters and its pre-flight at their edges — each test is a code-round finding reproduced first.</summary>
public sealed class ImportAdaptersTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_source_named_with_a_trailing_separator_still_finds_its_files()
    {
        using var root = NewRoot();
        await File.WriteAllTextAsync(Path.Combine(root.Path, "runs.jsonl"), "{}", Ct);

        var source = DirectoryImportSource.Open(root.Path + Path.DirectorySeparatorChar).Ok();

        source.Exists("runs.jsonl").Should().BeTrue("a shell's tab completion adds the separator, and every lookup failed with it");
        source.Label.Should().Be(Path.GetFileName(root.Path));
    }

    private sealed class Unreadable : IImportSource
    {
        public string Label => "workspace";

        public bool Exists(string relative) => relative == CalibPreflight.RunsFile;

        public Task<Outcome<byte[]>> ReadBytesAsync(string relative, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<byte[]>.Failure($"{relative} is locked by another process"));

        public Task<IReadOnlyList<string>> FilesUnderAsync(string relativeDirectory, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    [Fact]
    public async Task A_runs_file_that_cannot_be_read_refuses_the_import_instead_of_reading_as_empty()
    {
        var read = await CalibPreflight.ReadAsync(new Unreadable(), ImportFixture.Suite, ImportFixture.Models, ImportFixture.Names, Ct);

        read.Reason().Should().Contain("locked by another process", "an unreadable file is not a workspace with no runs");
    }

    /// <summary>A workspace whose runs.jsonl reads but one other file does not — the failure the import must name.</summary>
    private sealed class OneUnreadable(IImportSource inner, string unreadable) : IImportSource
    {
        public string Label => inner.Label;

        public bool Exists(string relative) => inner.Exists(relative);

        public Task<Outcome<byte[]>> ReadBytesAsync(string relative, CancellationToken cancellationToken) =>
            relative.EndsWith(unreadable, StringComparison.Ordinal)
                ? Task.FromResult(Outcome<byte[]>.Failure($"{relative} is locked by another process"))
                : inner.ReadBytesAsync(relative, cancellationToken);

        public Task<IReadOnlyList<string>> FilesUnderAsync(string relativeDirectory, CancellationToken cancellationToken) =>
            inner.FilesUnderAsync(relativeDirectory, cancellationToken);
    }

    [Fact]
    public async Task A_reply_or_a_run_file_that_cannot_be_read_is_a_refusal_naming_it_never_an_empty_answer_or_a_skip()
    {
        using var root = NewRoot();
        ImportFixture.Materialize(root.Path);
        var source = DirectoryImportSource.Open(root.Path).Ok();

        var reply = await CalibPreflight.ReadAsync(new OneUnreadable(source, "p2-grok-4.7-js3-r1/reply.json"), ImportFixture.Suite, ImportFixture.Models, ImportFixture.Names, Ct);
        var copied = await ImportedFiles.CopyAsync(new OneUnreadable(source, "tap/call-01.json"), "runs/p2-grok-4.7-js3-r1", Ct);

        reply.Reason().Should().Contain("reply.json is locked", "a reply that could not be read is not a reply with no findings");
        copied.Reason().Should().Contain("call-01.json is locked", "a file that could not be read is not 'a name outside the alphabet', and a cell committed without it would read 'unchanged' forever");
    }

    [Fact]
    public async Task A_record_whose_product_sha_does_not_read_is_refused_by_the_pre_flight_not_pinned_to_nothing()
    {
        using var root = NewRoot();
        ImportFixture.Materialize(root.Path, edit: line =>
        {
            if ((string)line["id"]! == "p2-grok-4.7-js3-r1")
            {
                line["product_sha"] = "not-a-sha";
            }

            return line;
        });

        var read = await CalibPreflight.ReadAsync(DirectoryImportSource.Open(root.Path).Ok(), ImportFixture.Suite, ImportFixture.Models, ImportFixture.Names, Ct);

        read.Reason().Should().Contain("p2-grok-4.7-js3-r1").And.Contain("not a git sha");
    }

    [Fact]
    public async Task An_empty_short_sha_is_refused_rather_than_resolved_to_the_checkouts_head()
    {
        using var repo = new DatedGitRepo(Ct);
        await repo.InitAsync(("readme.md", "hello"), ("init", "2026-09-01T10:00:00Z"));

        var resolved = await new GitCommitResolver(repo.Root).ResolveAsync("  ", Ct);

        resolved.Reason().Should().Contain("no sha", "'^{commit}' alone is HEAD — a case with no commit would be pinned to whatever the checkout is at");
    }
}
