using System.Globalization;
using System.Text;

namespace Bench.Domain.Scouts;

/// <summary>The sizing as text (E2 S8) — the Markdown <c>bench scouts sizing --out</c> writes and the lines it prints. It carries counts, rates
/// and the pilot's observed mean difference only: no task id, label, repository, statement or path, so a private task cannot reach it by any
/// route — nothing private is passed in. Byte-deterministic for the same result.</summary>
public static class ScoutSizingReport
{
    public const string ReportedNotAGate = "reported, not a gate";

    public static string Markdown(Guid runId, ScoutRunKind kind, ScoutSizingResult result)
    {
        var s = result.Settings;
        var text = new StringBuilder()
            .Append("# Scout sizing — how many tasks would a decision run need\n\n")
            .Append(Invariant($"> **{ReportedNotAGate}** (plan D7, §3.8). Run `{runId}` ({ScoutRunKinds.Word(kind)}). The pre-registered decision, "))
            .Append(Invariant($"simulated {s.Simulations} times per task count and per true Δ: seed {s.Seed}, {s.RunsPerTask} runs per task, "))
            .Append(Invariant($"{s.TasksPerRepository} task per repository, each simulated decision's clustered bootstrap at its pre-registered seed with {s.Resamples} resamples. "))
            .Append(Invariant($"Target: ≥ {Percent(ScoutSizing.PowerTarget)} passes at Δ = 0 and ≤ {Percent(ScoutSizing.FalsePassLimit)} false passes at Δrecall = {ScoutDecisions.RecallMargin:0.00}. "))
            .Append(Invariant($"A rate near the {Percent(ScoutSizing.FalsePassLimit)} limit carries a Monte Carlo error of about ±{MonteCarloPoints(s.Simulations):0.0} points at {s.Simulations} simulations.\n\n"))
            .Append("## The pilot it is drawn from\n\n")
            .Append("| workload | paired tasks | repositories | cells A | cells B | observed Δrecall (B − A) | wrong claims |\n")
            .Append("|---|---|---|---|---|---|---|\n");

        foreach (var workload in Enum.GetValues<ScoutWorkload>())
        {
            text.Append(PilotRow(workload, result.Pilot.FirstOrDefault(p => p.Workload == workload) is { } pilot ? [pilot] : []));
        }

        text.Append("\n## Per task count\n\n")
            .Append(Invariant($"| tasks | pass at Δ = 0 ({Workloads(result)}) | pass at Δrecall = {ScoutDecisions.RecallMargin:0.00} ({Workloads(result)}) | the rule passes at 0 | worst false pass | meets both |\n"))
            .Append("|---|---|---|---|---|---|\n");

        foreach (var row in result.Rows)
        {
            text.Append(Invariant($"| {row.Tasks} | {Rates(row, w => w.PassAtZero)} | {Rates(row, w => w.PassAtBoundary)} | {Percent(row.PassAtZero)} | {Percent(row.FalsePassAtBoundary)} | {(row.Meets ? "yes" : "no")} |\n"));
        }

        return text.Append("\n## The answer\n\n").Append(Answer(result)).Append('\n').ToString();
    }

    /// <summary>What the verb prints: one line per task count, then the answer.</summary>
    public static IReadOnlyList<string> Lines(ScoutSizingResult result) =>
        [.. result.Rows.Select(r => Invariant($"sizing         {r.Tasks,2} task(s) — the rule passes at Δ = 0 in {Percent(r.PassAtZero)}, the worst workload at the margin in {Percent(r.FalsePassAtBoundary)}{(r.Meets ? " — meets both" : string.Empty)}")),
         "answer         " + Answer(result)];

    public static string Answer(ScoutSizingResult result) =>
        (result.Found, result.Pilot.All(p => p.WrongSimulated), result.Pilot.Count == Enum.GetValues<ScoutWorkload>().Length) switch
        {
            (true, var wrong, var all) => Invariant($"{result.SmallestTasks} tasks is the smallest count from {result.Settings.MinTasks} to {result.Settings.MaxTasks} that meets both targets") + Caveats(wrong, all),
            (false, var wrong, var all) => Invariant($"no task count from {result.Settings.MinTasks} to {result.Settings.MaxTasks} meets both targets — precision is bought with tasks, never with a wider margin (§3.8)") + Caveats(wrong, all),
        };

    private static string Caveats(bool wrongSimulated, bool everyWorkload) =>
        (wrongSimulated ? string.Empty : "; wrong claims were not simulated for every workload (no assessed claim), so that part is of recall alone")
        + (everyWorkload ? "." : "; not every workload had a paired task, so the rule's rate covers only the workloads listed.");

    private static string PilotRow(ScoutWorkload workload, IReadOnlyList<ScoutSizingPilot> pilot) =>
        pilot.Count == 0
            ? $"| {workload} | 0 | 0 | 0 | 0 | — | not sized — no task had both deciding arms scored |\n"
            : Invariant($"| {workload} | {pilot[0].Tasks} | {pilot[0].Repositories} | {pilot[0].CellsA} | {pilot[0].CellsB} | {pilot[0].ObservedRecallDelta:+0.000;-0.000;0.000} | {(pilot[0].WrongSimulated ? "simulated" : "not simulated — no assessed claim; sized on recall alone")} |\n");

    private static string Workloads(ScoutSizingResult result) => string.Join(" · ", result.Pilot.Select(p => p.Workload.ToString()));

    private static string Rates(ScoutSizingRow row, Func<ScoutSizingWorkload, double> rate) => string.Join(" · ", row.Workloads.Select(w => Percent(rate(w))));

    /// <summary>The binomial standard error, in percentage points, of a rate at the false-pass limit over <paramref name="simulations"/> runs.</summary>
    public static double MonteCarloPoints(int simulations) =>
        100 * Math.Sqrt(ScoutSizing.FalsePassLimit * (1 - ScoutSizing.FalsePassLimit) / simulations);

    private static string Percent(double share) => (share * 100).ToString("0.0", CultureInfo.InvariantCulture) + " %";

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
