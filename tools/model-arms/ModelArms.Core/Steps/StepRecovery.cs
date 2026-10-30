using Bench.Domain;
using ModelArms.Core.Experiment;

namespace ModelArms.Core.Steps;

/// <summary>What recovery can see of a step's files, read by the runner and handed in as values (plan D7): whether <c>.done</c> exists, the session id and whether claude left its transcript, the number of the last
/// attempt (the streams on disk; 0 when there is none) and how that stream ended read ALONE — so it can never be <see cref="StepEnd.HardDeadline"/>, which only the timing line knows — the timing lines, and the gate's
/// <c>.continue</c> marker (a failure is "no marker", or one that cannot be read).</summary>
public sealed record RecoveryFacts(
    bool DoneMarker,
    Outcome<Guid> Session,
    bool TranscriptExists,
    int LastAttempt,
    StepEnd LastEnd,
    IReadOnlyList<TimingLine> Timing,
    Outcome<ContinueMarker> Continue);

/// <summary>Where the next entry into a step begins (plan D7).</summary>
public abstract record StepEntry
{
    private StepEntry()
    {
    }

    /// <summary>The step is done: nothing to run.</summary>
    public sealed record Skip : StepEntry;

    /// <summary>The step finished before its runner died: done, recorded as recovered, never resumed.</summary>
    public sealed record RecoveredDone(StepEnd End, int Attempt) : StepEntry;

    /// <summary>Start the step over with a new session id.</summary>
    public sealed record Fresh(int Attempt, int BudgetSeconds) : StepEntry;

    /// <summary>Continue <paramref name="Session"/> with what is left of the budget.</summary>
    public sealed record Resume(Guid Session, int Attempt, int BudgetSeconds) : StepEntry;
}

/// <summary>Recovery as a pure decision over the files a step left (plan D7).</summary>
public static class StepRecovery
{
    /// <summary>The state word of an end line the wall killed — read from the one spelling, never retyped.</summary>
    private static readonly string DeadlineWord = new StepEnd.HardDeadline().Word;

    /// <summary>The entry, in this order: a <c>.continue</c> marker naming the LAST attempt resumes that finished session whatever <c>.done</c> says (the gate's recorded intent, D10 — a marker for any other attempt is stale and
    /// decides nothing); <c>.done</c> skips; a last stream that finished, or whose end line says <c>hard-deadline</c>, is recovered as done and never resumed (the previous generation's arm C); no readable session, or a
    /// session without its transcript, starts fresh; anything else — an error result, a limit, an interruption — resumes. Both launches carry <see cref="StepBudget.RemainingSeconds"/>, floored at the configured minimum.</summary>
    public static StepEntry Decide(string step, RecoveryFacts facts, StepSpec spec, Limits limits)
    {
        var remaining = StepBudget.RemainingSeconds(spec.Minutes, facts.Timing, step, limits.MinimumRemainingSeconds);
        return Continued(facts) ? Launch(facts, remaining) : Settled(step, facts, remaining);
    }

    /// <summary>True when the gate asked for the last attempt's session to go on: a readable marker whose <c>After</c> is the last attempt, and there is one.</summary>
    private static bool Continued(RecoveryFacts facts) =>
        facts.LastAttempt > 0 && facts.Continue is Outcome<ContinueMarker>.Ok { Value.After: var after } && after == facts.LastAttempt;

    private static StepEntry Settled(string step, RecoveryFacts facts, int remaining) =>
        facts.DoneMarker ? new StepEntry.Skip() : DoneAlready(step, facts) ?? Launch(facts, remaining);

    private static StepEntry? DoneAlready(string step, RecoveryFacts facts) =>
        facts.LastAttempt <= 0 ? null
        : facts.LastEnd is StepEnd.Finished ? new StepEntry.RecoveredDone(facts.LastEnd, facts.LastAttempt)
        : EndedOnItsDeadline(step, facts) ? new StepEntry.RecoveredDone(new StepEnd.HardDeadline(), facts.LastAttempt)
        : null;

    private static bool EndedOnItsDeadline(string step, RecoveryFacts facts) =>
        facts.Timing.OfType<TimingLine.End>().Any(end => end.Step == step && end.Attempt == facts.LastAttempt && end.State == DeadlineWord);

    /// <summary>The choice the last two rows share: resume the session when it is readable and claude left its transcript, else start fresh.</summary>
    private static StepEntry Launch(RecoveryFacts facts, int remaining) =>
        facts.Session is Outcome<Guid>.Ok { Value: var session } && facts.TranscriptExists
            ? new StepEntry.Resume(session, facts.LastAttempt + 1, remaining)
            : new StepEntry.Fresh(facts.LastAttempt + 1, remaining);
}
