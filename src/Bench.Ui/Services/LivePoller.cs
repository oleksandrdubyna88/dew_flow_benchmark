namespace Bench.Ui.Services;

/// <summary>A disposal-safe poll on a <see cref="PeriodicTimer"/> — the durable-status rule's "poll while something is in flight,
/// self-terminating when idle" (<c>.claude/rules/shared/common/durable-status.md</c> rule 3), written once for the console.
/// <list type="bullet">
/// <item><b>Started only on demand, at most once.</b> <see cref="Start"/> while running is a no-op, so a page may call it after
/// every read without stacking a second loop.</item>
/// <item><b>Stopped from inside a tick.</b> <see cref="Stop"/> disposes the timer, so the loop ends at its next wait and no further
/// tick runs — the page stops polling the moment a read says nothing is in flight.</item>
/// <item><b>Never after disposal.</b> <see cref="DisposeAsync"/> cancels the ONE token every tick of every loop receives and disposes
/// the timer; a <see cref="Start"/> after it is refused. It does NOT wait a running tick out — a read that ignores cancellation
/// would hold the page's teardown hostage — so a tick checks its token before it renders: a late HTTP answer is still a late
/// answer, and it is dropped. The token's source is never disposed for the same reason: a tick still finishing may read it.</item>
/// </list>
/// <para>
/// The <see cref="TimeProvider"/> is the seam that makes the poll testable without sleeping: a test advances a manual clock and the
/// timer fires; production gets <see cref="TimeProvider.System"/> from <c>AddBenchUi</c>.
/// </para></summary>
public sealed class LivePoller(TimeProvider clock, TimeSpan interval) : IAsyncDisposable
{
    private readonly CancellationTokenSource _life = new();
    private PeriodicTimer? _timer;
    private bool _disposed;

    public bool IsRunning => _timer is not null;

    public void Start(Func<CancellationToken, Task> tick)
    {
        if (_disposed || _timer is not null)
        {
            return;
        }

        _timer = new PeriodicTimer(interval, clock);
        _ = LoopAsync(_timer, tick, _life.Token);
    }

    /// <summary>Ends the poll: the timer is disposed, so the loop's next wait answers "no more ticks". Safe from inside a tick.</summary>
    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        // The timer goes first and synchronously: whatever the cancellation's callbacks do, no tick can be scheduled after this line.
        _disposed = true;
        Stop();
        await _life.CancelAsync();
    }

    private static async Task LoopAsync(PeriodicTimer timer, Func<CancellationToken, Task> tick, CancellationToken token)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                await tick(token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Disposed mid-wait or mid-tick: the page is gone, and nothing is left to tell.
        }
    }
}
