using ModelArms.Core.Ports;

namespace ModelArms.Verbs;

/// <summary>What <c>run-arm</c> is handed that production never changes: the clock, the wait, and the gate lock (none = the root's own file lock). STUB.</summary>
internal sealed record RunArmSeams(TimeProvider Clock, Func<TimeSpan, CancellationToken, Task> Wait, IGateLock? Lock)
{
    public static RunArmSeams Real { get; } = new(TimeProvider.System, (span, ct) => Task.Delay(span, ct), null);
}

/// <summary>STUB — E2.S8's red phase.</summary>
public static class RunArmVerb
{
    public static Task<int> RunAsync(ExperimentInput input) => RunAsync(input, RunArmSeams.Real);

    internal static Task<int> RunAsync(ExperimentInput input, RunArmSeams seams)
    {
        input.Verb.Error.WriteLine("model-arms run-arm: stub");
        return Task.FromResult(4);
    }
}
