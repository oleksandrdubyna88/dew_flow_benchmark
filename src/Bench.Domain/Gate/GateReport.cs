using Bench.Domain.Trace;

namespace Bench.Domain.Gate;

/// <summary>The gate report — pure, over the facts and the verdicts, with the <see cref="Rubric"/> as a REQUIRED
/// dimension exactly as <c>--metric</c> has no default: <c>strict-v1</c> and <c>lenient-worth-v1</c> are two
/// populations, and there is no aggregate across rubrics anywhere — not here, not in the API, not on the page.
/// Which runs and verdicts a figure is over is <see cref="GatePopulation"/>'s decision, taken once. Refusals are
/// words: <i>withheld</i>, <i>unassessed</i>, <i>unknown</i>. No reviewer is ever nominated best by score.</summary>
public static class GateReport
{
    /// <summary>Below this many readings, a spread is withheld — a spread of two is a difference, not a spread.</summary>
    public const int MinRepeatsForVariance = 3;

    /// <summary>The scopes the runs span. A scope with two products PARTITIONS — two pins in one campaign, or
    /// two builds behind one version text, land in two partitions, never averaged — the arms page's move.</summary>
    public static IReadOnlyList<GateScope> Scopes(IReadOnlyList<GateRunRecord> runs) =>
        [.. runs.Select(r => r.Scope).Distinct()
            .OrderBy(s => s.SuiteStamp, StringComparer.Ordinal).ThenBy(s => s.Gate)
            .ThenBy(s => s.ProductVersion, StringComparer.Ordinal).ThenBy(s => s.ProductSha256, StringComparer.Ordinal)
            .ThenBy(s => s.SettingsHash, StringComparer.Ordinal)];

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

    /// <summary>The per-model table: measured tasks in <see cref="ModelTable.Rows"/>, calibration tasks apart in
    /// <see cref="ModelTable.Calibration"/>.</summary>
    public static ModelTable PerModel(GateScope scope, Rubric rubric, GateReportInput input)
    {
        var population = GatePopulation.Of(scope, rubric, input);
        var calibration = input.Tasks.Where(t => t.IsCalibration).Select(t => t.Id.Value).ToHashSet(StringComparer.Ordinal);

        return new ModelTable(
            scope,
            rubric,
            Rows(population, run => !calibration.Contains(run.Task.Value), input.Tasks),
            Rows(population, run => calibration.Contains(run.Task.Value), input.Tasks),
            PerTask(scope, rubric, input),
            Variance(scope, rubric, input));
    }

    public static IReadOnlyList<PerTaskRow> PerTask(GateScope scope, Rubric rubric, GateReportInput input)
    {
        var population = GatePopulation.Of(scope, rubric, input);
        var tasks = input.Tasks.ToDictionary(t => t.Id.Value, StringComparer.Ordinal);

        return [.. ByTaskAndReviewer(population.Runs)
            .Select(g => TaskRowOf(g.Key.Task, g.Key.Reviewer, [.. g.OrderBy(r => r.Repeat)], tasks.GetValueOrDefault(g.Key.Task.Value), population.Verdicts))];
    }

    public static IReadOnlyList<VarianceReading> Variance(GateScope scope, Rubric rubric, GateReportInput input)
    {
        var population = GatePopulation.Of(scope, rubric, input);

        return [.. ByTaskAndReviewer(population.Runs).Select(g => VarianceOf(g.Key.Task, g.Key.Reviewer, [.. g], population.Verdicts))];
    }

    /// <summary>One task × reviewer's variance. The ids come from the caller, not from <c>runs[0]</c>, so a group
    /// of nothing is an explicit empty state (withheld findings, unassessed seeds) rather than an index error.</summary>
    public static VarianceReading VarianceOf(GateTaskId task, GateReviewerId reviewer, IReadOnlyList<GateRunRecord> runs, IReadOnlyList<GateVerdict> verdicts)
    {
        var byRun = verdicts.ToLookup(v => v.RunId);
        var repeats = runs.Select(r => r.Repeat).Distinct().Count();
        var seeds = runs.Where(r => GatePopulation.IsAssessed(r, [.. byRun[r.RunId]])).Select(r => SeedsHit(byRun[r.RunId])).ToList();
        var findings = repeats >= MinRepeatsForVariance ? runs.Select(r => r.FindingsCount).ToList() : [];
        var seedsState = SpreadState(seeds.Count);
        var stated = seedsState == VarianceState.Stated ? seeds : [];

        return new VarianceReading(
            task, reviewer, repeats, seeds.Count,
            seedsState, MinOf(stated), MaxOf(stated),
            findings.Count > 0 ? VarianceState.Stated : VarianceState.Withheld, MinOf(findings), MaxOf(findings));
    }

    /// <summary>One task × reviewer cell of the per-task table, with the ids from the caller — an empty run list
    /// is a row of nothing, not an index error.</summary>
    public static PerTaskRow TaskRowOf(
        GateTaskId task, GateReviewerId reviewer, IReadOnlyList<GateRunRecord> runs, TaskSummary? summary, IReadOnlyList<GateVerdict> verdicts)
    {
        var byRun = verdicts.ToLookup(v => v.RunId);

        return new PerTaskRow(
            task,
            summary?.Language ?? string.Empty,
            summary?.IsCalibration ?? false,
            reviewer,
            runs.Count,
            runs.Count(r => r.Facts.Valid),
            [.. runs.Select(r => r.FindingsCount)],
            [.. runs.Select(r => SeedsHitFigure(r, summary, [.. byRun[r.RunId]]))],
            [.. runs.Select(r => r.Facts.Turns)],
            [.. runs.Select(r => r.Facts.SecondsTotal)],
            [.. runs.Select(r => r.Facts.CostUsd.WasCaptured ? Figure.Of((double)r.Facts.CostUsd.Value) : Figure.Unknown)]);
    }

    private static IEnumerable<IGrouping<(GateTaskId Task, GateReviewerId Reviewer), GateRunRecord>> ByTaskAndReviewer(IReadOnlyList<GateRunRecord> runs) =>
        runs.GroupBy(r => (r.Task, r.Reviewer))
            .OrderBy(g => g.Key.Task.Value, StringComparer.Ordinal).ThenBy(g => g.Key.Reviewer.Value, StringComparer.Ordinal);

    private static IReadOnlyList<ModelRow> Rows(GatePopulation population, Func<GateRunRecord, bool> include, IReadOnlyList<TaskSummary> tasks)
    {
        var attempts = population.Attempts.Where(include).ToLookup(r => r.Reviewer.Value, StringComparer.Ordinal);

        return [.. population.Runs.Where(include)
            .GroupBy(r => r.Reviewer.Value, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => ReviewerRow.Of(ReviewerAggregate.Of(g.First().Reviewer, [.. g], [.. attempts[g.Key]], population.Verdicts, tasks)))];
    }

    private static VarianceState SpreadState(int readings) => readings switch
    {
        0 => VarianceState.Unassessed,
        < MinRepeatsForVariance => VarianceState.Withheld,
        _ => VarianceState.Stated,
    };

    private static int MinOf(IReadOnlyList<int> values) => values.Count > 0 ? values.Min() : 0;

    private static int MaxOf(IReadOnlyList<int> values) => values.Count > 0 ? values.Max() : 0;

    private static Figure SeedsHitFigure(GateRunRecord run, TaskSummary? task, IReadOnlyList<GateVerdict> verdicts) =>
        (task is { IsSeeded: false }, GatePopulation.IsAssessed(run, verdicts)) switch
        {
            (true, _) => Figure.NotApplicable,
            (_, false) => Figure.Unassessed,
            _ => Figure.Of(SeedsHit(verdicts)),
        };

    internal static int SeedsHit(IEnumerable<GateVerdict> verdicts) =>
        verdicts.Select(v => v.Reading).OfType<Verdict.Strict>().Select(v => v.SeedHit).OfType<SeedHit.Of>()
            .Select(h => h.Seed.Value).Distinct(StringComparer.Ordinal).Count();
}

/// <summary>One per-model row, folded from a <see cref="ReviewerAggregate"/>.</summary>
internal static class ReviewerRow
{
    public static ModelRow Of(ReviewerAggregate a)
    {
        var seeds = a.SeedsHitPerRun;
        var seconds = a.Captured(r => (true, r.Facts.SecondsTotal));
        var costs = a.Captured(r => (r.Facts.CostUsd.WasCaptured, (double)r.Facts.CostUsd.Value));
        var tokensIn = a.Captured(r => Count(r.Facts.TokensIn));
        var tokensCached = a.Captured(r => Count(r.Facts.TokensCached));

        return new ModelRow(
            a.Reviewer,
            a.RunCount,
            a.Attempts.Count,
            a.Attempts.Count(r => !r.Facts.Valid),
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
            a.AssessorFamilyMatched,
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
