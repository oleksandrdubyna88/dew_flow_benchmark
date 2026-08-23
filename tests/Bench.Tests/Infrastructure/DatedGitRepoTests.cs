using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The fixture's own guarantees, because two of them cost three days of red CI.
/// <para>
/// Both failures had the same shape — a test that passed on every developer machine and failed on the
/// runner — and both came from the fixture depending on something ambient instead of establishing it.
/// </para></summary>
public sealed class DatedGitRepoTests
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TWO_fixtures_with_identical_content_do_not_produce_the_same_commit()
    {
        using var first = new DatedGitRepo(Ct);
        using var second = new DatedGitRepo(Ct);

        var a = await first.InitAsync(("src/Policy.cs", "class Policy { }\n"), ("seed", "2026-05-01T12:00:00"));
        var b = await second.InitAsync(("src/Policy.cs", "class Policy { }\n"), ("seed", "2026-05-01T12:00:00"));

        // Four test files build a byte-identical fixture, and `--date` pins only the AUTHOR date — so the
        // one thing separating their shas was a committer timestamp with one-second resolution. Two in the
        // same second collided, both derived the same `fix-<sha12>` question id, and the second insert into
        // the shared bank was refused: "the question id is already in the bank". It only ever bit on CI,
        // where the tests run closer together.
        a.Should().NotBe(b, "a fixture's commits must be unique to it, not to the second it ran in");
    }

    [Fact]
    public async Task A_fixture_commits_WITHOUT_a_global_git_identity_on_the_machine()
    {
        using var repo = new DatedGitRepo(Ct);
        var sha = await repo.InitAsync(("src/A.cs", "class A { }\n"), ("seed", "2026-05-01T12:00:00"));

        // The identity is configured ON the repository, so every verb has one — not just `commit`, which
        // used to carry it on -c flags. `merge` had none, and the runner answered "Committer identity
        // unknown" while every developer machine sailed through on its global config.
        var identity = await repo.GitAsync("config", "user.email");

        sha.Should().NotBeEmpty();
        identity.Trim().Should().StartWith("bench+").And.EndWith("@test");
    }

    [Fact]
    public async Task A_MERGE_commit_is_something_the_fixture_can_actually_make()
    {
        using var repo = new DatedGitRepo(Ct);
        await repo.InitAsync(("src/A.cs", "class A { }\n"), ("seed", "2026-05-01T12:00:00"));

        await repo.GitAsync("checkout", "-q", "-b", "side");
        await repo.CommitAsync(("src/B.cs", "class B { }\n"), ("side work", "2026-05-02T12:00:00"));
        await repo.GitAsync("checkout", "-q", "-");
        await repo.CommitAsync(("src/C.cs", "class C { }\n"), ("main work", "2026-05-03T12:00:00"));
        await repo.GitAsync("merge", "--no-ff", "-q", "-m", "merge side", "side");

        // The verb that had no identity. Asserted directly rather than only through the harvest tests that
        // happen to use it, so the fixture's own capability has its own red.
        var parents = await repo.GitAsync("rev-list", "--parents", "-n", "1", "HEAD");

        parents.Trim().Split(' ').Should().HaveCount(3, "a merge commit has two parents beside its own sha");
    }
}
