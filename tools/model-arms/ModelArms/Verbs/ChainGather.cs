using Bench.Cli;
using ModelArms.Adapters;
using ModelArms.Core.Chain;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;

namespace ModelArms.Verbs;

/// <summary>What the chain reads of the world before each decision (E5.S4, step 6): for every arm of the run its markers, its timing and whether its runner holds its lock; for every OTHER arm of the map whether its runner does; the run's three markers; the isolation snapshot; the digest <see cref="LockVerb.PlanOf"/> gives this run; the grace and the ceiling of the round it is in. Every file is opened BY NAME through
/// the kernel's presence call or the bounded road — never a listing, because the root and <c>pids/</c> are at 300 while the isolation stands. A presence the kernel cannot answer is exit 3, a refused liveness 4 and an unread one 3: <i>cannot tell</i> is never read as <i>no</i>. The spans (<see cref="ChainFacts.StartingFor"/> and the others) are the walk's, from its own instants; this leaves them zero.</summary>
internal static class ChainGather
{
    private const int TimingLimit = 16 * 1024 * 1024;

    public static Passage<ChainFacts> Read(ExperimentInput input, ChainSeams seams, RunSpec run, IReadOnlySet<string> startedHere)
    {
        var name = $"model-arms chain {run.Name}";
        return IsolationLocking.MapOf(input, PlatformFactsReader.CurrentRoad).Bind(map =>
            Each(RunArmNames(run), arm => ArmOf(input, seams, map, arm, name)).Bind(arms =>
                Others(input, seams, map, run).Bind(others =>
                    Markers(input, run, name).Bind(markers =>
                        Digest(input, seams, run, arms, markers).Map(digest => Facts(input, run, map, arms, others, markers, digest, startedHere))))));
    }

    /// <summary>This run's arms in round order, once each.</summary>
    internal static IReadOnlyList<string> RunArmNames(RunSpec run) => [.. run.Rounds.SelectMany(round => round).Distinct(StringComparer.Ordinal)];

    private static ChainFacts Facts(ExperimentInput input, RunSpec run, ArmMap map, IReadOnlyList<ArmFacts> arms, IReadOnlyList<string> others, (bool Locked, bool Unlocked, bool ArmsDone) markers, string? digest, IReadOnlySet<string> startedHere)
    {
        var experiment = input.Experiment;
        var round = run.Rounds.FirstOrDefault(each => each.Any(arm => !arms.First(facts => facts.Arm == arm).Done)) ?? run.Rounds[0];
        return new ChainFacts(
            run,
            arms,
            others,
            markers.Locked,
            markers.Unlocked,
            markers.ArmsDone,
            IsolationFiles.State(input.Root),
            digest,
            startedHere,
            TimeSpan.Zero,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(Math.Max(experiment.Limits.KillGraceSeconds, 0)),
            TimeSpan.Zero,
            ChainWaitCeiling.OfRound(experiment, round, map.Entries.Count, GateStep.MaxLockCeiling));
    }

    private static Passage<IReadOnlyList<T>> Each<T>(IEnumerable<string> names, Func<string, Passage<T>> read)
    {
        var all = new List<T>();
        foreach (var each in names)
        {
            var one = read(each);
            if (one is Passage<T>.Stopped stopped)
            {
                return new Passage<IReadOnlyList<T>>.Stopped(stopped.ExitCode, stopped.Lines);
            }

            all.Add(((Passage<T>.Through)one).Value);
        }

        return new Passage<IReadOnlyList<T>>.Through(all);
    }

    private static Passage<ArmFacts> ArmOf(ExperimentInput input, ChainSeams seams, ArmMap map, string arm, string name) =>
        Present(input.Root.Made(arm), name).Bind(made =>
            Present(input.Root.ObjectiveMarker(arm), name).Bind(@checked =>
                Present(input.Root.PidFile(arm), name).Bind(ran =>
                    Alive(input, seams, arm).Bind(alive =>
                        DoneOf(input, map, arm, name).Map(done => new ArmFacts(arm, made, done, alive, ran, @checked))))));

    /// <summary>The map's arms this run does not name, asked whether their runner holds its lock.</summary>
    private static Passage<IReadOnlyList<string>> Others(ExperimentInput input, ChainSeams seams, ArmMap map, RunSpec run)
    {
        var own = RunArmNames(run);
        return Each(map.Entries.Select(entry => entry.Key).Where(arm => !own.Contains(arm, StringComparer.Ordinal)), arm => Alive(input, seams, arm).Map(alive => alive ? arm : string.Empty))
            .Map(all => (IReadOnlyList<string>)[.. all.Where(arm => arm.Length > 0)]);
    }

    private static Passage<bool> Alive(ExperimentInput input, ChainSeams seams, string arm) =>
        seams.Liveness(input.Root, arm) switch
        {
            RunnerState.Alive => new Passage<bool>.Through(true),
            RunnerState.Refused refused => Passage<bool>.Stop(ExitCodes.Configuration, $"model-arms chain: {refused.Reason}"),
            RunnerState.Unread unread => Passage<bool>.Stop(ExitCodes.Environment, $"model-arms chain: {unread.Reason}"),
            _ => new Passage<bool>.Through(false),
        };

    private static Passage<bool> Present(string path, string name) =>
        KernelFacts.Present(path).Match<Passage<bool>>(
            present => new Passage<bool>.Through(present),
            reason => Passage<bool>.Stop(ExitCodes.Environment, $"{name}: cannot tell whether {path} is there: {reason}"));

    // The order above is the point: the pid file, then the runner's lock, THEN the timing. A runner appends its `arm-done` line and exits; read in the other order, the chain could see "no arm-done" and then "no lock" for an arm that finished between the two reads, and stop a finished arm as died. In this order a runner that finishes between the reads is seen alive (awaited) or done.

    /// <summary>Whether the arm's <c>out/timing.jsonl</c> says it finished (<see cref="TimingLines.IsArmDone"/>); an arm with no token yet, or no timing, has not.</summary>
    private static Passage<bool> DoneOf(ExperimentInput input, ArmMap map, string arm, string name)
    {
        if (map.TokenOf(arm) is not Bench.Domain.Outcome<string>.Ok token)
        {
            return new Passage<bool>.Through(false);
        }

        return BoundedFile.Read(input.Root.Arm(token.Value).Timing, TimingLimit, PlatformFactsReader.CurrentRoad) switch
        {
            BoundedRead.Bytes bytes => new Passage<bool>.Through(TimingLines.IsArmDone(TimingLines.Read(System.Text.Encoding.UTF8.GetString(bytes.Value)), arm)),
            BoundedRead.Refused refused => Passage<bool>.Stop(ExitCodes.Configuration, $"{name}: {arm}'s timing is refused: {refused.Reason}"),
            _ => new Passage<bool>.Through(false),
        };
    }

    private static Passage<(bool Locked, bool Unlocked, bool ArmsDone)> Markers(ExperimentInput input, RunSpec run, string name) =>
        Present(input.Root.RunMarker(run.Name, RunStage.Locked), name).Bind(locked =>
            Present(input.Root.RunMarker(run.Name, RunStage.Unlocked), name).Bind(unlocked =>
                Present(input.Root.RunMarker(run.Name, RunStage.ArmsDone), name).Map(done => (locked, unlocked, done))));

    /// <summary>The digest of this run's plan — needed ONLY by the reconciliation (rule 5), so it is asked only when rules 1 to 4 do not already decide: a plan that cannot be made (a <c>closedArms</c> entry never made, an arm the map lacks) must not stop the chain's own make, lock or checks. <c>null</c> when it is not asked, when the run closes nothing, and when the folder isolation of this road is
    /// <see cref="NoIsolation"/> (<see cref="FolderIsolations.For"/> — the SAME choice <c>lock</c> makes, so the two cannot drift: on Windows <c>lock</c> says <i>POSIX only</i> and <c>locked</c> stands over no snapshot). A plan that cannot be made is the stop <see cref="LockVerb.PlanOf"/> gives.</summary>
    private static Passage<string?> Digest(ExperimentInput input, ChainSeams seams, RunSpec run, IReadOnlyList<ArmFacts> arms, (bool Locked, bool Unlocked, bool ArmsDone) markers)
    {
        if (!markers.Locked || markers.Unlocked || markers.ArmsDone || arms.Any(arm => !arm.Made))
        {
            return new Passage<string?>.Through(null);
        }

        using var isolation = FolderIsolations.For(seams.Road, hands: null, events: null);
        return isolation.Applies ? LockVerb.PlanOf(input, run.Name).Map(plan => plan?.Digest) : new Passage<string?>.Through(null);
    }
}
