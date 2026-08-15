using Bench.Application;
using Bench.Domain.Telemetry;
using Bench.Domain.Trace;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>Server-observed tool calls, stored so an ingest can be run twice and mean it once.</summary>
public sealed class PostgresTelemetryStore(BenchDbContext db) : ITelemetryStore
{
    public async Task<IngestReport> AppendAsync(
        IReadOnlyList<ToolTelemetry> records, CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            return IngestReport.Empty;
        }

        // Deduplicate WITHIN the batch first: a spool can legitimately contain the same line twice, and
        // the database's unique index would reject the whole SaveChanges rather than the repeat.
        var byFingerprint = records
            .Select(r => (Fingerprint: TelemetryCodec.Fingerprint(r), Record: r))
            .GroupBy(x => x.Fingerprint, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Record, StringComparer.Ordinal);

        var duplicatesInBatch = records.Count - byFingerprint.Count;

        var known = await db.ToolTelemetry.AsNoTracking()
            .Where(t => byFingerprint.Keys.Contains(t.Fingerprint))
            .Select(t => t.Fingerprint)
            .ToListAsync(cancellationToken);

        var fresh = byFingerprint.Where(kv => !known.Contains(kv.Key, StringComparer.Ordinal)).ToList();

        db.ToolTelemetry.AddRange(fresh.Select(kv => ToRow(kv.Key, kv.Value)));
        await db.SaveChangesAsync(cancellationToken);

        // Refused and Retained are the FILE reader's business, not the store's: this port is handed
        // records that already parsed.
        return new IngestReport(fresh.Count, duplicatesInBatch + known.Count, 0, 0, []);
    }

    public async Task<IReadOnlyList<PhaseTelemetryTotals>> ByPhaseAsync(string leg, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(leg))
        {
            return [];
        }

        var groups = await db.ToolTelemetry.AsNoTracking()
            .Where(t => t.LegCaptured && t.Leg == leg)
            .GroupBy(t => t.Phase)
            .Select(g => new PhaseTelemetryTotals(
                g.Key,
                g.Count(),
                g.Count(t => t.Outcome == ToolOutcome.Answered),
                g.Count(t => t.Outcome == ToolOutcome.Refused),
                g.Count(t => t.Outcome == ToolOutcome.Error),
                g.Sum(t => t.ServerMs),
                // Only the calls that reported tokens contribute. A file read has no tokens, and adding
                // its zero into the sum would report a smaller total over a denominator nobody stated.
                g.Sum(t => t.TokensCaptured ? t.Tokens : 0),
                g.Count(t => t.TokensCaptured)))
            .ToListAsync(cancellationToken);

        return [.. groups.OrderBy(g => g.Phase, StringComparer.Ordinal)];
    }

    /// <summary>Per-key totals, computed entirely in the database.
    /// <para>
    /// <b>This was a client-side aggregation and that was a defect</b>, worth recording because the
    /// comment justifying it was confidently wrong. LINQ cannot express a percentile, so the first
    /// version projected each group's durations with <c>g.Select(t =&gt; t.ServerMs).ToList()</c> and
    /// sorted them here — reasoning that the cost was "cheap next to the scan that produced it". The
    /// scan happens in the DATABASE; the transfer does not. Every <c>ServerMs</c> in the largest table
    /// in the system crossed the wire and was held in memory to compute two numbers.
    /// </para>
    /// <para>
    /// So: raw SQL. <c>percentile_disc</c> rather than <c>percentile_cont</c> deliberately — the
    /// discrete form returns a value some call actually took, which is what anyone reading
    /// "p95 = 3.5 s" believes they are looking at, while the continuous form interpolates between two
    /// calls and reports a duration nobody experienced.
    /// </para></summary>
    public async Task<IReadOnlyList<ToolTelemetryTotals>> TotalsAsync(
        DateTimeOffset since, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();

        // The outcome column is stored as its NAME (HasConversion<string>), so these are text
        // comparisons rather than ordinals — which is the point of storing it that way.
        command.CommandText =
            """
            SELECT "Tool", "CallerKey",
                   COUNT(*)                                                    AS calls,
                   COUNT(*) FILTER (WHERE "Outcome" = 'Answered')              AS answered,
                   COUNT(*) FILTER (WHERE "Outcome" = 'Refused')               AS refused,
                   COUNT(*) FILTER (WHERE "Outcome" = 'Error')                 AS errored,
                   percentile_disc(0.50) WITHIN GROUP (ORDER BY "ServerMs")    AS median_ms,
                   percentile_disc(0.95) WITHIN GROUP (ORDER BY "ServerMs")    AS p95_ms
            FROM tool_telemetry
            WHERE "At" >= @since
            GROUP BY "Tool", "CallerKey"
            ORDER BY calls DESC, "Tool", "CallerKey"
            """;

        var window = command.CreateParameter();
        window.ParameterName = "since";
        window.Value = since;
        command.Parameters.Add(window);

        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var totals = new List<ToolTelemetryTotals>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var tool = reader.GetString(0);
            var caller = reader.GetString(1);
            totals.Add(new ToolTelemetryTotals(
                $"{tool}|{caller}",
                tool,
                caller,
                (int)reader.GetInt64(2),
                (int)reader.GetInt64(3),
                (int)reader.GetInt64(4),
                (int)reader.GetInt64(5),
                reader.GetDouble(6),
                reader.GetDouble(7)));
        }

        return totals;
    }

    private static ToolTelemetryRow ToRow(string fingerprint, ToolTelemetry record) => new()
    {
        Id = Guid.CreateVersion7(),
        Fingerprint = fingerprint,
        At = record.At,
        EmitterApp = record.Emitter.App,
        EmitterPid = record.Emitter.Pid,
        EmitterMachine = record.Emitter.Machine,
        ClientNameCaptured = record.Caller.ClientName.WasCaptured,
        ClientName = record.Caller.ClientName.Value,
        ClientNameReason = record.Caller.ClientName.Reason,
        ClientVersionCaptured = record.Caller.ClientVersion.WasCaptured,
        ClientVersion = record.Caller.ClientVersion.Value,
        ClientVersionReason = record.Caller.ClientVersion.Reason,
        ModelCaptured = record.Caller.Model.WasCaptured,
        Model = record.Caller.Model.Value,
        ModelReason = record.Caller.Model.Reason,
        Transport = record.Caller.Transport,
        CallerKey = record.Caller.Key,
        Tool = record.Tool,
        Scope = record.Scope,
        ArgumentsJson = record.ArgumentsJson,
        ArgumentsTruncatedBytes = record.ArgumentsTruncatedBytes,
        Outcome = record.Outcome,
        Error = record.Error,
        ResponseChars = record.ResponseChars,
        ResponseBody = record.ResponseBody,
        ResponseTruncatedBytes = record.ResponseTruncatedBytes,
        TokensCaptured = record.Tokens.WasCaptured,
        Tokens = record.Tokens.Value,
        TokensReason = record.Tokens.Reason,
        ServerMs = record.ServerTime.TotalMilliseconds,
        LegCaptured = record.Correlation.Leg.WasCaptured,
        Leg = record.Correlation.Leg.Value,
        LegReason = record.Correlation.Leg.Reason,
        PhaseCaptured = record.Correlation.Phase.WasCaptured,
        Phase = record.Correlation.Phase.Value,
        PhaseReason = record.Correlation.Phase.Reason,
    };

    /// <summary>Reading a row back. Present so a consumer never has to reconstruct the
    /// captured/not-captured split from columns by hand — the one place that could quietly turn an
    /// unknown into a value.</summary>
    public static ToolTelemetry ToDomain(ToolTelemetryRow row) => new(
        row.At,
        new TelemetryEmitter(row.EmitterApp, row.EmitterPid, row.EmitterMachine),
        new TelemetryCaller(
            new Captured(row.ClientNameCaptured, row.ClientName, row.ClientNameReason),
            new Captured(row.ClientVersionCaptured, row.ClientVersion, row.ClientVersionReason),
            new Captured(row.ModelCaptured, row.Model, row.ModelReason),
            row.Transport),
        row.Tool,
        row.Scope,
        row.ArgumentsJson,
        row.ArgumentsTruncatedBytes,
        row.Outcome,
        row.Error,
        row.ResponseChars,
        row.ResponseBody,
        row.ResponseTruncatedBytes,
        new CapturedCount(row.TokensCaptured, row.Tokens, row.TokensReason),
        TimeSpan.FromMilliseconds(row.ServerMs),
        new TelemetryCorrelation(
            new Captured(row.LegCaptured, row.Leg, row.LegReason),
            new Captured(row.PhaseCaptured, row.Phase, row.PhaseReason)));
}
