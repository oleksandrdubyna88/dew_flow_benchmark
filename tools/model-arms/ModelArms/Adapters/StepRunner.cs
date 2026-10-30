using Bench.Application;
using ModelArms.Core.Briefs;
using ModelArms.Core.Experiment;
using ModelArms.Core.Ports;
using ModelArms.Core.Steps;

namespace ModelArms.Adapters;

/// <summary>Everything the runner needs to run ONE step of ONE arm (plan C8, E2 §8 items 25 and 30): the arm, its paths, the scheme and the position of the step's entry (C12 — the entry is
/// <c>Scheme.Entries[Position]</c>, a <see cref="SchemeEntry.Step"/>), the experiment, the two resolved executables — <see cref="GitExecutable"/> is the REAL path <c>run-arm</c> resolved, and every git
/// launch of the step goes by it (D17 d) — the arm's clone as the working directory, the parent's environment, the briefs, the leak guard, the facts the brief states, and whether the runner derives
/// the step's outputs (the gate step turns it off and does it itself, E2.S7).</summary>
public sealed record StepContext(
    string Arm,
    ArmPaths Paths,
    Scheme Scheme,
    int Position,
    Experiment Experiment,
    string ClaudeExecutable,
    string GitExecutable,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> ParentEnvironment,
    BriefTemplateFiles Templates,
    BriefLeakGuard Guard,
    int ScenarioCount,
    TaskFacts Task,
    string Problems,
    bool DeriveOutputs = true)
{
    /// <summary>The scheme entry at <see cref="Position"/>. A position that is not a step entry is a caller's bug.</summary>
    public SchemeEntry.Step Step =>
        Scheme.Entries[Position] as SchemeEntry.Step
        ?? throw new InvalidOperationException($"position {Position} of scheme {Scheme.Name} is not a step entry — a tasks block runs its steps through the task pipeline");

    /// <summary>The step's configuration: <c>Experiment.Steps[Step.Id]</c>.</summary>
    public StepSpec Spec => Experiment.Steps[Step.Id];
}

/// <summary>How one run of a step ended. <see cref="Done"/>: the step is done (<paramref name="Skipped"/> when it was done already, <paramref name="Recovered"/> when its stream had finished before the runner died).
/// <see cref="Stopped"/>: the arm stops with this exit code (D16) and this reason; the step has no <c>.done</c>, except a skipped one whose derived output could not be reconciled.</summary>
public abstract record StepOutcome
{
    private StepOutcome()
    {
    }

    public sealed record Done(StepEnd End, bool Skipped, bool Recovered) : StepOutcome;

    public sealed record Stopped(int ExitCode, string Why) : StepOutcome;
}

/// <summary>The places in a step's run a test can stand in (E2 §8 item 45): a probe that throws there is a runner killed at that point. <see cref="Prepared"/> is the prompt on disk, with no stream and no start line yet
/// (E2 §8 item 56: the start line waits for the stream, so a restart in this window never leaves two start lines for one attempt).</summary>
public enum StepPoint
{
    SessionWritten,
    Prepared,
    Launched,
    Classified,
    OutputsDerived,
}

/// <summary>A seam production never uses: <see cref="None"/> does nothing at every point.</summary>
public class StepProbe
{
    public static StepProbe None { get; } = new();

    public virtual void Reached(StepPoint point)
    {
    }
}

/// <summary>One step, end to end (plan §4.5, D6–D9, D21; <c>run-arm.sh:76-126</c>): recover from what the step left, prepare and launch an attempt, classify it from the files it wrote, wait out a limit and resume the SAME session,
/// derive the outputs, write <c>.done</c>. The flow itself is <c>StepRun</c> (one per call) and <c>StepAttempt</c> (one per attempt); this type is the contract the gate step and <c>run-arm</c> hold.
/// <para>
/// Residual: the runner is one process per arm; two runners over one arm's folder are not supported (the gate's lock is a different thing).
/// </para></summary>
public sealed class StepRunner(ICliAgentTranscripts cli, ISessionTranscripts transcripts, TimeProvider clock, Func<TimeSpan, CancellationToken, Task> wait, TextWriter log, ArmMap map, StepProbe probe)
{
    private readonly StepDeps _deps = new(cli, transcripts, clock, wait, log, map, probe);

    /// <summary>Step 1's decision over the step's files — the gate step asks it too (E2.S7). Nothing is launched.</summary>
    public StepEntry Recover(StepContext context) => new StepRun(context, _deps).Recover();

    public Task<StepOutcome> RunAsync(StepContext context, CancellationToken stopping) => new StepRun(context, _deps).RunAsync(stopping);
}
