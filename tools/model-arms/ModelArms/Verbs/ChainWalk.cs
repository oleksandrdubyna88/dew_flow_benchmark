using Bench.Cli;
using ModelArms.Adapters;
using ModelArms.Core.Chain;
using ModelArms.Core.Experiment;

namespace ModelArms.Verbs;

/// <summary>The chain's loop (E5.S4, step 6): read the facts (<see cref="ChainGather"/>), ask <see cref="ChainPlan.Next"/>, carry the action out, again — until an action ends the chain or a stage fails. It remembers only what this PROCESS did (the arms it started, when, how long it has waited), never a marker's meaning: a restarted chain decides afresh from the files.
/// <para>
/// <b>Every bounded wait sleeps what REMAINS, never a whole period</b> (§8 item 48): the startup poll every second up to <see cref="ChainWaitCeiling.StartupCeiling"/>, the wait for a done runner to exit up to the kill grace, the round's wait up to its ceiling, each <c>min(period, bound − elapsed)</c>, the elapsed time the seam clock's now minus an instant this process recorded — so a grace of 90 s is checked at 0, 60 and 90 s and expires at exactly 90.
/// This type holds no member that signals a process, writes a mode or unlocks outside <c>Unlock</c>: every other exit leaves the isolation as it stands.
/// </para></summary>
internal sealed class ChainWalk
{
    private static readonly TimeSpan StartupPeriod = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan WaitPeriod = TimeSpan.FromSeconds(60);

    /// <summary>What carrying an action out came to: the chain goes on to read the world again, or it ends with a code.</summary>
    private abstract record Step
    {
        private Step()
        {
        }

        public sealed record Goes : Step;

        public sealed record Ends(int Code) : Step;
    }

    private static readonly Step Go = new Step.Goes();

    private readonly ExperimentInput _input;
    private readonly ChainSeams _seams;
    private readonly RunSpec _run;
    private readonly string _name;
    private readonly HashSet<string> _startedHere = new(StringComparer.Ordinal);
    private DateTimeOffset? _startedAt;
    private DateTimeOffset? _awaitingExitSince;
    private int _round = -1;
    private DateTimeOffset _roundSince;

    public ChainWalk(ExperimentInput input, ChainSeams seams, RunSpec run)
    {
        _input = input;
        _seams = seams;
        _run = run;
        _name = $"model-arms chain {run.Name}";
    }

    public async Task<int> RunAsync()
    {
        while (true)
        {
            _input.Verb.Stopping.ThrowIfCancellationRequested();
            var read = ChainGather.Read(_input, _seams, _run, _startedHere);
            if (read is Passage<ChainFacts>.Stopped stop)
            {
                return stop.Print(_input.Verb.Error);
            }

            var facts = Timed(((Passage<ChainFacts>.Through)read).Value);
            var action = ChainPlan.Next(facts);
            Noted(action, facts);
            if (await CarryAsync(action, facts) is Step.Ends ends)
            {
                return ends.Code;
            }
        }
    }

    private DateTimeOffset Now => _seams.Clock.GetUtcNow();

    private int RoundOf(ChainFacts facts) => _run.Rounds.ToList().FindIndex(round => round.Any(arm => !facts.Arms.First(each => each.Arm == arm).Done));

    /// <summary>The spans, measured now from the instants this process recorded.</summary>
    private ChainFacts Timed(ChainFacts facts) =>
        facts with { StartingFor = StartingFor(facts), AwaitedFor = SpanSince(_awaitingExitSince), WaitedFor = _round >= 0 && RoundOf(facts) == _round ? Now - _roundSince : TimeSpan.Zero };

    /// <summary>Since the last <c>Start</c>, while an arm this process started is neither alive, done nor ran; else zero.</summary>
    private TimeSpan StartingFor(ChainFacts facts) =>
        facts.Arms.Any(arm => _startedHere.Contains(arm.Arm) && !arm.Done && !arm.Alive && !arm.Ran) ? SpanSince(_startedAt) : TimeSpan.Zero;

    private TimeSpan SpanSince(DateTimeOffset? instant) => instant is { } since ? Now - since : TimeSpan.Zero;

    /// <summary>What carrying <paramref name="action"/> out starts counting: an uninterrupted run of <c>AwaitExit</c>, and the round's wait from this process's first action in it.</summary>
    private void Noted(ChainAction action, ChainFacts facts)
    {
        _awaitingExitSince = action is ChainAction.AwaitExit ? _awaitingExitSince ?? Now : null;
        if (IsRoundAction(action))
        {
            CountRound(RoundOf(facts));
        }
    }

    private static bool IsRoundAction(ChainAction action) => action is ChainAction.Start or ChainAction.AwaitStart or ChainAction.AwaitExit or ChainAction.WaitFor;

    private void CountRound(int round)
    {
        if (round >= 0 && round != _round)
        {
            (_round, _roundSince) = (round, Now);
        }
    }

    private Task<Step> CarryAsync(ChainAction action, ChainFacts facts) =>
        action switch
        {
            ChainAction.Make => MakeAsync(facts),
            ChainAction.Lock => LockAsync(),
            ChainAction.Start start => StartAsync(start),
            ChainAction.AwaitStart wait => Awaited(wait.Expired, () => Ended(StartupLines(wait, facts), ExitCodes.NoReport), () => Sleep(StartupPeriod, ChainWaitCeiling.StartupCeiling - facts.StartingFor)),
            ChainAction.AwaitExit wait => Awaited(wait.Expired, () => Ended([$"{_name}: {string.Join(", ", wait.Arms)} wrote arm-done but the runner has not exited within limits.killGraceSeconds ({(int)facts.Grace.TotalSeconds} s) — run me again"], ExitCodes.NoReport), () => Sleep(WaitPeriod, facts.Grace - facts.AwaitedFor)),
            ChainAction.WaitFor wait => Awaited(wait.Expired, () => Ended([StillRunning(wait, facts)], ExitCodes.NoReport), () => Sleep(WaitPeriod, facts.WaitCeiling - facts.WaitedFor)),
            ChainAction.StopArmDied died => Task.FromResult(Ended([$"{_name}: {died.Arm} stopped without finishing — resume it (model-arms run-all {died.Arm}), then start me again"], ExitCodes.NoReport)),
            ChainAction.StopOtherRunAlive other => Task.FromResult(Ended([$"{_name}: {other.Arm} of another run is alive — run me again once it has ended"], ExitCodes.NoReport)),
            ChainAction.RefuseMarker refuse => Task.FromResult(Ended(Refusal(refuse, facts), ExitCodes.Configuration)),
            ChainAction.Unlock => UnlockAsync(),
            ChainAction.Check check => CheckAsync(check),
            _ => Task.FromResult(Finished(facts)),
        };

    private static Task<Step> Awaited(bool expired, Func<Step> end, Func<Task<Step>> sleep) => expired ? Task.FromResult(end()) : sleep();

    private async Task<Step> Sleep(TimeSpan period, TimeSpan remaining)
    {
        await _seams.Tick(remaining < period ? remaining : period, _input.Verb.Stopping);
        return Go;
    }

    /// <summary>An error ending the chain: its lines on stderr, the code.</summary>
    private Step Ended(IEnumerable<string> lines, int code)
    {
        foreach (var line in lines)
        {
            _input.Verb.Error.WriteLine(line);
        }

        return new Step.Ends(code);
    }

    private void Stage(string line) => _input.Verb.Output.WriteLine($"{_name}: {line}");

    private IEnumerable<string> StartupLines(ChainAction.AwaitStart wait, ChainFacts facts) =>
        wait.Arms.Select(arm => $"{_name}: {arm} was started {(int)facts.StartingFor.TotalSeconds} s ago and its runner has not taken pids/{arm}.lock — see pids/{arm}.log; resume it (model-arms run-all {arm}), then start me again");

    private string StillRunning(ChainAction.WaitFor wait, ChainFacts facts) =>
        $"{_name}: {wait.Arm} is still running after {(long)facts.WaitedFor.TotalSeconds} s, the longest its own step budgets and limit waits allow — no runner was stopped; see pids/{wait.Arm}.log, then run me again";

    /// <summary>The marker is over a state it does not match: nothing is started, and what to do depends on the state (parent §4.7).</summary>
    private IEnumerable<string> Refusal(ChainAction.RefuseMarker refuse, ChainFacts facts)
    {
        yield return $"{_name}: chain/{_run.Name}/locked is written but the isolation is not this run's — {refuse.State} — no runner is started";
        yield return facts.Snapshot is Core.Isolation.LockState.None or Core.Isolation.LockState.Applying
            ? $"  recovery: model-arms lock {_run.Name}, then model-arms chain {_run.Name} again"
            : $"  recovery: model-arms unlock once no arm is alive, then model-arms lock {_run.Name}, then model-arms chain {_run.Name} again";
    }

    private async Task<Step> MakeAsync(ChainFacts facts)
    {
        var unmade = facts.Arms.Where(arm => !arm.Made).Select(arm => arm.Arm).ToList();
        Stage($"make {string.Join(", ", unmade)}");
        return Coded(await _seams.Make(_input, unmade));
    }

    private async Task<Step> LockAsync()
    {
        Stage("lock");
        var outcome = await _seams.Lock(_input, _run.Name, _input.Verb.Stopping);
        return outcome switch
        {
            LockOutcome.Applied applied => Marked(RunStage.Locked, applied.Line),
            LockOutcome.AlreadyLocked already => Marked(RunStage.Locked, already.Line),
            LockOutcome.NoIsolation none => Marked(RunStage.Locked, none.Line),
            LockOutcome.Stopped stopped => Ended([$"{_name}: lock: {stopped.Why}"], stopped.ExitCode),
            _ => Ended([$"{_name}: lock came to an outcome this build does not know"], ExitCodes.Environment),
        };
    }

    private async Task<Step> UnlockAsync()
    {
        Stage("unlock");
        var outcome = await _seams.Unlock(_input, _input.Verb.Stopping);
        return outcome switch
        {
            UnlockOutcome.Restored restored => Marked(RunStage.Unlocked, restored.Line),
            UnlockOutcome.NothingLocked nothing => Marked(RunStage.Unlocked, nothing.Line),
            UnlockOutcome.NoIsolation none => Marked(RunStage.Unlocked, none.Line),
            UnlockOutcome.RefusedAlive alive => Ended([$"{_name}: unlock refused — {alive.Arm} is alive; run me again once it has ended"], ExitCodes.NoReport),
            UnlockOutcome.Stopped stopped => Ended([$"{_name}: unlock: {stopped.Why}"], stopped.ExitCode),
            _ => Ended([$"{_name}: unlock came to an outcome this build does not know"], ExitCodes.Environment),
        };
    }

    private async Task<Step> StartAsync(ChainAction.Start start)
    {
        Stage($"start {string.Join(", ", start.Arms)}");
        _startedHere.UnionWith(start.Arms);
        _startedAt = Now;
        return Coded(await _seams.Start(_input, start.Arms));
    }

    private async Task<Step> CheckAsync(ChainAction.Check check)
    {
        Stage($"check {check.Arm}");
        return Coded(await _seams.Check(_input, check.Arm));
    }

    /// <summary>A verb's exit code: 0 goes on, anything else ends the chain with it.</summary>
    private static Step Coded(int code) => code == ExitCodes.Pass ? Go : new Step.Ends(code);

    private Step Finished(ChainFacts facts)
    {
        var written = facts.ArmsDone ? Go : Marked(RunStage.ArmsDone, "every arm checked");
        if (written is Step.Ends)
        {
            return written;
        }

        Stage("done");
        return new Step.Ends(ExitCodes.Pass);
    }

    /// <summary>The stage's line, then its marker, written AFTER the verb returned (the verbs know nothing of markers, D11): an empty file staged and renamed by name. A marker that cannot be written ends the chain with 3; the stage is then done and the next run redoes it.</summary>
    private Step Marked(RunStage stage, string line)
    {
        Stage(line);
        try
        {
            ArmFiles.ReplaceFile(_input.Root.RunMarker(_run.Name, stage), string.Empty);
            return Go;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Ended([$"{_name}: {_input.Root.RunMarker(_run.Name, stage)} cannot be written: {ex.Message}"], ExitCodes.Environment);
        }
    }
}
