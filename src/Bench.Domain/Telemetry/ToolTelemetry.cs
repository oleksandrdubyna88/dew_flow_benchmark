using Bench.Domain.Trace;

namespace Bench.Domain.Telemetry;

/// <summary>How a tool call ended, from the server's side.
/// <para>
/// Three states, not two. A refused call and an executed one were once indistinguishable because the
/// ledger recorded a result's LENGTH rather than its outcome, and a read-only guarantee was asserted
/// on that basis for months and was false. A boolean merges "the guard worked" with "the disk broke"
/// permanently.
/// </para></summary>
public enum ToolOutcome
{
    Answered,
    Refused,
    Error,
}

/// <summary>Who made a call, to the limit of what the emitting surface could actually observe.
/// <para>
/// <see cref="Model"/> is <see cref="Captured.Unavailable"/> for every real session, and that is the
/// correct reading rather than a gap to be filled: no MCP revision tells a server which model drives
/// it. It is populated only where the caller genuinely knows — a benchmark leg declaring its own.
/// </para></summary>
public sealed record TelemetryCaller(
    Captured ClientName,
    Captured ClientVersion,
    Captured Model,
    string Transport)
{
    /// <summary>The aggregation key. A mid-day switch of client or model must not blend two
    /// populations into one row — an upstream system shipped daily aggregates without an engine column
    /// and then could not attribute a latency change to the switch that caused it.</summary>
    public string Key =>
        $"{Read(ClientName)}/{Read(Model)}/{(Transport.Length == 0 ? "?" : Transport)}";

    private static string Read(Captured value) => value.WasCaptured ? value.Value : "?";
}

/// <summary>One tool call as the SERVER saw it — the other vantage point from the bench-side trace.
/// <para>
/// The harness only ever sees its own runs; this covers all traffic, benchmark and real sessions
/// alike, and knows what the harness can only infer: the payload actually returned, the project the
/// call was scoped to, and how long the server itself took. Neither substitutes for the other.
/// </para>
/// <para>
/// Payload sizes are always exact; payload BODIES arrive already cut to the emitter's byte budget,
/// with the loss recorded. Retention is therefore a property of the schema rather than of a clean-up
/// job that has to be written later against data somebody already has.
/// </para></summary>
public sealed record ToolTelemetry(
    DateTimeOffset At,
    TelemetryEmitter Emitter,
    TelemetryCaller Caller,
    string Tool,
    string Scope,
    string ArgumentsJson,
    int ArgumentsTruncatedBytes,
    ToolOutcome Outcome,
    string Error,
    int ResponseChars,
    string ResponseBody,
    int ResponseTruncatedBytes,
    CapturedCount Tokens,
    TimeSpan ServerTime)
{
    /// <summary>The contract version this domain understands. Stamped on every record and checked on
    /// the way in: a consumer meeting a version it does not know must refuse that line BY NAME rather
    /// than read it as though it were this one.</summary>
    public const string ContractVersion = "telemetry/v0";
}

/// <summary>Which process emitted a record. One machine runs several of these hosts, and an aggregate
/// that cannot separate them is an aggregate over two populations.</summary>
public sealed record TelemetryEmitter(string App, int Pid, string Machine);
