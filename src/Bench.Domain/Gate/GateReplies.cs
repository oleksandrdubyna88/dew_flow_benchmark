using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bench.Domain.Trace;

namespace Bench.Domain.Gate;

/// <summary>One finding as the reply carried it — the TEXT included, because this record lives only between the reply
/// and the artefact store: <see cref="GateFinding.Of"/> keeps the hashes, <c>findings.jsonl</c> keeps
/// <see cref="Json"/>.</summary>
public sealed record ParsedFinding(
    int Ordinal,
    FindingSeverity Severity,
    FindingCategory Category,
    bool IsGating,
    int Line,
    string File,
    string Text,
    string Json);

/// <summary>A tool reply, parsed: the <see cref="GateReply"/> the facts are computed from, the findings, the number
/// of findings a resolve must decide, and the orders the round handed back.</summary>
public sealed record ParsedReply(GateReply Reply, IReadOnlyList<ParsedFinding> Findings, IReadOnlyList<string> Commands)
{
    public GateVerdictWord Verdict => Reply is GateReply.Answered a ? a.Verdict : GateVerdictWord.Unknown;
}

/// <summary>The reply parser — the other harness's <c>parse_reply</c> plus <c>summarise</c>'s reading of the reply. A
/// text that is not JSON is <see cref="GateReply.NotJson"/>; the product's refusal object <c>{"error": …}</c> is
/// <see cref="GateReply.Refused"/>; anything else is an answer whose findings are a LIST or <i>not captured</i>.</summary>
public static class GateReplyParser
{
    public static ParsedReply Parse(string text)
    {
        var root = Json(text);

        if (root is not JsonObject reply)
        {
            return new ParsedReply(new GateReply.NotJson(text.Length), [], []);
        }

        if (reply["error"] is JsonValue error && !reply.ContainsKey("verdict"))
        {
            return new ParsedReply(new GateReply.Refused(Str(error)), [], []);
        }

        var list = reply["findings"] as JsonArray;
        var findings = list is null ? [] : list.Select((f, i) => Finding(i, f as JsonObject ?? [])).ToList();
        var count = list is null ? CapturedCount.Unavailable("the reply carried no findings list") : CapturedCount.Number(findings.Count);

        return new ParsedReply(
            new GateReply.Answered(GateVerdictWords.Parse(Text(reply, "verdict")), count),
            findings,
            [.. (reply["commands"] as JsonArray ?? []).Select(c => c is JsonValue v ? Str(v) : string.Empty)]);
    }

    /// <summary>Accept every finding of a round: the bench measures what the gate produced, not a policy for arguing
    /// with it — a rejection would change the next round's arithmetic and make two runs incomparable.</summary>
    public static string AcceptAll(int findings) =>
        new JsonArray([.. Enumerable.Range(0, findings).Select(i => (JsonNode)new JsonObject { ["finding"] = i, ["action"] = "accept" })]).ToJsonString();

    /// <summary>The three verdicts that open the code stage — the same three the product acts on
    /// (<c>AdvanceOnResolve</c> is set by proceed and by the good_enough / continue_anyway policies).</summary>
    public static bool Passed(GateVerdictWord verdict) =>
        verdict is GateVerdictWord.Proceed or GateVerdictWord.GoodEnough or GateVerdictWord.ContinueAnyway;

    /// <summary>What a resolve reply refused, or empty when it took the decisions.</summary>
    public static string RefusalIn(string resolveText) =>
        Json(resolveText) switch
        {
            JsonObject o when o["error"] is JsonValue e => Str(e),
            JsonObject => string.Empty,
            _ => resolveText.Length == 0 ? "the resolve answered with nothing" : "the resolve answered non-JSON",
        };

    private static ParsedFinding Finding(int ordinal, JsonObject f)
    {
        var severity = FindingWords.Severity(Text(f, "severity"));
        var gating = f["isGating"] is JsonValue g && g.TryGetValue<bool>(out var flag) ? flag : severity is FindingSeverity.Blocking or FindingSeverity.Major;
        var text = string.Join('\n', new[] { Text(f, "title"), Text(f, "why"), Text(f, "fix") }.Where(t => t.Length > 0));

        return new ParsedFinding(
            ordinal,
            severity,
            FindingWords.Category(Text(f, "category")),
            gating,
            f["line"] is JsonValue l && l.TryGetValue<int>(out var line) && line > 0 ? line : 0,
            Text(f, "file"),
            text.Length > 0 ? text : f.ToJsonString(),
            f.ToJsonString());
    }

    internal static JsonNode? Json(string text)
    {
        try
        {
            return text.Trim().Length == 0 ? null : JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static string Text(JsonObject o, string name) => o[name] is JsonValue v ? Str(v) : string.Empty;

    private static string Str(JsonValue v) => v.TryGetValue<string>(out var s) ? s : v.ToJsonString();
}

/// <summary>The product's usage ledger, read: <c>usage.jsonl</c> rows → reviewer turns — the other harness's
/// <c>read_ledger</c> plus its turn filter. Only REVIEW rows of the gate's own stage count (a code cell's plan loop and
/// a consultation are not the code reviewer's spend); a field the product did not write is <i>not captured</i>, never
/// zero; an unreadable line is counted.</summary>
public sealed record LedgerRows(IReadOnlyList<LedgerTurn> Turns, int Unparsed)
{
    /// <summary>The stage word the product writes on a ledger row (its <c>Stage</c> enum).</summary>
    public static string StageWord(GateKind gate) => gate switch
    {
        GateKind.Plan => "PlanReview",
        GateKind.Code => "CodeReview",
        _ => "FeatureReview",
    };

    public static LedgerRows Parse(string jsonl, GateKind gate)
    {
        var turns = new List<LedgerTurn>();
        var unparsed = 0;

        foreach (var line in jsonl.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))
        {
            if (GateReplyParser.Json(line) is not JsonObject row)
            {
                unparsed++;
                continue;
            }

            if (IsReviewTurn(row, gate))
            {
                turns.Add(Turn(row));
            }
        }

        return new LedgerRows(turns, unparsed);
    }

    private static bool IsReviewTurn(JsonObject row, GateKind gate) =>
        GateReplyParser.Text(row, "provider").Length > 0
        && string.Equals(GateReplyParser.Text(row, "stage"), StageWord(gate), StringComparison.Ordinal)
        && GateReplyParser.Text(row, "kind") is "" or "review";

    private static LedgerTurn Turn(JsonObject row) => new(
        row["seconds"] is JsonValue s && s.TryGetValue<double>(out var seconds) ? seconds : 0,
        Count(row, "tokensIn"),
        Count(row, "tokensOut"),
        Count(row, "tokensCached"),
        Count(row, "tokensReasoning"),
        row["costUsd"] is JsonValue c && c.TryGetValue<double>(out var cost) ? CapturedUsd.Amount((decimal)cost) : CapturedUsd.Unavailable("the ledger row carried no cost"),
        GateReplyParser.Text(row, "outcome"));

    private static CapturedCount Count(JsonObject row, string name) =>
        row[name] is JsonValue v && v.TryGetValue<long>(out var n)
            ? CapturedCount.Number(n)
            : CapturedCount.Unavailable($"the ledger row carried no {name}");
}

/// <summary>What the product's stderr says about a run — ports of <c>served_and_refused</c> and the argv match of
/// <c>copy_answer_files</c>. Colour codes are stripped first; an absent line is zero served and no prompt file, never
/// a failure (a CLI reviewer launches no shim and says nothing of the sort).</summary>
public static partial class StderrFacts
{
    [GeneratedRegex(@"\x1b\[[0-9;]*m")]
    private static partial Regex Ansi { get; }

    [GeneratedRegex(@"--prompt-file (\S+) --schema-file \S+ --out (\S+)")]
    private static partial Regex ArgvFiles { get; }

    [GeneratedRegex(@"reviewer \S+ answered in [^\n]*")]
    private static partial Regex Answered { get; }

    [GeneratedRegex(@"turn \d+: ")]
    private static partial Regex TurnMark { get; }

    [GeneratedRegex(@"\(\d+-\d+ of \d+\)")]
    private static partial Regex Span { get; }

    public sealed record Facts(int Served, int Refused, IReadOnlyList<string> PromptFiles);

    public static Facts Of(string stderr)
    {
        var log = Ansi.Replace(stderr, string.Empty);
        var prompts = ArgvFiles.Matches(log).Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal).ToList();
        var line = Answered.Match(log);

        if (!line.Success)
        {
            return new Facts(0, 0, prompts);
        }

        var source = line.Value.Contains("source:", StringComparison.Ordinal) ? line.Value.Split("source:", 2)[1] : string.Empty;
        var (served, refused) = TurnMark.Split(source).Select(p => p.Trim()).Where(p => p.Length > 0).Aggregate((0, 0), Count);

        return new Facts(served, refused, prompts);
    }

    private static (int Served, int Refused) Count((int Served, int Refused) total, string part)
    {
        var split = part.Split("; not served ", 2);
        var served = split[0].StartsWith("served ", StringComparison.Ordinal) ? Span.Matches(split[0]).Count : 0;
        var refused = split.Length > 1 ? Regex.Matches(split[1], " - ").Count : 0;

        return (total.Served + served, total.Refused + refused);
    }
}

/// <summary>One exchange the tap recorded, read back — the other harness's <c>read_calls</c> + <c>facts</c>: the facts
/// file (status, wall) and the raw response (finish reason, cached and reasoning tokens, content size). A call the tap
/// forwarded and never saw answered has no facts file: status 0, which is a failure, never a silent success.</summary>
public static class TapCallFacts
{
    public static HttpCallFacts From(string factsJson, string responseJson)
    {
        var facts = GateReplyParser.Json(factsJson) as JsonObject ?? [];
        var response = GateReplyParser.Json(responseJson) as JsonObject ?? [];
        var choice = (response["choices"] as JsonArray)?.FirstOrDefault() as JsonObject ?? [];
        var usage = response["usage"] as JsonObject ?? [];

        return new HttpCallFacts(
            facts["status"] is JsonValue s && s.TryGetValue<int>(out var status) ? status : 0,
            GateReplyParser.Text(choice, "finish_reason"),
            Tokens(usage["prompt_tokens_details"] as JsonObject, "cached_tokens"),
            Tokens(usage["completion_tokens_details"] as JsonObject, "reasoning_tokens"),
            facts["wall_s"] is JsonValue w && w.TryGetValue<double>(out var wall) ? wall : 0,
            ContentChars(choice));
    }

    private static CapturedCount Tokens(JsonObject? details, string name) =>
        details?[name] is JsonValue v && v.TryGetValue<long>(out var n) ? CapturedCount.Number(n) : CapturedCount.Unavailable($"the response carried no {name}");

    private static int ContentChars(JsonObject choice) =>
        (choice["message"] as JsonObject)?["content"] is JsonValue c && c.TryGetValue<string>(out var content) ? content.Length : 0;

    /// <summary>The call number a tap file belongs to — <c>call-07.json</c> → 7; zero for a file of any other name.</summary>
    public static int CallNumber(string fileName) =>
        fileName.StartsWith("call-", StringComparison.Ordinal) && fileName.Length >= 7
        && int.TryParse(fileName.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;
}
