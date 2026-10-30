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

    /// <summary>Looks and creates nothing: a root with no lock file is free; a lock somebody has is <c>held</c> and, when the holder's note names a live process, <c>by pid N since &lt;utc&gt;</c>. A path the lock may not use is refused (4), a read the host could not make is exit 3.</summary>
    private static Task<int> Status(ExperimentInput input, FileGateLock gateLock)
    {
        var path = input.Root.GateLock;
        var code = gateLock.Take(path, create: false, note: false) switch
        {
            GateLockAnswer.Held held => Free(input, held),
            GateLockAnswer.Absent => Free(input, null),
            GateLockAnswer.Refused refused => Stopped(input, refused.Reason, ExitCodes.Configuration),
            GateLockAnswer.Unread unread => Stopped(input, unread.Reason, ExitCodes.Environment),
            _ => Said(input, $"{Name}: held {FileGateLock.WhoHolds(path)}"),
        };
        return Task.FromResult(code);
    }

    private static int Free(ExperimentInput input, GateLockAnswer.Held? taken)
    {
        taken?.Handle.Dispose();
        return Said(input, $"{Name}: free");
    }

    private static int Said(ExperimentInput input, string line)
    {
        input.Verb.Output.WriteLine(line);
        return ExitCodes.Pass;
    }

    private static int Stopped(ExperimentInput input, string reason, int code)
    {
        input.Verb.Error.WriteLine($"{Name}: {reason}");
        return code;
    }

    private static async Task<int> HoldAsync(ExperimentInput input, FileGateLock gateLock, TimeSpan waitCeiling)
    {
        var seconds = input.Verb.Flags.Int("seconds", 0);
        if (seconds is < 1 or > MaxSeconds)
        {
            return Stopped(input, $"gate-lock hold takes --seconds N, from 1 to {MaxSeconds}", ExitCodes.Configuration);
        }

        return await Ended(input, await AcquiredAsync(input, gateLock, waitCeiling), waitCeiling, seconds);
    }

    private static Task<int> Ended(ExperimentInput input, GateLockAnswer answer, TimeSpan waitCeiling, int seconds) =>
        answer switch
        {
            GateLockAnswer.Held held => KeptAsync(input, held.Handle, seconds),
            GateLockAnswer.Refused refused => Task.FromResult(Stopped(input, refused.Reason, ExitCodes.Configuration)),
            GateLockAnswer.Unread unread => Task.FromResult(Stopped(input, unread.Reason, ExitCodes.Environment)),
            _ => Task.FromResult(Stopped(input, $"gate-lock hold: the lock was not free within {waitCeiling.TotalSeconds:0.###} s — `gate-lock status` says who has it", ExitCodes.Environment)),
        };

    /// <summary>The lock, the refusal that ended the wait, or <c>Busy</c> when the ceiling ran out first. The ceiling is OURS: it is told apart from the operator's stop by that token's state, and only the stop travels up as a cancellation.</summary>
    private static async Task<GateLockAnswer> AcquiredAsync(ExperimentInput input, FileGateLock gateLock, TimeSpan waitCeiling)
    {
        using var ceiling = CancellationTokenSource.CreateLinkedTokenSource(input.Verb.Stopping);
        ceiling.CancelAfter(waitCeiling);
        try
        {
            return await gateLock.AcquireAnswerAsync(input.Root.GateLock, ceiling.Token);
        }
        catch (OperationCanceledException) when (!input.Verb.Stopping.IsCancellationRequested)
        {
            return new GateLockAnswer.Busy();
        }
    }

    /// <summary>Holds for <paramref name="seconds"/>, then lets go BEFORE it says so: the lock is free when <c>released</c> is printed. A stop while holding releases through the <c>using</c> as it unwinds.</summary>
    private static async Task<int> KeptAsync(ExperimentInput input, IDisposable held, int seconds)
    {
        using (held)
        {
            input.Verb.Output.WriteLine($"{Name}: held {input.Root.GateLock}");
            input.Verb.Output.Flush();
            await Task.Delay(TimeSpan.FromSeconds(seconds), TimeProvider.System, input.Verb.Stopping);
        }

        return Said(input, $"{Name}: released");
    }
}
