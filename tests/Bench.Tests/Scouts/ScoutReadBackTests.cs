using System.Text.Json.Nodes;
using Bench.Domain.Probes;
using Bench.Domain.Scouts;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Scouts.ScoutStreams;

namespace Bench.Tests.Scouts;

/// <summary>The read-back of §3.5 on every cell. Each test starts from a cell that passes — arm B1, two Sonnet scouts, the two frozen
/// prompts, every read inside the tree — and plants exactly ONE defect, so a void names the defect and nothing else.</summary>
public sealed class ScoutReadBackTests
{
    private static readonly ScoutArm B1 = ScoutArms.Of(ScoutArmId.B1);

    private static readonly ReadBackExpectation Expected = new(B1, [Hash("focus F1"), Hash("focus F2")], Sonnet, Tree);

    private static ReadBackVerdict Check(string stream) => ScoutReadBack.Check(ClaudeSubagents.Read(stream), Expected, LinkTargets.None);

    private static IReadOnlyList<ChildSpec> Team(Func<ChildSpec, ChildSpec>? second = null) =>
        [new ChildSpec("focus F1"), (second ?? (c => c))(new ChildSpec("focus F2"))];

    [Fact]
    public void A_cell_that_did_exactly_what_it_was_told_is_scored()
    {
        var verdict = Check(Cell(Team()));

        verdict.Voids.Should().BeEmpty();
        verdict.Stops.Should().BeEmpty();
        (verdict.IsValid, verdict.Outcome, verdict.Reason).Should().Be((true, ScoutCellOutcome.Scored, ScoutReason.None));
        verdict.Audit.Should().HaveCount(2).And.OnlyContain(c => c.Finding == PathFinding.Inside);
    }

    /// <summary>The break-it target: a child whose <c>resolvedModel</c> is another model while every <c>message.model</c> still says the
    /// arm's — only the resolved model gives it away.</summary>
    [Fact]
    public void A_child_resolved_to_another_model_voids_the_cell_even_when_its_messages_name_the_right_one()
    {
        Check(Cell(Team(c => c with { ResolvedModel = Opus }))).Voids.Should().Equal(ScoutReason.ChildModelMismatch);
    }

    [Fact]
    public void A_child_whose_messages_answered_with_another_model_voids_the_cell()
    {
        Check(Cell(Team(c => c with { MessageModel = Opus }))).Voids.Should().Equal(ScoutReason.ChildModelMismatch);
    }

    [Fact]
    public void A_child_with_no_resolved_model_is_a_void_reason_not_a_crash()
    {
        var verdict = Check(Cell(Team(c => c with { ResolvedModel = string.Empty })));

        verdict.Voids.Should().Equal(ScoutReason.ChildModelNotCaptured);
        verdict.Outcome.Should().Be(ScoutCellOutcome.Void);
    }

    [Fact]
    public void A_prompt_that_is_not_the_frozen_one_byte_for_byte_voids_the_cell()
    {
        Check(Cell(Team(c => c with { Prompt = "focus F2 " }))).Voids.Should().Equal(ScoutReason.PromptHashMismatch);
    }

    [Fact]
    public void The_frozen_prompts_are_matched_as_a_set_so_their_order_inside_the_message_does_not_matter_and_a_duplicate_does()
    {
        Check(Cell([new ChildSpec("focus F2"), new ChildSpec("focus F1")])).IsValid.Should().BeTrue();
        Check(Cell([new ChildSpec("focus F1"), new ChildSpec("focus F1")])).Voids.Should().Equal(ScoutReason.PromptHashMismatch);
    }

    [Fact]
    public void An_agent_call_asking_for_another_model_alias_voids_the_cell()
    {
        Check(Cell(Team(c => c with { InputModel = "opus" }))).Voids.Should().Equal(ScoutReason.InputModelMismatch);
    }

    [Fact]
    public void An_agent_call_naming_another_agent_type_voids_the_cell()
    {
        Check(Cell(Team(c => c with { AgentType = ScoutArms.ExploreType }))).Voids.Should().Equal(ScoutReason.AgentTypeMismatch);
    }

    [Fact]
    public void A_wrong_number_of_agent_calls_voids_the_cell()
    {
        Check(Cell([new ChildSpec("focus F1")])).Voids.Should().Contain(ScoutReason.WrongCallCount);
        Check(Cell([.. Team(), new ChildSpec("focus F3")])).Voids.Should().Contain(ScoutReason.WrongCallCount);
    }

    [Fact]
    public void A_web_call_in_a_child_voids_the_cell()
    {
        var web = Team(c => c with { Tools = [new ToolSpec("WebFetch", """{"url":"https://example.com"}""")] });

        Check(Cell(web)).Voids.Should().Equal(ScoutReason.ChildToolOutsideReadOnlySet);
    }

    [Fact]
    public void A_main_session_call_other_than_agent_voids_the_cell()
    {
        Check(Cell(Team(), mainTools: ["Read"])).Voids.Should().Equal(ScoutReason.MainSessionNonAgentCall);
    }

    [Fact]
    public void Fewer_completed_subagents_than_scouts_voids_the_cell()
    {
        Check(Cell(Team(), completed: 1)).Voids.Should().Equal(ScoutReason.SubagentsNotCompleted);
    }

    [Fact]
    public void A_stream_without_its_envelope_voids_the_cell_as_incomplete()
    {
        Check(Cell(Team(), envelope: false)).Voids.Should().Equal(ScoutReason.StreamIncomplete, ScoutReason.SubagentsNotCompleted);
    }

    [Fact]
    public void An_agent_call_with_no_result_voids_the_cell()
    {
        Check(Cell(Team(c => c with { ResultMissing = true }))).Voids.Should().Contain(ScoutReason.AgentCallFailed);
    }

    /// <summary>The restricted launch of 2026-10-12: the plugin agent was "not found", so the one call failed and nothing was spawned.</summary>
    [Fact]
    public void The_restricted_probe_whose_agent_type_was_not_found_reads_back_void()
    {
        var restricted = ClaudeSubagents.Read(Fixture("confine-restricted"));
        var call = restricted.Agents.Single();
        var expected = new ReadBackExpectation(ScoutArms.Of(ScoutArmId.B1) with { Count = 1 }, [call.PromptSha256], Sonnet, "C:/work/tree");

        var verdict = ScoutReadBack.Check(restricted, expected, LinkTargets.None);

        verdict.Voids.Should().Equal(ScoutReason.AgentCallFailed, ScoutReason.SubagentsNotCompleted);
        verdict.Stops.Should().BeEmpty();
    }

    [Fact]
    public void A_boundary_stop_outranks_every_void_and_becomes_the_cells_one_reason()
    {
        var leaky = Team(c => c with { InputModel = "opus", Tools = [new ToolSpec("Read", """{"file_path":"C:/work/outside.txt"}""", Result: "secret")] });

        var verdict = Check(Cell(leaky));

        verdict.Voids.Should().Equal(ScoutReason.InputModelMismatch);
        verdict.Stops.Should().Equal(ScoutReason.OutsideReadReturnedContent);
        (verdict.Outcome, verdict.Reason).Should().Be((ScoutCellOutcome.Stopped, ScoutReason.OutsideReadReturnedContent));
    }

    [Fact]
    public void Every_reason_the_read_back_gives_is_a_void_or_a_stop_word_from_the_allow_list()
    {
        Enum.GetValues<ScoutReason>().Where(ScoutReasons.IsVoid).Should().HaveCount(11);
        Enum.GetValues<ScoutReason>().Where(ScoutReasons.IsStop).Should().HaveCount(5);
        Enum.GetValues<ScoutReason>().Should().OnlyContain(r => !(ScoutReasons.IsVoid(r) && ScoutReasons.IsStop(r)));
    }

    /// <summary>The live two-children probe launched one Sonnet and one Opus scout — under any arm it voids, and its Opus child's
    /// Globs into a GRANTED directory returned content, which stops the campaign.</summary>
    [Fact]
    public void The_live_two_children_probe_reads_back_as_a_mixed_team_that_also_crossed_the_boundary()
    {
        var transcript = ClaudeSubagents.Read(Fixture("two-children-models"));
        var expected = new ReadBackExpectation(B1, [.. transcript.Agents.Select(a => a.PromptSha256)], Sonnet, Tree);

        var verdict = ScoutReadBack.Check(transcript, expected, LinkTargets.None);

        verdict.Voids.Should().Equal(ScoutReason.InputModelMismatch, ScoutReason.ChildModelMismatch);
        verdict.Stops.Should().Equal(ScoutReason.OutsideReadReturnedContent);
    }

    [Fact]
    public void An_edited_live_stream_whose_child_resolved_elsewhere_is_caught_by_the_resolved_model()
    {
        var edited = Edit(Fixture("confine-default"), line =>
        {
            if (line["tool_use_result"] is JsonObject report && report["resolvedModel"] is not null)
            {
                report["resolvedModel"] = Opus;
            }

            return line;
        });
        var transcript = ClaudeSubagents.Read(edited);
        var expected = new ReadBackExpectation(B1 with { Count = 1 }, [transcript.Agents.Single().PromptSha256], Sonnet, "C:/work/tree");

        ScoutReadBack.Check(transcript, expected, LinkTargets.None).Voids.Should().Equal(ScoutReason.ChildModelMismatch);
    }
}
