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

/// <summary>The same distinction for a COUNT, and the sharper half of it: a zero that means "none" and
/// a zero that means "nobody counted" are opposite readings of one number, and only one of them is
/// safe to average. A tool that embeds or reranks knows its tokens; a file read does not, and the
/// difference must be visible rather than rendered as zero.</summary>
public sealed record CapturedCount(bool WasCaptured, long Value, string Reason)
{
    public static CapturedCount Number(long value) => new(true, value, string.Empty);

    public static CapturedCount Unavailable(string reason) => new(false, 0, reason);
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

/// <summary>One stage of the retrieval funnel: how many candidates went in, how many came out, and what
/// it cost.
/// <para>
/// <see cref="Ms"/> was missing from this contract's first draft and had to be added the moment a real
/// engine emitted a funnel — a stage that reports only counts can say a pool was starved but never that
/// it was slow, and "where did the eight seconds go" is half of what a funnel is for.
/// </para></summary>
public sealed record FunnelStage(string Name, int In, int Out, long Ms)
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
/// <param name="TotalMs">Measured END TO END, independently of the stages — never their sum.</param>
/// <param name="Absent">Contract stages this engine does not perform, by NAME. Not reported as
/// <c>0 in / 0 out</c>, because that reads as "it ran and found nothing", which is a different claim
/// from "it does not exist here".</param>
public sealed record RetrievalFunnel(
    string ContractVersion,
    IReadOnlyList<FunnelStage> Stages,
    long TotalMs,
    IReadOnlyList<string> Absent)
{
    public static RetrievalFunnel None => new(string.Empty, [], 0, []);

    public bool IsPresent => Stages.Count > 0;

    public long AccountedMs => Stages.Sum(s => s.Ms);

    /// <summary>Time inside the call that no stage claimed.
    /// <para>
    /// The single most valuable number here, and the reason <see cref="TotalMs"/> is measured
    /// separately rather than summed. An instrument upstream printed the sum of the stages it knew
    /// about and called that the total: measured components came to ~4.3 s of an ~8 s call, and
    /// roughly half the time belonged to nobody with nothing saying so. A sum cannot show a missing
    /// part; a remainder can, and it points at which direction to look.
    /// </para></summary>
    public long UnattributedMs => Math.Max(0, TotalMs - AccountedMs);
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
