using Bench.Domain.Runs;

namespace Bench.Domain.Gate;

/// <summary>What kind of end a gate cell reached, flattened for storage. <c>None</c> is the honest state of
/// a cell that has not finished. Whether the run it produced was VALID is a fact about the run
/// (<c>GateRunFacts</c>), not about the cell: a cell that completed its product session and got a
/// <c>call_human</c> back is <see cref="Completed"/> here and invalid there.</summary>
public enum GateCellOutcomeKind
{
    None,

    /// <summary>The product session ran to its end and a run record was written.</summary>
    Completed,

    /// <summary>The session could not be driven to an end — a process that died, a pipe that broke, or a
    /// cell abandoned after its attempts ran out. The detail says which.</summary>
    Failed,
}

/// <summary>One unit of work in a gate run: a task, a reviewer, a repeat, and the slot and position it runs in.
/// <para>
/// Composes <see cref="Claimable"/> exactly as <see cref="RunCell"/> does, so the claim/settle/sweep rules —
/// including the three-attempt abandonment — are the same functions rather than a second reading of them.
/// What is its own here is the <see cref="Pin"/>: a gate cell is claimed UNDER a product pin, stored on the
/// cell at that moment, because a campaign may be allowed to change product mid-way and every cell must then
/// say which bytes it measured.
/// </para></summary>
public sealed record GateCell(
    Guid Id,
    Guid RunId,
    GateTaskId Task,
    GateReviewerId Reviewer,
    int Repeat,
    int Slot,
    int Position,
    Claimable Claim,
    ProductPin Pin,
    GateCellOutcomeKind OutcomeKind,
    string OutcomeDetail)
{
    public static GateCell Pending(Guid runId, GateMatrixCell cell) => new(
        Guid.CreateVersion7(),
        runId,
        cell.Task,
        cell.Reviewer,
        cell.Repeat,
        cell.Slot,
        cell.Position,
        Claimable.Fresh,
        ProductPin.None,
        GateCellOutcomeKind.None,
        OutcomeDetail: string.Empty);

    public CellState State => Claim.State;

    public int Attempts => Claim.Attempts;

    public WorkerIdentity Owner => Claim.Owner;

    public DateTimeOffset ClaimedAt => Claim.ClaimedAt;

    public bool IsTerminal => Claim.IsTerminal;

    public string Subject => $"gate cell {Id}";
}

/// <summary>The claim/settle/sweep transitions over a <see cref="GateCell"/>, as pure functions — the same
/// shape as <see cref="CellLifecycle"/>, over the same <see cref="Claimable"/>. What is decided here is only
/// what an abandonment means for the gate cell's outcome columns, and that a claim carries a pin.</summary>
public static class GateCellLifecycle
{
    /// <summary>Takes the cell for <paramref name="owner"/> under <paramref name="pin"/>. Refused without a
    /// pin: a cell measured under unknown bytes is a number about nothing.</summary>
    public static Outcome<GateCell> Claim(GateCell cell, WorkerIdentity owner, DateTimeOffset now, ProductPin pin)
    {
        if (!pin.IsPinned)
        {
            return Outcome<GateCell>.Failure(
                $"{cell.Subject} is claimed under a product pin, and none was given — the pin is stored per cell at claim time");
        }

        return Claimable.Claim(cell.Claim, owner, now, cell.Subject).Match(
            claim => Outcome<GateCell>.Success(cell with { Claim = claim, Pin = pin }),
            Outcome<GateCell>.Failure);
    }

    public static Outcome<GateCell> Settle(GateCell cell, GateCellOutcomeKind kind, string detail)
    {
        if (kind == GateCellOutcomeKind.None)
        {
            return Outcome<GateCell>.Failure($"{cell.Subject} cannot settle with no outcome — say Completed or Failed");
        }

        return Claimable.Settle(cell.Claim, cell.Subject).Match(
            claim => Outcome<GateCell>.Success(cell with { Claim = claim, OutcomeKind = kind, OutcomeDetail = detail }),
            Outcome<GateCell>.Failure);
    }

    /// <summary>The sweep decision for one cell. The pin stays as it was: the next claim overwrites it with
    /// whatever the campaign is pinned to THEN, which is how a run allowed to change product records both.</summary>
    public static GateCell Reclaim(GateCell cell) =>
        Claimable.Reclaim(cell.Claim) switch
        {
            ReclaimDecision.Requeued requeued => cell with { Claim = requeued.Claim },
            ReclaimDecision.Abandoned abandoned => cell with
            {
                Claim = abandoned.Claim,
                OutcomeKind = GateCellOutcomeKind.Failed,
                OutcomeDetail = abandoned.Detail,
            },
            _ => cell,
        };

    public static bool IsStale(GateCell cell, DateTimeOffset now, TimeSpan staleAfter) =>
        Claimable.IsStale(cell.Claim, now, staleAfter);
}
