using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Targets;

namespace Bench.Tests.Gate.Assessment;

/// <summary>What the assessment tests build again and again, through the real factories — a row the code would refuse
/// cannot quietly turn a test into an assertion about the empty input.</summary>
internal static class AssessmentFixtures
{
    public static readonly GateTaskId Cs2 = GateTaskId.Parse("cs2").Ok();
    public static readonly GateTaskId Rs3 = GateTaskId.Parse("rs3").Ok();
    public static readonly GateReviewerId Grok = GateReviewerId.Parse("grok-medium").Ok();
    public static readonly GateReviewerId Codex = GateReviewerId.Parse("codex-astra").Ok();
    public static readonly CommitSha Base = CommitSha.Parse(new string('1', 40)).Ok();
    public static readonly CommitSha Head = CommitSha.Parse(new string('2', 40)).Ok();

    public static readonly TaskEvidence Evidence = new("C:/checkouts/worktrees/aa11/2222", Base, Head, "C:/artefacts/assess/seeds/cs2.json");

    public static BlindedId Id(string hex) => BlindedId.Parse(hex).Ok();

    public static SeedId Seed(string id) => SeedId.Parse(id).Ok();

    public static string FindingJson(int n) =>
        $$"""{"severity":"major","category":"Reliability","file":"src/Orders/Export{{n}}.cs","line":{{10 + n}},"title":"finding {{n}}","why":"because {{n}}","fix":"fix {{n}}","isGating":true}""";

    public static AssessmentRow Row(string hex, GateTaskId? task = null) =>
        AssessmentRow.Of(Id(hex), task ?? Cs2, FindingJson(0), Evidence);

    /// <summary>One schema-shaped answer row, as the assessor writes it.</summary>
    public static string AnswerRow(string id, string task = "cs2", string verdict = "supported", string seed = "none", string value = "high") =>
        $$"""{"id":"{{id}}","task":"{{task}}","verdict":"{{verdict}}","value":"{{value}}","severity_fair":"yes","grounded":"yes","cluster":"{{task}}:null-export","seed_hit":"{{seed}}","note":"src/Orders/Export0.cs:10 reads the null"}""";

    public static string Answer(params string[] rows) => $$"""{"rows":[{{string.Join(",", rows)}}]}""";
}
