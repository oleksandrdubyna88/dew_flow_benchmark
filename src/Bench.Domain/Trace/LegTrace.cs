using Bench.Domain.Engines;
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
public sealed record ToolCall(string Name, string ArgumentsJson, bool Refused, string Error, TimeSpan Duration)
{
    /// <summary>
    /// One engine answer as a record — the ONE place that decides what each outcome stores.
    ///
    /// <para>There were two, and they had drifted. <c>LegRecorder</c> put a refusal's sentence in
    /// <see cref="Error"/>, which <c>TracePortTests</c> pins by name against the defect it was written for:
    /// "a denied call and an executed one look identical if the ledger records only the size of the result."
    /// <c>ToolLoopRunner</c>, written later and without checking, matched only <c>Failed</c> and left every
    /// refusal's reason on the floor. Both produce the same record; only one of them can be right about what
    /// it holds.</para>
    ///
    /// <para>So: <see cref="Refused"/> says WHICH kind of non-answer it was — the tool understood and said
    /// no, against the tool broke — and <see cref="Error"/> says why, for either. A refusal with no sentence
    /// is barely worth storing, because the whole value of an expected refusal is that a subject could read
    /// it and correct itself: "the path was outside the checkout" and "the argument was the wrong kind" are
    /// the evidence a tool DESCRIPTION is judged on, and an empty string collapses them into each other.</para>
    /// </summary>
    public static ToolCall Of(string name, string argumentsJson, ToolAnswer answer, TimeSpan duration) =>
        new(name, argumentsJson, answer.WasRefused, answer.Match(_ => string.Empty, r => r, f => f), duration);
}

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

    /// <summary>Why the funnel is absent, when an engine CLAIMED one and its payload could not be read.
    /// <para>
    /// Empty for an engine that never claimed a contract — that case is already legible from the
    /// funnel's absence. This field exists for the case that is not: an engine declaring
    /// <c>trace/v0</c> and then sending stages this build does not define renders exactly like a
    /// black-box engine unless the mismatch is carried, and a contract mismatch that looks like a
    /// design decision is a mismatch nobody fixes.
    /// </para>
    /// <para>An <c>init</c> property rather than a positional parameter so that adding it cannot break
    /// a caller that constructs a trace positionally.</para></summary>
    public string FunnelNote { get; init; } = string.Empty;

    public bool IsWhiteBox => Funnel.IsPresent;
}

/// <summary>One cheap reading of the machine — processor and memory, taken out of band and joined to a leg
/// by its timestamp.
/// <para>
/// Separate from <see cref="VramSample"/> because the two cost three orders of magnitude apart, and pretending
/// otherwise is how a sampler becomes the thing it measures. These come from process times and
/// <c>GC.GetGCMemoryInfo</c>: no file, no process launch, microseconds. A VRAM read on Windows costs about a
/// second (measured: <c>research/PLAN_hardware_sampler.md</c> §7.3), so it ticks on its own far slower clock and
/// its summary says how few readings it managed.
/// </para></summary>
public readonly record struct LoadSample(
    DateTimeOffset TakenAt,
    double CpuUtilisationPercent,
    long RamBytesUsed,
    long RamBytesTotal);

/// <summary>One reading of the accelerator's memory. Expensive, and therefore rare.</summary>
/// <param name="UsedBytes">What is on the card — ALL of it, not this process's share. Whose it is cannot be
/// read from here, which is why a summary built from these is <c>Observed</c> unless a lease proves the leg
/// held the accelerator alone.</param>
public readonly record struct VramSample(DateTimeOffset TakenAt, long UsedBytes, long TotalBytes);
