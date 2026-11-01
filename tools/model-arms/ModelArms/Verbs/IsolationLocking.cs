using Bench.Cli;
using Bench.Domain;
using System.Text;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;
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

        var waiting = new Waiting(input, seams, ((Outcome<FileGateLock>.Ok)created).Value);
        while (true)
        {
            if (stopping.IsCancellationRequested)
            {
                return Passage<IDisposable>.Stop(ExitCodes.NoReport, $"stopped while waiting for {input.Root.IsolationLock} — run the same command again");
            }

            if (waiting.Next() is { } done)
            {
                return done;
            }

            await Delayed(seams, stopping);
        }
    }

    private static async Task Delayed(IsolationSeams seams, CancellationToken stopping)
    {
        try
        {
            await Task.Delay(Poll, seams.Clock, stopping);
        }
        catch (OperationCanceledException)
        {
            // the stop is answered at the top of the loop, as a value
        }
    }

    /// <summary>One wait for the exclusive lock: each <see cref="Next"/> is one try and, when it did not take the lock, one look at who has it. <c>null</c> means <i>keep waiting</i>.</summary>
    private sealed class Waiting(ExperimentInput input, IsolationSeams seams, FileGateLock gate)
    {
        private readonly long _started = seams.Clock.GetTimestamp();
        private TimeSpan _probed = TimeSpan.MinValue;

        public Passage<IDisposable>? Next() => Attempt() ?? Refusal() ?? Ceiling();

        private Passage<IDisposable>? Attempt() =>
            gate.Take(input.Root.IsolationLock, create: true, note: true) switch
            {
                GateLockAnswer.Held held => new Passage<IDisposable>.Through(held.Handle),
                GateLockAnswer.Refused refused => Passage<IDisposable>.Stop(ExitCodes.Configuration, refused.Reason),
                GateLockAnswer.Unread unread => Passage<IDisposable>.Stop(ExitCodes.Environment, unread.Reason),
                _ => null,
            };

        /// <summary>The first time and then every 5 s: is an arm of the map alive? Alive (or a probe the root refused) ends the wait.</summary>
        private Passage<IDisposable>? Refusal()
        {
            var waited = seams.Clock.GetElapsedTime(_started);
            if (_probed != TimeSpan.MinValue && waited - _probed < ProbeEvery)
            {
                return null;
            }

            _probed = waited;
            return Alive(input, seams) is Passage<bool>.Stopped stopped ? Passage<IDisposable>.Stop(stopped.ExitCode, [.. stopped.Lines]) : null;
        }

        private Passage<IDisposable>? Ceiling() =>
            seams.Clock.GetElapsedTime(_started) >= seams.ExclusiveWait
                ? Passage<IDisposable>.Stop(ExitCodes.Environment, $".isolation.lock is held {gate.Describe(input.Root.IsolationLock)} — another lock or unlock is running; it was not free within {seams.ExclusiveWait.TotalSeconds:0.##} s")
                : null;
    }

    /// <summary>Through(false) when no arm of the map is alive; a stop when one is (4), when the root refused a probe (4) or the host could not make it (3).</summary>
    private static Passage<bool> Alive(ExperimentInput input, IsolationSeams seams)
    {
        var map = MapOf(input, seams.Road);
        if (map is Passage<ArmMap>.Stopped stopped)
        {
            return new Passage<bool>.Stopped(stopped.ExitCode, stopped.Lines);
        }

        var alive = new List<string>();
        foreach (var arm in ((Passage<ArmMap>.Through)map).Value.Entries.Select(entry => entry.Key))
        {
            var state = seams.Liveness(input.Root, arm, seams.Road);
            if (state is RunnerState.Refused or RunnerState.Unread)
            {
                return Passage<bool>.Stop(state is RunnerState.Refused ? ExitCodes.Configuration : ExitCodes.Environment, state is RunnerState.Refused refused ? refused.Reason : ((RunnerState.Unread)state).Reason);
            }

            if (state is RunnerState.Alive running)
            {
                alive.Add($"{arm} is alive: {running.Holder}");
            }
        }

        return alive.Count == 0
            ? new Passage<bool>.Through(false)
            : Passage<bool>.Stop(ExitCodes.Configuration, $".isolation.lock is held shared by a running arm ({string.Join(", ", alive)}) — no mode changes while an arm runs; wait for it, or stop it");
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

    /// <summary>The paths <c>unlock</c> may set a mode on (finding 11): the root, the home folder, every configured closed and unlisted path, every run's closed paths and every arm folder of the map — and what the SNAPSHOT recorded: each entry the lock closed carries the configuration entry that authorised it, and when
    /// the snapshot's own entries spell the digest it records (<see cref="SnapshotFiles.DigestOf(SavedSnapshot)"/>) those paths are in the bound, so a lock stays unlockable after the block that named it was removed. A path added to the file by hand changes the digest it spells and is not.</summary>
    public static RestoreBound BoundOf(ExperimentInput input, ArmMap map, SavedSnapshot? saved = null) =>
        new(
            input.Root.Root,
            ExperimentGate.Home(input.Verb.Environment),
            [.. input.Experiment.Isolation.Closed, .. input.Experiment.Isolation.Unlisted, .. input.Experiment.Runs.SelectMany(run => run.Closed), .. map.Entries.Select(entry => input.Root.Arm(entry.Value).Folder), .. Recorded(saved)]);

    private static IEnumerable<string> Recorded(SavedSnapshot? saved) =>
        saved is not null && string.Equals(SnapshotFiles.DigestOf(saved), saved.Digest, StringComparison.Ordinal)
            ? saved.Modes.Where(mode => mode.Source.Length > 0).Select(mode => mode.Path)
            : [];
}
