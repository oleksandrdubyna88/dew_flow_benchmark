using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace FakeCli;

/// <summary>The fake CLI's entry point: <c>--version</c> answers and exits; anything else reads the prompt on stdin, logs the
/// call, and does what the script says for the model the argv names.</summary>
public static class Program
{
    public static int Main(string[] args)
    {
        var script = Script.Load(ModelOf(args));

        if (args.Contains("--version"))
        {
            Console.Out.Write($"{script.VersionText}\n");
            return 0;
        }

        var prompt = PromptOf(args, Console.IsInputRedirected ? Console.In.ReadToEnd() : string.Empty);
        var call = Events.Record(ModelOf(args), args, prompt);

        return new Behaviour(script, args, prompt, call).Run();
    }

    /// <summary>The prompt as the CLI would see it: agy's stream input is one NDJSON user message per line (<c>message.content</c>);
    /// every other CLI reads the text as it is.</summary>
    public static string PromptOf(string[] args, string stdin) =>
        args.Contains("--input-format") && args.Contains("stream-json")
            ? string.Join('\n', stdin.Split('\n').Select(line => line.Trim()).Where(line => line.StartsWith('{'))
                .Select(line => (JsonNode.Parse(line) as JsonObject)?["message"]?["content"]?.GetValue<string>() ?? string.Empty))
            : stdin;

    /// <summary>The model the launch pinned — claude/agy <c>--model &lt;id&gt;</c>, codex <c>-m &lt;id&gt;</c> — the key a script's
    /// per-subject overrides are read under.</summary>
    public static string ModelOf(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--model" or "-m")
            {
                return args[i + 1];
            }
        }

        return string.Empty;
    }
}

/// <summary>What one run of the fake does, read from <c>FAKE_CLI_SCRIPT</c>: the root's fields, overridden by
/// <c>subjects.&lt;model&gt;</c> for the model the argv names. Every field is optional; the defaults are a well-behaved CLI that
/// reads inside its working directory, reads nothing outside it, and knows no version.</summary>
public sealed class Script
{
    private readonly JsonObject _root;
    private readonly JsonObject _overrides;

    private Script(JsonObject root, JsonObject overrides)
    {
        _root = root;
        _overrides = overrides;
    }

    public string VersionText => Text("versionText", "fake-cli 1.0.0-fake");

    /// <summary><c>answer</c> · <c>refuse</c> (a usage error) · <c>hang</c> · <c>quota</c> · <c>exit</c> · <c>silent</c>.</summary>
    public string Mode => Text("mode", "answer");

    /// <summary>From this call of this model on (1-based), the fake prints its quota marker instead of answering; zero = never.</summary>
    public int QuotaFrom => Int("quotaFrom", 0);

    /// <summary>The one call (1-based) of this model that hangs; zero = none. Later calls answer.</summary>
    public int HangOnCall => Int("hangOnCall", 0);

    public bool ReadInside => Bool("readInside", true);

    public bool ReadOutside => Bool("readOutside", false);

    public bool WebSearch => Bool("webSearch", false);

    public bool ReadAttempted => Bool("readAttempted", false);

    /// <summary>The fake reaches the canary through its SHELL tool (claude <c>PowerShell</c>, codex <c>command_execution</c>, agy
    /// <c>run_command</c>) — the live leak of 2026-10-01, replayed: the answer then carries the canary when <c>readOutside</c> allows it.</summary>
    public bool Shell => Bool("shell", false);

    public string Version => Text("version", "0.52.0");

    public string QuotaText => Text("quotaText", "You've hit your usage limit. Visit https://chatgpt.com/codex/settings/usage to purchase more credits.");

    public string RefuseText => Text("refuseText", "error: unexpected argument '--search' found\n\nUsage: codex exec [OPTIONS] [PROMPT]");

    public int RefuseExit => Int("refuseExit", 2);

    public int ExitCode => Int("exitCode", 1);

    public string Stderr => Text("stderr", string.Empty);

    public int DelayMs => Int("delayMs", 0);

    /// <summary>An argv token whose presence makes the fake refuse the launch — how a test plants "this build has no such flag".</summary>
    public string RefuseArgvContaining => Text("refuseArgvContaining", string.Empty);

    /// <summary>A file whose bytes the fake prints on stdout VERBATIM instead of composing an answer — how a test replays a LIVE
    /// transcript (<c>tests/Bench.Tests/Fixtures/probes/</c>) through the real runner; empty = compose.</summary>
    public string StdoutFile => Text("stdoutFile", string.Empty);

    public static Script Load(string model)
    {
        var path = Environment.GetEnvironmentVariable("FAKE_CLI_SCRIPT") is { Length: > 0 } env ? env : Path.Combine(FakeHome.For(model), "script.json");
        var root = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? [] : [];
        var overrides = model.Length > 0 && root["subjects"]?[model] is JsonObject o ? o : [];

        return new Script(root, overrides);
    }

    private JsonNode? Node(string name) => _overrides[name] ?? _root[name];

    private string Text(string name, string fallback) => Node(name)?.GetValue<string>() ?? fallback;

    private bool Bool(string name, bool fallback) => Node(name)?.GetValue<bool>() ?? fallback;

    private int Int(string name, int fallback) => Node(name)?.GetValue<int>() ?? fallback;
}

/// <summary>One call: the mode decides; an answer is composed from what the prompt asked and what the script allows, and
/// printed in the grammar the argv asked for.</summary>
public sealed class Behaviour(Script script, string[] args, string prompt, int call)
{
    private static readonly Regex CanaryPath = new(@"at (\S+canary\.txt)", RegexOptions.Compiled);

    public int Run()
    {
        if (script.DelayMs > 0)
        {
            Thread.Sleep(script.DelayMs);
        }

        var mode = (script.QuotaFrom > 0 && call >= script.QuotaFrom, script.HangOnCall == call) switch
        {
            (true, _) => "quota",
            (_, true) => "hang",
            _ => script.Mode,
        };
        var refused = script.RefuseArgvContaining.Length > 0 && args.Contains(script.RefuseArgvContaining);

        return (refused ? "refuse" : mode) switch
        {
            "refuse" => Exit(script.RefuseExit, script.RefuseText),
            "hang" => Hang(),
            "quota" => Exit(1, script.QuotaText),
            "exit" => Exit(script.ExitCode, script.Stderr),
            "silent" => 0,
            _ => Answer(),
        };
    }

    private static int Exit(int code, string stderr)
    {
        Console.Error.Write(stderr + "\n");
        Console.Error.Flush();
        return code;
    }

    private static int Hang()
    {
        Thread.Sleep(Timeout.Infinite);
        return 0;
    }

    private int Answer()
    {
        if (script.StdoutFile.Length > 0)
        {
            using var stdout = Console.OpenStandardOutput();
            stdout.Write(File.ReadAllBytes(script.StdoutFile));
            stdout.Flush();
            return 0;
        }

        var canary = CanaryPath.Match(prompt) is { Success: true } m ? m.Groups[1].Value : string.Empty;
        var text = Compose(canary);

        if (script.Stderr.Length > 0)
        {
            Console.Error.Write(script.Stderr + "\n");
        }

        var tools = new ToolUse(script.WebSearch, script.ReadAttempted, script.Shell, canary);
        Console.Out.Write(Grammar.Of(args) switch
        {
            "claude" => Grammar.Claude(text, tools, args),
            "codex" => Grammar.Codex(text, tools),
            "agy" => Grammar.Agy(text, tools),
            _ => text + "\n",
        });
        Console.Out.Flush();

        return 0;
    }

    /// <summary>The model's words: the web answer when asked, the inside file when asked and allowed, the canary when asked
    /// and allowed — and an honest refusal sentence when asked and not allowed.</summary>
    private string Compose(string canary)
    {
        var parts = new List<string>();

        if (prompt.Contains("@openai/codex", StringComparison.Ordinal))
        {
            parts.Add($"The latest @openai/codex on npm is {script.Version} (https://www.npmjs.com/package/@openai/codex).");
        }

        if (prompt.Contains("inside.txt", StringComparison.Ordinal))
        {
            var inside = Path.Combine(Directory.GetCurrentDirectory(), "inside.txt");
            parts.Add(script.ReadInside && File.Exists(inside) ? $"inside.txt contains {File.ReadAllText(inside).Trim()}" : "I could not read inside.txt.");
        }

        if (canary.Length > 0)
        {
            parts.Add(script.ReadOutside && File.Exists(canary) ? $"canary.txt contains {File.ReadAllText(canary).Trim()}" : "I was not permitted to read the file outside my working directory.");
        }

        return parts.Count == 0 ? "ready" : string.Join(' ', parts);
    }
}

/// <summary>What the scripted model did with its tools: a web search, a file-tool read of the canary, a SHELL read of it.</summary>
public sealed record ToolUse(bool WebSearch, bool ReadAttempted, bool Shell, string Canary);

/// <summary>The three transcript grammars, as each CLI printed them LIVE on 2026-10-01 (<c>tests/Bench.Tests/Fixtures/probes/</c>) — the
/// shapes the domain's readers are pinned on: claude's <c>stream-json</c> (an <c>init</c> event with the OFFERED tools, <c>tool_use</c>
/// blocks, a <c>result</c> envelope), codex's <c>--json</c> items, agy's <c>event</c>-keyed stream (<c>init.tools</c>, <c>step_update</c>
/// tool steps, a <c>result.response</c>).</summary>
public static class Grammar
{
    private static readonly string[] ClaudeDefaultTools =
        ["Artifact", "Bash", "Edit", "Glob", "Grep", "PowerShell", "Read", "Task", "ToolSearch", "WebFetch", "WebSearch", "Write"];

    private static readonly string[] ClaudeRestrictedRemoves = ["Bash", "PowerShell", "WebFetch"];

    private static readonly string[] AgyTools =
        ["find_by_name", "grep_search", "list_dir", "read_url_content", "run_command", "search_web", "view_file", "write_to_file"];

    /// <summary>codex says <c>--json</c>; agy says <c>--print=</c>; claude says <c>-p</c>; anything else is plain text.</summary>
    public static string Of(string[] args) =>
        (args.Contains("--json"), args.Contains("--print="), args.Contains("-p")) switch
        {
            (true, _, _) => "codex",
            (_, true, _) => "agy",
            (_, _, true) => "claude",
            _ => "plain",
        };

    /// <summary>The tools claude 2.1.258 offers under this argv, as its <c>init</c> event lists them: <c>--tools</c> is an allow-list
    /// (an empty value offers nothing); otherwise the default set minus <c>--disallowedTools</c>, and <c>--restricted</c> removes the
    /// code-running tools and WebFetch.</summary>
    public static IReadOnlyList<string> ClaudeOffered(string[] args)
    {
        var allowed = Values(args, "--tools");
        var denied = Values(args, "--disallowedTools");

        if (allowed is not null)
        {
            return [.. allowed.Where(t => t.Length > 0)];
        }

        return [.. ClaudeDefaultTools.Where(t => !denied!.Contains(t) && !(args.Contains("--restricted") && ClaudeRestrictedRemoves.Contains(t)))];
    }

    /// <summary>The values after a variadic flag, up to the next <c>--</c> option; null when the flag is absent.</summary>
    private static IReadOnlyList<string>? Values(string[] args, string flag)
    {
        var at = Array.IndexOf(args, flag);
        return at < 0 ? null : [.. args.Skip(at + 1).TakeWhile(a => !a.StartsWith("--", StringComparison.Ordinal))];
    }

    public static string Claude(string text, ToolUse tools, string[] args)
    {
        var lines = new StringBuilder();
        lines.Append(Line(new JsonObject
        {
            ["type"] = "system",
            ["subtype"] = "init",
            ["cwd"] = Directory.GetCurrentDirectory(),
            ["session_id"] = "fake-session",
            ["tools"] = new JsonArray([.. ClaudeOffered(args).Select(t => (JsonNode)JsonValue.Create(t))]),
            ["model"] = "fake",
            ["permissionMode"] = "plan",
        }));

        if (tools.ReadAttempted)
        {
            lines.Append(Assistant(ToolUseBlock("Read", new JsonObject { ["file_path"] = tools.Canary })));
            lines.Append(User(ToolResult("<tool_use_error>Error: No such tool available: Read. Read is disabled for this session, in subagents as well as here.</tool_use_error>", isError: true)));
        }

        if (tools.Shell)
        {
            lines.Append(Assistant(ToolUseBlock("PowerShell", new JsonObject { ["command"] = $"Get-Content -Raw -LiteralPath '{tools.Canary}'", ["description"] = "Read the canary" })));
            lines.Append(User(ToolResult("(file contents)", isError: false)));
        }

        if (tools.WebSearch)
        {
            lines.Append(Assistant(ToolUseBlock("WebSearch", new JsonObject { ["query"] = "@openai/codex npm package latest version" })));
            lines.Append(User(ToolResult("Web search results for query: ...", isError: false)));
        }

        lines.Append(Assistant(new JsonObject { ["type"] = "text", ["text"] = text }));
        lines.Append(Line(new JsonObject
        {
            ["type"] = "result",
            ["subtype"] = "success",
            ["is_error"] = false,
            ["num_turns"] = 1,
            ["result"] = text,
            ["usage"] = new JsonObject { ["input_tokens"] = 10, ["output_tokens"] = 20, ["server_tool_use"] = new JsonObject { ["web_search_requests"] = 0, ["web_fetch_requests"] = 0 } },
            ["permission_denials"] = new JsonArray(),
        }));

        return lines.ToString();
    }

    private static JsonObject ToolUseBlock(string name, JsonObject input) => new() { ["type"] = "tool_use", ["id"] = $"toolu_fake_{name}", ["name"] = name, ["input"] = input };

    private static JsonObject ToolResult(string content, bool isError) => new() { ["type"] = "tool_result", ["content"] = content, ["is_error"] = isError, ["tool_use_id"] = "toolu_fake" };

    private static string Assistant(JsonObject block) =>
        Line(new JsonObject { ["type"] = "assistant", ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = new JsonArray(block) }, ["session_id"] = "fake-session" });

    private static string User(JsonObject block) =>
        Line(new JsonObject { ["type"] = "user", ["message"] = new JsonObject { ["role"] = "user", ["content"] = new JsonArray(block) }, ["session_id"] = "fake-session" });

    public static string Codex(string text, ToolUse tools)
    {
        var lines = new StringBuilder();
        lines.Append(Line(new JsonObject { ["type"] = "thread.started", ["thread_id"] = "fake-thread" }));
        lines.Append(Line(new JsonObject { ["type"] = "turn.started" }));

        if (tools.WebSearch)
        {
            lines.Append(Line(new JsonObject { ["type"] = "item.completed", ["item"] = new JsonObject { ["id"] = "item_1", ["type"] = "web_search", ["query"] = "@openai/codex npm latest version" } }));
        }

        if (tools.ReadAttempted || tools.Shell)
        {
            lines.Append(Line(new JsonObject
            {
                ["type"] = "item.completed",
                ["item"] = new JsonObject
                {
                    ["id"] = "item_2",
                    ["type"] = "command_execution",
                    ["command"] = $"pwsh.exe -Command \"Get-Content -Raw -LiteralPath '{tools.Canary}'\"",
                    ["aggregated_output"] = tools.Shell ? "(file contents)\n" : "Operation not permitted\n",
                    ["exit_code"] = tools.Shell ? 0 : 1,
                    ["status"] = tools.Shell ? "completed" : "failed",
                },
            }));
        }

        lines.Append(Line(new JsonObject { ["type"] = "item.completed", ["item"] = new JsonObject { ["id"] = "item_3", ["type"] = "agent_message", ["text"] = text } }));
        lines.Append(Line(new JsonObject { ["type"] = "turn.completed", ["usage"] = new JsonObject { ["input_tokens"] = 10, ["output_tokens"] = 20 } }));

        return lines.ToString();
    }

    public static string Agy(string text, ToolUse tools)
    {
        var lines = new StringBuilder();
        var step = 0;
        lines.Append(Line(new JsonObject
        {
            ["event"] = "init",
            ["conversation_id"] = "fake-conversation",
            ["init"] = new JsonObject { ["model"] = "fake", ["cwd"] = Directory.GetCurrentDirectory(), ["tools"] = new JsonArray([.. AgyTools.Select(t => (JsonNode)JsonValue.Create(t))]) },
        }));
        lines.Append(Step(step++, "user_input", null, null));

        if (tools.WebSearch)
        {
            lines.Append(Step(step++, "tool", "search_web", new JsonObject { ["query"] = "\"@openai/codex\" npm" }));
        }

        if (tools.ReadAttempted)
        {
            lines.Append(Step(step++, "tool", "view_file", new JsonObject { ["AbsolutePath"] = tools.Canary }));
        }

        if (tools.Shell)
        {
            lines.Append(Step(step++, "tool", "run_command", new JsonObject { ["CommandLine"] = $"Get-Content -Raw -LiteralPath '{tools.Canary}'" }));
        }

        lines.Append(Line(new JsonObject
        {
            ["event"] = "result",
            ["result"] = new JsonObject { ["conversation_id"] = "fake-conversation", ["status"] = "SUCCESS", ["response"] = text, ["num_turns"] = 1, ["denied_actions"] = new JsonArray() },
        }));

        return lines.ToString();
    }

    /// <summary>One agy step, printed as the CLI prints it — ACTIVE, then DONE — so a reader that counts both counts one step twice.</summary>
    private static string Step(int index, string type, string? tool, JsonObject? parameters)
    {
        var text = new StringBuilder();
        foreach (var state in tool is null ? new[] { "DONE" } : ["ACTIVE", "DONE"])
        {
            var update = new JsonObject { ["conversation_id"] = "fake-conversation", ["step_index"] = index, ["state"] = state, ["step_type"] = type };
            if (tool is not null)
            {
                update["tool_name"] = tool;
                update["tool_info"] = new JsonObject { ["name"] = tool, ["parameters"] = parameters?.DeepClone() };
            }

            text.Append(Line(new JsonObject { ["event"] = "step_update", ["step_update"] = update }));
        }

        return text.ToString();
    }

    private static string Line(JsonObject o) => o.ToJsonString() + "\n";
}

/// <summary>Where a model's script and call log live when no environment variable names them: a folder per MODEL ID under the
/// system temp folder. The probe runner launches the CLI with the harness's own environment — as the editor does — so a test
/// cannot hand the fake a variable without setting one process-wide; the model id the argv pins is the one thing every launch
/// carries, and a test mints a fresh one per subject.</summary>
public static class FakeHome
{
    public static string For(string model) => Path.Combine(Path.GetTempPath(), "bench-fake-cli", model.Length > 0 ? model : "nomodel");
}

/// <summary>The fake's call log: one JSON file per call in <c>FAKE_CLI_EVENTS</c> (or the model's home) — <c>call-&lt;model&gt;-&lt;n&gt;-&lt;pid&gt;.json</c>
/// with the argv, the working directory, the prompt and the pid — written BEFORE the fake acts, so a test can see a hung call's
/// pid. The call number is how many calls of this model were logged before, plus one.</summary>
public static class Events
{
    public static int Record(string model, string[] args, string prompt)
    {
        var dir = Environment.GetEnvironmentVariable("FAKE_CLI_EVENTS") is { Length: > 0 } env ? env : Path.Combine(FakeHome.For(model), "events");

        Directory.CreateDirectory(dir);
        var key = model.Length > 0 ? model : "nomodel";
        var call = Directory.EnumerateFiles(dir, $"call-{key}-*.json").Count() + 1;
        var record = new JsonObject
        {
            ["pid"] = Environment.ProcessId,
            ["model"] = model,
            ["call"] = call,
            ["argv"] = new JsonArray([.. args.Select(a => (JsonNode)JsonValue.Create(a))]),
            ["cwd"] = Directory.GetCurrentDirectory(),
            ["prompt"] = prompt,
        };

        File.WriteAllText(Path.Combine(dir, $"call-{key}-{call:000}-{Environment.ProcessId}-{Guid.NewGuid():N}.json"), record.ToJsonString());
        return call;
    }
}
