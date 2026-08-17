using Bench.Domain.Runs;
using Bench.Domain.Trace;

namespace Bench.Domain.Models;

/// <summary>What the runtime said about one answer, beside the answer itself.
/// <para>
/// Stored per result because these are the numbers a published figure gets re-checked against: how many
/// tokens it cost, how long it took, what sampling actually went out, and why the model stopped. Every one
/// of them is unrecoverable after the fact — a re-run is a different call.
/// </para>
/// <para>
/// The captured/uncaptured distinction is carried rather than flattened: a runtime that reports no tokens
/// gives an unknown cost, not a free one, and <see cref="CapturedCount"/> is where that difference lives.
/// </para></summary>
/// <param name="ResponseBytes">Size of the runtime's response payload. Part of the per-cell log volume the
/// benchmark records from what it observes itself, rather than from a daemon log nobody can attribute to a
/// leg.</param>
public sealed record ResponseMeta(
    CapturedCount PromptTokens,
    CapturedCount CompletionTokens,
    TimeSpan Latency,
    SamplingAsSent Sampling,
    StopReason Stop,
    string StopDetail,
    long ResponseBytes)
{
    /// <summary>The state of a leg that never reached a runtime. Not zeroes: nobody counted.</summary>
    public static ResponseMeta None { get; } = new(
        CapturedCount.Unavailable("the leg reached no runtime"),
        CapturedCount.Unavailable("the leg reached no runtime"),
        TimeSpan.Zero,
        SamplingAsSent.NotCaptured("the leg reached no runtime"),
        StopReason.Unknown,
        string.Empty,
        0);

    public static ResponseMeta Of(ModelAnswer answer) => new(
        answer.PromptTokens,
        answer.CompletionTokens,
        answer.Latency,
        answer.Sampling,
        answer.Stop,
        answer.StopDetail,
        answer.ResponseBytes);
}
