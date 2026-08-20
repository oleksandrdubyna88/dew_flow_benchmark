using Bench.Domain.Trace;
using Bench.Infrastructure.Machine;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The buffer a long campaign lives beside.
/// <para>
/// Neither rule here is about the readings — those are the domain's. These are about the thing that runs for
/// days: it must stay bounded, and answering a window must not destroy it. Both are properties a timing test
/// would prove slowly and badly, which is why the buffer is its own type.
/// </para></summary>
public sealed class SampleWindowTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Reading_a_window_LEAVES_the_samples_where_they_are()
    {
        // The reason the port takes an interval rather than draining: two legs in one process would each get
        // half the readings and neither would know it.
        var window = Filled(10);

        var first = window.Between(Start, Start.AddSeconds(10));
        var second = window.Between(Start, Start.AddSeconds(10));

        first.Should().HaveCount(10);
        second.Should().HaveCount(first.Count);
        window.Count.Should().Be(10, "asking is not taking");
    }

    [Fact]
    public void The_window_is_half_open_so_two_back_to_back_legs_cannot_both_claim_one_reading()
    {
        var window = Filled(10);

        var first = window.Between(Start, Start.AddSeconds(5));
        var next = window.Between(Start.AddSeconds(5), Start.AddSeconds(10));

        first.Should().HaveCount(5);
        next.Should().HaveCount(5);
        first.Concat(next).Distinct().Should().HaveCount(10, "no reading appears in both");
    }

    [Fact]
    public void A_window_nobody_sampled_is_EMPTY_rather_than_the_nearest_readings()
    {
        // Returning the closest samples would attribute another leg's machine state to this one — the defect
        // the half-open boundary prevents, one level up.
        Filled(10).Between(Start.AddHours(-2), Start.AddHours(-1)).Should().BeEmpty();
    }

    [Fact]
    public void Everything_older_than_the_cutoff_is_retired_so_the_buffer_cannot_grow_with_uptime()
    {
        var window = Filled(100);

        window.Retire(Start.AddSeconds(90));

        window.Count.Should().Be(10, "a campaign runs for days; a collection sized by uptime is a slow leak");
        window.Between(DateTimeOffset.MinValue, DateTimeOffset.MaxValue)
            .Should().OnlyContain(s => s.TakenAt >= Start.AddSeconds(90));
    }

    [Fact]
    public void A_reader_that_had_nothing_to_give_adds_nothing()
    {
        // A failed probe is an ABSENT sample, never a zero — which is what keeps a summary able to say
        // "not sampled" instead of reporting an idle machine.
        var window = new SampleWindow<LoadSample>(s => s.TakenAt);

        window.Add(null);

        window.Count.Should().Be(0);
    }

    private static SampleWindow<LoadSample> Filled(int count)
    {
        var window = new SampleWindow<LoadSample>(s => s.TakenAt);

        foreach (var second in Enumerable.Range(0, count))
        {
            window.Add(new LoadSample(Start.AddSeconds(second), second, second, 100));
        }

        return window;
    }
}
