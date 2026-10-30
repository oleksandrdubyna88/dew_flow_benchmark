using Bench.Domain;
using ModelArms.Core.Experiment;

namespace ModelArms.Core.Steps;

/// <summary>What recovery can see of a step's files.</summary>
public sealed record RecoveryFacts(bool DoneMarker, Outcome<Guid> Session, bool TranscriptExists, int LastAttempt, StepEnd LastEnd, IReadOnlyList<TimingLine> Timing, Outcome<ContinueMarker> Continue);

/// <summary>Where a step's next entry begins.</summary>
public abstract record StepEntry
{
    private StepEntry()
    {
    }

    public sealed record Skip : StepEntry;

    public sealed record RecoveredDone(StepEnd End, int Attempt) : StepEntry;

    public sealed record Fresh(int Attempt, int BudgetSeconds) : StepEntry;

    public sealed record Resume(Guid Session, int Attempt, int BudgetSeconds) : StepEntry;
}

/// <summary>Recovery as a pure decision.</summary>
public static class StepRecovery
{
    public static StepEntry Decide(string step, RecoveryFacts facts, StepSpec spec, Limits limits) => new StepEntry.Fresh(1, 0);
}
