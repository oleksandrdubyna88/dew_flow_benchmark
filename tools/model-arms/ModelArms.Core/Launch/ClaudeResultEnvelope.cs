using Bench.Domain;
using Bench.Domain.Trace;

namespace ModelArms.Core.Launch;

/// <summary>The last <c>result</c> line of a <c>claude --output-format stream-json</c> attempt, read as facts (plan C5, D6): whether it is an error, the HTTP status
/// the API gave, the words it says, and where in the stream it sat. It decides nothing — <c>StepClassifier</c> does.
/// <para>
/// <b>It carries no cost.</b> A resumed attempt's result holds the SESSION's cumulative <c>total_cost_usd</c> and <c>modelUsage</c>, never the attempt's own (measured on
/// the part-6 streams, 2026-10): the cost of a step that was resumed is the LAST attempt's number per session, never the sum of its attempts' results. This type does not read
/// the field, so no reader can add two of them by mistake; whoever reads cost later reads it from the last result of the last attempt of each session.
/// </para></summary>
/// <param name="IsError">True only when <c>is_error</c> is the JSON literal <c>true</c>; the string <c>"true"</c>, a number and an absent key are false.</param>
/// <param name="ApiErrorStatus">The HTTP status, captured from an integral number or a string of ASCII digits; every other shape (null, absent, a fraction, text) is
/// <c>Unavailable</c> with the reason — never 0.</param>
/// <param name="ApiError">The <c>api_error</c> string, or <c>""</c>.</param>
/// <param name="Result">The <c>result</c> text, or <c>""</c>.</param>
/// <param name="Errors">The string items of <c>errors</c>; any other item is dropped.</param>
/// <param name="Subtype">The <c>subtype</c> string, kept for the record. It decides nothing: a real 429 prints <c>"subtype":"success"</c> beside <c>"is_error":true</c>.</param>
/// <param name="LineNumber">The 1-based line of the stream the result was read from.</param>
public sealed record ClaudeResultEnvelope(bool IsError, CapturedCount ApiErrorStatus, string ApiError, string Result, IReadOnlyList<string> Errors, string Subtype, int LineNumber)
{
    /// <summary>Reads the LAST line of <paramref name="stream"/> that is a JSON object whose <c>type</c> is the string <c>result</c>, wherever the key sits in the line.
    /// A line that is not JSON, is truncated, is an array or is an object of another type is skipped; nothing throws. Failure: no line of the stream is a result object.</summary>
    public static Outcome<ClaudeResultEnvelope> LastOf(string stream) =>
        Outcome<ClaudeResultEnvelope>.Success(new(false, CapturedCount.Unavailable("stub — not read yet"), string.Empty, string.Empty, [], string.Empty, 0));
}
