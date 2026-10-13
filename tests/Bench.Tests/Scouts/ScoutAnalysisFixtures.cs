using Bench.Domain.Probes;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;
using Bench.Domain.Trace;
using Bench.Tests.Infrastructure;

namespace Bench.Tests.Scouts;

/// <summary>Hand-built settled cells for the analysis and report tests — every number set by the test, so each expected value can be computed
/// by hand in a comment beside it.</summary>
internal static class ScoutAnalysisFixtures
{
    private static int _slot;

    /// <summary>One latest-settled cell. <paramref name="verdicts"/> are the assessed claims of its ONE scout (parsed = verdicts + <paramref name="unassessed"/>).</summary>
    public static ScoutCellEvidence Cell(
        string task, ScoutArmId arm, int repeat = 1, string repository = "org/repo-a", long found = 1, long total = 2,
        ScoutClaimVerdict[]? verdicts = null, ScoutCellOutcome outcome = ScoutCellOutcome.Scored, int unassessed = 0, bool silent = false,
        long teamMs = 5_000, long scoutMs = 1_000, decimal teamUsd = 0.01m)
    {
        var said = verdicts ?? [ScoutClaimVerdict.Right, ScoutClaimVerdict.Wrong];
        var scored = outcome == ScoutCellOutcome.Scored;
        var facts = new ScoutCellFacts(
            scored ? CapturedCount.Number(found) : CapturedCount.Unavailable("not scored"),
            scored ? CapturedCount.Number(total) : CapturedCount.Unavailable("not scored"),
            CapturedCount.Number(teamMs), CapturedCount.Number(20_000), CapturedUsd.Amount(teamUsd * 3), CapturedUsd.Amount(teamUsd * 3))
        {
            TeamTokens = CapturedTokens.Of(new TokenUsage(10, 100, 1_000, 200, 0)),
            TeamUsd = CapturedUsd.Amount(teamUsd),
        };
        var reason = outcome switch
        {
            ScoutCellOutcome.Void => ScoutReason.WrongCallCount,
            ScoutCellOutcome.Stopped => ScoutReason.CostMismatch,
            ScoutCellOutcome.Aborted => ScoutReason.SpendLimit,
            _ => ScoutReason.None,
        };
        var cell = new ScoutCell(
            Guid.CreateVersion7(), Guid.Empty, ScoutTaskId.Parse(task).Ok(), arm, repeat, 1, Interlocked.Increment(ref _slot), 0,
            Claimable.Stored(CellState.Settled, 1, WorkerIdentity.Nobody, ScoutStoreFixtures.Noon), ScoutStoreFixtures.Pin(), outcome, reason, facts);
        var scout = ScoutStoreFixtures.Scout(1) with
        {
            DurationMs = CapturedCount.Number(scoutMs),
            Silent = silent,
            ClaimsParsed = silent ? 0 : said.Length + unassessed,
            ClaimsUnparseable = 0,
            KeyFiles = ["src/A.cs", "src/B.cs", "src/a.cs"],
        };

        return new ScoutCellEvidence(cell, repository, [scout], silent ? [] : said);
    }

    public static ScoutClaimVerdict[] Verdicts(int wrong, int right, int unresolved = 0) =>
        [.. Enumerable.Repeat(ScoutClaimVerdict.Wrong, wrong), .. Enumerable.Repeat(ScoutClaimVerdict.Right, right), .. Enumerable.Repeat(ScoutClaimVerdict.Unresolved, unresolved)];
}
