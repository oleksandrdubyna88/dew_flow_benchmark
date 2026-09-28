using System.Text.Json.Nodes;
using Bench.Domain;
using Bench.Domain.Gate;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate.Import;

/// <summary>S5.3 and S5.4, the mapping halves: a coai-bench record → plan and code stages, and a results table → summary-only
/// numbers. Both inputs are made up in the sources' shapes.</summary>
public sealed class SummaryAndCoaiBenchRecordTests
{
    private const string Document = """
        # RESULTS — a made-up comparison

        ## The table

        Some prose first.

        | model | run 1 | run 2 | mean | sec (1 / 2) | tokens in/out | ~$ per run |
        |---|---|---|---|---|---|---|
        | **Model-A 1.5** | 6 | **8** | **7.0** | 22 / 30 | 289k / 4.2 M | $0.070 |
        | Model-B *(second run)* | 4 | — | *(4)* | 171 / **1056, no answer** | — | electricity |

        ## Next section
        """;

    [Fact]
    public void A_results_table_becomes_numbers_with_a_citation_and_no_text()
    {
        var table = SummaryTables.Parse(Document, "The table").Ok();

        table.Section.Should().Be("the-table");
        table.Rows.Select(r => r.Label).Should().Equal("model-a-1-5", "model-b-second-run");
        var a = table.Rows[0].Figures.ToDictionary(f => f.Metric);
        a["run-2"].Value.Should().Be(8);
        a["mean"].Value.Should().Be(7.0);
        a["sec-1-2-part1"].Value.Should().Be(22);
        a["sec-1-2-part2"].Value.Should().Be(30);
        a["tokens-in-out-part1"].Value.Should().Be(289_000);
        a["tokens-in-out-part2"].Value.Should().Be(4_200_000);
        a["per-run"].Value.Should().Be(0.07);

        var b = table.Rows[1].Figures.ToDictionary(f => f.Metric);
        b["run-2"].Captured.Should().BeFalse("a dash is not a zero");
        b["per-run"].Captured.Should().BeFalse("'electricity' is not a price");
        b["sec-1-2-part2"].Value.Should().Be(1056, "a thousands separator is not a decimal");
    }

    [Fact]
    public void A_table_whose_columns_read_alike_or_a_heading_that_is_not_there_is_refused()
    {
        SummaryTables.Parse("## T\n| model | Run 1 | run-1 |\n|---|---|---|\n| a | 1 | 2 |\n", "T").Reason().Should().Contain("both read as metric 'run-1'");
        SummaryTables.Parse(Document, "Nowhere").Reason().Should().Contain("no heading");
        SummaryTables.Number("`939175d`").Should().BeNull("a short sha is not a number");
        SummaryTables.Number("70 %").Should().Be(70);
    }

    private static JsonObject Record(string useful = "yes") => (JsonObject)JsonNode.Parse($$"""
        {"case": {"name": "made-up-case", "planFile": "docs/plan.md", "commit": "1234567", "baseRef": "abcdef0"},
         "arm": "codex,gemini", "repeat": 2, "lane": 1, "startedUtc": "2026-09-05T20:39:22.2856054Z", "judgedBy": "claude-opus-5",
         "stages": [
          {"stage": "plan-1", "seconds": 50.8, "verdict": "proceed", "error": "", "gatingCount": 1, "reviewers": "all 2 reviewers answered",
           "tokensIn": 15282, "tokensOut": 0, "costUsd": null, "commands": [], "resolveRefused": "",
           "findings": [
             {"severity": "Major", "category": "Security", "file": "", "line": 0, "title": "t0", "why": "w", "fix": "f", "isGating": true, "useful": "{{useful}}", "verdict": "the judge's reason"},
             {"severity": "Minor", "category": "Clarity", "file": "a.cs", "line": 3, "title": "t1", "why": "w", "fix": "f", "isGating": false, "useful": "unjudged", "verdict": ""}]},
          {"stage": "code", "seconds": 90.0, "verdict": "good_enough", "error": "", "tokensIn": 1, "tokensOut": 2, "costUsd": 0.5, "findings": []}]}
        """)!;

    [Fact]
    public void A_coai_bench_record_becomes_a_plan_stage_and_a_code_stage_keyed_by_the_record()
    {
        var stages = CoaiBenchRecords.Parse(new JsonArray(Record()).ToJsonString(), PrivateNames.None).Ok();

        stages.Select(s => s.Gate).Should().Equal(GateKind.Plan, GateKind.Code);
        stages[0].Key.Should().Be("codex,gemini|made-up-case|2|2026-09-05T20:39:22.2856054Z|plan-1");
        stages[0].Reviewer.Ok().Value.Should().Be("coai-bench-codex-gemini", "an arm is a vendor set — named for the set, no model invented");
        stages[0].Worth.Should().Equal(WorthWord.Yes, WorthWord.Unjudged);
        stages[0].Findings.Should().HaveCount(2);
        stages[0].Findings[0].Json.Should().NotContain("useful").And.NotContain("the judge", "a finding's text is the reviewer's, not the judge's");
        stages[0].RunJson.Should().NotContain("the judge", "a later judge pass is not a changed run");
        stages[0].SourceJson.Should().Contain("the judge", "the stage is kept whole in the artefact root");
    }

    [Fact]
    public void What_coai_bench_never_recorded_is_not_captured_and_the_gate_valid_rule_applies()
    {
        var stages = CoaiBenchRecords.Parse(new JsonArray(Record()).ToJsonString(), PrivateNames.None).Ok();
        var plan = stages[0].Facts;
        var code = stages[1].Facts;

        plan.TurnFactsCaptured.Should().BeFalse("coai-bench kept no ledger: its zero turns are not a count");
        plan.TokensIn.Value.Should().Be(15282);
        plan.TokensOut.WasCaptured.Should().BeFalse("a zero from the cost block is a CLI that reported nothing");
        plan.TokensCached.WasCaptured.Should().BeFalse();
        plan.CostUsd.WasCaptured.Should().BeFalse();
        plan.Valid.Should().BeTrue();
        code.Valid.Should().BeFalse("good_enough is not proceed or revise — the rule a native plan or code cell is judged by");
        code.Failure.Kind.Should().Be(FailureKind.VerdictNotPassing);
        code.CostUsd.Value.Should().Be(0.5m);
    }

    [Fact]
    public void An_unknown_stage_is_refused_by_name_and_a_judge_is_matched_against_the_arms_clis()
    {
        var record = Record();
        ((JsonObject)((JsonArray)record["stages"]!)[1]!)["stage"] = "document";

        CoaiBenchRecords.Parse(new JsonArray(record).ToJsonString(), PrivateNames.None).Reason().Should().Contain("stage 'document'");
        VendorFamily.MatchesAnyOf("claude-opus-5", ["codex", "gemini", "local"]).Should().BeFalse();
        VendorFamily.MatchesAnyOf("gpt-6-astra", ["codex", "local"]).Should().BeTrue("codex is OpenAI's CLI");
        VendorFamily.MatchesAnyOf("claude-opus-5", ["local"]).Should().BeFalse("a word no family is known for never matches");
    }
}
