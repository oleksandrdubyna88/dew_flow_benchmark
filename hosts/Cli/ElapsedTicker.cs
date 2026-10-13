using System.Diagnostics;

namespace Bench.Cli;

/// <summary>A long await said aloud (E2 code round): the work is awaited as it is, and every <c>every</c> while it runs the elapsed time is
/// handed to <c>tick</c> — the console's "still running" line for a paid launch that takes a minute or more. The ticking stops with the work,
/// however it ended, and never fires after it; the work's own result or exception passes through untouched. A <see cref="PeriodicTimer"/>,
/// as the rule asks — never a <c>System.Threading.Timer</c>.</summary>
public static class ElapsedTicker
{
    public static async Task<T> WithProgressAsync<T>(Task<T> work, TimeSpan every, Action<TimeSpan> tick, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        using var done = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ticking = TickAsync(every, () => tick(Stopwatch.GetElapsedTime(started)), done.Token);

        try
        {
            return await work;
        }
        finally
        {
            await done.CancelAsync();
            await ticking;
        }
    }

    private static async Task TickAsync(TimeSpan every, Action tick, CancellationToken token)
    {
        using var timer = new PeriodicTimer(every);

        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                tick();
            }
        }
        catch (OperationCanceledException)
        {
            // The work ended; whatever ticked, ticked before it.
        }
    }
}
