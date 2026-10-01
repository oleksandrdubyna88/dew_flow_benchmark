using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FakeCoai;

/// <summary>The fake product's entry point: <c>--version</c> answers like the product and exits; anything else
/// serves newline JSON-RPC on stdin/stdout until stdin closes.</summary>
public static class Program
{
    public static int Main(string[] args)
    {
        var script = Script.Load();

        if (args.Contains("--version"))
        {
            Console.Out.Write($"connect-other-ais {script.VersionText}\n");
            return 0;
        }

        if (args.Contains("--probe-api"))
        {
            return ProbeApi(script, args);
        }

        return new Server(script).Serve();
    }

    /// <summary>The product's <c>--probe-api</c> (the probes, S2): logs the argv, prints the scripted report lines (default: one
    /// 200 row), echoes the vault key on stderr when the script asks — the scrub test's planted leak — and exits as scripted
    /// (0 ok · 65 bad arguments · 78 no vault/no key).</summary>
    private static int ProbeApi(Script script, string[] args)
    {
        var probe = script.Root["probeApi"] as JsonObject ?? [];
        new Events().Write($"probe-api {string.Join(' ', args)}");

        if (probe["echoCredsKey"]?.GetValue<bool>() == true)
        {
            Console.Error.WriteLine($"debug: COAI_CREDS_KEY={Environment.GetEnvironmentVariable("COAI_CREDS_KEY")}");
        }

        if (probe["echoVariable"]?.GetValue<string>() is { Length: > 0 } echoed)
        {
            Console.Error.WriteLine($"debug: {echoed}={Environment.GetEnvironmentVariable(echoed) ?? "<unset>"}");
        }

        foreach (var line in probe["lines"] is JsonArray lines ? lines.Select(l => l?.GetValue<string>() ?? string.Empty) : ["probe-api vendor row: HTTP 200 chat/completions model=fake tokens=12/34"])
        {
            Console.Out.WriteLine(line);
        }

        Console.Out.Flush();
        Console.Error.Flush();
        return probe["exitCode"]?.GetValue<int>() ?? 0;
    }
}

/// <summary>What one run of the fake does, read from the JSON file <c>FAKE_COAI_SCRIPT</c> names. Every field is
/// optional; the defaults are a well-behaved product that passes its plan round and finds one thing.</summary>
public sealed class Script
{
    private Script(JsonObject root) => Root = root;

    public JsonObject Root { get; }

    public string VersionText => Text("versionText", "0.39.0-fake");

    public string ServerVersion => Text("serverVersion", "0.39.0");

    public int ReviewMilliseconds => Root["reviewMs"]?.GetValue<int>() ?? 50;

    public bool Hangs(string tool) => Listed("hang", tool);

    public bool Crashes(string tool) => Listed("crash", tool);

    public bool AnswersNonJson(string tool) => Listed("nonJson", tool);

    /// <summary>A variable the fake accepts and then IGNORES — writes the product default into the session file —
    /// the accepted-and-ignored knob S3.5 exists to catch.</summary>
    public bool Ignores(string variable) => Listed("ignoreEnv", variable);

    /// <summary>The scripted replies for a tool, in call order; the last one repeats.</summary>
    public JsonNode? ReplyFor(string tool, int call) =>
        Root["replies"]?[tool] is JsonArray replies && replies.Count > 0
            ? replies[Math.Min(call, replies.Count - 1)]?.DeepClone()
            : null;

    public JsonArray LedgerFor(string tool) =>
        Root["ledger"]?[tool] is JsonArray rows ? (JsonArray)rows.DeepClone() : [];

    public IEnumerable<string> StderrFor(string tool) =>
        Root["stderr"]?[tool] is JsonArray lines ? lines.Select(l => l?.GetValue<string>() ?? string.Empty) : [];

    public static Script Load()
    {
        var path = Environment.GetEnvironmentVariable("FAKE_COAI_SCRIPT") ?? string.Empty;

        return new Script(path.Length > 0 && File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? [] : []);
    }

    private string Text(string name, string fallback) => Root[name]?.GetValue<string>() ?? fallback;

    private bool Listed(string list, string item) =>
        Root[list] is JsonArray items && items.Any(i => string.Equals(i?.GetValue<string>(), item, StringComparison.Ordinal));
}

/// <summary>One fake product process.</summary>
public sealed class Server(Script script)
{
    private static readonly string[] Passing = ["proceed", "good_enough", "continue_anyway"];

    private readonly Dictionary<string, int> _calls = new(StringComparer.Ordinal);
    private readonly Events _events = new();
    private readonly Lock _stdout = new();
    private string _lastVerdict = string.Empty;
    private int _lastFindings;
    private bool _planProceeded;

    public int Serve()
    {
        _events.Write("open");
        _events.Write($"env {Environment.GetEnvironmentVariable("COAI_CALLER_SESSION")} {Environment.GetEnvironmentVariable("COAI_DATA_DIR")}");
        Console.Error.WriteLine("fake coai-mcp starting");
        if (script.Root["echoCredsKey"]?.GetValue<bool>() == true)
        {
            Console.Error.WriteLine($"debug: COAI_CREDS_KEY={Environment.GetEnvironmentVariable("COAI_CREDS_KEY")}");
        }

        if (script.Root["echoVariable"]?.GetValue<string>() is { Length: > 0 } echoed)
        {
            Console.Error.WriteLine($"debug: {echoed}={Environment.GetEnvironmentVariable(echoed)}");
        }

        Console.Error.Flush();

        try
        {
            while (Console.In.ReadLine() is { } line)
            {
                if (line.Trim().Length > 0)
                {
                    Handle(JsonNode.Parse(line) as JsonObject ?? []);
                }
            }
        }
        finally
        {
            _events.Write("close");
        }

        return 0;
    }

    private void Handle(JsonObject message)
    {
        var method = message["method"]?.GetValue<string>() ?? string.Empty;
        _events.Write($"recv {method}");

        switch (method)
        {
            case "initialize":
                Answer(message, new JsonObject
                {
                    ["protocolVersion"] = "2025-06-18",
                    ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
                    ["serverInfo"] = new JsonObject { ["name"] = "connect-other-ais", ["version"] = script.ServerVersion },
                });
                break;
            case "tools/list":
                Answer(message, new JsonObject { ["tools"] = new JsonArray([.. ToolNames.Select(n => (JsonNode)new JsonObject { ["name"] = n })]) });
                break;
            case "tools/call":
                Call(message);
                break;
            default:
                break; // a notification (notifications/initialized) needs no answer
        }
    }

    private static readonly string[] ToolNames = ["providers", "open", "review_plan", "review_code", "review_feature", "resolve", "status"];

    private void Call(JsonObject message)
    {
        var tool = message["params"]?["name"]?.GetValue<string>() ?? string.Empty;
        var arguments = message["params"]?["arguments"] as JsonObject ?? [];
        var call = _calls.GetValueOrDefault(tool);
        _calls[tool] = call + 1;
        _events.Write($"call {tool} {arguments.ToJsonString()}");

        if (script.Crashes(tool))
        {
            Console.Error.WriteLine($"FAILED: the fake crashes on {tool}");
            Console.Error.Flush();
            Environment.Exit(3);
        }

        if (script.Hangs(tool))
        {
            Thread.Sleep(Timeout.Infinite);
        }

        if (script.Root["rpcErrorWithKey"] is JsonArray failing && failing.Any(t => t?.GetValue<string>() == tool))
        {
            Fail(message, $"the vault refused {Environment.GetEnvironmentVariable("COAI_CREDS_KEY")}");
            return;
        }

        var text = script.AnswersNonJson(tool) ? "this is not json at all" : Reply(tool, call, arguments).ToJsonString();
        Answer(message, new JsonObject { ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }) });
    }

    private JsonNode Reply(string tool, int call, JsonObject arguments) => tool switch
    {
        "review_plan" or "review_code" or "review_feature" when Unticked(tool) is { Length: > 0 } stage =>
            Error($"nothing could review the {stage} stage: no vendor here can run any of the roles this round was going to ask"),
        "review_plan" or "review_feature" => Review(tool, call, arguments),
        "review_code" when !_planProceeded => Error("no plan round has reached 'proceed' in this session — the plan gate comes first (review_plan)"),
        "review_code" => Review(tool, call, arguments),
        "resolve" => Resolve(call, arguments),
        "open" => new JsonObject { ["sessionId"] = "fake0001", ["stage"] = "PlanReview" },
        "providers" => script.ReplyFor(tool, call) ?? new JsonObject { ["vendors"] = new JsonArray() },
        _ => Error($"unknown tool {tool}"),
    };

    private JsonNode Review(string tool, int call, JsonObject arguments)
    {
        var endpoint = Endpoint();
        _events.Write($"review-start {endpoint}");
        Thread.Sleep(script.ReviewMilliseconds);

        foreach (var line in script.StderrFor(tool))
        {
            Console.Error.WriteLine(line.Replace("{data}", DataDir(), StringComparison.Ordinal));
        }

        Console.Error.Flush();
        AppendLedger(tool);
        WriteSession(arguments);
        _events.Write($"review-end {endpoint}");

        var reply = AccountOut() is { Length: > 0 } sentence ? NobodyAnswered(sentence) : script.ReplyFor(tool, call) as JsonObject ?? DefaultReview(tool);
        if (script.Root["replyEchoesKey"]?.GetValue<bool>() == true)
        {
            reply["instruction"] = $"resolve with key {Environment.GetEnvironmentVariable("COAI_CREDS_KEY")}";
        }

        _lastVerdict = reply["verdict"]?.GetValue<string>() ?? string.Empty;
        _lastFindings = (reply["findings"] as JsonArray)?.Count ?? 0;

        return reply;
    }

    private JsonNode Resolve(int call, JsonObject arguments)
    {
        var decisions = JsonNode.Parse(arguments["decisions"]?.GetValue<string>() ?? "[]") as JsonArray ?? [];
        var scripted = script.ReplyFor("resolve", call);

        if (scripted is not null)
        {
            return scripted;
        }

        if (decisions.Count != _lastFindings)
        {
            return Error($"the last round had {_lastFindings} finding(s) and {decisions.Count} decision(s) were sent");
        }

        _planProceeded |= Passing.Contains(_lastVerdict);
        return new JsonObject { ["stage"] = _planProceeded ? "CodeReview" : "PlanReview", ["recordedDecisions"] = decisions.Count };
    }

    private static JsonObject DefaultReview(string tool) => new()
    {
        ["verdict"] = tool == "review_feature" ? "revise" : "proceed",
        ["gatingCount"] = 1,
        ["threshold"] = 6,
        ["reviewers"] = "1 of 1 reviewers answered",
        ["findings"] = new JsonArray(new JsonObject
        {
            ["severity"] = "Major",
            ["category"] = "Reliability",
            ["file"] = "src/Orders/OrderService.cs",
            ["line"] = 42,
            ["title"] = "a finding",
            ["why"] = "because",
            ["fix"] = "fix it",
            ["providers"] = new JsonArray("fake"),
            ["isGating"] = true,
        }),
        ["instruction"] = "resolve every finding",
        ["cost"] = new JsonObject { ["tokensIn"] = 1000, ["tokensOut"] = 100 },
    };

    /// <summary>The failure sentence the script's <c>accountOut</c> map gives THIS process's reviewer (the first vendor
    /// row's id), or empty — the switch a test uses to put one reviewer's account out while another's still answers.</summary>
    private string AccountOut() =>
        script.Root["accountOut"]?[VendorId()]?.GetValue<string>() ?? string.Empty;

    /// <summary>A round no reviewer answered, worded as the product words it: the reviewer, its role, and what failed.</summary>
    private static JsonObject NobodyAnswered(string sentence)
    {
        var line = $"0 of 1 reviewers answered; failed: {VendorId()}/PlanCritique: {sentence}";

        return new JsonObject
        {
            ["verdict"] = "call_human",
            ["gatingCount"] = 0,
            ["threshold"] = 6,
            ["reviewers"] = line,
            ["findings"] = new JsonArray(),
            ["instruction"] = $"Rounds exhausted: no reviewer answered — nothing was reviewed. {line}. A human decides.",
            ["cost"] = new JsonObject { ["tokensIn"] = 0, ["tokensOut"] = 0 },
        };
    }

    private static string VendorId() =>
        (JsonNode.Parse(Environment.GetEnvironmentVariable("COAI_VENDORS") ?? "[]") as JsonArray)?.FirstOrDefault()?["id"]?.GetValue<string>() ?? string.Empty;

    private static JsonObject Error(string text) => new() { ["error"] = text };

    private void AppendLedger(string tool)
    {
        var rows = script.LedgerFor(tool);
        if (rows.Count == 0)
        {
            rows.Add(new JsonObject
            {
                ["utc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                ["provider"] = "fake",
                ["model"] = "fake-model",
                ["role"] = "PlanCritique",
                ["stage"] = StageOf(tool),
                ["seconds"] = 1.5,
                ["tokensIn"] = 1000,
                ["tokensOut"] = 100,
                ["costUsd"] = 0.01,
                ["outcome"] = "ok",
                ["email"] = string.Empty,
                ["kind"] = "review",
                ["tokensCached"] = 0,
                ["costNote"] = string.Empty,
            });
        }

        var dataDir = DataDir();
        Directory.CreateDirectory(dataDir);
        File.AppendAllLines(Path.Combine(dataDir, "usage.jsonl"), rows.Select(r => r!.ToJsonString()));
    }

    private static string StageOf(string tool) => tool switch
    {
        "review_plan" => "PlanReview",
        "review_code" => "CodeReview",
        _ => "FeatureReview",
    };

    /// <summary>A session file shaped like the product's: <c>state.repoPath</c>, <c>state.branch</c>,
    /// <c>state.config</c> with the rounds and thresholds the environment asked for — unless the script says to
    /// ignore a variable, in which case the product default is written, exactly as a knob accepted and ignored.</summary>
    private void WriteSession(JsonObject arguments)
    {
        var repo = arguments["repoPath"]?.GetValue<string>() ?? string.Empty;
        var branch = arguments["branch"]?.GetValue<string>() ?? arguments["planPath"]?.GetValue<string>() ?? string.Empty;
        var sessions = Path.Combine(DataDir(), "sessions");
        Directory.CreateDirectory(sessions);
        var name = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(repo + "|" + branch)))[..16];

        var session = new JsonObject
        {
            ["state"] = new JsonObject
            {
                ["sessionId"] = "fake0001",
                ["repoPath"] = repo,
                ["branch"] = branch,
                ["config"] = new JsonObject
                {
                    ["roles"] = new JsonObject
                    {
                        ["PlanCritique"] = new JsonObject
                        {
                            ["maxRounds"] = Knob("COAI_ROUNDS_PLANCRITIQUE", 1),
                            ["threshold"] = Knob("COAI_THRESHOLD_PLANCRITIQUE", 6),
                            ["enabled"] = true,
                        },
                    },
                    ["onExhausted"] = Word("COAI_ON_EXHAUSTED", "good_enough"),
                },
            },
            ["rounds"] = new JsonArray(),
        };

        File.WriteAllText(Path.Combine(sessions, $"session-{name}.json"), session.ToJsonString());
    }

    private int Knob(string variable, int fallback) =>
        !script.Ignores(variable) && int.TryParse(Environment.GetEnvironmentVariable(variable), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private string Word(string variable, string fallback) =>
        !script.Ignores(variable) && Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value ? value : fallback;

    private static string DataDir() => Environment.GetEnvironmentVariable("COAI_DATA_DIR") ?? Path.Combine(Path.GetTempPath(), "fake-coai-data");

    /// <summary>The product refuses a round no vendor row is ticked for (<c>"plan"</c>, <c>"code"</c>, <c>"feature"</c>) — S7.3's
    /// code gate, 2026-09-29, sent rows ticked for code only, and the real product refused every plan round the code
    /// protocol needs first; this fake answered them, so no driver test saw it. Empty when some row is ticked, or when
    /// the run names no vendors at all.</summary>
    private static string Unticked(string tool)
    {
        var vendors = JsonNode.Parse(Environment.GetEnvironmentVariable("COAI_VENDORS") ?? "[]") as JsonArray ?? [];
        var key = tool switch { "review_plan" => "plan", "review_code" => "code", _ => "feature" };

        return vendors.Count == 0 || vendors.OfType<JsonObject>().Any(v => v[key]?.GetValue<bool>() == true) ? string.Empty : StageOf(tool);
    }

    /// <summary>Where this review would go: the first vendor row's base url, or its runtime word.</summary>
    private static string Endpoint()
    {
        var vendors = Environment.GetEnvironmentVariable("COAI_VENDORS") ?? "[]";
        var row = (JsonNode.Parse(vendors) as JsonArray)?.FirstOrDefault() as JsonObject;
        var url = row?["baseUrl"]?.GetValue<string>() ?? string.Empty;

        return url.Length > 0 ? url : "runtime:" + (row?["runtime"]?.GetValue<string>() ?? "none");
    }

    private void Fail(JsonObject request, string text)
    {
        var reply = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = request["id"]?.DeepClone(), ["error"] = new JsonObject { ["code"] = -32000, ["message"] = text } };

        lock (_stdout)
        {
            Console.Out.Write(reply.ToJsonString() + "\n");
            Console.Out.Flush();
        }
    }

    private void Answer(JsonObject request, JsonObject result)
    {
        var reply = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = request["id"]?.DeepClone(), ["result"] = result };

        lock (_stdout)
        {
            Console.Out.Write(reply.ToJsonString() + "\n");
            Console.Out.Flush();
        }
    }
}

/// <summary>The fake's event log: one file per process in <c>FAKE_COAI_EVENTS</c>, one line per event —
/// <c>&lt;utc ticks&gt; &lt;pid&gt; &lt;event&gt;</c> — flushed as written, so a crash leaves what happened.</summary>
public sealed class Events
{
    private readonly string _path = Environment.GetEnvironmentVariable("FAKE_COAI_EVENTS") is { Length: > 0 } dir
        ? Path.Combine(dir, $"events-{Environment.ProcessId}-{Guid.NewGuid():N}.log")
        : string.Empty;

    public void Write(string text)
    {
        if (_path.Length == 0)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var line = Encoding.UTF8.GetBytes($"{Stopwatch.GetTimestamp().ToString(CultureInfo.InvariantCulture)} {Environment.ProcessId} {text}\n");

        // Shared for reading AND writing: a test polls this file while the fake runs, and an append refused because a
        // reader holds it would crash the fake mid-review — the test would then be measuring its own poll.
        using var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        stream.Write(line);
    }
}
