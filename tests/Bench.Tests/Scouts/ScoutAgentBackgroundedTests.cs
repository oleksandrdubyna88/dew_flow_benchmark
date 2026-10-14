using System.Text.Json.Nodes;
using Bench.Domain.Probes;
using Bench.Domain.Scouts;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>E2 S8's third instrument correction — the pilot's FIRST cell (2026-10-14). Under the scout launch (the allow-list, the pinned
/// plugin, <c>--forward-subagent-text</c>) claude 2.1.289 ran the Agent call in the BACKGROUND: its result came back at once as
/// <c>status: async_launched</c> with no <c>totalDurationMs</c> and no usage, the child's answer arrived later in a
/// <c>system/task_notification</c>, and the main session took a second turn with a second <c>result</c> envelope. The instrument reads a
/// SYNCHRONOUS Agent result — the cost check stopped the cell (CostMismatch), and the time and the answer would have been wrong too.
/// Measured with a cheap probe the same night: the same launch backgrounded the agent; with <c>CLAUDE_CODE_DISABLE_BACKGROUND_TASKS=1</c> it
/// ran synchronously (<c>completed</c>, its duration and usage, one envelope); a plain launch was synchronous either way.
/// So every scout launch carries that variable, and a cell whose agent was backgrounded anyway STOPS by name
/// (<see cref="ScoutReason.AgentBackgrounded"/>) — a silent change of mode can never be scored. The live fixture is that probe's stream,
/// scrubbed (<c>claude-2.1.289-agent-backgrounded.ndjson</c>).</summary>
public sealed class ScoutAgentBackgroundedTests
{
    private const string Tree = "C:/work/tree";

    private static readonly ScoutTask Task = ScoutTask.Frozen(
        ScoutTaskId.Parse("t01").Ok(), "example-org/example-repo", Bench.Domain.Targets.CommitSha.Parse(new string('a', 40)).Ok(),
        Bench.Domain.Targets.CommitSha.Parse(new string('b', 40)).Ok(), ScoutQuestionKind.HowEndToEnd, "How does a cell reach its settlement after a crash?",
        ["src/Alpha.cs", "src/Beta.cs", "src/Gamma.cs"], [], [], new string('c', 64)).Ok();

    private static readonly IReadOnlyList<string> Files = ["README.md", "src/Alpha.cs", "src/Beta.cs", "src/Gamma.cs", "src/Other.cs"];

    [Fact]
    public void The_live_backgrounded_stream_shows_an_async_launched_agent_and_two_envelopes()
    {
        var stream = ScoutStreams.Fixture("agent-backgrounded");
        var transcript = ClaudeSubagents.Read(stream);

        transcript.Agents.Should().ContainSingle().Which.Result.Status.Should().Be(ScoutCellReading.AsyncLaunched);
        transcript.Agents[0].Result.TotalDurationMs.WasCaptured.Should().BeFalse("a backgrounded agent's result carries no totalDurationMs");
        stream.Split('\n').Count(l => l.Length > 0 && JsonNode.Parse(l)?["type"]?.GetValue<string>() == "result").Should().Be(2, "the main session took a second turn");
    }

    [Fact]
    public void The_live_backgrounded_stream_read_as_a_cell_stops_it_naming_the_mode()
    {
        var stream = ScoutStreams.Fixture("agent-backgrounded");
        var verdict = ScoutCellReading.Read(ClaudeSubagents.Read(stream), stream, Context(ScoutArms.Of(ScoutArmId.A1)), LinkTargets.None);

        verdict.Settlement.Should().Match<ScoutSettlement>(s => s.Outcome == ScoutCellOutcome.Stopped && s.Reason == ScoutReason.AgentBackgrounded);
    }

    [Fact]
    public void A_backgrounded_agent_planted_in_a_clean_cell_stops_it()
    {
        var arm = ScoutArms.Of(ScoutArmId.B2);
        var clean = ScoutCellStreams.For(Task, arm, tree: Tree);
        var planted = ScoutStreams.Edit(clean, Backgrounded(ScoutStreams.AgentId(1)));

        Read(arm, clean).Settlement.Outcome.Should().Be(ScoutCellOutcome.Scored, "the same cell with synchronous agents scores");
        var verdict = Read(arm, planted);

        verdict.Settlement.Should().Match<ScoutSettlement>(s => s.Outcome == ScoutCellOutcome.Stopped && s.Reason == ScoutReason.AgentBackgrounded);
        verdict.Settlement.Facts.RecallFound.WasCaptured.Should().BeFalse("a stopped cell carries no recall");
    }

    [Fact]
    public void The_backgrounded_reason_is_a_stop()
    {
        ScoutReasons.IsStop(ScoutReason.AgentBackgrounded).Should().BeTrue();
        ScoutReasons.IsVoid(ScoutReason.AgentBackgrounded).Should().BeFalse();
    }

    [Fact]
    public void Every_scout_launch_turns_background_tasks_off_and_a_probe_launch_is_unchanged()
    {
        var parent = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PATH"] = "/usr/bin",
            [ProbeChildEnvironment.BackgroundTasksVariable] = "0",
        };

        var scout = ProbeChildEnvironment.ForScouts(parent).Variables.ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);
        var probe = ProbeChildEnvironment.Of(parent).Variables.Select(v => v.Key).ToList();

        scout.Should().Contain(ProbeChildEnvironment.BackgroundTasksVariable, "1", "a fixed policy value, whatever the parent holds");
        scout.Should().Contain(ProbeChildEnvironment.ModelGuardVariable, ProbeChildEnvironment.ModelGuardOff);
        probe.Should().NotContain(ProbeChildEnvironment.BackgroundTasksVariable, "the probes measure the CLI as it is");
    }

    /// <summary>The Agent call <paramref name="agentId"/>'s result as the backgrounded launch printed it: <c>async_launched</c>, no duration, no usage.</summary>
    private static Func<JsonObject, JsonObject> Backgrounded(string agentId) => line =>
    {
        if (line["tool_use_result"] is JsonObject report && line["message"]?["content"] is JsonArray content
            && content.Any(b => b?["tool_use_id"]?.GetValue<string>() == agentId))
        {
            report["status"] = ScoutCellReading.AsyncLaunched;
            report["isAsync"] = true;
            report.Remove("totalDurationMs");
            report.Remove("usage");
        }

        return line;
    };

    private static ScoutCellVerdict Read(ScoutArm arm, string stream) =>
        ScoutCellReading.Read(ClaudeSubagents.Read(stream), stream, Context(arm), LinkTargets.None);

    private static ScoutCellContext Context(ScoutArm arm) =>
        new(new ReadBackExpectation(arm, ScoutPrompts.ForCell(Task, arm).Sha256s, arm.Model == ScoutModel.Opus ? ScoutStreams.Opus : ScoutStreams.Sonnet, Tree),
            ScoutStoreFixtures.Pins(), ScoutCellStreams.Prices, Task.GroundTruth, Files, string.Empty);
}
