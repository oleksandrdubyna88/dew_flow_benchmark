using Bench.Contracts;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

/// <summary>The gate report flattened onto the wire — the ONE mapping <c>bench gate report --json</c> and
/// <c>/api/bench/gate/*</c> both answer with, so the two surfaces cannot drift into two truths (the
/// <see cref="RunReportContract"/> precedent). Every refusal stays a STATE: a figure the domain did not know is never
/// flattened into a zero here.</summary>
public static class GateReportContract
{
    public static GateFigureDto Figure(Figure figure) =>
        figure.State switch
        {
            FigureState.Known => GateFigureDto.Of(figure.Value),
            FigureState.Unassessed => GateFigureDto.Unassessed,
            FigureState.Unknown => GateFigureDto.Unknown,
            FigureState.Withheld => GateFigureDto.Withheld,
            FigureState.NotApplicable => GateFigureDto.NotApplicable,
            FigureState.NotHandChecked => GateFigureDto.NotHandChecked,
            _ => throw new InvalidOperationException($"a figure state the wire has no word for: {figure.State}"),
        };

    public static GateModelTableDto Table(ModelTable table, GateScopeDto scope) =>
        new(
            scope,
            table.Rubric.Kind.ToString(),
            table.Rubric.Id.Value,
            [.. table.Rows.Select(Row)],
            [.. table.Calibration.Select(Row)],
            [.. table.PerTask.Select(PerTask)],
            [.. table.Variance.Select(Variance)],
            [.. table.AllTasks.Select(Row)]);

    public static GateModelRowDto Row(ModelRow r) =>
        new(
            r.Reviewer.Value, r.Runs, r.Attempts, r.AttemptsFailed, r.ValidRuns, r.AssessedRuns,
            Figure(r.ValidPct), Figure(r.FindingsPerRun), Figure(r.SeedsHitMean), r.SeedsHitMin, r.SeedsHitMax,
            r.DistinctSeeds, r.DistinctCrossEpicSeeds, r.Assessed, r.Supported, r.Partial, r.Refuted, r.Unresolved,
            r.AssessmentFailed, r.AssessorFamilyMatched, Figure(r.SupportedPct), Figure(r.SupportedOrPartialPct),
            Figure(r.HighValuePerRun), Figure(r.OverstatedPct), Figure(r.SecondsP50), Figure(r.SecondsP90),
            Figure(r.TurnsMean), Figure(r.RepairRuns), Figure(r.RepairCalls), Figure(r.ServedMean), Figure(r.RefusedMean),
            Figure(r.TokensInPerRun), Figure(r.TokensOutPerRun), Figure(r.TokensCachedPerRun), Figure(r.CachePct),
            Figure(r.TurnOneCachedMean), r.WarmRuns, Figure(r.ReasoningPerRun), Figure(r.CostPerRun),
            Figure(r.CostPerSeed), Figure(r.CostTotal),
            [.. r.Failures.Select(f => new GateFailureCountDto(f.Kind.ToString(), f.Runs))]);

    public static GatePerTaskRowDto PerTask(PerTaskRow r) =>
        new(
            r.Task.Value, r.Language, r.Calibration, r.Reviewer.Value, r.Runs, r.ValidRuns,
            r.Findings, [.. r.SeedsHit.Select(Figure)], [.. r.Turns.Select(Figure)], r.Seconds, [.. r.Cost.Select(Figure)]);

    public static GateVarianceDto Variance(VarianceReading v) =>
        new(
            v.Task.Value, v.Reviewer.Value, v.Repeats, v.SeedReadings,
            Word(v.SeedsState), v.SeedsHitMin, v.SeedsHitMax, Word(v.FindingsState), v.FindingsMin, v.FindingsMax);

    /// <summary>A scope as the scope list and every table header show it: its key, its five fields, its runs (one per
    /// cell), where they came from, and the rubrics its verdicts carry — the only ones a rubric control may offer.</summary>
    public static GateScopeDto Scope(GateScope scope, IReadOnlyList<GateRunRecord> records, IReadOnlyList<GateVerdict> verdicts, bool tasksRecorded) =>
        new(
            scope.Id,
            scope.SuiteStamp,
            GateWord.Of(scope.Gate),
            scope.ProductVersion,
            scope.ProductSha256,
            scope.SettingsHash,
            records.Count - GateRunList.Superseded(records).Count,
            [.. records.Select(r => r.Source.Label).Distinct().Order(StringComparer.Ordinal)],
            Rubrics(verdicts),
            tasksRecorded);

    /// <param name="task">The task as the database recorded it — <see cref="TaskOrNot"/> reads a missing one as NOT
    /// RECORDED, never as a measured task with no language.</param>
    public static GateRunSummaryDto Summary(GateRunRecord r, TaskOrNot task, bool superseded) =>
        new(
            r.RunId, GateWord.Of(r.Scope.Gate), r.Task.Value, task.Language, task.Calibration, task.Recorded,
            r.Reviewer.Value, r.Repeat, r.Attempt, superseded, r.Source.Label, r.Facts.Valid, r.Facts.Verdict.ToString(),
            r.FindingsCount,
            r.Facts.TurnFactsCaptured ? GateFigureDto.Of(r.Facts.Turns) : GateFigureDto.Unknown,
            r.Facts.SecondsTotal,
            r.Facts.CostUsd.WasCaptured ? GateFigureDto.Of((double)r.Facts.CostUsd.Value) : GateFigureDto.Unknown,
            r.Facts.Failure.Kind.ToString(), r.Facts.Failure.Text, r.Scope.ProductVersion, r.Scope.ProductSha256);

    public static GateRunDetailDto Detail(
        GateRunSummaryDto summary, GateScopeDto scope, GateRunRecord r, string promptHash, IReadOnlyList<GateVerdict> verdicts) =>
        new(
            summary,
            scope,
            r.Scope.SettingsHash,
            promptHash,
            new GateTokensDto(Count(r.Facts.TokensIn), Count(r.Facts.TokensOut), Count(r.Facts.TokensCached), Count(r.Facts.TokensReasoning), CachePct(r.Facts)),
            [.. r.Findings.Select(f => new GateFindingDto(f.Ordinal, f.Severity.ToString(), f.Category.ToString(), f.IsGating, f.Line, f.TextHash, f.FileHash))],
            [.. verdicts.OrderBy(v => v.FindingOrdinal).ThenBy(v => v.Rubric.Id.Value, StringComparer.Ordinal).Select(VerdictOf)]);

    private static IReadOnlyList<GateRubricDto> Rubrics(IReadOnlyList<GateVerdict> verdicts) =>
        [.. verdicts.GroupBy(v => v.Rubric)
            .Select(g => new GateRubricDto(g.Key.Id.Value, g.Key.Kind.ToString(), g.Key.Hash, g.Key.Stamp, g.Count()))
            .OrderBy(r => r.Id, StringComparer.Ordinal).ThenBy(r => r.Hash, StringComparer.Ordinal)];

    private static GateVerdictDto VerdictOf(GateVerdict v) =>
        v.Reading switch
        {
            Verdict.Strict s => Verdict(v, s.Reading.ToString(), s.Value.ToString(), s.SeverityFair.ToString(), s.Grounded.ToString(), s.ClusterHash, SeedOf(s.SeedHit), string.Empty),
            Verdict.Lenient l => Verdict(v, l.WorthHaving ? "WorthHaving" : "NotWorthHaving", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
            Verdict.AssessmentFailure f => Verdict(v, "AssessmentFailure", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, f.Cause.ToString()),
            _ => throw new InvalidOperationException("unreachable"),
        };

    private static GateVerdictDto Verdict(
        GateVerdict v, string reading, string value, string severityFair, string grounded, string cluster, string seed, string cause) =>
        new(v.FindingOrdinal, v.Rubric.Id.Value, v.Rubric.Kind.ToString(), v.Rubric.Hash, reading, value, severityFair, grounded,
            cluster, seed, v.Assessor.Value, v.AssessorFamilyMatches, cause);

    private static string SeedOf(SeedHit hit) => hit is SeedHit.Of of ? of.Seed.Value : string.Empty;

    private static GateFigureDto Count(Bench.Domain.Trace.CapturedCount count) =>
        count.WasCaptured ? GateFigureDto.Of(count.Value) : GateFigureDto.Unknown;

    private static GateFigureDto CachePct(GateRunFacts facts) =>
        facts.TokensIn.WasCaptured && facts.TokensCached.WasCaptured && facts.TokensIn.Value > 0
            ? Figure(Domain.Gate.Figure.Percent(facts.TokensCached.Value, facts.TokensIn.Value))
            : GateFigureDto.Unknown;

    private static string Word(VarianceState state) => state.ToString().ToLowerInvariant();
}

/// <summary>A run's task as the database recorded it, or <see cref="NotRecorded"/> — an explicit state rather than a
/// null, because a missing task and a task with an empty language are different facts.</summary>
public sealed record TaskOrNot(bool Recorded, string Language, bool Calibration)
{
    public static TaskOrNot NotRecorded { get; } = new(false, string.Empty, false);

    public static TaskOrNot Of(TaskSummary task) => new(true, task.Language, task.IsCalibration);

    public static TaskOrNot From(IReadOnlyDictionary<string, TaskSummary> tasks, GateTaskId id) =>
        tasks.TryGetValue(id.Value, out var task) ? Of(task) : NotRecorded;
}
