using Bench.Domain.Models;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Runs;

/// <summary>
/// Scoring what a subject CALLED, and the fairness rule that makes the floor comparable.
///
/// <para>The whole point of the negative half is easy to miss: <c>ToolNotUsed</c> is not decoration. A
/// description that makes a model reach for a tool where it should not have is a defect in the description,
/// and it is invisible unless something asserts the negative — which is the axis this benchmark exists to
/// measure.</para>
/// </summary>
public sealed class ToolScoringTests
{
    [Fact]
    public void A_tool_the_question_wanted_and_the_subject_called_scores_one()
    {
        var metric = Only(Question(Used("rag_search_project_context")), Called("rag_search_project_context"));

        metric.Value.Should().Be("1");
        metric.Failed.Should().BeFalse();
        metric.Name.Should().Contain("rag_search_project_context");
    }

    [Fact]
    public void A_tool_that_was_offered_and_never_called_FAILS_and_says_so()
    {
        // The failure this whole plan exists to catch: a good tool nobody calls. The detail has to say that
        // it was offered, or a reader cannot tell it from a lane that never had it.
        var metric = Only(Question(Used("graf_search_types")), Called("rt_read_local_file"));

        metric.Value.Should().Be("0");
        metric.Failed.Should().BeTrue();
        metric.Reason.Should().Contain("offered and never called");
    }

    [Fact]
    public void A_tool_the_question_forbade_and_the_subject_avoided_scores_one()
    {
        Only(Question(NotUsed("rt_read_local_file")), Called("graf_search_types"))
            .Value.Should().Be("1");
    }

    [Fact]
    public void A_forbidden_tool_that_was_called_reads_as_a_defect_in_the_DESCRIPTION()
    {
        var metric = Only(Question(NotUsed("rt_read_local_file")), Called("rt_read_local_file"));

        metric.Failed.Should().BeTrue();
        metric.Reason.Should().Contain("defect in its description",
            "a metric that only said 'failed' would send a reader looking at the model instead of the words");
    }

    [Fact]
    public void A_tool_expectation_in_a_lane_with_NO_tools_is_not_applicable_rather_than_a_miss()
    {
        // The fairness rule, identical to anchor recall's and load-bearing for the same reason: the no-tools
        // floor exists to be compared fairly. Scoring it zero would make the baseline look worse than it is
        // and flatter every tool lane by exactly that much.
        var metric = Only(Question(Used("rag_search_project_context")), ToolUsageObservation.None);

        metric.Failed.Should().BeFalse();
        metric.Value.Should().Be("not applicable");
        metric.Reason.Should().Contain("that is the arm, not the subject");
    }

    [Fact]
    public void The_not_applicable_metric_is_TEXT_so_an_aggregate_skips_it()
    {
        // Emitted as text rather than as a zero, so the numeric mean reports a smaller denominator instead
        // of a diluted average — the same shape the recall metric already uses.
        Only(Question(NotUsed("rt_read_local_file")), ToolUsageObservation.None)
            .Kind.Should().Be(MetricKind.Text);
    }

    [Fact]
    public void A_question_with_no_tool_expectations_produces_no_tool_metric()
    {
        AnswerScoring.Score(Question(), Answer(), RetrievalObservation.None, Called("anything"))
            .Should().NotContain(m => m.Name.StartsWith(AnswerScoring.ToolUse, StringComparison.Ordinal));
    }

    [Fact]
    public void Every_tool_expectation_gets_its_own_metric()
    {
        var metrics = AnswerScoring.Score(
            Question(Used("a"), Used("b"), NotUsed("c")), Answer(), RetrievalObservation.None, Called("a"));

        metrics.Where(m => m.Name.StartsWith(AnswerScoring.ToolUse, StringComparison.Ordinal))
            .Should().HaveCount(3);
    }

    [Fact]
    public void Scoring_without_a_tool_observation_behaves_exactly_as_it_did_before_tools_existed()
    {
        // The overload every existing caller still uses. A question with no tool expectations must produce
        // the same metrics it always produced — otherwise this axis rewrote history.
        var before = AnswerScoring.Score(Question(), Answer(), RetrievalObservation.None);
        var after = AnswerScoring.Score(Question(), Answer(), RetrievalObservation.None, ToolUsageObservation.None);

        after.Select(m => m.Name).Should().Equal(before.Select(m => m.Name));
    }

    [Fact]
    public void Repeat_calls_are_kept_because_how_often_is_a_fact_about_the_surface()
    {
        var observed = ToolUsageObservation.Of("search", "search", "read");

        observed.ToolsCalled.Should().Equal("search", "search", "read");
    }

    private static StoredMetric Only(Question question, ToolUsageObservation tools) =>
        AnswerScoring.Score(question, Answer(), RetrievalObservation.None, tools)
            .Single(m => m.Name.StartsWith(AnswerScoring.ToolUse, StringComparison.Ordinal));

    private static ToolUsageObservation Called(params string[] tools) => ToolUsageObservation.Of(tools);

    /// <summary>A tool expectation's anchor is empty — the tool's NAME is the anchor, and it rides in
    /// Text. This is exactly the shape a misspelt kind used to be silently coerced into, which is why the
    /// loader now refuses one.</summary>
    private static readonly SourceAnchor Anchor =
        SourceAnchor.File(string.Empty, Bench.Domain.Targets.CommitSha.Parse(new string('a', 40)).Ok());

    private static Expectation Used(string tool) =>
        new(ExpectationKind.ToolUsed, Anchor, tool, Required: true);

    private static Expectation NotUsed(string tool) =>
        new(ExpectationKind.ToolNotUsed, Anchor, tool, Required: true);

    private static Question Question(params Expectation[] expectations) =>
        Bench.Domain.Suites.Question.Ask("q1", "where is the total computed?", expectations);

    private static ModelAnswer Answer() =>
        new(
            Captured.Text("in OrderService.Total"),
            CapturedCount.Unavailable("fake"),
            CapturedCount.Unavailable("fake"),
            TimeSpan.FromMilliseconds(5),
            SamplingAsSent.NotCaptured("fake"),
            StopReason.Completed,
            "stop");
}
