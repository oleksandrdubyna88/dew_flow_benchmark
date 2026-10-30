using System.Globalization;
using System.Text.Json;
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
/// <param name="IsError">False only when <c>is_error</c> is the JSON literal <c>false</c> or is absent; ANY other value (<c>true</c>, the string <c>"true"</c>, a number, <c>null</c>) reads as an
/// error — the safe side of the incident's direction, which was a false "done" (E2 §8, the E2.S1 fix round; the shell took any truthy value as an error).</param>
/// <param name="ApiErrorStatus">The HTTP status, captured from an integral number or a string of ASCII digits; every other shape (null, absent, a fraction, text) is
/// <c>Unavailable</c> with the reason — never 0.</param>
/// <param name="ApiError">The <c>api_error</c> string, or <c>""</c>.</param>
/// <param name="Result">The <c>result</c> text, or <c>""</c>.</param>
/// <param name="Errors">The string items of <c>errors</c>; any other item is dropped.</param>
/// <param name="Subtype">The <c>subtype</c> string, kept for the record. It decides nothing: a real 429 prints <c>"subtype":"success"</c> beside <c>"is_error":true</c>.</param>
/// <param name="LineNumber">The 1-based line of the stream the result was read from.</param>
public sealed record ClaudeResultEnvelope(bool IsError, CapturedCount ApiErrorStatus, string ApiError, string Result, IReadOnlyList<string> Errors, string Subtype, int LineNumber)
{
    /// <summary>True when the stream's last non-blank line is a result line that could not be read (cut off, or unreadable), so this envelope — an EARLIER result — is not the
    /// stream's last word: a classifier may stand by an earlier limit or error, and must not call an earlier success finished (E2 §8, the E2.S1 fix round).</summary>
    public bool FollowedByUnreadResult { get; init; }

    private static readonly JsonDocumentOptions Tolerant = new() { AllowDuplicateProperties = true, AllowTrailingCommas = true };

    private enum LineKind
    {
        Blank,
        Skipped,
        Unreadable,
        Result,
    }

    /// <summary>Reads the LAST line of <paramref name="stream"/> that is a JSON object whose <c>type</c> is the string <c>result</c>, wherever the key sits in the line.
    /// A line that is not JSON, is truncated, is an array or is an object of another type is skipped; nothing throws. Failure: no line of the stream is a result object.
    /// <para>
    /// The options are the domain's own for a measured reason: a duplicated key in a real stream must skip nothing and fault nothing. A line is examined inside ONE guard that
    /// catches the three exceptions a line can raise — <see cref="JsonException"/> (malformed), <see cref="ArgumentException"/> (a RAW lone surrogate cannot be transcoded) and
    /// <see cref="InvalidOperationException"/> (an escaped lone surrogate in a key or a value, or a wrong-kind <c>Get…</c>) — and the whole reading of the line is inside it; a line
    /// that raises one is skipped. No <c>GetString</c>, <c>GetBoolean</c> or <c>GetInt64</c> is called before the element's kind was checked.
    /// </para>
    /// <para>
    /// A skipped line is not always harmless. When the LAST non-blank line could not be read AND holds the text <c>"type":"result"</c> it was probably a result that was cut off,
    /// and an earlier result is not the stream's last word: the envelope found is returned with <see cref="FollowedByUnreadResult"/> set. A cut before that text cannot be told
    /// from noise and sets nothing.
    /// </para></summary>
    public static Outcome<ClaudeResultEnvelope> LastOf(string stream)
    {
        var lines = stream.Split('\n');
        var cutAtEnd = EndsInUnreadResult(lines);
        for (var number = lines.Length; number >= 1; number--)
        {
            if (Examine(lines[number - 1], number, out var envelope) == LineKind.Result)
            {
                return Outcome<ClaudeResultEnvelope>.Success(envelope! with { FollowedByUnreadResult = cutAtEnd });
            }
        }

        return Outcome<ClaudeResultEnvelope>.Failure("no line of the stream is a result object");
    }

    /// <summary>True when the last non-blank line is unreadable and holds the result marker.</summary>
    private static bool EndsInUnreadResult(string[] lines)
    {
        var last = lines.LastOrDefault(line => !string.IsNullOrWhiteSpace(line));
        return last is not null && HasResultMarker(last) && Examine(last, 0, out _) == LineKind.Unreadable;
    }

    private static bool HasResultMarker(string line) =>
        line.Contains("\"type\":\"result\"", StringComparison.Ordinal) || line.Contains("\"type\": \"result\"", StringComparison.Ordinal);

    private static LineKind Examine(string text, int number, out ClaudeResultEnvelope? envelope)
    {
        envelope = null;
        var line = text.EndsWith('\r') ? text[..^1] : text;
        if (string.IsNullOrWhiteSpace(line))
        {
            return LineKind.Blank;
        }

        return line.AsSpan().TrimStart() is ['{', ..] ? Guarded(line, number, out envelope) : LineKind.Skipped;
    }

    private static LineKind Guarded(string line, int number, out ClaudeResultEnvelope? envelope)
    {
        envelope = null;
        try
        {
            using var document = JsonDocument.Parse(line, Tolerant);
            envelope = IsResult(document.RootElement) ? Read(document.RootElement, number) : null;
            return envelope is null ? LineKind.Skipped : LineKind.Result;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            envelope = null;
            return LineKind.Unreadable;
        }
    }

    private static bool IsResult(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty("type", out var type)
        && type.ValueKind == JsonValueKind.String
        && type.ValueEquals("result");

    private static ClaudeResultEnvelope Read(JsonElement root, int line) =>
        new(ErrorValue(root), Status(root), Text(root, "api_error"), Text(root, "result"), Strings(root, "errors"), Text(root, "subtype"), line);

    private static bool ErrorValue(JsonElement root) =>
        root.TryGetProperty("is_error", out var value) && value.ValueKind != JsonValueKind.False;

    private static CapturedCount Status(JsonElement root) =>
        root.TryGetProperty("api_error_status", out var status) ? StatusOf(status) : CapturedCount.Unavailable("no api_error_status");

    private static CapturedCount StatusOf(JsonElement status) =>
        status.ValueKind switch
        {
            JsonValueKind.Null => CapturedCount.Unavailable("api_error_status is null"),
            JsonValueKind.Number => StatusOfNumber(status),
            JsonValueKind.String => StatusOfString(StringOf(status)),
            var kind => CapturedCount.Unavailable($"api_error_status is a {kind}, not a number"),
        };

    private static CapturedCount StatusOfNumber(JsonElement status) =>
        status.TryGetInt64(out var number)
            ? CapturedCount.Number(number)
            : CapturedCount.Unavailable($"api_error_status {status.GetRawText()} is not an integer");

    private static CapturedCount StatusOfString(string text) =>
        text.Length is > 0 and <= 18 && text.All(char.IsAsciiDigit)
            ? CapturedCount.Number(long.Parse(text, CultureInfo.InvariantCulture))
            : CapturedCount.Unavailable($"api_error_status '{text}' is not a number");

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? StringOf(value) : string.Empty;

    private static IReadOnlyList<string> Strings(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(StringOf)]
            : [];

    /// <summary>The text of an element that IS a string. A JSON string holding a lone surrogate escape (<c>"\ud800"</c>) is well-formed JSON whose <c>GetString</c> throws
    /// <see cref="InvalidOperationException"/>; that one string is read as its raw JSON text without the quotes — the words in it still count — so a result line with such a value is
    /// still a result.</summary>
    private static string StringOf(JsonElement value)
    {
        try
        {
            return value.GetString() ?? string.Empty;
        }
        catch (InvalidOperationException)
        {
            return value.GetRawText()[1..^1];
        }
    }
}
