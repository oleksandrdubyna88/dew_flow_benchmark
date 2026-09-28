using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bench.Domain.Trace;

namespace Bench.Domain.Gate;

/// <summary>A calibration line's facts, field for field — the other harness's <c>summarise</c> read back into
/// <see cref="GateRunFacts"/>, with every "nobody counted" made a state instead of a zero:
/// <list type="bullet">
/// <item>the ledger sums (<c>tokens_in</c>, <c>tokens_out</c>, <c>tokens_cached</c>) are <i>not captured</i> when the line
/// has NO ledger turn — Python's <c>sum([])</c> is <c>0</c>, and a zero over nothing reads as "none";</item>
/// <item><c>tokens_reasoning</c>, <c>cost_usd</c> and each <c>cached_per_call</c> entry are <i>not captured</i> when
/// <c>null</c>;</item>
/// <item><c>findings</c> is <i>not captured</i> when <c>null</c> (a reply with no findings LIST);</item>
/// <item>served / refused follow the other harness's REPORT (<c>served_count or note.count("served ")</c>), because the
/// report is the table these numbers are held against;</item>
/// <item>the failure sentence is redacted (<see cref="FailureRedaction"/>) and its KIND read off its first reason.</item>
/// </list></summary>
public static partial class CalibFacts
{
    private const string NoTurn = "the other harness's ledger held no turn";

    public static GateRunFacts Of(JsonObject o, PrivateNames privateNames)
    {
        var turns = JsonRead.Int(o, "turns");
        var valid = JsonRead.Flag(o, "valid");

        return new GateRunFacts(
            valid,
            GateVerdictWords.Parse(JsonRead.Text(o, "verdict")),
            ReplyParsed: o["verdict"] is JsonValue,
            JsonRead.Count(o, "findings", "the reply carried no findings list"),
            turns,
            JsonRead.Int(o, "http_calls"),
            [.. JsonRead.List(o, "finish_reasons").Select(n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : string.Empty)],
            [.. JsonRead.List(o, "statuses").Select(n => (int)(JsonRead.Long(n) ?? 0))],
            LedgerSum(o, "tokens_in", turns),
            LedgerSum(o, "tokens_out", turns),
            LedgerSum(o, "tokens_cached", turns),
            JsonRead.Count(o, "tokens_reasoning", "no call reported reasoning tokens"),
            JsonRead.Double(o["seconds_total"]),
            ReviewSeconds(o),
            [.. JsonRead.List(o, "seconds_per_turn").Select(JsonRead.Double)],
            [.. JsonRead.List(o, "cached_per_call").Select(n => JsonRead.Long(n) is { } c ? CapturedCount.Number(c) : CapturedCount.Unavailable("the call reported no cached count"))],
            Cost(o),
            Counted(o, "served_count", "served "),
            Counted(o, "refused_count", "refused "),
            valid ? FailureCause.None : Failure(JsonRead.Text(o, "failure"), privateNames));
    }

    /// <summary>The kind of a calibration failure, read off its FIRST reason — <c>failure_cause</c> put the reasons in the
    /// order it found them, joined by <c>" | "</c>, and the page groups by the first, as <see cref="FailureCauses"/> does.</summary>
    public static FailureKind KindOf(string failure)
    {
        var first = failure.Split(" | ")[0];

        return first switch
        {
            "" => FailureKind.Unexplained,
            _ when first.StartsWith("tool answered non-JSON", StringComparison.Ordinal) => FailureKind.NonJsonReply,
            _ when HttpReason.IsMatch(first) => FailureKind.HttpError,
            _ when first.Contains("finish_reason=length", StringComparison.Ordinal) => FailureKind.LengthCut,
            _ => KindOfTurn(first),
        };
    }

    [GeneratedRegex(@"^call \d+: HTTP ")]
    private static partial Regex HttpReason { get; }

    private static FailureKind KindOfTurn(string first) => first switch
    {
        _ when first.Contains("empty content", StringComparison.Ordinal) => FailureKind.EmptyContent,
        _ when first.StartsWith("verdict ", StringComparison.Ordinal) => FailureKind.VerdictNotPassing,
        _ when first.Contains("FAILED", StringComparison.Ordinal) || first.StartsWith("no vendor answer", StringComparison.Ordinal) => FailureKind.TurnFailed,
        _ => FailureKind.Unexplained,
    };

    private static FailureCause Failure(string text, PrivateNames privateNames) =>
        new(KindOf(text), FailureRedaction.Redact(text.Length > 0 ? text : "the other harness recorded the run invalid and named no cause", privateNames));

    private static CapturedCount LedgerSum(JsonObject o, string name, int turns) =>
        turns > 0 ? JsonRead.Count(o, name, NoTurn) : CapturedCount.Unavailable(NoTurn);

    /// <summary><c>review_seconds</c> where the line has it; the earliest lines predate the field, and <c>summarise</c>
    /// computed it as the rounded sum of the turns' seconds, which is what those lines carry per turn.</summary>
    private static double ReviewSeconds(JsonObject o) =>
        o["review_seconds"] is JsonValue
            ? JsonRead.Double(o["review_seconds"])
            : PythonRound.Of(JsonRead.List(o, "seconds_per_turn").Sum(JsonRead.Double), 1);

    private static CapturedUsd Cost(JsonObject o) =>
        o["cost_usd"] is JsonValue v && v.TryGetValue<double>(out var usd)
            ? CapturedUsd.Amount((decimal)usd)
            : CapturedUsd.Unavailable("the other harness recorded no cost");

    /// <summary><c>served_count or note.count("served ")</c> — the report's reading, over the note and the <c>served</c>
    /// field together, exactly as <c>write_all</c> joins them.</summary>
    private static int Counted(JsonObject o, string countField, string word)
    {
        var counted = JsonRead.Int(o, countField);
        var note = $"{JsonRead.Text(o, "reviewer_note")} {JsonRead.Text(o, "served")}";

        return counted != 0 ? counted : Occurrences(note, word);
    }

    private static int Occurrences(string text, string word)
    {
        var count = 0;
        for (var at = text.IndexOf(word, StringComparison.Ordinal); at >= 0; at = text.IndexOf(word, at + word.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
