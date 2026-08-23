using Bench.Delivered;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Delivered;

/// <summary>The scale, and the protocol string that travels with every score produced under it.
/// <para>
/// These are mostly PARITY assertions, and that is the point: the ten anchor lines are inherited because
/// their stability is measured — models quote the matching line back and land on its score reproducibly
/// over 2,088 production units — and a port that "improved" the wording would have discarded exactly that
/// evidence. So the tests pin the text rather than describing it.
/// </para></summary>
public sealed class WeightingProtocolTests
{
    [Fact]
    public void The_floor_is_ZERO_because_a_floor_is_a_payment()
    {
        // The inflation finding in one assertion. With a floor of 1, 158 padded steps priced at 1-2
        // out-totalled the real work beside them and supplied 46 % of an inflated score — the weigher had
        // already judged them worthless and had no number to say so with.
        WeightingProtocol.MinScore.Should().Be(0);
        WeightingProtocol.MaxScore.Should().Be(10);
    }

    [Fact]
    public void A_score_OFF_the_scale_is_refused_rather_than_clamped()
    {
        // A weigher answering 11 has not produced a low-confidence reading; it has produced something this
        // protocol cannot record. Clamping would turn a parse failure into a plausible number.
        WeightingProtocol.IsOnScale(0).Should().BeTrue();
        WeightingProtocol.IsOnScale(10).Should().BeTrue();
        WeightingProtocol.IsOnScale(11).Should().BeFalse();
        WeightingProtocol.IsOnScale(-1).Should().BeFalse();
    }

    [Fact]
    public void The_TEN_anchor_lines_are_carried_verbatim()
    {
        var lines = WeightingProtocol.AnchorScale.Split('\n');

        lines.Should().HaveCount(10);
        lines[0].Should().Be(
            "  1  a one-line declarative change: add a field to a mapping, a constant to a list, a label");
        lines[9].Should().Be(" 10  reworking a core rule the rest of the system is built on");
    }

    [Fact]
    public void The_ZERO_BAND_is_kept_OUT_of_the_inherited_block()
    {
        // Separated on purpose: the ten lines are the measured artefact and must stay byte-identical, so
        // the new band is rendered above them rather than woven in.
        WeightingProtocol.AnchorScale.Should().NotContain("  0  ");
        WeightingProtocol.ZeroAnchor.Should().StartWith("  0  the step serves nothing");
    }

    [Fact]
    public void The_zero_RULE_defines_the_band_by_WHAT_REACHES_IT_rather_than_by_size()
    {
        // The failure mode to design against is over-use, not under-use: "no new logic" describes a
        // translation label for a shipped checkbox as well as it describes a registry nothing reads, and
        // the first is a real 1.
        WeightingProtocol.ZeroRule.Should()
            .Contain("not for work that is small")
            .And.Contain("actually uses is a 1")
            .And.Contain("behave differently");
    }

    [Fact]
    public void The_rule_covers_the_two_cases_a_size_heuristic_gets_WRONG()
    {
        // Both are mechanical-looking and both are real work: a declaration the runtime enforces changes
        // what happens when something is WRONG, and an established deletion means somebody proved nothing
        // reaches it. A rule that only said "small is zero" would price both at nothing.
        WeightingProtocol.ZeroRule.Should()
            .Contain("failure paths")
            .And.Contain("Deleting code is a 1 when the deletion had to be established");

        // …and the counter-case, so the deletion clause is not a blanket credit.
        WeightingProtocol.ZeroRule.Should().Contain("Deleting a comment").And.Contain("stays a 0");
    }

    [Fact]
    public void The_band_with_NO_EXAMPLE_is_named_rather_than_left_to_inference()
    {
        // Zero of 2,540 scored units ever landed on 10. "The anchors stop at 9" is a fact a reader must
        // not have to infer from the length of a list.
        WeightingProtocol.BandWithoutExample.Should().Be(10);
    }

    [Fact]
    public void The_protocol_string_ACKNOWLEDGES_what_it_inherited()
    {
        // A score is comparable only with scores produced by the same protocol, so the string is part of
        // the measurement. Naming the inheritance inside it is what lets a console badge a run without a
        // second table saying which runs deserve one.
        WeightingProtocol.Protocol.Should()
            .StartWith("delivered-work-v1")
            .And.Contain("anchors inherited")
            .And.Contain("scoreMeter diff-weighting-v3");
    }

    [Fact]
    public void The_few_shot_examples_are_NOT_inherited_and_the_reason_is_recorded()
    {
        // They quote another repository and name its pull requests. As few-shots against a .NET target
        // they would teach the shape of a Symfony diff rather than the meaning of a band — and the
        // source's own admission rule is that examples enter only where history agrees.
        WeightingProtocol.WhyNoExamples.Should().Contain("this project's own history");
        WeightingProtocol.Scale.Should().NotContain("PR #");
    }

    [Fact]
    public void The_rendered_scale_puts_zero_ABOVE_the_ten_and_the_rule_below_them()
    {
        var scale = WeightingProtocol.Scale;

        scale.IndexOf(WeightingProtocol.ZeroAnchor, StringComparison.Ordinal)
            .Should().BeLessThan(scale.IndexOf(WeightingProtocol.AnchorScale, StringComparison.Ordinal));
        scale.IndexOf(WeightingProtocol.AnchorScale, StringComparison.Ordinal)
            .Should().BeLessThan(scale.IndexOf(WeightingProtocol.ZeroRule, StringComparison.Ordinal));
    }
}
