using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Runs;

/// <summary>The three things an arbiter's verdict must never become.
/// <para>
/// All three are the same mistake in different clothes: a reading that was not taken arriving as a
/// reading of zero. The mechanical scorer already learned it once with anchor recall in a lane that
/// surfaces nothing; a judge has two more ways to produce it, and both are cheap to get wrong.
/// </para></summary>
public sealed class JudgeScoringTests
{
    private static readonly CommitSha Commit = CommitSha.Parse(new string('a', 40)).Ok();

    [Fact]
    public void A_question_with_no_reference_answer_is_a_gap_in_the_suite_not_a_failing_leg()
    {
        var metric = JudgeScoring.Score(Question("", "q"), JudgeReading.Silent("no reference answer"), "opus", "qwen");

        metric.Kind.Should().Be(MetricKind.Text);
        metric.Value.Should().Be("not judgeable");
        metric.Failed.Should().BeFalse("nobody wrote the reference down — that is not the subject's fault");
        metric.AsNumber().Failed().Should().BeTrue("and it must stay out of the numeric aggregate rather than dilute it");
    }

    [Fact]
    public void An_arbiter_that_could_not_be_reached_says_nothing_rather_than_NO()
    {
        var metric = JudgeScoring.Score(
            Question("the reference", "q"), JudgeReading.Silent("connection refused"), "opus", "qwen");

        metric.Value.Should().Be("not judged");
        metric.Failed.Should().BeFalse(
            "an arbiter that has stopped answering would otherwise look exactly like a subject that has stopped being right");
        metric.Reason.Should().Contain("connection refused");
    }

    [Fact]
    public void A_real_verdict_is_a_boolean_carrying_the_arbiters_own_reason()
    {
        var metric = JudgeScoring.Score(
            Question("the reference", "q"), JudgeReading.Verdict(false, "names the wrong mechanism"), "opus", "qwen");

        metric.Kind.Should().Be(MetricKind.Boolean);
        metric.Value.Should().Be("false");
        metric.Failed.Should().BeTrue();
        metric.Reason.Should().Be("names the wrong mechanism");
        metric.AsNumber().Ok().Should().Be(0, "a pass rate over verdicts has to be a group-by like any other");
    }

    [Fact]
    public void Two_arbiters_over_one_answer_are_two_named_series_that_cannot_collide()
    {
        JudgeScoring.MetricName("opus").Should().NotBe(JudgeScoring.MetricName("qwen"));
        JudgeScoring.MetricName("opus").Should().Contain("opus");
    }

    [Fact]
    public void Self_judging_is_recorded_rather_than_refused()
    {
        var same = JudgeScoring.Score(Question("ref", "q"), JudgeReading.Verdict(true, "agrees"), "qwen", "qwen");
        var other = JudgeScoring.Score(Question("ref", "q"), JudgeReading.Verdict(true, "agrees"), "opus", "qwen");

        same.Metadata["selfJudged"].Should().Be("true", "a model marking its own homework is a reading, but not the same reading");
        other.Metadata["selfJudged"].Should().Be("false");
        same.Failed.Should().BeFalse("recorded, not refused — the filter belongs to whoever reads the aggregate");
    }

    private static Question Question(string reference, string id) =>
        new(id, "how is the delay computed?", [Expectation.File(SourceAnchor.File("src/X.cs", Commit))], reference);
}
