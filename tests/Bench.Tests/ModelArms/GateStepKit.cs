using System.Text.Json.Nodes;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Ports;
using ModelArms.Core.Steps;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>A gate lock that holds nothing and says what was done to it: <c>acquire</c> and <c>release</c> go into the one list of events the test also feeds with every launch and every wait, so the ORDER is what is asserted. Both members
/// grant at once; the real lock's refusals and waits are E2.S6's.</summary>
internal sealed class RecordingGateLock(List<string> events) : IGateLock
{
    public List<string> Paths { get; } = [];

    public IDisposable? TryAcquire(string path)
    {
        Paths.Add(path);
        events.Add("acquire");
        return new Release(events);
    }

    public Task<IDisposable> AcquireAsync(string path, CancellationToken cancellationToken) => Task.FromResult(TryAcquire(path)!);

    public string Describe(string path) => "(holder unknown)";

    private sealed class Release(List<string> events) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                events.Add("release");
            }
        }
    }
}

/// <summary>A lock that refuses its path, as the real one does for a link, a folder or a FIFO (D17).</summary>
internal sealed class RefusingGateLock : IGateLock
{
    public IDisposable? TryAcquire(string path) => throw new GateLockRefusedException($"{path} is a link — refused (D17)");

    public Task<IDisposable> AcquireAsync(string path, CancellationToken cancellationToken) => throw new GateLockRefusedException($"{path} is a link — refused (D17)");

    public string Describe(string path) => "(holder unknown)";
}

/// <summary>A lock somebody else holds for ever, and says who: the description is the DOUBLE's, so a gate that asks the file instead of the port is seen.</summary>
internal sealed class BusyGateLock : IGateLock
{
    public const string Holder = "by the double, pid 4242, since never";

    public IDisposable? TryAcquire(string path) => null;

    public async Task<IDisposable> AcquireAsync(string path, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("unreachable");
    }

    public string Describe(string path) => Holder;
}

/// <summary>The runner's probe that notes every launch as an event.</summary>
internal sealed class LaunchEvents(List<string> events) : StepProbe
{
    public override void Reached(StepPoint point)
    {
        if (point == StepPoint.Launched)
        {
            events.Add("launch");
        }
    }
}

/// <summary>A gate killed at a point: throws <c>IOException("killed")</c> the <paramref name="nth"/> time <paramref name="point"/> is reached, and lets every other pass.</summary>
internal sealed class GateKilledAt(GatePoint point, int nth) : GateProbe
{
    private int _reached;

    public override void Reached(GatePoint reached)
    {
        if (reached == point && ++_reached == nth)
        {
            throw new IOException("killed");
        }
    }
}

/// <summary>One gate over a <see cref="StepBench"/>: its event list, its lock, its runner (launches are events) and the wait the bench records.</summary>
internal sealed class GateRig
{
    public List<string> Events { get; } = [];

    public RecordingGateLock Lock { get; }

    public GateRig() => Lock = new RecordingGateLock(Events);

    public GateStep Gate(StepBench bench, GateProbe? probe = null, IGateLock? gateLock = null, TimeSpan? ceiling = null) =>
        new(bench.Runner(new LaunchEvents(Events)), gateLock ?? Lock, bench.Clock, bench.Waits.Wait, bench.Log, bench.Map, ceiling ?? GateStep.DefaultLockCeiling, probe);

    /// <summary>What the bench's wait does: it notes a <c>wait</c> event and hands <paramref name="each"/> the number of waits recorded so far (this one included). Earlier waits are forgotten, so a test that runs a second gate counts from one.</summary>
    public void WhenWaiting(StepBench bench, Action<int> each)
    {
        bench.Waits.Recorded.Clear();
        bench.Waits.OnWait = (_, _) =>
        {
            Events.Add("wait");
            each(bench.Waits.Recorded.Count);
            return Task.CompletedTask;
        };
    }
}

/// <summary>What the gate step's scenarios read and write, said once: <c>gate.json</c> as the gate model writes it, the timing's gate lines, the diff and the marker.</summary>
internal static class GateStepKit
{
    public const string GateName = "4-gate";

    public static readonly TimeSpan GateWait = TimeSpan.FromSeconds(1800);

    /// <summary><c>notes/gate.json</c>, as the gate model writes it; the fake cannot write it.</summary>
    public static void WriteStatus(this StepBench bench, string status, string why = "")
    {
        var file = bench.NotesFile("notes/gate.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, new JsonObject { ["status"] = status, ["why"] = why }.ToJsonString());
    }

    public static IReadOnlyList<TimingLine.GateWait> GateWaits(this StepBench bench) => [.. bench.Timing().OfType<TimingLine.GateWait>()];

    public static int Attempts(this StepBench bench) => DoneMarkers.Read(File.ReadAllText(bench.Paths.Done(GateName))).Ok().Attempts;

    /// <summary>A script: call <paramref name="first"/> replays one recorded stream, every later call another.</summary>
    public static void ReplaysByCall(this StepBench bench, params (int Call, string Fixture)[] calls) =>
        bench.Script(new JsonObject { ["stdoutByCall"] = new JsonObject(calls.Select(call => KeyValuePair.Create(call.Call.ToString(), (JsonNode?)StepBench.Replay(call.Fixture)))) });

    public static StepContext GateContext(this StepBench bench) => bench.Context("C", 3);

    /// <summary>The gate model's session id of the first call, and every later call resuming it.</summary>
    public static void OneSession(this StepBench bench)
    {
        var calls = bench.Fake.Calls();
        var session = calls[0].After("--session-id");
        foreach (var call in calls.Skip(1))
        {
            call.After("--resume").Should().Be(session, $"call {call.Call} continues the first call's session");
        }
    }

    public static CancellationToken Ct => TestContext.Current.CancellationToken;
}
