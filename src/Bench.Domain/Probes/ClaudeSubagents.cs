using System.Globalization;
using System.Text.Json;
using Bench.Domain.Trace;

namespace Bench.Domain.Probes;

/// <summary>Claude's <c>-p --output-format stream-json --verbose</c> transcript of a session that launches SUBAGENTS, as claude 2.1.289
/// printed it on 2026-10-12 (<c>tests/Bench.Tests/Fixtures/scouts/claude-2.1.289-*.ndjson</c>). <see cref="ClaudeStream"/> reads the
/// whole stream as one tool trace; this reader groups it by <c>parent_tool_use_id</c>, so one subagent is told from another:
/// <list type="bullet">
/// <item>the MAIN session's events carry <c>parent_tool_use_id: null</c>; each <c>Agent</c> <c>tool_use</c> there names
/// <c>subagent_type</c>, <c>model</c> and <c>prompt</c>;</item>
/// <item>every CHILD event carries <c>parent_tool_use_id</c> = its Agent call's id, and each child assistant event its <c>message.model</c>;</item>
/// <item>the Agent call's <c>tool_result</c> event carries <c>tool_use_result</c> with <c>resolvedModel</c>, <c>totalDurationMs</c>,
/// <c>status</c>, the final iteration's <c>usage</c> and the report text — or a STRING when the call failed
/// ("Agent type … not found");</item>
/// <item>a <c>system/permission_denied</c> event names the <c>tool_use_id</c> it refused;</item>
/// <item>the final <c>result</c> envelope carries <c>duration_ms</c>, <c>total_cost_usd</c>, the main session's <c>usage</c>,
/// <c>modelUsage</c> and <c>subagent_stats</c>.</item>
/// </list>
/// Every reader goes through <see cref="ProbeJson"/> (duplicate keys tolerated, non-JSON lines skipped).</summary>
public static class ClaudeSubagents
{
    public const string AgentTool = "Agent";

    public static SubagentTranscript Read(string stdout)
    {
        var documents = ProbeJson.Lines(stdout);

        try
        {
            return Read(documents.Select(d => d.RootElement).ToList());
        }
        finally
        {
            ProbeJson.Dispose(documents);
        }
    }

    private static SubagentTranscript Read(IReadOnlyList<JsonElement> events)
    {
        var init = events.FirstOrDefault(e => Is(e, "system", "init"));
        var result = events.LastOrDefault(e => ProbeJson.Text(e, "type") == "result");
        var results = ToolResults(events);
        var denied = DeniedIds(events);
        var main = events.Where(e => ProbeJson.Text(e, "type") == "assistant" && Parent(e).Length == 0).ToList();
        var mainCalls = main.SelectMany(ToolUses).Select(u => (Call: new MainToolCall(ProbeJson.Text(u.Block, "id"), ProbeJson.Text(u.Block, "name")), u.Event, u.Block)).ToList();

        return new SubagentTranscript(
            init.ValueKind == JsonValueKind.Object && result.ValueKind == JsonValueKind.Object,
            ProbeJson.Text(init, "model"),
            ProbeJson.Text(init, "claude_code_version"),
            ProbeJson.Text(init, "cwd"),
            [.. mainCalls.Select(c => c.Call)],
            DeduplicatedUsage(main),
            [.. mainCalls.Where(c => c.Call.Name == AgentTool).Select(c => Agent(c.Event, c.Block, events, results, denied))],
            Envelope(result));
    }

    private static SubagentCall Agent(JsonElement useEvent, JsonElement block, IReadOnlyList<JsonElement> events, IReadOnlyDictionary<string, (JsonElement Event, JsonElement Block)> results, IReadOnlySet<string> denied)
    {
        var id = ProbeJson.Text(block, "id");
        var input = ProbeJson.Object(block, "input");
        var prompt = ProbeJson.Text(input, "prompt");
        var child = events.Where(e => ProbeJson.Text(e, "type") == "assistant" && Parent(e) == id).ToList();

        return new SubagentCall(
            id,
            ProbeJson.Text(input, "subagent_type"),
            ProbeJson.Text(input, "model"),
            prompt,
            StableHash.Of(prompt),
            Moment(useEvent),
            results.TryGetValue(id, out var found) ? AgentResultOf(found.Event, found.Block) : AgentResult.NotCaptured,
            new ChildTrace(
                [.. child.Select(e => ProbeJson.Text(ProbeJson.Object(e, "message"), "model")).Where(m => m.Length > 0).Distinct(StringComparer.Ordinal)],
                DeduplicatedUsage(child),
                [.. child.SelectMany(ToolUses).Select(u => ChildCall(u.Block, results, denied))],
                LastText(child)));
    }

    private static AgentResult AgentResultOf(JsonElement resultEvent, JsonElement block)
    {
        var report = ProbeJson.Property(resultEvent, "tool_use_result");
        var isObject = report.ValueKind == JsonValueKind.Object;

        return new AgentResult(
            true,
            IsError(block),
            Moment(resultEvent),
            ProbeJson.Text(report, "status"),
            ProbeJson.Text(report, "resolvedModel"),
            ProbeJson.Number(report, "totalDurationMs", out var ms) ? CapturedCount.Number(ms) : CapturedCount.Unavailable(isObject ? "no totalDurationMs" : "the Agent result is not an object"),
            Usage(ProbeJson.Object(report, "usage")),
            string.Join('\n', ProbeJson.Array(report, "content").Select(b => ProbeJson.Text(b, "text")).Where(t => t.Length > 0)));
    }

    private static ChildToolCall ChildCall(JsonElement block, IReadOnlyDictionary<string, (JsonElement Event, JsonElement Block)> results, IReadOnlySet<string> denied)
    {
        var id = ProbeJson.Text(block, "id");
        var found = results.TryGetValue(id, out var result);
        var fate = (denied.Contains(id), found, found && IsError(result.Block)) switch
        {
            (true, _, _) => ChildToolFate.Denied,
            (_, false, _) => ChildToolFate.NotCaptured,
            (_, _, true) => ChildToolFate.Errored,
            _ => ChildToolFate.Returned,
        };

        return new ChildToolCall(id, ProbeJson.Text(block, "name"), ProbeJson.Raw(ProbeJson.Object(block, "input")), fate, found ? ContentText(result.Block) : string.Empty);
    }

    /// <summary>Usage summed over messages, DEDUPLICATED by <c>message.id</c>: the stream prints one event per content block and repeats
    /// the message's usage on each, so the LAST event of an id stands for the message. An event with no id stands for itself.</summary>
    private static TokenUsage DeduplicatedUsage(IReadOnlyList<JsonElement> assistantEvents)
    {
        var byMessage = new Dictionary<string, TokenUsage>(StringComparer.Ordinal);

        foreach (var (e, i) in assistantEvents.Select((e, i) => (e, i)))
        {
            var message = ProbeJson.Object(e, "message");
            var id = ProbeJson.Text(message, "id");
            byMessage[id.Length > 0 ? id : $"#{i}"] = Usage(ProbeJson.Object(message, "usage"));
        }

        return byMessage.Values.Aggregate(TokenUsage.Zero, (sum, u) => sum.Plus(u));
    }

    /// <summary>A usage object: the cache write split by <c>cache_creation.ephemeral_5m/1h_input_tokens</c> when the split is printed,
    /// otherwise the whole write as 5-minute (the API's default TTL).</summary>
    private static TokenUsage Usage(JsonElement usage)
    {
        var split = ProbeJson.Object(usage, "cache_creation");
        var hasSplit = ProbeJson.Number(split, "ephemeral_5m_input_tokens", out var fiveMinutes) | ProbeJson.Number(split, "ephemeral_1h_input_tokens", out var oneHour);
        ProbeJson.Number(usage, "cache_creation_input_tokens", out var write);

        return new TokenUsage(Count(usage, "input_tokens"), Count(usage, "output_tokens"), Count(usage, "cache_read_input_tokens"), hasSplit ? fiveMinutes : write, hasSplit ? oneHour : 0);
    }

    private static ResultEnvelope Envelope(JsonElement result) =>
        result.ValueKind != JsonValueKind.Object
            ? ResultEnvelope.NotCaptured
            : new ResultEnvelope(
                true,
                IsError(result),
                ProbeJson.Text(result, "subtype"),
                Duration(result),
                Cost(result),
                Usage(ProbeJson.Object(result, "usage")),
                ModelUsage(ProbeJson.Object(result, "modelUsage")),
                Stats(ProbeJson.Object(result, "subagent_stats")));

    private static CapturedCount Duration(JsonElement result) =>
        ProbeJson.Number(result, "duration_ms", out var ms) ? CapturedCount.Number(ms) : CapturedCount.Unavailable("no duration_ms");

    private static CapturedUsd Cost(JsonElement result) =>
        ProbeJson.Decimal(result, "total_cost_usd", out var usd) ? CapturedUsd.Amount(usd) : CapturedUsd.Unavailable("no total_cost_usd");

    private static IReadOnlyList<ModelUsageEntry> ModelUsage(JsonElement models) =>
        models.ValueKind != JsonValueKind.Object
            ? []
            : [.. models.EnumerateObject().Select(m => new ModelUsageEntry(
                m.Name, Count(m.Value, "inputTokens"), Count(m.Value, "outputTokens"), Count(m.Value, "cacheReadInputTokens"), Count(m.Value, "cacheCreationInputTokens"),
                ProbeJson.Decimal(m.Value, "costUSD", out var cost) ? cost : 0m))];

    private static SubagentStats Stats(JsonElement stats) =>
        stats.ValueKind != JsonValueKind.Object
            ? SubagentStats.NotCaptured
            : new SubagentStats(true, Count(stats, "spawned"), Count(stats, "completed"), Count(stats, "failed"), Count(ProbeJson.Object(stats, "refused"), "budget"));

    /// <summary>Every <c>tool_result</c> block by its <c>tool_use_id</c>, with the event that carried it (for its timestamp and its
    /// <c>tool_use_result</c>). The last one wins, as for the probes.</summary>
    private static IReadOnlyDictionary<string, (JsonElement Event, JsonElement Block)> ToolResults(IReadOnlyList<JsonElement> events)
    {
        var results = new Dictionary<string, (JsonElement, JsonElement)>(StringComparer.Ordinal);

        foreach (var e in events.Where(e => ProbeJson.Text(e, "type") == "user"))
        {
            foreach (var block in ProbeJson.Array(ProbeJson.Object(e, "message"), "content").Where(b => ProbeJson.Text(b, "type") == "tool_result"))
            {
                results[ProbeJson.Text(block, "tool_use_id")] = (e, block);
            }
        }

        return results;
    }

    private static IReadOnlySet<string> DeniedIds(IReadOnlyList<JsonElement> events) =>
        events.Where(e => Is(e, "system", "permission_denied")).Select(e => ProbeJson.Text(e, "tool_use_id")).Where(id => id.Length > 0).ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<(JsonElement Event, JsonElement Block)> ToolUses(JsonElement assistant) =>
        ProbeJson.Array(ProbeJson.Object(assistant, "message"), "content").Where(b => ProbeJson.Text(b, "type") == "tool_use").Select(b => (assistant, b));

    private static string LastText(IReadOnlyList<JsonElement> assistantEvents) =>
        assistantEvents.SelectMany(e => ProbeJson.Array(ProbeJson.Object(e, "message"), "content"))
            .Where(b => ProbeJson.Text(b, "type") == "text")
            .Select(b => ProbeJson.Text(b, "text"))
            .LastOrDefault(t => t.Length > 0) ?? string.Empty;

    /// <summary>A tool result's text: a string as it is, an array's text blocks joined by newlines. Anything else is empty — and an
    /// empty result is never read as "nothing found" by the path audit, which reads the FATE first.</summary>
    private static string ContentText(JsonElement block)
    {
        var content = ProbeJson.Property(block, "content");

        return content.ValueKind switch
        {
            JsonValueKind.String => content.GetString() ?? string.Empty,
            JsonValueKind.Array => string.Join('\n', content.EnumerateArray().Select(b => ProbeJson.Text(b, "text")).Where(t => t.Length > 0)),
            _ => string.Empty,
        };
    }

    private static bool IsError(JsonElement block) => block.TryGetProperty("is_error", out var flag) && flag.ValueKind == JsonValueKind.True;

    private static bool Is(JsonElement e, string type, string subtype) => ProbeJson.Text(e, "type") == type && ProbeJson.Text(e, "subtype") == subtype;

    private static string Parent(JsonElement e) => ProbeJson.Text(e, "parent_tool_use_id");

    private static long Count(JsonElement element, string name) => ProbeJson.Number(element, name, out var value) ? value : 0;

    private static StreamMoment Moment(JsonElement e) =>
        DateTimeOffset.TryParse(ProbeJson.Text(e, "timestamp"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at)
            ? StreamMoment.At(at)
            : StreamMoment.None;
}
