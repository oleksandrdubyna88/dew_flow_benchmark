using System.Text.Json.Nodes;
using Bench.Domain;
using Bench.Domain.Gate;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate.Import;

/// <summary>S5.1, the mapping half: one calibration line → the gate's cell identity and facts, field for field, every
/// "nobody counted" a state rather than a zero. The lines are real ones from the redacted fixture.</summary>
public sealed class CalibRecordTests
{
    private static CalibRecord Read(JsonObject line) => CalibRecords.Parse(line.ToJsonString(), 1, ImportFixture.Names).Ok();

    [Fact]
    public void A_real_calibration_line_round_trips_every_fact()
    {
        var line = ImportFixture.Line("p2-grok-4.7-js3-r1");
        var record = Read(line);
        var facts = record.Facts;

        (record.Id, record.Phase, record.Model, record.Task.Value, record.Repeat, record.Attempt).Should().Be(("p2-grok-4.7-js3-r1", 2, "grok-4.7", "js3", 1, 1));
        record.Preset.Should().Be(new CalibPreset("xai", "medium", 8192, 20, 3, 20));
        record.ProductSha.Should().Be((string)line["product_sha"]!);
        record.DirtyFiles.Value.Should().Be((long)line["product_dirty_files"]!);
        record.Started.Should().Be(DateTimeOffset.Parse((string)line["started"]!, System.Globalization.CultureInfo.InvariantCulture));

        facts.Valid.Should().BeTrue();
        facts.Verdict.Should().Be(GateVerdictWord.Revise);
        facts.ReplyParsed.Should().BeTrue();
        facts.Findings.Value.Should().Be((long)line["findings"]!);
        facts.Turns.Should().Be((int)line["turns"]!);
        facts.HttpCalls.Should().Be((int)line["http_calls"]!);
        facts.FinishReasons.Should().Equal(((JsonArray)line["finish_reasons"]!).Select(n => (string)n!));
        facts.Statuses.Should().Equal(((JsonArray)line["statuses"]!).Select(n => (int)n!));
        facts.TokensIn.Value.Should().Be((long)line["tokens_in"]!);
        facts.TokensOut.Value.Should().Be((long)line["tokens_out"]!);
        facts.TokensCached.Value.Should().Be((long)line["tokens_cached"]!);
        facts.TokensReasoning.Value.Should().Be((long)line["tokens_reasoning"]!);
        facts.SecondsTotal.Should().Be((double)line["seconds_total"]!);
        facts.ReviewSeconds.Should().Be((double)line["review_seconds"]!);
        facts.SecondsPerTurn.Should().Equal(((JsonArray)line["seconds_per_turn"]!).Select(n => (double)n!));
        facts.CachedPerCall.Select(c => c.Value).Should().Equal(((JsonArray)line["cached_per_call"]!).Select(n => (long)n!));
        facts.CostUsd.Value.Should().Be((decimal)(double)line["cost_usd"]!);
        facts.Served.Should().Be((int)line["served_count"]!);
        facts.Refused.Should().Be((int)line["refused_count"]!);
        facts.Failure.Should().Be(FailureCause.None);
        facts.TurnFactsCaptured.Should().BeTrue();
        record.SourceJson.Should().Be(line.ToJsonString());
    }

    [Fact]
    public void A_zero_over_no_ledger_turn_and_every_null_are_not_captured_never_zero()
    {
        var line = ImportFixture.Line("p2-grok-4.7-py3-r1");
        line["turns"] = 0;
        line["tokens_in"] = 0;
        line["tokens_cached"] = 0;

        var facts = Read(line).Facts;

        facts.TokensIn.WasCaptured.Should().BeFalse("Python's sum over no ledger turn is 0, and a zero over nothing reads as 'none'");
        facts.TokensCached.WasCaptured.Should().BeFalse();
        facts.TokensReasoning.WasCaptured.Should().BeFalse("a null reasoning count is not a count");
        facts.CostUsd.WasCaptured.Should().BeFalse("an unknown cost is never free");
        facts.CachedPerCall.Should().OnlyContain(c => !c.WasCaptured);
    }

    [Fact]
    public void A_failed_line_keeps_its_kind_and_a_redacted_sentence()
    {
        var line = ImportFixture.Line("p2-grok-4.7-py3-r1");
        line["failure"] = "call 1: HTTP 500 at https://api.x.ai/v1 for C:\\Users\\someone\\x | contoso-orders said FAILED";

        var failure = Read(line).Facts.Failure;

        failure.Kind.Should().Be(FailureKind.HttpError, "the kind is the FIRST reason's, as failure_cause ordered them");
        failure.Text.Should().NotContain("://").And.NotContain("Users").And.NotContain("contoso-orders");
        CalibFacts.KindOf("[09:09:35 WRN] reviewer x FAILED").Should().Be(FailureKind.TurnFailed);
        CalibFacts.KindOf("call 2: finish_reason=length (out=64)").Should().Be(FailureKind.LengthCut);
        CalibFacts.KindOf("tool answered non-JSON: …").Should().Be(FailureKind.NonJsonReply);
        CalibFacts.KindOf(string.Empty).Should().Be(FailureKind.Unexplained);
    }

    [Fact]
    public void Served_and_refused_follow_the_other_reports_rule_when_the_count_is_zero()
    {
        var line = ImportFixture.Line("p2-grok-4.7-js3-r1");
        line["served_count"] = 0;
        line["refused_count"] = 0;
        line["reviewer_note"] = "turn 2: served a (1-2 of 3), served b (1-2 of 3); refused c";

        var facts = Read(line).Facts;

        facts.Served.Should().Be(2, "report.py reads served_count or note.count('served ')");
        facts.Refused.Should().Be(1);
    }

    [Fact]
    public void A_phase_one_iteration_is_its_own_repeat_and_an_absent_cap_is_the_twenty_minutes_child_env_sent()
    {
        var line = ImportFixture.Line("p2-grok-4.7-js3-r1");
        line["phase"] = 1;
        line["iteration"] = 4;
        line["id"] = "p1-grok-4.7-js3-it04";
        ((JsonObject)line["transport"]!).Remove("capMin");

        var record = Read(line);

        record.Repeat.Should().Be(4);
        record.Preset.CapMinutes.Should().Be(20);
        record.Population.Should().Be("calib-py phase 1");
        record.Pin.Ok().VersionText.Should().Contain("calib-py phase 1").And.NotContain(record.ProductSha);
    }

    [Fact]
    public void The_last_line_of_an_id_supersedes_the_earlier_and_a_torn_line_is_refused_by_number()
    {
        var first = ImportFixture.Line("p2-grok-4.7-js3-r1");
        var rerun = (JsonObject)first.DeepClone();
        rerun["findings"] = 9;

        var latest = CalibRecords.Latest([Read(first), Read(rerun), Read(ImportFixture.Line("p2-grok-4.7-cs2-r1"))]);

        latest.Should().HaveCount(2);
        latest[0].Facts.Findings.Value.Should().Be(9, "latest_by_id: a --force re-run supersedes the earlier line");
        CalibRecords.Parse("{\"id\": \"p2-x", 17, PrivateNames.None).Reason().Should().Contain("line 17").And.Contain("never skipped");
    }

    [Fact]
    public void A_derived_id_is_the_same_for_the_same_key_and_a_version_8_uuid()
    {
        var a = ImportIds.Of("calib-py", "s#1|cell|p2-grok-4.7-js3-r1");

        a.Should().Be(ImportIds.Of("calib-py", "s#1|cell|p2-grok-4.7-js3-r1"));
        a.Should().NotBe(ImportIds.Of("calib-py", "s#1|cell|p2-grok-4.7-js3-r1-a2"));
        a.Should().NotBe(ImportIds.Of("coai-bench", "s#1|cell|p2-grok-4.7-js3-r1"));
        a.Version.Should().Be(8);
        ImportSlug.Of("**GPT-5.6-Luna** *(second run)*").Should().Be("gpt-5-6-luna-second-run");
    }
}
