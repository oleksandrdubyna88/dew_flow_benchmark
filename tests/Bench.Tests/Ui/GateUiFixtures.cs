using Bench.Contracts;

namespace Bench.Tests.Ui;

/// <summary>Wire shapes for the Gate pages' tests — built as the API would answer them, so a page test pins what the page
/// does with a real answer, figures in their real states.</summary>
internal static class GateUiFixtures
{
    public static readonly GateRubricDto Strict = new("strict-v1", "Strict", new string('a', 64), "strict-v1#aaaaaaaaaaaa", 340);
    public static readonly GateRubricDto Lenient = new("lenient-worth-v1", "LenientWorth", new string('b', 64), "lenient-worth-v1#bbbbbbbbbbbb", 692);

    public static GateScopeDto Scope(string id, string version = "imported from calib-py phase 2 — binary not hashed", bool tasksRecorded = true, params GateRubricDto[] rubrics) =>
        new(id, "gate-seeded#fb80578c897f", "feature", version, string.Empty, new string('c', 64), 84, ["calib-py"], rubrics, tasksRecorded);

    public static GateModelRowDto Row(
        string reviewer, GateFigureDto? seedsHit = null, GateFigureDto? cost = null, GateFigureDto? supported = null, int assessmentFailed = 0) =>
        new(reviewer, 21, 23, 2, 20, 20,
            GateFigureDto.Of(95.2), GateFigureDto.Of(4.1), seedsHit ?? GateFigureDto.Of(1.33), 0, 3, 5, 2,
            80, 50, 10, 15, 5, assessmentFailed, 0,
            supported ?? GateFigureDto.NotHandChecked, GateFigureDto.NotHandChecked,
            GateFigureDto.Of(0.8), GateFigureDto.Of(12.5), GateFigureDto.Of(181.2), GateFigureDto.Of(402.9),
            GateFigureDto.Of(2.1), GateFigureDto.Of(3), GateFigureDto.Of(4), GateFigureDto.Of(6), GateFigureDto.Of(0),
            GateFigureDto.Of(160_000), GateFigureDto.Of(12_000), GateFigureDto.Of(80_000), GateFigureDto.Of(50),
            GateFigureDto.Of(0), 12, GateFigureDto.Unknown, cost ?? GateFigureDto.Of(0.19), GateFigureDto.Of(0.14), GateFigureDto.Of(3.99),
            [new GateFailureCountDto("HttpError", 1)]);

    public static GateModelTableDto Table(
        GateScopeDto scope, GateRubricDto rubric, IReadOnlyList<GateModelRowDto> rows, IReadOnlyList<GateModelRowDto>? calibration = null,
        IReadOnlyList<GateVarianceDto>? variance = null, IReadOnlyList<GatePerTaskRowDto>? perTask = null) =>
        new(scope, rubric.Kind, rubric.Id, rows, calibration ?? [], perTask ?? [], variance ?? [], [.. rows, .. calibration ?? []]);

    public static GateVarianceDto Variance(string task, string state) =>
        new(task, "grok", 2, 2, state, 0, 0, state, 0, 0);

    public static GatePerTaskRowDto PerTask(string task, bool calibration) =>
        new(task, "C#", calibration, "grok", 3, 3, [4, 5, 3], [GateFigureDto.Of(1), GateFigureDto.Unassessed, GateFigureDto.Of(2)],
            [GateFigureDto.Of(2), GateFigureDto.Unknown, GateFigureDto.Of(2)], [181.2, 190, 170], [GateFigureDto.Of(0.19), GateFigureDto.Unknown, GateFigureDto.Of(0.2)]);

    public static GateRunSummaryDto RunSummary(
        string task, int attempt = 1, bool superseded = false, bool taskRecorded = true, bool calibration = false, GateFigureDto? turns = null) =>
        new(Guid.CreateVersion7(), "feature", task, taskRecorded ? "C#" : string.Empty, calibration, taskRecorded, "grok", 1, attempt, superseded,
            "calib-py", !superseded, superseded ? "CallHuman" : "Proceed", 4, turns ?? GateFigureDto.Of(2), 181.2,
            GateFigureDto.Of(0.19), superseded ? "HttpError" : "None", superseded ? "call 2: status 500" : string.Empty,
            "imported from calib-py phase 2 — binary not hashed", string.Empty);
}
