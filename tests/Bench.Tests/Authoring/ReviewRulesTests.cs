using Bench.Domain.Authoring;
using Bench.Domain.Bank;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Authoring;

/// <summary>The two rules that decide whether a machine's mark may be taken, and what a set of marks means.
/// <para>
/// Pure, and tested without a database or an agent, because both are judgements rather than storage: one says
/// a model may not vouch for its own writing, the other says what "reviewed" means when three slots exist.
/// </para></summary>
public sealed class ReviewRulesTests
{
    [Fact]
    public void A_model_reviewing_its_OWN_authorship_is_refused_by_default()
    {
        var refused = SelfReview.Check("reviewer-1", "claude-sonnet-4-6", "claude-sonnet-4-6", allowed: false);

        // The cheapest way to manufacture agreement, and it would not look like a defect afterwards: the bank
        // would hold three approvals per question and no record that one model wrote and blessed them all.
        refused.Reason().Should().Contain("reviewer-1").And.Contain("claude-sonnet-4-6");
        refused.Reason().Should().Contain("its own");
    }

    [Fact]
    public void Allowing_self_review_still_says_what_it_COSTS()
    {
        var allowed = SelfReview.Check("reviewer-1", "claude-sonnet-4-6", "claude-sonnet-4-6", allowed: true).Ok();

        // An escape hatch that is silent is an escape hatch nobody remembers taking. This one is the operator's
        // decision of 2026-08-18 — three Claude slots — and the cost has to travel with every batch it marks.
        allowed.Should().Contain("one opinion sampled three times");
    }

    [Fact]
    public void A_DIFFERENT_model_reviewing_is_taken_with_nothing_to_say()
    {
        SelfReview.Check("reviewer-2", "gpt-5", "claude-sonnet-4-6", allowed: false).Ok().Should().BeEmpty();
    }

    [Fact]
    public void The_comparison_is_on_the_MODEL_not_the_slot_key()
    {
        // Two registry rows, two reviewer slots, one model underneath. Comparing slot keys would call this a
        // clean review; comparing registry keys would too. Only the resolved model id catches it.
        SelfReview.Check("reviewer-3", "Claude-Sonnet-4-6", "claude-sonnet-4-6", allowed: false)
            .Reason().Should().Contain("its own");
    }

    [Fact]
    public void A_human_slot_with_no_model_bound_can_never_be_a_self_review() =>
        SelfReview.Check("reviewer-4", string.Empty, "claude-sonnet-4-6", allowed: false).Ok().Should().BeEmpty();

    [Fact]
    public void Every_configured_reviewer_approving_is_the_only_thing_that_promotes()
    {
        var reviewers = Slots(3);
        var marks = reviewers.Select(r => Mark(r, ReviewVerdict.Approved)).ToList();

        var decided = Promotion.Decide(reviewers, marks);

        // The strict rule, and it is the default because the alternative — a majority — is a quality claim
        // nobody here has measured.
        decided.Kind.Should().Be(PromotionKind.Accept);
        decided.Reason.Should().Contain("3");
    }

    [Fact]
    public void ONE_rejection_rejects_the_question_and_NAMES_who_rejected_it()
    {
        var reviewers = Slots(3);

        var decided = Promotion.Decide(reviewers, [
            Mark(reviewers[0], ReviewVerdict.Approved),
            Mark(reviewers[1], ReviewVerdict.Rejected),
            Mark(reviewers[2], ReviewVerdict.Approved),
        ]);

        decided.Kind.Should().Be(PromotionKind.Reject);
        decided.Reason.Should().Contain("reviewer-2");
    }

    [Fact]
    public void A_MISSING_mark_is_a_wait_rather_than_a_promotion()
    {
        var reviewers = Slots(3);

        var decided = Promotion.Decide(reviewers, [Mark(reviewers[0], ReviewVerdict.Approved)]);

        // The failure this guards is the one that looks like success: two of three approving and the question
        // going into a suite anyway, which would make "every reviewer approved" a sentence nobody could trust.
        decided.Kind.Should().Be(PromotionKind.Wait);
        decided.Reason.Should().Contain("reviewer-2").And.Contain("reviewer-3");
    }

    [Fact]
    public void A_bank_with_no_reviewer_rows_promotes_NOTHING()
    {
        var decided = Promotion.Decide([], []);

        // Vacuous truth is the trap: "every configured reviewer approved" is technically true of no reviewers,
        // and would accept a whole machine-written bank on the strength of an empty table.
        decided.Kind.Should().Be(PromotionKind.Wait);
        decided.Reason.Should().Contain("no reviewer");
    }

    [Fact]
    public void A_rejection_from_an_INELIGIBLE_reviewer_still_rejects()
    {
        var all = Slots(3);
        var eligible = all.Take(2).ToList();

        var decided = Promotion.Decide(eligible, [Mark(all[0], ReviewVerdict.Approved), Mark(all[2], ReviewVerdict.Rejected)]);

        // A named defect is a named defect. The third slot may not APPROVE this question — it is the model that
        // wrote it — but a rejection it already recorded is a judgement, and discarding it would let a question
        // through on the strength of who was allowed to speak.
        decided.Kind.Should().Be(PromotionKind.Reject);
    }

    [Fact]
    public void When_every_slot_is_the_authors_own_model_NOTHING_is_eligible_and_nothing_promotes()
    {
        var decided = Promotion.Decide([], [Mark(Slots(1)[0], ReviewVerdict.Approved)]);

        // The state of this bank on 2026-08-18: three slots, one model, same model authored. Without the
        // one-third design or the flag, there is nobody who may vouch for anything.
        decided.Kind.Should().Be(PromotionKind.Wait);
        decided.Reason.Should().Contain("every configured slot is the model that wrote it");
    }

    [Fact]
    public void Two_of_three_approving_is_ENOUGH_when_the_third_is_the_author()
    {
        var all = Slots(3);
        var eligible = all.Skip(1).ToList();

        var decided = Promotion.Decide(eligible, [Mark(all[1], ReviewVerdict.Approved), Mark(all[2], ReviewVerdict.Approved)]);

        // The one-third design's whole point: unanimity is still required, of the reviewers who MAY judge. Two
        // launches per question instead of three, and no self-review anywhere.
        decided.Kind.Should().Be(PromotionKind.Accept);
        decided.Reason.Should().Contain("2 eligible");
    }

    private static IReadOnlyList<Reviewer> Slots(int count) =>
        [.. Enumerable.Range(1, count).Select(n => Reviewer.Create($"reviewer-{n}", $"Reviewer {n}", n).Ok())];

    private static QuestionReview Mark(Reviewer reviewer, ReviewVerdict verdict) =>
        new(Guid.CreateVersion7(), reviewer.Id, verdict, "checked", DateTimeOffset.UnixEpoch);
}
