using System.Text.Json;
using System.Text.Json.Serialization;
using Bench.Domain;
using Bench.Domain.Telemetry;
using Bench.Domain.Trace;

namespace Bench.Application;

/// <summary>Reads the `telemetry/v0` lines an emitting server writes.
/// <para>
/// The version check is the point. A line stamped with a version this build does not know is REFUSED
/// by name — never read as though it were this one, and never silently skipped. Both alternatives
/// produce the same artefact: an ingest that reports success over data it did not understand.
/// </para>
/// <para>
/// One line is one record, so a malformed line costs that line. A spool is written by a process that
/// can be killed mid-write, and a codec that gives up on the first bad line loses everything after a
/// half-written last one.
/// </para></summary>
public static class TelemetryCodec
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static Outcome<ToolTelemetry> ReadLine(string line)
    {
        var wire = Parse(line);
        if (wire is Outcome<TelemetryWire>.Fail fail)
        {
            return Outcome<ToolTelemetry>.Failure(fail.Reason);
        }

        var record = ((Outcome<TelemetryWire>.Ok)wire).Value;

        return record.Schema != ToolTelemetry.ContractVersion
            ? Outcome<ToolTelemetry>.Failure(
                $"unknown telemetry schema '{record.Schema}' — this build reads {ToolTelemetry.ContractVersion}")
            : Outcome<ToolTelemetry>.Success(ToDomain(record));
    }

    /// <summary>A stable identity for one record, used to make ingest idempotent: re-reading a spool
    /// file, or resuming one that was interrupted, must change nothing. Computed over the CANONICAL
    /// line rather than the raw bytes so re-serialisation cannot invent a second identity for one
    /// call.</summary>
    public static string Fingerprint(ToolTelemetry record) =>
        StableHash.Of(JsonSerializer.Serialize(ToWire(record), Json));

    private static Outcome<TelemetryWire> Parse(string line)
    {
        try
        {
            var wire = JsonSerializer.Deserialize<TelemetryWire>(line, Json);
            return wire is null
                ? Outcome<TelemetryWire>.Failure("empty telemetry record")
                : Outcome<TelemetryWire>.Success(wire);
        }
        catch (JsonException ex)
        {
            return Outcome<TelemetryWire>.Failure($"malformed telemetry line: {ex.Message}");
        }
    }

    /// <summary>Named states on the wire, matched exactly. An unrecognised outcome becomes
    /// <see cref="ToolOutcome.Error"/> and keeps its raw text in the error field via the caller —
    /// guessing "answered" for a word we do not know would invent a success.</summary>
    private static ToolOutcome ToOutcome(string outcome) => outcome switch
    {
        "answered" => ToolOutcome.Answered,
        "refused" => ToolOutcome.Refused,
        _ => ToolOutcome.Error,
    };

    private static string ToWire(ToolOutcome outcome) => outcome switch
    {
        ToolOutcome.Answered => "answered",
        ToolOutcome.Refused => "refused",
        _ => "error",
    };

    private static ToolTelemetry ToDomain(TelemetryWire wire) => new(
        wire.At,
        new TelemetryEmitter(wire.Emitter.App, wire.Emitter.Pid, wire.Emitter.Machine),
        new TelemetryCaller(
            ToDomain(wire.Caller.ClientName),
            ToDomain(wire.Caller.ClientVersion),
            ToDomain(wire.Caller.Model),
            wire.Caller.Transport),
        wire.Tool,
        wire.Scope,
        wire.ArgumentsJson,
        wire.ArgumentsTruncatedBytes,
        ToOutcome(wire.Outcome),
        wire.Error,
        wire.ResponseChars,
        wire.ResponseBody,
        wire.ResponseTruncatedBytes,
        new CapturedCount(wire.Tokens.Captured, wire.Tokens.Value, wire.Tokens.Reason),
        TimeSpan.FromMilliseconds(wire.ServerMs));

    private static Captured ToDomain(CapturedTextWire wire) =>
        new(wire.Captured, wire.Value, wire.Reason);

    private static TelemetryWire ToWire(ToolTelemetry record) => new()
    {
        Schema = ToolTelemetry.ContractVersion,
        At = record.At,
        Emitter = new EmitterWire
        {
            App = record.Emitter.App,
            Pid = record.Emitter.Pid,
            Machine = record.Emitter.Machine,
        },
        Caller = new CallerWire
        {
            ClientName = ToWire(record.Caller.ClientName),
            ClientVersion = ToWire(record.Caller.ClientVersion),
            Model = ToWire(record.Caller.Model),
            Transport = record.Caller.Transport,
        },
        Tool = record.Tool,
        Scope = record.Scope,
        ArgumentsJson = record.ArgumentsJson,
        ArgumentsTruncatedBytes = record.ArgumentsTruncatedBytes,
        Outcome = ToWire(record.Outcome),
        Error = record.Error,
        ResponseChars = record.ResponseChars,
        ResponseBody = record.ResponseBody,
        ResponseTruncatedBytes = record.ResponseTruncatedBytes,
        Tokens = new CapturedNumberWire
        {
            Captured = record.Tokens.WasCaptured,
            Value = record.Tokens.Value,
            Reason = record.Tokens.Reason,
        },
        ServerMs = record.ServerTime.TotalMilliseconds,
    };

    private static CapturedTextWire ToWire(Captured value) =>
        new() { Captured = value.WasCaptured, Value = value.Value, Reason = value.Reason };
}

/// <summary>The wire shape, kept separate from the domain record on purpose: the emitter lives in
/// another repository and its JSON is a CONTRACT, so it must be able to change shape without dragging
/// the domain behind it — and the domain must not grow JSON attributes to satisfy a serializer.</summary>
internal sealed record TelemetryWire
{
    [JsonPropertyName("schema")] public string Schema { get; init; } = string.Empty;

    [JsonPropertyName("at")] public DateTimeOffset At { get; init; }

    [JsonPropertyName("emitter")] public EmitterWire Emitter { get; init; } = new();

    [JsonPropertyName("caller")] public CallerWire Caller { get; init; } = new();

    [JsonPropertyName("tool")] public string Tool { get; init; } = string.Empty;

    [JsonPropertyName("scope")] public string Scope { get; init; } = string.Empty;

    [JsonPropertyName("argumentsJson")] public string ArgumentsJson { get; init; } = string.Empty;

    [JsonPropertyName("argumentsTruncatedBytes")] public int ArgumentsTruncatedBytes { get; init; }

    [JsonPropertyName("outcome")] public string Outcome { get; init; } = string.Empty;

    [JsonPropertyName("error")] public string Error { get; init; } = string.Empty;

    [JsonPropertyName("responseChars")] public int ResponseChars { get; init; }

    [JsonPropertyName("responseBody")] public string ResponseBody { get; init; } = string.Empty;

    [JsonPropertyName("responseTruncatedBytes")] public int ResponseTruncatedBytes { get; init; }

    [JsonPropertyName("tokens")] public CapturedNumberWire Tokens { get; init; } = new();

    [JsonPropertyName("serverMs")] public double ServerMs { get; init; }
}

internal sealed record EmitterWire
{
    [JsonPropertyName("app")] public string App { get; init; } = string.Empty;

    [JsonPropertyName("pid")] public int Pid { get; init; }

    [JsonPropertyName("machine")] public string Machine { get; init; } = string.Empty;
}

internal sealed record CallerWire
{
    [JsonPropertyName("clientName")] public CapturedTextWire ClientName { get; init; } = new();

    [JsonPropertyName("clientVersion")] public CapturedTextWire ClientVersion { get; init; } = new();

    [JsonPropertyName("model")] public CapturedTextWire Model { get; init; } = new();

    [JsonPropertyName("transport")] public string Transport { get; init; } = string.Empty;
}

internal sealed record CapturedTextWire
{
    [JsonPropertyName("captured")] public bool Captured { get; init; }

    [JsonPropertyName("value")] public string Value { get; init; } = string.Empty;

    [JsonPropertyName("reason")] public string Reason { get; init; } = string.Empty;
}

internal sealed record CapturedNumberWire
{
    [JsonPropertyName("captured")] public bool Captured { get; init; }

    [JsonPropertyName("value")] public long Value { get; init; }

    [JsonPropertyName("reason")] public string Reason { get; init; } = string.Empty;
}
