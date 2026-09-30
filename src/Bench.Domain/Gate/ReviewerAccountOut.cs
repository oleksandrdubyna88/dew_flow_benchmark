using System.Text.Json.Nodes;

namespace Bench.Domain.Gate;

/// <summary>Whether a product reply says the reviewer's ACCOUNT is out — a spent balance, a hit usage or spend limit, a
/// refused key — rather than anything the reviewer answered.
/// <para>
/// T5 of <c>todo/PLAN_gate_measurement_tail.md</c>. On 2026-09-29 three accounts ran dry in one day and every cell after
/// that was recorded as a measurement: the product's round came back <c>call_human</c>, the plan loop "never passed", and
/// a settled cell resets the lane breaker, so 44 Fable cells were spent that way. The cause was in the reply the whole
/// time, in its <c>reviewers</c> line — <c>0 of 1 reviewers answered; failed: &lt;id&gt;/&lt;role&gt;: &lt;why&gt;</c> —
/// once coai #622 stopped dropping the Claude CLI's reason.
/// </para>
/// <para>
/// <b>Only when NOBODY answered.</b> A round that another reviewer answered is a measurement of that reviewer, whatever
/// became of the one that failed. <b>Only a named account failure.</b> A plain <c>rate limited</c> is transient and the
/// product retries it; <c>billing</c> alone is too common a word to mean anything; a bare <c>connection refused</c> is not
/// a refused key. A refused key counts whether it is spent or wrong: neither mends itself mid-campaign, and grok's spent
/// balance arrived exactly as one.
/// </para></summary>
public static class ReviewerAccountOut
{
    /// <summary>The fixed start of the refusal a runner returns for such a cell — read back by the campaign the way
    /// <c>ClaimRefusal.NoPendingCell</c> is.</summary>
    public const string Marker = "the reviewer's account is out";

    private const string Failed = "; failed: ";
    private const string NobodyAnswered = "0 of ";

    private static readonly string[] Markers =
        ["spend limit", "usage limit", "credit balance", "insufficient credit", "insufficient_quota", "(HTTP 402)", "refused the key"];

    /// <summary>The failed reviewer and why (<c>&lt;id&gt;/&lt;role&gt;: &lt;why&gt;</c>) when the reply is a round nobody
    /// answered because an account is out; empty for every other reply, including one that is not JSON.</summary>
    public static string Reason(string replyJson) =>
        GateReplyParser.Json(replyJson) is JsonObject reply ? FromLine(GateReplyParser.Text(reply, "reviewers")) : string.Empty;

    /// <summary>The refusal a runner hands back for the cell: the marker, then the reason.</summary>
    public static string Refusal(string reason) => $"{Marker}: {reason}";

    public static bool IsRefusal(string text) => text.StartsWith(Marker, StringComparison.Ordinal);

    private static string FromLine(string line)
    {
        var at = line.IndexOf(Failed, StringComparison.Ordinal);
        var failure = at < 0 ? string.Empty : line[(at + Failed.Length)..];

        return line.StartsWith(NobodyAnswered, StringComparison.Ordinal) && NamesAnAccount(failure) ? failure : string.Empty;
    }

    private static bool NamesAnAccount(string failure) =>
        Markers.Any(m => failure.Contains(m, StringComparison.OrdinalIgnoreCase));
}
