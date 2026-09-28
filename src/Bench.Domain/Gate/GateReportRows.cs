namespace Bench.Domain.Gate;

/// <summary>One row of the per-model table — the operator's columns. Every refusal is a <see cref="Figure"/>
/// state. <see cref="Runs"/> counts one run per cell (task, reviewer, repeat) — its LATEST attempt — and every
/// figure is over those, failed ones included; <see cref="Attempts"/> and <see cref="AttemptsFailed"/> count
/// every attempt, the other harness's <c>attempts (failed)</c> column.</summary>
public sealed record ModelRow(
    GateReviewerId Reviewer,
    int Runs,
    int Attempts,
    int AttemptsFailed,
    int ValidRuns,
    int AssessedRuns,
    Figure ValidPct,
    Figure FindingsPerRun,
    Figure SeedsHitMean,
    int SeedsHitMin,
    int SeedsHitMax,
    int DistinctSeeds,
    int DistinctCrossEpicSeeds,
    int Assessed,
    int Supported,
    int Partial,
    int Refuted,
    int Unresolved,
    int AssessmentFailed,
    int AssessorFamilyMatched,
    Figure SupportedPct,
    Figure SupportedOrPartialPct,
    Figure HighValuePerRun,
    Figure OverstatedPct,
    Figure SecondsP50,
    Figure SecondsP90,
    Figure TurnsMean,
    Figure RepairRuns,
    Figure RepairCalls,
    Figure ServedMean,
    Figure RefusedMean,
    Figure TokensInPerRun,
    Figure TokensOutPerRun,
    Figure TokensCachedPerRun,
    Figure CachePct,
    Figure TurnOneCachedMean,
    int WarmRuns,
    Figure ReasoningPerRun,
    Figure CostPerRun,
    Figure CostPerSeed,
    Figure CostTotal,
    IReadOnlyList<FailureCount> Failures);

public sealed record FailureCount(FailureKind Kind, int Runs);

public sealed record PerTaskRow(
    GateTaskId Task,
    string Language,
    bool Calibration,
    GateReviewerId Reviewer,
    int Runs,
    int ValidRuns,
    IReadOnlyList<int> Findings,
    IReadOnlyList<Figure> SeedsHit,
    IReadOnlyList<Figure> Turns,
    IReadOnlyList<double> Seconds,
    IReadOnlyList<Figure> Cost);

public enum VarianceState
{
    Stated,
    Withheld,
    Unassessed,
}

/// <summary>Run-to-run variance for one task × reviewer, as TWO spreads with a state each, because they have
/// different populations: the findings count is read off every reply, so it needs
/// <see cref="GateReport.MinRepeatsForVariance"/> repeats; seeds hit exist only for ASSESSED runs, so that spread
/// needs as many <see cref="SeedReadings"/>. A withheld or unassessed spread's numbers are zero and MUST NOT be
/// read.</summary>
public sealed record VarianceReading(
    GateTaskId Task,
    GateReviewerId Reviewer,
    int Repeats,
    int SeedReadings,
    VarianceState SeedsState,
    int SeedsHitMin,
    int SeedsHitMax,
    VarianceState FindingsState,
    int FindingsMin,
    int FindingsMax);

/// <summary>The per-model table for one scope under ONE rubric. Calibration tasks are reported apart in
/// <see cref="Calibration"/> and never inside <see cref="Rows"/>: a calibration task settled a transport, so its
/// numbers describe the tuning as much as the model.</summary>
public sealed record ModelTable(
    GateScope Scope,
    Rubric Rubric,
    IReadOnlyList<ModelRow> Rows,
    IReadOnlyList<ModelRow> Calibration,
    IReadOnlyList<PerTaskRow> PerTask,
    IReadOnlyList<VarianceReading> Variance)
{
    /// <summary>Every task, calibration tasks INCLUDED — the population the other harness's per-model table is computed
    /// over (<c>report.py: per_model</c> reads every phase-2 task). It exists so an imported measurement can be held
    /// against the table it was published as, like with like; it is never the default reading, which is
    /// <see cref="Rows"/> with the calibration tasks apart.</summary>
    public IReadOnlyList<ModelRow> AllTasks { get; init; } = [];
}
