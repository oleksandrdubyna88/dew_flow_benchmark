using Bench.Domain;
using Bench.Domain.Gate;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Gate.Assessment.AssessmentFixtures;
using static Bench.Tests.Gate.GateReportFixtures;

namespace Bench.Tests.Gate.Assessment;

/// <summary>The E4 Definition of Done: no strict percentage is shown for a population until a person has hand-checked
/// twenty of its verdicts and that is recorded — and a recorded check must be a check of the verdicts as STORED.</summary>
public sealed class HandCheckTests
{
    private static readonly Guid Campaign = Guid.Parse("0199cccc-0000-7000-8000-000000000003");

    [Fact]
    public void A_scope_with_verdicts_and_no_hand_check_shows_no_strict_percentage_but_still_shows_the_counts()
    {
        var (input, run) = Assessed();

        var row = GateReport.PerModel(Scope(PinA), StrictRubric, input).Rows.Single();

        row.SupportedPct.Should().Be(Figure.NotHandChecked, "twenty verdicts read by a person come before any strict %");
        row.SupportedOrPartialPct.Should().Be(Figure.NotHandChecked);
        row.Supported.Should().Be(1, "the counts are the evidence a person checks against; only the rate waits");
        run.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void With_a_recorded_hand_check_of_that_campaign_assessor_and_rubric_the_percentage_is_shown()
    {
        var (input, _) = Assessed();
        var checkedInput = input with { HandChecks = [Check(Campaign, Codex, StrictRubric)] };

        GateReport.PerModel(Scope(PinA), StrictRubric, checkedInput).Rows.Single().SupportedPct.Should().Be(Figure.Of(50));
    }

    [Theory]
    [InlineData("another-campaign")]
    [InlineData("another-assessor")]
    [InlineData("another-rubric")]
    public void A_hand_check_of_something_else_does_not_unlock_the_percentage(string what)
    {
        var (input, _) = Assessed();
        var strictV2 = Rubric.Of("strict-v2", RubricKind.Strict, StableHash.Of("strict-v2")).Ok();

        var check = what switch
        {
            "another-campaign" => Check(Guid.CreateVersion7(), Codex, StrictRubric),
            "another-assessor" => Check(Campaign, GateReviewerId.Parse("claude-opus").Ok(), StrictRubric),
            _ => Check(Campaign, Codex, strictV2),
        };

        GateReport.PerModel(Scope(PinA), StrictRubric, input with { HandChecks = [check] }).Rows.Single().SupportedPct
            .Should().Be(Figure.NotHandChecked);
    }

    [Fact]
    public void A_lenient_rubric_is_an_imported_label_and_is_not_gated() =>
        HandCheckGate.Allows([], LenientRubric, [(Campaign, Codex)]).Should().BeTrue();

    [Fact]
    public void Fewer_than_twenty_verdicts_read_is_not_a_hand_check() =>
        HandCheck.Of([Campaign], StrictRubric, Codex, 19, 19, new string('a', 64), Noon).Reason().Should().Contain("at least 20");

    [Fact]
    public void Answers_count_only_when_every_answered_row_was_drawn_and_still_shows_the_stored_verdict()
    {
        var drawn = Enumerable.Range(0, 20).Select(i => new HandCheckTruth(Id($"{i:x8}"), Campaign, "b1", "supported")).ToList();
        var answers = drawn.Select((t, i) => new HandCheckAnswer(t.Id, t.BatchId, t.Reading, true, i % 4 != 0)).ToList();

        HandCheckAnswers.Verify(answers, drawn).Ok().Should().Be((20, 15));

        HandCheckAnswers.Verify([.. answers.Skip(1)], drawn).Reason().Should().Contain("19 row(s) answered");
        HandCheckAnswers.Verify([.. answers.Skip(1), answers[0] with { Reading = "refuted" }], drawn).Reason().Should().Contain("no longer shows the verdict as stored");
        HandCheckAnswers.Verify([.. answers.Skip(1), answers[0] with { Id = Id("ffffffff") }], drawn).Reason().Should().Contain("was not drawn");
        HandCheckAnswers.Verify([.. answers, answers[0]], drawn).Reason().Should().Contain("answered 2 times");
    }

    /// <summary>One reviewer, one valid run with two findings, both read by codex: one supported, one refuted.</summary>
    private static (GateReportInput Input, Guid Run) Assessed()
    {
        var run = Run("cs2", "grok-medium", 1, campaign: Campaign);
        var verdicts = new[]
        {
            GateVerdict.Under(Catalog, StrictHash, run.RunId, 0, Strict(StrictReading.Supported), Codex, "b1", StableHash.Of("p"), false).Ok(),
            GateVerdict.Under(Catalog, StrictHash, run.RunId, 1, Strict(StrictReading.Refuted), Codex, "b1", StableHash.Of("p"), false).Ok(),
        };

        return (Input([run], verdicts), run.RunId);
    }

    private static HandCheck Check(Guid campaign, GateReviewerId assessor, Rubric rubric) =>
        HandCheck.Of([campaign], rubric, assessor, 20, 18, new string('b', 64), Noon).Ok();

    private static readonly DateTimeOffset Noon = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
}
