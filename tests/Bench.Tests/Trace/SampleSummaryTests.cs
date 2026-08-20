using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Trace;

/// <summary>Readings, and the two things a bare number cannot say: how many there were, and whether anybody
/// took one.</summary>
public sealed class SampleSummaryTests
{
    [Fact]
    public void Not_sampled_is_not_zero()
    {
        var nothing = SampleSummary.Nothing("no vendor tool on this host");

        nothing.Sampled.Should().BeFalse();
        nothing.Describe.Should().Contain("not sampled").And.Contain("no vendor tool");
        nothing.Should().NotBe(SampleSummary.Of([0, 0, 0]),
            "a machine nobody read and a card that was genuinely idle are opposite readings of the same digit");
    }

    [Fact]
    public void A_sampler_that_ran_and_caught_nothing_has_still_measured_nothing()
    {
        SampleSummary.Of([]).Sampled.Should().BeFalse(
            "an empty set summarised as zeroes would be a claim about the machine rather than about the sampler");
    }

    [Fact]
    public void The_count_travels_because_a_maximum_over_two_is_not_a_maximum_over_two_thousand()
    {
        var thin = SampleSummary.Of([10, 90]);
        var thick = SampleSummary.Of([.. Enumerable.Repeat(90d, 1999).Append(10d)]);

        thin.Maximum.Should().Be(thick.Maximum);
        thin.Count.Should().Be(2);
        thick.Count.Should().Be(2000);
        thin.Mean.Should().NotBe(thick.Mean, "which is the whole reason the mean travels beside the extremes");
        thin.Describe.Should().Contain("2 sample(s)");
    }

    [Fact]
    public void A_reading_taken_while_the_card_was_SHARED_is_observed_and_never_attributed()
    {
        // Concurrent passes once co-loaded a coder and an embedder: 30 GB on a 32 GB card. "We used 20 GB"
        // and "somebody else held 20 GB and we got the rest" lead to opposite conclusions about the
        // configuration under test, so the two states are stored apart and never averaged.
        var shared = VramReading.Observed(SampleSummary.Of([20e9, 21e9]), "ollama: qwen3-coder");

        shared.Attribution.Should().Be(VramAttribution.Observed);
        shared.Describe.Should().Contain("OBSERVED").And.Contain("qwen3-coder");
    }

    [Fact]
    public void Only_a_leg_that_held_the_accelerator_alone_may_claim_its_number()
    {
        var alone = VramReading.Attributed(SampleSummary.Of([20e9]));

        alone.Attribution.Should().Be(VramAttribution.Attributed);
        alone.SharedWith.Should().BeEmpty();
        alone.Describe.Should().Contain("this leg alone");
    }

    [Fact]
    public void An_unsampled_reading_says_so_in_one_field_rather_than_two()
    {
        var none = VramReading.NotSampled("the sampler was not running");

        none.Attribution.Should().Be(VramAttribution.NotSampled);
        none.Bytes.Sampled.Should().BeFalse();
        none.Describe.Should().Contain("not sampled");
    }
}
