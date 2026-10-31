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
/// of step and nothing about C, CR or CHH by name; a scheme with a <c>tasks</c> block is refused until E4.S3 (exit 4). Admission is <see cref="RunArmAdmission"/> (the scheme, the token, the two resolved executables and the scenarios — each refusal
/// ends the walk before anything is launched), the walk is <see cref="RunArmWalk"/>. Exit 0 the arm is done; 3 the account is out or the host cannot say (a limit still on after the waits, a CLI that did not start); 4 a wrong configuration; 5 a
/// step stopped and the same command resumes it (an interrupted attempt, an error that is not a limit, the operator's stop).
/// <para>
/// <b>An exit 5 is never repeated by this verb.</b> A relaunch of an attempt that ended without a verdict costs one paid attempt (E2 §8 item 56): the verb returns, and the operator — or <c>run-all</c> — decides.
/// </para>
/// <para>
/// <b>The gate lock's ceiling (E2 §8 items 59 and 61) is a total time, and this verb names it:</b> <see cref="GateLockCeiling"/> — for each of the other arms of the arm's own round (the largest round of the run that names it; every arm of
/// <c>arms.map.json</c> only when no run does, and the log says so), the longest gate step's minutes plus the kill grace plus the usage-limit waits the runner may spend under the lock and the floor of each resumed attempt (<c>limits.tries</c> × (<c>limits.waitSeconds</c> + <c>limits.minimumRemainingSeconds</c>)); never less than
/// one such span. What remains is a wedged holder, which only a heartbeat (out of bounds for E5) tells from a slow one: it is told apart only when the ceiling runs out. <c>run-all</c> (E5.S3) sizes nothing of its own: it refuses to have more arms alive than the smallest queue any of them was sized from, so a ceiling is never outrun by the arms it started.
/// </para>
/// <para>
/// <b>The runner is identified by two locks held for its life</b> (E5.S1, D13): <c>pids/&lt;arm&gt;.lock</c> — the answer to whether the arm runs, free the instant the process dies — and the SHARED <c>.isolation.lock</c>, so no <c>lock</c> or <c>unlock</c> changes a mode while it runs; its pid is written to <c>pids/&lt;arm&gt;.pid</c>. Both are taken by <see cref="RunArmAdmission"/> before anything else and
/// disposed here after the walk, whatever it came to.
/// </para>
/// <para>
/// What this does NOT do: detach (<c>run-all</c> starts it detached, E5.S3); run the objective checks (E3.S5's <c>checks</c>).
/// </para></summary>
public static class RunArmVerb
{
    private const string Name = "model-arms run-arm";

    public static Task<int> RunAsync(ExperimentInput input) => RunAsync(input, RunArmSeams.Real);

    internal static async Task<int> RunAsync(ExperimentInput input, RunArmSeams seams)
    {
        var admitted = await RunArmAdmission.AdmitAsync(input);
        return await admitted.Match(plan => HeldAsync(plan, seams), stop => Task.FromResult(Refused(input, stop)));
    }

    /// <summary>The walk, with the runner's two locks released after it whatever it came to (the kernel releases them when the process dies).</summary>
    private static async Task<int> HeldAsync(ArmPlan plan, RunArmSeams seams)
    {
        try
        {
            return await WalkAsync(plan, seams);
        }
        finally
        {
            plan.RunnerLock.Dispose();
            plan.IsolationHold.Dispose();
        }
    }

    private static int Refused(ExperimentInput input, Passage<ArmPlan>.Stopped stop) => stop.Print(input.Verb.Error);

    private static Task<int> WalkAsync(ArmPlan plan, RunArmSeams seams) =>
        RunArmAdmission.LockFor(seams).Match(
            gateLock => RunAsync(plan, new RunArmWalk(plan, seams, gateLock)),
            reason => Task.FromResult(Stopped(plan.Input, ExitCodes.Environment, $"{Name}: {reason}")));

    private static async Task<int> RunAsync(ArmPlan plan, RunArmWalk walk)
    {
        PrintExecutables(plan);
        walk.SayQueue();
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
