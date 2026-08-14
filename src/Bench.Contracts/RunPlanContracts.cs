namespace Bench.Contracts;

/// <summary>The wire shape of a planned run: what would execute, in what order, and which half of the
/// suite each question belongs to. Emitted by the CLI as JSON and by the API as a response body — one
/// shape, so an agent reading the CLI and a browser reading the API never see different truths.</summary>
public sealed record RunPlanDto(
    string TargetCanonical,
    string SuiteStamp,
    string EngineCanonical,
    int QuestionCount,
    int Repeats,
    int LegCount,
    IReadOnlyList<PlannedCellDto> Cells,
    IReadOnlyDictionary<string, int> FirstPositionCounts,
    IReadOnlyDictionary<string, string> SplitHalves,
    IReadOnlyList<string> Warnings);

public sealed record PlannedCellDto(string QuestionId, int Repeat, string Leg, int Position);
