using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Domain;
using ModelArms.Core.Launch;

namespace ModelArms.Core.Steps;

/// <summary>The text of a review step's verdict file, from the LAST result of its final attempt and from nothing else (plan C10, D21, D25; <c>review_json</c>, <c>run-arm.sh:132-140</c>). The runner binds the text to the
/// output the step's <c>verdict</c> names — <c>notes/review.json</c> for the Opus review, <c>notes/hA.json</c> and <c>notes/hB.json</c> for CHH's two Haiku reviews — and this type knows none of those names.
/// <para>
/// <b>When there is an answer.</b> Only when <see cref="StepClassifier"/> calls the last result finished — the same decision the runner took about the attempt, asked once more of the same code, so a verdict file is never
/// written from a result the step did not finish on. For each <see cref="StepEnd"/> kind: <see cref="StepEnd.Finished"/> — the answer below; <see cref="StepEnd.Limited"/> (a captured 429 or 529 whatever <c>is_error</c> says, or an
/// error with limit words), <see cref="StepEnd.Errored"/>, <see cref="StepEnd.Interrupted"/> (no result; or the stream's last line was a result that could not be read, which makes an earlier success untrusted),
/// <see cref="StepEnd.HardDeadline"/> (no result) and <see cref="StepEnd.NotStarted"/> (no stream) — a failure, and no file is written. A limit is never mistaken for a review that found nothing.
/// </para>
/// <para>
/// <b>The answer.</b> The shell's span — from the FIRST <c>{</c> to the LAST <c>}</c> of the text (<c>/\{[\s\S]*\}/</c>, greedy) — byte for byte, when it parses as a JSON object: comments, trailing commas and single quotes are refused, and so is
/// a name defined twice at any depth (a verdict written twice has no single reading). Otherwise <c>{"verdict":"unparsed","raw":&lt;the whole text&gt;}</c>: the shell wrote an invalid span as it was, and a reader that then crashed on it is
/// worse than an honest <c>unparsed</c>. A text with no braces, an empty text, two objects, a fence followed by prose with braces, an unbalanced brace and a span nested deeper than the reader allows are all <c>unparsed</c>; the raw text
/// is never cut. The only change made to it is a lone surrogate: an ESCAPED one (<c>\ud83d</c>) is already U+FFFD when <c>ClaudeResultEnvelope</c> decodes the text (<c>JsonStringText</c>), and a RAW one — only a hand-built envelope has one — is written by the JSON writer as U+FFFD, because JSON text cannot hold one. Nothing in here throws on any text.
/// </para>
/// Pure: it reads its one input and opens nothing. It validates no schema (<c>findings</c>, <c>severity</c>) — the fixer and the analysis read that.</summary>
public static class ReviewAnswer
{
    private const string NoReview = "the review's last result is an error or absent — no review.json is written";

    /// <summary>Comments and trailing commas are refused by the reader's defaults; a duplicated name is refused here.</summary>
    private static readonly JsonDocumentOptions Strict = new() { AllowDuplicateProperties = false };

    /// <summary>The file is read by a person and by a fixer: an apostrophe, an angle bracket and a letter outside ASCII stay as they are.</summary>
    private static readonly JsonSerializerOptions Readable = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The verdict file's text. <paramref name="lastResult"/> is <c>ClaudeResultEnvelope.LastOf</c> of the review's final attempt's stream. Failure: the last result is absent, an error, a limit, or not trusted.</summary>
    public static Outcome<string> Extract(Outcome<ClaudeResultEnvelope> lastResult) =>
        lastResult is Outcome<ClaudeResultEnvelope>.Ok ok && StepClassifier.Classify(lastResult, string.Empty, AttemptFacts.Ran) is StepEnd.Finished
            ? Outcome<string>.Success(AnswerOf(ok.Value.Result))
            : Outcome<string>.Failure(NoReview);

    private static string AnswerOf(string text) => SpanOf(text) is { Length: > 0 } span && IsJsonObject(span) ? span : Unparsed(text);

    /// <summary>From the first <c>{</c> to the last <c>}</c>, both included; <c>""</c> when there is no such pair in that order.</summary>
    private static string SpanOf(string text)
    {
        var first = text.IndexOf('{');
        var last = text.LastIndexOf('}');
        return first >= 0 && last > first ? text[first..(last + 1)] : string.Empty;
    }

    /// <summary>True when the text parses as a JSON object. A malformed text and one nested too deep raise <see cref="JsonException"/>; a RAW lone surrogate cannot be transcoded and raises <see cref="ArgumentException"/>.</summary>
    private static bool IsJsonObject(string span)
    {
        try
        {
            using var document = JsonDocument.Parse(span, Strict);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return false;
        }
    }

    private static string Unparsed(string text) =>
        new JsonObject { ["verdict"] = "unparsed", ["raw"] = text }.ToJsonString(Readable);
}
