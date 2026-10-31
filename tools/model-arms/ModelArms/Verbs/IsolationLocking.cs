using Bench.Cli;
using Bench.Domain;
using System.Text;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Ports;

namespace ModelArms.Verbs;

/// <summary>What <c>lock</c> and <c>unlock</c> share before they decide anything: the EXCLUSIVE, non-blocking take of <c>.isolation.lock</c> (question 8), the arm map read by name, which arms are alive, and the bound of what <c>unlock</c> may restore. The runners hold the same lock SHARED for their life
/// (<see cref="FileGateLock.TakeShared"/>), so a busy exclusive take with a runner alive is exit 4 at once, and a restore under the exclusive lock happens beside no runner.</summary>
internal static class IsolationLocking
{
    private const int MapLimit = 1024 * 1024;
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ProbeEvery = TimeSpan.FromSeconds(5);

    /// <summary>The exclusive lock, held — released by disposing it — or the stop. ONE try per 250 ms, up to <see cref="IsolationSeams.ExclusiveWait"/>: <c>Busy</c> with an arm alive is 4 at once (a runner holds it shared for its life; it will not free); with none alive another <c>lock</c> or <c>unlock</c> has it, and that one is waited
    /// for (its note names it) — the arms probed again every 5 s, so a runner that started meanwhile is 4. The operator's stop is checked before every try (exit 5, said as such), the ceiling is 3. This loop is the verb's own: <c>AcquireAnswerAsync</c> never answers <c>Busy</c> and its ceiling would arrive as the operator's stop.</summary>
    public static async Task<Passage<IDisposable>> TakeExclusiveAsync(ExperimentInput input, IsolationSeams seams, CancellationToken stopping)
    {
        var created = FileGateLock.Create(seams.Clock, Poll, seams.Road);
        if (created is Outcome<FileGateLock>.Fail failed)
        {
            return Passage<IDisposable>.Stop(ExitCodes.Environment, failed.Reason);
        }

        var gate = ((Outcome<FileGateLock>.Ok)created).Value;
        var path = input.Root.IsolationLock;
        var started = seams.Clock.GetTimestamp();
        var probed = TimeSpan.MinValue;
        while (true)
        {
            if (stopping.IsCancellationRequested)
            {
                return Passage<IDisposable>.Stop(ExitCodes.NoReport, $"stopped while waiting for {path} — run the same command again");
            }

            switch (gate.Take(path, create: true, note: true))
            {
                case GateLockAnswer.Held held:
                    return new Passage<IDisposable>.Through(held.Handle);
                case GateLockAnswer.Refused refused:
                    return Passage<IDisposable>.Stop(ExitCodes.Configuration, refused.Reason);
                case GateLockAnswer.Unread unread:
                    return Passage<IDisposable>.Stop(ExitCodes.Environment, unread.Reason);
            }

            var waited = seams.Clock.GetElapsedTime(started);
            if (probed == TimeSpan.MinValue || waited - probed >= ProbeEvery)
            {
                probed = waited;
                if (Alive(input, seams) is { } alive)
                {
                    return Passage<IDisposable>.Stop(alive.Code, alive.Why);
                }
            }

            if (waited >= seams.ExclusiveWait)
            {
                return Passage<IDisposable>.Stop(ExitCodes.Environment, $".isolation.lock is held {gate.Describe(path)} — another lock or unlock is running; it was not free within {seams.ExclusiveWait.TotalSeconds:0.##} s");
            }

            try
            {
                await Task.Delay(Poll, seams.Clock, stopping);
            }
            catch (OperationCanceledException)
            {
                // the stop is answered at the top of the loop, as a value
            }
        }
    }

    /// <summary>A refusal of the busy lock: an arm of the map is alive (4), or a probe the root refused (4) or the host could not make (3); <c>null</c> when no arm is alive.</summary>
    private static (int Code, string Why)? Alive(ExperimentInput input, IsolationSeams seams)
    {
        var map = MapOf(input, seams.Road);
        if (map is Passage<ArmMap>.Stopped stopped)
        {
            return (stopped.ExitCode, string.Join(' ', stopped.Lines));
        }

        var alive = new List<string>();
        foreach (var arm in ((Passage<ArmMap>.Through)map).Value.Entries.Select(entry => entry.Key))
        {
            switch (seams.Liveness(input.Root, arm, seams.Road))
            {
                case RunnerState.Alive running:
                    alive.Add($"{arm} is alive: {running.Holder}");
                    break;
                case RunnerState.Refused refused:
                    return (ExitCodes.Configuration, refused.Reason);
                case RunnerState.Unread unread:
                    return (ExitCodes.Environment, unread.Reason);
            }
        }

        return alive.Count == 0 ? null : (ExitCodes.Configuration, $".isolation.lock is held shared by a running arm ({string.Join(", ", alive)}) — no mode changes while an arm runs; wait for it, or stop it");
    }

    /// <summary><c>arms.map.json</c> by name through the bounded road: absent is no arms, a refusal is 4.</summary>
    public static Passage<ArmMap> MapOf(ExperimentInput input, PlatformRoad road) =>
        BoundedFile.Read(input.Root.ArmMapFile, MapLimit, road) switch
        {
            BoundedRead.Bytes bytes => ArmMap.Read(Encoding.UTF8.GetString(bytes.Value)).Match(
                map => (Passage<ArmMap>)new Passage<ArmMap>.Through(map),
                reason => Passage<ArmMap>.Stop(ExitCodes.Configuration, $"arms.map.json is refused: {reason}")),
            BoundedRead.Refused refused => Passage<ArmMap>.Stop(ExitCodes.Configuration, $"arms.map.json is refused: {refused.Reason}"),
            _ => new Passage<ArmMap>.Through(ArmMap.Empty),
        };

    /// <summary>The paths <c>unlock</c> may set a mode on (finding 11), from today's <c>experiment.json</c> and the map: the root, the home folder, every configured closed and unlisted path, every run's closed paths and every arm folder of the map.</summary>
    public static RestoreBound BoundOf(ExperimentInput input, ArmMap map) =>
        new(
            input.Root.Root,
            ExperimentGate.Home(input.Verb.Environment),
            [.. input.Experiment.Isolation.Closed, .. input.Experiment.Isolation.Unlisted, .. input.Experiment.Runs.SelectMany(run => run.Closed), .. map.Entries.Select(entry => input.Root.Arm(entry.Value).Folder)]);
}
