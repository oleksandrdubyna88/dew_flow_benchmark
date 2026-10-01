using System.Text.Json;
using Bench.Domain.Trace;

namespace Bench.Domain.Probes;

/// <summary>Claude's <c>-p --output-format stream-json --verbose</c> transcript, as claude 2.1.258 printed it on 2026-10-01
/// (<c>tests/Bench.Tests/Fixtures/probes/claude-2.1.258-*.ndjson</c>): one JSON object per line —
/// <list type="bullet">
/// <item><c>{"type":"system","subtype":"init","tools":[…]}</c> — the tools OFFERED to the model;</item>
/// <item><c>{"type":"assistant","message":{"content":[{"type":"tool_use","id":…,"name":…,"input":{…}} | {"type":"text",…}]}}</c> — each call;</item>
/// <item><c>{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":…,"is_error":…}]}}</c> — each result: read for its
/// <c>is_error</c> ONLY (S2c, finding 1 — whether the call was STOPPED), never for its content, which can carry the canary;</item>
/// <item><c>{"type":"system","subtype":"permission_denied","tool_use_id":…}</c> — a call the CLI refused (seen live under <c>--restricted</c>);</item>
/// <item><c>{"type":"result","result":"…","usage":{"server_tool_use":{…}},"permission_denials":[…]}</c> — the final envelope.</item>
/// </list>
/// Other lines (<c>rate_limit_event</c>, <c>system/thinking_tokens</c>, <c>system/task_*</c>) are skipped.
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

    /// <summary>The CLI's own voice (S2c, finding 5): the <c>result</c> envelope's text, subtype and errors — never a tool result.</summary>
    public static string OwnVoice(string stdout)
    {
        var documents = ProbeJson.Lines(stdout);

        try
        {
            var result = documents.Select(d => d.RootElement).LastOrDefault(e => ProbeJson.Text(e, "type") == "result");

            return string.Join('\n', new[] { ProbeJson.Text(result, "subtype"), ProbeJson.Text(result, "result") }
                .Concat(ProbeJson.Array(result, "errors").Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString() ?? string.Empty))
                .Where(t => t.Length > 0));
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
        var fates = Fates(events);
        var used = events.Where(e => ProbeJson.Text(e, "type") == "assistant").SelectMany(a => ToolUses(a, fates)).ToList();
        var denied = ProbeJson.Array(result, "permission_denials").Select(d => new ProbeToolCall(ProbeJson.Text(d, "tool_name"), ProbeJson.Raw(ProbeJson.Object(d, "tool_input")), ProbeFact.Yes)).ToList();

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

    /// <summary>Each call's fate by its <c>tool_use_id</c>: a <c>permission_denied</c> event or an <c>is_error</c> result is a STOP, a
    /// result that is not an error is a RUN. The result's CONTENT is never read.</summary>
    private static IReadOnlyDictionary<string, ProbeFact> Fates(IReadOnlyList<JsonElement> events)
    {
        var fates = new Dictionary<string, ProbeFact>(StringComparer.Ordinal);

        foreach (var block in events.Where(e => ProbeJson.Text(e, "type") == "user").SelectMany(u => ProbeJson.Array(ProbeJson.Object(u, "message"), "content")).Where(b => ProbeJson.Text(b, "type") == "tool_result"))
        {
            fates[ProbeJson.Text(block, "tool_use_id")] = IsError(block) ? ProbeFact.Yes : ProbeFact.No;
        }

        foreach (var denied in events.Where(e => ProbeJson.Text(e, "type") == "system" && ProbeJson.Text(e, "subtype") == "permission_denied"))
        {
            fates[ProbeJson.Text(denied, "tool_use_id")] = ProbeFact.Yes;
        }

        return fates;
    }

    private static bool IsError(JsonElement block) => block.TryGetProperty("is_error", out var flag) && flag.ValueKind == JsonValueKind.True;

    private static IEnumerable<ProbeToolCall> ToolUses(JsonElement assistant, IReadOnlyDictionary<string, ProbeFact> fates) =>
        ProbeJson.Array(ProbeJson.Object(assistant, "message"), "content")
            .Where(block => ProbeJson.Text(block, "type") == "tool_use")
            .Select(block => new ProbeToolCall(
                ProbeJson.Text(block, "name"),
                ProbeJson.Raw(ProbeJson.Object(block, "input")),
                fates.GetValueOrDefault(ProbeJson.Text(block, "id"), ProbeFact.NotCaptured)));

    /// <summary><c>usage.server_tool_use.web_search_requests + web_fetch_requests</c> — not captured when the envelope has no counters.</summary>
    private static CapturedCount ServerWebRequests(JsonElement result)
    {
        var tools = ProbeJson.Object(ProbeJson.Object(result, "usage"), "server_tool_use");

        return ProbeJson.Number(tools, "web_search_requests", out var searches)
            ? CapturedCount.Number(searches + (ProbeJson.Number(tools, "web_fetch_requests", out var fetches) ? fetches : 0))
            : CapturedCount.Unavailable("the envelope carries no server_tool_use");
    }
}
