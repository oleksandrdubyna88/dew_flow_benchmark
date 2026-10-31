using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ModelArms.Core.Ports;

namespace ModelArms.Adapters;

/// <summary>The Windows keep-awake (D12): <c>SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED)</c> — <c>0x80000003</c>, as <c>keep-awake.ps1</c> sets it — held for the scope and released with <c>ES_CONTINUOUS</c> alone (<c>0x80000000</c>).
/// <para>
/// <b>One dedicated thread.</b> The execution state belongs to the thread that set it, and an async continuation after an <c>await</c> can run on another one — so the set, the wait and the release are one thread's body (E5 §8 item 15). A previous state of 0 is the call failing:
/// one line says the host may sleep, the thread is released and the chain goes on (a keep-awake never fails the chain).
/// </para>
/// <para>
/// <b>Not proven on this host</b> (Linux/WSL): no Windows machine ran it; <c>WindowsAwakeTests</c> asserts the previous state is non-zero and that the set and the release ran on one managed thread, on Windows only. The P/Invoke is <c>DllImport</c> like the module's others (<c>RealPath</c>, <c>KernelFacts</c>).
/// </para></summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsAwake : IHostAwake
{
    private const uint Continuous = 0x80000000;
    private const uint Held = Continuous | 0x00000001 | 0x00000002;

    private static readonly TimeSpan JoinAtMost = TimeSpan.FromSeconds(10);

    private readonly ManualResetEventSlim _release = new(initialState: false);
    private readonly Thread _thread;
    private readonly TaskCompletionSource<(uint Previous, int Thread)> _held = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<int> _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private WindowsAwake()
    {
        _thread = new Thread(Body) { IsBackground = true, Name = "model-arms keep-awake" };
    }

    private volatile string _failure = string.Empty;

    /// <summary>What it does — or, when the call failed (a previous state of 0, or the thread faulted), that the host may sleep. The factory writes <c>keep-awake: &lt;Describe&gt;</c>; this class writes nothing.</summary>
    public string Describe => Previous == 0 ? $"SetThreadExecutionState failed — the host may sleep{_failure}" : "SetThreadExecutionState (system and display), held on one thread";

    /// <summary>The state the call replaced; 0 when it failed.</summary>
    internal uint Previous { get; private set; }

    /// <summary>The managed thread id the set ran on, and the one the release ran on (the same, by construction).</summary>
    internal int HeldOnThread { get; private set; }

    internal Task<int> ReleasedOnThread => _released.Task;

    /// <summary>Starts the thread and awaits, until <paramref name="ct"/>, the previous state it handed back (on cancellation the thread is released first).</summary>
    public static async Task<WindowsAwake> StartAsync(CancellationToken ct)
    {
        var awake = new WindowsAwake();
        awake._thread.Start();
        try
        {
            var (previous, thread) = await awake._held.Task.WaitAsync(ct);
            awake.Previous = previous;
            awake.HeldOnThread = thread;
            return awake;
        }
        catch (OperationCanceledException)
        {
            await awake.DisposeAsync();
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        _release.Set();
        if (_thread.IsAlive)
        {
            _thread.Join(JoinAtMost);
        }

        return ValueTask.CompletedTask;
    }

    private void Body()
    {
        try
        {
            var previous = SetThreadExecutionState(Held);
            _held.TrySetResult((previous, Environment.CurrentManagedThreadId));
            if (previous != 0)
            {
                _release.Wait();
                SetThreadExecutionState(Continuous);
            }

            _released.TrySetResult(Environment.CurrentManagedThreadId);
        }
        catch (Exception ex)
        {
            _failure = $" ({ex.GetType().Name}: {ex.Message})";
            _held.TrySetResult((0, Environment.CurrentManagedThreadId));
            _released.TrySetResult(Environment.CurrentManagedThreadId);
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "SetThreadExecutionState")]
    private static extern uint SetThreadExecutionState(uint flags);
}
