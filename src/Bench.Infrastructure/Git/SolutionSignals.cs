using Bench.Domain;
using Bench.Domain.Authoring;
using Bench.Domain.Runs;
using Bench.Domain.Targets;

namespace Bench.Infrastructure.Git;

/// <summary>What the mechanical signals observed about ONE solution — a solver's diff at the base tree
/// (todo/PLAN_investigate_vs_implement.md step 8; the signal table is PLAN_code_lane.md §5.1).
/// Signals 0–2 of the five: applies+compiles, lands in the right files, hidden tests green. The
/// teeth-proof (signal 3) waits for solver-authored tests, and the neighbour suite (signal 4) for a
/// per-task filter — both named open rather than silently absent.</summary>
public sealed record SolutionReport(
    bool Applies, string ApplyDetail,
    bool Builds, string BuildDetail,
    bool RightFiles, string RightFilesDetail,
    bool HiddenTestsGreen, string TestsDetail)
{
    public bool Passed => Applies && Builds && RightFiles && HiddenTestsGreen;

    public string Describe =>
        $"applies: {(Applies ? "yes" : "NO")}; builds: {(Builds ? "yes" : "NO")}; "
        + $"right files: {(RightFiles ? "yes" : "NO")}; hidden tests: {(HiddenTestsGreen ? "green" : "NOT GREEN")}";

    /// <summary>The signals as metric rows, one per signal — the shape every comparison reads.</summary>
    public IReadOnlyList<StoredMetric> Metrics() =>
    [
        StoredMetric.Boolean("Solution applies", Applies, ApplyDetail, failed: !Applies, Rating(Applies)),
        StoredMetric.Boolean("Solution builds", Builds, BuildDetail, failed: !Builds, Rating(Builds)),
        StoredMetric.Boolean("Solution touches a causal file", RightFiles, RightFilesDetail, failed: !RightFiles, Rating(RightFiles)),
        StoredMetric.Boolean("Hidden tests green", HiddenTestsGreen, TestsDetail, failed: !HiddenTestsGreen, Rating(HiddenTestsGreen)),
    ];

    private static string Rating(bool passed) => passed ? "Good" : "Unacceptable";
}

/// <summary>The attended executor for a produced solution: a scratch worktree at base, the solver's
/// diff applied, the fix's own tests materialised as the HIDDEN tests, one build, one test run.
/// <para>
/// ATTENDED-ONLY, exactly as the harvest gates are: this builds a tree that contains a model-authored
/// diff, which is arbitrary code execution by design, and the isolation assertions of
/// `PLAN_code_lane.md` §4.2 are what would make it schedulable. Until they pass, this runs while an
/// operator watches — which is also why nothing here is wired into `LegRunner`, whose queue IS the
/// scheduled shape.
/// </para>
/// <para>
/// A solution that does not apply, or does not build, is a REPORT with the reason — never an Outcome
/// failure: those are verdicts about the solution, and `BuildFailed` being distinct from `wrong` is
/// the plan's own rule. An Outcome failure here means the INSTRUMENT could not run at all.
/// </para></summary>
public static class SolutionSignals
{
    public static async Task<Outcome<SolutionReport>> RunAsync(
        string repositoryDir,
        CommitSha baseCommit,
        CommitSha fixCommit,
        string solverDiff,
        IReadOnlyList<string> causalPaths,
        IReadOnlyList<string> testPaths,
        GateCommand build,
        GateCommand test,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (testPaths.Count == 0)
        {
            return Outcome<SolutionReport>.Failure(
                "the task carries no hidden-test files — signal 2 would assert nothing, and a green light nobody earned is worse than none");
        }

        var deadline = new GateDeadline(timeout);
        var scratch = await ScratchWorktree.AddAsync(repositoryDir, baseCommit, deadline.Remaining(), cancellationToken);

        if (scratch is Outcome<string>.Fail cannotAdd)
        {
            return Outcome<SolutionReport>.Failure(cannotAdd.Reason);
        }

        var tree = ((Outcome<string>.Ok)scratch).Value;

        try
        {
            return await SignalsAsync(
                tree, fixCommit, solverDiff, causalPaths, testPaths, build, test, deadline, cancellationToken);
        }
        finally
        {
            await ScratchWorktree.RemoveAsync(repositoryDir, tree);
        }
    }

    private static async Task<Outcome<SolutionReport>> SignalsAsync(
        string tree,
        CommitSha fixCommit,
        string solverDiff,
        IReadOnlyList<string> causalPaths,
        IReadOnlyList<string> testPaths,
        GateCommand build,
        GateCommand test,
        GateDeadline deadline,
        CancellationToken cancellationToken)
    {
        var (rightFiles, rightDetail) = TouchesCause(solverDiff, causalPaths);
        var applied = await ApplyAsync(tree, solverDiff, deadline.Remaining(), cancellationToken);

        if (applied is not Outcome<string>.Ok)
        {
            var why = applied.Match(_ => string.Empty, reason => reason);

            return Outcome<SolutionReport>.Success(new SolutionReport(
                false, why,
                false, "not built: the diff did not apply",
                rightFiles, rightDetail,
                false, "not run: the diff did not apply"));
        }

        var testsIn = await GitCommand.RunAsync(
            tree, deadline.Remaining(), cancellationToken, ["checkout", fixCommit.Value, "--", .. testPaths]);

        if (testsIn is Outcome<string>.Fail noTests)
        {
            return Outcome<SolutionReport>.Failure(
                $"could not materialise the hidden tests: {noTests.Reason}");
        }

        var built = await GateProcess.RunAsync(tree, build, deadline.Remaining(), cancellationToken);

        if (built is Outcome<Process.ProcessResult>.Fail buildBroken)
        {
            return Outcome<SolutionReport>.Failure(buildBroken.Reason);
        }

        var buildResult = ((Outcome<Process.ProcessResult>.Ok)built).Value;

        if (!buildResult.Ok)
        {
            return Outcome<SolutionReport>.Success(new SolutionReport(
                true, "the diff applied cleanly",
                false, $"BuildFailed — distinct from wrong, per the plan: {GateProcess.Tail(buildResult.Output)}",
                rightFiles, rightDetail,
                false, "not run: the tree does not build"));
        }

        var tested = await GateProcess.RunAsync(tree, test, deadline.Remaining(), cancellationToken);

        return tested.Match(
            result => Outcome<SolutionReport>.Success(new SolutionReport(
                true, "the diff applied cleanly",
                true, "build green with the hidden tests in the tree",
                rightFiles, rightDetail,
                result.Ok,
                result.Ok ? "the hidden tests pass with the solution" : GateProcess.Tail(result.Output))),
            Outcome<SolutionReport>.Failure);
    }

    /// <summary>Signal 1, computed PURELY from the two diffs' file lists — no worktree needed, so it is
    /// reported even for a solution that never applied. ANY causal file touched counts in v1; the
    /// stricter span-level form is a tightening for later, named rather than implied.</summary>
    private static (bool Touches, string Detail) TouchesCause(string solverDiff, IReadOnlyList<string> causalPaths)
    {
        var parsed = FixDiff.Parse(solverDiff);

        if (parsed is not Outcome<FixDiff>.Ok(var diff))
        {
            return (false, "the solver's diff does not parse as a unified diff");
        }

        var touched = diff.Files.Where(f => !f.IsTest).Select(f => f.OldPath).ToList();
        var hit = touched.FirstOrDefault(t => causalPaths.Any(c => AnchorMatching.SamePath(t, c)));

        return hit is null
            ? (false, $"none of the {touched.Count} touched file(s) is causal — the fix landed somewhere else")
            : (true, $"touches {hit}");
    }

    private static async Task<Outcome<string>> ApplyAsync(
        string tree, string solverDiff, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var patch = Path.Combine(Path.GetTempPath(), $"bench-solution-{Guid.CreateVersion7():N}.patch");

        try
        {
            // A patch file must END in a newline or git refuses it whole as corrupt — measured, not
            // assumed. Captured process output loses the final newline, and a model-produced diff
            // frequently never had one; appending it changes no hunk.
            await File.WriteAllTextAsync(
                patch, solverDiff.EndsWith('\n') ? solverDiff : solverDiff + "\n", cancellationToken);

            var applied = await GitCommand.RunAsync(
                tree, timeout, cancellationToken, "apply", "--whitespace=nowarn", patch);

            return applied.Match(
                _ => Outcome<string>.Success("applied"),
                reason => Outcome<string>.Failure($"the diff does not apply at base: {GateProcess.Tail(reason)}"));
        }
        finally
        {
            try
            {
                File.Delete(patch);
            }
            catch (IOException)
            {
                // A leaked patch file in temp is a nuisance, not a failed measurement.
            }
        }
    }
}
