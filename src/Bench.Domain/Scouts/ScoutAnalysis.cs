using Bench.Domain.Gate;

namespace Bench.Domain.Scouts;

/// <summary>One cell as the analysis reads it — the highest SETTLED generation of its lineage — with its task's repository (the bootstrap's
/// cluster), its scouts' records, and the verdicts its claims received so far.</summary>
public sealed record ScoutCellEvidence(ScoutCell Cell, string Repository, IReadOnlyList<ScoutRecord> Scouts, IReadOnlyList<ScoutClaimVerdict> Verdicts)
{
    public bool Scored => Cell.Outcome == ScoutCellOutcome.Scored;

    /// <summary>Wrong ÷ decided over the cell's verdicts.</summary>
    public ClaimTally Tally => ScoutClaimVerdicts.Tally(Verdicts);

    /// <summary>Claims parsed and still waiting for a verdict.</summary>
    public int Unassessed => Math.Max(0, Scouts.Sum(s => s.ClaimsParsed) - Verdicts.Count);

    /// <summary>A scored cell whose team has NO decided claim — no wrong-claim rate for this cell (§3.6). Every scout silent, every claim
    /// unparseable, unresolved or not yet assessed: the cell still counts for recall, and its arm's silent share is what the 10 % rule reads.</summary>
    public bool Silent => Scored && Tally.Assessed == 0;

    /// <summary>Found ÷ |G|, for a scored cell whose recall was captured.</summary>
    public Figure Recall =>
        Scored && Cell.Facts.RecallFound.WasCaptured && Cell.Facts.RecallTotal is { WasCaptured: true, Value: > 0 } total
            ? Figure.Of((double)Cell.Facts.RecallFound.Value / total.Value)
            : Figure.Unknown;

    /// <summary>The caller's load: the size of the union of the team's KEY FILES.</summary>
    public int CallerLoad => Scouts.SelectMany(s => s.KeyFiles).Distinct(StringComparer.OrdinalIgnoreCase).Count();
}

/// <summary>One task under one arm: its scored cells (its runs) averaged FIRST — recall as the mean over runs, the wrong-claim rate POOLED over
/// runs (Σ wrong ÷ Σ decided × 10, so a run with one claim does not weigh as much as a run with ten).</summary>
public sealed record ScoutTaskArm(string Task, string Repository, ScoutArmId Arm, int Cells, int Scored, Figure Recall, ClaimRate Wrong, Figure TeamMs, Figure TeamUsd);

/// <summary>An arm's counts — every cell of it, by how it ended, and every claim of its scored cells, by what became of it.</summary>
public sealed record ScoutArmCounts(
    int Cells, int Scored, int Void, int Stopped, int Aborted,
    int SilentCells, int SilentScouts, int UnparseableClaims, int Wrong, int Decided, int Unresolved, int Unassessed, int TeamTimeBelowSlowestScout);

/// <summary>An arm's tokens: the team's per scored cell (exact), and one scout's — input and cache always its own, its OUTPUT only where the
/// scout was the one scout on its model (<see cref="ScoutTokens"/>), else not captured.</summary>
public sealed record ScoutArmTokens(Figure TeamInput, Figure TeamOutput, Figure TeamCacheRead, Figure TeamCacheWrite, Figure ScoutInput, Figure ScoutOutput)
{
    /// <summary>The team's cache writes by their TTL — priced differently, so the cost split (2026-10-14) needs them apart.</summary>
    public Figure TeamCacheWrite5m { get; init; } = Figure.Unknown;

    public Figure TeamCacheWrite1h { get; init; } = Figure.Unknown;
}

/// <summary>Why an arm's wrong-claim result is not measured — the half of D5 it cannot then answer.</summary>
public enum ScoutWrongGap
{
    None,

    /// <summary>More than 10 % of the arm's scored cells are silent (§3.6).</summary>
    SilentCells,

    /// <summary>Claims of its scored cells are still waiting for <c>bench scouts assess</c> — a rate over part of them is biased by which part.</summary>
    ClaimsUnassessed,

    /// <summary>No scored cell.</summary>
    NoScoredCell,
}

/// <summary>One arm, every number §3.6 reports: recall and wrong claims (mean over tasks), team, slowest-scout, scout and session time, tokens,
/// team and session cost, cost per right file, the caller's load, and the counts.</summary>
public sealed record ScoutArmSummary(
    ScoutArm Arm,
    Figure Recall,
    Figure WrongPerTen,
    ScoutWrongGap WrongGap,
    Figure TeamMs,
    Figure SlowestScoutMs,
    Figure ScoutMs,
    Figure SessionMs,
    ScoutArmTokens Tokens,
    Figure TeamUsd,
    Figure SessionUsd,
    Figure UsdPerRightFile,
    Figure CallerLoad,
    ScoutArmCounts Counts)
{
    public bool WrongMeasured => WrongGap == ScoutWrongGap.None;
}

public sealed record ScoutAnalysisResult(IReadOnlyList<ScoutTaskArm> TaskArms, IReadOnlyList<ScoutArmSummary> Arms, IReadOnlyList<ScoutWorkloadReading> Workloads);

/// <summary>The analysis of §3.6 and §3.8, pure: the latest settled cell of every lineage in, per-task and per-arm figures and the three
/// workloads' paired intervals out. The task is the unit — its runs are averaged first — and only SCORED cells carry numbers: a void,
/// stopped or aborted cell is counted, never measured.</summary>
public static class ScoutAnalysis
{
    /// <summary>"More than 10 % of an arm's cells are silent" (§3.6).</summary>
    public const double SilentShareLimit = 0.10;

    public static ScoutAnalysisResult Of(IReadOnlyList<ScoutCellEvidence> cells, IReadOnlyList<ScoutArmId> arms, BootstrapSettings settings)
    {
        var taskArms = TaskArms(cells, arms);
        var summaries = arms.Select(ScoutArms.Of).Select(arm => Summary(arm, [.. cells.Where(c => c.Cell.Arm == arm.Id)], taskArms)).ToList();

        return new ScoutAnalysisResult(taskArms, summaries, Workloads(taskArms, summaries, settings));
    }

    /// <summary>One row per (task, arm) the run planned, in task order then arm order.</summary>
    public static IReadOnlyList<ScoutTaskArm> TaskArms(IReadOnlyList<ScoutCellEvidence> cells, IReadOnlyList<ScoutArmId> arms) =>
        [.. cells.GroupBy(c => (c.Cell.Task.Value, c.Cell.Arm))
            .Where(g => arms.Contains(g.Key.Arm))
            .OrderBy(g => g.Key.Value, StringComparer.Ordinal).ThenBy(g => arms.ToList().IndexOf(g.Key.Arm))
            .Select(g => TaskArm(g.Key.Value, g.Key.Arm, [.. g]))];

    private static ScoutTaskArm TaskArm(string task, ScoutArmId arm, IReadOnlyList<ScoutCellEvidence> cells)
    {
        var scored = cells.Where(c => c.Scored).ToList();

        return new ScoutTaskArm(
            task, cells[0].Repository, arm, cells.Count, scored.Count,
            Mean(scored.Select(c => c.Recall)),
            ScoutClaimRates.Pooled(scored.Select(c => c.Tally)),
            Mean(scored.Select(c => Count(c.Cell.Facts.TeamMs))),
            Mean(scored.Select(c => Usd(c.Cell.Facts.TeamUsd))));
    }

    private static ScoutArmSummary Summary(ScoutArm arm, IReadOnlyList<ScoutCellEvidence> cells, IReadOnlyList<ScoutTaskArm> taskArms)
    {
        var scored = cells.Where(c => c.Scored).ToList();
        var tasks = taskArms.Where(t => t.Arm == arm.Id).ToList();
        var gap = WrongGap(scored);

        return new ScoutArmSummary(
            arm,
            Mean(tasks.Select(t => t.Recall)),
            gap == ScoutWrongGap.None ? Mean(tasks.Select(t => t.Wrong.Measured ? Figure.Of((double)t.Wrong.PerTen) : Figure.Unknown)) : Figure.Withheld,
            gap,
            Mean(scored.Select(c => Count(c.Cell.Facts.TeamMs))),
            Mean(scored.Select(SlowestScout)),
            Mean(scored.SelectMany(c => c.Scouts).Select(s => Count(s.DurationMs))),
            Mean(scored.Select(c => Count(c.Cell.Facts.SessionMs))),
            Tokens(scored),
            Mean(scored.Select(c => Usd(c.Cell.Facts.TeamUsd))),
            Mean(scored.Select(c => Usd(c.Cell.Facts.ReportedUsd))),
            PerRightFile(scored),
            Mean(scored.Select(c => Figure.Of(c.CallerLoad))),
            Counts(cells, scored));
    }

    /// <summary>§3.6's rule: the wrong-claim result is not measured when more than 10 % of the arm's scored cells are silent, or while any
    /// claim of them is unassessed.</summary>
    public static ScoutWrongGap WrongGap(IReadOnlyList<ScoutCellEvidence> scored) =>
        (scored.Count, scored.Sum(c => c.Unassessed), scored.Count(c => c.Silent)) switch
        {
            (0, _, _) => ScoutWrongGap.NoScoredCell,
            (_, > 0, _) => ScoutWrongGap.ClaimsUnassessed,
            (var n, _, var silent) when (double)silent / n > SilentShareLimit => ScoutWrongGap.SilentCells,
            _ => ScoutWrongGap.None,
        };

    private static ScoutArmCounts Counts(IReadOnlyList<ScoutCellEvidence> cells, IReadOnlyList<ScoutCellEvidence> scored)
    {
        var tally = scored.Aggregate(ClaimTally.Zero, (sum, c) => sum.Plus(c.Tally));

        return new ScoutArmCounts(
            cells.Count, scored.Count,
            cells.Count(c => c.Cell.Outcome == ScoutCellOutcome.Void),
            cells.Count(c => c.Cell.Outcome == ScoutCellOutcome.Stopped),
            cells.Count(c => c.Cell.Outcome == ScoutCellOutcome.Aborted),
            scored.Count(c => c.Silent),
            scored.SelectMany(c => c.Scouts).Count(s => s.Silent),
            scored.SelectMany(c => c.Scouts).Sum(s => s.ClaimsUnparseable),
            tally.Wrong,
            tally.Assessed,
            scored.Sum(c => c.Verdicts.Count(v => v == ScoutClaimVerdict.Unresolved)),
            scored.Sum(c => c.Unassessed),
            scored.Count(TeamTimeBelowSlowestScout));
    }

    /// <summary>The team-time cross-check (§3.6): team time runs from the first Agent call to the last Agent result, so it can never be
    /// shorter than the slowest scout's own <c>totalDurationMs</c> — a cell where it is says the timestamps or the reader are wrong.</summary>
    private static bool TeamTimeBelowSlowestScout(ScoutCellEvidence cell) =>
        cell.Cell.Facts.TeamMs.WasCaptured && SlowestScout(cell) is { Known: true } slowest && cell.Cell.Facts.TeamMs.Value < slowest.Value;

    private static Figure SlowestScout(ScoutCellEvidence cell) =>
        cell.Scouts.Where(s => s.DurationMs.WasCaptured).Select(s => (double)s.DurationMs.Value).DefaultIfEmpty(double.NaN).Max() is var max && !double.IsNaN(max)
            ? Figure.Of(max)
            : Figure.Unknown;

    private static ScoutArmTokens Tokens(IReadOnlyList<ScoutCellEvidence> scored)
    {
        var teams = scored.Where(c => c.Cell.Facts.TeamTokens.WasCaptured).Select(c => c.Cell.Facts.TeamTokens.Value).ToList();
        var scouts = scored.SelectMany(c => c.Scouts).ToList();

        return new ScoutArmTokens(
            Mean(teams.Select(t => Figure.Of(t.Input))),
            Mean(teams.Select(t => Figure.Of(t.Output))),
            Mean(teams.Select(t => Figure.Of(t.CacheRead))),
            Mean(teams.Select(t => Figure.Of(t.CacheWrite))),
            Mean(scouts.Select(s => Figure.Of(s.Tokens.Input))),
            Mean(scouts.Where(s => s.OutputSeparable).Select(s => Figure.Of(s.Tokens.Output))))
        {
            TeamCacheWrite5m = Mean(teams.Select(t => Figure.Of(t.CacheWrite5m))),
            TeamCacheWrite1h = Mean(teams.Select(t => Figure.Of(t.CacheWrite1h))),
        };
    }

    /// <summary>Team cost ÷ files of G found, over the scored cells whose cost and recall were captured; unknown when nothing was found.</summary>
    private static Figure PerRightFile(IReadOnlyList<ScoutCellEvidence> scored)
    {
        var priced = scored.Where(c => c.Cell.Facts.TeamUsd.WasCaptured && c.Cell.Facts.RecallFound.WasCaptured).ToList();
        var found = priced.Sum(c => c.Cell.Facts.RecallFound.Value);

        return found > 0 ? Figure.Of((double)priced.Sum(c => c.Cell.Facts.TeamUsd.Value) / found) : Figure.Unknown;
    }

    /// <summary>The paired readings of W1..W3 — B − A per task, over the tasks where both arms have the value, clustered by repository.</summary>
    private static IReadOnlyList<ScoutWorkloadReading> Workloads(IReadOnlyList<ScoutTaskArm> taskArms, IReadOnlyList<ScoutArmSummary> summaries, BootstrapSettings settings) =>
        [.. Enum.GetValues<ScoutWorkload>()
            .Select(w => (Workload: w, A: summaries.FirstOrDefault(s => IsDeciding(s.Arm, w, ScoutModel.Opus)), B: summaries.FirstOrDefault(s => IsDeciding(s.Arm, w, ScoutModel.Sonnet))))
            .Where(pair => pair.A is not null && pair.B is not null)
            .Select(pair => new ScoutWorkloadReading(
                pair.Workload,
                ScoutBootstrap.Of(Deltas(taskArms, pair.A!.Arm.Id, pair.B!.Arm.Id, t => t.Recall), settings),
                ScoutBootstrap.Of(Deltas(taskArms, pair.A.Arm.Id, pair.B.Arm.Id, t => t.Wrong.Measured ? Figure.Of((double)t.Wrong.PerTen) : Figure.Unknown), settings),
                pair.A.WrongMeasured && pair.B.WrongMeasured))];

    /// <summary>The DECIDING pair of a workload: A (Opus N) against B (Sonnet ⌈1.5·N⌉) — C3 and the Explore replication are reported, not decided.
    /// Public since E2 S8: the sizing simulates exactly the pairs the decision reads.</summary>
    public static bool IsDeciding(ScoutArm arm, ScoutWorkload workload, ScoutModel model) =>
        arm.Workload == workload && arm.Model == model && !arm.IsReplication && arm.Id != ScoutArmId.C3;

    public static IReadOnlyList<TaskDelta> Deltas(IReadOnlyList<ScoutTaskArm> taskArms, ScoutArmId a, ScoutArmId b, Func<ScoutTaskArm, Figure> value) =>
        [.. taskArms.Where(t => t.Arm == a)
            .Join(taskArms.Where(t => t.Arm == b), x => x.Task, y => y.Task, (x, y) => (A: x, B: y), StringComparer.Ordinal)
            .Where(p => value(p.A).Known && value(p.B).Known)
            .Select(p => new TaskDelta(p.A.Task, p.A.Repository, value(p.B).Value - value(p.A).Value))];

    /// <summary>The mean of the known figures, unknown when none is.</summary>
    public static Figure Mean(IEnumerable<Figure> figures)
    {
        var known = figures.Where(f => f.Known).Select(f => f.Value).ToList();

        return known.Count > 0 ? Figure.Of(known.Sum() / known.Count) : Figure.Unknown;
    }

    private static Figure Count(Trace.CapturedCount count) => count.WasCaptured ? Figure.Of(count.Value) : Figure.Unknown;

    private static Figure Usd(Trace.CapturedUsd usd) => usd.WasCaptured ? Figure.Of((double)usd.Value) : Figure.Unknown;
}
