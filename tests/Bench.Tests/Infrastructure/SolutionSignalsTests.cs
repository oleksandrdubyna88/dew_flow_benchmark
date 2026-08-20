using Bench.Domain.Targets;
using Bench.Infrastructure.Git;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The mechanical signals over a solver's diff (todo/PLAN_investigate_vs_implement.md step 8):
/// a scratch worktree at base, the diff applied, the fix's own tests as the HIDDEN tests, one build,
/// one test run — over a real repository and real processes, git standing in for both commands.
/// A solution that does not apply or build is a REPORT with the reason, never an instrument failure:
/// BuildFailed being distinct from wrong is the plan's own rule.</summary>
public sealed class SolutionSignalsTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private static readonly GateCommand Build = new("git", ["--version"]);

    private static readonly GateCommand GrepFixed = new("git", ["grep", "-q", "FIXED", "--", "src/Policy.cs"]);

    private readonly DatedGitRepo _repo;

    public SolutionSignalsTests() => _repo = new DatedGitRepo(TestContext.Current.CancellationToken);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_correct_solution_passes_all_four_signals()
    {
        var (baseSha, fixSha) = await FixtureAsync();
        var solverDiff = await _repo.GitAsync("diff", baseSha, fixSha, "--", "src");

        var report = (await SolutionSignals.RunAsync(
            _repo.Root, Sha(baseSha), Sha(fixSha), solverDiff,
            ["src/Policy.cs"], ["tests/PolicyTests.cs"], Build, GrepFixed, Timeout, Ct)).Ok();

        report.Passed.Should().BeTrue(report.Describe);
        report.Metrics().Should().OnlyContain(m => m.Value == "true");
    }

    [Fact]
    public async Task A_solution_in_the_wrong_file_fails_the_right_file_signal_and_the_hidden_tests()
    {
        var (baseSha, fixSha) = await FixtureAsync();
        await _repo.GitAsync("checkout", "-q", "-b", "wrong", baseSha);
        await _repo.CommitAsync(("src2/Elsewhere.cs", "class Elsewhere { /* FIXED-ish */ }\n"), ("wrong place", "2026-08-12T10:00:00"));
        var wrongDiff = await _repo.GitAsync("diff", baseSha, "wrong", "--", "src2");
        await _repo.GitAsync("checkout", "-q", "-");

        var report = (await SolutionSignals.RunAsync(
            _repo.Root, Sha(baseSha), Sha(fixSha), wrongDiff,
            ["src/Policy.cs"], ["tests/PolicyTests.cs"], Build, GrepFixed, Timeout, Ct)).Ok();

        report.Applies.Should().BeTrue("the diff is well-formed — it is merely aimed wrong");
        report.RightFiles.Should().BeFalse();
        report.HiddenTestsGreen.Should().BeFalse("the marker never reached src/Policy.cs");
        report.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task A_diff_that_does_not_apply_is_a_verdict_not_an_instrument_failure()
    {
        var (baseSha, fixSha) = await FixtureAsync();

        var report = (await SolutionSignals.RunAsync(
            _repo.Root, Sha(baseSha), Sha(fixSha), "this is not a diff at all",
            ["src/Policy.cs"], ["tests/PolicyTests.cs"], Build, GrepFixed, Timeout, Ct)).Ok();

        report.Applies.Should().BeFalse();
        report.Passed.Should().BeFalse();
        report.TestsDetail.Should().Contain("not run", "a phase that never ran must not read as one that failed");
    }

    [Fact]
    public async Task The_scratch_worktree_is_returned_on_every_path_out()
    {
        var (baseSha, fixSha) = await FixtureAsync();

        await SolutionSignals.RunAsync(
            _repo.Root, Sha(baseSha), Sha(fixSha), "garbage",
            ["src/Policy.cs"], ["tests/PolicyTests.cs"], Build, GrepFixed, Timeout, Ct);

        (await _repo.GitAsync("worktree", "list")).Trim().Split('\n').Should().HaveCount(1);
    }

    private async Task<(string BaseSha, string FixSha)> FixtureAsync()
    {
        var baseSha = await _repo.InitAsync(
            ("src/Policy.cs", "class Policy { /* OLD */ }\n"), ("seed", "2026-05-01T12:00:00"));
        var fixSha = await _repo.CommitManyAsync(
            [
                ("src/Policy.cs", "class Policy { /* FIXED */ }\n"),
                ("tests/PolicyTests.cs", "class PolicyTests { }\n"),
            ],
            ("fix: the marker", "2026-08-11T09:30:00"));

        return (baseSha, fixSha);
    }

    private static CommitSha Sha(string value) => CommitSha.Parse(value).Ok();

    public void Dispose() => _repo.Dispose();
}
