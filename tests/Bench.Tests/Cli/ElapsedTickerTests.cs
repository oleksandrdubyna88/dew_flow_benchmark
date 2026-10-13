using Bench.Cli;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>Review finding (UX, E2 code round): a paid launch runs a minute or more and the console said nothing until it ended. The ticker
/// awaits the work and, every interval while it runs, hands the elapsed time to a line writer — then stops with the work, however the work
/// ended, and never ticks after it. Real time here, with wide margins: no fake clock is wired into <see cref="PeriodicTimer"/> in this suite.</summary>
public sealed class ElapsedTickerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_tick_fires_every_interval_while_the_work_runs_and_stops_with_it()
    {
        var ticks = new List<TimeSpan>();

        var result = await ElapsedTicker.WithProgressAsync(Delayed(7, 700), TimeSpan.FromMilliseconds(100), ticks.Add, Ct);

        result.Should().Be(7);
        ticks.Should().HaveCountGreaterThan(1, "700 ms of work at a 100 ms cadence is several ticks even on a loaded machine").And.BeInAscendingOrder();
        ticks.Should().OnlyContain(t => t > TimeSpan.Zero && t < TimeSpan.FromSeconds(5));
        var after = ticks.Count;
        await Task.Delay(300, Ct);
        ticks.Count.Should().Be(after, "no tick after the work ended");
    }

    [Fact]
    public async Task Work_that_ends_at_once_never_ticks_and_a_faulted_work_still_surfaces_its_exception()
    {
        var ticks = new List<TimeSpan>();

        (await ElapsedTicker.WithProgressAsync(Task.FromResult("done"), TimeSpan.FromMilliseconds(50), ticks.Add, Ct)).Should().Be("done");
        await Task.Delay(200, Ct);
        ticks.Should().BeEmpty();

        var faulted = () => ElapsedTicker.WithProgressAsync(Faulted(), TimeSpan.FromMilliseconds(50), ticks.Add, Ct);

        await faulted.Should().ThrowAsync<InvalidOperationException>().WithMessage("the work failed");
    }

    private static async Task<int> Delayed(int value, int milliseconds)
    {
        await Task.Delay(milliseconds, Ct);
        return value;
    }

    private static async Task<int> Faulted()
    {
        await Task.Delay(120, Ct);
        throw new InvalidOperationException("the work failed");
    }
}
