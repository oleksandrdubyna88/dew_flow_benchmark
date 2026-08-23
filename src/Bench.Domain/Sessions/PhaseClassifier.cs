namespace Bench.Domain.Sessions;

/// <summary>The phase label, decided by arithmetic and never by a model.
/// <para>
/// This is the first thing this whole system replaces with a formula, and it is deliberately the smallest:
/// a call's phase is a function of what the tool does, what the working tree said, and what the previous
/// call was. Asking a model "what was the agent doing here" would inherit the model's variance into the
/// denominator of every later measurement — the corpus would then be measuring its own judge.
/// </para></summary>
public static class PhaseClassifier
{
    /// <summary>One call's phase.
    /// <para>
    /// <b>The working tree outranks the allowlist.</b> A call that changed tracked files is execution
    /// whatever it was called and whatever the command looked like — that is the entire point of paying for
    /// the <c>git status --porcelain</c> check. The disagreement is not swallowed: the call keeps both
    /// readings and <see cref="SessionToolCall.Disagrees"/> surfaces the ones where a supposedly harmless
    /// tool moved the tree.
    /// </para>
    /// <para>
    /// A neutral or unclassified call CARRIES the phase around it rather than starting one. A todo update
    /// between two edits is part of the edit run, and a classifier that let it open a phase of its own
    /// would report switch counts that are mostly bookkeeping.
    /// </para></summary>
    public static SessionPhase Resolve(ToolClass toolClass, MutationEvidence mutation, SessionPhase previous) =>
        mutation == MutationEvidence.Changed
            ? SessionPhase.Execution
            : FromClass(toolClass, previous);

    /// <summary>Re-labels a whole sequence — the recomputation path.
    /// <para>
    /// Phases are stored on the calls, so improving this classifier would otherwise only reach sessions
    /// measured after the change. The inputs it reads are all persisted beside the label, which is what
    /// makes a re-label possible without measuring anything again.
    /// </para></summary>
    public static IReadOnlyList<SessionToolCall> Label(IReadOnlyList<SessionToolCall> calls)
    {
        var labelled = new List<SessionToolCall>(calls.Count);
        var previous = SessionPhase.Unknown;

        foreach (var call in calls)
        {
            previous = Resolve(call.Class, call.Mutation, previous);
            labelled.Add(call with { Phase = previous });
        }

        return labelled;
    }

    private static SessionPhase FromClass(ToolClass toolClass, SessionPhase previous) => toolClass switch
    {
        ToolClass.Read => SessionPhase.Research,
        ToolClass.Write => SessionPhase.Execution,
        ToolClass.Verify => SessionPhase.Verification,
        _ => Carry(previous),
    };

    /// <summary>A session that opens with a neutral call is researching. Not a guess about intent — it is
    /// the only phase that can precede the first read or write, and leaving it
    /// <see cref="SessionPhase.Unknown"/> would put a bucket nobody can act on at the head of every
    /// report.</summary>
    private static SessionPhase Carry(SessionPhase previous) =>
        previous == SessionPhase.Unknown ? SessionPhase.Research : previous;
}
