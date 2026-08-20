namespace Bench.Contracts;

/// <summary>The wire shape of a run's comparison. Emitted by <c>bench report --json</c> and by the API as a
/// response body — <b>one shape</b>, for the reason <see cref="RunPlanDto"/> already states: an agent
/// reading the CLI and a browser reading the API must never see different truths.
/// <para>
/// It is a flattening of the Application's typed view rather than the view itself, because this project
/// depends on nothing: a contract able to reference the domain is a contract that leaks it, and
/// <c>ProofState</c> would leave with it. So the verdict travels as a NAME — an ordinal changes meaning the
/// day somebody inserts an enum member, and this object is published beside the results.
/// </para></summary>
public sealed record RunReportDto(
    Guid RunId,
    string Label,
    string TargetCanonical,
    string SuiteStamp,
    string EngineCanonical,
    string MetricName,
    int LegsScored,
    int LegsPassed,
    int SelectionQuestions,
    int HeldOutQuestions,
    IReadOnlyList<DimensionReportDto> Dimensions,
    DiscriminationDto Discrimination,
    IReadOnlyList<string> Warnings,
    MachineDto Machine,
    string Load);

/// <param name="Recorded">False for every run measured before a machine was read. A consumer must render
/// that as "not recorded" rather than as a host whose fields happened to be empty.</param>
/// <param name="Fingerprint">What two runs are compared by. Empty when nothing was recorded.</param>
/// <param name="UnhealthyAdapters">Adapters not working properly — a card at Windows' error code 31 is still
/// enumerated while everything silently runs on the CPU, so this is the one field that changes how every
/// number in the report reads.</param>
public sealed record MachineDto(
    bool Recorded,
    string Hostname,
    string Fingerprint,
    string OperatingSystem,
    string Cpu,
    long TotalRamBytes,
    IReadOnlyList<string> Adapters,
    IReadOnlyList<string> UnhealthyAdapters,
    string Volume);

/// <param name="Baseline">The arm the others were read against; empty when none was stated, which is not
/// the same as none existing — a report never nominates one by score.</param>
/// <param name="RankingRefusal">Empty when these arms may be ranked. Non-empty when they may not, naming
/// the leg counts that decided it. The averages are present either way.</param>
public sealed record DimensionReportDto(
    string Dimension,
    IReadOnlyList<ArmReadingDto> Arms,
    string Baseline,
    string RankingRefusal);

/// <param name="Proof">One of <c>Confirmed</c>, <c>Unproven</c>, <c>Suspicious</c>, <c>NotAWinner</c>.
/// <c>Unproven</c> is its own value and never a smaller <c>Confirmed</c>: a configuration that won only on
/// the half that chose it is the shape of every false winner, and a consumer must be able to render it as
/// such rather than infer it from a margin.</param>
/// <param name="Margin">How far this arm beat the baseline on the held-out half; <c>0</c> when there is no
/// verdict. Deliberately not a threshold — a floor nobody has measured would be a quality claim.</param>
public sealed record ArmReadingDto(
    string Arm,
    double Average,
    int Legs,
    HalfReadingDto Selection,
    HalfReadingDto HeldOut,
    string Proof,
    double Margin);

/// <param name="Measured">False when this arm ran no leg on this half. A half nobody measured and a half
/// scored zero are opposite readings of one number, so the flag travels beside the value rather than the
/// value being rendered as <c>0</c>.</param>
public sealed record HalfReadingDto(bool Measured, double Average, int Legs);

/// <summary>How the run's questions behaved across its subjects. Counts only — nothing here proposes
/// retiring anything, because discrimination is a property of a COMPARISON and not of a question.</summary>
public sealed record DiscriminationDto(
    int Discriminating,
    int EveryonePasses,
    int NobodyPasses,
    int TooClose,
    int Unusable,
    string Describe);

/// <summary>One row of the console's run list.</summary>
/// <param name="ComputeArm">The route, provider and device this run was measured on — <c>windows-to-wsl/migraphx/R9700</c>
/// — or <c>not declared</c> when the engine echoed nothing. Its own field rather than a segment of
/// <paramref name="EngineCanonical"/>, which joins five facts with pipes for EQUALITY: a console grouping by
/// the arm would otherwise parse a format nobody promised it. The undeclared state travels as its own words
/// because a blank label groups every un-echoed run under whatever the page renders for empty.</param>
public sealed record RunSummaryDto(
    Guid RunId,
    string Label,
    string TargetCanonical,
    string SuiteStamp,
    string EngineCanonical,
    string ComputeArm,
    string Status,
    DateTimeOffset CreatedAt);

/// <summary>The one label the console must recognise rather than merely print.
/// <para>
/// Grouping by the compute arm means telling an arm from the absence of one, and the absence is a state
/// (<c>BackendDeclaration.NotDeclared</c>) rather than a missing string. Naming it here, in the project both
/// sides already reference, is what stops the console from matching a domain word it guessed — and stops the
/// projection from drifting away from what the console looks for, since a test pins both to this constant.
/// </para></summary>
public static class ComputeArmLabel
{
    public const string NotDeclared = "not declared";
}

/// <summary>What a route answers when it refuses. Moved here from the endpoint project when the console was
/// built: a page that cannot read the REASON behind a 400 has to render "could not be reached" over a server
/// that answered perfectly well and explained itself.</summary>
public sealed record ProblemDto(string Reason);

public sealed record HealthDto(string Status);

/// <summary>The metric names a report can be asked for, published so a console can OFFER them.
/// <para>
/// A report requires a metric and has no default, deliberately — one picked for the reader would report on
/// an axis nobody chose. That is right for the CLI, where the operator knows the name, and unusable for a
/// page, where nothing on screen would say what to type. So the names travel as a contract.
/// </para>
/// <para>
/// <b>Copies of domain constants, pinned by a test.</b> The domain holds the authoritative strings and this
/// project may not reference it (a contract able to see the domain is a contract that leaks it), so the two
/// are asserted equal instead. A rename becomes a red test rather than a page offering a metric no result
/// carries — which would render as an empty report and read as a broken run.
/// </para></summary>
public static class WellKnownMetrics
{
    public const string AnchorRecall = "Anchor recall";
    public const string ToolUse = "Tool use";
    public const string RetrievalMrr = "Retrieval MRR";
    public const string RetrievalFirstHitRank = "Retrieval first-hit rank";
    public const string DiagnosisParses = "Diagnosis parses";
    public const string DiagnosisAnchorRecall = "Diagnosis anchor recall";
    public const string DiagnosisAnchorPrecision = "Diagnosis anchor precision";
    public const string DiagnosisSymptomOnly = "Diagnosis symptom-only";

    /// <summary>Offered in the order a reader would try them: the retrieval answer first, then the funnel,
    /// then the fix lane's own instruments.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        AnchorRecall,
        ToolUse,
        RetrievalMrr,
        RetrievalFirstHitRank,
        DiagnosisParses,
        DiagnosisAnchorRecall,
        DiagnosisAnchorPrecision,
        DiagnosisSymptomOnly,
    ];
}

/// <summary>The compute-backend comparison, as the wire sees it — <c>bench arms</c> and the API answer this
/// same shape, for the reason <see cref="RunReportDto"/> already states.
/// <para>
/// <b>A LIST of scoped comparisons rather than one table.</b> Two runs are only comparable inside one
/// target-and-suite scope, and real databases span several; refusing whenever they do is correct and useless,
/// so each scope is its own comparison and nothing crosses a boundary.
/// </para></summary>
/// <param name="Excluded">Runs left out, with the reason. A comparison that silently dropped rows would be a
/// comparison of an unknown population.</param>
/// <param name="Refusal">Empty when some scope holds two arms. Otherwise why there is nothing to compare —
/// which is a different piece of news from "these cannot be compared".</param>
public sealed record ArmComparisonDto(
    string MetricName,
    IReadOnlyList<ScopedArmsDto> Scopes,
    IReadOnlyList<string> Excluded,
    string Refusal);

/// <param name="RankingRefusal">Empty when these arms may be ranked; otherwise what decided it. The averages
/// are present either way.</param>
public sealed record ScopedArmsDto(
    string TargetCanonical,
    string SuiteStamp,
    IReadOnlyList<ArmAverageDto> Arms,
    string Baseline,
    string RankingRefusal);

/// <param name="Runs">How many runs this average rests on. Beside the leg count because an average over one
/// run and one over five are different evidence about a backend.</param>
public sealed record ArmAverageDto(
    string Arm,
    double Average,
    int Legs,
    int Runs,
    HalfReadingDto Selection,
    HalfReadingDto HeldOut,
    string Proof,
    double Margin);
