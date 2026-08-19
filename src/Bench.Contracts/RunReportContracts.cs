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
    IReadOnlyList<string> Warnings);

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

/// <param name="Scored">Legs of this run that carry a result.</param>
public sealed record RunSummaryDto(
    Guid RunId,
    string Label,
    string TargetCanonical,
    string SuiteStamp,
    string EngineCanonical,
    string Status,
    DateTimeOffset CreatedAt);
