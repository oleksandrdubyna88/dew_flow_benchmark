using System.Text.RegularExpressions;
using Bench.Domain.Trace;

namespace ModelArms.Core.Checks;

/// <summary>What a Microsoft Testing Platform log says of a run (<c>objective-checks.sh:33-34</c>, <c>:56</c>): the three counts of its indented summary, each <c>Unavailable</c> when the log does not carry it, and the name of every test
/// printed as <c>failed &lt;name&gt; (…)</c>, in log order, the <c>suite.namePrefix</c> taken off the front.</summary>
public sealed record MtpSummary(CapturedCount Total, CapturedCount Succeeded, CapturedCount Skipped, IReadOnlyList<string> Failed);

/// <summary>The shell's three <c>num(…)</c> reads and its <c>failed (\S+)</c> read, over the suite's log. The counts are the first match ANYWHERE in the log (<c>/total: (\d+)/</c> is not anchored); the failed records are anchored at a line.</summary>
public static class TestLogSummary
{
    private static readonly Regex Total = new(@"total: (\d+)", RegexOptions.ECMAScript);
    private static readonly Regex Succeeded = new(@"succeeded: (\d+)", RegexOptions.ECMAScript);
    private static readonly Regex Skipped = new(@"skipped: (\d+)", RegexOptions.ECMAScript);
    private static readonly Regex Failed = new($"^failed ({JsLog.NonBlank}+)", JsLog.Options);

    public static MtpSummary Read(string log, string namePrefix) =>
        new(
            JsLog.Count(Total, log, "`total:` count"),
            JsLog.Count(Succeeded, log, "`succeeded:` count"),
            JsLog.Count(Skipped, log, "`skipped:` count"),
            [.. Failed.Matches(JsLog.Lines(log)).Select(match => Unprefixed(match.Groups[1].Value, namePrefix))]);

    /// <summary>The name with <paramref name="prefix"/> taken off once, at its start — the shell's <c>replace(/^CoaiMcp\.Tests\./, '')</c>.</summary>
    private static string Unprefixed(string name, string prefix) =>
        prefix.Length > 0 && name.StartsWith(prefix, StringComparison.Ordinal) ? name[prefix.Length..] : name;
}
