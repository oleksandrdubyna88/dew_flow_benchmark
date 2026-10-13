namespace Bench.Domain.Scouts;

/// <summary>Every "why" a scout row can carry — an ALLOW-LISTED word, stored by name, so no free sentence reaches the database (the
/// probes' D11). Three families, told apart by <see cref="ScoutReasons"/>: a VOID reason makes the cell unscored and re-runnable as a
/// next generation; a STOP reason stops the whole campaign, because re-running would keep the broken boundary or the broken
/// instrument; the rest end a cell for a reason that is neither.</summary>
public enum ScoutReason
{
    None,

    // Void — the read-back (§3.5).

    /// <summary>The stream has no <c>init</c> or no <c>result</c> envelope: it did not finish saying what happened.</summary>
    StreamIncomplete,

    /// <summary>Not exactly N Agent calls.</summary>
    WrongCallCount,

    /// <summary>An Agent call's <c>input.model</c> is not the arm's alias.</summary>
    InputModelMismatch,

    /// <summary>An Agent call's prompt is not one of the frozen prompts, byte for byte.</summary>
    PromptHashMismatch,

    /// <summary>An Agent call's <c>subagent_type</c> is not the arm's agent type.</summary>
    AgentTypeMismatch,

    /// <summary>An Agent call has no result, or its result is an error.</summary>
    AgentCallFailed,

    /// <summary>A child's <c>resolvedModel</c> is not in its Agent result.</summary>
    ChildModelNotCaptured,

    /// <summary>A child's <c>resolvedModel</c> or a message's <c>message.model</c> is not the arm's resolved id.</summary>
    ChildModelMismatch,

    /// <summary>A child called a tool outside {Read, Glob, Grep, LS}.</summary>
    ChildToolOutsideReadOnlySet,

    /// <summary>The main session called something other than Agent.</summary>
    MainSessionNonAgentCall,

    /// <summary><c>subagent_stats.completed</c> is not N, or the stats are missing.</summary>
    SubagentsNotCompleted,

    // Stop — the campaign (§3.2's path audit, §3.6's cost check, §3.4's pins).

    /// <summary>A child read, grepped or listed a target outside the tree, and it returned content.</summary>
    OutsideReadReturnedContent,

    /// <summary>A Glob result listed a path outside the tree.</summary>
    OutsideGlobListedPath,

    /// <summary>A call on an outside target whose result is missing, truncated or unrecognised — never a safe read.</summary>
    OutsideResultNotCaptured,

    /// <summary>The computed cost and <c>total_cost_usd</c> differ by more than 2 %.</summary>
    CostMismatch,

    /// <summary>A frozen pin moved mid-run.</summary>
    PinMoved,

    // Neither.

    /// <summary>A spend-limit or quota error: the cell is <i>aborted:spend</i>, never scored, and the run stops.</summary>
    SpendLimit,

    /// <summary>Handed back too many times by the sweep.</summary>
    Abandoned,
}

/// <summary>The families of <see cref="ScoutReason"/>.</summary>
public static class ScoutReasons
{
    public static bool IsVoid(ScoutReason reason) => reason is >= ScoutReason.StreamIncomplete and <= ScoutReason.SubagentsNotCompleted;

    public static bool IsStop(ScoutReason reason) => reason is >= ScoutReason.OutsideReadReturnedContent and <= ScoutReason.PinMoved;
}
