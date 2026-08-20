using Bench.Domain.Retrieval;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Retrieval;

/// <summary>Turning a returned list of hits into retrieval numbers.
/// <para>
/// The matching rule is the substance here, and the bias is deliberate: <b>a false positive is worse than a
/// miss</b>. These figures are what a retrieval claim is made of, so a rule generous enough to inflate one
/// would produce a number indistinguishable from a real one — while a strict rule that misses shows up as a
/// suspiciously low recall somebody goes and investigates.
/// </para></summary>
public sealed class RetrievalScoringTests
{
    private static readonly CommitSha Commit = CommitSha.Parse(new string('a', 40)).Ok();

    [Fact]
    public void A_hit_whose_lines_cover_the_anchor_reaches_it_even_when_the_names_do_not_line_up()
    {
        var question = Asking(Member("src/Retry/RetryHelper.cs", "RetryHelper.DecorrelatedJitterBackoffV2", 75, 111));

        // The engine's own member key is a format this side does not share, and its readable name here is
        // deliberately different. The SPAN is what proves the code came back.
        var matched = RetrievalScoring.Match(question, [Hit(1, "src/Retry/RetryHelper.cs", 60, 130, member: "Other.Name")]);

        matched.Single().Surfaced.Should().BeTrue("a hit whose lines cover the anchor's has literally returned that code");
        matched.Single().Rank.Should().Be(1);
    }

    [Fact]
    public void A_hit_in_the_right_file_at_the_wrong_lines_does_NOT_reach_a_member_anchor()
    {
        var question = Asking(Member("src/Retry/RetryHelper.cs", "RetryHelper.DecorrelatedJitterBackoffV2", 75, 111));

        var matched = RetrievalScoring.Match(question, [Hit(1, "src/Retry/RetryHelper.cs", 200, 240, member: "RetryHelper.Sleep")]);

        matched.Single().Surfaced.Should().BeFalse(
            "the file was right and the member was not — counting that would inflate every recall figure in the report");
    }

    [Fact]
    public void The_readable_member_identity_reaches_an_anchor_when_the_spans_have_moved()
    {
        // A suite authored against one commit and replayed at another: the lines shift, the name does not.
        var question = Asking(Member("src/Retry/RetryHelper.cs", "RetryHelper.Backoff", 75, 111));

        var matched = RetrievalScoring.Match(
            question, [Hit(1, "src/Retry/RetryHelper.cs", 900, 940, member: "RetryHelper.Backoff")]);

        matched.Single().Surfaced.Should().BeTrue();
    }

    [Fact]
    public void A_member_name_that_merely_ENDS_with_the_anchors_does_not_reach_it()
    {
        var question = Asking(Member("src/Retry/RetryHelper.cs", "RetryHelper.Retry", 75, 111));

        var matched = RetrievalScoring.Match(
            question, [Hit(1, "src/Retry/RetryHelper.cs", 300, 340, member: "RetryHelper.NoRetry")]);

        matched.Single().Surfaced.Should().BeFalse(
            "a suffix rule would let NoRetry answer for Retry, and this number is a claim");
    }

    [Fact]
    public void A_file_anchor_is_reached_by_any_hit_in_that_file()
    {
        var question = Asking(Expectation.File(SourceAnchor.File("src/Retry/RetryHelper.cs", Commit)));

        var matched = RetrievalScoring.Match(question, [Hit(1, "src/Retry/RetryHelper.cs", 900, 940)]);

        matched.Single().Surfaced.Should().BeTrue("a file expectation asks for the file, and the file came back");
    }

    [Fact]
    public void Paths_are_compared_across_the_separators_and_the_casing_of_two_operating_systems()
    {
        var question = Asking(Expectation.File(SourceAnchor.File("src\\Retry\\RetryHelper.cs", Commit)));

        var matched = RetrievalScoring.Match(question, [Hit(1, "src/retry/RetryHelper.cs", 1, 10)]);

        matched.Single().Surfaced.Should().BeTrue(
            "the same suite is authored on Windows and replayed on a Linux runner — that difference is a fact "
            + "about a filesystem, not about retrieval");
    }

    [Fact]
    public void A_hit_in_another_file_reaches_nothing_however_well_its_member_matches()
    {
        var question = Asking(Member("src/Retry/RetryHelper.cs", "RetryHelper.Backoff", 75, 111));

        var matched = RetrievalScoring.Match(
            question, [Hit(1, "test/Retry/RetryHelperTests.cs", 75, 111, member: "RetryHelper.Backoff")]);

        matched.Single().Surfaced.Should().BeFalse("the test file is not the implementation, and copies of a name are common");
    }

    [Fact]
    public void The_rank_recorded_is_the_FIRST_hit_that_reached_the_anchor()
    {
        var question = Asking(Member("src/A.cs", "A.M", 10, 20));

        var matched = RetrievalScoring.Match(question, [
            Hit(1, "src/B.cs", 1, 5),
            Hit(2, "src/A.cs", 12, 18),
            Hit(3, "src/A.cs", 10, 20),
        ]);

        matched.Single().Rank.Should().Be(2, "rank is the position a subject reads at, so the earliest match is the fact");
    }

    [Fact]
    public void Anchor_recall_is_computed_by_the_ONE_scorer_that_already_defines_it()
    {
        var question = Asking(
            Member("src/A.cs", "A.M", 10, 20),
            Member("src/B.cs", "B.M", 30, 40));

        var observed = RetrievalScoring.Observe(question, Context(Hit(1, "src/A.cs", 10, 20)));
        var metrics = AnswerScoring.Score(question, Answered("anything"), observed);

        // Reusing AnswerScoring rather than computing a second recall: two definitions of recall in one
        // system is two numbers a report can print for one question.
        var recall = metrics.Single(m => m.Name == AnswerScoring.AnchorRecall);
        recall.Kind.Should().Be(MetricKind.Numeric);
        recall.Value.Should().Be("0.5");
        recall.Reason.Should().Contain("1 of 2");
    }

    [Fact]
    public void MRR_averages_over_the_anchors_the_question_NAMES_not_over_the_ones_that_were_found()
    {
        var question = Asking(
            Member("src/A.cs", "A.M", 10, 20),
            Member("src/B.cs", "B.M", 30, 40),
            Member("src/C.cs", "C.M", 50, 60));

        var metrics = RetrievalScoring.Score(question, Context(Hit(1, "src/A.cs", 10, 20)));

        // One of three at rank 1 is not a perfect engine. Averaging over hits only is the arithmetic that
        // would have said it was.
        metrics.Single(m => m.Name == RetrievalScoring.Mrr).AsNumber().Ok()
            .Should().BeApproximately(1.0 / 3, 0.0001);
    }

    [Fact]
    public void A_first_hit_rank_that_does_not_exist_is_TEXT_rather_than_a_zero()
    {
        var question = Asking(Member("src/A.cs", "A.M", 10, 20));

        var metrics = RetrievalScoring.Score(question, Context(Hit(1, "src/Z.cs", 1, 5)));

        // A smaller rank is a better one, so a zero would average into "excellent" — the same trap the
        // not-applicable recall metric avoids by being text.
        var first = metrics.Single(m => m.Name == RetrievalScoring.FirstHitRank);
        first.Kind.Should().Be(MetricKind.Text);
        first.Value.Should().Be("not surfaced");
        first.AsNumber().Failed().Should().BeTrue("and it must stay out of the numeric aggregate rather than sink it");
    }

    [Fact]
    public void Recall_is_reported_at_cut_offs_a_subject_actually_reads()
    {
        var question = Asking(
            Member("src/A.cs", "A.M", 10, 20),
            Member("src/B.cs", "B.M", 30, 40));

        var metrics = RetrievalScoring.Score(question, Context(
            Hit(1, "src/A.cs", 10, 20),
            Hit(8, "src/B.cs", 30, 40)));

        metrics.Single(m => m.Name == RetrievalScoring.RecallAt(5)).AsNumber().Ok().Should().Be(0.5);
        metrics.Single(m => m.Name == RetrievalScoring.RecallAt(10)).AsNumber().Ok().Should().Be(1.0);
    }

    [Fact]
    public void A_leg_that_performed_no_retrieval_gets_no_retrieval_metrics_at_all()
    {
        var question = Asking(Member("src/A.cs", "A.M", 10, 20));

        var metrics = RetrievalScoring.Score(question, RetrievedContext.NotPerformed);

        // The control arm keeps exactly the metric set it had before this lane existed. Zeroes here would
        // put the no-retrieval baseline into every retrieval aggregate, at the bottom of it.
        metrics.Should().BeEmpty();
        RetrievalScoring.Observe(question, RetrievedContext.NotPerformed).Available.Should().BeFalse();
    }

    [Fact]
    public void A_question_with_no_anchors_says_so_rather_than_scoring_zero()
    {
        var question = new Question("q", "what does this do?", [], string.Empty);

        var metrics = RetrievalScoring.Score(question, Context(Hit(1, "src/A.cs", 1, 5)));

        metrics.Should().OnlyContain(m => m.Kind == MetricKind.Text && m.Value == "nothing to find");
        metrics.Should().OnlyContain(m => !m.Failed, "a question that asked for no anchors cannot have missed one");
    }

    private static Question Asking(params Expectation[] expectations) =>
        new("q", "how does the retry delay work?", expectations, string.Empty);

    private static Expectation Member(string path, string memberKey, int start, int end) =>
        Expectation.Member(SourceAnchor.Member(path, memberKey, new LineSpan(start, end), Commit));

    private static RetrievedHit Hit(int rank, string path, int start, int end, string member = "") => new(
        rank, path, start, end, member, $"engine|key|{member}", "signature", 0.9, "rerank", ["dense"], [1],
        HitSnippet.Text("the source"));

    private static RetrievedContext Context(params RetrievedHit[] hits) =>
        RetrievedContext.Of("code_x", hits, RetrievalFunnel.None, string.Empty, EngineAxes.None, EngineAxes.None, EngineAxes.None, 0, 0);

    private static Bench.Domain.Models.ModelAnswer Answered(string text) => new(
        Captured.Text(text),
        CapturedCount.Number(10),
        CapturedCount.Number(5),
        TimeSpan.FromMilliseconds(1),
        SamplingAsSent.From(Sampling.Deterministic(1), "test"),
        Bench.Domain.Models.StopReason.Completed,
        "stop");
}
