using ModelArms.Adapters;
using ModelArms.Core.Experiment;

namespace ModelArms.Verbs;

/// <summary>What <c>lock</c> and <c>unlock</c> are handed that production never changes: the clock every stamp is written with, the platform road (the test hands the Mac's to select the runtime adapter on Linux), the longest an exclusive take waits for another writer, the test's hands on the adapter, the list the
/// adapter and the verbs append their doings to (<c>pin</c>, <c>snapshot</c>, <c>apply &lt;path&gt;</c>, <c>verify</c>, <c>rollback &lt;path&gt;</c>, <c>restore &lt;path&gt;</c>, <c>aside &lt;name&gt;</c>), and the liveness question. Internal: a console that could be given another would skip the isolation it exists for.</summary>
internal sealed record IsolationSeams(TimeProvider Clock, PlatformRoad Road, TimeSpan ExclusiveWait, IsolationHands? Hands, List<string>? Events, Func<RootPaths, string, PlatformRoad, RunnerState> Liveness)
{
    private static readonly Lazy<Bench.Domain.Outcome<FileGateLock>> Gate = new(() => FileGateLock.Create(TimeProvider.System, TimeSpan.FromMilliseconds(250)));

    public static IsolationSeams Real { get; } = new(
        TimeProvider.System,
        PlatformFactsReader.CurrentRoad,
        TimeSpan.FromSeconds(60),
        null,
        null,
        (root, arm, road) => Gate.Value.Match(gate => RunnerLiveness.Of(root, arm, road, gate), reason => new RunnerState.Unread(reason)));
}

/// <summary>What <c>lock</c> came to (C22). <c>Line</c> is what the verb prints (without its <c>model-arms lock &lt;run&gt;: </c> prefix).</summary>
internal abstract record LockOutcome
{
    private LockOutcome()
    {
    }

    public sealed record Applied(string Line) : LockOutcome;

    public sealed record AlreadyLocked(string Line) : LockOutcome;

    public sealed record NoIsolation(string Line) : LockOutcome;

    public sealed record Stopped(int ExitCode, string Why) : LockOutcome;
}

/// <summary>What <c>unlock</c> came to (C22).</summary>
internal abstract record UnlockOutcome
{
    private UnlockOutcome()
    {
    }

    public sealed record Restored(int Count, string Line) : UnlockOutcome;

    public sealed record NothingLocked(string Line) : UnlockOutcome;

    public sealed record NoIsolation(string Line) : UnlockOutcome;

    public sealed record RefusedAlive(string Arm, string Why) : UnlockOutcome;

    public sealed record Stopped(int ExitCode, string Why) : UnlockOutcome;
}
