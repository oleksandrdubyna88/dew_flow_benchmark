using System.Runtime.InteropServices;

namespace Bench.Cli;

/// <summary>Ctrl+C and SIGTERM, turned into the root cancellation token every verb runs under.
/// <para>
/// Without this the harness had no signal handling at all, which made <b>every</b> orchestrator stop
/// the exact equivalent of a crash: the leg in flight died mid-settle and its cell stayed Claimed, so
/// a planned restart began by stranding work. A planned stop must instead produce a resumable state —
/// claim nothing more, let the leg in flight settle, exit saying so.
/// </para>
/// <para>
/// <b>The second signal is not intercepted.</b> An operator who signals twice has stopped waiting for
/// a graceful stop, and a harness that refuses to die is a harness someone kills with a bigger hammer.
/// </para></summary>
public sealed class ShutdownSignal : IDisposable
{
    private readonly CancellationTokenSource _stopping = new();
    private readonly List<IDisposable> _signals = [];
    private readonly TextWriter _notice;
    private int _requested;

    private ShutdownSignal(TextWriter notice) => _notice = notice;

    /// <summary>The token every command runs under. Cancelled once, by the first signal.</summary>
    public CancellationToken Token => _stopping.Token;

    public static ShutdownSignal Install(TextWriter notice)
    {
        var signal = new ShutdownSignal(notice);

        Console.CancelKeyPress += signal.OnConsoleCancel;
        signal.Watch(PosixSignal.SIGTERM);
        signal.Watch(PosixSignal.SIGQUIT);

        return signal;
    }

    /// <summary>What a handler calls. Public because the guarantee — first signal stops gracefully,
    /// second one is left to the runtime — is worth a test, and raising a real SIGTERM at a test process
    /// to get one is a way to lose a test run rather than to prove something.</summary>
    /// <returns>Whether this call handled the signal. <c>false</c> means "let it through".</returns>
    public bool Request(string source)
    {
        if (Interlocked.Exchange(ref _requested, 1) == 1)
        {
            return false;
        }

        _notice.WriteLine($"bench: {source} — finishing the leg in flight, then stopping. Signal again to abort.");
        _stopping.Cancel();
        return true;
    }

    public void Dispose()
    {
        Console.CancelKeyPress -= OnConsoleCancel;

        foreach (var signal in _signals)
        {
            signal.Dispose();
        }

        _stopping.Dispose();
    }

    /// <summary>A signal this platform does not map is not an error — it is one fewer way to be asked to
    /// stop, and the others still work.</summary>
    private void Watch(PosixSignal signal)
    {
        try
        {
            _signals.Add(PosixSignalRegistration.Create(signal, context => context.Cancel = Request(signal.ToString())));
        }
        catch (PlatformNotSupportedException)
        {
            // Left unwatched deliberately; Console.CancelKeyPress covers the interactive case everywhere.
        }
    }

    private void OnConsoleCancel(object? sender, ConsoleCancelEventArgs e) => e.Cancel = Request("Ctrl+C");
}
