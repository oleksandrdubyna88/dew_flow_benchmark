using ModelArms.Core.Experiment;
using ModelArms.Core.Ports;

namespace ModelArms.Adapters;

/// <summary>The places in a gate step's loop a test can stand in (E2 §8 item 44): a probe that throws there is a gate killed at that point.</summary>
public enum GatePoint
{
    StatusRead,
    WaitRecorded,
    ContinueWritten,
    DoneRemoved,
}

/// <summary>A seam production never uses: <see cref="None"/> does nothing at every point.</summary>
public class GateProbe
{
    public static GateProbe None { get; } = new();

    public virtual void Reached(GatePoint point)
    {
    }
}

/// <summary>STUB (E2.S7, red first): one run of the step, no lock, no judgement.</summary>
public sealed class GateStep(StepRunner runner, IGateLock gateLock, TimeProvider clock, Func<TimeSpan, CancellationToken, Task> wait, TextWriter log, ArmMap map, TimeSpan lockCeiling, GateProbe? probe = null)
{
    public static readonly TimeSpan DefaultLockCeiling = TimeSpan.FromHours(6);

    public Task<StepOutcome> RunAsync(StepContext context, CancellationToken stopping) => runner.RunAsync(context, stopping);
}
