using System.Text.Json.Nodes;
using Bench.Domain;
using Bench.Domain.Scouts;

namespace Bench.Tests.Scouts;

/// <summary>One child tool call of a synthetic stream: its name, its input JSON, and how it ended — <c>returned</c> with
/// <see cref="Result"/>, <c>denied</c> (a <c>permission_denied</c> event and an <c>is_error</c> result), <c>errored</c> (an
/// <c>is_error</c> result alone), or <c>none</c> (no result at all — a truncated stream).</summary>
internal sealed record ToolSpec(string Name, string Input, string Fate = "returned", string Result = "ok");

/// <summary>One scout of a synthetic stream.</summary>
internal sealed record ChildSpec(
    string Prompt,
    string InputModel = "sonnet",
    string ResolvedModel = ScoutStreams.Sonnet,
    string MessageModel = ScoutStreams.Sonnet,
    string AgentType = ScoutArms.CodeExplorerType,
    IReadOnlyList<ToolSpec>? Tools = null,
    string Report = "KEY FILES\n- src/a.cs\nCLAIMS\n- src/a.cs:3 — defines A",
    bool ResultMissing = false);

/// <summary>The scout stream fixtures and a builder for synthetic cell streams in the SAME shape claude 2.1.289 printed on 2026-10-12
/// (the fixtures are the reference: init, the main session's Agent tool_use events, each child's events under its
/// <c>parent_tool_use_id</c>, each Agent result with its <c>tool_use_result</c>, the result envelope).</summary>
internal static class ScoutStreams
{
    public const string Tree = "C:/work/tree";

    public const string Sonnet = "claude-sonnet-5-5";

    public const string Opus = "claude-opus-5-5";

    public static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "scouts", $"claude-2.1.289-{name}.ndjson"));

    /// <summary>The stream with every line passed through <paramref name="edit"/> — a JSON object in, the object to write out (the same
    /// one, edited in place, or another).</summary>
    public static string Edit(string stream, Func<JsonObject, JsonObject> edit) =>
        string.Join('\n', stream.Split('\n').Where(l => l.Trim().Length > 0).Select(l => edit(JsonNode.Parse(l)!.AsObject()).ToJsonString())) + "\n";

    /// <summary>The stream without its last event — a kill or a wall landing one line early.</summary>
    public static string Truncated(string stream)
    {
        var lines = stream.TrimEnd('\n').Split('\n');
        return string.Join('\n', lines[..^1]) + "\n";
    }

    public static string Hash(string prompt) => StableHash.Of(prompt);

    /// <summary>A whole cell: the main session launches one Agent call per child, each child runs its tools, each Agent result carries
    /// its resolved model, and the envelope carries <c>subagent_stats.completed</c> (default: the number of children with a result).</summary>
    public static string Cell(IReadOnlyList<ChildSpec> children, IReadOnlyList<string>? mainTools = null, bool envelope = true, int? completed = null)
    {
        var lines = new List<JsonObject>
        {
            new() { ["type"] = "system", ["subtype"] = "init", ["cwd"] = Tree, ["model"] = Opus, ["claude_code_version"] = "2.1.289", ["tools"] = new JsonArray("Task", "Read", "Glob", "Grep") },
        };

        foreach (var (child, i) in children.Select((c, i) => (c, i)))
        {
            lines.Add(MainEvent($"m-agent-{i}", new JsonObject { ["type"] = "tool_use", ["id"] = AgentId(i), ["name"] = "Agent", ["input"] = AgentInput(child) }, Moment(i)));
        }

        foreach (var (tool, i) in (mainTools ?? []).Select((t, i) => (t, i)))
        {
            lines.Add(MainEvent($"m-tool-{i}", new JsonObject { ["type"] = "tool_use", ["id"] = $"toolu_main_{i}", ["name"] = tool, ["input"] = new JsonObject() }, Moment(i)));
        }

        lines.AddRange(children.SelectMany((child, i) => Child(child, i)));

        if (envelope)
        {
            lines.Add(Envelope(completed ?? children.Count(c => !c.ResultMissing)));
        }

        return string.Join('\n', lines.Select(l => l.ToJsonString())) + "\n";
    }

    public static string AgentId(int i) => $"toolu_agent_{i}";

    private static JsonObject AgentInput(ChildSpec child) => new()
    {
        ["subagent_type"] = child.AgentType,
        ["model"] = child.InputModel,
        ["description"] = "scout",
        ["prompt"] = child.Prompt,
    };

    private static IEnumerable<JsonObject> Child(ChildSpec child, int i)
    {
        var parent = AgentId(i);
        var tools = child.Tools ?? [new ToolSpec("Read", $$"""{"file_path":"{{Tree}}/src/a.cs"}""")];

        foreach (var (tool, t) in tools.Select((tool, t) => (tool, t)))
        {
            var id = $"toolu_c{i}_t{t}";

            if (tool.Fate == "denied")
            {
                yield return new JsonObject { ["type"] = "system", ["subtype"] = "permission_denied", ["tool_name"] = tool.Name, ["tool_use_id"] = id };
            }

            yield return Assistant($"msg_c{i}_a", child.MessageModel, new JsonObject { ["type"] = "tool_use", ["id"] = id, ["name"] = tool.Name, ["input"] = JsonNode.Parse(tool.Input) }, parent, Moment(i));

            if (tool.Fate != "none")
            {
                var result = new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = id, ["content"] = tool.Result };
                if (tool.Fate is "denied" or "errored")
                {
                    result["is_error"] = true;
                }

                yield return new JsonObject { ["type"] = "user", ["message"] = new JsonObject { ["role"] = "user", ["content"] = new JsonArray(result) }, ["parent_tool_use_id"] = parent, ["timestamp"] = Moment(i) };
            }
        }

        yield return Assistant($"msg_c{i}_b", child.MessageModel, new JsonObject { ["type"] = "text", ["text"] = child.Report }, parent, Moment(i));

        if (!child.ResultMissing)
        {
            yield return AgentResult(child, i);
        }
    }

    private static JsonObject AgentResult(ChildSpec child, int i)
    {
        var report = new JsonObject
        {
            ["status"] = "completed",
            ["agentType"] = child.AgentType,
            ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = child.Report }),
            ["totalDurationMs"] = 1000 + i,
            ["usage"] = Usage(2, 50, 100, 10),
        };

        if (child.ResolvedModel.Length > 0)
        {
            report["resolvedModel"] = child.ResolvedModel;
        }

        return new JsonObject
        {
            ["type"] = "user",
            ["message"] = new JsonObject { ["role"] = "user", ["content"] = new JsonArray(new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = AgentId(i), ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = "[Subagent hand-back] " + child.Report }) }) },
            ["parent_tool_use_id"] = null,
            ["timestamp"] = Moment(i + 5),
            ["tool_use_result"] = report,
        };
    }

    private static JsonObject Envelope(int completed) => new()
    {
        ["type"] = "result",
        ["subtype"] = "success",
        ["is_error"] = false,
        ["duration_ms"] = 20000,
        ["total_cost_usd"] = 0.5,
        ["usage"] = Usage(4, 100, 1000, 0),
        ["modelUsage"] = new JsonObject(),
        ["subagent_stats"] = new JsonObject { ["spawned"] = completed, ["completed"] = completed, ["failed"] = 0, ["refused"] = new JsonObject { ["budget"] = 0 } },
    };

    private static JsonObject MainEvent(string messageId, JsonObject block, string at) => Assistant(messageId, Opus, block, null, at);

    private static JsonObject Assistant(string messageId, string model, JsonObject block, string? parent, string at) => new()
    {
        ["type"] = "assistant",
        ["message"] = new JsonObject { ["model"] = model, ["id"] = messageId, ["role"] = "assistant", ["content"] = new JsonArray(block), ["usage"] = Usage(2, 4, 0, 100) },
        ["parent_tool_use_id"] = parent,
        ["timestamp"] = at,
    };

    private static JsonObject Usage(long input, long output, long cacheRead, long cacheWrite) => new()
    {
        ["input_tokens"] = input,
        ["output_tokens"] = output,
        ["cache_read_input_tokens"] = cacheRead,
        ["cache_creation_input_tokens"] = cacheWrite,
        ["cache_creation"] = new JsonObject { ["ephemeral_5m_input_tokens"] = cacheWrite, ["ephemeral_1h_input_tokens"] = 0 },
    };

    private static string Moment(int second) => $"2026-10-12T10:00:{second:00}.000Z";
}
