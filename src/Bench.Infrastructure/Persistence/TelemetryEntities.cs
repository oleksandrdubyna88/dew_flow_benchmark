using Bench.Domain.Telemetry;

namespace Bench.Infrastructure.Persistence;

/// <summary>One server-observed tool call.
/// <para>
/// Deliberately NOT joined to a run: most of these rows belong to no benchmark at all. Covering real
/// sessions is the whole reason this vantage point exists — the harness only ever sees its own legs —
/// and a foreign key to a run would have made that impossible to store.
/// </para>
/// <para>
/// Captured-or-not travels as a flag beside every value it qualifies, never as a nullable column. A
/// NULL would be read as "no model" by the first consumer that wrote a GROUP BY, which is exactly the
/// reading the flag exists to prevent.
/// </para></summary>
public sealed class ToolTelemetryRow
{
    public Guid Id { get; set; }

    /// <summary>SHA-256 of the canonical record. The idempotency key: ingesting a spool twice, or
    /// resuming one that was interrupted, inserts nothing the second time.</summary>
    public string Fingerprint { get; set; } = string.Empty;

    public DateTimeOffset At { get; set; }

    public string EmitterApp { get; set; } = string.Empty;

    public int EmitterPid { get; set; }

    public string EmitterMachine { get; set; } = string.Empty;

    public bool ClientNameCaptured { get; set; }

    public string ClientName { get; set; } = string.Empty;

    public string ClientNameReason { get; set; } = string.Empty;

    public bool ClientVersionCaptured { get; set; }

    public string ClientVersion { get; set; } = string.Empty;

    public string ClientVersionReason { get; set; } = string.Empty;

    public bool ModelCaptured { get; set; }

    public string Model { get; set; } = string.Empty;

    public string ModelReason { get; set; } = string.Empty;

    public string Transport { get; set; } = string.Empty;

    /// <summary>The aggregation key, stored rather than computed at query time: every report groups by
    /// it, and a key derived in C# cannot be indexed.</summary>
    public string CallerKey { get; set; } = string.Empty;

    public string Tool { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public string ArgumentsJson { get; set; } = string.Empty;

    public int ArgumentsTruncatedBytes { get; set; }

    public ToolOutcome Outcome { get; set; }

    public string Error { get; set; } = string.Empty;

    /// <summary>Always exact, even when the body below was cut. A size is cheap and a truncated size
    /// is a lie about the payload.</summary>
    public int ResponseChars { get; set; }

    public string ResponseBody { get; set; } = string.Empty;

    public int ResponseTruncatedBytes { get; set; }

    public bool TokensCaptured { get; set; }

    public long Tokens { get; set; }

    public string TokensReason { get; set; } = string.Empty;

    /// <summary>Server-side processing only. Not the caller's latency, and never a wait for an
    /// accelerator — that is infrastructure wait, and folding it in here makes a busy card read as a
    /// slow tool.</summary>
    public double ServerMs { get; set; }

    /// <summary>What the CALLER said this call belongs to. Empty and uncaptured for every real session —
    /// see <see cref="Bench.Domain.Telemetry.TelemetryCorrelation"/>. Present, it is what lets server-side
    /// time and tokens be split across a fix task's investigate / fix / verify phases.</summary>
    public bool LegCaptured { get; set; }

    public string Leg { get; set; } = string.Empty;

    public string LegReason { get; set; } = string.Empty;

    public bool PhaseCaptured { get; set; }

    public string Phase { get; set; } = string.Empty;

    public string PhaseReason { get; set; } = string.Empty;
}
