using Bench.Delivered;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Delivered;

/// <summary>The arm that decides whether the inherited badge may be retired.
/// <para>
/// Its subject is the instrument itself. Everything else in this module measures a change; this measures
/// whether the measurement can be gamed by volume — so the padding must be code that adds lines and cannot
/// add behaviour, and the generator's properties are what make that a fact about the text rather than an
/// assurance about it.
/// </para></summary>
public sealed class InflationArmTests
{
    // ---- the padding generator -----------------------------------------------------------------

    [Fact]
    public void The_padding_is_DETERMINISTIC_so_an_arm_can_be_rebuilt_by_somebody_who_was_not_there()
    {
        var first = InflationPadding.Generate("Acme.Pad", 3);
        var second = InflationPadding.Generate("Acme.Pad", 3);

        // No model writes any of it. The source's own precedent was hand-written padding and therefore not
        // reproducible — which is the flaw this exists to fix, not a detail of it.
        first.Should().BeEquivalentTo(second, o => o.WithStrictOrdering());
    }

    [Fact]
    public void The_padding_names_NOTHING_the_real_change_touches()
    {
        var files = InflationPadding.Generate("Acme.Pad", 2);
        var generated = InflationPadding.TypesIn(files);

        // "Behaviour-neutral" is checkable rather than claimed: every type the padding references is a type
        // the padding generated. It reads nothing the application writes and writes nothing it reads.
        var referenced = files
            .SelectMany(f => f.Content.Split([' ', '(', ')', '<', '>', ':', '\n', ';'], StringSplitOptions.RemoveEmptyEntries))
            .Where(token => token.StartsWith('G') && token.Length > 3 && char.IsDigit(token[1]))
            .Distinct();

        referenced.Should().OnlyContain(type => generated.Any(g => type.StartsWith(g, StringComparison.Ordinal)
            || g.StartsWith(type, StringComparison.Ordinal)));
    }

    [Fact]
    public void Every_generated_type_is_WIRED_because_dead_code_would_prove_nothing()
    {
        var files = InflationPadding.Generate("Acme.Pad", 1);
        var all = string.Join('\n', files.Select(f => f.Content));

        // Dead code is dismissed in a sentence and the arm proves nothing. The padding has to be the kind a
        // reviewer calls over-engineering, not the kind they call a mistake — so the registries construct
        // their members and a root constructs the registries.
        all.Should().Contain("new G01TimeoutFailure()").And.Contain("new G01NotEmptyRule()");
        files.Should().Contain(f => f.Path.EndsWith("G01Root.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void SCALE_multiplies_the_graph_rather_than_lengthening_one_file()
    {
        var one = InflationPadding.Generate("Acme.Pad", 1);
        var ten = InflationPadding.Generate("Acme.Pad", 10);

        // A x10 arm is ten realistic graphs, not one absurd file. The shape has to stay the shape a real
        // over-engineering attempt produces, or it is measuring a strawman.
        ten.Count.Should().Be(one.Count * 10);
        InflationPadding.Generate("Acme.Pad", 0).Should().BeEmpty();
    }

    [Fact]
    public void The_CLEANED_LINES_scale_with_it_which_is_the_arms_whole_premise()
    {
        var one = LocCalculator.Compute(InflationPadding.AsDiff(InflationPadding.Generate("Acme.Pad", 1)));
        var ten = LocCalculator.Compute(InflationPadding.AsDiff(InflationPadding.Generate("Acme.Pad", 10)));

        // "Ten times the cleaned lines" has to be true of the CLEANED figure, not of the file count — the
        // generator emits comments and blanks nowhere, but a future edit that did would make a x10 arm
        // quietly less than x10 and shift every exponent it produced.
        var ratio = ten.Cleaned / (double)one.Cleaned;

        ratio.Should().BeApproximately(10, 0.5);
        one.Cleaned.Should().BeGreaterThan(50, "an arm built on a handful of lines cannot outweigh a real change");
    }

    [Fact]
    public void The_padding_becomes_a_DIFF_the_same_pipeline_can_measure()
    {
        var diff = InflationPadding.AsDiff(InflationPadding.Generate("Acme.Pad", 1));

        // It has to go through the same cleaner and the same figures as the real change, or the two arms
        // are measured by two instruments and the comparison says nothing.
        var figures = LocCalculator.Compute(diff);

        diff.Should().StartWith("diff --git ");
        figures.Cleaned.Should().BeGreaterThan(50);
        figures.FilesCounted.Should().BeGreaterThan(10);
    }

    // ---- the verdict ---------------------------------------------------------------------------

    [Fact]
    public void An_instrument_that_RESISTS_inflation_passes_all_three_conditions()
    {
        // The source's passing shape: x10 the lines, x0.88 the score, exponent -0.06.
        var verdict = InflationArm.Measure(
            new ArmReading("honest", 100, 50, StepsScoredZero: 0, Steps: 20),
            new ArmReading("padded", 1000, 44, StepsScoredZero: 32, Steps: 52),
            honestBefore: 52);

        verdict.Exponent.Should().BeLessThan(0);
        verdict.Passed.Should().BeTrue();
        verdict.Describe.Should().Contain("volume bought nothing");
    }

    [Fact]
    public void An_instrument_that_PAYS_for_volume_fails_and_says_which_condition()
    {
        // The source's failing shape under the 1-10 scale: x10 the lines, x1.7 the score, exponent 0.22.
        var verdict = InflationArm.Measure(
            new ArmReading("honest", 100, 50, 0, 20),
            new ArmReading("padded", 1000, 85, StepsScoredZero: 0, Steps: 52));

        verdict.Exponent.Should().BeApproximately(0.23, 0.02);
        verdict.Passed.Should().BeFalse();
        verdict.Note.Should().Contain("volume still bought score");
    }

    [Fact]
    public void A_NEUTRAL_exponent_is_not_enough_on_its_own()
    {
        // A scale that had stopped paying for ANYTHING would produce a lovely exponent. Requiring the
        // padded steps to land on zero is what separates "resists inflation" from "measures nothing".
        var verdict = InflationArm.Measure(
            new ArmReading("honest", 100, 50, 0, 20),
            new ArmReading("padded", 1000, 40, StepsScoredZero: 0, Steps: 52));

        verdict.VolumeBoughtNothing.Should().BeTrue();
        verdict.Passed.Should().BeFalse();
        verdict.Note.Should().Contain("not discriminating");
    }

    [Fact]
    public void An_honest_score_that_COLLAPSED_fails_even_with_a_perfect_exponent()
    {
        // The check most easily forgotten, and the reason the source re-ran its honest arms rather than
        // reusing their old scores: a band that quietly deflates real work looks exactly like one that
        // resists padding, from the exponent alone.
        var verdict = InflationArm.Measure(
            new ArmReading("honest", 100, 20, 0, 20),
            new ArmReading("padded", 1000, 18, 32, 52),
            honestBefore: 50);

        verdict.VolumeBoughtNothing.Should().BeTrue();
        verdict.PaddingScoredZero.Should().BeTrue();
        verdict.Passed.Should().BeFalse();
        verdict.Note.Should().Contain("deflating real work");
    }

    [Fact]
    public void With_NO_earlier_score_the_third_leg_is_UNVERIFIED_rather_than_passed()
    {
        var verdict = InflationArm.Measure(
            new ArmReading("honest", 100, 50, 0, 20),
            new ArmReading("padded", 1000, 44, 32, 52));

        // The same three-state honesty as everywhere else here: nobody measured it, which is not the same
        // as it having held. The arm still passes — but the note says which leg nobody checked.
        verdict.Passed.Should().BeTrue();
        verdict.Note.Should().Contain("UNVERIFIED");
    }

    [Theory]
    [InlineData(0, 50, 1000, "no cleaned lines")]
    [InlineData(100, 0, 1000, "scored zero")]
    [InlineData(100, 50, 90, "not larger")]
    public void An_UNMEASURABLE_arm_says_why_rather_than_producing_a_number(
        int honestLoc, int honestScore, int paddedLoc, string why)
    {
        var verdict = InflationArm.Measure(
            new ArmReading("honest", honestLoc, honestScore, 0, 5),
            new ArmReading("padded", paddedLoc, 40, 10, 20));

        // A ratio against zero, or against a padded arm that is not bigger, is arithmetic that produces a
        // number and means nothing — which is worse than refusing, because it would be published.
        verdict.Passed.Should().BeFalse();
        verdict.Note.Should().Contain(why);
    }

    [Fact]
    public void Both_thresholds_carry_the_measurement_they_were_inherited_from()
    {
        // Neither has been re-measured on this corpus, and both are the arm's own knobs — an arm whose
        // pass condition nobody can source is an arm that passes whatever it was tuned to pass.
        InflationArm.NeutralExponent.Should().Be(0.05);
        InflationArm.HonestTolerance.Should().Be(0.15);
    }
}
