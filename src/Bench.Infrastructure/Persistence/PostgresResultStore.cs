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

    public Task<IReadOnlyList<MetricByDimension>> AverageByEngineAsync(
        Guid runId, string metricName, CancellationToken cancellationToken) =>
        AverageAsync(runId, metricName, r => r.Cell!.Run!.EngineKind.ToString(), cancellationToken);

    public Task<IReadOnlyList<MetricByDimension>> AverageByLaneAsync(
        Guid runId, string metricName, CancellationToken cancellationToken) =>
        AverageAsync(runId, metricName, r => r.Cell!.Leg, cancellationToken);

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
        Metrics = [.. result.Metrics.Select(m => new MetricRow
        {
            Id = Guid.CreateVersion7(),
            ResultId = result.Id,
            Name = m.Name,
            Kind = m.Kind,
            Value = m.Value,
            Reason = m.Reason,
            Failed = m.Failed,
            Rating = m.Rating,
            MetadataJson = JsonSerializer.Serialize(m.Metadata, Json),
        })],
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
