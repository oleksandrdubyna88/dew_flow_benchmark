using Bench.Domain.Trace;

namespace Bench.Domain.Gate;

/// <summary>One row of the per-model table — the operator's columns. Every refusal is a <see cref="Figure"/>
/// state; every count is over ALL runs of the reviewer in the scope, failed ones included.</summary>
public sealed record ModelRow(
    GateReviewerId Reviewer,
    int Runs,
    int ValidRuns,
    int AssessedRuns,
    Figure ValidPct,
    Figure FindingsPerRun,
    Figure SeedsHitMean,
    int SeedsHitMin,
    int SeedsHitMax,
    int DistinctSeeds,
    int DistinctCrossEpicSeeds,
    int Assessed,
    int Supported,
    int Partial,
    int Refuted,
    int Unresolved,
    int AssessmentFailed,
    Figure SupportedPct,
    Figure SupportedOrPartialPct,
    Figure HighValuePerRun,
    Figure OverstatedPct,
    Figure SecondsP50,
    Figure SecondsP90,
    Figure TurnsMean,
    int RepairRuns,
    int RepairCalls,
    Figure ServedMean,
    Figure RefusedMean,
    Figure TokensInPerRun,
    Figure TokensOutPerRun,
    Figure TokensCachedPerRun,
    Figure CachePct,
    Figure TurnOneCachedMean,
    int WarmRuns,
    Figure ReasoningPerRun,
    Figure CostPerRun,
    Figure CostPerSeed,
    Figure CostTotal,
    IReadOnlyList<FailureCount> Failures);

public sealed record FailureCount(FailureKind Kind, int Runs);

public sealed record PerTaskRow(
    GateTaskId Task,
    string Language,
    bool Calibration,
    GateReviewerId Reviewer,
    int Runs,
    int ValidRuns,
    IReadOnlyList<int> Findings,
    IReadOnlyList<Figure> SeedsHit,
    IReadOnlyList<int> Turns,
    IReadOnlyList<double> Seconds,
    IReadOnlyList<Figure> Cost);

public enum VarianceState
{
    Stated,
    Withheld,
    Unassessed,
}

/// <summary>Run-to-run variance for one task × reviewer: the spread of seeds hit and of findings across the
/// repeats. <see cref="VarianceState.Withheld"/> below <see cref="GateReport.MinRepeatsForVariance"/> — the
/// numbers are then zero and MUST NOT be read.</summary>
public sealed record VarianceReading(
    GateTaskId Task,
    GateReviewerId Reviewer,
    int Repeats,
    VarianceState State,
    int SeedsHitMin,
    int SeedsHitMax,
    int FindingsMin,
    int FindingsMax);

public sealed record ModelTable(
    GateScope Scope,
    RubricKind Rubric,
    IReadOnlyList<ModelRow> Rows,
    IReadOnlyList<PerTaskRow> PerTask,
    IReadOnlyList<VarianceReading> Variance);

/// <summary>The gate report — pure, over the facts and the verdicts, with the <see cref="RubricKind"/> as a
/// REQUIRED dimension exactly as <c>--metric</c> has no default: <c>strict-v1</c> and <c>lenient-worth-v1</c>
/// are two populations, and there is no aggregate across kinds anywhere — not here, not in the API, not on
/// the page. Refusals are words: <i>withheld</i>, <i>unassessed</i>, <i>unknown</i>. No reviewer is ever
/// nominated best by score.</summary>
public static class GateReport
{
    /// <summary>Below this many repeats, variance is withheld — a spread of two is a difference, not a spread.</summary>
    public const int MinRepeatsForVariance = 3;

    /// <summary>The scopes the runs span. A scope with two product versions PARTITIONS — cells claimed under
    /// two pins in one campaign land in two partitions, never averaged — the arms page's move.</summary>
    public static IReadOnlyList<GateScope> Scopes(IReadOnlyList<GateRunRecord> runs) =>
        [.. runs.Select(r => r.Scope).Distinct()
            .OrderBy(s => s.SuiteStamp, StringComparer.Ordinal).ThenBy(s => s.Gate)
            .ThenBy(s => s.ProductVersion, StringComparer.Ordinal).ThenBy(s => s.SettingsHash, StringComparer.Ordinal)];

    /// <summary>A supported rate over verdicts of ONE kind. Two kinds in one population are refused by name —
    /// this is the function every rate goes through, so the refusal cannot be bypassed by a caller who forgot.</summary>
    public static Outcome<Figure> SupportedRate(IReadOnlyList<GateVerdict> verdicts)
    {
        var kinds = verdicts.Select(v => v.Rubric.Kind).Distinct().Order().ToList();
        if (kinds.Count > 1)
        {
            return Outcome<Figure>.Failure(
                $"these verdicts span two rubric kinds ({string.Join(", ", kinds)}) — no aggregate across kinds: "
                + "a strict 'supported' and a lenient 'worth having' are two populations, and a mean over both is a number about nothing");
        }

        var counted = verdicts.Where(v => v.Reading.CountsInRates).ToList();

        return Outcome<Figure>.Success(Figure.Percent(counted.Count(v => v.Reading.IsSupported), counted.Count));
    }

    public static ModelTable PerModel(GateScope scope, RubricKind rubric, GateReportInput input)
    {
        var runs = input.Runs.Where(r => r.Scope == scope).ToList();
        var verdicts = input.Verdicts.Where(v => v.Rubric.Kind == rubric).ToList();

        var rows = runs.GroupBy(r => r.Reviewer.Value, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => Row(ReviewerAggregate.Of(g.First().Reviewer, [.. g], verdicts, input.Tasks)))
            .ToList();

        return new ModelTable(scope, rubric, rows, PerTask(scope, rubric, input), Variance(scope, rubric, input));
    }

    public static IReadOnlyList<PerTaskRow> PerTask(GateScope scope, RubricKind rubric, GateReportInput input)
    {
        var verdicts = input.Verdicts.Where(v => v.Rubric.Kind == rubric).ToLookup(v => v.RunId);
        var tasks = input.Tasks.ToDictionary(t => t.Id.Value, StringComparer.Ordinal);

        return [.. input.Runs.Where(r => r.Scope == scope)
            .GroupBy(r => (Task: r.Task.Value, Reviewer: r.Reviewer.Value))
            .OrderBy(g => g.Key.Task, StringComparer.Ordinal).ThenBy(g => g.Key.Reviewer, StringComparer.Ordinal)
            .Select(g => TaskRow([.. g.OrderBy(r => r.Repeat)], tasks.GetValueOrDefault(g.Key.Task), verdicts))];
    }

    public static IReadOnlyList<VarianceReading> Variance(GateScope scope, RubricKind rubric, GateReportInput input)
    {
        var verdicts = input.Verdicts.Where(v => v.Rubric.Kind == rubric).ToLookup(v => v.RunId);

        return [.. input.Runs.Where(r => r.Scope == scope)
            .GroupBy(r => (Task: r.Task.Value, Reviewer: r.Reviewer.Value))
            .OrderBy(g => g.Key.Task, StringComparer.Ordinal).ThenBy(g => g.Key.Reviewer, StringComparer.Ordinal)
            .Select(g => VarianceOf([.. g], verdicts))];
    }

    private static VarianceReading VarianceOf(IReadOnlyList<GateRunRecord> runs, ILookup<Guid, GateVerdict> verdicts)
    {
        var first = runs[0];
        var repeats = runs.Select(r => r.Repeat).Distinct().Count();
        var assessed = runs.Where(r => verdicts[r.RunId].Any()).Select(r => SeedsHit(verdicts[r.RunId])).ToList();
        var findings = runs.Select(r => r.FindingsCount).ToList();

        return (repeats >= MinRepeatsForVariance, assessed.Count) switch
        {
            (false, _) => new VarianceReading(first.Task, first.Reviewer, repeats, VarianceState.Withheld, 0, 0, 0, 0),
            (_, 0) => new VarianceReading(first.Task, first.Reviewer, repeats, VarianceState.Unassessed, 0, 0, findings.Min(), findings.Max()),
            _ => new VarianceReading(first.Task, first.Reviewer, repeats, VarianceState.Stated, assessed.Min(), assessed.Max(), findings.Min(), findings.Max()),
        };
    }

    private static PerTaskRow TaskRow(IReadOnlyList<GateRunRecord> runs, TaskSummary? task, ILookup<Guid, GateVerdict> verdicts) =>
        new(
            runs[0].Task,
            task?.Language ?? string.Empty,
            task?.IsCalibration ?? false,
            runs[0].Reviewer,
            runs.Count,
            runs.Count(r => r.Facts.Valid),
            [.. runs.Select(r => r.FindingsCount)],
            [.. runs.Select(r => SeedsHitFigure(r, task, verdicts))],
            [.. runs.Select(r => r.Facts.Turns)],
            [.. runs.Select(r => r.Facts.SecondsTotal)],
            [.. runs.Select(r => r.Facts.CostUsd.WasCaptured ? Figure.Of((double)r.Facts.CostUsd.Value) : Figure.Unknown)]);

    private static Figure SeedsHitFigure(GateRunRecord run, TaskSummary? task, ILookup<Guid, GateVerdict> verdicts) =>
        (task is { IsSeeded: false }, verdicts[run.RunId].Any()) switch
        {
            (true, _) => Figure.NotApplicable,
            (_, false) => Figure.Unassessed,
            _ => Figure.Of(SeedsHit(verdicts[run.RunId])),
        };

    private static int SeedsHit(IEnumerable<GateVerdict> verdicts) =>
        verdicts.Select(v => v.Reading).OfType<Verdict.Strict>().Select(v => v.SeedHit).OfType<SeedHit.Of>()
            .Select(h => h.Seed.Value).Distinct(StringComparer.Ordinal).Count();

    private static ModelRow Row(ReviewerAggregate a)
    {
        var seeds = a.SeedsHitPerRun;
        var seconds = a.Captured(r => (true, r.Facts.SecondsTotal));
        var costs = a.Captured(r => (r.Facts.CostUsd.WasCaptured, (double)r.Facts.CostUsd.Value));
        var tokensIn = a.Captured(r => Count(r.Facts.TokensIn));
        var tokensCached = a.Captured(r => Count(r.Facts.TokensCached));

        return new ModelRow(
            a.Reviewer,
            a.RunCount,
            a.ValidRuns,
            a.Assessed.Count,
            Figure.Percent(a.ValidRuns, a.RunCount),
            Figure.Mean([.. a.Runs.Select(r => (double)r.FindingsCount)]),
            seeds.Count > 0 ? Figure.Mean([.. seeds.Select(s => (double)s)]) : Figure.Unassessed,
            seeds.Count > 0 ? seeds.Min() : 0,
            seeds.Count > 0 ? seeds.Max() : 0,
            a.DistinctSeeds.Count,
            a.DistinctCrossEpic,
            a.Judged,
            a.Supported,
            a.Partial,
            a.Refuted,
            a.Unresolved,
            a.AssessmentFailed,
            Figure.Percent(a.Supported, a.Judged),
            Figure.Percent(a.Supported + a.Partial, a.Judged),
            a.HighValuePerRun.Count > 0 ? Figure.Mean([.. a.HighValuePerRun.Select(h => (double)h)]) : Figure.Unassessed,
            Figure.Percent(a.Overstated, a.Graded),
            Quantile.Q(seconds, 0.5),
            Quantile.Q(seconds, 0.9),
            Figure.Mean([.. a.Runs.Select(r => (double)r.Facts.Turns)]),
            a.Runs.Count(r => r.Facts.ExtraCalls > 0),
            a.Runs.Sum(r => r.Facts.ExtraCalls),
            Figure.Mean([.. a.Runs.Select(r => (double)r.Facts.Served)], 1),
            Figure.Mean([.. a.Runs.Select(r => (double)r.Facts.Refused)], 1),
            Figure.Mean(tokensIn, 0),
            Figure.Mean(a.Captured(r => Count(r.Facts.TokensOut)), 0),
            Figure.Mean(tokensCached, 0),
            tokensIn.Sum() > 0 ? Figure.Percent((long)tokensCached.Sum(), (long)tokensIn.Sum()) : Figure.Unknown,
            Figure.Mean(a.Captured(TurnOneCached), 0),
            a.Runs.Count(r => r.Facts.IsWarm),
            Figure.Mean(a.Captured(r => Count(r.Facts.TokensReasoning)), 0),
            costs.Count > 0 ? Figure.Of(Math.Round(costs.Average(), 4)) : Figure.Unknown,
            CostPerSeed(costs, a.SeedsFoundTotal),
            costs.Count > 0 ? Figure.Of(Math.Round(costs.Sum(), 4)) : Figure.Unknown,
            [.. a.Runs.Where(r => !r.Facts.Valid).GroupBy(r => r.Facts.Failure.Kind).OrderBy(g => g.Key)
                .Select(g => new FailureCount(g.Key, g.Count()))]);
    }

    private static Figure CostPerSeed(IReadOnlyList<double> costs, int seedsFound) =>
        (costs.Count, seedsFound) switch
        {
            (0, _) => Figure.Unknown,
            (_, 0) => Figure.NotApplicable,
            _ => Figure.Of(Math.Round(costs.Sum() / seedsFound, 4)),
        };

    private static (bool Captured, double Value) Count(CapturedCount count) => (count.WasCaptured, count.Value);

    private static (bool Captured, double Value) TurnOneCached(GateRunRecord run) =>
        run.Facts.CachedPerCall.Count > 0 && run.Facts.CachedPerCall[0].WasCaptured
            ? (true, run.Facts.CachedPerCall[0].Value)
            : (false, 0);
}
