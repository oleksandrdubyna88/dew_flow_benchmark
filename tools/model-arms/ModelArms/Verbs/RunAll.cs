using Bench.Cli;
using Bench.Domain;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;
using ModelArms.Core.Ports;
using ModelArms.Core.Steps;

namespace ModelArms.Verbs;

/// <summary>What <c>run-all</c> is handed that production never changes: how to make the launcher from the resolved <c>setsid</c> (the Windows road ignores it), how to ask whether an arm runs (the runner's own lock — <see cref="RunnerLiveness.Of(RootPaths, string)"/>), and the road this host starts a detached program by. Internal: only the tests can hand in a recording launcher,
/// a liveness map or another host's road.</summary>
internal sealed record RunAllSeams(Func<string, IDetachedLauncher> Launcher, Func<RootPaths, string, RunnerState> Liveness, DetachedRoad Road, TimeSpan Settle)
{
    public static RunAllSeams Real { get; } = new(
        setsid => DetachedRoads.Current == DetachedRoad.Windows ? new WindowsLauncher() : new SetsidLauncher(setsid),
        RunnerLiveness.Of,
        DetachedRoads.Current,
        TimeSpan.FromSeconds(RunAllVerb.SettleSeconds));
}

/// <summary><c>run-all &lt;arm&gt;…</c> (D13, E5.S3): starts each arm it is given that is not alive, DETACHED, and returns — it never waits for a runner, never starts one twice and never starts an arm again because it ended (E5 §8 item 13: the operator's own <c>run-all</c> IS the ask, and resumes it). Nothing is started until every refusal has been made:
/// the arms (named twice, scheme, <c>chain/made-&lt;arm&gt;</c> by the gate, the token), the road (an unmeasured host is exit 3 with the manual road), the PATH and configuration folders the runners inherit (D17 b, D26 c), <c>setsid</c> and the module resolved to REAL paths and printed with the isolation line, <c>pids/</c> made by name, every arm of the map asked whether it runs, and the gate lock's queue.
/// <para>
/// <b>The queue (parent §4.7).</b> Every arm that would be alive at once — those already alive plus those to start — waits for the one gate lock; an arm sized for a queue of N (<see cref="GateLockCeiling.QueueOf"/>: its own run's largest round) has a ceiling for N−1 others. More alive than the SMALLEST queue among them is exit 4 naming that arm, because its wait runs out first and a relaunch costs a paid attempt. The count includes arms already alive, and an arm
/// that wrote <c>arm-done</c> whose runner still holds its lock (it may be exiting; the chain waits for such runners before it hands out a round, E5.S4).
/// </para>
/// <para>
/// <b>Alive is the runner's lock</b> (<see cref="RunnerLiveness"/>), asked BEFORE any start: an alive arm is left alone — no second runner, no line in its log. The runner is started as <c>[setsid, &lt;module real path&gt;, run-arm, &lt;arm&gt;, --experiment, &lt;root&gt;]</c> (D13 completed by E5 §8 item 25), in the verb's own environment map, with the experiment root as its working directory, by <see cref="IDetachedLauncher"/>; it names itself — <c>pids/&lt;arm&gt;.pid</c> — and holds <c>pids/&lt;arm&gt;.lock</c>.
/// One arm failing to start is recorded and the next is still tried (one unit never ends the campaign). Exit: 0 every arm started or already running; 4 the most severe refusal or failure of configuration; 3 the host (a start that failed, an unmeasured host, a lock that could not be read).
/// </para>
/// <para>
/// <b>The count is atomic across calls</b> (E5.S3 review): a runner takes its lock only after the runtime starts, <c>pids/</c> is made and its holds are taken, so two <c>run-all</c> calls back to back would each see nobody alive. A call therefore holds <c>pids/.run-all.lock</c> EXCLUSIVE, never waiting (busy → exit 4 naming the holder), from before it counts until each runner it started
/// holds its own lock — polled for at most <see cref="SettleSeconds"/> seconds, after which a note says the runner has not shown and a later call may not count it. <b>Residual:</b> a runner that is slow past that bound, or that dies before its lock, leaves a window the count cannot see; a <c>run-all</c> run by hand beside a <c>chain</c> on another root is not serialised (the lock is per root).
/// </para>
/// <para>
/// What this does NOT do: wedged-holder detection (parent §4.7, §9: told apart only when a gate step's ceiling runs out); keep the host awake or lock the experiment (<c>chain</c>); prove a runner ran — <see cref="DetachedStart.Started"/> is the operating system's acceptance, and the runner's lock is the proof.
/// </para></summary>
internal static class RunAllVerb
{
    private const string Verb = "run-all";
    private const string Name = "model-arms " + Verb;
    private const string ResumeWords = " (it ran before and ended without arm-done: this is the resume you asked for)";

    private sealed record Placed(ExperimentInput Input, IReadOnlyList<string> Arms, ArmMap Map);

    private sealed record Equipped(Placed Placed, string Setsid, string Module);

    /// <summary>The call's exclusive hold of <c>pids/.run-all.lock</c>, disposed when the call ends.</summary>
    private sealed record Held(Equipped Equipped, IDisposable Hold);

    /// <summary>The longest a call waits, after starting an arm, for its runner to take its lock; the lock of the call is held that long (E5.S3 review).</summary>
    internal const int SettleSeconds = 15;

    private static readonly TimeSpan SettlePoll = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan SettleLast = TimeSpan.FromMilliseconds(500);

    /// <summary>The arms of the map that run now, each with the holder words its lock gave.</summary>
    private sealed record Probed(Equipped Equipped, IReadOnlyDictionary<string, string> Alive);

    public static Task<int> RunAsync(ExperimentInput input) => StartAsync(input, input.Verb.Positionals, RunAllSeams.Real);

    internal static Task<int> StartAsync(ExperimentInput input, IReadOnlyList<string> arms, RunAllSeams seams)
    {
        var admitted = Chosen(input, arms)
            .Bind(chosen => Placing(input, chosen))
            .Bind(placed => Roaded(placed, seams))
            .Bind(placed => Equipping(placed, seams))
            .Bind(Locked);
        return Task.FromResult(admitted.Match(held => Counted(held, seams), stop => stop.Print(input.Verb.Error)));
    }

    /// <summary>The count and the starts under the call's lock, which is released when the last started runner shows its own (or the bound passes).</summary>
    private static int Counted(Held held, RunAllSeams seams)
    {
        using (held.Hold)
        {
            var input = held.Equipped.Placed.Input;
            return Probing(held.Equipped, seams).Bind(Queued).Match(probed => Starting(probed, seams), stop => stop.Print(input.Verb.Error));
        }
    }

    /// <summary>Takes <c>pids/.run-all.lock</c> exclusive and never waits: another call holding it is exit 4 naming the holder — that call is counting or starting, and this one would count the same empty queue.</summary>
    private static Passage<Held> Locked(Equipped equipped)
    {
        var path = equipped.Placed.Input.Root.RunAllLock;
        return FileGateLock.Create(TimeProvider.System, SettlePoll).Match<Passage<Held>>(
            gate => gate.Take(path, create: true, note: true) switch
            {
                GateLockAnswer.Held held => new Passage<Held>.Through(new Held(equipped, held.Handle)),
                GateLockAnswer.Busy => Passage<Held>.Stop(ExitCodes.Configuration, $"{Name}: another run-all holds {path} ({gate.Describe(path)}) — it is counting or starting arms; run this again once it has returned"),
                GateLockAnswer.Refused refused => Passage<Held>.Stop(ExitCodes.Configuration, $"{Name}: {refused.Reason}"),
                GateLockAnswer.Unread unread => Passage<Held>.Stop(ExitCodes.Environment, $"{Name}: {unread.Reason}"),
                _ => Passage<Held>.Stop(ExitCodes.Environment, $"{Name}: {path} could not be taken"),
            },
            reason => Passage<Held>.Stop(ExitCodes.Environment, $"{Name}: {reason}"));
    }

    /// <summary>Step 1: an arm named twice, then each arm's scheme (the task pipeline refused by the same constant <c>run-arm</c> uses).</summary>
    private static Passage<IReadOnlyList<string>> Chosen(ExperimentInput input, IReadOnlyList<string> arms)
    {
        if (arms.GroupBy(arm => arm, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1) is { } twice)
        {
            return Passage<IReadOnlyList<string>>.Stop(ExitCodes.Configuration, $"{Name}: {ExecutableRule.Printable(twice.Key)} is named twice");
        }

        foreach (var arm in arms)
        {
            if (ArmChoices.Of(input, Verb, arm, RunArmAdmission.AllowsTasks) is Passage<ArmChoice>.Stopped stopped)
            {
                return new Passage<IReadOnlyList<string>>.Stopped(stopped.ExitCode, stopped.Lines);
            }
        }

        return new Passage<IReadOnlyList<string>>.Through(arms);
    }

    /// <summary>Step 2: <c>arms.map.json</c> through the bounded road (1 MiB, no link followed), a map, and every given arm's token.</summary>
    private static Passage<Placed> Placing(ExperimentInput input, IReadOnlyList<string> arms) =>
        ExperimentGate.TextOf(input.Root.ArmMapFile, PlatformFactsReader.CurrentRoad.Bounded).Match(
            text => ArmMap.Read(text).Match(
                map => Tokened(new Placed(input, arms, map)),
                reason => Passage<Placed>.Stop(ExitCodes.Configuration, $"{Name}: arms.map.json is refused: {reason}")),
            reason => Passage<Placed>.Stop(ExitCodes.Configuration, $"{Name}: {reason}"));

    private static Passage<Placed> Tokened(Placed placed)
    {
        foreach (var arm in placed.Arms)
        {
            if (placed.Map.TokenOf(arm) is Outcome<string>.Fail unknown)
            {
                return Passage<Placed>.Stop(ExitCodes.Configuration, $"{Name}: {unknown.Reason}");
            }
        }

        return new Passage<Placed>.Through(placed);
    }

    /// <summary>Step 3: a host nobody measured the launch on starts nothing and says how to start an arm by hand (<c>common.platform-limits</c>: it fails closed).</summary>
    private static Passage<Placed> Roaded(Placed placed, RunAllSeams seams)
    {
        if (seams.Road != DetachedRoad.Unmeasured)
        {
            return new Passage<Placed>.Through(placed);
        }

        var root = placed.Input.Root;
        return Passage<Placed>.Stop(
            ExitCodes.Environment,
            [
                $"{Name}: starting a detached arm is {DetachedRoads.Measured} — on this host start each one by hand:",
                .. placed.Arms.Select(arm => $"  setsid {placed.Input.Verb.Module.Executable} run-arm {arm} --experiment {root.Root} < /dev/null >> {root.PidLog(arm)} 2>&1 &"),
            ]);
    }

    /// <summary>Step 4: the PATH and configuration folders the runners inherit, <c>setsid</c> (on its road) and the module resolved to their REAL paths, <c>pids/</c> made by name; then what will be launched is printed, with the isolation line, before anything starts.</summary>
    private static Passage<Equipped> Equipping(Placed placed, RunAllSeams seams)
    {
        var input = placed.Input;
        var environment = input.Verb.Environment;
        var roots = ExecutableRule.ForbiddenRoots(input.Experiment.Target.Source, input.Experiment.Root);
        var refusal = LaunchAdmission.FirstRefusal(environment, roots);
        if (refusal.Length > 0)
        {
            return Passage<Equipped>.Stop(ExitCodes.Configuration, $"{Name}: {refusal}");
        }

        var setsid = seams.Road == DetachedRoad.Setsid ? LaunchAdmission.Resolve(Verb, "setsid", environment, roots) : new Passage<string>.Through(string.Empty);
        return setsid
            .Bind(real => LaunchAdmission.Resolve(Verb, input.Verb.Module.Executable, environment, roots).Map(module => new Equipped(placed, real, module)))
            .Bind(equipped => PidsMade(input).Map(_ => equipped).Bind(Printed));
    }

    /// <summary><c>&lt;root&gt;/pids</c> made by name (E5.S1's one helper — a link or a file there is exit 4, a host failure exit 3), its sentence in this verb's words.</summary>
    private static Passage<string> PidsMade(ExperimentInput input) =>
        RootFolders.Pids(input.Root).Match<Passage<string>>(
            made => new Passage<string>.Through(made),
            stop => new Passage<string>.Stopped(stop.ExitCode, [.. stop.Lines.Select(line => $"{Name}: {line}")]));

    private static Passage<Equipped> Printed(Equipped equipped)
    {
        var (input, map) = (equipped.Placed.Input, equipped.Placed.Map);
        var output = input.Verb.Output;
        output.WriteLine("resolved executables:");
        if (equipped.Setsid.Length > 0)
        {
            output.WriteLine(map.Scrub($"  setsid → {equipped.Setsid}"));
        }

        output.WriteLine(map.Scrub($"  model-arms → {equipped.Module}"));
        output.WriteLine(map.Scrub(IsolationLine.Of(IsolationFiles.State(input.Root))));
        output.Flush();
        return new Passage<Equipped>.Through(equipped);
    }

    /// <summary>Step 5: the road's one portable line when it has one, then EVERY arm of the map asked whether it runs — the queue counts arms this call was not asked to start.</summary>
    private static Passage<Probed> Probing(Equipped equipped, RunAllSeams seams)
    {
        var (input, map) = (equipped.Placed.Input, equipped.Placed.Map);
        SayPortable(input);
        var alive = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var arm in map.Entries.Select(entry => entry.Key))
        {
            var asked = Holder(seams.Liveness(input.Root, arm));
            if (asked is Passage<string>.Stopped stopped)
            {
                return new Passage<Probed>.Stopped(stopped.ExitCode, stopped.Lines);
            }

            if (asked is Passage<string>.Through { Value.Length: > 0 } running)
            {
                alive[arm] = running.Value;
            }
        }

        return new Passage<Probed>.Through(new Probed(equipped, alive));
    }

    private static void SayPortable(ExperimentInput input)
    {
        if (RunnerLiveness.PortableLine(PlatformFactsReader.CurrentRoad) is { Length: > 0 } portable)
        {
            input.Verb.Output.WriteLine(portable);
        }
    }

    /// <summary>The words an alive runner's lock gave, empty for an arm that is not running; a lock that is refused (4) or unread (3) ends the call.</summary>
    private static Passage<string> Holder(RunnerState state) =>
        state switch
        {
            RunnerState.Alive running => new Passage<string>.Through(running.Holder),
            RunnerState.Refused refused => Passage<string>.Stop(ExitCodes.Configuration, $"{Name}: {refused.Reason}"),
            RunnerState.Unread unread => Passage<string>.Stop(ExitCodes.Environment, $"{Name}: {unread.Reason}"),
            _ => new Passage<string>.Through(string.Empty),
        };

    /// <summary>Step 6 (parent §4.7): the arms alive once this call has run — those alive now plus those it will start — against the SMALLEST gate queue any of them was sized from. Counting only the arms started, or comparing with the largest queue, would let an arm be started that the others' waits outlast.</summary>
    private static Passage<Probed> Queued(Probed probed)
    {
        var placed = probed.Equipped.Placed;
        var atOnce = placed.Map.Entries.Select(entry => entry.Key).Where(arm => probed.Alive.ContainsKey(arm) || placed.Arms.Contains(arm)).ToList();
        var sized = atOnce.Select(arm => (Arm: arm, Queue: GateLockCeiling.QueueOf(placed.Input.Experiment.Runs, arm, placed.Map.Entries.Count).Arms)).ToList();
        if (sized.Count == 0 || atOnce.Count <= sized.Min(each => each.Queue))
        {
            return new Passage<Probed>.Through(probed);
        }

        var smallest = sized.Min(each => each.Queue);
        var which = sized.First(each => each.Queue == smallest).Arm;
        return Passage<Probed>.Stop(
            ExitCodes.Configuration,
            $"{Name}: {atOnce.Count} arms would be alive at once ({string.Join(", ", atOnce)}), but {which} was sized for a gate queue of {smallest} (its own run's largest round) — its gate lock's wait runs out first; start at most {smallest}, or wait for one to end");
    }

    /// <summary>Steps 7 and 8: each given arm in order — left alone when its lock is held, else started — and the most severe code kept (4 before 3).</summary>
    private static int Starting(Probed probed, RunAllSeams seams)
    {
        var placed = probed.Equipped.Placed;
        var input = placed.Input;
        var launcher = seams.Launcher(probed.Equipped.Setsid);
        if (!launcher.WritesLog)
        {
            input.Verb.Output.WriteLine($"{Name}: the runners' output is not kept in pids/<arm>.log on this road (Windows: not observed — no Windows host here)");
        }

        var started = new Dictionary<string, string>(StringComparer.Ordinal);
        var codes = placed.Arms.Select(arm => probed.Alive.TryGetValue(arm, out var holder) ? Left(input, arm, holder) : Started(probed, launcher, arm, started)).ToList();
        Settle(input, seams, started);
        input.Verb.Output.Flush();
        return codes.Contains(ExitCodes.Configuration) ? ExitCodes.Configuration : codes.Contains(ExitCodes.Environment) ? ExitCodes.Environment : ExitCodes.Pass;
    }

    /// <summary>Holds the call's lock until each runner it started has taken its own (alive, or its lock refused or unread — either way a later call can see it), polled with a backoff from 50 ms doubling to 500 ms for at most <see cref="SettleSeconds"/> seconds; one line says so first. A runner that has started and ALREADY ENDED (its pid file differs from before the start, its lock free) is not waited for: a line points at its log.
    /// A runner that has not shown by the bound is a stderr note naming its log, never a failure: the start was accepted, and a slow host is not a dead runner.</summary>
    private static void Settle(ExperimentInput input, RunAllSeams seams, Dictionary<string, string> started)
    {
        if (started.Count == 0 || seams.Settle <= TimeSpan.Zero)
        {
            return;
        }

        input.Verb.Output.WriteLine($"{Name}: waiting up to {SettleSeconds} s for the runners to take their locks ({string.Join(", ", started.Keys)})");
        input.Verb.Output.Flush();
        Reported(input, Waited(input, seams, started));
    }

    /// <summary>The poll loop: the arms still unseen when every one has shown or the bound has passed.</summary>
    private static List<string> Waited(ExperimentInput input, RunAllSeams seams, Dictionary<string, string> started)
    {
        var begun = TimeProvider.System.GetTimestamp();
        var pending = started.Keys.ToList();
        var pause = SettlePoll;
        while (pending.Count > 0 && TimeProvider.System.GetElapsedTime(begun) < seams.Settle)
        {
            pending.RemoveAll(arm => Shown(input, seams, arm, started[arm]));
            if (pending.Count > 0)
            {
                Thread.Sleep(pause);
                pause = TimeSpan.FromTicks(Math.Min(pause.Ticks * 2, SettleLast.Ticks));
            }
        }

        return pending;
    }

    private static void Reported(ExperimentInput input, List<string> pending)
    {
        foreach (var arm in pending)
        {
            input.Verb.Error.WriteLine($"{Name} {arm}: its runner did not take its lock within {SettleSeconds} s — see pids/{arm}.log; a run-all run now may not count it");
        }
    }

    /// <summary>True when nothing more is to be waited for on <paramref name="arm"/>: its lock is held (or refused, or unread), or its runner has already ended — said, with its log, and only when the lock is STILL free after the pid file was seen to change (a runner that took its lock between the two looks is alive, not ended).</summary>
    private static bool Shown(ExperimentInput input, RunAllSeams seams, string arm, string stampBefore)
    {
        if (seams.Liveness(input.Root, arm) is not RunnerState.Gone)
        {
            return true;
        }

        if (PidStamp(input, arm) == stampBefore)
        {
            return false;
        }

        if (seams.Liveness(input.Root, arm) is not RunnerState.Gone)
        {
            return true;
        }

        input.Verb.Error.WriteLine($"{Name} {arm}: its runner started and has already ended — see pids/{arm}.log");
        input.Verb.Error.Flush();
        return true;
    }

    /// <summary>What the arm's pid file said and when it was written, as one comparable text: the runner writes it, by rename, after taking its lock, so a different stamp with the lock free means a runner of THIS start lived and ended. The pid is part of it because a rewrite inside the file system's timestamp resolution leaves the time unchanged; the started process's own pid is not known (<c>setsid</c> forks), so the file's content stands for it. Absent: the empty stamp.</summary>
    private static string PidStamp(ExperimentInput input, string arm)
    {
        var path = input.Root.PidFile(arm);
        var ticks = File.GetLastWriteTimeUtc(path).Ticks;
        var content = BoundedFile.Read(path, 64, PlatformFactsReader.CurrentRoad) is BoundedRead.Bytes bytes ? System.Text.Encoding.ASCII.GetString(bytes.Value).Trim() : string.Empty;
        return $"{ticks}|{content}";
    }

    private static int Left(ExperimentInput input, string arm, string holder)
    {
        input.Verb.Output.WriteLine($"{arm}: already running ({holder})");
        return ExitCodes.Pass;
    }

    /// <summary>One arm started; whatever it comes to is that arm's line and code, and the next arm is still tried.</summary>
    private static int Started(Probed probed, IDetachedLauncher launcher, string arm, Dictionary<string, string> started)
    {
        var input = probed.Equipped.Placed.Input;
        try
        {
            var arguments = new[] { "run-arm", arm, "--experiment", input.Root.Root };
            var resume = Resumed(probed.Equipped.Placed, arm);
            var stamp = PidStamp(input, arm);
            return launcher.Start(probed.Equipped.Module, arguments, input.Root.PidLog(arm), input.Verb.Environment, input.Root.Root) switch
            {
                DetachedStart.Started accepted => Said(input, arm, accepted, resume, started, stamp),
                DetachedStart.Failed failed => Refused(input, arm, failed.ExitCode, failed.Why),
                _ => throw new InvalidOperationException("unreachable"),
            };
        }
        catch (Exception ex) when (!input.Verb.Stopping.IsCancellationRequested)
        {
            return Refused(input, arm, ExitCodes.Environment, $"unexpected {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static int Said(ExperimentInput input, string arm, DetachedStart.Started accepted, bool resume, Dictionary<string, string> started, string stamp)
    {
        started[arm] = stamp;
        input.Verb.Output.WriteLine($"{arm}: started — its runner writes pids/{arm}.pid{(resume ? ResumeWords : string.Empty)}");
        if (accepted.Note.Length > 0)
        {
            input.Verb.Error.WriteLine($"{Name} {arm}: {accepted.Note}");
        }

        return ExitCodes.Pass;
    }

    private static int Refused(ExperimentInput input, string arm, int code, string why)
    {
        input.Verb.Error.WriteLine($"{arm}: not started — {why}");
        return code;
    }

    /// <summary>Whether the arm ran before and ended without <c>arm-done</c>: its <c>out/timing.jsonl</c> (bounded read, no link) has lines and none of them finishes the scheme. A timing that is absent or refused says nothing.</summary>
    private static bool Resumed(Placed placed, string arm)
    {
        var paths = placed.Input.Root.Arm(((Outcome<string>.Ok)placed.Map.TokenOf(arm)).Value);
        if (BoundedFile.Read(paths.Timing, ExperimentGate.RootFileLimitBytes, PlatformFactsReader.CurrentRoad) is not BoundedRead.Bytes bytes)
        {
            return false;
        }

        var timing = TimingLines.Read(System.Text.Encoding.UTF8.GetString(bytes.Value));
        return timing.Count > 0 && !TimingLines.IsArmDone(timing, arm);
    }
}
