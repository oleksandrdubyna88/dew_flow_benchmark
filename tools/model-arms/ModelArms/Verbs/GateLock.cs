using Bench.Cli;
using ModelArms.Adapters;

namespace ModelArms.Verbs;

/// <summary><c>gate-lock status|hold</c> (D10): the other process of the two-process proof, and the operator's way to see the lock (<c>.gate.lock</c> of the root; D11's <c>.isolation.lock</c> is a different lock).
/// <c>status</c> takes the lock to look and gives it back at once: <c>free</c> or <c>held</c>, exit 0. <c>hold --seconds N</c> waits for the lock — at most <see cref="WaitCeiling"/>, then exit 3 in its own words — prints <c>held &lt;path&gt;</c>
/// and flushes, keeps the lock N seconds, releases it, and prints <c>released</c> (exit 0); a stop while holding releases it and travels up as every verb's stop (5). N must be 1 to <see cref="MaxSeconds"/> (a day): a bigger one
/// is no probe, and <c>Task.Delay</c> itself refuses what is past about 49 days. A runtime that cannot lock refuses with 3 before anything is taken.</summary>
public static class GateLockVerb
{
    private const string Name = "model-arms gate-lock";

    /// <summary>The longest <c>hold</c>: a day. A hold is a test and diagnosis tool; a gate step holds the lock for its own wall.</summary>
    public const int MaxSeconds = 86400;

    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(250);

    /// <summary>How long <c>hold</c> waits for a lock somebody else has: a gate step is minutes, a queue of arms a few of them, and a probe that waits longer is a wedged holder, which <c>status</c> shows.</summary>
    private static readonly TimeSpan WaitCeiling = TimeSpan.FromMinutes(15);

    public static Task<int> RunAsync(ExperimentInput input) => RunAsync(input, WaitCeiling);

    /// <summary>The same with the wait's ceiling handed in, so a test does not wait a quarter of an hour to see the verb give up.</summary>
    internal static Task<int> RunAsync(ExperimentInput input, TimeSpan waitCeiling) =>
        FileGateLock.Create(TimeProvider.System, Poll).Match(
            gateLock => input.Verb.Positionals[0] == "status" ? Status(input, gateLock) : HoldAsync(input, gateLock, waitCeiling),
            reason => Refused(input, reason));

    private static Task<int> Refused(ExperimentInput input, string reason)
    {
        input.Verb.Error.WriteLine($"{Name}: {reason}");
        return Task.FromResult(ExitCodes.Environment);
    }

    private static Task<int> Status(ExperimentInput input, FileGateLock gateLock)
    {
        using var taken = gateLock.TryAcquire(input.Root.GateLock);
        input.Verb.Output.WriteLine(taken is null ? $"{Name}: held" : $"{Name}: free");
        return Task.FromResult(ExitCodes.Pass);
    }

    private static async Task<int> HoldAsync(ExperimentInput input, FileGateLock gateLock, TimeSpan waitCeiling)
    {
        var seconds = input.Verb.Flags.Int("seconds", 0);
        if (seconds is < 1 or > MaxSeconds)
        {
            input.Verb.Error.WriteLine($"{Name}: gate-lock hold takes --seconds N, from 1 to {MaxSeconds}");
            return ExitCodes.Configuration;
        }

        var held = await AcquiredAsync(input, gateLock, waitCeiling);
        if (held is null)
        {
            input.Verb.Error.WriteLine($"{Name}: gate-lock hold: the lock was not free within {waitCeiling.TotalSeconds:0.###} s — `gate-lock status` says who has it");
            return ExitCodes.Environment;
        }

        await KeptAsync(input, held, seconds);
        input.Verb.Output.WriteLine($"{Name}: released");
        return ExitCodes.Pass;
    }

    /// <summary>The lock, or <c>null</c> when the ceiling ran out first. The ceiling is OURS: it is told apart from the operator's stop by that token's state, and only the stop travels up as a cancellation.</summary>
    private static async Task<IDisposable?> AcquiredAsync(ExperimentInput input, FileGateLock gateLock, TimeSpan waitCeiling)
    {
        using var ceiling = CancellationTokenSource.CreateLinkedTokenSource(input.Verb.Stopping);
        ceiling.CancelAfter(waitCeiling);
        try
        {
            return await gateLock.AcquireAsync(input.Root.GateLock, ceiling.Token);
        }
        catch (OperationCanceledException) when (!input.Verb.Stopping.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>Holds for <paramref name="seconds"/>, then lets go BEFORE the caller says so: the lock is free when <c>released</c> is printed. A stop while holding releases through the <c>using</c> as it unwinds.</summary>
    private static async Task KeptAsync(ExperimentInput input, IDisposable held, int seconds)
    {
        using (held)
        {
            input.Verb.Output.WriteLine($"{Name}: held {input.Root.GateLock}");
            input.Verb.Output.Flush();
            await Task.Delay(TimeSpan.FromSeconds(seconds), TimeProvider.System, input.Verb.Stopping);
        }
    }
}
