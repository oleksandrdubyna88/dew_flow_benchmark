using Bench.Domain.Authoring;
using Bench.Infrastructure.Git;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>Reading a merged fix out of a real repository (todo/PLAN_investigate_vs_implement.md §3.5,
/// step 3's git half): the base commit, the seed date and the diff are DERIVED from the repository,
/// never authored — over a temp repo and the real git, because the failure modes worth testing
/// (a root commit, a merge, an unknown ref) are git's answers, not ours.</summary>
public sealed class FixHarvestTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly DatedGitRepo _repo;

    public FixHarvestTests() => _repo = new DatedGitRepo(TestContext.Current.CancellationToken);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_fix_commit_is_read_whole_base_seed_subject_and_diff()
    {
        await _repo.InitAsync(
            ("src/Policy.cs", "class Policy { int A() => 1; }\n"),
            commit: ("seed the tree", "2026-05-01T12:00:00"));
        var fixSha = await _repo.CommitAsync(
            ("src/Policy.cs", "class Policy { int A() => 2; }\n"),
            commit: ("fix: A returned the wrong constant", "2026-08-11T09:30:00"));

        var harvested = (await FixHarvest.ReadAsync(_repo.Root, fixSha, Timeout, Ct)).Ok();

        harvested.Fix.Value.Should().Be(fixSha);
        harvested.Base.Value.Should().NotBe(fixSha).And.HaveLength(40);
        harvested.AuthoredOn.Should().Be(new DateOnly(2026, 8, 11), "the seed is the repository's date, never an authored one");
        harvested.Subject.Should().Be("fix: A returned the wrong constant");
        harvested.DiffText.Should().Contain("--- a/src/Policy.cs").And.Contain("+++ b/src/Policy.cs");

        FixDiff.Parse(harvested.DiffText).Ok().Files.Should().ContainSingle(f => f.OldPath == "src/Policy.cs");
    }

    [Fact]
    public async Task A_short_ref_resolves_to_the_full_sha()
    {
        await _repo.InitAsync(("a.txt", "one\n"), commit: ("first", "2026-05-01T12:00:00"));
        var fixSha = await _repo.CommitAsync(("a.txt", "two\n"), commit: ("fix: two", "2026-06-01T12:00:00"));

        var harvested = (await FixHarvest.ReadAsync(_repo.Root, fixSha[..10], Timeout, Ct)).Ok();

        harvested.Fix.Value.Should().Be(fixSha, "the record must pin the full sha, whatever shorthand the operator typed");
    }

    [Fact]
    public async Task A_root_commit_is_refused_there_is_no_buggy_tree_before_it()
    {
        var rootSha = await _repo.InitAsync(("a.txt", "one\n"), commit: ("first", "2026-05-01T12:00:00"));

        (await FixHarvest.ReadAsync(_repo.Root, rootSha, Timeout, Ct))
            .Reason().Should().Contain("parent");
    }

    [Fact]
    public async Task A_merge_commit_is_refused_by_name()
    {
        await _repo.InitAsync(("a.txt", "one\n"), commit: ("first", "2026-05-01T12:00:00"));
        await _repo.GitAsync("checkout", "-q", "-b", "side");
        await _repo.CommitAsync(("b.txt", "side\n"), commit: ("side change", "2026-05-02T12:00:00"));
        await _repo.GitAsync("checkout", "-q", "-");
        await _repo.CommitAsync(("c.txt", "main\n"), commit: ("main change", "2026-05-03T12:00:00"));
        await _repo.GitAsync("merge", "--no-ff", "-q", "-m", "merge side", "side");
        var mergeSha = (await _repo.GitAsync("rev-parse", "HEAD")).Trim();

        (await FixHarvest.ReadAsync(_repo.Root, mergeSha, Timeout, Ct))
            .Reason().Should().Contain("merge");
    }

    [Fact]
    public async Task An_unknown_ref_is_a_named_refusal_not_a_crash()
    {
        await _repo.InitAsync(("a.txt", "one\n"), commit: ("first", "2026-05-01T12:00:00"));

        (await FixHarvest.ReadAsync(_repo.Root, new string('f', 40), Timeout, Ct))
            .Reason().Should().Contain("resolve");
    }

    public void Dispose() => _repo.Dispose();
}
