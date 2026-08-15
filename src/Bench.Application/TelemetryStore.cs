using Bench.Domain.Telemetry;

namespace Bench.Application;

/// <param name="Key">The aggregation key — tool, client, model, transport. Never tool alone: a
/// mid-day switch of client or model would otherwise blend two populations into one row.</param>
/// <param name="Tool">The tool this row is about.</param>
/// <param name="Caller">The caller identity component of the key, rendered for reading.</param>
/// <param name="Calls">How many calls contributed.</param>
/// <param name="Answered">How many answered. With <paramref name="Refused"/> and
/// <paramref name="Errored"/> this is the whole outcome split, kept as three numbers rather than a
/// rate, because a rate over four calls and a rate over four thousand read identically.</param>
public readonly record struct ToolTelemetryTotals(
    string Key,
    string Tool,
    string Caller,
    int Calls,
    int Answered,
    int Refused,
    int Errored,
    double MedianServerMs,
    double P95ServerMs);

/// <summary>How many records an ingest actually took.
/// <para>
/// <paramref name="Duplicate"/> is a first-class outcome rather than an error: re-running an ingest
/// over a spool it has already drained must be a no-op, and the number that proves it is this one.
/// <paramref name="Refused"/> counts lines this build would not read — an unknown schema version or a
/// malformed line — and it is deliberately NOT folded into the others, because "we ingested 900 of
/// 1000" is a materially different report from "we ingested 900".
/// </para></summary>
/// <remarks>
/// <b>Do not compare two of these with <c>==</c>.</b> A record struct compares its members with
/// <c>EqualityComparer&lt;T&gt;.Default</c>, which for <see cref="Reasons"/> is REFERENCE equality —
/// two reports with identical contents are unequal, and two empty ones are equal only by the accident
/// that an empty collection expression is a singleton. Compare the counts, and the reasons as a
/// sequence.
/// </remarks>
public readonly record struct IngestReport(int Ingested, int Duplicate, int Refused, IReadOnlyList<string> Reasons)
{
    public static IngestReport Empty => new(0, 0, 0, []);

    public IngestReport Plus(IngestReport other) => new(
        Ingested + other.Ingested,
        Duplicate + other.Duplicate,
        Refused + other.Refused,
        [.. Reasons, .. other.Reasons]);
}

/// <summary>Where server-side tool telemetry lands.
/// <para>
/// Separate from <see cref="IResultStore"/> because it is a different vantage point, not a different
/// table of the same thing: results are what the harness measured about its OWN legs, this is what a
/// server saw of ALL traffic — benchmark runs and real sessions alike. Neither substitutes for the
/// other, and joining them into one port would force every consumer to filter one out.
/// </para></summary>
public interface ITelemetryStore
{
    /// <summary>Appends a batch, skipping records already stored. Returns what it actually took, so a
    /// resumed ingest can report "0 new" rather than claiming success over data it re-read.</summary>
    Task<IngestReport> AppendAsync(IReadOnlyList<ToolTelemetry> records, CancellationToken cancellationToken);

    /// <summary>Per-key totals across everything stored, most-called first.</summary>
    Task<IReadOnlyList<ToolTelemetryTotals>> TotalsAsync(CancellationToken cancellationToken);
}
