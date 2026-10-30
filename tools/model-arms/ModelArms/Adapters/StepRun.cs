using System.Globalization;
using Bench.Domain;
using Bench.Domain.Trace;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;
using ModelArms.Core.Steps;

namespace ModelArms.Adapters;

/// <summary>One run of one step (plan §4.5, D6–D9, D21): recover, then loop over attempts until the step is done or the arm must stop. Built per call by <see cref="StepRunner"/>: it holds the step's files and its ONE <see cref="ArmGit"/>
/// — the real git path <c>run-arm</c> resolved, the clone as its working directory, the parent's environment without any <c>GIT_*</c> (D17 d).</summary>
internal sealed class StepRun
{
    private const string OperatorStop = "stopped by the operator — run the same command again to resume";

    private readonly StepContext _context;
    private readonly StepDeps _deps;
    private readonly ArmFiles _files;
    private readonly ArmGit _git;
    private readonly StepLog _log;
    private readonly StepAttempt _attempt;
    private readonly string _step;
    private readonly StepSpec _spec;
    private readonly ArmPaths _paths;

    public StepRun(StepContext context, StepDeps deps)
    {
        _context = context;
        _deps = deps;
        _paths = context.Paths;
        _step = context.Step.Name;
        _spec = context.Spec;
        _files = new ArmFiles(_paths);
        _git = new ArmGit(context.GitExecutable, context.WorkingDirectory, GitEnvironment.Of(context.ParentEnvironment));
        _log = new StepLog(deps.Log, deps.Map, deps.Clock, context.Arm, _files);
        _attempt = new StepAttempt(context, _files, _git, _log, deps);
    }

    /// <summary>The entry the next run would take, over the step's files; nothing is launched or written.</summary>
    public StepEntry Recover() => Decide(Facts());

    public async Task<StepOutcome> RunAsync(CancellationToken stopping)
    {
        _files.Prepare();
        for (var waits = 0; ; waits++)
        {
            var facts = Facts();
            var entry = Decide(facts);
            if (await PreludeAsync(entry, facts, stopping) is { } ended)
            {
                return ended;
            }

            if (await AfterAsync(await _attempt.RunAsync(entry, stopping), waits, stopping) is { } final)
            {
                return final;
            }
        }
    }

    private StepEntry Decide(RecoveryFacts facts) => StepRecovery.Decide(_step, facts, _spec, _context.Experiment.Limits);

    private RecoveryFacts Facts()
    {
        var session = _files.ReadSession(_step);
        var last = _files.LastAttempt(_step);
        return new RecoveryFacts(_files.Exists(_paths.Done(_step)), session, TranscriptExists(session), last, LastEnd(last), _files.ReadTiming(), ContinueMarker());
    }

    private bool TranscriptExists(Outcome<Guid> session) => session is Outcome<Guid>.Ok ok && _deps.Transcripts.Exists(_context.WorkingDirectory, ok.Value);

    /// <summary>How the last stream ends read ALONE, as recovery sees it (D6); the wall is not visible here.</summary>
    private StepEnd LastEnd(int last) =>
        last == 0
            ? new StepEnd.NotStarted("no attempt has run")
            : StepClassifier.Classify(ClaudeResultEnvelope.LastOf(StreamText(last)), _files.Tail(_paths.Stderr(_step, last), StepClassifier.StderrTailCharacters), AttemptFacts.Ran);

    private string StreamText(int attempt) => _files.ReadText(_paths.Stream(_step, attempt)).Match(text => text, _ => string.Empty);

    /// <summary>Failure is "no marker", or one that cannot be read — and an unreadable one is said.</summary>
    private Outcome<ContinueMarker> ContinueMarker()
    {
        var path = _paths.Continue(_step);
        if (!_files.Exists(path))
        {
            return Outcome<ContinueMarker>.Failure("no continue marker");
        }

        var marker = _files.ReadText(path).Match(ContinueMarkers.Read, Outcome<ContinueMarker>.Failure);
        if (marker is Outcome<ContinueMarker>.Fail unreadable)
        {
            _log.Line($"{_step}.continue could not be read ({unreadable.Reason}) — ignored");
        }

        return marker;
    }

    private async Task<StepOutcome?> PreludeAsync(StepEntry entry, RecoveryFacts facts, CancellationToken stopping) =>
        entry switch
        {
            StepEntry.Skip => await SkippedAsync(stopping),
            StepEntry.RecoveredDone recovered => await RecoveredAsync(recovered, stopping),
            _ => await BeforeLaunchAsync(facts, stopping),
        };

    /// <summary>Before an attempt: say that a marker sent the step on, remove the <c>.done</c> it left, take the <c>.before</c> snapshot once.</summary>
    private async Task<StepOutcome?> BeforeLaunchAsync(RecoveryFacts facts, CancellationToken stopping)
    {
        Continuing(facts);
        if (!StepOutputs.NeedsBefore(_spec) || _files.Exists(_paths.Before(_step)))
        {
            return null;
        }

        var head = await _git.HeadAsync(stopping);
        if (head is Outcome<string>.Ok ok)
        {
            _files.WriteOnce(_paths.Before(_step), ok.Value + "\n");
            return null;
        }

        return _log.Stop(_step, 5, $"{_step}.before cannot be written: git could not read HEAD ({((Outcome<string>.Fail)head).Reason})");
    }

    private static bool IsContinued(RecoveryFacts facts) =>
        facts.LastAttempt > 0 && facts.Continue is Outcome<ContinueMarker>.Ok { Value.After: var after } && after == facts.LastAttempt;

    /// <summary>The gate asked for the finished session to go on: a <c>.done</c> it died before removing is removed NOW, so the step is finished by the runner, not left half-done.</summary>
    private void Continuing(RecoveryFacts facts)
    {
        if (!IsContinued(facts))
        {
            return;
        }

        var marker = ((Outcome<ContinueMarker>.Ok)facts.Continue).Value;
        _log.Line($"{_step}: continuing its finished session as the gate asked after attempt {marker.After.ToString(CultureInfo.InvariantCulture)} ({marker.Why})");
        if (facts.DoneMarker)
        {
            _files.Delete(_paths.Done(_step));
        }
    }

    private async Task<StepOutcome> SkippedAsync(CancellationToken stopping)
    {
        _log.Line($"{_step} already done — skipped");
        return await ReconciledAsync(stopping) ?? (StepOutcome)new StepOutcome.Done(DoneEnd(), Skipped: true, Recovered: false);
    }

    private StepEnd DoneEnd() =>
        _files.ReadText(_paths.Done(_step)).Match(DoneMarkers.Read, Outcome<DoneMarker>.Failure) is Outcome<DoneMarker>.Ok { Value.State: var state } && state == new StepEnd.HardDeadline().Word
            ? new StepEnd.HardDeadline()
            : new StepEnd.Finished();

    /// <summary>A stream that finished before its runner died: its end line (when none), its outputs, its <c>.done</c> — never resumed.</summary>
    private async Task<StepOutcome> RecoveredAsync(StepEntry.RecoveredDone recovered, CancellationToken stopping)
    {
        var exit = await RecoveredEndAsync(recovered);
        if (await ReconciledAsync(stopping) is { } stopped)
        {
            return stopped;
        }

        _files.WriteOnce(_paths.Done(_step), DoneMarkers.Write(new DoneMarker(recovered.Attempt, exit, _paths.Stream(_step, recovered.Attempt), recovered.End.Word, Recovered: true)));
        _files.Delete(_paths.Continue(_step));
        _log.Line($"{_step} had finished before its runner died — marked done, not resumed");
        return new StepOutcome.Done(recovered.End, Skipped: false, Recovered: true);
    }

    private async Task<CapturedCount> RecoveredEndAsync(StepEntry.RecoveredDone recovered)
    {
        var existing = _files.ReadTiming().OfType<TimingLine.End>().FirstOrDefault(end => end.Step == _step && end.Attempt == recovered.Attempt);
        if (existing is not null)
        {
            return existing.Exit;
        }

        var unknown = CapturedCount.Unavailable(TimingLines.NotRecorded);
        var head = (await _git.HeadShortAsync(CancellationToken.None)).Match(text => text, _ => string.Empty);
        var stream = _paths.Stream(_step, recovered.Attempt);
        _files.AppendTiming(new TimingLine.End(_context.Arm, _step, recovered.Attempt, _files.ModifiedUtc(stream), unknown, unknown, recovered.End is StepEnd.Finished, false, head, recovered.End.Word, true));
        return unknown;
    }

    /// <summary>D21, only when the runner derives this step's outputs: what is missing and can be derived is written; a missing input is the resumable stop. Null when nothing stops the step.</summary>
    private async Task<StepOutcome.Stopped?> ReconciledAsync(CancellationToken stopping)
    {
        if (!_context.DeriveOutputs)
        {
            return null;
        }

        var stopped = await AnsweredAsync(StepOutputs.Reconcile(_spec, DerivedOutputs.FactsOf(_context, _files)), stopping);
        if (stopped is null)
        {
            _deps.Probe.Reached(StepPoint.OutputsDerived);
        }

        return stopped;
    }

    private async Task<StepOutcome.Stopped?> AnsweredAsync(OutputAnswer answer, CancellationToken stopping) =>
        answer switch
        {
            OutputAnswer.Resumable resumable => _log.Stop(_step, 5, resumable.Reason),
            OutputAnswer.Regenerate regenerate => await RegeneratedAsync(regenerate, stopping),
            _ => null,
        };

    private async Task<StepOutcome.Stopped?> RegeneratedAsync(OutputAnswer.Regenerate regenerate, CancellationToken stopping) =>
        await DerivedOutputs.WriteAsync(_context, regenerate.Outputs, _files, _git, stopping) is Outcome<IReadOnlyList<DerivedOutput>>.Fail failed ? _log.Stop(_step, 5, failed.Reason) : null;

    private async Task<StepOutcome?> AfterAsync(AttemptResult result, int waits, CancellationToken stopping) =>
        result is AttemptResult.Ended ended ? await EndedAsync(ended, waits, stopping) : _log.Stop(_step, 4, ((AttemptResult.Refused)result).Reason);

    private async Task<StepOutcome?> EndedAsync(AttemptResult.Ended ended, int waits, CancellationToken stopping) =>
        ended.StoppedByOperator ? _log.Stop(_step, 5, OperatorStop)
        : ended.End.IsDone ? await FinishedAsync(ended, stopping)
        : ended.End is StepEnd.Limited limited ? await LimitedAsync(limited, waits, stopping)
        : NotDone(ended.End);

    private StepOutcome NotDone(StepEnd end) =>
        end switch
        {
            StepEnd.Errored errored => _log.Stop(_step, 5, $"{_step} ended in an ERROR result that is not a limit ({errored.Text}) — stopping; run me again to resume it"),
            StepEnd.Interrupted interrupted => _log.Stop(_step, 5, $"{_step} INTERRUPTED ({interrupted.Why}) — run me again to resume it"),
            StepEnd.NotStarted notStarted => _log.Stop(_step, 3, notStarted.Reason),
            _ => throw new InvalidOperationException("a step that is not done ended as Errored, Interrupted, NotStarted or Limited"),
        };

    private async Task<StepOutcome> FinishedAsync(AttemptResult.Ended ended, CancellationToken stopping)
    {
        if (await ReconciledAsync(stopping) is { } stopped)
        {
            return stopped;
        }

        _files.WriteOnce(_paths.Done(_step), DoneMarkers.Write(new DoneMarker(ended.Attempt, ended.Exit, _paths.Stream(_step, ended.Attempt), ended.End.Word, Recovered: false)));
        _files.Delete(_paths.Continue(_step));
        _log.Line($"{_step} done ({ended.End.Word})");
        return new StepOutcome.Done(ended.End, Skipped: false, Recovered: false);
    }

    /// <summary>A limit: wait and loop (null), or stop when the waits are spent (exit 3) or the operator stops during the wait (exit 5). A wait is not counted against the step's budget.</summary>
    private async Task<StepOutcome?> LimitedAsync(StepEnd.Limited limited, int waits, CancellationToken stopping)
    {
        var limits = _context.Experiment.Limits;
        if (waits + 1 > limits.Tries)
        {
            return _log.Stop(_step, 3, $"still limited after {limits.Tries.ToString(CultureInfo.InvariantCulture)} waits — run me again later");
        }

        _log.Line($"{_step} LIMITED ({limited.Reason}) — waiting {limits.WaitSeconds.ToString(CultureInfo.InvariantCulture)}s, then resuming the same session (wait {(waits + 1).ToString(CultureInfo.InvariantCulture)})");
        try
        {
            await _deps.Wait(TimeSpan.FromSeconds(limits.WaitSeconds), stopping);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            return _log.Stop(_step, 5, OperatorStop);
        }

        return null;
    }
}
