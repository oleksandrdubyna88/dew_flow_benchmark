using System.Text.Json.Nodes;
using Bench.Domain.Probes;
using Bench.Domain.Scouts;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Scouts.ScoutStreams;

namespace Bench.Tests.Scouts;

/// <summary><see cref="ClaudeSubagents"/> on the LIVE 2026-10-12 probe stream (claude 2.1.289, <c>Fixtures/scouts/</c>): a main Opus
/// session that launched two <c>feature-dev:code-explorer</c> children in one message, one asked for <c>sonnet</c> and one for
/// <c>opus</c>. The numbers asserted here were read off the stream and its envelope by hand — they are the reader's, checked against
/// the CLI's own <c>modelUsage</c>, not guessed.</summary>
public sealed class ClaudeSubagentsTests
{
    private static readonly SubagentTranscript TwoChildren = ClaudeSubagents.Read(Fixture("two-children-models"));

    [Fact]
    public void The_two_children_are_told_apart_by_their_parent_call_with_the_right_models_and_durations()
    {
        TwoChildren.Complete.Should().BeTrue();
        (TwoChildren.MainModel, TwoChildren.CliVersion).Should().Be((Opus, "2.1.289"));
        TwoChildren.Agents.Select(a => (a.InputModel, a.SubagentType, a.Result.ResolvedModel, a.Result.TotalDurationMs.Value)).Should().Equal(
            ("sonnet", ScoutArms.CodeExplorerType, Sonnet, 3614L),
            ("opus", ScoutArms.CodeExplorerType, Opus, 19815L));
        TwoChildren.Agents.Select(a => a.Child.MessageModels).Should().BeEquivalentTo(new[] { new[] { Sonnet }, new[] { Opus } }, o => o.WithStrictOrdering(),
            "every child event names its parent call, so the Opus child's messages never land on the Sonnet child");
        TwoChildren.Agents[0].Child.Tools.Select(t => t.Name).Should().Equal("Glob");
        TwoChildren.Agents[1].Child.Tools.Select(t => t.Name).Should().Equal("Read", "Read", "Glob", "Glob", "Glob", "Glob", "Glob", "Glob", "Glob");
        TwoChildren.MainCalls.Select(c => c.Name).Should().Equal("Agent", "Agent");
        TwoChildren.Agents.Should().OnlyContain(a => a.PromptSha256 == Hash(a.Prompt) && a.Prompt.Length > 0);
    }

    /// <summary>The probe's unpredicted finding: usage repeats on every event of a message, so it is summed per child DEDUPLICATED by
    /// message id. Checked against the envelope: the Sonnet child alone equals <c>modelUsage[sonnet]</c>, and the Opus child plus the
    /// main session equal <c>modelUsage[opus]</c> — to the token, for input, cache read and cache write.</summary>
    [Fact]
    public void Child_usage_deduplicated_by_message_id_matches_the_envelopes_model_usage_to_the_token()
    {
        var sonnet = TwoChildren.Agents[0].Child.StreamUsage;
        var opus = TwoChildren.Agents[1].Child.StreamUsage;
        var main = TwoChildren.MainStreamUsage;
        var lines = TwoChildren.Envelope.ModelUsage.ToDictionary(m => m.Model);

        (sonnet.Input, sonnet.CacheRead, sonnet.CacheWrite).Should().Be((lines[Sonnet].Input, lines[Sonnet].CacheRead, lines[Sonnet].CacheCreation));
        (sonnet.Input, sonnet.CacheRead, sonnet.CacheWrite5m, sonnet.CacheWrite1h).Should().Be((4L, 5951L, 8384L, 0L));
        (opus.Input + main.Input, opus.CacheRead + main.CacheRead, opus.CacheWrite + main.CacheWrite)
            .Should().Be((lines[Opus].Input, lines[Opus].CacheRead, lines[Opus].CacheCreation));
        (opus.Input, opus.CacheRead, opus.CacheWrite).Should().Be((10L, 79310L, 28277L));
        (main.CacheWrite1h, main.CacheWrite5m).Should().Be((45937L, 0L), "the main session's cache writes are 1-hour, the children's 5-minute — priced differently");
    }

    /// <summary>The naive sum the deduplication exists for: the Opus child printed 15 assistant events for 5 messages, and summing every
    /// event would count its input three times over.</summary>
    [Fact]
    public void The_naive_per_event_sum_would_count_a_messages_usage_once_per_content_block_and_that_is_the_point()
    {
        var events = Fixture("two-children-models").Split('\n').Where(l => l.Trim().Length > 0).Select(l => JsonNode.Parse(l)!.AsObject())
            .Where(e => (string?)e["type"] == "assistant" && (string?)e["parent_tool_use_id"] == TwoChildren.Agents[1].ToolUseId)
            .ToList();

        events.Should().HaveCount(15);
        events.Sum(e => (long)e["message"]!["usage"]!["input_tokens"]!).Should().Be(30, "naive: every event's usage added");
        TwoChildren.Agents[1].Child.StreamUsage.Input.Should().Be(10, "deduplicated: five messages, two input tokens each");
    }

    /// <summary>Not predicted by the plan, measured on the same stream: a message that called a tool streams its usage as of its START,
    /// so <c>output_tokens</c> there is a placeholder. The Sonnet child streamed 16 + 22 and was billed 118; the Opus child 35 against
    /// 1,442. <see cref="ScoutTokens"/> therefore takes a team's output from the envelope.</summary>
    [Fact]
    public void Streamed_output_tokens_are_placeholders_and_the_team_output_comes_from_the_envelope()
    {
        var lines = TwoChildren.Envelope.ModelUsage.ToDictionary(m => m.Model);

        TwoChildren.Agents[0].Child.StreamUsage.Output.Should().Be(38).And.NotBe(lines[Sonnet].Output);
        TwoChildren.Agents[1].Child.StreamUsage.Output.Should().Be(35);

        var usage = ScoutTokens.Attribute(TwoChildren).Ok();

        usage.Teams.Single(t => t.Model == Sonnet).Usage.Output.Should().Be(118, "modelUsage[sonnet] — no main session on Sonnet");
        usage.Teams.Single(t => t.Model == Opus).Usage.Output.Should().Be(1798 - 356, "modelUsage[opus] minus the main session's own output (result.usage)");
        usage.Scouts.Should().OnlyContain(s => s.OutputSeparable, "each model has exactly one scout here");
        usage.Internal.Select(i => i.Model).Should().Equal(["claude-haiku-4-5-20251001"], "the CLI's own Haiku call belongs to neither the main session nor a scout");
        usage.Main.Should().Be(TwoChildren.Envelope.MainUsage);
    }

    [Fact]
    public void The_envelope_and_the_team_time_are_read()
    {
        TwoChildren.Envelope.Captured.Should().BeTrue();
        (TwoChildren.Envelope.DurationMs.Value, TwoChildren.Envelope.TotalCostUsd.Value).Should().Be((24556L, 0.594582m));
        TwoChildren.Envelope.Stats.Should().Be(new SubagentStats(true, 2, 2, 0, 0));
        TwoChildren.TeamMs.Should().Be(Bench.Domain.Trace.CapturedCount.Number(20988),
            "the first Agent tool_use at 10:21:28.539 to the last Agent tool_result at 10:21:49.527");
        TwoChildren.Agents.Select(a => a.Result.FinalText).Should().OnlyContain(t => t.Length > 0 && !t.StartsWith("[Subagent hand-back]", StringComparison.Ordinal),
            "the report is tool_use_result's content — the text the child wrote, without the frame the harness adds");
    }

    [Fact]
    public void A_stream_with_its_result_cut_off_reads_as_incomplete_and_its_tokens_cannot_be_attributed()
    {
        var cut = ClaudeSubagents.Read(Truncated(Fixture("two-children-models")));

        cut.Complete.Should().BeFalse();
        cut.Envelope.Should().Be(ResultEnvelope.NotCaptured);
        cut.Envelope.Stats.Captured.Should().BeFalse("no stats is not zero stats");
        cut.Agents.Should().HaveCount(2, "what the stream did show is still read");
        ScoutTokens.Attribute(cut).Reason().Should().Contain("no result envelope");
    }

    [Fact]
    public void A_child_whose_result_carries_no_resolved_model_is_read_not_thrown_on()
    {
        var edited = Edit(Fixture("two-children-models"), line =>
        {
            if (line["tool_use_result"] is JsonObject report)
            {
                report.Remove("resolvedModel");
            }

            return line;
        });

        var transcript = ClaudeSubagents.Read(edited);

        transcript.Agents.Should().OnlyContain(a => a.Result.Captured && a.Result.ResolvedModel.Length == 0);
        transcript.Agents.Select(a => a.Child.MessageModels.Single()).Should().Equal([Sonnet, Opus], "message.model is still there to price the child by");
    }

    /// <summary>The restricted launch removed the plugin agent: the Agent call's result is an <c>is_error</c> block and its
    /// <c>tool_use_result</c> a STRING ("Agent type … not found") — every field of the result reads absent, nothing is invented.</summary>
    [Fact]
    public void A_failed_agent_call_whose_result_is_a_string_reads_as_an_error_with_nothing_invented()
    {
        var restricted = ClaudeSubagents.Read(Fixture("confine-restricted"));

        restricted.Agents.Should().ContainSingle();
        var result = restricted.Agents[0].Result;
        (result.Captured, result.IsError, result.ResolvedModel, result.TotalDurationMs.WasCaptured).Should().Be((true, true, string.Empty, false));
        restricted.Agents[0].Child.Should().BeEquivalentTo(ChildTrace.Nothing);
        restricted.Envelope.Stats.Should().Be(new SubagentStats(true, 0, 0, 0, 0));
    }

    [Fact]
    public void A_denied_child_call_and_an_error_result_are_told_apart_from_a_returned_one()
    {
        var tools = ClaudeSubagents.Read(Fixture("confine-default")).Agents.Single().Child.Tools;

        tools.Select(t => (t.Name, t.Fate)).Should().Equal(
            ("Read", ChildToolFate.Returned),
            ("Read", ChildToolFate.Denied),
            ("Read", ChildToolFate.Denied),
            ("Glob", ChildToolFate.Returned),
            ("Grep", ChildToolFate.Denied));
        tools[3].ResultText.Should().Be("No files found");
    }

    [Fact]
    public void Text_that_is_not_a_stream_reads_as_nothing_and_never_throws()
    {
        var nothing = ClaudeSubagents.Read("not json\n{\"type\":\n\n");

        (nothing.Complete, nothing.Agents.Count, nothing.MainCalls.Count, nothing.TeamMs.WasCaptured).Should().Be((false, 0, 0, false));
    }
}
