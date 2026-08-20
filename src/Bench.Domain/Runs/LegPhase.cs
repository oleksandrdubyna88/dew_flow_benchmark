namespace Bench.Domain.Runs;

/// <summary>What kind of work a suite's questions ask for, which decides how many phases a leg has.</summary>
public enum TaskKind
{
    /// <summary>Answer a question about the code. One working phase, then the arbiter.
    /// <para>
    /// Kept as a regression guard rather than as the interesting question: measured 2026-08-15, a strong
    /// reader scored 89–100 % across four deliberately hard reading shapes against a ≤80 % discrimination
    /// threshold. A subject that answers perfectly measures its own ceiling.
    /// </para></summary>
    Reading,

    /// <summary>Fix a real, live bug. Three working phases, and scoring that is mostly mechanical —
    /// which is why this is the discriminator.</summary>
    Fix,
}

public enum PhaseKind
{
    /// <summary>Reading task: produce the answer.</summary>
    Answer,

    /// <summary>Fix task: understand the defect before touching anything.</summary>
    Investigate,

    /// <summary>Fix task: produce the diff, and a test of its own.</summary>
    Fix,

    /// <summary>Fix task: the solver checks its own work before handing it over.</summary>
    Verify,

    /// <summary>The arbiter, over evidence that is already established.</summary>
    Judge,
}

public enum PhaseState
{
    Pending,
    Running,
    Done,
    Stopped,
}

/// <summary>One phase of one leg, with its own budget and its own clock.
/// <para>
/// Phases are OURS. The adopted evaluation library's unit is a single evaluation with no notion of a
/// phase (see <c>research/SPIKE_dotnet_eval_library.md</c>, follow-up 1), and that is the right division:
/// <b>they score, we execute</b>. Cramming three phases into three scenario names would push the
/// structure back into a string, which is the flattening this project refuses everywhere else.
/// </para></summary>
public sealed record LegPhase(
    Guid Id,
    Guid CellId,
    PhaseKind Kind,
    int Ordinal,
    PhaseState State,
    TimeSpan Tools,
    TimeSpan Thinking,
    TimeSpan InfrastructureWait,
    decimal CostUsd,
    LegOutcomeKind Outcome,
    string Detail)
{
    public static LegPhase Pending(Guid cellId, PhaseKind kind, int ordinal) => new(
        Guid.CreateVersion7(), cellId, kind, ordinal, PhaseState.Pending,
        TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, 0m, LegOutcomeKind.None, string.Empty);

    public TimeSpan Elapsed => Tools + Thinking + InfrastructureWait;
}

/// <summary>Which phases a leg has, and what a phase's ending means for the ones after it.</summary>
public static class PhasePlan
{
    /// <summary>The working phases of a task kind, in order, followed by the arbiter.
    /// <para>
    /// The arbiter is last and always present — but for a fix task it grades a leg whose facts are
    /// already mechanical (the diff landed in the right file, the hidden tests are green, the solver's
    /// own test goes red without the fix, the neighbours are intact). It is there for the part no
    /// assertion covers: whether the diagnosis was right or a symptom was patched.
    /// </para></summary>
    public static IReadOnlyList<PhaseKind> For(TaskKind kind) =>
        kind switch
        {
            TaskKind.Reading => [PhaseKind.Answer, PhaseKind.Judge],
            TaskKind.Fix => [PhaseKind.Investigate, PhaseKind.Fix, PhaseKind.Verify, PhaseKind.Judge],
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown task kind"),
        };

    /// <summary>The phases of one ARM of a task (todo/PLAN_investigate_vs_implement.md §3.1).
    /// <para>
    /// <see cref="FixArm.Full"/> is the plan the kind always had. The two slices exist for the split
    /// measurement: investigate-only ends at the judged diagnosis, implement-only starts from a handed
    /// one. A reading task REFUSES a fix arm by name — an arm the kind cannot honour must fail the
    /// plan, never load as something else.
    /// </para></summary>
    public static Outcome<IReadOnlyList<PhaseKind>> For(TaskKind kind, FixArm arm) =>
        (kind, arm) switch
        {
            (_, FixArm.Full) => Outcome<IReadOnlyList<PhaseKind>>.Success(For(kind)),
            (TaskKind.Fix, FixArm.InvestigateOnly) =>
                Outcome<IReadOnlyList<PhaseKind>>.Success([PhaseKind.Investigate, PhaseKind.Judge]),
            (TaskKind.Fix, FixArm.ImplementOnly) =>
                Outcome<IReadOnlyList<PhaseKind>>.Success([PhaseKind.Fix, PhaseKind.Verify, PhaseKind.Judge]),
            _ => Outcome<IReadOnlyList<PhaseKind>>.Failure(
                $"a {kind} task has no {arm.Canonical()} arm — the arms slice a fix task, and running one "
                + "here would measure nothing"),
        };

    public static IReadOnlyList<LegPhase> Materialise(Guid cellId, TaskKind kind) =>
        [.. For(kind).Select((phase, ordinal) => LegPhase.Pending(cellId, phase, ordinal))];

    public static Outcome<IReadOnlyList<LegPhase>> Materialise(Guid cellId, TaskKind kind, FixArm arm) =>
        For(kind, arm).Match(
            phases => Outcome<IReadOnlyList<LegPhase>>.Success(
                [.. phases.Select((phase, ordinal) => LegPhase.Pending(cellId, phase, ordinal))]),
            Outcome<IReadOnlyList<LegPhase>>.Failure);

    /// <summary>Starting a phase. Refused unless every earlier phase has ended — a fix produced before
    /// the investigation ran is not a fix, it is a guess that happened to compile.</summary>
    public static Outcome<LegPhase> Start(LegPhase phase, IReadOnlyList<LegPhase> all)
    {
        if (phase.State != PhaseState.Pending)
        {
            return Outcome<LegPhase>.Failure($"phase {phase.Kind} is {phase.State}, not Pending");
        }

        var unfinished = all.Where(p => p.Ordinal < phase.Ordinal && p.State != PhaseState.Done).ToList();

        return unfinished.Count == 0
            ? Outcome<LegPhase>.Success(phase with { State = PhaseState.Running })
            : Outcome<LegPhase>.Failure(
                $"phase {phase.Kind} cannot start while {string.Join(", ", unfinished.Select(p => $"{p.Kind}({p.State})"))} has not finished");
    }

    /// <summary>Ending a phase. A ceiling stops the LEG, not just the phase: continuing to the next
    /// phase after the budget ran out would measure a leg that got more than the arm allowed it.</summary>
    public static (LegPhase Ended, IReadOnlyList<LegPhase> Others) End(
        LegPhase phase, IReadOnlyList<LegPhase> all, LegOutcome outcome)
    {
        var (kind, detail) = LegOutcomeCodec.Encode(outcome);

        return End(phase, all, kind, detail);
    }

    /// <summary>Ending a phase from a STORED outcome — the kind and detail exactly as a settled cell
    /// carries them.
    /// <para>
    /// The runner closes a fix leg's phase record AFTER the cell settled, and a stored outcome
    /// deliberately has no decoder (<see cref="LegOutcomeCodec"/>: parsing the display sentence back
    /// into structure is a lie that works until somebody edits the wording). The transition needs only
    /// whether the leg COMPLETED; everything else rides as the text it already is.
    /// </para></summary>
    public static (LegPhase Ended, IReadOnlyList<LegPhase> Others) End(
        LegPhase phase, IReadOnlyList<LegPhase> all, LegOutcomeKind kind, string detail)
    {
        var ended = phase with { State = PhaseState.Done, Outcome = kind, Detail = detail };

        var stopsTheLeg = kind is not LegOutcomeKind.Completed;

        var others = all
            .Where(p => p.Id != phase.Id)
            .Select(p => stopsTheLeg && p.Ordinal > phase.Ordinal && p.State == PhaseState.Pending
                ? p with { State = PhaseState.Stopped, Outcome = kind, Detail = $"not run: {phase.Kind} ended with {kind}" }
                : p)
            .ToList();

        return (ended, others);
    }

    /// <summary>A leg is finished when no phase can still run.</summary>
    public static bool IsFinished(IReadOnlyList<LegPhase> all) =>
        all.All(p => p.State is PhaseState.Done or PhaseState.Stopped);
}
