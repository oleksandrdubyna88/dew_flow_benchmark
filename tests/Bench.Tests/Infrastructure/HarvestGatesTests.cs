using Bench.Domain.Targets;
using Bench.Infrastructure.Git;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The two harvest gates over a real repository and real processes — git itself standing in
/// for the build and the test commands, so the whole protocol (scratch worktree at base, the fix's
/// tests materialised, red, move to fix, green, cleanup) is exercised without a dotnet build.
/// <para>
/// The "test" is <c>git grep -q FIXED -- src/Policy.cs</c>: it exits non-zero at base (the marker is
/// not there — the bug is live) and zero at the fix. A fake that agreed with our assumptions about
/// worktrees would prove nothing; these run the real thing.
/// </para></summary>
public sealed class HarvestGatesTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private static readonly GateCommand Build = new("git", ["--version"]);

    private static readonly GateCommand GrepFixed = new("git", ["grep", "-q", "FIXED", "--", "src/Policy.cs"]);

    private readonly DatedGitRepo _repo;

    public HarvestGatesTests() => _repo = new DatedGitRepo(TestContext.Current.CancellationToken);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_live_bug_with_a_working_fix_passes_both_gates()
    {
        var (baseSha, fixSha) = await FixturePairAsync();

        var report = (await HarvestGates.RunAsync(
            _repo.Root, Sha(baseSha), Sha(fixSha), ["tests/PolicyTests.cs"], Build, GrepFixed, Timeout, Ct)).Ok();

        report.RedAtBase.Should().BeTrue("the marker is absent at base, so the test command fails there — the bug is live");
        report.GreenWithFix.Should().BeTrue();
        report.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task A_bug_that_does_not_reproduce_at_base_fails_the_red_gate_by_name()
    {
        var (baseSha, fixSha) = await FixturePairAsync();

        // Grepping for OLD finds it at base — the "test" passes on the buggy tree, which is exactly
        // the already-fixed-on-HEAD trap the gate exists to catch.
        var passesAtBase = new GateCommand("git", ["grep", "-q", "OLD", "--", "src/Policy.cs"]);

        var report = (await HarvestGates.RunAsync(
            _repo.Root, Sha(baseSha), Sha(fixSha), ["tests/PolicyTests.cs"], Build, passesAtBase, Timeout, Ct)).Ok();

        report.RedAtBase.Should().BeFalse();
        report.Passed.Should().BeFalse();
        report.RedDetail.Should().Contain("does not reproduce");
    }

    [Fact]
    public async Task A_fix_with_no_test_files_cannot_be_gated_and_says_why()
    {
        var (baseSha, fixSha) = await FixturePairAsync();

        (await HarvestGates.RunAsync(_repo.Root, Sha(baseSha), Sha(fixSha), [], Build, GrepFixed, Timeout, Ct))
            .Reason().Should().Contain("no test file");
    }

    [Fact]
    public async Task The_scratch_worktree_is_removed_on_the_way_out()
    {
        var (baseSha, fixSha) = await FixturePairAsync();

        await HarvestGates.RunAsync(
            _repo.Root, Sha(baseSha), Sha(fixSha), ["tests/PolicyTests.cs"], Build, GrepFixed, Timeout, Ct);

        (await _repo.GitAsync("worktree", "list")).Trim().Split('\n')
            .Should().HaveCount(1, "only the repository's own tree may remain — a leaked worktree is disk litter with a registry entry");
    }

    /// <summary>Base: the bug (OLD, no marker). Fix: the cure (FIXED) landing WITH its test — one
    /// commit, the shape a real merged fix has.</summary>
    private async Task<(string BaseSha, string FixSha)> FixturePairAsync()
    {
        var baseSha = await _repo.InitAsync(
            ("src/Policy.cs", "class Policy { /* OLD */ }\n"), ("seed", "2026-05-01T12:00:00"));
        var fixSha = await _repo.CommitManyAsync(
            [
                ("src/Policy.cs", "class Policy { /* FIXED */ }\n"),
                ("tests/PolicyTests.cs", "class PolicyTests { /* expects FIXED */ }\n"),
            ],
            ("fix: the marker", "2026-08-11T09:30:00"));

        return (baseSha, fixSha);
    }

    private static CommitSha Sha(string value) => CommitSha.Parse(value).Ok();

    public void Dispose() => _repo.Dispose();
}
