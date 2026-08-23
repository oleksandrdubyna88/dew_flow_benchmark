using Bench.Domain.Sessions;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Sessions;

/// <summary>The phase label, and the two decisions in it that were nearly made the other way.
/// <para>
/// The first is that the working tree OUTRANKS the tool's name. The second is that a neutral call carries
/// the phase around it rather than opening one of its own. Both are pinned here against the draft that
/// had them wrong, so a later simplification has to argue with a red test.
/// </para></summary>
public sealed class PhaseClassifierTests
{
    [Fact]
    public void A_read_is_research_and_an_edit_is_execution()
    {
        PhaseClassifier.Resolve(ToolClass.Read, MutationEvidence.NotChecked, SessionPhase.Unknown)
            .Should().Be(SessionPhase.Research);

        PhaseClassifier.Resolve(ToolClass.Write, MutationEvidence.NotChecked, SessionPhase.Research)
            .Should().Be(SessionPhase.Execution);
    }

    [Fact]
    public void A_build_is_verification_and_never_folded_into_research()
    {
        PhaseClassifier.Resolve(ToolClass.Verify, MutationEvidence.Unchanged, SessionPhase.Execution)
            .Should().Be(SessionPhase.Verification,
                "the compile-failure count has nowhere else to come from, and a build filed under research "
                + "makes the verification loop invisible");
    }

    [Fact]
    public void The_working_tree_outranks_the_allowlist_when_they_disagree()
    {
        // `Bash` running something the allowlist calls read-only — and the porcelain digest moved anyway.
        // This is the whole reason the check is worth its milliseconds.
        PhaseClassifier.Resolve(ToolClass.Read, MutationEvidence.Changed, SessionPhase.Research)
            .Should().Be(SessionPhase.Execution);
    }

    [Fact]
    public void A_neutral_call_carries_the_phase_around_it_rather_than_opening_one()
    {
        PhaseClassifier.Resolve(ToolClass.Neutral, MutationEvidence.NotChecked, SessionPhase.Execution)
            .Should().Be(SessionPhase.Execution,
                "a todo update between two edits is part of the edit run; a classifier that let it open a "
                + "phase would report switch counts that are mostly bookkeeping");
    }

    [Fact]
    public void A_session_that_opens_with_a_neutral_call_is_researching()
    {
        PhaseClassifier.Resolve(ToolClass.Neutral, MutationEvidence.NotChecked, SessionPhase.Unknown)
            .Should().Be(SessionPhase.Research,
                "Unknown at the head of every report is a bucket nobody can act on");
    }

    [Fact]
    public void An_unclassified_tool_does_not_invent_a_phase_switch()
    {
        PhaseClassifier.Resolve(ToolClass.Unknown, MutationEvidence.NotChecked, SessionPhase.Verification)
            .Should().Be(SessionPhase.Verification);
    }

    [Fact]
    public void Relabelling_a_whole_sequence_reproduces_what_the_store_wrote_call_by_call()
    {
        var calls = new[]
        {
            Call(1, ToolClass.Read, MutationEvidence.NotChecked),
            Call(2, ToolClass.Write, MutationEvidence.Changed),
            Call(3, ToolClass.Neutral, MutationEvidence.NotChecked),
            Call(4, ToolClass.Verify, MutationEvidence.Unchanged),
        };

        PhaseClassifier.Label(calls).Select(c => c.Phase).Should().Equal(
            SessionPhase.Research,
            SessionPhase.Execution,
            SessionPhase.Execution,
            SessionPhase.Verification);
    }

    internal static SessionToolCall Call(
        int ordinal,
        ToolClass toolClass,
        MutationEvidence mutation,
        string target = "",
        string tool = "Read",
        int second = 0) =>
        new(
            ordinal,
            tool,
            toolClass,
            SessionPhase.Unknown,
            target,
            "{}",
            DateTimeOffset.UnixEpoch.AddSeconds(second == 0 ? ordinal : second),
            CallState.Finished,
            CallOutcome.Answered,
            Captured.Text("ok"),
            ResponseChars: 2,
            CapturedCount.Unavailable("not measured"),
            mutation,
            CompileFailure: false);
}

