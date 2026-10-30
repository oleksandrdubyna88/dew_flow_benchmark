using System.Globalization;
using Bench.Domain;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;
using ModelArms.Core.Ports;
using ModelArms.Core.Steps;

namespace ModelArms.Adapters;

/// <summary>The places in a gate step's loop a test can stand in (E2 §8 item 44): a probe that throws there is a gate killed at that point. <see cref="StatusRead"/> is the judgement made and not yet acted on, <see cref="WaitRecorded"/> the round
/// written to <c>timing.jsonl</c>, <see cref="ContinueWritten"/> the marker on disk with <c>.done</c> still standing, <see cref="DoneRemoved"/> the marker alone.</summary>
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

/// <summary>The gate step (plan D10, §4.5; <c>run-arm.sh:144-162</c>, rewritten by E2 §8 item 44): take the gate lock, run the step under it through the runner, let the lock go, JUDGE <c>gate.json</c> on EVERY entry (a skipped step's
/// included), and while it says <c>blocked</c> and the tries are not spent decide a round — write it down, write the marker that asks for the session to go on, remove the <c>.done</c> that contradicts it, wait, loop. The wait count is the number
/// of the step's <c>gate-wait</c> lines in <c>timing.jsonl</c>, so a restart reads back what every earlier entry decided; a gate that finished still blocked is not revived unless <c>limits.gateTries</c> is raised in the experiment file. When
/// the loop ends, the step's <c>.diff</c> is written once from its <c>.before</c>. The gate reads no <c>.done</c> and keeps no count of its own, and it never calls <see cref="StepRunner.Recover"/>: it asks the runner to RUN, and the runner's
/// recovery is the one rule.
/// <para>
/// <b>The wait for the lock is bounded</b> (E2 §8 item 57, common.reliability): a holder that is stopped or hung keeps the lock until something kills it, so the gate waits under a token that carries <see cref="DefaultLockCeiling"/> (or the one
/// it was given) and, before it waits, says who holds the lock; when the ceiling runs out it stops resumable (exit 5) with the holder's description in the <c>stopped</c> line. It never breaks the lock. An exit 5 from the runner is returned at
/// once — a relaunch costs one paid attempt, so the NEXT entry decides, never this loop.
/// </para>
/// <para>
/// What this does NOT do: judge a <c>gate.json</c> bigger than memory (it is read whole, as every file of the arm is); decide what the gate MODEL meant by <c>proceed</c> beyond the word.
/// </para></summary>
public sealed class GateStep
{
    /// <summary>Six hours: a gate step runs up to its own <c>minutes</c> under the lock (150 in the worked example) and a few arms queue behind it, so a ceiling shorter than two of them would stop healthy queues; one longer than a working day
    /// is a wedged holder the operator should be told about. The wait is NOT the experiment file's to set yet — the arm's runner (E2.S8) passes the value it wants.</summary>
    public static readonly TimeSpan DefaultLockCeiling = TimeSpan.FromHours(6);

    /// <summary>The longest ceiling a timer can carry: <c>CancelAfter</c> takes at most <c>uint.MaxValue - 1</c> milliseconds (about 49.7 days) and throws past it — and only when the timer fires under contention, so a larger value is refused HERE, where it is passed.</summary>
    public static readonly TimeSpan MaxLockCeiling = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    private readonly GateDeps _deps;

    /// <param name="runner">Runs the step under the lock.</param>
    /// <param name="gateLock">The experiment's one gate lock (<c>FileGateLock</c> in production).</param>
    /// <param name="clock">Stamps the <c>gate-wait</c> and <c>stopped</c> lines and the log.</param>
    /// <param name="wait">The wait of a blocked round, with the operator's stop as its token.</param>
    /// <param name="log">The verb's output.</param>
    /// <param name="map">The arm map the log is scrubbed through.</param>
    /// <param name="lockCeiling">The longest this gate waits for the lock; positive and at most <see cref="MaxLockCeiling"/>.</param>
    /// <param name="probe">A test's hand on the loop; production passes none.</param>
    public GateStep(StepRunner runner, IGateLock gateLock, TimeProvider clock, Func<TimeSpan, CancellationToken, Task> wait, TextWriter log, ArmMap map, TimeSpan lockCeiling, GateProbe? probe = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lockCeiling, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lockCeiling, MaxLockCeiling);
        _deps = new GateDeps(runner, gateLock, clock, wait, log, map, lockCeiling, probe ?? GateProbe.None);
    }

    public Task<StepOutcome> RunAsync(StepContext context, CancellationToken stopping) => new GateRun(context, _deps).RunAsync(stopping);
}

/// <summary>What the gate step is given, once.</summary>
internal sealed record GateDeps(StepRunner Runner, IGateLock Lock, TimeProvider Clock, Func<TimeSpan, CancellationToken, Task> Wait, TextWriter Log, ArmMap Map, TimeSpan LockCeiling, GateProbe Probe);

/// <summary>One run of the gate step (built per call, as <c>StepRun</c> is): the step's files, its log and its git, and the loop. The operator's stop is absorbed wherever it lands — the lock wait, the blocked round's wait — into a <c>stopped</c>
/// line and exit 5; the runner absorbs its own.</summary>
internal sealed class GateRun
{
    private const string OperatorStop = "stopped by the operator — run the same command again to resume";

    /// <summary>The longest text of a model's <c>why</c> or status put in a log line: the reasons are the model's, and a log line is one line.</summary>
    private const int ShownCharacters = 300;

    /// <summary>The longest <c>why</c> that is WRITTEN DOWN — in the <c>gate-wait</c> line and the marker, which every later entry reads again: a model's reason is a stranger's text, and a few kilobytes say what it means.</summary>
    private const int KeptCharacters = 2048;

    /// <summary>How much of an unjudgeable status the stop's sentence quotes.</summary>
    private const int BeginsCharacters = 200;

    private readonly StepContext _context;
    private readonly GateDeps _deps;
    private readonly ArmFiles _files;
    private readonly StepLog _log;
    private readonly string _step;
    private readonly string _statusFile;

    public GateRun(StepContext context, GateDeps deps)
    {
        _context = context;
        _deps = deps;
        _step = context.Step.Name;
        _files = new ArmFiles(context.Paths);
        _log = new StepLog(deps.Log, deps.Map, deps.Clock, context.Arm, _files);
        _statusFile = context.Paths.NotesFile(
            context.Spec.Outputs.FirstOrDefault(binding => binding.Field == context.Spec.Status)
            ?? throw new InvalidOperationException($"step {_step} binds no status output — a gate step names the output it reads status from"));
    }

    private Limits Limits => _context.Experiment.Limits;

    public async Task<StepOutcome> RunAsync(CancellationToken stopping)
    {
        try
        {
            return await LoopAsync(stopping);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            return IsFinal() ? new StepOutcome.Stopped(5, OperatorStop) : _log.Stop(_step, 5, OperatorStop);
        }
    }

    /// <summary>The gate has nothing left to do: its step is done (recovery would skip it) and what <c>gate.json</c> says does not ask for another round. A stop that lands while a rerun waits for the lock of such a gate still ends the run with exit 5, but writes
    /// no <c>stopped</c> line — nothing was interrupted, and a line after the arm's <c>arm-done</c> would say it was.</summary>
    private bool IsFinal()
    {
        var status = GateStatusFile.Read(_statusFile);
        if (status is GateStatusFile.Answer.Refused || _deps.Runner.Recover(_context) is not StepEntry.Skip)
        {
            return false;
        }

        var judged = Read(status);
        return !judged.Reading.IsUnjudgeable && !GateStatus.WaitAgain(judged.Reading, judged.Waits, Limits.GateTries);
    }

    /// <summary>Lock, step, unlock, judge; a blocked round with tries left is decided, waited and the loop goes round; anything else ends it. A <c>Stopped</c> step ends it at once — never run again inside this loop.</summary>
    private async Task<StepOutcome> LoopAsync(CancellationToken stopping)
    {
        while (true)
        {
            var ran = await RunLockedAsync(stopping);
            if ((ran is StepOutcome.Done done ? await AfterStepAsync(done, stopping) : ran) is { } ended)
            {
                return ended;
            }
        }
    }

    /// <summary>The step is done: read its status, and either end (the status is judged final, or cannot be read) or decide a round and wait it out (null: the loop goes round). A status that is there and cannot be read — a link, a FIFO, a file past the
    /// limit — is NOT read as <c>missing</c>: the shell's "missing" means the model wrote nothing and the gate finishes, and finishing on a file nobody could judge would let a blocked gate pass. It stops resumable (exit 5) naming the file; the next entry
    /// judges again, so the operator fixes the file and runs the arm again.</summary>
    private async Task<StepOutcome?> AfterStepAsync(StepOutcome.Done done, CancellationToken stopping)
    {
        var status = GateStatusFile.Read(_statusFile);
        if (status is GateStatusFile.Answer.Refused refused)
        {
            return _log.Stop(_step, 5, CannotJudge(refused.Reason, null));
        }

        var judged = Judge(status);
        if (judged.Reading.IsUnjudgeable)
        {
            return _log.Stop(_step, 5, CannotJudge(judged.Reading.Unjudgeable, Begins(status)));
        }

        if (!GateStatus.WaitAgain(judged.Reading, judged.Waits, Limits.GateTries))
        {
            return await FinalizedAsync(done, judged, stopping);
        }

        await RoundAsync(judged, stopping);
        return null;
    }

    /// <summary>What <c>gate.json</c> says and how many blocked rounds this step has already spent, both read from the files — on EVERY entry, whatever the runner did.</summary>
    private Judgement Judge(GateStatusFile.Answer status)
    {
        var judged = Read(status);
        _deps.Probe.Reached(GatePoint.StatusRead);
        return judged;
    }

    private Judgement Read(GateStatusFile.Answer status)
    {
        var reading = GateStatus.Read(status is GateStatusFile.Answer.Text text ? Outcome<string>.Success(text.Value) : Outcome<string>.Failure("gate.json does not exist"));
        return new Judgement(reading with { Why = Clamped(reading.Why) }, GateStatus.WaitsSoFar(_files.ReadTiming(), _step));
    }

    /// <summary>A blocked round, in the one order that cannot lose it: the round is recorded (a try is spent when it is DECIDED), then the marker that says the session is to go on, then the <c>.done</c> that contradicts the marker is removed; the
    /// wait comes last. A death at any point leaves a state the next entry judges again (E2 §8 item 44).</summary>
    private async Task RoundAsync(Judgement judged, CancellationToken stopping)
    {
        var attempt = _files.LastAttempt(_step);
        var paths = _context.Paths;
        _files.AppendTiming(new TimingLine.GateWait(_context.Arm, _step, attempt, judged.Waits + 1, judged.Reading.Why, _deps.Clock.GetUtcNow()));
        _deps.Probe.Reached(GatePoint.WaitRecorded);
        _files.Replace(paths.Continue(_step), ContinueMarkers.Write(new ContinueMarker(attempt, "gate blocked: " + judged.Reading.Why)));
        _deps.Probe.Reached(GatePoint.ContinueWritten);
        _files.Delete(paths.Done(_step));
        _deps.Probe.Reached(GatePoint.DoneRemoved);
        _log.Line($"{_step}: the gate was blocked ({Shown(judged.Reading.Why)}) — waiting {Limits.GateWaitSeconds.ToString(CultureInfo.InvariantCulture)}s, then continuing the session (wait {(judged.Waits + 1).ToString(CultureInfo.InvariantCulture)} of {Limits.GateTries.ToString(CultureInfo.InvariantCulture)})");
        await _deps.Wait(TimeSpan.FromSeconds(Limits.GateWaitSeconds), stopping);
    }

    /// <summary>The step under the lock; the lock is released when the step returns, whatever it returned — before any wait.</summary>
    private async Task<StepOutcome> RunLockedAsync(CancellationToken stopping)
    {
        _log.Line($"{_step}: waiting for the gate lock");
        return await TakeAsync(stopping) switch
        {
            LockAnswer.Held held => await RunHeldAsync(held.Handle, stopping),
            LockAnswer.Refused refused => _log.Stop(_step, 4, refused.Reason),
            LockAnswer.CeilingPassed passed => _log.Stop(_step, 5, passed.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    private async Task<StepOutcome> RunHeldAsync(IDisposable handle, CancellationToken stopping)
    {
        using (handle)
        {
            _log.Line($"{_step}: gate lock taken");
            return await _deps.Runner.RunAsync(_context with { DeriveOutputs = false }, stopping);
        }
    }

    /// <summary>The lock if it is free, else the wait for it: said first, bounded by the ceiling. A path the lock may not use is a refusal (D17), never a wait.</summary>
    private async Task<LockAnswer> TakeAsync(CancellationToken stopping)
    {
        var path = new RootPaths(_context.Paths.Root).GateLock;
        try
        {
            return _deps.Lock.TryAcquire(path) is { } free ? new LockAnswer.Held(free) : await QueuedAsync(path, stopping);
        }
        catch (GateLockRefusedException refused)
        {
            return new LockAnswer.Refused(refused.Message);
        }
    }

    /// <summary>The wait is OURS: the ceiling is told apart from the operator's stop by that token's state — only the stop travels up as a cancellation — and the ceiling's end is the gate's own words, with the holder in them.</summary>
    private async Task<LockAnswer> QueuedAsync(string path, CancellationToken stopping)
    {
        var ceiling = Said(_deps.LockCeiling);
        _log.Line($"{_step}: the gate lock is held {_deps.Lock.Describe(path)} — waiting at most {ceiling} for it (a holder that keeps it longer is stuck, not queued; `model-arms gate-lock status` names it)");
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        limit.CancelAfter(_deps.LockCeiling);
        try
        {
            return new LockAnswer.Held(await _deps.Lock.AcquireAsync(path, limit.Token));
        }
        catch (OperationCanceledException) when (!stopping.IsCancellationRequested)
        {
            return new LockAnswer.CeilingPassed($"{_step} could not take the gate lock in {ceiling}: it is held {_deps.Lock.Describe(path)} — the lock is not broken; run me again to resume it");
        }
    }

    /// <summary>The loop is over: the step's <c>.diff</c> is written once, from the <c>.before</c>, now that the step is finished; then the status is said and the step is done.</summary>
    private async Task<StepOutcome> FinalizedAsync(StepOutcome.Done done, Judgement judged, CancellationToken stopping)
    {
        if (await DiffAsync(stopping) is { } stopped)
        {
            return stopped;
        }

        _log.Line(judged.Reading.IsBlocked
            ? $"{_step}: gate status blocked — after {judged.Waits.ToString(CultureInfo.InvariantCulture)} waits, finished as the shell did"
            : $"{_step}: gate status {Shown(judged.Reading.Status)}");
        return done;
    }

    /// <summary>D21 for the step's outputs, done here and not in the loop: a missing input (the <c>.before</c>) is the resumable stop; a diff that is missing is written.</summary>
    private async Task<StepOutcome.Stopped?> DiffAsync(CancellationToken stopping) =>
        StepOutputs.Reconcile(_context.Spec, DerivedOutputs.FactsOf(_context, _files)) switch
        {
            OutputAnswer.Resumable resumable => _log.Stop(_step, 5, resumable.Reason),
            OutputAnswer.Regenerate regenerate => await WrittenAsync(regenerate, stopping),
            _ => null,
        };

    private async Task<StepOutcome.Stopped?> WrittenAsync(OutputAnswer.Regenerate regenerate, CancellationToken stopping)
    {
        var git = new ArmGit(_context.GitExecutable, _context.WorkingDirectory, GitEnvironment.Of(_context.ParentEnvironment));
        return await DerivedOutputs.WriteAsync(_context, regenerate.Outputs, _files, git, stopping) is Outcome<IReadOnlyList<DerivedOutput>>.Fail failed ? _log.Stop(_step, 5, failed.Reason) : null;
    }

    /// <summary>The stop's sentence for a status that is there and cannot be judged. <paramref name="begins"/> is the start of the text, so the operator sees what the model wrote without opening the file; <c>null</c> when no text was read (a refused file).
    /// Removing the file finishes the gate as <c>missing</c> — the shell's answer — and the sentence says so.</summary>
    private string CannotJudge(string reason, string? begins) =>
        $"{_step}: the gate's status cannot be judged — {Shown(reason)}; {(begins is null ? string.Empty : $"it begins: {begins}; ")}fix it and run me again (removing it finishes the gate as missing)";

    private static string Begins(GateStatusFile.Answer status) =>
        status is GateStatusFile.Answer.Text { Value.Length: > 0 } text ? ExecutableRule.Printable(text.Value.Length <= BeginsCharacters ? text.Value : Cut(text.Value, BeginsCharacters) + "…") : "(nothing)";

    /// <summary>At most <see cref="KeptCharacters"/> characters, never cut inside a surrogate pair.</summary>
    private static string Clamped(string text) => Cut(text, KeptCharacters);

    /// <summary>A model's text as one short line of the log.</summary>
    private static string Shown(string text) => ExecutableRule.Printable(text.Length <= ShownCharacters ? text : Cut(text, ShownCharacters) + "…");

    private static string Cut(string text, int limit) => text.Length <= limit ? text : text[..(char.IsHighSurrogate(text[limit - 1]) ? limit - 1 : limit)];

    private static string Said(TimeSpan span) =>
        span >= TimeSpan.FromMinutes(1) ? $"{span.TotalMinutes.ToString("0.##", CultureInfo.InvariantCulture)} min"
        : span >= TimeSpan.FromSeconds(1) ? $"{span.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture)} s"
        : $"{span.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture)} ms";

    private sealed record Judgement(GateReading Reading, int Waits);

    /// <summary>How the wait for the lock ended: held, refused (the path is not one the lock may use), or the ceiling ran out.</summary>
    private abstract record LockAnswer
    {
        private LockAnswer()
        {
        }

        public sealed record Held(IDisposable Handle) : LockAnswer;

        public sealed record Refused(string Reason) : LockAnswer;

        public sealed record CeilingPassed(string Reason) : LockAnswer;
    }
}
