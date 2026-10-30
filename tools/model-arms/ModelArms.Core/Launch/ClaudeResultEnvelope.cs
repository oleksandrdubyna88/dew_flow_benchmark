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
    /// <summary>True when the stream's last non-blank line is a result line that could not be read (cut off, or unreadable), so this envelope — an EARLIER result — is not the
    /// stream's last word: a classifier may stand by an earlier limit or error, and must not call an earlier success finished (E2 §8, the E2.S1 fix round).</summary>
    public bool FollowedByUnreadResult { get; init; }

    private static readonly JsonDocumentOptions Tolerant = new() { AllowDuplicateProperties = true, AllowTrailingCommas = true };

    /// <summary>Reads the LAST line of <paramref name="stream"/> that is a JSON object whose <c>type</c> is the string <c>result</c>, wherever the key sits in the line.
    /// A line that is not JSON, is truncated, is an array or is an object of another type is skipped; nothing throws. Failure: no line of the stream is a result object.
    /// <para>
    /// The options are the domain's own for a measured reason: a duplicated key in a real stream must skip nothing and fault nothing. No <c>GetString</c>, <c>GetBoolean</c> or
    /// <c>GetInt64</c> is called before the element's kind was checked — the wrong-kind call throws <see cref="InvalidOperationException"/>, not <see cref="JsonException"/>.
    /// </para></summary>
    public static Outcome<ClaudeResultEnvelope> LastOf(string stream)
    {
        var (document, line) = LastResultOf(stream);
        using (document)
        {
            return document is null
                ? Outcome<ClaudeResultEnvelope>.Failure("no line of the stream is a result object")
                : Outcome<ClaudeResultEnvelope>.Success(Read(document.RootElement, line));
        }
    }

    /// <summary>Walks the stream from its end, so the first result object met is the last one of the stream; the document is the caller's to dispose.</summary>
    private static (JsonDocument? Document, int Line) LastResultOf(string stream)
    {
        var lines = stream.Split('\n');
        for (var number = lines.Length; number >= 1; number--)
        {
            var document = ResultDocument(lines[number - 1]);
            if (document is not null)
            {
                return (document, number);
            }
        }

        return (null, 0);
    }

    private static JsonDocument? ResultDocument(string text)
    {
        var document = Parse(text);
        if (document is not null && IsResult(document.RootElement))
        {
            return document;
        }

        document?.Dispose();
        return null;
    }

    /// <summary>The line as a document, or null for a line that holds no JSON object: blank, not starting with <c>{</c>, truncated or malformed.</summary>
    private static JsonDocument? Parse(string text)
    {
        var line = text.EndsWith('\r') ? text[..^1] : text;
        if (line.AsSpan().TrimStart() is not ['{', ..])
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(line, Tolerant);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsResult(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty("type", out var type)
        && type.ValueKind == JsonValueKind.String
        && type.ValueEquals("result");

    private static ClaudeResultEnvelope Read(JsonElement root, int line) =>
        new(IsTrue(root, "is_error"), Status(root), Text(root, "api_error"), Text(root, "result"), Strings(root, "errors"), Text(root, "subtype"), line);

    private static bool IsTrue(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static CapturedCount Status(JsonElement root)
    {
        if (!root.TryGetProperty("api_error_status", out var status))
        {
            return CapturedCount.Unavailable("no api_error_status");
        }

        return status.ValueKind switch
        {
            JsonValueKind.Null => CapturedCount.Unavailable("api_error_status is null"),
            JsonValueKind.Number => status.TryGetInt64(out var number)
                ? CapturedCount.Number(number)
                : CapturedCount.Unavailable($"api_error_status {status.GetRawText()} is not an integer"),
            JsonValueKind.String => StatusOfString(StringOf(status)),
            var kind => CapturedCount.Unavailable($"api_error_status is a {kind}, not a number"),
        };
    }

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
    /// <see cref="InvalidOperationException"/>; the reader never throws, so that one string is read as its raw JSON text without the quotes — the words in it still count.</summary>
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
