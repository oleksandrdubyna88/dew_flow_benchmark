using System.Text.Json;
using Bench.Application;
using Bench.Domain;
using Bench.Domain.Runs;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>Scored legs, stored so the measurement key stays queryable.</summary>
public sealed class PostgresResultStore(BenchDbContext db) : IResultStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public async Task<Outcome<LegResult>> SaveAsync(LegResult result, CancellationToken cancellationToken)
    {
        if (!await db.Cells.AnyAsync(c => c.Id == result.CellId, cancellationToken))
        {
            return Outcome<LegResult>.Failure($"no cell {result.CellId} — a result without a leg has no measurement key");
        }

        if (await db.Results.AnyAsync(r => r.CellId == result.CellId, cancellationToken))
        {
            return Outcome<LegResult>.Failure(
                $"cell {result.CellId} already has a result — a leg is scored once, and a second write is a bug rather than a revision");
        }

        db.Results.Add(ToRow(result));
        await db.SaveChangesAsync(cancellationToken);

        return Outcome<LegResult>.Success(result);
    }

    public Task<bool> HasResultAsync(Guid cellId, CancellationToken cancellationToken) =>
        db.Results.AsNoTracking().AnyAsync(r => r.CellId == cellId, cancellationToken);

    public async Task<IReadOnlyList<LegResult>> ForRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        var rows = await db.Results.AsNoTracking()
            .Include(r => r.Metrics)
            .Where(r => r.Cell!.RunId == runId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(ToDomain)];
    }

    /// <summary>Two counts, both computed in the database.
    /// <para>
    /// The passed predicate mirrors <c>LegResult.Passed</c> exactly — metrics present, none of them
    /// failed — as an EXISTS pair rather than a fold over hydrated rows. Two round trips instead of one
    /// grouped query, deliberately: a single <c>COUNT(*) FILTER</c> over a correlated collection is at the
    /// mercy of the provider's translation, and a summary that silently falls back to client evaluation is
    /// the very defect this replaced.
    /// </para></summary>
    public async Task<RunScoreboard> ScoreboardAsync(Guid runId, CancellationToken cancellationToken)
    {
        var scored = await db.Results.AsNoTracking()
            .CountAsync(r => r.Cell!.RunId == runId, cancellationToken);

        var passed = await db.Results.AsNoTracking()
            .CountAsync(
                r => r.Cell!.RunId == runId && r.Metrics.Count > 0 && !r.Metrics.Any(m => m.Failed),
                cancellationToken);

        return new RunScoreboard(scored, passed);
    }

    public Task<IReadOnlyList<MetricByDimension>> AverageByEngineAsync(
        Guid runId, string metricName, CancellationToken cancellationToken) =>
        AverageAsync(runId, metricName, r => r.Cell!.Run!.EngineKind.ToString(), cancellationToken);

    public Task<IReadOnlyList<MetricByDimension>> AverageByLaneAsync(
        Guid runId, string metricName, CancellationToken cancellationToken) =>
        AverageAsync(runId, metricName, r => r.Cell!.LaneName, cancellationToken);

    /// <summary>Legs of this run that no arbiter of this name has read yet.
    /// <para>
    /// A NOT-EXISTS against the metric name, which is what makes the whole judge lane restartable: it is
    /// the same query whether nothing has been judged, half has, or a second arbiter is starting from
    /// scratch, and it cannot produce a duplicate because the row it would duplicate is the row that
    /// excludes it.
    /// </para></summary>
    public async Task<IReadOnlyList<JudgeableLeg>> WithoutMetricAsync(
        Guid runId, string metricName, CancellationToken cancellationToken)
    {
        var rows = await db.Results.AsNoTracking()
            .Include(r => r.Cell!)
            .Where(r => r.Cell!.RunId == runId && !r.Metrics.Any(m => m.Name == metricName))
            .OrderBy(r => r.CreatedAt)
            .Select(r => new JudgeableLeg(r.Id, r.Cell!.QuestionId, r.Cell!.SubjectModelId, r.Prompt, r.Answer))
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<Outcome<int>> AppendMetricsAsync(
        Guid resultId, IReadOnlyList<StoredMetric> metrics, CancellationToken cancellationToken)
    {
        if (!await db.Results.AnyAsync(r => r.Id == resultId, cancellationToken))
        {
            return Outcome<int>.Failure($"no result {resultId} — a verdict about nothing is not evidence");
        }

        db.Metrics.AddRange(metrics.Select(m => ToRow(resultId, m)));
        await db.SaveChangesAsync(cancellationToken);

        return Outcome<int>.Success(metrics.Count);
    }

    /// <summary>One metric across a run, grouped by a dimension of the key.
    /// <para>
    /// Booleans are read as 1 and 0 so a pass rate and a numeric score aggregate the same way; a metric
    /// with no numeric reading at all is excluded rather than counted as zero, because "not a number" and
    /// "zero" are different facts and folding them together invents data.
    /// </para></summary>
    private async Task<IReadOnlyList<MetricByDimension>> AverageAsync(
        Guid runId,
        string metricName,
        Func<ResultRow, string> dimension,
        CancellationToken cancellationToken)
    {
        var rows = await db.Results.AsNoTracking()
            .Include(r => r.Metrics.Where(m => m.Name == metricName))
            .Include(r => r.Cell!).ThenInclude(c => c.Run!)
            .Where(r => r.Cell!.RunId == runId && r.Metrics.Any(m => m.Name == metricName))
            .ToListAsync(cancellationToken);

        var grouped = rows
            .Select(row => (Dimension: dimension(row), Number: ToDomain(row.Metrics[0]).AsNumber()))
            .Where(x => x.Number is Outcome<double>.Ok)
            .GroupBy(x => x.Dimension, StringComparer.Ordinal)
            .Select(g => new MetricByDimension(
                g.Key,
                g.Average(x => ((Outcome<double>.Ok)x.Number).Value),
                g.Count()))
            .OrderBy(x => x.Dimension, StringComparer.Ordinal);

        return [.. grouped];
    }

    private static ResultRow ToRow(LegResult result) => new()
    {
        Id = result.Id,
        CellId = result.CellId,
        Prompt = result.Prompt,
        Answer = result.Answer,
        CreatedAt = result.CreatedAt,
        Metrics = [.. result.Metrics.Select(m => ToRow(result.Id, m))],
    };

    private static MetricRow ToRow(Guid resultId, StoredMetric metric) => new()
    {
        Id = Guid.CreateVersion7(),
        ResultId = resultId,
        Name = metric.Name,
        Kind = metric.Kind,
        Value = metric.Value,
        Reason = metric.Reason,
        Failed = metric.Failed,
        Rating = metric.Rating,
        MetadataJson = JsonSerializer.Serialize(metric.Metadata, Json),
    };

    private static LegResult ToDomain(ResultRow row) => new(
        row.Id,
        row.CellId,
        row.Prompt,
        row.Answer,
        [.. row.Metrics.Select(ToDomain)],
        row.CreatedAt);

    private static StoredMetric ToDomain(MetricRow row) => new(
        row.Name,
        row.Kind,
        row.Value,
        row.Reason,
        row.Failed,
        row.Rating,
        JsonSerializer.Deserialize<Dictionary<string, string>>(row.MetadataJson, Json) ?? []);
}
