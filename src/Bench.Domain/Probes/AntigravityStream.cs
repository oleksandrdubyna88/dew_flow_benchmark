using System.Text.Json;
using Bench.Domain.Trace;

namespace Bench.Domain.Probes;

/// <summary>Antigravity's <c>--print= --input-format stream-json --output-format stream-json</c> transcript, as agy 1.2.14 printed
/// it on 2026-10-01 (<c>tests/Bench.Tests/Fixtures/probes/agy-1.2.14-*.ndjson</c>) — NOT the <c>type/tool_use/message</c> shape S1
/// guessed. One JSON object per line, keyed by <c>event</c>:
/// <list type="bullet">
/// <item><c>{"event":"init","init":{"model":…,"cwd":…,"tools":[…]}}</c> — the tools OFFERED;</item>
/// <item><c>{"event":"step_update","step_update":{"step_index":n,"state":"ACTIVE"|"DONE","step_type":"tool","tool_name":…,"tool_info":{"parameters":{…}}}}</c>
/// — one tool step, printed twice (active, then done), counted once by its <c>step_index</c>. <c>DONE</c> says nothing about success or
/// failure, so a step's fate is <i>not captured</i> (S2c, finding 1) — only a <c>denied_actions</c> entry is a STOP;</item>
/// <item><c>{"event":"result","result":{"status":…,"response":"…","denied_actions":[{"action":…}]}}</c> — the answer, and what headless
/// mode auto-denied (the live web-search cell: <c>read_url</c>, with an EMPTY response).</item>
/// </list></summary>
public static class AntigravityStream
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

    /// <summary>The CLI's own voice (S2c, finding 5): the <c>result</c>'s status and response, and any <c>error</c> event — never a step's output.</summary>
    public static string OwnVoice(string stdout)
    {
        var documents = ProbeJson.Lines(stdout);

        try
        {
            var events = documents.Select(d => d.RootElement).ToList();
            var result = ProbeJson.Object(events.LastOrDefault(e => ProbeJson.Text(e, "event") == "result"), "result");
            var errors = events.Where(e => ProbeJson.Text(e, "event") == "error")
                .Select(e => ProbeJson.Text(e, "message") is { Length: > 0 } message ? message : ProbeJson.Text(ProbeJson.Object(e, "error"), "message"));

            return string.Join('\n', new[] { ProbeJson.Text(result, "status"), ProbeJson.Text(result, "response") }.Concat(errors).Where(t => t.Length > 0));
        }
        finally
        {
            ProbeJson.Dispose(documents);
        }
    }

    private static (string Answer, ToolTrace Trace) Read(IReadOnlyList<JsonElement> events)
    {
        var init = ProbeJson.Object(events.FirstOrDefault(e => ProbeJson.Text(e, "event") == "init"), "init");
        var result = ProbeJson.Object(events.LastOrDefault(e => ProbeJson.Text(e, "event") == "result"), "result");
        var steps = events.Select(e => ProbeJson.Object(e, "step_update")).Where(s => ProbeJson.Text(s, "step_type") == "tool").ToList();
        var used = steps
            .GroupBy(s => ProbeJson.Number(s, "step_index", out var index) ? index : -1)
            .Select(g => new ProbeToolCall(ProbeJson.Text(g.Last(), "tool_name"), ProbeJson.Raw(ProbeJson.Object(ProbeJson.Object(g.Last(), "tool_info"), "parameters"))))
            .ToList();
        var denied = ProbeJson.Array(result, "denied_actions").Select(d => new ProbeToolCall(ProbeJson.Text(d, "action"), ProbeJson.Text(d, "display_name"), ProbeFact.Yes)).ToList();

        return (
            ProbeJson.Text(result, "response").Trim(),
            new ToolTrace(
                Complete: result.ValueKind == JsonValueKind.Object,
                OfferedCaptured: init.ValueKind == JsonValueKind.Object && init.TryGetProperty("tools", out var tools) && tools.ValueKind == JsonValueKind.Array,
                [.. ProbeJson.Array(init, "tools").Where(t => t.ValueKind == JsonValueKind.String).Select(t => t.GetString() ?? string.Empty)],
                used,
                denied,
                CapturedCount.Unavailable("agy prints no server-side counters")));
    }
}
