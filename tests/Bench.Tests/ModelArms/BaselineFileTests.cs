using System.Text.Json.Nodes;
using Bench.Domain;
using Bench.Domain.Targets;
using FluentAssertions;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The baseline is what the target's two suites failed BEFORE any arm touched it. Absent is not zero: a baseline with an empty
/// failure list claims the suite passed, so one is composed only from runs that completed, and read back only whole.
/// <para>
/// What this does NOT prove: that the failures listed are the failures that happened (the caller measures them; this composes and
/// reads), or that <c>prep-base</c> writes the file (E4.S4).
/// </para></summary>
public sealed class BaselineFileTests
{
    private const string Sha = "6786582e30d3e771549d05ed719478396f8099cd";
    private static readonly DateTimeOffset At = new(2026, 10, 29, 12, 30, 15, TimeSpan.Zero);

    private static CommitSha BaseSha() => CommitSha.Parse(Sha).Ok();

    private static SuiteRun Ran(int total, params string[] failed) => new(true, failed.Length == 0 ? 0 : 1, failed, total);

    private static Baseline Composed() =>
        BaselineFile.Compose(BaseSha(), At, "wsl-ubuntu", Ran(1200, "Mcp.A", "Mcp.B"), Ran(300, "ts one"), "measured twice").Ok();

    private static string Edited(Action<JsonObject> edit)
    {
        var node = JsonNode.Parse(BaselineFile.Write(Composed()))!.AsObject();
        edit(node);
        return node.ToJsonString();
    }

    [Fact]
    public void A_baseline_round_trips_its_shape_with_the_shells_failure_keys()
    {
        var baseline = Composed();
        var json = BaselineFile.Write(baseline);

        var node = JsonNode.Parse(json)!.AsObject();
        node.Select(m => m.Key).Should().Equal("schema", "base", "measuredAt", "host", "mcpFailed", "tsFailed", "suiteTotal", "extensionTotal", "note");
        node["schema"]!.GetValue<string>().Should().Be("model-arms/baseline-v1");
        node["base"]!.GetValue<string>().Should().Be(Sha);
        node["measuredAt"]!.GetValue<string>().Should().Be("2026-10-29T12:30:15.0000000Z", "an instant is written in UTC, ending in Z");
        node["mcpFailed"]!.AsArray().Select(n => n!.GetValue<string>()).Should().Equal("Mcp.A", "Mcp.B");
        node["tsFailed"]!.AsArray().Select(n => n!.GetValue<string>()).Should().Equal("ts one");
        BaselineFile.Read(json).Ok().Should().BeEquivalentTo(baseline);
    }

    [Fact]
    public void An_instant_with_an_offset_is_written_as_the_same_instant_in_utc()
    {
        var local = new DateTimeOffset(2026, 10, 29, 14, 30, 15, TimeSpan.FromHours(2));

        var baseline = BaselineFile.Compose(BaseSha(), local, "h", Ran(1), Ran(1), string.Empty).Ok();

        BaselineFile.Read(BaselineFile.Write(baseline)).Ok().MeasuredAt.Should().Be(At);
        BaselineFile.Write(baseline).Should().Contain("2026-10-29T12:30:15.0000000Z");
    }

    [Fact]
    public void A_note_may_be_empty_but_must_be_present()
    {
        var empty = BaselineFile.Compose(BaseSha(), At, "h", Ran(1), Ran(1), string.Empty).Ok();

        BaselineFile.Read(BaselineFile.Write(empty)).Ok().Note.Should().BeEmpty();
        BaselineFile.Read(Edited(n => n.Remove("note"))).Reason().Should().Contain("note is missing");
    }

    [Fact]
    public void A_suite_that_did_not_run_to_completion_refuses_the_baseline()
    {
        var reason = BaselineFile.Compose(BaseSha(), At, "h", new SuiteRun(false, 137, [], 0), Ran(1), string.Empty).Reason();

        reason.Should().Contain("absent is not zero").And.Contain("the main suite did not run to completion");
    }

    [Fact]
    public void An_extension_run_that_did_not_complete_is_refused_and_both_are_named_when_both_failed()
    {
        var extension = BaselineFile.Compose(BaseSha(), At, "h", Ran(1), new SuiteRun(false, 1, [], 0), string.Empty).Reason();
        var both = BaselineFile.Compose(BaseSha(), At, "h", new SuiteRun(false, 1, [], 0), new SuiteRun(false, 1, [], 0), string.Empty).Reason();

        extension.Should().Contain("the extension suite did not run to completion").And.NotContain("the main suite");
        both.Split('\n').Should().HaveCount(2);
    }

    [Fact]
    public void A_negative_total_or_a_blank_host_is_refused_at_composition()
    {
        BaselineFile.Compose(BaseSha(), At, "h", Ran(-1), Ran(1), string.Empty).Reason().Should().Contain("total");
        BaselineFile.Compose(BaseSha(), At, "  ", Ran(1), Ran(1), string.Empty).Reason().Should().Contain("host");
    }

    [Fact]
    public void An_unknown_member_or_another_schema_is_refused()
    {
        BaselineFile.Read(Edited(n => n["extra"] = 1)).Reason().Should().Contain("extra").And.Contain("not a member this build knows");
        BaselineFile.Read(Edited(n => n["schema"] = "x")).Reason().Should().Contain("schema").And.Contain("model-arms/baseline-v1");
    }

    [Fact]
    public void Every_problem_comes_back_at_once()
    {
        var reason = BaselineFile.Read(Edited(n =>
        {
            n.Remove("host");
            n["base"] = "abc";
            n["measuredAt"] = "yesterday";
            n["suiteTotal"] = -3;
            n["mcpFailed"] = "all";
        })).Reason();

        var lines = reason.Split('\n');
        lines.Should().Contain(l => l.Contains("host is missing", StringComparison.Ordinal));
        lines.Should().Contain(l => l.Contains("base", StringComparison.Ordinal) && l.Contains("abbreviations", StringComparison.Ordinal));
        lines.Should().Contain(l => l.Contains("measuredAt", StringComparison.Ordinal));
        lines.Should().Contain(l => l.Contains("suiteTotal", StringComparison.Ordinal));
        lines.Should().Contain(l => l.Contains("mcpFailed must be an array of text", StringComparison.Ordinal));
    }

    [Fact]
    public void An_instant_with_an_offset_is_refused_on_read_because_a_stored_instant_is_utc()
    {
        BaselineFile.Read(Edited(n => n["measuredAt"] = "2026-10-29T14:30:15.0000000+02:00")).Reason().Should().Contain("measuredAt").And.Contain("UTC");
    }

    [Fact]
    public void Text_that_is_not_a_json_object_is_refused()
    {
        BaselineFile.Read("[]").Reason().Should().Contain("baseline.json must be a JSON object");
        BaselineFile.Read("{").Reason().Should().Contain("baseline.json is not JSON");
    }
}
