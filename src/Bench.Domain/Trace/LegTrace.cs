namespace Bench.Domain.Trace;

/// <summary>Text that may or may not have been obtainable.
/// <para>
/// "Not captured" and "empty" are different facts and are stored as different states on purpose. Some
/// runtimes hand back only the SIZE of a tool result rather than its text, and a harness that renders
/// that unknown as an empty string quietly turns a gap in instrumentation into a claim about the model.
/// </para></summary>
public sealed record Captured(bool WasCaptured, string Value, string Reason)
{
    public static Captured Text(string value) => new(true, value, string.Empty);

    public static Captured Unavailable(string reason) => new(false, string.Empty, reason);
}

/// <summary>Where a leg's wall-clock went. Three buckets, not two.
/// <para>
/// The third exists because a busy accelerator otherwise reads as a slow model: time spent waiting for
/// a GPU lease, a queue slot or a cold start is neither the model thinking nor a tool working, and
/// folding it into either makes hardware contention look like a quality difference.
/// </para></summary>
public readonly record struct TimeBuckets(TimeSpan Tools, TimeSpan Thinking, TimeSpan InfrastructureWait)
{
    public TimeSpan Total => Tools + Thinking + InfrastructureWait;
}

public readonly record struct TokenSplit(long Fresh, long CacheRead, long CacheWrite)
{
    public long Total => Fresh + CacheRead + CacheWrite;
}

/// <summary>One tool invocation, with its arguments and how it ENDED.
/// <para>
/// <see cref="Refused"/> is the field a whole read-only guarantee once turned on upstream: a denied
/// tool call and an executed one look identical if the ledger records only the size of the result, and
/// for months a guarantee was asserted on that basis and was false.
/// </para></summary>
public sealed record ToolCall(string Name, string ArgumentsJson, bool Refused, string Error, TimeSpan Duration);

/// <summary>One stage of the retrieval funnel: how many candidates went in, how many came out.</summary>
public sealed record FunnelStage(string Name, int In, int Out)
{
    public int Dropped => Math.Max(0, In - Out);
}

/// <summary>The white-box artefact — the whole reason the trace port exists in two modes.
/// <para>
/// Answering "is this a recall failure or a ranking failure" once required a purpose-built one-off
/// probe, and its answer (the target absent from the entire candidate pool for nine questions out of
/// ten) closed a whole class of proposed fixes at a stroke. Every run should produce that as a
/// by-product instead of as an expedition.
/// </para></summary>
public sealed record RetrievalFunnel(string ContractVersion, IReadOnlyList<FunnelStage> Stages)
{
    public static RetrievalFunnel None => new(string.Empty, []);

    public bool IsPresent => Stages.Count > 0;
}

/// <summary>Everything observed about one leg. The funnel is absent for a black-box engine and the
/// report renders identically either way — empty columns, never zeroes.</summary>
public sealed record LegTrace(
    Captured Prompt,
    Captured Response,
    IReadOnlyList<ToolCall> ToolCalls,
    TimeBuckets Time,
    TokenSplit Tokens,
    decimal CostUsd,
    RetrievalFunnel Funnel)
{
    public static LegTrace Empty => new(
        Captured.Unavailable("not run"),
        Captured.Unavailable("not run"),
        [],
        default,
        default,
        0m,
        RetrievalFunnel.None);

    public bool IsWhiteBox => Funnel.IsPresent;
}

/// <summary>One sample of the machine, taken out of band and joined to a run by its timestamp.</summary>
public readonly record struct HardwareSample(
    DateTimeOffset TakenAt,
    double GpuUtilisationPercent,
    long VramBytesUsed,
    double CpuUtilisationPercent,
    long DiskBytesPerSecond);
