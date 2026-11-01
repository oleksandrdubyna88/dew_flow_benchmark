using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;

namespace ModelArms.Core.Chain;

/// <summary>The bounds of the chain's waits (§8 items 46 and 49), pure. A wait with no bound is a state with no timeout (common.reliability), so even <c>WaitFor</c> has one — computed from what the runners themselves obey, so the experiment file gains no member. It is generous on purpose: it bounds a wedged runner, it does not judge a slow one.</summary>
public static class ChainWaitCeiling
{
    private const long SecondsPerMinute = 60;

    /// <summary>How long a started arm may take to hold its runner's lock before the chain says it did not (§8 item 46): the runner's own waits before its lock are at most four seconds plus a process start, and this is the shell's own tick.</summary>
    public static TimeSpan StartupCeiling { get; } = TimeSpan.FromSeconds(60);

    /// <summary>One arm's longest wall by its own budgets: for every step its scheme places (<see cref="SchemePositions.Steps"/>) its <c>minutes</c> · 60, the kill grace and <c>tries</c> · (<c>waitSeconds</c> + <c>minimumRemainingSeconds</c>) — a <c>taskCode</c> step counted <c>tasks.max</c> times — plus, when the scheme has a gate step, <c>gateTries</c> · <c>gateWaitSeconds</c> and the gate lock's wait
    /// (<see cref="GateLockCeiling.For"/>, the arithmetic of <c>RunArmWalk.Ceiling</c>), plus <see cref="StartupCeiling"/>. Long arithmetic, each factor clamped at 0 first and each term at <paramref name="maximum"/> before it is added (a stranger's number is clamped before it is multiplied); the result at most <paramref name="maximum"/>, what a timer carries. An arm whose scheme is not among the experiment's has only the startup ceiling.</summary>
    public static TimeSpan OfArm(Experiment.Experiment experiment, string arm, int armsInMap, TimeSpan maximum)
    {
        var ceiling = (long)maximum.TotalSeconds;
        var steps = SchemeOf(experiment, arm) is { } scheme ? SchemePositions.Steps(scheme, experiment.Tasks) : [];
        var placed = steps.Select(step => experiment.Steps.GetValueOrDefault(step.Id)).OfType<StepSpec>().ToList();
        var seconds = placed.Sum(spec => Clamped(StepSeconds(experiment, spec, ceiling) * Times(experiment, spec), ceiling)) + Clamped(Gate(experiment, arm, armsInMap, placed, maximum), ceiling) + (long)StartupCeiling.TotalSeconds;
        return seconds >= ceiling ? maximum : TimeSpan.FromSeconds(seconds);
    }

    /// <summary>A round's = the largest of its arms'.</summary>
    public static TimeSpan OfRound(Experiment.Experiment experiment, IReadOnlyList<string> round, int armsInMap, TimeSpan maximum) =>
        round.Select(arm => OfArm(experiment, arm, armsInMap, maximum)).DefaultIfEmpty(StartupCeiling).Max();

    private static Scheme? SchemeOf(Experiment.Experiment experiment, string arm) =>
        ArmNames.SchemeOf(arm).Match(name => experiment.Schemes.FirstOrDefault(scheme => scheme.Name == name), _ => null);

    /// <summary>One run of the step: its wall, the kill grace and the usage-limit waits the runner may spend inside it — each product clamped before it is added to the next.</summary>
    private static long StepSeconds(Experiment.Experiment experiment, StepSpec spec, long ceiling)
    {
        var limits = experiment.Limits;
        var limitWaits = Clamped(Math.Max(limits.Tries, 0) * ((long)Math.Max(limits.WaitSeconds, 0) + Math.Max(limits.MinimumRemainingSeconds, 0)), ceiling);
        return Clamped(Math.Max(spec.Minutes, 0) * SecondsPerMinute, ceiling) + Math.Max(limits.KillGraceSeconds, 0) + limitWaits;
    }

    /// <summary>How many times the step runs: a task coder once per task, at most <c>tasks.max</c>.</summary>
    private static long Times(Experiment.Experiment experiment, StepSpec spec) => spec.Kind == StepKind.TaskCode ? Math.Max(experiment.Tasks.Max, 0) : 1;

    /// <summary>With a gate step: the gate's own tries and waits, and the wait for the lock behind the other arms of the arm's queue.</summary>
    private static long Gate(Experiment.Experiment experiment, string arm, int armsInMap, IReadOnlyList<StepSpec> placed, TimeSpan maximum)
    {
        if (!placed.Any(spec => spec.Kind == StepKind.Gate))
        {
            return 0;
        }

        var limits = experiment.Limits;
        var longestGate = experiment.Steps.Values.Where(spec => spec.Kind == StepKind.Gate).Select(spec => spec.Minutes).DefaultIfEmpty(0).Max();
        var queue = GateLockCeiling.QueueOf(experiment.Runs, arm, armsInMap);
        var lockWait = GateLockCeiling.For(queue.Arms, longestGate, limits.KillGraceSeconds, limits.Tries, limits.WaitSeconds, limits.MinimumRemainingSeconds, maximum);
        return (Math.Max(limits.GateTries, 0) * (long)Math.Max(limits.GateWaitSeconds, 0)) + (long)lockWait.TotalSeconds;
    }

    private static long Clamped(long seconds, long ceiling) => Math.Min(Math.Max(seconds, 0), ceiling);
}
