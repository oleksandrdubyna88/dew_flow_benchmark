namespace Bench.Domain.Scouts;

/// <summary>One scored cell as the sizing reads it: its arm, its task and the task's repository (the bootstrap's cluster), its recall and its
/// claims' tally (zero when its claims are not all assessed — a rate over part of them is biased by which part, §3.6).</summary>
public sealed record ScoutSizingSample(ScoutArmId Arm, string Task, string Repository, double Recall, ClaimTally Claims);

/// <summary>The sizing's settings. The seed fixes every simulated run; <see cref="Resamples"/> is the bootstrap INSIDE each simulated decision
/// (at its pre-registered seed, as the real decision runs it — fewer resamples than the report's 10 000, because the simulation repeats the
/// decision thousands of times); runs per task are FIXED at 3 and one task per repository (D10's draw), as §3.8 says.</summary>
public sealed record ScoutSizingSettings(int Seed, int Simulations, int Resamples, int MinTasks, int MaxTasks, int RunsPerTask, int TasksPerRepository)
{
    public const int DefaultSeed = 20261013;

    /// <summary>1 000: a rate near the 5 % limit then carries a Monte Carlo error of about ±0.7 points (at 200 it was ±1.5, enough to move the
    /// answer by several task counts — measured while building this).</summary>
    public const int DefaultSimulations = 1_000;

    public const int DefaultResamples = 1_000;

    /// <summary>§3.8: "with runs per task fixed at 3".</summary>
    public const int FixedRunsPerTask = 3;

    public static ScoutSizingSettings Default { get; } = new(DefaultSeed, DefaultSimulations, DefaultResamples, 8, 30, FixedRunsPerTask, 1);

    public static Outcome<ScoutSizingSettings> Of(int seed, int simulations, int resamples, int minTasks = 8, int maxTasks = 30) =>
        Refusal(simulations, resamples, minTasks, maxTasks) is { Length: > 0 } refusal
            ? Outcome<ScoutSizingSettings>.Failure(refusal)
            : Outcome<ScoutSizingSettings>.Success(new ScoutSizingSettings(seed, simulations, resamples, minTasks, maxTasks, FixedRunsPerTask, 1));

    private static string Refusal(int simulations, int resamples, int minTasks, int maxTasks) => (simulations, resamples, minTasks, maxTasks) switch
    {
        ( < 20 or > 100_000, _, _, _) => $"{simulations} simulations cannot size anything — 20 to 100 000",
        (_, < 100 or > 100_000, _, _) => $"{resamples} resamples is not a bootstrap — 100 to 100 000",
        _ when minTasks < 2 || maxTasks < minTasks || maxTasks > 200 => $"task counts {minTasks}..{maxTasks} are not a range to size — from at least 2, up to 200",
        _ => string.Empty,
    };
}

/// <summary>One workload at one task count: how often the simulated decision passed at a true Δ of 0, and at the −0.03 boundary.</summary>
public sealed record ScoutSizingWorkload(ScoutWorkload Workload, double PassAtZero, double PassAtBoundary);

/// <summary>One task count. <see cref="PassAtZero"/> is the RULE's — every sized workload passing — taken as the product of the workloads'
/// rates (they are simulated independently); <see cref="FalsePassAtBoundary"/> is the worst workload's: the chance that a workload sitting
/// exactly on the margin is passed anyway.</summary>
public sealed record ScoutSizingRow(int Tasks, IReadOnlyList<ScoutSizingWorkload> Workloads, double PassAtZero, double FalsePassAtBoundary)
{
    public bool Meets => PassAtZero >= ScoutSizing.PowerTarget && FalsePassAtBoundary <= ScoutSizing.FalsePassLimit;
}

/// <summary>What the pilot gave one workload: its paired tasks and their repositories, the cells per arm, the observed mean Δrecall (removed
/// before a true Δ is imposed), and whether wrong claims could be simulated at all (no assessed claim → the sizing is of recall alone).</summary>
public sealed record ScoutSizingPilot(ScoutWorkload Workload, int Tasks, int Repositories, int CellsA, int CellsB, double ObservedRecallDelta, bool WrongSimulated);

/// <summary>The sizing: the pilot it was drawn from, one row per task count, and the smallest count that meets both targets (0 = none).</summary>
public sealed record ScoutSizingResult(ScoutSizingSettings Settings, IReadOnlyList<ScoutSizingPilot> Pilot, IReadOnlyList<ScoutSizingRow> Rows, int SmallestTasks)
{
    public bool Found => SmallestTasks > 0;
}

/// <summary>§3.8's sizing, pure and seeded (E2 S8). Under D7 it is REPORTED, never a gate: it says how many tasks a decision run would need.
/// <para>
/// The pilot's cells are a non-parametric model of the noise: a simulated task draws one pilot task that has both arms of the workload, then
/// <see cref="ScoutSizingSettings.RunsPerTask"/> cells per arm from that task's cells (with replacement), averages recall and pools wrong claims
/// over them — the task is the unit, its runs averaged first, as the analysis does. The pilot's own mean difference is removed and the true Δ
/// imposed (recall Δ = 0 or −0.03; wrong-claim Δ = 0 in both, so the boundary row measures the recall margin alone). Each simulated task is
/// its own repository (one task per repository, D10), the deltas go through <see cref="ScoutBootstrap"/> at its pre-registered seed and
/// <see cref="ScoutDecisions.Workload"/> decides them as a DECISION run would. The same draws serve both Δs (common random numbers), so the two
/// rates of a row differ by the Δ, not by luck.
/// </para></summary>
public static class ScoutSizing
{
    /// <summary>§3.8: at least 80 % passes when the teams are equal.</summary>
    public const double PowerTarget = 0.80;

    /// <summary>§3.8: at most 5 % passes when the Sonnet team sits exactly on the recall margin.</summary>
    public const double FalsePassLimit = 0.05;

    /// <summary>The scored cells whose recall was captured; a cell with claims still waiting for a verdict contributes no claim tally.</summary>
    public static IReadOnlyList<ScoutSizingSample> Samples(IReadOnlyList<ScoutCellEvidence> cells) =>
        [.. cells.Where(c => c.Scored && c.Recall.Known).Select(c => new ScoutSizingSample(
            c.Cell.Arm, c.Cell.Task.Value, c.Repository, c.Recall.Value, c.Unassessed > 0 ? ClaimTally.Zero : c.Tally))];

    public static Outcome<ScoutSizingResult> Of(IReadOnlyList<ScoutSizingSample> samples, ScoutSizingSettings settings)
    {
        var models = Enum.GetValues<ScoutWorkload>().SelectMany(w => ScoutSizingModel.Of(w, samples)).ToList();

        if (models.Count == 0)
        {
            return Outcome<ScoutSizingResult>.Failure("no workload has a task with both of its deciding arms scored in this run — nothing to size from");
        }

        var rows = Enumerable.Range(settings.MinTasks, settings.MaxTasks - settings.MinTasks + 1).Select(tasks => Row(models, tasks, settings)).ToList();

        return Outcome<ScoutSizingResult>.Success(new ScoutSizingResult(
            settings, [.. models.Select(m => m.Summary)], rows, rows.FirstOrDefault(r => r.Meets)?.Tasks ?? 0));
    }

    private static ScoutSizingRow Row(IReadOnlyList<ScoutSizingModel> models, int tasks, ScoutSizingSettings settings)
    {
        var workloads = models.Select(m => new ScoutSizingWorkload(
            m.Workload, PassRate(m, tasks, 0.0, settings), PassRate(m, tasks, ScoutDecisions.RecallMargin, settings))).ToList();

        return new ScoutSizingRow(tasks, workloads, workloads.Aggregate(1.0, (p, w) => p * w.PassAtZero), workloads.Max(w => w.PassAtBoundary));
    }

    /// <summary>The share of simulated decision runs of <paramref name="tasks"/> tasks that PASS the workload at a true recall Δ.</summary>
    private static double PassRate(ScoutSizingModel model, int tasks, double trueDelta, ScoutSizingSettings settings)
    {
        var draws = new SeededDraws(SeedOf(settings.Seed, tasks, model.Workload));
        var bootstrap = new BootstrapSettings(BootstrapSettings.DefaultSeed, settings.Resamples);
        var passes = 0;

        for (var run = 0; run < settings.Simulations; run++)
        {
            passes += Passes(model.Simulate(tasks, trueDelta, settings, draws), bootstrap) ? 1 : 0;
        }

        return (double)passes / settings.Simulations;
    }

    private static bool Passes(ScoutSimulatedRun run, BootstrapSettings bootstrap)
    {
        var reading = new ScoutWorkloadReading(run.Workload, ScoutBootstrap.Of(run.Recall, bootstrap), Wrong(run, bootstrap), run.Wrong.Count > 0 || !run.WrongSimulated);
        return ScoutDecisions.Workload(reading, ScoutRunKind.Decision).Outcome == ScoutWorkloadOutcome.Pass;
    }

    /// <summary>The wrong-claim interval: the bootstrap over the simulated deltas; when the pilot had no assessed claim, an interval that passes
    /// (Δ = 0 everywhere) — the sizing is then of recall alone, and the pilot summary says so.</summary>
    private static ScoutInterval Wrong(ScoutSimulatedRun run, BootstrapSettings bootstrap) =>
        run.WrongSimulated
            ? ScoutBootstrap.Of(run.Wrong, bootstrap)
            : new ScoutInterval(true, 0, 0, 0, run.Recall.Count, run.Recall.Select(d => d.Cluster).Distinct(StringComparer.Ordinal).Count());

    /// <summary>One stream per (task count, workload), the SAME at both Δs: the rows do not depend on the order they are computed in.</summary>
    private static ulong SeedOf(int seed, int tasks, ScoutWorkload workload) =>
        unchecked(((ulong)(uint)seed << 32) | (uint)((tasks << 4) | (int)workload));
}

/// <summary>One simulated decision run of one workload: the per-task recall and wrong-claim deltas, each task its own cluster.</summary>
internal sealed record ScoutSimulatedRun(ScoutWorkload Workload, IReadOnlyList<TaskDelta> Recall, IReadOnlyList<TaskDelta> Wrong, bool WrongSimulated);

/// <summary>A pilot task with both deciding arms of a workload: its cells per arm, in a fixed order (so the input order of the samples does not
/// move a draw).</summary>
internal sealed record ScoutPilotTask(string Task, string Repository, IReadOnlyList<ScoutSizingSample> A, IReadOnlyList<ScoutSizingSample> B);

/// <summary>What the simulation draws from for one workload: the pilot's paired tasks, and the observed mean differences it removes before a
/// true Δ is imposed.</summary>
internal sealed record ScoutSizingModel(ScoutWorkload Workload, IReadOnlyList<ScoutPilotTask> Tasks, double RecallOffset, double WrongOffset, bool WrongSimulated)
{
    public ScoutSizingPilot Summary => new(
        Workload, Tasks.Count, Tasks.Select(t => t.Repository).Distinct(StringComparer.Ordinal).Count(), Tasks.Sum(t => t.A.Count), Tasks.Sum(t => t.B.Count),
        RecallOffset, WrongSimulated);

    /// <summary>The workload's model — none when no pilot task has both of its deciding arms.</summary>
    public static IReadOnlyList<ScoutSizingModel> Of(ScoutWorkload workload, IReadOnlyList<ScoutSizingSample> samples)
    {
        var (a, b) = (DecidingArm(workload, ScoutModel.Opus), DecidingArm(workload, ScoutModel.Sonnet));
        var tasks = samples.GroupBy(s => s.Task, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new ScoutPilotTask(g.Key, g.First().Repository, Ordered(g, a), Ordered(g, b)))
            .Where(t => t.A.Count > 0 && t.B.Count > 0)
            .ToList();
        var wrong = tasks.Select(t => (A: Rate(t.A), B: Rate(t.B))).Where(r => r.A.Measured && r.B.Measured).ToList();

        return tasks.Count == 0
            ? []
            : [new ScoutSizingModel(workload, tasks, tasks.Average(t => Mean(t.B) - Mean(t.A)), wrong.Count > 0 ? wrong.Average(r => (double)(r.B.PerTen - r.A.PerTen)) : 0.0, wrong.Count > 0)];
    }

    public ScoutSimulatedRun Simulate(int count, double trueDelta, ScoutSizingSettings settings, SeededDraws draws)
    {
        var recall = new List<TaskDelta>(count);
        var wrong = new List<TaskDelta>(count);

        for (var t = 0; t < count; t++)
        {
            var pilot = Tasks[draws.Below(Tasks.Count)];
            var (a, b) = (Runs(pilot.A, settings.RunsPerTask, draws), Runs(pilot.B, settings.RunsPerTask, draws));
            var (name, cluster) = ($"s{t:000}", $"r{t / settings.TasksPerRepository:000}");
            recall.Add(new TaskDelta(name, cluster, Mean(b) - Mean(a) - RecallOffset + trueDelta));
            AddWrong(wrong, name, cluster, Rate(a), Rate(b));
        }

        return new ScoutSimulatedRun(Workload, recall, wrong, WrongSimulated);
    }

    private void AddWrong(List<TaskDelta> wrong, string name, string cluster, ClaimRate a, ClaimRate b)
    {
        if (a.Measured && b.Measured)
        {
            wrong.Add(new TaskDelta(name, cluster, (double)(b.PerTen - a.PerTen) - WrongOffset));
        }
    }

    private static IReadOnlyList<ScoutSizingSample> Runs(IReadOnlyList<ScoutSizingSample> cells, int runs, SeededDraws draws) =>
        [.. Enumerable.Range(0, runs).Select(_ => cells[draws.Below(cells.Count)])];

    private static IReadOnlyList<ScoutSizingSample> Ordered(IEnumerable<ScoutSizingSample> cells, ScoutArmId arm) =>
        [.. cells.Where(c => c.Arm == arm).OrderBy(c => c.Recall).ThenBy(c => c.Claims.Wrong).ThenBy(c => c.Claims.Assessed)];

    private static double Mean(IReadOnlyList<ScoutSizingSample> cells) => cells.Average(c => c.Recall);

    private static ClaimRate Rate(IReadOnlyList<ScoutSizingSample> cells) => ScoutClaimRates.Pooled(cells.Select(c => c.Claims));

    private static ScoutArmId DecidingArm(ScoutWorkload workload, ScoutModel model) =>
        ScoutArms.All.First(arm => ScoutAnalysis.IsDeciding(arm, workload, model)).Id;
}
