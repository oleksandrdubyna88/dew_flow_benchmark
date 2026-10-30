using Bench.Infrastructure.Models;
using Microsoft.Extensions.Logging.Abstractions;
using ModelArms.Adapters;
using ModelArms.Core.Briefs;
using ModelArms.Core.Experiment;
using ModelArms.Core.Ports;
using ModelArms.Core.Steps;

namespace ModelArms.Verbs;

/// <summary>The walk of one arm: one <see cref="StepRunner"/> and one <see cref="GateStep"/> built over the SAME clock, the SAME log writer and the SAME <see cref="ArmMap"/> (E2 §8 item 58 — the gate stamps its own <c>gate-wait</c> and <c>stopped</c>
/// lines and scrubs its own log through them), the scheme's entries in order, each through the runner or, for a gate step, through the gate; the first step that stops ends the walk and nothing after it launches. The arm's <c>arm-done</c> line is
/// written once, <b>unless this arm already has one after the timing's last start or gate-wait line</b>: a re-run of a finished arm adds nothing (the shell appended one per run). Every wait is the one the seams hold, bounded and cancellable by the operator's stop.</summary>
internal sealed class RunArmWalk
{
    private readonly ArmPlan _plan;
    private readonly TimeProvider _clock;
    private readonly StepRunner _runner;
    private readonly GateStep _gate;
    private readonly ArmFiles _files;
    private readonly StepLog _log;
    private readonly BriefTemplateFiles _templates;
    private readonly BriefLeakGuard _guard;

    public RunArmWalk(ArmPlan plan, RunArmSeams seams, IGateLock gateLock)
    {
        var input = plan.Input;
        var environment = input.Verb.Environment;
        _plan = plan;
        _clock = seams.Clock;
        _files = new ArmFiles(plan.Paths);
        _log = new StepLog(input.Verb.Output, plan.Map, seams.Clock, plan.Arm, _files);
        _templates = new BriefTemplateFiles(input.Verb.Module.ShippedBriefs, input.Root.Briefs);
        _guard = new BriefLeakGuard(plan.Map, plan.Arm, [.. input.Experiment.Schemes.Select(scheme => scheme.Name)], input.Root.Root);
        _runner = new StepRunner(
            new CliAgentRuntime(NullLogger<CliAgentRuntime>.Instance), new ClaudeSessionTranscripts(SessionTranscripts.ClaudeHome(environment)), seams.Clock, seams.Wait, input.Verb.Output, plan.Map, StepProbe.None);
        _gate = new GateStep(_runner, gateLock, seams.Clock, seams.Wait, input.Verb.Output, plan.Map, Ceiling(plan));
    }

    /// <summary>The gate lock's ceiling for this arm (E2 §8 items 59 and 61): each holder's wall, kill grace and limit waits under the lock, for the other arms of the arm's own round; see <see cref="GateLockCeiling"/>.</summary>
    internal static TimeSpan Ceiling(ArmPlan plan)
    {
        var experiment = plan.Input.Experiment;
        var longestGate = experiment.Steps.Values.Where(spec => spec.Kind == StepKind.Gate).Select(spec => spec.Minutes).DefaultIfEmpty(0).Max();
        var limits = experiment.Limits;
        return GateLockCeiling.For(Queue(plan).Arms, longestGate, limits.KillGraceSeconds, limits.Tries, limits.WaitSeconds, limits.MinimumRemainingSeconds, GateStep.MaxLockCeiling);
    }

    private static GateQueue Queue(ArmPlan plan) => GateLockCeiling.QueueOf(plan.Input.Experiment.Runs, plan.Arm, plan.Map.Entries.Count);

    /// <summary>Said once, before the first launch: an arm no run names is sized from every arm of the map, which may include closed arms of earlier runs.</summary>
    public void SayQueue()
    {
        var queue = Queue(_plan);
        if (!queue.FromRound)
        {
            _log.Line($"{_plan.Arm} is in no run — the gate lock's wait is sized from the {queue.Arms.ToString(System.Globalization.CultureInfo.InvariantCulture)} arms of arms.map.json, closed arms of earlier runs included");
        }
    }

    /// <summary>The scheme in order. Null when every step is done; else the step that stopped the arm — its exit code and reason (D16).</summary>
    public async Task<StepOutcome.Stopped?> RunAsync(CancellationToken stopping)
    {
        for (var position = 0; position < _plan.Scheme.Entries.Count; position++)
        {
            var context = ContextAt(position);
            var outcome = context.Spec.Kind == StepKind.Gate ? await _gate.RunAsync(context, stopping) : await _runner.RunAsync(context, stopping);
            if (outcome is StepOutcome.Stopped stopped)
            {
                return stopped;
            }
        }

        return null;
    }

    /// <summary>The arm ran its whole scheme: one <c>arm-done</c> line, unless this arm already has one AFTER the last <c>start</c> or <c>gate-wait</c> line — nothing has run since it. A <c>stopped</c> line after an <c>arm-done</c> (a stop while a rerun
    /// waited for the gate lock) does not make the arm unfinished: only work does.</summary>
    public void Finish()
    {
        if (!TimingLines.IsArmDone(_files.ReadTiming(), _plan.Arm))
        {
            _files.AppendTiming(new TimingLine.ArmDone(_plan.Arm, _clock.GetUtcNow()));
        }

        _log.Line("arm done");
    }

    private StepContext ContextAt(int position) =>
        new(
            _plan.Arm,
            _plan.Paths,
            _plan.Scheme,
            position,
            _plan.Input.Experiment,
            _plan.Tools.Claude,
            _plan.Tools.Git,
            _plan.Paths.CloneFolder,
            _plan.Input.Verb.Environment,
            _templates,
            _guard,
            _plan.ScenarioCount,
            TaskFacts.None,
            string.Empty);
}
