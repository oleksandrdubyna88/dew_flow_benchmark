using Bench.Cli;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Ports;

namespace ModelArms.Verbs;

/// <summary>What <c>run-arm</c> is handed that production never changes: the clock every stamp and every log line is written with, the wait of a limit and of a blocked gate round, and the gate lock (none: the root's own file lock, <c>.gate.lock</c>).
/// Internal, so only the tests can hand in another: a console that could be given a lock of its own would skip the one the other arms take.</summary>
internal sealed record RunArmSeams(TimeProvider Clock, Func<TimeSpan, CancellationToken, Task> Wait, IGateLock? Lock)
{
    public static RunArmSeams Real { get; } = new(TimeProvider.System, (span, stopping) => Task.Delay(span, stopping), null);
}

/// <summary><c>run-arm &lt;arm&gt;</c> (D16; plan §4.1) for every sequential scheme — C, CR, CHH: the arm's scheme walked entry by entry over the step contract, one step after the other, and the arm is done when its last step is. The verb knows four kinds
/// of step and nothing about C, CR or CHH by name; a scheme with a <c>tasks</c> block is refused until E4 (exit 4). Admission is <see cref="RunArmAdmission"/> (the scheme, the token, the two resolved executables and the scenarios — each refusal
/// ends the walk before anything is launched), the walk is <see cref="RunArmWalk"/>. Exit 0 the arm is done; 3 the account is out or the host cannot say (a limit still on after the waits, a CLI that did not start); 4 a wrong configuration; 5 a
/// step stopped and the same command resumes it (an interrupted attempt, an error that is not a limit, the operator's stop).
/// <para>
/// <b>An exit 5 is never repeated by this verb.</b> A relaunch of an attempt that ended without a verdict costs one paid attempt (E2 §8 item 56): the verb returns, and the operator — or <c>run-all</c> — decides.
/// </para>
/// <para>
/// <b>The gate lock's ceiling (E2 §8 item 59) is a total time, and this verb names it:</b> <see cref="GateLockCeiling"/> over the arms of <c>arms.map.json</c> and the longest gate step of the experiment — (arms − 1) × (that step's minutes plus the kill grace),
/// never less than one such span — so a queue of gates that each hold the lock for their wall never passes it. It does NOT cover a usage limit waited out WHILE the lock is held (the runner waits inside the gate step: up to the step's minutes plus tries × waitSeconds — hours in the example), nor the closed arms of earlier runs that <c>arms.map.json</c> still lists (they count as queued); a wedged holder is told apart only when the ceiling runs out. E4's <c>run-all</c> must size its own from the largest round of its run (a gate's wall plus its limit waits, for the arms of that run only), or replace the
/// total time with a heartbeat (the holder is alive and its stream grows).
/// </para>
/// <para>
/// What this does NOT do: detach (E4); run the objective checks (E5).
/// </para></summary>
public static class RunArmVerb
{
    private const string Name = "model-arms run-arm";

    public static Task<int> RunAsync(ExperimentInput input) => RunAsync(input, RunArmSeams.Real);

    internal static async Task<int> RunAsync(ExperimentInput input, RunArmSeams seams)
    {
        var admitted = await RunArmAdmission.AdmitAsync(input);
        return await admitted.Match(plan => WalkAsync(plan, seams), stop => Task.FromResult(Refused(input, stop)));
    }

    private static int Refused(ExperimentInput input, Passage<ArmPlan>.Stopped stop)
    {
        foreach (var line in stop.Lines)
        {
            input.Verb.Error.WriteLine(line);
        }

        return stop.ExitCode;
    }

    private static Task<int> WalkAsync(ArmPlan plan, RunArmSeams seams) =>
        RunArmAdmission.LockFor(seams).Match(
            gateLock => RunAsync(plan, new RunArmWalk(plan, seams, gateLock)),
            reason => Task.FromResult(Stopped(plan.Input, ExitCodes.Environment, $"{Name}: {reason}")));

    private static async Task<int> RunAsync(ArmPlan plan, RunArmWalk walk)
    {
        PrintExecutables(plan);
        var stopped = await walk.RunAsync(plan.Input.Verb.Stopping);
        if (stopped is not null)
        {
            return Stopped(plan.Input, stopped.ExitCode, $"{Name} {plan.Arm}: {stopped.Why}");
        }

        walk.Finish();
        return ExitCodes.Pass;
    }

    private static int Stopped(ExperimentInput input, int code, string line)
    {
        input.Verb.Error.WriteLine(line);
        return code;
    }

    /// <summary>The two REAL paths the arm will launch, said before the first launch (D17 c): the value printed is the value every <c>StepContext</c> carries, never a second look-up.</summary>
    private static void PrintExecutables(ArmPlan plan)
    {
        var output = plan.Input.Verb.Output;
        output.WriteLine("resolved executables:");
        output.WriteLine($"  claude → {plan.Tools.Claude}");
        output.WriteLine($"  git → {plan.Tools.Git}");
        output.Flush();
    }
}
