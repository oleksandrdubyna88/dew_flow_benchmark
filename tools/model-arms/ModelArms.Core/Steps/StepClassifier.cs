using System.Globalization;
using Bench.Domain;
using Bench.Domain.Trace;
using ModelArms.Core.Launch;

namespace ModelArms.Core.Steps;

/// <summary>The one decision of how an attempt ended (plan C5, D6), in this order, the first match winning: not started; a last result that is a limit (by its captured status whatever
/// <c>is_error</c> says; for an error, by its <c>api_error</c> or its words), an error, or finished (Interrupted when the stream's last line was a cut-off result line); no result and limit words on stderr, the wall, the operator, otherwise interrupted. Structured fields decide before
/// words — the 2026-10-28 incident was a limit the words did not match, and nine steps were marked done. A result that was printed decides even when the wall then killed the tree: a
/// session that printed its own end has ended. Stderr is read ONLY when there is no result.
/// <para>
/// Pure: it reads its three inputs and nothing else, in particular no <c>rate_limit_event</c> line and no field of a line that is not the last result.
/// </para></summary>
public static class StepClassifier
{
    /// <summary>How much of the end of stderr is searched for limit words — the shell's <c>slice(-4000)</c> (<c>run-arm.sh:62</c>).</summary>
    public const int StderrTailCharacters = 4000;

    private const int HeadCharacters = 200;

    private const string NoText = "an error result with no text";

    private const string CutResult = "the last line of the stream is a result line that could not be read — an earlier success is not trusted";

    private const string NoWords = "no result line and no limit — the process ended without saying how";

    /// <summary>The attempt's end. <paramref name="lastResult"/> is <c>ClaudeResultEnvelope.LastOf</c> of the attempt's stream; <paramref name="stderr"/> its stderr text.</summary>
    public static StepEnd Classify(Outcome<ClaudeResultEnvelope> lastResult, string stderr, AttemptFacts attempt) =>
        !attempt.Started ? new StepEnd.NotStarted(attempt.NotStartedReason)
        : lastResult is Outcome<ClaudeResultEnvelope>.Ok ok ? FromResult(ok.Value)
        : WithoutResult(stderr, attempt);

    /// <summary>A captured 429 or 529 is a limit whatever <c>is_error</c> says; an error is a limit by <c>api_error</c> or its words, else errored; a success is finished — unless the
    /// stream's last line was a result line that could not be read, when an earlier success is not trusted (an earlier limit or error stands).</summary>
    private static StepEnd FromResult(ClaudeResultEnvelope envelope) =>
        LimitReason(envelope) is { Length: > 0 } limit ? new StepEnd.Limited(limit)
        : envelope.IsError ? new StepEnd.Errored(Text(envelope))
        : envelope.FollowedByUnreadResult ? new StepEnd.Interrupted(CutResult)
        : new StepEnd.Finished();

    /// <summary>What decided that a result is a limit — the status (any <c>is_error</c>), then, for an error, <c>api_error</c>, then the words — followed by the result's head; <c>""</c> when
    /// none did. A success is never read for words.</summary>
    private static string LimitReason(ClaudeResultEnvelope envelope) =>
        IsLimitStatus(envelope.ApiErrorStatus) ? $"api_error_status {envelope.ApiErrorStatus.Value.ToString(CultureInfo.InvariantCulture)} — {Text(envelope)}"
        : envelope.IsError ? ErrorLimitReason(envelope)
        : string.Empty;

    private static string ErrorLimitReason(ClaudeResultEnvelope envelope)
    {
        var words = WordsIn(envelope);
        return LimitWords.FirstIn(envelope.ApiError).Length > 0 ? $"api_error '{envelope.ApiError}' — {Text(envelope)}"
            : words.Length > 0 ? $"the words '{words}' in the result — {Text(envelope)}"
            : string.Empty;
    }

    private static bool IsLimitStatus(CapturedCount status) => status.WasCaptured && status.Value is 429 or 529;

    /// <summary>The words in the result and in the <c>errors</c> items; "in the result" covers both.</summary>
    private static string WordsIn(ClaudeResultEnvelope envelope) =>
        LimitWords.FirstIn(envelope.Result + "\n" + string.Join("\n", envelope.Errors));

    private static StepEnd WithoutResult(string stderr, AttemptFacts attempt)
    {
        var words = LimitWords.FirstIn(Tail(stderr));
        return words.Length > 0 ? new StepEnd.Limited($"the words '{words}' on stderr")
            : attempt.WallKilled ? new StepEnd.HardDeadline()
            : attempt.StoppedByOperator ? new StepEnd.Interrupted("stopped by the operator")
            : new StepEnd.Interrupted(NoWords);
    }

    private static string Tail(string stderr) =>
        stderr.Length <= StderrTailCharacters ? stderr : stderr[^StderrTailCharacters..];

    private static string Text(ClaudeResultEnvelope envelope) =>
        string.IsNullOrWhiteSpace(envelope.Result) ? NoText : Head(envelope.Result);

    /// <summary>The first 200 characters and an ellipsis, or the text when it is no longer. The cut never lands between the halves of a surrogate pair (it moves back one unit): a lone
    /// surrogate in a reason would make the line writers of E2.S2 throw.</summary>
    private static string Head(string text) => text.Length <= HeadCharacters ? text : text[..CutAt(text)] + "…";

    private static int CutAt(string text) => char.IsHighSurrogate(text[HeadCharacters - 1]) ? HeadCharacters - 1 : HeadCharacters;
}
