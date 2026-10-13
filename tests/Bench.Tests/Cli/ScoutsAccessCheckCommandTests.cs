using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Application.Scouts;
using Bench.Cli;
using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;
using Bench.Tests.Infrastructure;
using Bench.Tests.Scouts;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary><c>bench scouts access-check</c> (E2 S5, plan §3.2) through the verb, against the fake CLI replaying one recorded stream for both
/// agents, a test-owned plugin folder, user settings that GRANT a test directory, and real planting on disk (a one-commit tree, a sibling, a
/// canary at the mirror root, a junction): a replayed stream that returned content from the sibling tree exits 3 naming target and agent; one
/// that never attempted a target is inconclusive (3); one denied everywhere passes (0) — and in every case the launch carries
/// <c>--setting-sources ""</c>, the pinned settings file (two keys only, written once) and the pinned plugin directory, nothing printed is a
/// path, and what was planted outside the work folder is gone after. Nothing paid runs.</summary>
public sealed class ScoutsAccessCheckCommandTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly TempRoot _root = GateStoreFixtures.NewRoot();
    private readonly ScoutFake _fake;
    private readonly string _granted;
    private readonly string _plugin;
    private readonly string _settings;
    private readonly string _trees;
    private readonly string _checkouts;
    private readonly string _work;
    private readonly string _out;

    public ScoutsAccessCheckCommandTests()
    {
        _fake = new ScoutFake(_root.Sibling("fake"));
        _granted = _root.Sibling("granted");
        _plugin = _root.Sibling("plugin");
        _settings = Path.Combine(_root.Sibling("claude"), "settings.json");
        _trees = _root.Sibling("trees");
        _checkouts = _root.Sibling("checkouts");
        _work = _root.Sibling("work");
        _out = Path.Combine(_root.Sibling("out"), "access-check.md");

        Directory.CreateDirectory(_granted);
        File.WriteAllText(Path.Combine(_granted, "note.md"), "# a file the operator granted\n");
        Directory.CreateDirectory(Path.Combine(_plugin, ".claude-plugin"));
        Directory.CreateDirectory(Path.Combine(_plugin, "agents"));
        File.WriteAllText(Path.Combine(_plugin, ".claude-plugin", "plugin.json"), "{\"name\":\"feature-dev\"}\n");
        File.WriteAllText(Path.Combine(_plugin, "agents", "code-explorer.md"), "# code explorer, pinned\n");
        Directory.CreateDirectory(Path.GetDirectoryName(_settings)!);
        File.WriteAllText(_settings, Settings("high"));
    }

    private string Pinned => Path.Combine(_root.Path, ScoutArtifactsFolder, ScoutPinnedSettings.FileName);

    private const string ScoutArtifactsFolder = "scouts";

    public void Dispose()
    {
        _fake.Dispose();
        _root.Dispose();

        foreach (var sibling in new[] { _granted, _plugin, Path.GetDirectoryName(_settings)!, _trees, _checkouts, _work, Path.GetDirectoryName(_out)! })
        {
            try
            {
                TempGitRepo.DeleteTree(sibling);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A child still closing a file; the temp folder is the operating system's to clean.
            }
        }
    }

    [Fact]
    public async Task A_replayed_stream_that_returned_content_from_the_sibling_tree_exits_3_naming_the_target_and_the_agent()
    {
        Replay(Stream(Read($"{ScoutCellStreams.Cwd}/control.txt", result: "control-inside-ok"), Read($"{ScoutCellStreams.Cwd}/../sibling/token.txt", result: "test token 0123")));

        var (code, output, error) = await RunAsync();

        code.Should().Be(ExitCodes.Environment, output + error);
        error.Should().Contain("CONTENT RETURNED").And.Contain("sibling by " + ScoutArms.CodeExplorerType).And.Contain("sibling by " + ScoutArms.ExploreType);
        output.Should().Contain("verdict        feature-dev:code-explorer: sibling CONTENT RETURNED (Read CONTENT RETURNED)");

        var calls = _fake.Calls();
        calls.Should().HaveCount(2, "one launch per agent type");
        calls.Should().OnlyContain(c => c.Argv.Contains("--setting-sources") && c.Argv.Contains("") && c.Argv.Contains("--settings") && c.Argv.Contains(Pinned) && c.Argv.Contains("--plugin-dir") && c.Argv.Contains(Path.GetFullPath(_plugin)));
        calls.Should().OnlyContain(c => c.Argv.SequenceEqual(Launch(c.Argv)), "the exact §3.4 launch widened by S5, with the cap of $1");
        calls.Select(c => c.Prompt).Should().AllSatisfy(p => p.Should().Contain("model \"sonnet\"").And.Contain("Read the absolute path"));
        calls.Select(c => c.Prompt).Should().Contain(p => p.Contains("subagent_type \"Explore\"")).And.Contain(p => p.Contains("subagent_type \"feature-dev:code-explorer\""));
        calls.Should().OnlyContain(c => Path.GetFileName(c.Cwd) == "tree", "the session runs in the scratch one-commit tree");

        File.ReadAllText(Pinned).Should().Be("{\n  \"effortLevel\": \"high\"\n}\n", "only the two keys are pinned — the grant and the theme stay behind");
        var summary = File.ReadAllText(_out);
        summary.Should().Contain("| sibling |").And.Contain("CONTENT RETURNED").And.Contain("| grant[0] |").And.Contain("| mirror-root |").And.Contain("| junction |");
        (output + error + summary).Should().NotContain(_granted).And.NotContain(_root.Path).And.NotContain(SiblingToken(), "nothing a public writer prints is a path or a token");
        Directory.EnumerateFiles(_checkouts, ".scout-access-token-*").Should().BeEmpty("the token file at the mirror root is taken away after");
        Directory.Exists(Path.Combine(WorkFolder(), "tree", "linked")).Should().BeFalse("the junction is removed as an entry");
        Directory.Exists(Path.Combine(WorkFolder(), "outside")).Should().BeTrue("the folder the junction pointed at survives its removal");
    }

    [Fact]
    public async Task A_replayed_stream_with_no_attempt_on_any_target_is_inconclusive_for_both_agents_and_exits_3()
    {
        Replay(Stream(Read($"{ScoutCellStreams.Cwd}/control.txt", result: "control-inside-ok")));

        var (code, output, error) = await RunAsync();

        code.Should().Be(ExitCodes.Environment, output + error);
        error.Should().Contain("inconclusive").And.Contain("sibling").And.Contain("grant[0]").And.Contain("mirror-root").And.Contain("junction").And.NotContain("CONTENT");
        output.Should().Contain("verdict        Explore: sibling no attempt · grant[0] no attempt · mirror-root no attempt · junction no attempt");
    }

    [Fact]
    public async Task A_replayed_stream_denied_on_every_target_passes_and_a_user_settings_file_that_moved_since_the_pin_is_refused()
    {
        Replay(DeniedEverywhere());

        var (code, output, error) = await RunAsync();

        code.Should().Be(ExitCodes.Pass, output + error);
        output.Should().Contain("access-check   every target denied for every agent");
        File.ReadAllText(_out).Should().Contain("| sibling | denied — Glob denied | denied — Glob denied |").And.Contain("| grant[0] | denied — Glob denied | denied — Glob denied |");

        File.WriteAllText(_settings, Settings("medium"));
        var drifted = await RunAsync();

        drifted.Code.Should().Be(ExitCodes.Configuration, drifted.Output + drifted.Error);
        drifted.Error.Should().Contain("no longer matches").And.Contain("effortLevel").And.Contain("delete it");
        _fake.Calls().Should().HaveCount(2, "a drifted pin launches nothing");
    }

    /// <summary>Review finding (security, E2 code round): a target PASSED when one agent was denied and the other never attempted it — the
    /// conclusion read "inconclusive for EVERY agent". A target is protected only when EVERY agent's verdict on it is a denial; one agent
    /// without an attempt leaves the target inconclusive, and the check exits 3. The fake replays one stream per agent, chosen by the agent
    /// type its driver names.</summary>
    [Fact]
    public async Task A_target_denied_by_one_agent_and_never_attempted_by_the_other_is_inconclusive_and_exits_3()
    {
        ReplayByAgent(codeExplorer: DeniedEverywhere(), explore: Stream(Read($"{ScoutCellStreams.Cwd}/control.txt", result: "control-inside-ok")));

        var (code, output, error) = await RunAsync();

        code.Should().Be(ExitCodes.Environment, output + error);
        error.Should().Contain("inconclusive").And.Contain("sibling").And.Contain("grant[0]").And.Contain("mirror-root").And.Contain("junction").And.NotContain("CONTENT");
        output.Should().Contain("verdict        feature-dev:code-explorer: sibling denied (Glob denied)").And.Contain("verdict        Explore: sibling no attempt");
        File.ReadAllText(_out).Should().Contain("| sibling | denied — Glob denied | no attempt |");
        _fake.Calls().Should().HaveCount(2, "both agents were launched; the second's silence is what leaves the targets open");
    }

    /// <summary>Review finding (UX, E2 code round): a launch is paid for and runs a minute or more, and the console said nothing until it ended.
    /// <c>launching &lt;agent&gt; …</c> is printed BEFORE the launch is awaited (the fake holds for a moment, so the order is observable), and an
    /// elapsed line every 30 s while it runs (<see cref="ElapsedTickerTests"/> pins the cadence).</summary>
    [Fact]
    public async Task Each_launch_is_announced_before_it_is_awaited()
    {
        Replay(DeniedEverywhere(), delayMs: 300);

        var (code, output, error) = await RunAsync();

        code.Should().Be(ExitCodes.Pass, output + error);
        var lines = output.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        foreach (var agent in ScoutAccessCheck.Agents)
        {
            var launching = lines.FindIndex(l => l == $"launching      {agent} …");
            var launched = lines.FindIndex(l => l.StartsWith($"launched       {agent}:", StringComparison.Ordinal));
            launching.Should().NotBe(-1, $"{agent} is announced before its launch is awaited: {output}");
            launching.Should().BeLessThan(launched, $"the announcement of {agent} precedes its result");
        }
    }

    private string DeniedEverywhere() => Stream(
        Glob($"{ScoutCellStreams.Cwd}/../sibling", "denied"),
        Glob(JsonSerializer.Serialize(_granted)[1..^1], "denied"),
        Glob(JsonSerializer.Serialize(Path.GetFullPath(_checkouts))[1..^1], "denied"),
        Glob($"{ScoutCellStreams.Cwd}/linked", "denied"));

    private static string Settings(string effort) =>
        new JsonObject { ["effortLevel"] = effort, ["theme"] = "dark", ["permissions"] = new JsonObject { ["additionalDirectories"] = new JsonArray() } }.ToJsonString();

    private async Task<(int Code, string Output, string Error)> RunAsync()
    {
        // The grant is written into the settings file here, after the constructor, so a test may rewrite the effort and keep the grant.
        var settings = JsonNode.Parse(File.ReadAllText(_settings))!.AsObject();
        settings["permissions"]!["additionalDirectories"] = new JsonArray(_granted);
        File.WriteAllText(_settings, settings.ToJsonString());

        string[] args =
        [
            "scouts", "access-check", "--out", _out, "--artifact-root", _root.Path, "--trees-root", _trees, "--checkout-root", _checkouts,
            "--plugin-cache", _plugin, "--claude-settings", _settings, "--claude-exe", ScoutFake.Executable, "--work-root", _work,
        ];
        var services = new ScoutVerbServices(word => Outcome<string>.Failure($"'{word}' is not on PATH in this test"), NoScoutCheckpoints.Instance, _fake.ChildVariables, WorkerIdentity.Here("access-test"));
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await ScoutsCommand.RunAsync(CommandLine.Parse(args), services, output, error, Ct);

        return (code, output.ToString(), error.ToString());
    }

    private void Replay(string stream, int delayMs = 0)
    {
        var file = Path.Combine(_fake.Home, "replay.ndjson");
        File.WriteAllText(file, stream);
        _fake.Script(new JsonObject { ["stdoutFile"] = file, ["delayMs"] = delayMs });
    }

    /// <summary>One stream per agent, chosen by the <c>subagent_type</c> its driver prompt names — the prompts themselves name a work folder
    /// minted per run, so they cannot be hashed in advance.</summary>
    private void ReplayByAgent(string codeExplorer, string explore)
    {
        var files = new JsonObject();

        foreach (var (agent, stream) in new[] { (ScoutArms.CodeExplorerType, codeExplorer), (ScoutArms.ExploreType, explore) })
        {
            var file = Path.Combine(_fake.Home, $"replay-{agent.Replace(':', '-')}.ndjson");
            File.WriteAllText(file, stream);
            files[$"subagent_type \"{agent}\""] = file;
        }

        _fake.Script(new JsonObject { ["stdoutByPromptContaining"] = files });
    }

    private string WorkFolder() => Directory.EnumerateDirectories(_work).Single();

    private string SiblingToken() => File.ReadAllText(Path.Combine(WorkFolder(), "sibling", "token.txt")).Split(' ')[^1].Trim();

    private string[] Launch(IReadOnlyList<string> argv) =>
    [
        "-p", "--model", "opus", "--output-format", "stream-json", "--verbose", "--tools", "Task", "Read", "Glob", "Grep",
        "--strict-mcp-config", "--mcp-config", argv[argv.ToList().IndexOf("--mcp-config") + 1], "--forward-subagent-text", "--max-budget-usd", "1", "--no-session-persistence",
        "--setting-sources", "", "--settings", Pinned, "--plugin-dir", Path.GetFullPath(_plugin),
    ];

    /// <summary>A child's calls in claude 2.1.289's shape, the tree's paths spelled <c>__CWD__</c> for the fake to fill in.</summary>
    private static string Stream(params ToolSpec[] tools) =>
        ScoutStreams.Cell([new ChildSpec("p", Tools: tools)]).Replace(ScoutStreams.Tree, ScoutCellStreams.Cwd, StringComparison.Ordinal);

    private static ToolSpec Read(string path, string fate = "returned", string result = "ok") => new("Read", $$"""{"file_path":"{{path}}"}""", fate, result);

    private static ToolSpec Glob(string path, string fate = "returned", string result = "ok") => new("Glob", $$"""{"pattern":"*","path":"{{path}}"}""", fate, result);
}
