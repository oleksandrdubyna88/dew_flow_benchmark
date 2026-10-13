namespace Bench.Domain.Scouts;

/// <summary>How one workload reads against D5's margins.</summary>
public enum ScoutWorkloadOutcome
{
    /// <summary>Lower bound of Δrecall ≥ −0.03 AND upper bound of Δwrong ≤ +0.5, both measured, both estimable.</summary>
    Pass,

    /// <summary>A whole interval lies beyond its margin — the Sonnet team was SHOWN worse there.</summary>
    Fail,

    /// <summary>An interval crosses its margin, or a half of the decision is not measured — inconclusive, which is a result (§3.8).</summary>
    NotShown,

    /// <summary>A preliminary run (D7): the intervals are reported, no reading is drawn from them.</summary>
    Preliminary,
}

/// <summary>Why a workload did not pass — closed words, so the report never carries a sentence.</summary>
public enum ScoutDecisionReason
{
    /// <summary>No task had both arms scored — nothing to compare.</summary>
    NoPairedTask,

    /// <summary>Every paired task comes from ONE repository: the clustered interval cannot be estimated.</summary>
    OneRepository,

    /// <summary>Even the upper bound of Δrecall is below −0.03.</summary>
    RecallBelowMargin,

    /// <summary>The Δrecall interval straddles −0.03.</summary>
    RecallCrossesMargin,

    /// <summary>An arm's wrong-claim result is not measured (§3.6: more than 10 % silent cells, or claims left unassessed) — D5 needs both
    /// halves, so the workload cannot pass.</summary>
    WrongNotMeasured,

    /// <summary>Even the lower bound of Δwrong is above +0.5 per 10.</summary>
    WrongAboveMargin,

    /// <summary>The Δwrong interval straddles +0.5 per 10.</summary>
    WrongCrossesMargin,

    /// <summary>The run measured no arm pair of this workload — the rule needs all three.</summary>
    WorkloadMissing,
}

/// <summary>The run's answer to D5. <see cref="RuleChangeShown"/> only when W1, W2 and W3 all pass on a DECISION run.</summary>
public enum ScoutVerdict
{
    RuleChangeShown,
    NotShown,
    Preliminary,
}

/// <summary>What one workload brings to the decision: its paired intervals and whether BOTH arms' wrong-claim results were measured.</summary>
public sealed record ScoutWorkloadReading(ScoutWorkload Workload, ScoutInterval Recall, ScoutInterval Wrong, bool WrongMeasured);

public sealed record ScoutWorkloadDecision(ScoutWorkload Workload, ScoutWorkloadOutcome Outcome, IReadOnlyList<ScoutDecisionReason> Reasons);

public sealed record ScoutDecision(ScoutVerdict Verdict, IReadOnlyList<ScoutWorkloadDecision> Workloads);

/// <summary>§3.8, pre-registered, as a pure function of the intervals. D5's margins are the OPERATOR's and live here as constants — a noisy
/// harness must never widen them. The decision reads the BOUNDS, never the point estimate: a point that passes inside an interval that
/// crosses a margin is exactly the inconclusive result the rule refuses to call a pass.</summary>
public static class ScoutDecisions
{
    /// <summary>D5: the Sonnet team may find at most 3 percentage points fewer of the right files.</summary>
    public const double RecallMargin = -0.03;

    /// <summary>D5: and make at most 0.5 more wrong claims per 10 claims.</summary>
    public const double WrongMargin = 0.5;

    public static ScoutDecision Decide(IReadOnlyList<ScoutWorkloadReading> readings, ScoutRunKind kind)
    {
        var decided = Enum.GetValues<ScoutWorkload>()
            .Select(w => readings.FirstOrDefault(r => r.Workload == w) is { } reading
                ? Workload(reading, kind)
                : new ScoutWorkloadDecision(w, kind == ScoutRunKind.Preliminary ? ScoutWorkloadOutcome.Preliminary : ScoutWorkloadOutcome.NotShown, [ScoutDecisionReason.WorkloadMissing]))
            .ToList();

        return new ScoutDecision(Verdict(decided, kind), decided);
    }

    private static ScoutVerdict Verdict(IReadOnlyList<ScoutWorkloadDecision> decided, ScoutRunKind kind) =>
        (kind, decided.All(d => d.Outcome == ScoutWorkloadOutcome.Pass)) switch
        {
            (ScoutRunKind.Preliminary, _) => ScoutVerdict.Preliminary,
            (_, true) => ScoutVerdict.RuleChangeShown,
            _ => ScoutVerdict.NotShown,
        };

    /// <summary>One workload. A preliminary run keeps only the reasons that say a half was not MEASURED — where its intervals sit against the
    /// margins is for the reader of the intervals, not a reading the run states.</summary>
    public static ScoutWorkloadDecision Workload(ScoutWorkloadReading reading, ScoutRunKind kind)
    {
        IReadOnlyList<ScoutDecisionReason> reasons = [.. RecallReasons(reading.Recall).Concat(WrongReasons(reading)).Distinct()];

        return kind == ScoutRunKind.Preliminary
            ? new ScoutWorkloadDecision(reading.Workload, ScoutWorkloadOutcome.Preliminary, [.. reasons.Where(IsMeasurement)])
            : new ScoutWorkloadDecision(reading.Workload, Outcome(reasons), reasons);
    }

    private static ScoutWorkloadOutcome Outcome(IReadOnlyList<ScoutDecisionReason> reasons) =>
        (reasons.Count, reasons.Any(r => r is ScoutDecisionReason.RecallBelowMargin or ScoutDecisionReason.WrongAboveMargin)) switch
        {
            (0, _) => ScoutWorkloadOutcome.Pass,
            (_, true) => ScoutWorkloadOutcome.Fail,
            _ => ScoutWorkloadOutcome.NotShown,
        };

    private static bool IsMeasurement(ScoutDecisionReason reason) =>
        reason is ScoutDecisionReason.NoPairedTask or ScoutDecisionReason.OneRepository or ScoutDecisionReason.WrongNotMeasured;

    /// <summary>Δrecall is held to its LOWER bound: below −0.03 is not shown, and an upper bound below it too is a failure.</summary>
    private static IReadOnlyList<ScoutDecisionReason> RecallReasons(ScoutInterval recall) => recall switch
    {
        { Known: false } => [ScoutDecisionReason.NoPairedTask],
        { Estimable: false } => [ScoutDecisionReason.OneRepository],
        { Upper: < RecallMargin } => [ScoutDecisionReason.RecallBelowMargin],
        { Lower: < RecallMargin } => [ScoutDecisionReason.RecallCrossesMargin],
        _ => [],
    };

    /// <summary>Δwrong is held to its UPPER bound: above +0.5 is not shown, and a lower bound above it too is a failure.</summary>
    private static IReadOnlyList<ScoutDecisionReason> WrongReasons(ScoutWorkloadReading reading) => (reading.WrongMeasured, reading.Wrong) switch
    {
        (false, _) or (_, { Known: false }) => [ScoutDecisionReason.WrongNotMeasured],
        (_, { Estimable: false }) => [ScoutDecisionReason.OneRepository],
        (_, { Lower: > WrongMargin }) => [ScoutDecisionReason.WrongAboveMargin],
        (_, { Upper: > WrongMargin }) => [ScoutDecisionReason.WrongCrossesMargin],
        _ => [],
    };
}
