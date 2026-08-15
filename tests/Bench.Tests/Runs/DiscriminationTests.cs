using Bench.Domain.Runs;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Runs;

/// <summary>Discrimination — and the rule that nothing is ever deleted for being easy.
/// <para>
/// The operator's correction, made into code: a question that a frontier model finds trivial is not
/// uninformative, it is uninformative <em>for a comparison of frontier models</em>. The same question is
/// excluded from one ranking and central to another.
/// </para></summary>
public sealed class DiscriminationTests
{
    private static readonly string[] Frontier = ["opus", "fable"];
    private static readonly string[] Cheap = ["haiku", "local-7b"];

    [Fact]
    public void The_same_question_is_useless_for_one_comparison_and_central_to_another()
    {
        var question = Spread(("opus", 1.0), ("fable", 1.0), ("haiku", 1.0), ("local-7b", 0.0));

        question.Judge(Frontier, Discrimination.DefaultMinSpread).Ok()
            .Should().Be(QuestionSpread.Verdict.EveryonePasses, "it cannot separate two models that both answer it");
        question.Judge(Cheap, Discrimination.DefaultMinSpread).Ok()
            .Should().Be(QuestionSpread.Verdict.Discriminates, "and it is exactly what separates the two cheaper ones");
    }

    [Fact]
    public void A_question_nobody_answers_is_reported_separately_from_one_everybody_answers()
    {
        var unanswered = Spread(("opus", 0.0), ("fable", 0.0));

        unanswered.Judge(Frontier, Discrimination.DefaultMinSpread).Ok()
            .Should().Be(
                QuestionSpread.Verdict.NobodyPasses,
                "a question nobody answers is as likely to be broken as hard, and merging it with the trivial ones hides that");
    }

    [Fact]
    public void A_gap_smaller_than_the_comparisons_floor_is_too_close_rather_than_discriminating()
    {
        var narrow = Spread(("opus", 0.7), ("fable", 0.6));

        narrow.Judge(Frontier, minSpread: 0.25).Ok().Should().Be(QuestionSpread.Verdict.TooClose);
        narrow.Judge(Frontier, minSpread: 0.05).Ok().Should().Be(QuestionSpread.Verdict.Discriminates);
    }

    [Fact]
    public void An_unmeasured_subject_is_reported_rather_than_scored_as_a_failure()
    {
        var partial = Spread(("opus", 1.0));

        partial.Unmeasured(Frontier).Should().Equal("fable");
        partial.Judge(Frontier, Discrimination.DefaultMinSpread)
            .Reason().Should().Contain("a spread needs two ends");
    }

    [Fact]
    public void A_spread_needs_two_measured_ends()
    {
        Spread(("opus", 1.0), ("fable", 0.0)).SpreadAcross(Frontier).Ok().Should().Be(1.0);
        Spread(("opus", 1.0)).SpreadAcross(Frontier).Failed().Should().BeTrue();
    }

    [Fact]
    public void Saturation_is_a_label_about_the_question_not_a_verdict_on_it()
    {
        var tiers = new Dictionary<string, int> { ["local-7b"] = 1, ["haiku"] = 2, ["fable"] = 3, ["opus"] = 4 };
        var question = Spread(("local-7b", 0.0), ("haiku", 0.5), ("fable", 1.0), ("opus", 1.0));

        var label = question.SaturatedAt(tiers);

        label.IsSaturated.Should().BeTrue();
        label.Rank.Should().Be(3, "everything at fable's tier and above answers it; haiku does not");
        label.Describe.Should().Contain("tier 3 and above");
    }

    [Fact]
    public void A_question_nothing_saturates_says_so_rather_than_claiming_a_tier()
    {
        var tiers = new Dictionary<string, int> { ["fable"] = 3, ["opus"] = 4 };

        Spread(("fable", 0.5), ("opus", 0.9)).SaturatedAt(tiers).IsSaturated
            .Should().BeFalse("0.9 is not 'trivial', and rounding it up would invent a ceiling");
    }

    [Fact]
    public void A_set_reports_every_category_rather_than_a_single_number()
    {
        var report = Discrimination.Over(
        [
            Spread(("opus", 1.0), ("fable", 0.0)),   // discriminates
            Spread(("opus", 1.0), ("fable", 1.0)),   // trivial here
            Spread(("opus", 0.0), ("fable", 0.0)),   // nobody
            Spread(("opus", 0.6), ("fable", 0.5)),   // too close
            Spread(("opus", 1.0)),                    // unmeasured on one side
        ], Frontier);

        report.Should().Be(new DiscriminationReport(1, 1, 1, 1, 1));
        report.Total.Should().Be(5);
        report.Describe.Should().Contain("1 of 5 discriminate");
    }

    [Fact]
    public void Only_the_discriminating_questions_may_carry_a_ranking()
    {
        var questions = new List<QuestionSpread>
        {
            Spread(("opus", 1.0), ("fable", 0.0)),
            Spread(("opus", 1.0), ("fable", 1.0)),
        };

        Discrimination.Usable(questions, Frontier).Should().ContainSingle(
            "a ranking that includes questions every subject passed is diluted by items that voted for nobody");
    }

    private static QuestionSpread Spread(params (string Model, double Rate)[] rates) =>
        QuestionSpread.Of("q1", rates.ToDictionary(r => r.Model, r => r.Rate, StringComparer.Ordinal));
}
