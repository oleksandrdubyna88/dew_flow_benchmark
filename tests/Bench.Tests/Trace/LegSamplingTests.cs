using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Trace;

/// <summary>What one leg may claim out of a stream somebody else was taking.
/// <para>
/// The judgements are all here: which readings belong to this leg, whether there are enough to say anything,
/// and whether a VRAM figure is this leg's or merely beside it. The last one is a DECISION rather than a
/// subtraction, inherited from the sidecar, and it is the difference between "we used 20 GB" and "somebody
/// else held 20 GB and we got the rest".
/// </para></summary>
public sealed class LegSamplingTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_window_is_HALF_OPEN_so_one_reading_cannot_inflate_two_legs()
    {
        // Legs run back to back. A sample taken at the instant one ended belongs to what came next, and a
        // boundary that counted both ways would let a single reading appear in two measurements.
        var samples = new[] { Load(0, cpu: 10), Load(10, cpu: 90) };

        var first = LegSampling.Over(samples, [], Start, Start.AddSeconds(10), false, string.Empty);

        first.CpuPercent.Count.Should().Be(1, "the reading at the closing instant belongs to the NEXT leg");
        first.CpuPercent.Maximum.Should().Be(10);
    }

    [Fact]
    public void A_leg_with_no_duration_inherits_nothing_from_whatever_ran_before_it()
    {
        var samples = new[] { Load(0, cpu: 50), Load(1, cpu: 50) };

        var none = LegSampling.Over(samples, [], Start, Start, false, string.Empty);

        none.Any.Should().BeFalse();
        none.CpuPercent.Reason.Should().Contain("no measurable window");
    }

    [Fact]
    public void A_leg_shorter_than_the_slow_streams_cadence_says_so_rather_than_reporting_nothing_happened()
    {
        // The consequence of the measured cost: a VRAM read is about a second, so a ten-second leg may catch
        // one or none. Saying "not sampled, and here is why" is legible; a smooth min/max would be fiction.
        var load = new[] { Load(1, cpu: 40), Load(3, cpu: 60) };

        var leg = LegSampling.Over(load, [], Start, Start.AddSeconds(10), false, string.Empty);

        leg.CpuPercent.Sampled.Should().BeTrue();
        leg.Vram.Bytes.Sampled.Should().BeFalse();
        leg.Vram.Bytes.Reason.Should().Contain("about a second each");
        leg.Describe.Should().Contain("not sampled");
    }

    [Fact]
    public void A_leg_that_did_NOT_hold_the_accelerator_alone_gets_an_OBSERVED_figure_naming_what_it_shared_with()
    {
        var leg = LegSampling.Over(
            [Load(1, cpu: 40)],
            [Vram(1, 20_000_000_000), Vram(4, 21_000_000_000)],
            Start,
            Start.AddSeconds(10),
            heldAcceleratorAlone: false,
            sharedWith: "ollama: qwen3-coder");

        leg.Vram.Attribution.Should().Be(VramAttribution.Observed);
        leg.Vram.Bytes.Maximum.Should().Be(21_000_000_000);
        leg.Vram.Describe.Should().Contain("OBSERVED").And.Contain("qwen3-coder");
    }

    [Fact]
    public void Only_a_leg_that_held_it_alone_may_call_the_number_its_own()
    {
        var leg = LegSampling.Over(
            [], [Vram(2, 18_000_000_000)], Start, Start.AddSeconds(10), heldAcceleratorAlone: true, string.Empty);

        leg.Vram.Attribution.Should().Be(VramAttribution.Attributed);
        leg.Vram.Describe.Should().Contain("this leg alone");
    }

    [Fact]
    public void The_window_travels_because_it_is_the_denominator_of_every_count()
    {
        // Two samples across ten seconds and two across ten minutes are different evidence, and a reader
        // holding only the count cannot tell them apart.
        var brief = LegSampling.Over([Load(1, 40), Load(2, 40)], [], Start, Start.AddSeconds(10), false, string.Empty);
        var long_ = LegSampling.Over([Load(1, 40), Load(2, 40)], [], Start, Start.AddMinutes(10), false, string.Empty);

        brief.CpuPercent.Count.Should().Be(long_.CpuPercent.Count);
        brief.Window.Should().Be(TimeSpan.FromSeconds(10));
        long_.Window.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void A_host_with_nothing_to_sample_produces_a_leg_that_says_so_and_not_a_leg_of_zeroes()
    {
        var leg = LegSampling.Over([], [], Start, Start.AddSeconds(10), false, string.Empty);

        leg.Any.Should().BeFalse();
        leg.CpuPercent.Maximum.Should().Be(0);
        leg.CpuPercent.Sampled.Should().BeFalse("which is what keeps that zero from being read as an idle CPU");
    }

    private static LoadSample Load(int second, double cpu) =>
        new(Start.AddSeconds(second), cpu, RamBytesUsed: 40_000_000_000, RamBytesTotal: 98_374_103_040);

    private static VramSample Vram(int second, long used) =>
        new(Start.AddSeconds(second), used, TotalBytes: 34_208_743_424);
}
