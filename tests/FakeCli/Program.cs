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
        // The CLIs this fake stands in for read and write UTF-8 on their pipes, whatever the console's code page — a prompt carrying an
        // em dash must hash the same on both sides of the pipe (the scouts' read-back compares prompt hashes).
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError(), utf8) { AutoFlush = true });
        var script = Script.Load(ModelOf(args));

        if (args.Contains("--version"))
        {
            Console.Out.Write($"{script.VersionText}\n");
            return 0;
        }

        var prompt = PromptOf(args, Console.IsInputRedirected ? new StreamReader(Console.OpenStandardInput(), utf8).ReadToEnd() : string.Empty);
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

    /// <summary>The replay file for THIS call of the model — <c>stdoutByCall.&lt;n&gt;</c>, n being the call number the log gave this launch
    /// (per model, 1-based) — so one test scripts a step's attempts in order: a limited attempt, then its resume (model arms E2.S3). Chosen
    /// before every other replay; a call with no entry falls through to them, exactly as before.</summary>
    public string StdoutForCall(int call) =>
        (Node("stdoutByCall") as JsonObject)?[call.ToString(CultureInfo.InvariantCulture)]?.GetValue<string>() ?? string.Empty;

    /// <summary>When set, a launch whose argv carries <c>--session-id &lt;id&gt;</c> or <c>--resume &lt;id&gt;</c> leaves the empty transcript file claude
    /// leaves for a session, before it answers (model arms E2.S3, E2 §8 item 36); absent = false, and the fake writes nothing of the kind.</summary>
    public bool Transcripts => Bool("transcripts", false);

    /// <summary>The replay file for THIS prompt — <c>stdoutByPrompt.&lt;sha256 of the prompt&gt;</c> — so every scout cell, all launched
    /// <c>--model opus</c>, replays its own recorded stream; falls back to <see cref="StdoutFile"/>.</summary>
    public string StdoutFor(string prompt) =>
        Node("stdoutByPrompt")?[Sha256(prompt)]?.GetValue<string>() is { Length: > 0 } file ? file
            : StdoutContaining(prompt) is { Length: > 0 } partial ? partial
            : StdoutFile;

    /// <summary>A replay chosen by a SUBSTRING of the prompt — <c>stdoutByPromptContaining.&lt;text&gt;</c>, the first entry whose key the prompt
    /// contains — for a test that cannot hash its prompts in advance (the access check's name a work folder minted per run) but can tell them
    /// apart by the agent type they name. After the exact hash, before <see cref="StdoutFile"/>.</summary>
    private string StdoutContaining(string prompt) =>
        (Node("stdoutByPromptContaining") as JsonObject)?.FirstOrDefault(kv => prompt.Contains(kv.Key, StringComparison.Ordinal)).Value?.GetValue<string>() ?? string.Empty;

    /// <summary>A replay prints its lines with this pause between them — what a test watching the stream arrive needs; zero = at once.</summary>
    public int LineDelayMs => Int("lineDelayMs", 0);

    /// <summary>A replay HOLDS after this many lines (1-based; zero = never): it flushes them, writes <c>held-&lt;pid&gt;</c> into its home
    /// and sleeps until it is killed — a CLI hung mid-stream, or a cell a test kills at a deterministic point.</summary>
    public int HoldAfterLine => Int("holdAfterLine", 0);

    /// <summary>A replay writes these to STDERR, the n-th (flushed) just before its n-th stdout line — a CLI that warns while it streams, for a
    /// test watching stderr arrive beside stdout (model arms E1.S5). Empty, the default, writes no stderr, as before; a replay holding any
    /// leaves the one-write fast path and goes line by line.</summary>
    public IReadOnlyList<string> StderrBeforeLines => [.. (Node("stderrBeforeLines") as JsonArray ?? []).Select(n => n?.GetValue<string>() ?? string.Empty)];

    /// <summary>The fake plays the scouts' claim ASSESSOR (codex <c>-o</c>): it reads the blinded rows out of the prompt and writes one verdict
    /// per row to the last-message file — <see cref="AssessVerdicts"/> maps a substring of a row to its verdict (first match wins), anything
    /// else gets <see cref="AssessDefault"/>, and a row matching <see cref="AssessSkip"/> is left out of the answer (an unanswered id).</summary>
    public bool Assessor => Node("assessVerdicts") is JsonObject || Bool("assessor", false);

    public JsonObject AssessVerdicts => Node("assessVerdicts") as JsonObject ?? [];

    public string AssessDefault => Text("assessDefault", "right");

    public string AssessSkip => Text("assessSkip", string.Empty);

    public static string Sha256(string text) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    public static Script Load(string model)
    {
        var path = Environment.GetEnvironmentVariable("FAKE_CLI_SCRIPT") is { Length: > 0 } env ? env : Path.Combine(FakeHome.Of(model), "script.json");
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

        var mode = ModeOf();

        // A launch claude would refuse starts no session; one it accepts leaves its transcript before anything else it does (a hang, a quota marker, an answer).
        return mode != "refuse" && Transcripts.Enter(script, args) is { Length: > 0 } refusal ? Exit(1, refusal) : Dispatch(mode);
    }

    private string ModeOf()
    {
        var mode = (script.QuotaFrom > 0 && call >= script.QuotaFrom, script.HangOnCall == call) switch
        {
            (true, _) => "quota",
            (_, true) => "hang",
            _ => script.Mode,
        };

        return script.RefuseArgvContaining.Length > 0 && args.Contains(script.RefuseArgvContaining) ? "refuse" : mode;
    }

    private int Dispatch(string mode) =>
        mode switch
        {
            "refuse" => Exit(script.RefuseExit, script.RefuseText),
            "hang" => Hang(),
            "quota" => Exit(1, script.QuotaText),
            "exit" => Exit(script.ExitCode, script.Stderr),
            "silent" => 0,
            _ => Answer(),
        };

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
        if (script.Assessor && Assessor.LastMessageFile(args) is { Length: > 0 } lastMessage)
        {
            return Assessor.Answer(script, prompt, lastMessage);
        }

        if (script.StdoutForCall(call) is { Length: > 0 } byCall)
        {
            return Replay(byCall);
        }

        if (script.StdoutFor(prompt) is { Length: > 0 } replay)
        {
            return Replay(replay);
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

    /// <summary>A recorded stream printed VERBATIM — except <c>__CWD__</c>, which becomes this process's working directory as a JSON string
    /// body (a synthetic scout stream names its tree's paths that way, so the path audit sees them inside the tree it was launched in). With
    /// no pause and no hold the bytes go out at once, as before; otherwise line by line, flushed, so a reader sees each line as it comes.</summary>
    private int Replay(string file)
    {
        var text = File.ReadAllText(file).Replace("__CWD__", JsonEscaped(Directory.GetCurrentDirectory()), StringComparison.Ordinal);

        if (script.LineDelayMs == 0 && script.HoldAfterLine == 0 && script.StderrBeforeLines.Count == 0)
        {
            using var stdout = Console.OpenStandardOutput();
            stdout.Write(Encoding.UTF8.GetBytes(text));
            stdout.Flush();
            return 0;
        }

        var lines = text.Split('\n');
        var stderr = script.StderrBeforeLines;

        for (var i = 0; i < lines.Length; i++)
        {
            if (i == script.HoldAfterLine && script.HoldAfterLine > 0)
            {
                File.WriteAllText(Path.Combine(FakeHome.Of(Program.ModelOf(args)), $"held-{Environment.ProcessId}"), i.ToString(CultureInfo.InvariantCulture));
                return Hang();
            }

            if (i < stderr.Count)
            {
                Console.Error.Write(stderr[i] + "\n");
                Console.Error.Flush();
            }

            Console.Out.Write(lines[i] + (i < lines.Length - 1 ? "\n" : string.Empty));
            Console.Out.Flush();
            Thread.Sleep(script.LineDelayMs);
        }

        return 0;
    }

    private static string JsonEscaped(string text) => JsonValue.Create(text).ToJsonString()[1..^1];

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

/// <summary>The scouts' claim assessor, replayed: the verdicts a recorded codex gave, keyed by what the row says rather than by its blinded id
/// (the ids are minted fresh on every pass, so a recording keyed by id could never be replayed). Writes the answer where codex <c>-o</c> would
/// and prints codex's progress line on stdout, which with <c>-o</c> is progress, not the answer.</summary>
public static class Assessor
{
    public static string LastMessageFile(string[] args)
    {
        var at = Array.IndexOf(args, "-o");
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : string.Empty;
    }

    public static int Answer(Script script, string prompt, string lastMessageFile)
    {
        var rows = new JsonArray();

        foreach (var row in prompt.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("{\"id\"", StringComparison.Ordinal)))
        {
            if (script.AssessSkip.Length > 0 && row.Contains(script.AssessSkip, StringComparison.Ordinal))
            {
                continue;
            }

            var id = (JsonNode.Parse(row) as JsonObject)?["id"]?.GetValue<string>() ?? string.Empty;
            rows.Add(new JsonObject { ["id"] = id, ["verdict"] = Verdict(script, row), ["note"] = "replayed" });
        }

        File.WriteAllText(lastMessageFile, new JsonObject { ["rows"] = rows }.ToJsonString());
        Console.Out.Write("codex: answer written\n");
        Console.Out.Flush();
        return 0;
    }

    private static string Verdict(Script script, string row) =>
        script.AssessVerdicts.FirstOrDefault(kv => row.Contains(kv.Key, StringComparison.Ordinal)).Value?.GetValue<string>() ?? script.AssessDefault;
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

        return [.. ClaudeDefaultTools.Where(t => !(denied ?? []).Contains(t) && !(args.Contains("--restricted") && ClaudeRestrictedRemoves.Contains(t)))];
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
            lines.Append(User(ToolResult("Read", "<tool_use_error>Error: No such tool available: Read. Read is disabled for this session, in subagents as well as here.</tool_use_error>", isError: true)));
        }

        if (tools.Shell)
        {
            lines.Append(Assistant(ToolUseBlock("PowerShell", new JsonObject { ["command"] = $"Get-Content -Raw -LiteralPath '{tools.Canary}'", ["description"] = "Read the canary" })));
            lines.Append(User(ToolResult("PowerShell", ShellOutput(tools.Canary), isError: false)));
        }

        if (tools.WebSearch)
        {
            lines.Append(Assistant(ToolUseBlock("WebSearch", new JsonObject { ["query"] = "@openai/codex npm package latest version" })));
            lines.Append(User(ToolResult("WebSearch", "Web search results for query: ...", isError: false)));
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

    /// <summary>What a shell read of the canary prints — the file's bytes, as the live PowerShell and pwsh results carried them (S2c: the
    /// verdict reads the token off the whole transcript, tool results included).</summary>
    public static string ShellOutput(string canary) => canary.Length > 0 && File.Exists(canary) ? File.ReadAllText(canary).Trim() : "(no such file)";

    private static JsonObject ToolUseBlock(string name, JsonObject input) => new() { ["type"] = "tool_use", ["id"] = $"toolu_fake_{name}", ["name"] = name, ["input"] = input };

    /// <summary>A result is paired with its call by <c>tool_use_id</c>, as the live stream pairs them — the reader reads a call's fate
    /// (stopped or ran) off that pairing (S2c), so a fake that mismatched the ids would leave every call's fate unknown.</summary>
    private static JsonObject ToolResult(string tool, string content, bool isError) => new() { ["type"] = "tool_result", ["content"] = content, ["is_error"] = isError, ["tool_use_id"] = $"toolu_fake_{tool}" };

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
                    ["aggregated_output"] = tools.Shell ? ShellOutput(tools.Canary) + "\n" : "Operation not permitted\n",
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

/// <summary>The transcript claude leaves for a session (model arms E2.S3, E2 §8 item 36): an EMPTY <c>&lt;home&gt;/projects/&lt;slug&gt;/&lt;id&gt;.jsonl</c>, <c>home</c>
/// being <c>CLAUDE_CONFIG_DIR</c> when it is set and non-empty, else <c>$HOME/.claude</c> — read from the environment, never from the platform's idea of the user's home, so
/// a test that forgot to give the fake one cannot reach the developer's own transcripts; with neither set the fake says so on stderr and writes nothing — and <c>slug</c>
/// this process's working directory with every character outside <c>[A-Za-z0-9]</c> replaced by <c>-</c>. The rule of the module's own path spelling, written a second time
/// on purpose: the fake references nothing of the module, so a disagreement between the two shows in a test instead of cancelling out.
/// <para>
/// The fake accepts no more than claude does: <c>--resume &lt;id&gt;</c> for a session with no transcript is refused with claude's own sentence
/// (<c>No conversation found with session ID: &lt;id&gt;</c>, exit 1), so a runner that resumes a session it never started goes red here as it would live. A launch the
/// fake refuses for another reason (<c>refuseArgvContaining</c>) leaves nothing. The other half, MEASURED on claude 2.1.310 on 2026-10-30: <c>--session-id &lt;id&gt;</c> for an id
/// whose transcript already exists exits 1 with <c>Error: Session ID &lt;id&gt; is already in use.</c> and writes nothing. An existing transcript is left as it is — a resumed session's file is appended to,
/// never truncated.
/// </para></summary>
public static class Transcripts
{
    /// <summary>One session argument of a launch: <c>--session-id</c> starts it, <c>--resume</c> continues it.</summary>
    public sealed record Request(bool Resume, Guid Id);

    /// <summary>Takes the launch in: the sentence claude refuses it with, or empty when it is accepted (the transcript left, or nothing asked for).</summary>
    public static string Enter(Script script, string[] args)
    {
        if (!script.Transcripts || RequestOf(args) is not { } request)
        {
            return string.Empty;
        }

        if (Home() is not { Length: > 0 } home)
        {
            Console.Error.Write("fake-cli: transcripts: neither CLAUDE_CONFIG_DIR nor HOME is set; no transcript left\n");
            return string.Empty;
        }

        return Admit(request, Path.Combine(home, "projects", Slug(Directory.GetCurrentDirectory()), $"{request.Id:D}.jsonl"));
    }

    private static string Admit(Request request, string file) =>
        (request.Resume, File.Exists(file)) switch
        {
            (true, false) => $"No conversation found with session ID: {request.Id:D}",
            (false, true) => $"Error: Session ID {request.Id:D} is already in use.",
            _ => Leave(file),
        };

    private static string Leave(string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.AppendAllText(file, string.Empty);
        return string.Empty;
    }

    /// <summary>The session argument, when its id is a guid (claude refuses anything else, so the fake leaves nothing for it).</summary>
    public static Request? RequestOf(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (IsSessionFlag(args[i]))
            {
                return Guid.TryParse(args[i + 1], out var id) ? new Request(args[i] == "--resume", id) : null;
            }
        }

        return null;
    }

    private static bool IsSessionFlag(string argument) => argument is "--session-id" or "--resume";

    private static string Home() =>
        Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") is { Length: > 0 } configured ? configured
            : Environment.GetEnvironmentVariable("HOME") is { Length: > 0 } home ? Path.Combine(home, ".claude")
            : string.Empty;

    private static string Slug(string workingDirectory) => Regex.Replace(workingDirectory, "[^A-Za-z0-9]", "-");
}

/// <summary>Where a model's script and call log live when no environment variable names them: a folder per MODEL ID under the
/// system temp folder. The probe runner launches the CLI under a MINIMAL replaced environment (S2c) — <c>TEMP</c> passes, nothing the
/// harness owns does — so a test cannot hand the fake a variable at all; the model id the argv pins is the one thing every launch
/// carries, and a test mints a fresh one per subject.</summary>
public static class FakeHome
{
    public static string For(string model) => Path.Combine(Path.GetTempPath(), "bench-fake-cli", model.Length > 0 ? model : "nomodel");

    /// <summary>The home THIS launch uses: <c>FAKE_CLI_HOME</c> when the launcher let it through — a test-owned home that does not depend
    /// on the model, which every scout cell pins to the same <c>opus</c> (the scouts' runner passes it in TESTS only) — else the model's.</summary>
    public static string Of(string model) =>
        Environment.GetEnvironmentVariable("FAKE_CLI_HOME") is { Length: > 0 } home ? home : For(model);
}

/// <summary>The fake's call log: one JSON file per call in <c>FAKE_CLI_EVENTS</c> (or the model's home) — <c>call-&lt;model&gt;-&lt;n&gt;-&lt;pid&gt;.json</c>
/// with the argv, the working directory, the prompt and the pid — written BEFORE the fake acts, so a test can see a hung call's
/// pid. The call number is how many calls of this model were logged before, plus one.</summary>
public static class Events
{
    public static int Record(string model, string[] args, string prompt)
    {
        var dir = Environment.GetEnvironmentVariable("FAKE_CLI_EVENTS") is { Length: > 0 } env ? env : Path.Combine(FakeHome.Of(model), "events");

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
            // The one VALUE the fake reports: whether the launch carried the sub-agent model-guard switch (a measured subject must run unguarded).
            ["subagentModelGuard"] = Environment.GetEnvironmentVariable("SUBAGENT_MODEL_GUARD") ?? string.Empty,
            // The NAMES of the environment the fake was launched under — never the values — so a test can see what the launcher let
            // through (S2c: the probes launch a CLI under a minimal, replaced environment).
            ["env"] = new JsonArray([.. Environment.GetEnvironmentVariables().Keys.Cast<object>().Select(k => (JsonNode)JsonValue.Create(k.ToString() ?? string.Empty)).OrderBy(n => n!.GetValue<string>(), StringComparer.Ordinal)]),
        };

        // Staged under a name `call-*.json` does not match, then renamed: a reader polling the folder (FakeCli.Calls) sees a whole
        // record or none — a direct write left an empty or partial file there for a moment, and a poll in that window threw.
        var name = $"call-{key}-{call:000}-{Environment.ProcessId}-{Guid.NewGuid():N}.json";
        var staged = Path.Combine(dir, $".{name}.tmp");
        File.WriteAllText(staged, record.ToJsonString());
        File.Move(staged, Path.Combine(dir, name));
        return call;
    }
}
