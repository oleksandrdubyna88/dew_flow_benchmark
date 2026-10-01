using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bench.Domain.Probes;

/// <summary>What a CLI's transcript showed of its TOOLS: whether it searched the web, and whether it TRIED to read a file.
/// Each in three states — a transcript the reader cannot parse, or a field the CLI did not print, is <i>not captured</i>.</summary>
public sealed record TranscriptEvidence(ProbeFact WebSearchUsed, ProbeFact ReadAttempted)
{
    public static TranscriptEvidence NotCaptured { get; } = new(ProbeFact.NotCaptured, ProbeFact.NotCaptured);
}

/// <summary>The three transcript grammars, read for tool evidence — claude's <c>-p --output-format json</c> result object, codex's
/// <c>exec --json</c> JSONL events, antigravity's <c>--output-format stream-json</c> NDJSON.
/// <para>
/// <b>Every reader is pinned on a recorded transcript (<c>tests/Bench.Tests/Fixtures/probes/</c>) and returns <i>not
/// captured</i> on any missing field.</b> Those fixtures are SYNTHETIC until S5's hand-check reads the first live cell of each
/// runtime against what the reader extracted (§4, gate round 1 finding 6); until then a runtime's evidence is reported as
/// <i>not captured</i> in every write-up table, never as <i>no</i>.
/// </para>
/// <para>
/// A stream grammar is read only from a COMPLETE turn — codex's <c>turn.completed</c>, antigravity's <c>result</c> event:
/// a stream cut by a kill or a wall has not finished saying which tools it used, and "no item seen" in a truncated stream
/// is not "no".
/// </para></summary>
public static class ProbeTranscripts
{
    /// <summary>Claude's tools that read the disk — a denial of one of these is a read ATTEMPT; a denied <c>WebFetch</c> is not.</summary>
    private static readonly IReadOnlySet<string> ClaudeFileTools =
        new HashSet<string>(StringComparer.Ordinal) { "Read", "Glob", "Grep", "LS", "Bash", "NotebookRead", "Edit", "MultiEdit", "Write", "NotebookEdit" };

    /// <summary>Antigravity's (gemini-cli lineage) tools that read the disk.</summary>
    private static readonly IReadOnlySet<string> AgyReadTools =
        new HashSet<string>(StringComparer.Ordinal) { "read_file", "read_many_files", "list_directory", "glob", "search_file_content", "grep_search", "run_shell_command", "shell" };

    /// <summary>What the model SAID — the final message of each grammar — as the text the verdict's canary check runs over
    /// (S2). Never the whole transcript: a codex <c>command_execution</c> item's <c>aggregated_output</c> or an agy
    /// <c>tool_result</c> can carry a file's bytes the model never repeated, and a token found there is not a token read into
    /// the answer. A transcript that is not the grammar at all is its own answer, trimmed — a CLI that printed plain text still
    /// answered; a grammar that parsed and carried no message answered nothing.</summary>
    public static string Answer(ProbeRuntime runtime, string stdout) => runtime switch
    {
        ProbeRuntime.Claude when Json(stdout) is JsonObject result => Text(result, "result").Trim(),
        ProbeRuntime.Codex when Lines(stdout) is { Count: > 0 } events => Joined(events
            .Select(e => e["item"]).OfType<JsonObject>()
            .Where(item => Text(item, "type") == "agent_message")
            .Select(item => Text(item, "text"))),
        ProbeRuntime.Antigravity when Lines(stdout) is { Count: > 0 } events => Joined(events
            .Where(e => Text(e, "type") == "message" && Text(e, "role") == "assistant")
            .Select(e => Text(e, "content"))),
        _ => stdout.Trim(),
    };

    private static string Joined(IEnumerable<string> messages) => string.Join('\n', messages.Select(m => m.Trim()).Where(m => m.Length > 0));

    public static TranscriptEvidence Read(ProbeRuntime runtime, string stdout) => runtime switch
    {
        ProbeRuntime.Claude => ClaudeJson(stdout),
        ProbeRuntime.Codex => CodexEvents(stdout),
        ProbeRuntime.Antigravity => AgyStream(stdout),
        _ => TranscriptEvidence.NotCaptured,
    };

    /// <summary>The result object: <c>usage.server_tool_use.web_search_requests</c> (a count; absent → not captured) and
    /// <c>permission_denials[]</c> (absent → not captured; present → whether any names a file tool).</summary>
    private static TranscriptEvidence ClaudeJson(string stdout) =>
        Json(stdout) is JsonObject result
            ? new TranscriptEvidence(ClaudeSearches(result), ClaudeDenials(result))
            : TranscriptEvidence.NotCaptured;

    private static ProbeFact ClaudeSearches(JsonObject result) =>
        result["usage"] is JsonObject usage && usage["server_tool_use"] is JsonObject tools && Long(tools["web_search_requests"]) is { } searches
            ? YesIf(searches > 0)
            : ProbeFact.NotCaptured;

    private static ProbeFact ClaudeDenials(JsonObject result) =>
        result["permission_denials"] is JsonArray denials
            ? YesIf(denials.OfType<JsonObject>().Any(d => ClaudeFileTools.Contains(Text(d, "tool_name"))))
            : ProbeFact.NotCaptured;

    /// <summary>JSONL events; the items of a turn that reached <c>turn.completed</c>: a <c>web_search</c> item is a search, a
    /// <c>command_execution</c> item is a read attempt.</summary>
    private static TranscriptEvidence CodexEvents(string stdout)
    {
        var events = Lines(stdout);

        if (!events.Any(e => Text(e, "type") == "turn.completed"))
        {
            return TranscriptEvidence.NotCaptured;
        }

        var items = events.Select(e => e["item"]).OfType<JsonObject>().Select(item => Text(item, "type")).ToList();

        return new TranscriptEvidence(YesIf(items.Contains("web_search")), YesIf(items.Contains("command_execution")));
    }

    /// <summary>NDJSON events; the <c>tool_use</c> events of a stream that reached its <c>result</c>: a web-search tool is a
    /// search, a file tool is a read attempt.</summary>
    private static TranscriptEvidence AgyStream(string stdout)
    {
        var events = Lines(stdout);

        if (!events.Any(e => Text(e, "type") == "result"))
        {
            return TranscriptEvidence.NotCaptured;
        }

        var tools = events.Where(e => Text(e, "type") == "tool_use").Select(e => Text(e, "tool_name")).ToList();

        return new TranscriptEvidence(
            YesIf(tools.Any(t => t.Contains("web_search", StringComparison.OrdinalIgnoreCase))),
            YesIf(tools.Any(AgyReadTools.Contains)));
    }

    private static ProbeFact YesIf(bool seen) => seen ? ProbeFact.Yes : ProbeFact.No;

    /// <summary>Every line that is a JSON object; a banner or a progress line between them is skipped, not fatal.</summary>
    private static IReadOnlyList<JsonObject> Lines(string stdout) =>
        [.. stdout.Split('\n').Select(line => Json(line)).OfType<JsonObject>()];

    private static JsonNode? Json(string text)
    {
        var trimmed = text.Trim();

        if (trimmed.Length == 0 || trimmed[0] is not ('{' or '['))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(trimmed);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Text(JsonObject o, string name) => o[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : string.Empty;

    private static long? Long(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<double>(out var d) && double.IsFinite(d) ? (long)Math.Round(d) : null;
}
