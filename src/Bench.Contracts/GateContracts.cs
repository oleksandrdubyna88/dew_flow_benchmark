namespace Bench.Contracts;

/// <summary>The gate benchmark's wire shapes — <c>bench gate report --json</c> and <c>/api/bench/gate/*</c>
/// answer with these, one shape for both surfaces.
/// <para>
/// <b>No free text from a finding or a prompt travels here.</b> Every string property on every <c>Gate*Dto</c>
/// is an id, a hash, an enum NAME, a language, a stamp or a version text, and a reflection test in the test
/// project holds each one to an allow-list — a new field is red until it is named there. The one exception is
/// <see cref="GateRunSummaryDto.FailureText"/>, a failure cause that passed the publication redaction, and the
/// test names it as the exception.
/// </para></summary>
/// <param name="Id">The scope's key (<c>GateScope.Id</c>) — what <c>?scope=</c> and <c>--scope</c> take.</param>
/// <param name="Runs">Runs in the scope — one per cell, its latest attempt.</param>
/// <param name="Sources">Where its runs came from: <c>native</c>, or an import's harness name — shown on screen, so an
/// imported scope is never read as a native measurement.</param>
/// <param name="Rubrics">The rubrics this scope's verdicts carry — the ONLY ones a rubric control may offer. Empty: nothing
/// was assessed.</param>
/// <param name="TasksRecorded">Whether the suite's tasks (language, calibration, seeds) are in the database. When they are
/// not, the per-model table is refused — it cannot put the calibration tasks apart — and the run list still renders.</param>
public sealed record GateScopeDto(
    string Id,
    string SuiteStamp,
    string Gate,
    string ProductVersion,
    string BinarySha256,
    string SettingsHash,
    int Runs,
    IReadOnlyList<string> Sources,
    IReadOnlyList<GateRubricDto> Rubrics,
    bool TasksRecorded);

/// <summary>A rubric a scope's verdicts were read under: its id, its KIND (<c>Strict</c> or <c>LenientWorth</c> — two
/// populations, never one column), the hash of its wording, the <c>id#hash12</c> stamp, and how many verdicts carry it.</summary>
public sealed record GateRubricDto(string Id, string Kind, string Hash, string Stamp, int Verdicts);

/// <summary>A number that may not be one. <paramref name="State"/> says why when it is not: <c>known</c>,
/// <c>unassessed</c> (nobody looked — rendered <c>—</c>, never <c>0</c>), <c>unknown</c> (could not be
/// captured — a CLI reviewer's cost, never free), <c>withheld</c> (too few repeats to state), or
/// <c>not-applicable</c> (a task with no seeds has no recall), or <c>not-hand-checked</c> (a strict percentage no person
/// has checked twenty verdicts of yet — the counts beside it stay visible).</summary>
public sealed record GateFigureDto(bool Known, double Value, string State)
{
    public const string KnownState = "known";
    public const string UnassessedState = "unassessed";
    public const string UnknownState = "unknown";
    public const string WithheldState = "withheld";
    public const string NotApplicableState = "not-applicable";
    public const string NotHandCheckedState = "not-hand-checked";

    public static GateFigureDto Of(double value) => new(true, value, KnownState);

    public static GateFigureDto Unassessed { get; } = new(false, 0, UnassessedState);

    public static GateFigureDto Unknown { get; } = new(false, 0, UnknownState);

    public static GateFigureDto Withheld { get; } = new(false, 0, WithheldState);

    public static GateFigureDto NotApplicable { get; } = new(false, 0, NotApplicableState);

    public static GateFigureDto NotHandChecked { get; } = new(false, 0, NotHandCheckedState);
}

/// <summary>One row of the per-model table — the operator's columns, with every refusal a state rather than a
/// number. Counts are integers; rates and means are figures; a failed run stays in every denominator.</summary>
public sealed record GateModelRowDto(
    string ReviewerId,
    int Runs,
    int Attempts,
    int AttemptsFailed,
    int ValidRuns,
    int AssessedRuns,
    GateFigureDto ValidPct,
    GateFigureDto FindingsPerRun,
    GateFigureDto SeedsHitMean,
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
    GateFigureDto SupportedPct,
    GateFigureDto SupportedOrPartialPct,
    GateFigureDto HighValuePerRun,
    GateFigureDto OverstatedPct,
    GateFigureDto SecondsP50,
    GateFigureDto SecondsP90,
    GateFigureDto TurnsMean,
    GateFigureDto RepairRuns,
    GateFigureDto RepairCalls,
    GateFigureDto ServedMean,
    GateFigureDto RefusedMean,
    GateFigureDto TokensInPerRun,
    GateFigureDto TokensOutPerRun,
    GateFigureDto TokensCachedPerRun,
    GateFigureDto CachePct,
    GateFigureDto TurnOneCachedMean,
    int WarmRuns,
    GateFigureDto ReasoningPerRun,
    GateFigureDto CostPerRun,
    GateFigureDto CostPerSeed,
    GateFigureDto CostTotal,
    IReadOnlyList<GateFailureCountDto> Failures);

public sealed record GateFailureCountDto(string FailureKind, int Runs);

/// <summary>Run-to-run variance for one task × reviewer: the spread of seeds hit and of findings across the
/// repeats, each with its own state — the seeds spread needs three ASSESSED runs, the findings spread three
/// repeats — and <c>withheld</c> below three.</summary>
public sealed record GateVarianceDto(
    string TaskId,
    string ReviewerId,
    int Repeats,
    int SeedReadings,
    string SeedsState,
    int SeedsHitMin,
    int SeedsHitMax,
    string FindingsState,
    int FindingsMin,
    int FindingsMax);

/// <summary>One task × reviewer cell of the per-task table: the raw readings per repeat, so a reader sees the
/// spread rather than a mean over three.</summary>
public sealed record GatePerTaskRowDto(
    string TaskId,
    string Language,
    bool Calibration,
    string ReviewerId,
    int Runs,
    int ValidRuns,
    IReadOnlyList<int> Findings,
    IReadOnlyList<GateFigureDto> SeedsHit,
    IReadOnlyList<GateFigureDto> Turns,
    IReadOnlyList<double> Seconds,
    IReadOnlyList<GateFigureDto> Cost);

/// <summary>The per-model table for ONE scope under ONE rubric. There is no shape for a table across rubric
/// kinds, and that absence is the rule. Calibration tasks are reported apart, in <paramref name="CalibrationRows"/>,
/// never inside <paramref name="Rows"/>; <paramref name="AllTaskRows"/> is every task, calibration included — the
/// population an imported harness published its table over, shown beside the default reading and never as it.</summary>
public sealed record GateModelTableDto(
    GateScopeDto Scope,
    string RubricKind,
    string RubricId,
    IReadOnlyList<GateModelRowDto> Rows,
    IReadOnlyList<GateModelRowDto> CalibrationRows,
    IReadOnlyList<GatePerTaskRowDto> PerTask,
    IReadOnlyList<GateVarianceDto> Variance,
    IReadOnlyList<GateModelRowDto> AllTaskRows);

/// <param name="Source">Where the run came from — <c>native</c>, or an import's harness name — so an imported
/// run never enters a native figure unlabelled.</param>
/// <param name="FailureText">The one free-text field in the gate contracts: the failure cause, after the
/// publication redaction. Empty on a valid run.</param>
/// <param name="TaskRecorded">Whether the task's language and calibration flag are in the database; when not, both read
/// <i>not recorded</i> on screen rather than as an empty language and a measured task.</param>
/// <param name="Superseded">An earlier attempt of a cell whose later attempt is the run the figures read.</param>
public sealed record GateRunSummaryDto(
    Guid RunId,
    string Gate,
    string TaskId,
    string Language,
    bool Calibration,
    bool TaskRecorded,
    string ReviewerId,
    int Repeat,
    int Attempt,
    bool Superseded,
    string Source,
    bool Valid,
    string Verdict,
    int Findings,
    GateFigureDto Turns,
    double Seconds,
    GateFigureDto CostUsd,
    string FailureKind,
    string FailureText,
    string ProductVersion,
    string BinarySha256);

public sealed record GateFindingDto(
    int Ordinal,
    string Severity,
    string Category,
    bool IsGating,
    int Line,
    string TextHash,
    string FileHash);

/// <param name="Reading">The rubric's reading name, or <c>AssessmentFailure</c>; <paramref name="FailureCause"/>
/// names the cause then and is empty otherwise.</param>
public sealed record GateVerdictDto(
    int Ordinal,
    string RubricId,
    string RubricKind,
    string RubricHash,
    string Reading,
    string Value,
    string SeverityFair,
    string Grounded,
    string ClusterHash,
    string SeedHit,
    string AssessorId,
    bool AssessorFamilyMatches,
    string FailureCause);

public sealed record GateTokensDto(
    GateFigureDto In,
    GateFigureDto Out,
    GateFigureDto Cached,
    GateFigureDto Reasoning,
    GateFigureDto CachePct);

public sealed record GateRunDetailDto(
    GateRunSummaryDto Summary,
    GateScopeDto Scope,
    string SettingsHash,
    string PromptHash,
    GateTokensDto Tokens,
    IReadOnlyList<GateFindingDto> Findings,
    IReadOnlyList<GateVerdictDto> Verdicts);

/// <summary>A figure in WORDS — the one rendering the CLI's text report and the console's page both use, so a refusal reads
/// the same on every surface and never as a number: <c>—</c> nobody looked, <i>unknown</i> nothing was captured,
/// <i>withheld</i> too few repeats, <i>n/a</i> the column has no business in the row, <i>not hand-checked</i> a strict
/// percentage no person has checked yet.</summary>
public static class GateFigureWords
{
    public const string Dash = "—";

    public static string Of(GateFigureDto figure, string format = "0.##") =>
        figure.State switch
        {
            GateFigureDto.KnownState => figure.Value.ToString(format, System.Globalization.CultureInfo.InvariantCulture),
            GateFigureDto.UnassessedState => Dash,
            GateFigureDto.UnknownState => "unknown",
            GateFigureDto.WithheldState => "withheld",
            GateFigureDto.NotApplicableState => "n/a",
            GateFigureDto.NotHandCheckedState => "not hand-checked",
            var other => other,
        };
}
