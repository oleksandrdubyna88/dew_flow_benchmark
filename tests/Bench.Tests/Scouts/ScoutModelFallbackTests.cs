using Bench.Domain.Probes;
using Bench.Domain.Scouts;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>E2 S8's first instrument correction (S5's live finding): claude 2.1.289 continues a session on ANOTHER model after the API's
/// safeguards refuse a request — the stream carries a <c>system/model_refusal_fallback</c> event naming both models, while <c>init.model</c>
/// still names the original. A cell whose main session or any child fell back was not measured on the pinned models, so it STOPS as a moved
/// pin does, with a reason code naming the event (<see cref="ScoutReason.ModelRefusalFallback"/>) and never a model id. The live fixture is
/// S5's refused access-check launch (<c>claude-2.1.289-model-refusal-fallback.ndjson</c>, scrubbed). Written RED first: the reader read the
/// fallback stream without seeing the event and a clean cell with a fallback planted in it SCORED.</summary>
public sealed class ScoutModelFallbackTests
{
    private const string Tree = "C:/work/tree";

    private static readonly ScoutTask Task = ScoutTask.Frozen(
        ScoutTaskId.Parse("t01").Ok(), "example-org/example-repo", Bench.Domain.Targets.CommitSha.Parse(new string('a', 40)).Ok(),
        Bench.Domain.Targets.CommitSha.Parse(new string('b', 40)).Ok(), ScoutQuestionKind.HowEndToEnd, "How does a cell reach its settlement after a crash?",
        ["src/Alpha.cs", "src/Beta.cs", "src/Gamma.cs"], [], [], new string('c', 64)).Ok();

    private static readonly IReadOnlyList<string> Files = ["README.md", "src/Alpha.cs", "src/Beta.cs", "src/Gamma.cs", "src/Other.cs"];

    [Fact]
    public void The_live_refused_stream_carries_its_fallback_while_init_model_still_names_the_original()
    {
        var transcript = ClaudeSubagents.Read(ScoutStreams.Fixture("model-refusal-fallback"));

        transcript.MainModel.Should().Be(ScoutStreams.Opus, "init.model does not say the session moved — which is why the event must be read");
        transcript.Fallbacks.Should().Equal(new ModelFallback(string.Empty, ScoutStreams.Opus, "claude-opus-4-8", "cyber"));
        transcript.Fallbacks[0].InMainSession.Should().BeTrue();
        ScoutPinDrift.FellBack(transcript).Should().BeTrue();
    }

    [Fact]
    public void The_live_refused_stream_read_as_a_cell_stops_it_naming_the_event()
    {
        var stream = ScoutStreams.Fixture("model-refusal-fallback");
        var verdict = ScoutCellReading.Read(ClaudeSubagents.Read(stream), stream, Context(ScoutArms.Of(ScoutArmId.A1)), LinkTargets.None);

        verdict.Settlement.Should().Match<ScoutSettlement>(s => s.Outcome == ScoutCellOutcome.Stopped && s.Reason == ScoutReason.ModelRefusalFallback,
            "a session that answered on a model nobody chose is a moved pin, whatever else the stream says");
        verdict.ReportedUsd.WasCaptured.Should().BeTrue("the money the CLI reported is still what the attempt settles with");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_fallback_planted_in_a_clean_cell_stops_it_in_the_main_session_and_in_a_child(bool inChild)
    {
        var arm = ScoutArms.Of(ScoutArmId.B2);
        var clean = ScoutCellStreams.For(Task, arm, tree: Tree);
        var planted = Plant(clean, inChild ? ScoutStreams.AgentId(1) : null);

        Read(arm, clean).Settlement.Outcome.Should().Be(ScoutCellOutcome.Scored, "the same cell without the event scores");
        var verdict = Read(arm, planted);

        verdict.Settlement.Should().Match<ScoutSettlement>(s => s.Outcome == ScoutCellOutcome.Stopped && s.Reason == ScoutReason.ModelRefusalFallback);
        verdict.Settlement.Facts.RecallFound.WasCaptured.Should().BeFalse("a stopped cell carries no recall");
        ClaudeSubagents.Read(planted).Fallbacks.Should().ContainSingle().Which.InMainSession.Should().Be(!inChild);
    }

    [Fact]
    public void The_fallback_reason_is_a_stop_and_a_word_never_a_model_id()
    {
        ScoutReasons.IsStop(ScoutReason.ModelRefusalFallback).Should().BeTrue();
        ScoutReasons.IsVoid(ScoutReason.ModelRefusalFallback).Should().BeFalse();
        ScoutReason.ModelRefusalFallback.ToString().Should().NotContain("claude");
    }

    [Fact]
    public void A_stream_without_the_event_has_no_fallback()
    {
        var transcript = ClaudeSubagents.Read(ScoutStreams.Fixture("two-children-models"));

        transcript.Fallbacks.Should().BeEmpty();
        ScoutPinDrift.FellBack(transcript).Should().BeFalse();
    }

    /// <summary>The live event's shape, inserted after the init line (the main session) or under an Agent call's id (a child).</summary>
    private static string Plant(string stream, string? parent)
    {
        var parentJson = parent is null ? "null" : $"\"{parent}\"";
        var fallback = "{\"type\":\"system\",\"subtype\":\"model_refusal_fallback\",\"trigger\":\"refusal\",\"direction\":\"retry\",\"scope\":\"session\","
            + "\"original_model\":\"claude-sonnet-5-5\",\"fallback_model\":\"claude-sonnet-4-8\",\"api_refusal_category\":\"cyber\",\"parent_tool_use_id\":" + parentJson + "}";
        var lines = stream.TrimEnd('\n').Split('\n').ToList();
        lines.Insert(parent is null ? 1 : lines.Count - 1, fallback);
        return string.Join('\n', lines) + "\n";
    }

    private static ScoutCellVerdict Read(ScoutArm arm, string stream) =>
        ScoutCellReading.Read(ClaudeSubagents.Read(stream), stream, Context(arm), LinkTargets.None);

    private static ScoutCellContext Context(ScoutArm arm) =>
        new(new ReadBackExpectation(arm, ScoutPrompts.ForCell(Task, arm).Sha256s, arm.Model == ScoutModel.Opus ? ScoutStreams.Opus : ScoutStreams.Sonnet, Tree),
            ScoutStoreFixtures.Pins(), ScoutCellStreams.Prices, Task.GroundTruth, Files, string.Empty);
}
