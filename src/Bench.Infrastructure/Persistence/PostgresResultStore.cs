using System.Text.Json;
using Bench.Application;
using Bench.Domain;
using Bench.Domain.Models;
using Bench.Domain.Retrieval;
using Bench.Domain.Runs;
using Bench.Domain.Trace;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>Scored legs, stored so the measurement key stays queryable.</summary>
public sealed class PostgresResultStore(BenchDbContext db, TimeProvider clock) : IResultStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    /// <summary>Enum members as their NAMES. An ordinal changes meaning the day somebody inserts a member,
    /// and old results staying readable is the point of this system.</summary>
    private static readonly JsonSerializerOptions Meta = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

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

    /// <summary>Every stored leg of a run, WHOLE — metrics, funnel and hits included.
    /// <para>
    /// The complete read, deliberately: a reading that silently omitted the evidence would hand a caller a
    /// leg claiming it performed no retrieval when it had. The cheap paths exist and are named —
    /// <see cref="ScoreboardAsync"/> for the two integers a summary prints,
    /// <see cref="WithoutMetricAsync"/> for the judge lane — so a caller who wants less can ask for less.
    /// </para></summary>
    public async Task<IReadOnlyList<LegResult>> ForRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        var rows = await db.Results.AsNoTracking()
            .Include(r => r.Metrics)
            .Include(r => r.Funnel)
            .Include(r => r.Hits.OrderBy(h => h.Rank))
            .Where(r => r.Cell!.RunId == runId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(ToDomain)];
    }

    /// <summary>Retention over the largest table in the system.
    /// <para>
    /// Two statements, neither of which hydrates a row: a sum of what is about to go, then an in-database
    /// update that blanks the text and stamps the drop. At the tens of thousands of cells this schema targets
    /// there are hundreds of thousands of hit rows, and a prune that loaded them to edit them would be the
    /// defect <see cref="ScoreboardAsync"/> was written to remove, in a bigger table.
    /// </para>
    /// <para>
    /// The filter is on the hit row's own <c>CreatedAt</c> — copied from the result on write — so this never
    /// joins <c>results</c> to decide what is old.
    /// </para></summary>
    public async Task<SnippetPruning> PruneHitSnippetsAsync(
        DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        var stale = db.RetrievedHits
            .Where(h => h.CreatedAt < olderThan && h.SnippetPrunedAt == default && h.SnippetBytes > 0);

        var bytes = await stale.SumAsync(h => h.SnippetBytes, cancellationToken);

        var pruned = await stale.ExecuteUpdateAsync(
            update => update
                .SetProperty(h => h.Snippet, string.Empty)
                .SetProperty(h => h.SnippetPrunedAt, clock.GetUtcNow()),
            cancellationToken);

        return new SnippetPruning(pruned, bytes);
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
        ThinkingText = result.Thinking.Value,
        // Empty exactly when the text was captured — which is what keeps "this runtime exposes no reasoning"
        // distinguishable from "it reasoned about nothing" after a round trip.
        ThinkingReason = result.Thinking.Reason,
        ResponseMetaJson = ResponseMetaJson.Write(result.Meta),
        CreatedAt = result.CreatedAt,
        Metrics = [.. result.Metrics.Select(m => ToRow(result.Id, m))],
        Hits = [.. Hits(result)],
        Funnel = Funnel(result),
    };

    /// <summary>The funnel row, or none for a leg that performed no retrieval. A degraded funnel is still
    /// written, with its reason: the black-box reading is evidence too, and dropping it makes an engine that
    /// broke its own trace contract look like one that never claimed a contract.</summary>
    private static FunnelRow? Funnel(LegResult result) =>
        result.Retrieval.WasPerformed
            ? new FunnelRow
            {
                Id = Guid.CreateVersion7(),
                ResultId = result.Id,
                ContractVersion = result.Retrieval.Funnel.ContractVersion,
                StagesJson = JsonSerializer.Serialize(result.Retrieval.Funnel.Stages, Meta),
                TotalMs = result.Retrieval.Funnel.TotalMs,
                AbsentJson = JsonSerializer.Serialize(result.Retrieval.Funnel.Absent, Meta),
                Degraded = !result.Retrieval.IsWhiteBox,
                DegradationReason = result.Retrieval.FunnelNote,
                PayloadBytes = result.Retrieval.PayloadBytes,
                ElapsedMs = result.Retrieval.ElapsedMs,
                Collection = result.Retrieval.Collection,
                RequestedAxesJson = JsonSerializer.Serialize(result.Retrieval.Requested.Values, Meta),
                AppliedAxesJson = JsonSerializer.Serialize(result.Retrieval.Applied.Values, Meta),
            }
            : null;

    private static IEnumerable<RetrievedHitRow> Hits(LegResult result) =>
        result.Retrieval.Hits.Select(hit => new RetrievedHitRow
        {
            Id = Guid.CreateVersion7(),
            ResultId = result.Id,
            Rank = hit.Rank,
            RelativePath = hit.RelativePath,
            StartLine = hit.StartLine,
            EndLine = hit.EndLine,
            Member = hit.Member,
            MemberKey = hit.MemberKey,
            Signature = hit.Signature,
            Score = hit.Score,
            Ordering = hit.Ordering,
            ChannelsJson = JsonSerializer.Serialize(hit.Channels, Meta),
            RanksJson = JsonSerializer.Serialize(hit.Ranks, Meta),
            Snippet = hit.Snippet.Value,
            SnippetBytes = hit.Snippet.Bytes,
            // The result's own time, copied so retention can select from this table alone.
            CreatedAt = result.CreatedAt,
        });

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
        row.CreatedAt)
    {
        Thinking = row.ThinkingReason.Length > 0
            ? Captured.Unavailable(row.ThinkingReason)
            : Captured.Text(row.ThinkingText),
        Meta = Application.ResponseMetaJson.Read(row.ResponseMetaJson),
        Retrieval = ToDomain(row.Funnel, row.Hits),
    };

    /// <summary>The retrieval reading of one stored leg. No funnel row means no retrieval was performed —
    /// which is a statement about the ARM, and the reason it is a named state rather than an empty list.</summary>
    private static RetrievedContext ToDomain(FunnelRow? funnel, IReadOnlyList<RetrievedHitRow> hits) =>
        funnel is null
            ? RetrievedContext.NotPerformed
            : RetrievedContext.Of(
                funnel.Collection,
                [.. hits.OrderBy(h => h.Rank).Select(ToDomain)],
                new RetrievalFunnel(
                    funnel.ContractVersion,
                    JsonSerializer.Deserialize<List<FunnelStage>>(funnel.StagesJson, Meta) ?? [],
                    funnel.TotalMs,
                    JsonSerializer.Deserialize<List<string>>(funnel.AbsentJson, Meta) ?? []),
                funnel.DegradationReason,
                new EngineAxes(JsonSerializer.Deserialize<List<Axis>>(funnel.RequestedAxesJson, Meta) ?? []),
                new EngineAxes(JsonSerializer.Deserialize<List<Axis>>(funnel.AppliedAxesJson, Meta) ?? []),
                funnel.PayloadBytes,
                funnel.ElapsedMs);

    private static RetrievedHit ToDomain(RetrievedHitRow row) => new(
        row.Rank,
        row.RelativePath,
        row.StartLine,
        row.EndLine,
        row.Member,
        row.MemberKey,
        row.Signature,
        row.Score,
        row.Ordering,
        JsonSerializer.Deserialize<List<string>>(row.ChannelsJson, Meta) ?? [],
        JsonSerializer.Deserialize<List<int>>(row.RanksJson, Meta) ?? [],
        Snippet(row));

    /// <summary>Which of the three snippet states this row is in. The middle case is the one that matters:
    /// a row whose text retention dropped must never read as an engine that sent none.</summary>
    private static HitSnippet Snippet(RetrievedHitRow row) =>
        (row.SnippetPrunedAt == default, row.SnippetBytes) switch
        {
            (true, > 0) => HitSnippet.Text(row.Snippet),
            (false, _) => HitSnippet.Pruned(row.SnippetBytes, row.SnippetPrunedAt),
            _ => HitSnippet.NotReported("the engine's hit carried an empty text field"),
        };

    private static StoredMetric ToDomain(MetricRow row) => new(
        row.Name,
        row.Kind,
        row.Value,
        row.Reason,
        row.Failed,
        row.Rating,
        JsonSerializer.Deserialize<Dictionary<string, string>>(row.MetadataJson, Json) ?? []);
}
