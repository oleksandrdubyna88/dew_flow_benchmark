namespace Bench.Domain.Runs;

public enum CellState
{
    /// <summary>Written before any work starts, waiting for a worker.</summary>
    Pending,

    /// <summary>Owned by a worker right now. A cell left here by a dead host is what the sweep repairs.</summary>
    Claimed,

    /// <summary>Finished, with an outcome — which includes "hit a ceiling" and "crashed".</summary>
    Settled,

    /// <summary>Handed back too many times. Deliberately terminal: see <see cref="CellLifecycle.MaxAttempts"/>.</summary>
    Abandoned,
}

/// <summary>What kind of end a cell reached, flattened for storage. `None` is the honest state of a cell
/// that has not finished — not a null, and not a zero that reads as "completed with nothing".</summary>
public enum LegOutcomeKind
{
    None,
    Completed,
    CapExceeded,
    Crashed,
}

/// <summary>One unit of work in a run: a question, a repeat, a leg, and the position it runs in.
/// <para>
/// Persisted <b>before</b> any work begins. That ordering is the whole durability story: a queue that is
/// enqueued first and written second loses everything a crash interrupts, and loses it invisibly — the
/// system this replaces committed a whole pass in a single save, so an interrupted run left no trace that
/// it had ever been requested.
/// </para></summary>
public sealed record RunCell(
    Guid Id,
    Guid RunId,
    string QuestionId,
    int Repeat,
    string Leg,
    /// <summary>The two axes of <paramref name="Leg"/>, carried BESIDE its canonical form rather than
    /// parsed back out of it. Everything downstream — a judge attributing a verdict to a subject, a
    /// discrimination reading grouping by model tier — needs these as values; recovering them by splitting
    /// a composite string is the exact trade this project refuses in its storage, and it would be no better
    /// here for being in memory.</summary>
    string SubjectModelId,
    string LaneName,
    int Position,
    CellState State,
    int Attempts,
    string Owner,
    DateTimeOffset ClaimedAt,
    LegOutcomeKind OutcomeKind,
    string OutcomeDetail)
{
    public static RunCell Pending(Guid runId, MatrixCell cell) => new(
        Guid.CreateVersion7(),
        runId,
        cell.QuestionId,
        cell.Repeat,
        cell.Leg.Canonical,
        cell.Leg.Subject.Model.Id,
        cell.Leg.Lane.Name,
        cell.Position,
        CellState.Pending,
        Attempts: 0,
        Owner: string.Empty,
        ClaimedAt: default,
        LegOutcomeKind.None,
        OutcomeDetail: string.Empty);

    public bool IsTerminal => State is CellState.Settled or CellState.Abandoned;
}

/// <summary>The claim/settle/sweep transitions, as pure functions.
/// <para>
/// They are pure so the rules can be tested without a database, and so the database's only job is the one
/// thing only it can do: make a claim atomic. Mixing the two is how a state machine ends up asserted in
/// prose and enforced nowhere.
/// </para></summary>
public static class CellLifecycle
{
    /// <summary>How many times a cell may be handed back before it is abandoned.
    /// <para>
    /// A sweep that re-queues forever is worse than one that gives up: a cell that kills its host is a
    /// cell that will kill the next host too, and an unbounded sweep turns that into a loop that survives
    /// reboots. Upstream this was learned by parking an item on its second attempt.
    /// </para></summary>
    public const int MaxAttempts = 3;

    public static Outcome<RunCell> Claim(RunCell cell, string owner, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            return Outcome<RunCell>.Failure("a claim needs an owner — an unowned claim can never be swept correctly");
        }

        return cell.State == CellState.Pending
            ? Outcome<RunCell>.Success(cell with
            {
                State = CellState.Claimed,
                Owner = owner,
                ClaimedAt = now,
                Attempts = cell.Attempts + 1,
            })
            : Outcome<RunCell>.Failure($"cell {cell.Id} is {cell.State}, not Pending");
    }

    public static Outcome<RunCell> Settle(RunCell cell, LegOutcome outcome)
    {
        var (kind, detail) = LegOutcomeCodec.Encode(outcome);

        return cell.State == CellState.Claimed
            ? Outcome<RunCell>.Success(cell with
            {
                State = CellState.Settled,
                OutcomeKind = kind,
                OutcomeDetail = detail,
            })
            : Outcome<RunCell>.Failure($"cell {cell.Id} is {cell.State}, not Claimed — only a claimed cell can settle");
    }

    /// <summary>The sweep decision for one cell. Not an <c>Outcome</c>: a sweep over a thousand cells asks
    /// this of every one of them, and "nothing to do here" is the normal answer rather than a failure.</summary>
    public static RunCell Reclaim(RunCell cell) =>
        cell.State != CellState.Claimed
            ? cell
            : cell.Attempts >= MaxAttempts
                ? cell with
                {
                    State = CellState.Abandoned,
                    Owner = string.Empty,
                    OutcomeKind = LegOutcomeKind.Crashed,
                    OutcomeDetail = $"abandoned after {cell.Attempts} attempts — a cell that kills its host will kill the next one",
                }
                : cell with { State = CellState.Pending, Owner = string.Empty, ClaimedAt = default };

    /// <summary>Whether a claim has gone quiet long enough to be treated as a dead host's.</summary>
    public static bool IsStale(RunCell cell, DateTimeOffset now, TimeSpan staleAfter) =>
        cell.State == CellState.Claimed && now - cell.ClaimedAt >= staleAfter;
}

/// <summary>Flattening <see cref="LegOutcome"/> for storage. One named function, so that adding a case to
/// the union breaks HERE — at a compiler error — rather than silently storing a new outcome as an old one.
/// <para>
/// <b>There is deliberately no decoder.</b> <see cref="LegOutcome.CapExceeded"/> carries four structured
/// fields and the stored detail is a sentence for a human to read; parsing that sentence back into
/// structure would be a lie that works until someone edits the wording. When a report needs "how many
/// cells hit a COST ceiling against a TIME ceiling", the answer is to add those columns deliberately, not
/// to reverse a display string.
/// </para></summary>
public static class LegOutcomeCodec
{
    public static (LegOutcomeKind Kind, string Detail) Encode(LegOutcome outcome) =>
        outcome switch
        {
            LegOutcome.Completed => (LegOutcomeKind.Completed, string.Empty),
            LegOutcome.CapExceeded c => (LegOutcomeKind.CapExceeded, $"{c.Kind}/{c.Scope}: reached {c.Reached} of {c.Limit}"),
            LegOutcome.Crashed c => (LegOutcomeKind.Crashed, c.Reason),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "unknown leg outcome"),
        };
}
