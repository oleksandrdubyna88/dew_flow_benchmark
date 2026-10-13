using Bench.Domain.Gate;
using Bench.Domain.Probes;
using Bench.Domain.Runs;
using Bench.Domain.Trace;

namespace Bench.Domain.Scouts;

/// <summary>How a settled cell ended.</summary>
public enum ScoutCellOutcome
{
    /// <summary>Not settled yet.</summary>
    None,

    /// <summary>The read-back passed; the cell counts.</summary>
    Scored,

    /// <summary>The read-back voided it — re-run as a next generation.</summary>
    Void,

    /// <summary>A campaign stop (path audit, cost check, pin) — unscored, and the run stopped.</summary>
    Stopped,

    /// <summary><i>aborted:spend</i> — a spend limit or a quota, never scored.</summary>
    Aborted,
}

/// <summary>The numbers a settled cell carries. Every one may be absent, and absent is never zero (<see cref="CapturedCount"/>,
/// <see cref="CapturedUsd"/>): a void cell has no recall, a cell whose result did not survive has no reported cost.</summary>
public sealed record ScoutCellFacts(
    CapturedCount RecallFound,
    CapturedCount RecallTotal,
    CapturedCount TeamMs,
    CapturedCount SessionMs,
    CapturedUsd ComputedUsd,
    CapturedUsd ReportedUsd)
{
    /// <summary>The one "not captured" a team cost reads as — in memory and read back from a row alike, so a settled cell round-trips exactly.
    /// Declared BEFORE <see cref="None"/>: static initialisers run in textual order, and <see cref="None"/>'s construction reads it.</summary>
    public static CapturedUsd TeamUsdNotCaptured { get; } = CapturedUsd.Unavailable("the team's cost was not captured");

    public static ScoutCellFacts None { get; } = new(
        CapturedCount.Unavailable("not settled"), CapturedCount.Unavailable("not settled"), CapturedCount.Unavailable("not settled"),
        CapturedCount.Unavailable("not settled"), CapturedUsd.Unavailable("not settled"), CapturedUsd.Unavailable("not settled"));

    /// <summary>The scout TEAM's tokens — exact per team (<see cref="ScoutTokens"/>: each scout model's <c>modelUsage</c> line minus the main
    /// session's own), the main session and the CLI's internal calls excluded.</summary>
    public CapturedTokens TeamTokens { get; init; } = CapturedTokens.NotCaptured;

    /// <summary>The scout TEAM's cost — <see cref="TeamTokens"/> at the run's price table (<see cref="CellCost.ScoutsUsd"/>): what "which team
    /// is cheaper" is answered with (§3.6).</summary>
    public CapturedUsd TeamUsd { get; init; } = TeamUsdNotCaptured;
}

/// <summary>Token counts that may not have been captured — never a zero for "absent".</summary>
public sealed record CapturedTokens(bool WasCaptured, TokenUsage Value)
{
    public static CapturedTokens NotCaptured { get; } = new(false, TokenUsage.Zero);

    public static CapturedTokens Of(TokenUsage value) => new(true, value);
}

/// <summary>How a claimed cell ends: the outcome, its reason (empty only for a scored cell), and the facts.</summary>
public sealed record ScoutSettlement(ScoutCellOutcome Outcome, ScoutReason Reason, ScoutCellFacts Facts);

/// <summary>One unit of a scout run: a task, an arm, a repeat, a GENERATION, and its slot and position in the seeded schedule.
/// Composes <see cref="Claimable"/> and is claimed under a <see cref="ProductPin"/> exactly as <see cref="Probes.ProbeCell"/> does —
/// one claim/settle/sweep lifecycle with the gate and the probes.</summary>
public sealed record ScoutCell(
    Guid Id,
    Guid RunId,
    ScoutTaskId Task,
    ScoutArmId Arm,
    int Repeat,
    int Generation,
    int Slot,
    int Position,
    Claimable Claim,
    ProductPin Pin,
    ScoutCellOutcome Outcome,
    ScoutReason Reason,
    ScoutCellFacts Facts)
{
    public const int FirstGeneration = 1;

    /// <summary>A cell before anyone claimed it; the id is the caller's, so the same schedule can always produce the same cells.</summary>
    public static ScoutCell Pending(Guid id, Guid runId, ScheduledCell cell) => new(
        id, runId, cell.Task, cell.Arm, cell.Repeat, FirstGeneration, cell.Slot, cell.Position,
        Claimable.Fresh, ProductPin.None, ScoutCellOutcome.None, ScoutReason.None, ScoutCellFacts.None);

    public CellState State => Claim.State;

    public int Attempts => Claim.Attempts;

    public WorkerIdentity Owner => Claim.Owner;

    public bool IsTerminal => Claim.IsTerminal;

    public string Label => $"scout cell {Id}";

    /// <summary>The identity a generation belongs to — what a re-run appends a generation of.</summary>
    public (Guid RunId, string Task, ScoutArmId Arm, int Repeat) Lineage => (RunId, Task.Value, Arm, Repeat);
}

/// <summary>The claim/settle/sweep transitions over a <see cref="ScoutCell"/>, as pure functions — <see cref="Probes.ProbeCellLifecycle"/>'s
/// shape over the same <see cref="Claimable"/>.</summary>
public static class ScoutCellLifecycle
{
    public static Outcome<ScoutCell> Claim(ScoutCell cell, WorkerIdentity owner, DateTimeOffset now, ProductPin pin) =>
        pin.IsPinned
            ? Claimable.Claim(cell.Claim, owner, now, cell.Label).Match(
                claim => Outcome<ScoutCell>.Success(cell with { Claim = claim, Pin = pin }),
                Outcome<ScoutCell>.Failure)
            : Outcome<ScoutCell>.Failure($"{cell.Label} is claimed under a product pin, and none was given — the pin is stored per cell at claim time");

    /// <summary>Records how a claimed cell ended. The outcome and the reason must agree: a scored cell has no reason, a void cell a
    /// VOID reason, a stopped cell a STOP reason, an aborted cell <see cref="ScoutReason.SpendLimit"/>.</summary>
    public static Outcome<ScoutCell> Settle(ScoutCell cell, ScoutSettlement settlement)
    {
        var refusal = Disagreement(settlement);

        return refusal.Length > 0
            ? Outcome<ScoutCell>.Failure($"{cell.Label}: {refusal}")
            : Claimable.Settle(cell.Claim, cell.Label).Match(
                claim => Outcome<ScoutCell>.Success(cell with { Claim = claim, Outcome = settlement.Outcome, Reason = settlement.Reason, Facts = settlement.Facts }),
                Outcome<ScoutCell>.Failure);
    }

    public static string Disagreement(ScoutSettlement settlement) =>
        (settlement.Outcome == ScoutCellOutcome.None, Accepts.TryGetValue(settlement.Outcome, out var accepts) && accepts(settlement.Reason)) switch
        {
            (true, _) => "a settlement says how the cell ended — scored, void, stopped or aborted",
            (_, true) => string.Empty,
            _ => $"a {settlement.Outcome} cell cannot carry the reason {settlement.Reason}",
        };

    /// <summary>The reasons each outcome may carry: none for a scored cell, a VOID word for a void one, a STOP word for a stopped one,
    /// <see cref="ScoutReason.SpendLimit"/> for an aborted one.</summary>
    private static readonly IReadOnlyDictionary<ScoutCellOutcome, Func<ScoutReason, bool>> Accepts = new Dictionary<ScoutCellOutcome, Func<ScoutReason, bool>>
    {
        [ScoutCellOutcome.Scored] = reason => reason == ScoutReason.None,
        [ScoutCellOutcome.Void] = ScoutReasons.IsVoid,
        [ScoutCellOutcome.Stopped] = ScoutReasons.IsStop,
        [ScoutCellOutcome.Aborted] = reason => reason == ScoutReason.SpendLimit,
    };

    public static ScoutCell Reclaim(ScoutCell cell) =>
        Claimable.Reclaim(cell.Claim) switch
        {
            ReclaimDecision.Requeued requeued => cell with { Claim = requeued.Claim, Reason = ScoutReason.None },
            ReclaimDecision.Abandoned abandoned => cell with { Claim = abandoned.Claim, Reason = ScoutReason.Abandoned },
            _ => cell,
        };

    /// <summary>The next generation of ONE lineage: a fresh Pending cell numbered max + 1 at the lineage's slot and position. Refused
    /// while any generation is still open, and for an empty or a mixed list.</summary>
    public static Outcome<ScoutCell> NextGeneration(IReadOnlyList<ScoutCell> generations, Guid newId)
    {
        var refusal = (generations.Count, generations.Select(g => g.Lineage).Distinct().Count(), generations.FirstOrDefault(g => !g.IsTerminal)) switch
        {
            (0, _, _) => "a next generation needs the lineage it follows — got no generation at all",
            (_, > 1, _) => "the cells span two lineages — a generation follows ONE (run, task, arm, repeat)",
            (_, _, { } open) => $"generation {open.Generation} of {open.Label} is {open.State} — it is still being measured; a re-run waits for it",
            _ => string.Empty,
        };

        return refusal.Length > 0
            ? Outcome<ScoutCell>.Failure(refusal)
            : Outcome<ScoutCell>.Success(generations.MaxBy(g => g.Generation)! with
            {
                Id = newId,
                Generation = ScoutGenerations.Highest(generations) + 1,
                Claim = Claimable.Fresh,
                Pin = ProductPin.None,
                Outcome = ScoutCellOutcome.None,
                Reason = ScoutReason.None,
                Facts = ScoutCellFacts.None,
            });
    }
}

/// <summary>The one reading of a lineage's generations a report uses: the highest SETTLED generation per (task, arm, repeat).</summary>
public static class ScoutGenerations
{
    public static IReadOnlyList<ScoutCell> LatestSettled(IEnumerable<ScoutCell> cells) =>
        [.. cells.Where(c => c.State == CellState.Settled)
            .GroupBy(c => c.Lineage)
            .Select(lineage => lineage.MaxBy(c => c.Generation)!)
            .OrderBy(c => c.Slot).ThenBy(c => c.Position)];

    public static int Highest(IEnumerable<ScoutCell> lineage) => lineage.Select(c => c.Generation).DefaultIfEmpty(0).Max();
}
