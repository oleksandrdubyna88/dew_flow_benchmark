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

        var prompt = Console.IsInputRedirected ? Console.In.ReadToEnd() : string.Empty;
        var call = Events.Record(ModelOf(args), args, prompt);

        return new Behaviour(script, args, prompt, call).Run();
    }

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

    public string Version => Text("version", "0.52.0");

    public string QuotaText => Text("quotaText", "You've hit your usage limit. Visit https://chatgpt.com/codex/settings/usage to purchase more credits.");

    public string RefuseText => Text("refuseText", "error: unexpected argument '--search' found\n\nUsage: codex exec [OPTIONS] [PROMPT]");

    public int RefuseExit => Int("refuseExit", 2);

    public int ExitCode => Int("exitCode", 1);

    public string Stderr => Text("stderr", string.Empty);

    public int DelayMs => Int("delayMs", 0);

    /// <summary>An argv token whose presence makes the fake refuse the launch — how a test plants "this build has no such flag".</summary>
    public string RefuseArgvContaining => Text("refuseArgvContaining", string.Empty);

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
        var canary = CanaryPath.Match(prompt) is { Success: true } m ? m.Groups[1].Value : string.Empty;
        var text = Compose(canary);

        if (script.Stderr.Length > 0)
        {
            Console.Error.Write(script.Stderr + "\n");
        }

        Console.Out.Write(Grammar.Of(args) switch
        {
            "claude" => Grammar.Claude(text, script.WebSearch, script.ReadAttempted, canary),
            "codex" => Grammar.Codex(text, script.WebSearch, script.ReadAttempted, canary),
            "agy" => Grammar.Agy(text, script.WebSearch, script.ReadAttempted, canary, prompt),
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

/// <summary>The three transcript grammars, as each CLI prints them — the shapes the domain's readers are pinned on.</summary>
public static class Grammar
{
    public static string Of(string[] args)
    {
        var json = args.Contains("--json");
        var format = Array.IndexOf(args, "--output-format") is var i and >= 0 && i + 1 < args.Length ? args[i + 1] : string.Empty;

        return (json, format) switch
        {
            (true, _) => "codex",
            (_, "json") => "claude",
            (_, "stream-json") => "agy",
            _ => "plain",
        };
    }

    public static string Claude(string text, bool webSearch, bool readAttempted, string canary)
    {
        var denials = new JsonArray();
        if (readAttempted)
        {
            denials.Add(new JsonObject { ["tool_name"] = "Read", ["tool_use_id"] = "toolu_fake", ["tool_input"] = new JsonObject { ["file_path"] = canary } });
        }

        var result = new JsonObject
        {
            ["type"] = "result",
            ["subtype"] = "success",
            ["is_error"] = false,
            ["num_turns"] = 1,
            ["result"] = text,
            ["usage"] = new JsonObject
            {
                ["input_tokens"] = 10,
                ["output_tokens"] = 20,
                ["server_tool_use"] = new JsonObject { ["web_search_requests"] = webSearch ? 1 : 0 },
            },
            ["permission_denials"] = denials,
        };

        return result.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }

    public static string Codex(string text, bool webSearch, bool readAttempted, string canary)
    {
        var lines = new StringBuilder();
        lines.Append(Line(new JsonObject { ["type"] = "thread.started", ["thread_id"] = "fake-thread" }));
        lines.Append(Line(new JsonObject { ["type"] = "turn.started" }));

        if (webSearch)
        {
            lines.Append(Line(new JsonObject { ["type"] = "item.completed", ["item"] = new JsonObject { ["id"] = "item_1", ["type"] = "web_search", ["query"] = "@openai/codex npm latest version" } }));
        }

        if (readAttempted)
        {
            lines.Append(Line(new JsonObject
            {
                ["type"] = "item.completed",
                ["item"] = new JsonObject { ["id"] = "item_2", ["type"] = "command_execution", ["command"] = $"cat {canary}", ["aggregated_output"] = "cat: Operation not permitted\n", ["exit_code"] = 1, ["status"] = "failed" },
            }));
        }

        lines.Append(Line(new JsonObject { ["type"] = "item.completed", ["item"] = new JsonObject { ["id"] = "item_3", ["type"] = "agent_message", ["text"] = text } }));
        lines.Append(Line(new JsonObject { ["type"] = "turn.completed", ["usage"] = new JsonObject { ["input_tokens"] = 10, ["output_tokens"] = 20 } }));

        return lines.ToString();
    }

    public static string Agy(string text, bool webSearch, bool readAttempted, string canary, string prompt)
    {
        var lines = new StringBuilder();
        var at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        lines.Append(Line(new JsonObject { ["type"] = "init", ["timestamp"] = at, ["session_id"] = "fake-session", ["model"] = "fake" }));
        lines.Append(Line(new JsonObject { ["type"] = "message", ["timestamp"] = at, ["role"] = "user", ["content"] = prompt }));

        if (webSearch)
        {
            lines.Append(Line(new JsonObject { ["type"] = "tool_use", ["timestamp"] = at, ["tool_name"] = "google_web_search", ["tool_id"] = "google_web_search-1", ["parameters"] = new JsonObject { ["query"] = "@openai/codex" } }));
            lines.Append(Line(new JsonObject { ["type"] = "tool_result", ["timestamp"] = at, ["tool_id"] = "google_web_search-1", ["status"] = "success", ["output"] = "npm: @openai/codex" }));
        }

        if (readAttempted)
        {
            lines.Append(Line(new JsonObject { ["type"] = "tool_use", ["timestamp"] = at, ["tool_name"] = "read_file", ["tool_id"] = "read_file-1", ["parameters"] = new JsonObject { ["absolute_path"] = canary } }));
            lines.Append(Line(new JsonObject { ["type"] = "tool_result", ["timestamp"] = at, ["tool_id"] = "read_file-1", ["status"] = "error", ["output"] = "Tool \"read_file\" is not allowed in this session." }));
        }

        lines.Append(Line(new JsonObject { ["type"] = "message", ["timestamp"] = at, ["role"] = "assistant", ["content"] = text }));
        lines.Append(Line(new JsonObject { ["type"] = "result", ["timestamp"] = at, ["status"] = "success", ["stats"] = new JsonObject { ["total_tokens"] = 30 } }));

        return lines.ToString();
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
