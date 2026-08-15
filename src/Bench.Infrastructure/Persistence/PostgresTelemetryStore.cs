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

        return new IngestReport(fresh.Count, duplicatesInBatch + known.Count, 0, []);
    }

    public async Task<IReadOnlyList<ToolTelemetryTotals>> TotalsAsync(CancellationToken cancellationToken)
    {
        // Grouped in the database; only the latency percentiles come back per row, because a median is
        // not an aggregate SQL gives us portably and a client-side one over a group's durations is
        // cheap next to the scan that produced it.
        var groups = await db.ToolTelemetry.AsNoTracking()
            .GroupBy(t => new { t.Tool, t.CallerKey })
            .Select(g => new
            {
                g.Key.Tool,
                g.Key.CallerKey,
                Calls = g.Count(),
                Answered = g.Count(t => t.Outcome == ToolOutcome.Answered),
                Refused = g.Count(t => t.Outcome == ToolOutcome.Refused),
                Errored = g.Count(t => t.Outcome == ToolOutcome.Error),
                Durations = g.Select(t => t.ServerMs).ToList(),
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. groups
                .Select(g => new ToolTelemetryTotals(
                    $"{g.Tool}|{g.CallerKey}",
                    g.Tool,
                    g.CallerKey,
                    g.Calls,
                    g.Answered,
                    g.Refused,
                    g.Errored,
                    Percentile(g.Durations, 0.50),
                    Percentile(g.Durations, 0.95)))
                .OrderByDescending(t => t.Calls)
                .ThenBy(t => t.Key, StringComparer.Ordinal),
        ];
    }

    /// <summary>Nearest-rank percentile. Chosen over interpolation because the value it reports is one
    /// a call actually took, which is what anyone reading "p95 = 3.5 s" believes they are looking at.</summary>
    private static double Percentile(List<double> values, double fraction)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        values.Sort();
        var rank = (int)Math.Ceiling(fraction * values.Count) - 1;
        return values[Math.Clamp(rank, 0, values.Count - 1)];
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
        TimeSpan.FromMilliseconds(row.ServerMs));
}
