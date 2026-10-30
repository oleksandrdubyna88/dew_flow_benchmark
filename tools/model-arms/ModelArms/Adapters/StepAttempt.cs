using System.Globalization;
using Bench.Application;
using Bench.Domain;
using Bench.Domain.Trace;
using ModelArms.Core.Briefs;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;
using ModelArms.Core.Steps;

namespace ModelArms.Adapters;

/// <summary>How one attempt went, as far as the runner's decision needs it: the brief was refused (before any launch — exit 4), or the attempt ended — the classifier's word, its number, the exit it returned and whether the operator stopped it.</summary>
internal abstract record AttemptResult
{
    private AttemptResult()
    {
    }

    public sealed record Refused(string Reason) : AttemptResult;

    public sealed record Ended(StepEnd End, int Attempt, CapturedCount Exit, bool StoppedByOperator) : AttemptResult;
}

/// <summary>One attempt of a step (plan §4.5 steps 3–6): the session, the prompt, the stream, the start line, the launch, the classification read back from the files, the end line. The start line is written AFTER the stream and its
/// stderr exist (E2 §8 item 56): the number of an attempt is reserved by its stream, so a start line and a stream are one-to-one, and a restart between the prompt and the stream never leaves two start lines naming one stream — which
/// <c>metrics.mjs</c> counts twice.</summary>
internal sealed class StepAttempt(StepContext context, ArmFiles files, ArmGit git, StepLog log, StepDeps deps)
{
    private readonly string _step = context.Step.Name;
    private readonly StepSpec _spec = context.Spec;

    private sealed record Plan(int Attempt, int Budget, Guid Session, bool Fresh)
    {
        public string How => Fresh ? "fresh" : "resume";
    }

    private sealed record Ran(AttemptFacts Facts, CapturedCount Exit, CapturedCount Wall);

    public async Task<AttemptResult> RunAsync(StepEntry entry, CancellationToken stopping)
    {
        var plan = PlanOf(entry);
        var now = deps.Clock.GetUtcNow();
        var deadline = StepBudget.Deadline(now, plan.Budget, deps.Clock.LocalTimeZone);
        var prompt = plan.Fresh ? FreshBrief(plan, deadline) : Outcome<string>.Success(ResumeNotice.For(context.Experiment.Target.Base.Value, deadline));
        if (prompt is Outcome<string>.Fail refused)
        {
            return new AttemptResult.Refused(refused.Reason);
        }

        Write(plan, ((Outcome<string>.Ok)prompt).Value);
        var head = await HeadAsync(stopping);
        deps.Probe.Reached(StepPoint.Prepared);
        var ran = await LaunchAsync(plan, ((Outcome<string>.Ok)prompt).Value, (now, deadline, head), stopping);
        return await RecordAsync(plan, ran);
    }

    private static Plan PlanOf(StepEntry entry) =>
        entry switch
        {
            StepEntry.Fresh fresh => new Plan(fresh.Attempt, fresh.BudgetSeconds, Guid.NewGuid(), Fresh: true),
            StepEntry.Resume resume => new Plan(resume.Attempt, resume.BudgetSeconds, resume.Session, Fresh: false),
            _ => throw new InvalidOperationException("an attempt is launched for a Fresh or a Resume entry only"),
        };

    /// <summary>A fresh session: its id on disk BEFORE the launch, then the filled brief — a refusal (an unfilled field, a leak) is exit 4 and nothing is launched.</summary>
    private Outcome<string> FreshBrief(Plan plan, string deadline)
    {
        files.ReplaceSession(_step, plan.Session);
        deps.Probe.Reached(StepPoint.SessionWritten);
        WarnAboutMissingOutputs();
        return context.Templates.Read(_spec.Brief).Match(
            template => BriefFiller.Fill(template, Fields(deadline), context.Guard, BriefTemplates.NameOf(_spec.Brief).Match(name => name, _ => _spec.Brief)),
            Outcome<string>.Failure);
    }

    private IReadOnlyDictionary<string, string> Fields(string deadline)
    {
        var experiment = context.Experiment;
        var facts = new BriefFacts(experiment.Target.Plan, experiment.Target.Base.Value, experiment.Target.Branch, context.ScenarioCount, deadline, experiment.Known, context.Task, context.Problems);
        return BriefFields.For(_spec, SchemePositions.Visible(context.Scheme, context.Position, _spec.Id, experiment.Steps, experiment.Tasks), experiment.Tasks, context.Paths, facts);
    }

    /// <summary>Bound is not produced (E2 §8 item 41): an output of an EARLIER entry whose file does not exist is said once, and the launch goes on with the path in the brief, as the shell's did.</summary>
    private void WarnAboutMissingOutputs()
    {
        foreach (var placed in SchemePositions.Outputs(context.Scheme, context.Experiment.Steps, context.Experiment.Tasks).Where(placed => placed.Position < context.Position && !files.Exists(context.Paths.NotesFile(placed.Binding))))
        {
            log.Line($"{_step}: {placed.Binding.Field} ({placed.Binding.RelativePath}) was not written by step {placed.Producer} — the brief names a file that does not exist");
        }
    }

    private void Write(Plan plan, string prompt)
    {
        if (files.WritePrompt(_step, plan.Attempt, prompt))
        {
            log.Line($"{_step}: attempt {plan.Attempt.ToString(CultureInfo.InvariantCulture)} was prepared by an earlier run and never launched — its prompt is rewritten");
        }
    }

    /// <summary>HEAD short, <c>""</c> and a log line when git cannot say.</summary>
    private async Task<string> HeadAsync(CancellationToken ct) =>
        (await git.HeadShortAsync(ct)).Match(
            head => head,
            reason =>
            {
                log.Line($"{_step}: git could not read HEAD ({reason}) — recorded as empty");
                return string.Empty;
            });

    /// <summary>The stream and stderr files open for exactly the launch: disposed when it ends, whatever happened. The start line follows their creation and precedes the launch.</summary>
    private async Task<Ran> LaunchAsync(Plan plan, string prompt, (DateTimeOffset Now, string Deadline, string Head) at, CancellationToken stopping)
    {
        var paths = context.Paths;
        using var stream = files.Open(paths.Stream(_step, plan.Attempt));
        using var err = files.Open(paths.Stderr(_step, plan.Attempt));
        files.AppendTiming(new TimingLine.Start(context.Arm, _step, plan.Attempt, plan.How, plan.Session.ToString("D"), _spec.Model, at.Now, plan.Budget, at.Deadline, at.Head, paths.Stream(_step, plan.Attempt)));
        log.Line($"{_step} attempt {plan.Attempt.ToString(CultureInfo.InvariantCulture)} ({plan.How}, session {plan.Session:D}, budget {plan.Budget.ToString(CultureInfo.InvariantCulture)}s) → {paths.Stream(_step, plan.Attempt)}");
        var limits = context.Experiment.Limits;
        var ask = StepLaunch.Ask(new StepLaunchRequest(
            context.ClaudeExecutable,
            _spec,
            context.Experiment.Tools,
            paths,
            plan.Fresh ? new StepSession.Fresh(plan.Session) : new StepSession.Resume(plan.Session),
            context.WorkingDirectory,
            prompt,
            StepBudget.Wall(plan.Budget, limits.KillGraceSeconds),
            context.ParentEnvironment)) with
        {
            Output = AgentOutputSink.Lines(stream.Line),
            ErrorOutput = AgentOutputSink.Lines(err.Line),
        };
        deps.Probe.Reached(StepPoint.Launched);
        return await TranscriptAsync(ask, stopping);
    }

    /// <summary>The launch. The operator's stop is the one exception it absorbs; any other exception is an unexpected infrastructure failure and propagates.</summary>
    private async Task<Ran> TranscriptAsync(AgentAsk ask, CancellationToken stopping)
    {
        var started = deps.Clock.GetTimestamp();
        try
        {
            return Of(await deps.Cli.TranscriptAsync(ask, stopping), deps.Clock.GetElapsedTime(started));
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            return new Ran(new AttemptFacts(string.Empty, false, true), CapturedCount.Unavailable("stopped by the operator"), Whole(deps.Clock.GetElapsedTime(started)));
        }
    }

    private static Ran Of(Outcome<AgentTranscript> launched, TimeSpan elapsed) =>
        launched.Match(
            transcript => new Ran(new AttemptFacts(string.Empty, transcript.TimedOut, false), transcript.ExitCode, Whole(elapsed)),
            reason => new Ran(new AttemptFacts(reason, false, false), CapturedCount.Unavailable(reason), CapturedCount.Unavailable("the launch was refused: nothing ran")));

    private static CapturedCount Whole(TimeSpan elapsed) => CapturedCount.Number((long)elapsed.TotalSeconds);

    /// <summary>Classified from the FILES — the same bytes recovery reads (D6) — then the end line, then the probe.</summary>
    private async Task<AttemptResult> RecordAsync(Plan plan, Ran ran)
    {
        var paths = context.Paths;
        var result = ClaudeResultEnvelope.LastOf(files.ReadText(paths.Stream(_step, plan.Attempt)).Match(text => text, _ => string.Empty));
        var end = StepClassifier.Classify(result, files.Tail(paths.Stderr(_step, plan.Attempt), StepClassifier.StderrTailCharacters), ran.Facts);
        var missing = MissingOutputs(end);
        files.AppendTiming(new TimingLine.End(context.Arm, _step, plan.Attempt, deps.Clock.GetUtcNow(), ran.Wall, ran.Exit, result is Outcome<ClaudeResultEnvelope>.Ok, end is StepEnd.Limited, await HeadAsync(CancellationToken.None), end.Word, false)
        {
            Missing = [.. missing.Select(binding => binding.Field)],
        });
        deps.Probe.Reached(StepPoint.Classified);
        return new AttemptResult.Ended(end, plan.Attempt, ran.Exit, ran.Facts.StoppedByOperator);
    }

    /// <summary>The model-written outputs a DONE attempt left unwritten (E2 §8 item 41): said, recorded, never required.</summary>
    private IReadOnlyList<OutputBinding> MissingOutputs(StepEnd end)
    {
        IReadOnlyList<OutputBinding> missing = end.IsDone ? [.. _spec.ModelWritten.Where(binding => !files.Exists(context.Paths.NotesFile(binding)))] : [];
        foreach (var binding in missing)
        {
            log.Line($"{_step} done without writing {binding.Field} ({binding.RelativePath})");
        }

        return missing;
    }
}
