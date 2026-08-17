namespace Bench.Domain.Runs;

public enum BudgetKind
{
    CostUsd,
    Wall,
    Turns,
    Context,
}

public enum BudgetScope
{
    /// <summary>The whole phase — a generation pass, a retrieval pass, a judging pass.</summary>
    Phase,

    /// <summary>One question, inside a phase.</summary>
    Question,
}

/// <summary>A ceiling, and the runtime that confirmed it arrived.
/// <para>
/// <see cref="AcceptedBy"/> is not bookkeeping. Upstream, a context-compaction ceiling was configured,
/// believed, and reasoned from for a whole series — and it turned out to be a knob of the local tool
/// loop that reached none of the CLI arms at all, so a real degradation was attributed to a flooded
/// context window that had never happened. A budget nobody confirmed is a budget that does not exist,
/// and a run under one is marked rather than scored.
/// </para></summary>
public sealed record Budget(BudgetKind Kind, BudgetScope Scope, decimal Limit, string AcceptedBy)
{
    public static Budget Of(BudgetKind kind, BudgetScope scope, decimal limit) => new(kind, scope, limit, string.Empty);

    public bool IsVerified => AcceptedBy.Length > 0;

    public Budget ConfirmedBy(string runtime) => this with { AcceptedBy = runtime };

    public string Describe => $"{Kind}/{Scope} <= {Limit}" + (IsVerified ? $" (accepted by {AcceptedBy})" : " (UNVERIFIED)");
}

/// <summary>How one leg ended. A closed union so no caller can quietly treat a ceiling as a zero.
/// <para>
/// The distinction that matters is <see cref="CapExceeded"/> against <see cref="Crashed"/> against a
/// completed-but-wrong answer. All three used to be "the leg failed", which then either scored as a
/// zero — dragging an average toward a number nothing measured — or excluded a whole repeat.
/// </para></summary>
public abstract record LegOutcome
{
    private LegOutcome() { }

    /// <summary>Ran to completion. Whether the ANSWER was right is a separate question, judged later.</summary>
    public sealed record Completed : LegOutcome;

    /// <summary>Stopped because a ceiling was reached. Not an error, and not a wrong answer.</summary>
    public sealed record CapExceeded(BudgetKind Kind, BudgetScope Scope, decimal Limit, decimal Reached) : LegOutcome;

    /// <summary>The leg died. Something is broken in the harness or the runtime, not in the model.</summary>
    public sealed record Crashed(string Reason) : LegOutcome;

    /// <summary>The leg was never run, because something about its ARM could not be trusted: an engine that
    /// applied an axis differently from the way it was asked, a corpus that is not the recipe's, an index
    /// built from another tree.
    /// <para>
    /// A fourth state rather than a crash, and the distinction is the whole point. A crash says this harness
    /// or that runtime is broken and invites somebody to fix a bug; a block says the CONFIGURATION does not
    /// hold together and invites them to fix a variant or rebuild an index. Scored as a wrong answer it would
    /// be worse still: the subject never saw the question.
    /// </para></summary>
    public sealed record Blocked(string Reason) : LegOutcome;

    /// <summary>Whether this leg may enter a paired delta. A capped or crashed leg may not: pairing it
    /// against a completed one measures the ceiling, not the configuration.</summary>
    public bool CountsInPairedDelta => this is Completed;

    public string Describe =>
        this switch
        {
            Completed => "completed",
            CapExceeded c => $"cap {c.Kind}/{c.Scope} at {c.Reached} of {c.Limit}",
            Crashed c => $"crashed: {c.Reason}",
            Blocked b => $"blocked: {b.Reason}",
            _ => "unknown",
        };
}
