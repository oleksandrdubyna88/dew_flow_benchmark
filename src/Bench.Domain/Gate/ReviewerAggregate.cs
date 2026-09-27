namespace Bench.Domain.Gate;

/// <summary>One reviewer's runs inside one scope, with the verdicts of ONE rubric kind joined on — the
/// intermediate the per-model row is folded from. Pure, and deliberately explicit about what was assessed:
/// a run with no verdict row under this rubric is UNASSESSED, and nothing about it enters a rate.</summary>
internal sealed record ReviewerAggregate(
    GateReviewerId Reviewer,
    IReadOnlyList<GateRunRecord> Runs,
    IReadOnlyList<(GateRunRecord Run, IReadOnlyList<GateVerdict> Verdicts)> Assessed,
    IReadOnlyDictionary<string, bool> CrossEpicBySeed)
{
    public static ReviewerAggregate Of(GateReviewerId reviewer, IReadOnlyList<GateRunRecord> runs, IReadOnlyList<GateVerdict> verdicts, IReadOnlyList<TaskSummary> tasks)
    {
        var byRun = verdicts.ToLookup(v => v.RunId);
        var assessed = runs
            .Select(run => (Run: run, Verdicts: (IReadOnlyList<GateVerdict>)[.. byRun[run.RunId]]))
            .Where(pair => pair.Verdicts.Count > 0)
            .ToList();

        var crossEpic = tasks.SelectMany(t => t.Seeds)
            .GroupBy(s => s.Id.Value, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Any(s => s.CrossEpic), StringComparer.Ordinal);

        return new ReviewerAggregate(reviewer, runs, assessed, crossEpic);
    }

    public int RunCount => Runs.Count;

    public int ValidRuns => Runs.Count(r => r.Facts.Valid);

    /// <summary>Every verdict that COUNTS — assessment failures are not among them.</summary>
    public IReadOnlyList<Verdict> Readings => [.. Assessed.SelectMany(a => a.Verdicts).Select(v => v.Reading).Where(v => v.CountsInRates)];

    public int AssessmentFailed => Assessed.SelectMany(a => a.Verdicts).Count(v => v.Reading is Verdict.AssessmentFailure);

    public int Supported => Readings.Count(v => v.IsSupported || v is Verdict.Lenient { WorthHaving: true });

    public int Partial => Readings.Count(v => v is Verdict.Strict { Reading: StrictReading.Partial });

    public int Refuted => Readings.Count(v => v is Verdict.Strict { Reading: StrictReading.Refuted } or Verdict.Lenient { WorthHaving: false });

    public int Unresolved => Readings.Count(v => v is Verdict.Strict { Reading: StrictReading.Unresolved });

    public int Judged => Readings.Count;

    /// <summary>Supported or partial — the population <c>overstated %</c> is read over.</summary>
    public int Graded => Readings.Count(v => v.IsSupportedOrPartial);

    public int Overstated => Readings.Count(v => v.IsSupportedOrPartial && v.IsOverstated);

    /// <summary>Distinct seeds hit per ASSESSED run.</summary>
    public IReadOnlyList<int> SeedsHitPerRun => [.. Assessed.Select(a => SeedsOf(a.Verdicts).Count)];

    public IReadOnlyList<int> HighValuePerRun => [.. Assessed.Select(a => a.Verdicts.Count(v => v.Reading.IsHighValue))];

    public IReadOnlySet<string> DistinctSeeds =>
        new HashSet<string>(Assessed.SelectMany(a => SeedsOf(a.Verdicts)), StringComparer.Ordinal);

    public int DistinctCrossEpic => DistinctSeeds.Count(seed => CrossEpicBySeed.TryGetValue(seed, out var cross) && cross);

    public int SeedsFoundTotal => SeedsHitPerRun.Sum();

    public IReadOnlyList<double> Captured(Func<GateRunRecord, (bool Captured, double Value)> read) =>
        [.. Runs.Select(read).Where(r => r.Captured).Select(r => r.Value)];

    private static IReadOnlyList<string> SeedsOf(IReadOnlyList<GateVerdict> verdicts) =>
        [.. verdicts.Select(v => v.Reading).OfType<Verdict.Strict>().Select(v => v.SeedHit).OfType<SeedHit.Of>()
            .Select(h => h.Seed.Value).Distinct(StringComparer.Ordinal)];
}
