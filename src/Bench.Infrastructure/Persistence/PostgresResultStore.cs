using System.Text.Json;
using Bench.Application;
using Bench.Domain;
using Bench.Domain.Models;
using Bench.Domain.Retrieval;
using Bench.Domain.Runs;
using Bench.Domain.Trace;
using Bench.Domain.Variants;
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
            .Include(r => r.ToolCalls.OrderBy(t => t.Ordinal))
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

    public async Task<IReadOnlyList<MetricByDimension>> AverageByAsync(
        Guid runId,
        ReportDimension dimension,
        string metricName,
        QuestionScope scope,
        CancellationToken cancellationToken)
    {
        var samples = await SampleAsync([runId], metricName, scope, cancellationToken);

        var grouped = Numbers(samples, s => Key(s, dimension))
            .GroupBy(x => x.Key, StringComparer.Ordinal)
            .Select(g => new MetricByDimension(g.Key, g.Average(x => x.Number), g.Count()))
            .OrderBy(x => x.Dimension, StringComparer.Ordinal);

        return [.. grouped];
    }

    /// <summary>Which metric names these runs carry, in name order.
    /// <para>
    /// A DISTINCT over one column — the cheapest question in this port, and the one that keeps a console from
    /// offering a metric nothing measured. A leg whose reading is non-numeric still counts as carrying the
    /// name: what it cannot do is contribute to an average, and that is the aggregate's business rather than
    /// this list's.
    /// </para></summary>
    public async Task<IReadOnlyList<string>> MetricNamesAsync(
        IReadOnlyList<Guid> runIds, CancellationToken cancellationToken)
    {
        if (runIds.Count == 0)
        {
            return [];
        }

        return await db.Results.AsNoTracking()
            .Where(r => runIds.Contains(r.Cell!.RunId))
            .SelectMany(r => r.Metrics.Select(m => m.Name))
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>What each leg cost, from the columns that already carry it.
    /// <para>
    /// Projected, never hydrated: the funnel's own total, the hit count, the DISTINCT files those hits came
    /// from, and the sampler's VRAM reading. A caller wanting these must not pull a campaign's prompts and
    /// answers to fold four numbers — the defect this port has recorded on three other reads.
    /// </para>
    /// <para>
    /// The VRAM figure comes from the stored <c>LoadJson</c> rather than a column, so it is folded here in
    /// memory. Its SAMPLE COUNT travels with it: an accelerator read costs about a second on Windows and
    /// nothing at all off it, so a short leg may catch none, and a peak drawn from one sample is not the same
    /// claim as one drawn from thirty.
    /// </para></summary>
    public async Task<IReadOnlyList<LegVitals>> VitalsAsync(
        IReadOnlyList<Guid> runIds, CancellationToken cancellationToken)
    {
        if (runIds.Count == 0)
        {
            return [];
        }

        var rows = await db.Results.AsNoTracking()
            .Where(r => runIds.Contains(r.Cell!.RunId))
            .Select(r => new
            {
                r.Cell!.RunId,
                RetrievalMs = r.Funnel == null ? 0d : r.Funnel.TotalMs,
                Hits = r.Hits.Count,
                Files = r.Hits.Select(h => h.RelativePath).Distinct().Count(),
                r.LoadJson,
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => Vitals(row.RunId, row.RetrievalMs, row.Hits, row.Files, row.LoadJson))];
    }

    /// <summary>One row's vitals, with the load half read out of its stored JSON.</summary>
    private static LegVitals Vitals(Guid runId, double retrievalMs, int hits, int files, string loadJson)
    {
        var load = LegLoadJson.Read(loadJson);

        return new LegVitals(
            runId,
            retrievalMs,
            load.Window.TotalSeconds,
            hits,
            files,
            load.Vram.Bytes.Sampled ? (long)load.Vram.Bytes.Maximum : 0,
            load.Vram.Bytes.Count);
    }

    /// <summary>Every scored leg of several runs, as bare numbers — the shape an ARM comparison needs.
    /// <para>
    /// Run id and question id and nothing else, because that is all the caller can use: the arm lives on the
    /// RUN and the half is derived from the question by a hash Postgres cannot compute, so the join and the
    /// split both happen above this. A reading with no numeric value is already dropped by the shared rule.
    /// </para></summary>
    public async Task<IReadOnlyList<MetricLeg>> LegsAsync(
        IReadOnlyList<Guid> runIds, string metricName, CancellationToken cancellationToken)
    {
        if (runIds.Count == 0)
        {
            return [];
        }

        var samples = await SampleAsync(runIds, metricName, QuestionScope.All, cancellationToken);

        return
        [
            .. Numbers(samples, sample => (sample.RunId, sample.QuestionId))
                .Select(x => new MetricLeg(x.Key.RunId, x.Key.QuestionId, x.Number)),
        ];
    }

    /// <summary>One column, nothing else. The prompts and answers of a campaign are megabytes and none of
    /// them bears on what the machine was doing.</summary>
    public async Task<IReadOnlyList<LegLoad>> LoadsAsync(Guid runId, CancellationToken cancellationToken)
    {
        var rows = await db.Results.AsNoTracking()
            .Where(r => r.Cell!.RunId == runId)
            .Select(r => r.LoadJson)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(LegLoadJson.Read)];
    }

    public async Task<IReadOnlyList<QuestionPassRate>> PassRateByQuestionAndSubjectAsync(
        Guid runId, string metricName, CancellationToken cancellationToken)
    {
        var samples = await SampleAsync([runId], metricName, QuestionScope.All, cancellationToken);

        // Grouped on a TUPLE, never on a composed string. A question id is data an author wrote, so a
        // separator inside one would silently split a pair — which is the string parsing this schema keeps
        // these axes in their own columns to avoid.
        var grouped = Numbers(samples, s => (s.QuestionId, s.Subject))
            .GroupBy(x => x.Key)
            .Select(g => new QuestionPassRate(g.Key.QuestionId, g.Key.Subject, g.Average(x => x.Number)))
            .OrderBy(x => x.QuestionId, StringComparer.Ordinal)
            .ThenBy(x => x.SubjectModelId, StringComparer.Ordinal);

        return [.. grouped];
    }

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

    /// <summary>Every axis key of one metric across a run — and NOTHING else.
    /// <para>
    /// <b>A projection, not a hydration, and the difference is the whole reason this method exists.</b> It
    /// used to <c>Include</c> the result, its cell and its run and then <c>ToListAsync</c>, which materialises
    /// full <c>ResultRow</c> entities: every <c>Prompt</c>, every <c>Answer</c>, every <c>ThinkingText</c> and
    /// every <c>ResponseMetaJson</c> of the run crossed the wire so that one number could be averaged. That is
    /// verbatim the diagnosis already written twice in this file's own port — on <c>ScoreboardAsync</c> and on
    /// <c>TotalsAsync</c> — and the aggregate standing between them repeated it. Harmless while nothing called
    /// it; a report that dies on the first ten-thousand-cell campaign the moment something did.
    /// </para>
    /// <para>
    /// <b>Why the fold is still here rather than an <c>AVG</c> in SQL.</b> A metric's numeric reading is a
    /// domain decision, not a cast: a boolean reads as 1 or 0, and a value that does not parse is EXCLUDED
    /// rather than counted as zero — because "not a number" and "zero" are different facts and merging them
    /// invents data. A `CAST(... AS double precision)` would throw on the row the rule exists to skip. So the
    /// grouping happens here, over five short columns per leg instead of whole rows, and
    /// <c>StoredMetric.AsNumber</c> stays the one place that decides what a reading is.
    /// </para>
    /// <para>
    /// All four dimension keys travel together rather than one selected in the query. An enum rendered to text
    /// server-side is at the mercy of the provider's translation, and a silent fall back to client evaluation
    /// is the defect <c>ScoreboardAsync</c>'s comment refuses; four short strings cost nothing beside the
    /// prompts this no longer carries.
    /// </para></summary>
    private async Task<IReadOnlyList<MetricSample>> SampleAsync(
        IReadOnlyList<Guid> runIds, string metricName, QuestionScope scope, CancellationToken cancellationToken)
    {
        // A LIST of runs rather than one, because the arm comparison spans them: the compute backend is
        // recorded on the run, so putting two arms beside each other is by construction a query over two
        // runs. WIDENED rather than copied — a second projection would be a second place for the "drop a
        // reading that is not a number" rule to drift away from.
        var query = db.Results.AsNoTracking()
            .Where(r => runIds.Contains(r.Cell!.RunId) && r.Metrics.Any(m => m.Name == metricName));

        // The half arrives as the question ids it holds, because SeedSplit assigns it by a hash Postgres
        // cannot compute. A suite has tens of questions where a run has thousands of legs, so this stays a
        // WHERE over a short list rather than a fold over every leg.
        if (scope is QuestionScope.Some some)
        {
            query = query.Where(r => some.Ids.Contains(r.Cell!.QuestionId));
        }

        return await query
            .SelectMany(r => r.Metrics.Where(m => m.Name == metricName).Select(m => new MetricSample(
                r.Cell!.RunId,
                r.Cell!.Run!.EngineKind,
                r.Cell!.Run!.EngineEndpoint,
                r.Cell!.Run!.EngineVersion,
                r.Cell!.Run!.IndexFingerprint,
                r.Cell!.Run!.EngineBackend,
                r.Cell!.LaneName,
                r.Cell!.SubjectModelId,
                r.Cell!.VariantId,
                r.Cell!.VariantName,
                r.Cell!.Arm,
                r.Cell!.QuestionId,
                m.Kind,
                m.Value,
                m.Name)))
            .ToListAsync(cancellationToken);
    }

    /// <summary>The readings that ARE numbers, keyed by whatever the caller groups on. A sample whose value
    /// has no numeric reading is dropped here — the one place that rule lives, so both aggregates inherit
    /// it.</summary>
    private static IEnumerable<(TKey Key, double Number)> Numbers<TKey>(
        IReadOnlyList<MetricSample> samples, Func<MetricSample, TKey> key) =>
        samples
            .Select(sample => (Key: key(sample), Number: sample.AsMetric().AsNumber()))
            .Where(x => x.Number is Outcome<double>.Ok)
            .Select(x => (x.Key, ((Outcome<double>.Ok)x.Number).Value));

    /// <summary>The dimension's value for one sample. A switch over a closed enum, so a member added without
    /// a key here is a compiler error rather than a silently empty column in a published report.</summary>
    private static string Key(MetricSample sample, ReportDimension dimension) =>
        dimension switch
        {
            // The engine's whole IDENTITY, not its kind. `EngineKind.ToString()` collapses every engine
            // this project can tell apart -- a different endpoint, version, index fingerprint or COMPUTE
            // BACKEND -- into one row labelled "Qln", which is exactly the defect the backend axis exists to
            // end, one level up. Two sidecars are two arms; they must not be one row here either.
            ReportDimension.Engine => string.Join(
                '|',
                sample.Engine,
                sample.EngineEndpoint,
                sample.EngineVersion,
                sample.EngineFingerprint,
                sample.EngineBackend),
            ReportDimension.Lane => sample.Lane,
            ReportDimension.Subject => sample.Subject,
            ReportDimension.Variant => VariantSelectionCodec.Decode(sample.VariantId, sample.VariantName).Canonical,
            ReportDimension.FixArm => sample.Arm.Canonical(),
            _ => throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "no key for this dimension"),
        };

    /// <summary>One leg's axis keys plus one metric's raw reading — the shape the aggregate projects to.
    /// <para>
    /// A row of this is short by construction, which is the point: it is what stands between averaging a
    /// number and reading a campaign's every prompt to do it.
    /// </para></summary>
    private sealed record MetricSample(
        Guid RunId,
        EngineKind Engine,
        string EngineEndpoint,
        string EngineVersion,
        string EngineFingerprint,
        string EngineBackend,
        string Lane,
        string Subject,
        Guid? VariantId,
        string VariantName,
        FixArm Arm,
        string QuestionId,
        MetricKind Kind,
        string Value,
        string Name)
    {
        /// <summary>Enough of a <see cref="StoredMetric"/> to ask it for its number. The fields a reading does
        /// not depend on are left empty rather than fetched — this exists to reuse
        /// <see cref="StoredMetric.AsNumber"/> instead of copying its rules into a second place.</summary>
        public StoredMetric AsMetric() =>
            new(Name, Kind, Value, string.Empty, false, string.Empty, new Dictionary<string, string>());
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
        LoadJson = LegLoadJson.Write(result.Load),
        CreatedAt = result.CreatedAt,
        Metrics = [.. result.Metrics.Select(m => ToRow(result.Id, m))],
        Hits = [.. Hits(result)],
        Funnel = Funnel(result),
        ToolsOffered = result.Calls.Offered,
        ToolCalls = [.. result.Calls.Entries.Select(e => ToRow(result.Id, result.Calls.Source, e))],
    };

    /// <summary>One ledger entry as a row. The source travels from the LEDGER rather than per entry — it is
    /// an invariant of the leg, not a label on a call, and a row-by-row flag would let a future writer blend
    /// the two kinds that must never be averaged together.</summary>
    private static ToolCallRow ToRow(Guid resultId, ToolCallSource source, LedgerEntry entry) => new()
    {
        Id = Guid.CreateVersion7(),
        ResultId = resultId,
        Ordinal = entry.Ordinal,
        Turn = entry.Turn,
        Phase = entry.Phase,
        ToolName = entry.Call.Name,
        ArgumentsJson = entry.Call.ArgumentsJson,
        Refused = entry.Call.Refused,
        Error = entry.Call.Error,
        DurationMs = (long)entry.Call.Duration.TotalMilliseconds,
        Source = source,
    };

    /// <summary>The ledger a stored leg reads back as.
    /// <para>
    /// <c>NotOffered</c> when the lane offered nothing — which is the column's whole reason for existing,
    /// since an empty row set alone cannot tell that apart from a subject that ignored every tool it had.
    /// The source comes off the first row and defaults to observed for a leg with none, because a leg that
    /// called nothing was still one the harness drove.
    /// </para></summary>
    private static ToolLedger Ledger(ResultRow row) =>
        !row.ToolsOffered
            ? ToolLedger.NotOffered
            : new ToolLedger(
                true,
                row.ToolCalls.Count > 0 ? row.ToolCalls[0].Source : ToolCallSource.Observed,
                [.. row.ToolCalls.OrderBy(c => c.Ordinal).Select(Entry)]);

    private static LedgerEntry Entry(ToolCallRow row) => new(
        row.Ordinal,
        row.Turn,
        row.Phase,
        new Bench.Domain.Trace.ToolCall(
            row.ToolName, row.ArgumentsJson, row.Refused, row.Error, TimeSpan.FromMilliseconds(row.DurationMs)));

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
        Load = LegLoadJson.Read(row.LoadJson),
        Retrieval = ToDomain(row.Funnel, row.Hits),
        Calls = Ledger(row),
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
                // The query-time SUBSET is not recoverable from storage: which axes are query-time is the
                // adapter's knowledge, not the row's. Empty rather than a copy of the requested set — empty
                // asserts vacuously (no false block) where a copy would re-create the confusion this split
                // exists to end. Nothing asserts a hydrated leg; the assertion happens live.
                EngineAxes.None,
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
