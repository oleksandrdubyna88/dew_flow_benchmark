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

    /// <summary>The markers a CLI prints on its OWN stdout/stderr when its account is out (S2 of the question-consultant plan,
    /// D8; measured 2026-10-01): claude <c>Claude AI usage limit reached</c>, codex <c>You've hit your usage limit</c> /
    /// <c>usage_limit_reached</c>, agy <c>Individual quota reached</c>. The product markers above apply too — a CLI signed in
    /// with an API key prints the vendor's <c>credit balance</c> sentence.</summary>
    private static readonly string[] CliMarkers = ["usage_limit_reached", "quota reached"];

    /// <summary>agy's gRPC code for a spent quota is also its code for a plain rate limit; it counts only beside quota wording.</summary>
    private const string ResourceExhausted = "RESOURCE_EXHAUSTED";

    /// <summary>The LINE of a CLI's own output that says its account is out — <see cref="CliMarkers"/>, the product markers, or
    /// <c>RESOURCE_EXHAUSTED</c> with quota wording beside it — trimmed to a sentence; empty when nothing does. A plain 429 or
    /// <c>rate limited</c> is transient and never benches a subject.</summary>
    public static string CliReason(string text) =>
        text.Split('\n').Select(line => line.Trim()).FirstOrDefault(IsCliAccountOut) is { } found ? Short(found) : string.Empty;

    private static bool IsCliAccountOut(string line) =>
        NamesAnAccount(line)
        || CliMarkers.Any(m => line.Contains(m, StringComparison.OrdinalIgnoreCase))
        || (line.Contains(ResourceExhausted, StringComparison.Ordinal) && line.Contains("quota", StringComparison.OrdinalIgnoreCase));

    private static string Short(string line) => line.Length <= 200 ? line : line[..200] + "…";

    private static string FromLine(string line)
    {
        var at = line.IndexOf(Failed, StringComparison.Ordinal);
        var failure = at < 0 ? string.Empty : line[(at + Failed.Length)..];

        return line.StartsWith(NobodyAnswered, StringComparison.Ordinal) && NamesAnAccount(failure) ? failure : string.Empty;
    }

    private static bool NamesAnAccount(string failure) =>
        Markers.Any(m => failure.Contains(m, StringComparison.OrdinalIgnoreCase));
}
