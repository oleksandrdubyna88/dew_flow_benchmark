using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Bench.Application;
using Bench.Domain.Trace;
using Bench.Infrastructure.Process;
using Microsoft.Extensions.Logging;

namespace Bench.Infrastructure.Machine;

/// <param name="Load">How often the cheap stream ticks — processor and memory, microseconds apiece.</param>
/// <param name="Vram">How often the expensive one does. Measured at about a second per read on Windows
/// (<c>research/PLAN_hardware_sampler.md</c> §7.3), so this is deliberately an order of magnitude slower than
/// <paramref name="Load"/>: a sampler that spent half the wall clock sampling would be measuring itself.</param>
/// <param name="Window">How much history to keep. The buffer is bounded on purpose — this process runs for
/// days, and a list that only grows is the shape two <c>GetOrAdd</c>-forever maps already had here.</param>
public readonly record struct SamplingCadence(TimeSpan Load, TimeSpan Vram, TimeSpan Window)
{
    public static SamplingCadence Default { get; } =
        new(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(30));
}

/// <summary>Reads the machine in the background and answers by window.
/// <para>
/// <b>Two clocks, because the readings cost three orders of magnitude apart.</b> Processor and memory come
/// from process times and <c>GC.GetGCMemoryInfo</c> — no file, no launch. VRAM on Windows goes through PDH,
/// the only vendor-neutral path, and costs about a second per read; it therefore ticks rarely and its
/// summaries carry a count so a short leg's thin evidence reads as thin.
/// </para>
/// <para>
/// <b>Nothing it does may fail a leg.</b> Every tick is wrapped: a reader that throws stops contributing and
/// the leg's summary says <em>not sampled</em>, which is a state the shapes carry. A benchmark that lost a
/// campaign because a performance counter went away would be a far worse trade.
/// </para></summary>
public sealed class HardwareSampler : IHardwareSampler, IAsyncDisposable
{
    private readonly SampleWindow<LoadSample> _load = new(s => s.TakenAt);
    private readonly SampleWindow<VramSample> _vram = new(s => s.TakenAt);
    private readonly CancellationTokenSource _stopping = new();
    private readonly ILogger<HardwareSampler> _logger;
    private readonly SamplingCadence _cadence;
    private readonly TimeProvider _clock;
    private readonly Task _loop;

    private TimeSpan _lastCpuTime;
    private DateTimeOffset _lastCpuAt;

    public HardwareSampler(ILogger<HardwareSampler> logger, TimeProvider clock, SamplingCadence cadence)
    {
        _logger = logger;
        _clock = clock;
        _cadence = cadence;
        _lastCpuAt = clock.GetUtcNow();
        _lastCpuTime = CurrentProcessorTime();
        _loop = Task.Run(() => RunAsync(_stopping.Token));
    }

    public Task<(IReadOnlyList<LoadSample> Load, IReadOnlyList<VramSample> Vram)> ReadAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        Task.FromResult((_load.Between(from, to), _vram.Between(from, to)));

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();

        try
        {
            await _loop;
        }
        catch (OperationCanceledException)
        {
            // The ordinary way this ends.
        }

        _stopping.Dispose();
    }

    /// <summary>One loop, two clocks. A second timer would be a second thing to stop, and the slow stream is
    /// simply "every Nth tick" — which also keeps the expensive read off the same instant as a cheap one.</summary>
    private async Task RunAsync(CancellationToken stopping)
    {
        using var timer = new PeriodicTimer(_cadence.Load);
        var everyNth = Math.Max(1, (int)(_cadence.Vram.Ticks / Math.Max(1, _cadence.Load.Ticks)));
        var tick = 0L;

        while (await timer.WaitForNextTickAsync(stopping))
        {
            _load.Add(ReadLoad());

            if (++tick % everyNth == 0)
            {
                _vram.Add(await ReadVramAsync(stopping));
            }

            var cutoff = _clock.GetUtcNow() - _cadence.Window;
            _load.Retire(cutoff);
            _vram.Retire(cutoff);
        }
    }

    /// <summary>Processor and memory, from what the runtime already knows.
    /// <para>
    /// CPU is a DELTA of processor time over wall time, which is the only way a percentage means anything —
    /// a total says how much work has been done since the process started, not how busy it is now. Memory is
    /// <c>GC.GetGCMemoryInfo</c>'s machine-wide load, which is what the collector itself uses for its
    /// heuristics: free, cross-platform, and honest about a container's limit where a raw physical read
    /// would report the host's.
    /// </para></summary>
    private LoadSample? ReadLoad()
    {
        try
        {
            var now = _clock.GetUtcNow();
            var processorTime = CurrentProcessorTime();
            var elapsed = now - _lastCpuAt;
            var used = processorTime - _lastCpuTime;

            _lastCpuAt = now;
            _lastCpuTime = processorTime;

            var memory = GC.GetGCMemoryInfo();

            return elapsed <= TimeSpan.Zero
                ? null
                : new LoadSample(
                    now,
                    100 * used.TotalMilliseconds / (elapsed.TotalMilliseconds * Math.Max(1, Environment.ProcessorCount)),
                    memory.MemoryLoadBytes,
                    memory.TotalAvailableMemoryBytes);
        }
        catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException)
        {
            _logger.LogDebug(ex, "The processor and memory reading is unavailable; this stream stops contributing");
            return null;
        }
    }

    /// <summary>What is on the card, machine-wide.
    /// <para>
    /// PDH rather than a vendor tool, because it is the one path that covers AMD, NVIDIA and Intel alike —
    /// and this machine's discrete card has no <c>nvidia-smi</c> to ask. It costs about a second, which is
    /// why the caller ticks it rarely; sampling it at the cheap stream's cadence would have the sampler
    /// spending half the wall clock on itself, measured rather than assumed.
    /// </para>
    /// <para>
    /// The TOTAL is not read here. It is a static fact and already recorded once per run in
    /// <c>MachineFacts.Adapters</c>, so re-reading it every tick would pay for a number that cannot change.
    /// </para></summary>
    private async Task<VramSample?> ReadVramAsync(CancellationToken stopping)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        try
        {
            var attempt = await ProcessRunner.RunAsync(
                "powershell.exe",
                ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", VramScript],
                Environment.CurrentDirectory,
                TimeSpan.FromSeconds(15),
                stopping);

            return attempt is ProcessAttempt.Completed { Result.Ok: true } done
                   && long.TryParse(done.Result.StandardOutput.Trim(), out var used)
                ? new VramSample(_clock.GetUtcNow(), used, TotalBytes: 0)
                : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "The accelerator reading is unavailable; that stream stops contributing");
            return null;
        }
    }

    private static TimeSpan CurrentProcessorTime() =>
        System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;

    /// <summary>The busiest adapter's dedicated usage, in bytes.
    /// <para>
    /// The MAXIMUM across instances rather than a sum: the counter reports one instance per physical adapter,
    /// and adding an integrated card's usage to a discrete one's would produce a number describing no piece
    /// of hardware. The busiest is the one a measurement is competing for.
    /// </para></summary>
    private const string VramScript =
        "$ErrorActionPreference='SilentlyContinue';"
        + "$c = (Get-Counter '\\GPU Adapter Memory(*)\\Dedicated Usage').CounterSamples;"
        + "if ($c) { [int64](($c | Measure-Object CookedValue -Maximum).Maximum) } else { '' }";
}
