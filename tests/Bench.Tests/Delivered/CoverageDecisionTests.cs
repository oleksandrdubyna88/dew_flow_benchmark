using Bench.Delivered;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Delivered;

/// <summary>The coverage gate, and the four measured cases that shaped it.
/// <para>
/// Every threshold here has a counter-example behind it, and the tests reproduce those counter-examples by
/// shape: a change whose band is unreachable by arithmetic (#14707), one too thin to judge at all (#14735),
/// one that only passes under its own size band (#15058), and a reason that is fluent and says nothing.
/// A port whose arithmetic differed on any of them would be a port of the vocabulary.
/// </para></summary>
public sealed class CoverageDecisionTests
{
    [Fact]
    public void A_decomposition_that_meets_its_band_is_ACCEPTED()
    {
        var verdict = CoverageDecision.Evaluate(accounted: 80m, cleanLoc: 100m, capped: false, reason: null, attempt: 1);

        verdict.Action.Should().Be(CoverageAction.Accept);
        verdict.Status.Should().Be(CoverageStatus.Passed);
        verdict.Note.Should().Be("met the threshold");
    }

    [Fact]
    public void A_band_that_ARITHMETIC_CANNOT_REACH_does_not_fail_a_reply_for_it()
    {
        // #14707's shape. Coverage is quantised: at 32 cleaned lines the reachable values straddle the
        // band, and the reply is rejected for a fraction it could never have earned. The tolerance
        // forgives exactly one covered line.
        var oneLineShort = CoverageDecision.Evaluate(
            accounted: 22m, cleanLoc: 32m, capped: false, reason: null, attempt: 1);

        oneLineShort.Coverage.Should().BeLessThan(oneLineShort.Band);
        oneLineShort.Action.Should().Be(CoverageAction.Accept);
        oneLineShort.Note.Should().Contain("quantisation-tolerant");
    }

    [Fact]
    public void The_TOLERANCE_is_capped_so_a_tiny_change_cannot_forgive_everything()
    {
        // One line of a 20-line change is 5 %; of a 3-line change it would be 33 %. The cap is what stops
        // the slack from swallowing the band whole at small sizes.
        CoverageDecision.EffectiveThreshold(20m).Should().Be(0.70m - (1m / 20m));
        CoverageDecision.EffectiveThreshold(2m).Should().Be(0.70m - Inherited.MaxTolerance);
    }

    [Fact]
    public void A_change_with_almost_nothing_COVERABLE_is_neither_passed_nor_failed()
    {
        // #14735: a 6-line change whose coverable universe is 3 lines. Its steps quoted ALL THREE and a
        // flat ratio failed it at 50 %, three independent times. Both verdicts would overclaim, so the
        // gate says which one it is refusing to give.
        var verdict = CoverageDecision.Evaluate(
            accounted: 3m, cleanLoc: 6m, capped: false, reason: null, attempt: 1, coverableLines: 3);

        verdict.Action.Should().Be(CoverageAction.Accept);
        verdict.Status.Should().Be(CoverageStatus.TooThinToGate);
        verdict.Note.Should().Contain("no decomposition to regulate");
    }

    [Fact]
    public void A_change_too_SMALL_for_the_tolerance_to_mean_anything_is_also_ungateable()
    {
        var verdict = CoverageDecision.Evaluate(
            accounted: 1m, cleanLoc: 6m, capped: false, reason: null, attempt: 1);

        verdict.Status.Should().Be(CoverageStatus.TooThinToGate);
        verdict.Note.Should().Contain("one line short from materially short");
    }

    [Theory]
    [InlineData(100, 0.70)]
    [InlineData(300, 0.70)]
    [InlineData(301, 0.55)]
    [InlineData(800, 0.55)]
    [InlineData(801, 0.45)]
    [InlineData(5000, 0.45)]
    public void The_band_LOOSENS_with_size_and_that_limitation_is_carried_openly(decimal cleanLoc, decimal band)
    {
        // Measured: ungated median coverage runs 105 % at 26-100 cleaned lines and 27 % above 800, and the
        // mechanism was measured independently as LOC^-0.286. The source's own report also says the gate
        // "still loosens with size" — carried across rather than quietly fixed, and recorded in Inherited.
        CoverageDecision.BandFor(cleanLoc).Should().Be(band);
    }

    [Fact]
    public void A_LARGE_change_passes_under_its_own_band_while_a_bad_one_still_fails()
    {
        // #15058 (1032 lines) climbed 26 % -> 64 % on a re-ask. Under its own band it passes; #13918 at
        // 21 % does not. The loosening is not an amnesty, and this is the pair that shows it.
        CoverageDecision.Evaluate(660m, 1032m, capped: false, reason: null, attempt: 2)
            .Action.Should().Be(CoverageAction.Accept);

        CoverageDecision.Evaluate(217m, 1032m, capped: false, reason: null, attempt: 2)
            .Action.Should().Be(CoverageAction.Fail);
    }

    [Fact]
    public void A_first_shortfall_is_a_RETRY_and_the_second_is_a_failure()
    {
        var first = CoverageDecision.Evaluate(20m, 100m, capped: false, reason: null, attempt: 1);
        var second = CoverageDecision.Evaluate(20m, 100m, capped: false, reason: null, attempt: 2);

        first.Action.Should().Be(CoverageAction.Retry);
        first.Status.Should().Be(CoverageStatus.UnderThreshold);
        second.Action.Should().Be(CoverageAction.Fail);
        second.Status.Should().Be(CoverageStatus.HardFailure);
        second.Note.Should().Contain("after 2 attempts");
    }

    [Fact]
    public void A_cap_with_an_ADMISSIBLE_cause_is_accepted_and_says_it_was_capped()
    {
        var verdict = CoverageDecision.Evaluate(
            20m, 100m, capped: true, attempt: 1,
            reason: "The remaining lines are generated scaffold and repetitive constructor wiring that "
                + "carries no logic of its own, so it cannot be split into further units.");

        // Accepted, but NEVER indistinguishable from a clean pass — that is the whole reason the status is
        // a name rather than a boolean.
        verdict.Action.Should().Be(CoverageAction.Accept);
        verdict.Status.Should().Be(CoverageStatus.CappedSubstantive);
        verdict.Reason.Should().Be(CapReason.Substantive);
    }

    [Fact]
    public void A_cap_with_an_UNLISTED_cause_is_accepted_and_FLAGGED_rather_than_refused()
    {
        var verdict = CoverageDecision.Evaluate(
            20m, 100m, capped: true, attempt: 1,
            reason: "The change is a single algebraic reformulation of one solver step, which has no "
                + "meaningful decomposition into independent units of behaviour.");

        // Sincerity is not machine-decidable. A real cause nobody listed must stay sayable, so it is
        // flagged for a human rather than rejected outright or silently blessed.
        verdict.Status.Should().Be(CoverageStatus.CappedBorderline);
        verdict.Note.Should().Contain("needs a human read");
    }

    [Theory]
    [InlineData(null, "no reason given")]
    [InlineData("", "no reason given")]
    [InlineData("cannot decompose", "under the 40")]
    [InlineData("cannot be decomposed further; no further decomposition; as required; n/a", "restates the verdict")]
    public void A_reason_that_EXPLAINS_NOTHING_is_refused_however_it_is_phrased(string? reason, string why)
    {
        var (judged, note) = CoverageDecision.JudgeReason(reason);

        // The last case is the one worth having: long enough, well-formed, and made entirely of phrases
        // that restate the verdict. Length alone would have passed it.
        judged.Should().Be(CapReason.Empty);
        note.Should().Contain(why);
    }

    [Fact]
    public void A_cap_claimed_on_a_reply_that_ALREADY_PASSED_is_recorded_rather_than_treated_as_an_error()
    {
        var verdict = CoverageDecision.Evaluate(
            80m, 100m, capped: true, attempt: 1,
            reason: "The remaining lines are generated boilerplate with no logic to decompose further.");

        // The model apologised for work it had in fact done. Not an error, and worth keeping: it is the
        // kind of noise that turns into a prompt fix once it is countable.
        verdict.Status.Should().Be(CoverageStatus.Passed);
        verdict.Note.Should().Contain("cap claim ignored");
        verdict.Capped.Should().BeTrue();
    }

    [Fact]
    public void A_hard_failure_without_a_cap_says_so_rather_than_blaming_a_reason_nobody_gave()
    {
        CoverageDecision.Evaluate(10m, 100m, capped: false, reason: null, attempt: 2)
            .Note.Should().Contain("no cap claimed");
    }

    [Fact]
    public void NOTHING_COVERABLE_answers_zero_rather_than_throwing_or_passing()
    {
        // A change with nothing coverable cannot be covered, and must not pass by ACCIDENT through a
        // division nobody guarded.
        CoverageDecision.CoverageOf(5m, 0m).Should().Be(0m);
        CoverageDecision.CoverageOf(0m, 0m).Should().Be(0m);
    }

    [Fact]
    public void The_BOUNDARY_case_where_both_sides_are_the_same_number_by_different_routes_is_accepted()
    {
        // At 580 cleaned lines the one-line-short coverage and the threshold are mathematically equal and
        // differ in the last bit. A bare comparison rejected a reply the rule accepts — and did so at only
        // a scattering of sizes, which reads as noise rather than as a bug.
        var verdict = CoverageDecision.Evaluate(318m, 580m, capped: false, reason: null, attempt: 1);

        verdict.Action.Should().Be(CoverageAction.Accept);
    }

    [Fact]
    public void Both_the_BAND_and_the_THRESHOLD_are_reported_because_they_are_different_readings()
    {
        var verdict = CoverageDecision.Evaluate(22m, 32m, capped: false, reason: null, attempt: 1);

        // A reply that met the band and one that only met the tolerant threshold are not the same claim,
        // and a verdict carrying one number could not tell them apart.
        verdict.Band.Should().Be(0.70m);
        verdict.Threshold.Should().BeLessThan(verdict.Band);
    }
}
