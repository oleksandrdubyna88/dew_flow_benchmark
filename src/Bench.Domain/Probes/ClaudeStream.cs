using System.Text.Json;
using Bench.Domain.Trace;

namespace Bench.Domain.Probes;

/// <summary>Claude's <c>-p --output-format stream-json --verbose</c> transcript, as claude 2.1.258 printed it on 2026-10-01
/// (<c>tests/Bench.Tests/Fixtures/probes/claude-2.1.258-*.ndjson</c>): one JSON object per line —
/// <list type="bullet">
/// <item><c>{"type":"system","subtype":"init","tools":[…]}</c> — the tools OFFERED to the model;</item>
/// <item><c>{"type":"assistant","message":{"content":[{"type":"tool_use","name":…,"input":{…}} | {"type":"text",…}]}}</c> — each call;</item>
/// <item><c>{"type":"user","message":{"content":[{"type":"tool_result","is_error":…}]}}</c> — each result (not read: an output can carry the canary);</item>
/// <item><c>{"type":"result","result":"…","usage":{"server_tool_use":{…}},"permission_denials":[…]}</c> — the final envelope.</item>
/// </list>
/// Other lines (<c>rate_limit_event</c>, <c>system/thinking_tokens</c>, <c>system/permission_denied</c>) are skipped.
/// <para>
/// What the live streams taught: a refused tool is a <c>tool_use</c> block followed by an <c>is_error</c> result and leaves
/// <c>permission_denials</c> EMPTY ("No such tool available: Read"); the web tools run as client-side <c>tool_use</c> blocks while
/// <c>usage.server_tool_use</c> stays at zero. So the calls are the evidence, and the envelope's counters are read only as a bonus.
/// The <c>--output-format json</c> envelope alone — a result with no init — is BLIND and reads as incomplete.
/// </para></summary>
public static class ClaudeStream
{
    public static (string Answer, ToolTrace Trace) Read(string stdout)
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

    private static (string Answer, ToolTrace Trace) Read(IReadOnlyList<JsonElement> events)
    {
        var init = events.FirstOrDefault(e => ProbeJson.Text(e, "type") == "system" && ProbeJson.Text(e, "subtype") == "init");
        var result = events.LastOrDefault(e => ProbeJson.Text(e, "type") == "result");
        var offered = ProbeJson.Array(init, "tools").Where(t => t.ValueKind == JsonValueKind.String).Select(t => t.GetString() ?? string.Empty).ToList();
        var used = events.Where(e => ProbeJson.Text(e, "type") == "assistant").SelectMany(ToolUses).ToList();
        var denied = ProbeJson.Array(result, "permission_denials").Select(d => new ProbeToolCall(ProbeJson.Text(d, "tool_name"), ProbeJson.Raw(ProbeJson.Object(d, "tool_input")))).ToList();

        return (
            ProbeJson.Text(result, "result").Trim(),
            new ToolTrace(
                Complete: init.ValueKind == JsonValueKind.Object && result.ValueKind == JsonValueKind.Object,
                OfferedCaptured: init.ValueKind == JsonValueKind.Object && init.TryGetProperty("tools", out var tools) && tools.ValueKind == JsonValueKind.Array,
                offered,
                used,
                denied,
                ServerWebRequests(result)));
    }

    private static IEnumerable<ProbeToolCall> ToolUses(JsonElement assistant) =>
        ProbeJson.Array(ProbeJson.Object(assistant, "message"), "content")
            .Where(block => ProbeJson.Text(block, "type") == "tool_use")
            .Select(block => new ProbeToolCall(ProbeJson.Text(block, "name"), ProbeJson.Raw(ProbeJson.Object(block, "input"))));

    /// <summary><c>usage.server_tool_use.web_search_requests + web_fetch_requests</c> — not captured when the envelope has no counters.</summary>
    private static CapturedCount ServerWebRequests(JsonElement result)
    {
        var tools = ProbeJson.Object(ProbeJson.Object(result, "usage"), "server_tool_use");

        return ProbeJson.Number(tools, "web_search_requests", out var searches)
            ? CapturedCount.Number(searches + (ProbeJson.Number(tools, "web_fetch_requests", out var fetches) ? fetches : 0))
            : CapturedCount.Unavailable("the envelope carries no server_tool_use");
    }
}
