using System.Text.Json;
using Bench.Domain.Trace;

namespace Bench.Domain.Probes;

/// <summary>Codex's <c>exec --json</c> transcript, as codex-cli 0.156.1 printed it on 2026-10-01
/// (<c>tests/Bench.Tests/Fixtures/probes/codex-0.156.1-*.jsonl</c>): JSONL events <c>thread.started</c>, <c>turn.started</c>,
/// <c>item.started</c>/<c>item.completed</c> with an <c>item</c> whose <c>type</c> is <c>agent_message</c> (the answer),
/// <c>command_execution</c> (a shell command — on Windows a <c>pwsh.exe -Command Get-Content …</c>), <c>web_search</c> (a search or an
/// <c>open_page</c> action with a url), <c>file_change</c>, …; then <c>turn.completed</c>.
/// <para>
/// A <c>web_search</c> item carries TWO <c>id</c> properties (finding 5) — read through <see cref="ProbeJson"/>, never a dictionary.
/// Codex prints no offered-tool list. An item is counted once, by its <c>id</c> and type, whichever of its events arrived.
/// </para></summary>
public static class CodexEvents
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
        var items = events.Select(e => ProbeJson.Object(e, "item")).Where(i => i.ValueKind == JsonValueKind.Object).ToList();
        var answer = string.Join('\n', items.Where(i => ProbeJson.Text(i, "type") == "agent_message").Select(i => ProbeJson.Text(i, "text").Trim()).Where(t => t.Length > 0));
        var used = items
            .Where(i => ProbeJson.Text(i, "type") is not ("agent_message" or "reasoning"))
            .GroupBy(i => (Id: ProbeJson.Text(i, "id"), Type: ProbeJson.Text(i, "type")))
            .Select(g => new ProbeToolCall(g.Key.Type, Input(g.Last())))
            .ToList();

        return (
            answer,
            new ToolTrace(
                Complete: events.Any(e => ProbeJson.Text(e, "type") == "turn.completed"),
                OfferedCaptured: false,
                [],
                used,
                [],
                CapturedCount.Unavailable("codex prints no server-side counters")));
    }

    /// <summary>The command for a shell item, the url or query for a search, the whole item otherwise.</summary>
    private static string Input(JsonElement item) => ProbeJson.Text(item, "type") switch
    {
        "command_execution" => ProbeJson.Text(item, "command"),
        "web_search" => ProbeJson.Text(ProbeJson.Object(item, "action"), "url") is { Length: > 0 } url ? url : ProbeJson.Text(item, "query"),
        _ => ProbeJson.Raw(item),
    };
}
