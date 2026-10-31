using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;
using Bench.Cli;
using Bench.Domain.Targets;
using Bench.Infrastructure.Process;
using Bench.Tests.Probes.Driver;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;
using ModelArmsApp = ModelArms;

namespace Bench.Tests.ModelArms;

/// <summary>What one pass through the gate left behind: the exit code, the two writers' text and every call the recording verb received.</summary>
internal sealed record GatePass(int ExitCode, string Output, string Error, IReadOnlyList<ModelArmsApp.ExperimentInput> Calls);

/// <summary>A real experiment root in a scratch folder: <c>&lt;temp&gt;/example/root</c> (mode 0700) holding the worked example's
/// <c>experiment.json</c> with its <c>/example/</c> moved under the scratch folder, and a one-row <c>scenarios.json</c> (both mode 0600), so the root
/// is the operator's own and closed to group and others. The scratch folder is a <see cref="TempFolder"/>: named <c>testsToDelete-modelarms-…</c>
/// on its own last component (common.test-leftovers) and removed on dispose.</summary>
internal sealed class ExperimentRoot : IDisposable
{
    private const string ScenariosText = """[{"id":"S1","kind":"cs","name":"A_scenario"}]""";
    private static readonly UnixFileMode Closed = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private const string PlanBranch = "docs/plan-a-spent-quota-cools-the-model-down";
    private const string PlanFile = "todo/PLAN_a_spent_quota_cools_the_model_down.md";
    private static readonly TimeSpan GitBudget = TimeSpan.FromSeconds(30);

    /// <summary>The host's git, found once through the module's own resolution against the test process's PATH: <c>&lt;temp&gt;/bin/git</c> is a link to it, so the git the
    /// console launches is a real one. Empty where no git resolves (the fixture then cannot be built).</summary>
    private static readonly Lazy<string> HostGit = new(() => ExecutableResolution
        .Resolve("git", (System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator), [], RootFacts.Host, OperatingSystem.IsWindows())
        is ExecutableAnswer.Found found ? found.Real : string.Empty);

    private readonly TempFolder _temp = new();
    private readonly Dictionary<string, string> _variables;

    public ExperimentRoot()
    {
        Root = _temp.Folder("example", "root");
        _variables = Variables(Path.Combine(Temp, "example", "home"));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(Root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var example = Path.Combine(Cli.Repository.Root, "tools", "model-arms", "examples", "part6.experiment.json");
        var moved = File.ReadAllText(example).Replace("/example/", Temp.Replace('\\', '/').TrimEnd('/') + "/example/", StringComparison.Ordinal);
        Write("experiment.json", moved);
        Write("scenarios.json", ScenariosText);
    }

    /// <summary>The experiment root: absolute, normalised, no trailing separator.</summary>
    public string Root { get; }

    /// <summary>The scratch folder that holds <c>example/root</c> (and <c>example/home</c>, the <c>HOME</c> of <see cref="Environment"/>).</summary>
    public string Temp => _temp.Path;

    /// <summary>The test process's variables plus <c>HOME</c> inside the scratch folder; <c>MODEL_ARMS_EXPERIMENT</c> is never carried over.</summary>
    public IReadOnlyDictionary<string, string> Environment => _variables;

    /// <summary>Makes the root validate on this host: the target <c>&lt;temp&gt;/example/target</c> as a git repository holding the plan on its plan branch
    /// (<c>target.base</c> of <c>experiment.json</c> rewritten to that commit), the coai binary and settings the example names, and <c>&lt;temp&gt;/bin</c> with an
    /// empty 0755 file for each executable the example configures — which becomes the only <c>PATH</c> of <see cref="Environment"/>. The facts about the
    /// source are read by the real <c>git</c> on the process's own PATH; the PATH map is what <c>validate</c> resolves the configured executables against.</summary>
    public async Task<ExperimentRoot> WithTargetAsync()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the fixture's git is a link to the host's git; links need a privilege on Windows");
        Assert.False(RealGit.Length == 0, "a git must resolve on the test process's PATH to build the fixture's target");
        var target = _temp.Folder("example", "target");
        await GitAsync(target, "init", "--quiet", "--initial-branch", PlanBranch);
        _temp.File("a plan\n", "example", "target", "todo", "PLAN_a_spent_quota_cools_the_model_down.md");
        await GitAsync(target, "add", PlanFile);
        await GitAsync(target, "-c", "user.name=test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", "the plan");
        var sha = (await GitAsync(target, "rev-parse", "HEAD")).Trim();
        Edit(top => top["target"]!["base"] = sha);

        Executable("example", "coai", "coai-mcp");
        _temp.File("{}", "example", "vault", "connectOtherAis", "wsl-ubuntu", "settings.json");
        Directory.CreateDirectory(Path.Combine(Temp, "bin"));
        File.CreateSymbolicLink(Path.Combine(Temp, "bin", "git"), RealGit);
        foreach (var name in new[] { "claude", "dotnet", "node", "npm", "pwsh.exe" })
        {
            Executable("bin", name);
        }

        _variables["PATH"] = Path.Combine(Temp, "bin");
        return this;
    }

    private byte _tokenCounter = 0xa0;

    /// <summary>Makes <paramref name="arm"/> runnable by <c>run-arm</c> the way <c>make</c> will leave it (E2.S8): its token in <c>arms.map.json</c> (the counter is the byte source, so two arms never share one), <c>chain/made-&lt;arm&gt;</c>,
    /// a completed <c>baseline.json</c>, the arm's clone <c>r/&lt;token&gt;/arm</c> as a git repository of one commit, the two MCP configs through <see cref="McpConfig"/>; every step's <c>model</c> is the fake's id and its <c>minutes</c> one;
    /// and <c>&lt;temp&gt;/bin</c> becomes the only <c>PATH</c>, holding <c>claude</c> as a link to the fake's apphost (which resolves its own real path) and <c>git</c> as a link to the host's git. The fake is told to leave a transcript
    /// (<c>transcripts: true</c>) so a re-entry finds one and resumes; a test that rewrites the script keeps that key (see <see cref="Script"/>). Linux only. <paramref name="keepIsolation"/> keeps the example's <c>isolation</c> block; by default it is REMOVED, so a runner whose run has a block does not warn that the isolation is not applied (E5.S1 question 20) in tests that are not about it.</summary>
    public async Task<ExperimentRoot> MakeArmAsync(string arm, FakeCli fake, bool keepIsolation = false)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the run-arm scenarios link claude to the fake; links and POSIX paths");
        Assert.False(RealGit.Length == 0, "a git must resolve on the test process's PATH to build the arm's clone");
        var map = ArmMap.Extend(CurrentMap(), [arm], () => [0, 0, 0, 0, 0, 0, 0, ++_tokenCounter]);
        Write("arms.map.json", map.ToJson());
        Write($"chain/made-{arm}", "made\n");
        Write("baseline.json", BaselineFile.Write(CompletedBaseline()));
        var paths = new RootPaths(Root).Arm(map.TokenOf(arm).Ok());
        await InitCloneAsync(paths.CloneFolder);
        File.WriteAllText(paths.McpNone, McpConfig.None());
        File.WriteAllText(paths.McpCoai, McpConfig.Coai(ExperimentOf().Coai, paths, _variables));
        Edit(top =>
        {
            foreach (var step in top["steps"]!.AsObject())
            {
                step.Value!["model"] = fake.Model;
                step.Value["minutes"] = 1;
            }

            if (!keepIsolation)
            {
                top.AsObject().Remove("isolation");
            }
        });

        var bin = Directory.CreateDirectory(Path.Combine(Temp, "bin")).FullName;
        LinkTo(Path.Combine(bin, "claude"), FakeCli.Executable);
        LinkTo(Path.Combine(bin, "git"), RealGit);
        _variables["PATH"] = bin;
        _variables["FAKE_CLI_HOME"] = fake.Home;
        fake.Rewrite(Script());
        return this;
    }

    /// <summary>The fake's script with <c>transcripts: true</c> and whatever <paramref name="more"/> adds, so a rewrite cannot lose the key a resume depends on.</summary>
    public static JsonObject Script(JsonObject? more = null)
    {
        var script = more ?? [];
        script["transcripts"] = true;
        return script;
    }

    /// <summary>Commits a file in the arm's clone — the work a step did, as the diff sees it.</summary>
    public async Task CommitInCloneAsync(string arm, string relative, string text)
    {
        var clone = PathsOf(arm).CloneFolder;
        await File.WriteAllTextAsync(Path.Combine(clone, relative), text, TestContext.Current.CancellationToken);
        await GitAsync(clone, "add", relative);
        await GitAsync(clone, "-c", "user.name=test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", "a step's work");
    }

    /// <summary>The paths of <paramref name="arm"/>, from the map the root holds.</summary>
    public ArmPaths PathsOf(string arm) => new RootPaths(Root).Arm(CurrentMap().TokenOf(arm).Ok());

    /// <summary>A folder inside the scratch folder (created), for a helper that needs a place of its own.</summary>
    public string Scratch(params string[] parts) => _temp.Folder(parts);

    private ArmMap CurrentMap() =>
        File.Exists(Path.Combine(Root, "arms.map.json")) ? ArmMap.Read(File.ReadAllText(Path.Combine(Root, "arms.map.json"))).Ok() : ArmMap.Empty;

    /// <summary>The experiment as the root's file holds it now.</summary>
    public Experiment ExperimentOf() =>
        ExperimentFile.Read(File.ReadAllText(Path.Combine(Root, "experiment.json")), new ExperimentLocation(Root, Path.Combine(Temp, "example", "home"), string.Empty, "/x/repo")).Ok();

    private static Baseline CompletedBaseline() =>
        BaselineFile.Compose(CommitSha.Parse("6786582e30d3e771549d05ed719478396f8099cd").Ok(), new DateTimeOffset(2026, 10, 29, 12, 30, 15, TimeSpan.Zero), "wsl-ubuntu", new SuiteRun(true, 0, [], 10), new SuiteRun(true, 0, [], 5), "fixture").Ok();

    private static async Task InitCloneAsync(string clone)
    {
        Directory.CreateDirectory(clone);
        await GitAsync(clone, "init", "--quiet", "--initial-branch", "main");
        await GitAsync(clone, "-c", "user.name=test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "--allow-empty", "-m", "the arm's first commit");
    }

    private static void LinkTo(string link, string target)
    {
        File.Delete(link);
        File.CreateSymbolicLink(link, target);
    }

    /// <summary>The host's git by its real path: what <c>&lt;temp&gt;/bin/git</c> links to, and what a shim ends in.</summary>
    public static string RealGit => HostGit.Value;

    /// <summary>The console's apphost, copied beside the test assembly by the project reference: the binary the scenario tests run as a process.</summary>
    public static string ConsoleBinary { get; } = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "model-arms.exe" : "model-arms");

    /// <summary>A <c>0755</c> shell script at <paramref name="path"/> that appends one line to <c>&lt;temp&gt;/markers/&lt;name&gt;</c> and then either exits 0 or, with
    /// <paramref name="thenRealGit"/>, runs the host's git with the same arguments. A program the target could place where something looks it up: the marker says it ran.</summary>
    public string Shim(string path, string name, bool thenRealGit = false) =>
        ShimScript(path, $"echo \"$0 $*\" >> '{MarkerFile(name)}'\n" + (thenRealGit ? $"exec '{RealGit}' \"$@\"\n" : "exit 0\n"));

    /// <summary>A <c>0755</c> shell script with the given body (after the shebang), its folder created, written by <see cref="ShimFiles"/> so it is proved startable.</summary>
    public string ShimScript(string path, string body)
    {
        Directory.CreateDirectory(Path.Combine(Temp, "markers"));
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "#!/bin/sh\n" + body);
            return path;
        }

        return ShimFiles.Write(path, "#!/bin/sh\n" + body);
    }

    /// <summary>Replaces <c>&lt;temp&gt;/bin/git</c> by a shim with the given body: the link to the host's git is deleted FIRST, always — writing through it would overwrite the operator's git itself (<c>ValidateVerbTests</c> has always done these two steps by hand). Returns the shim's path.</summary>
    public string ReplaceGit(string body)
    {
        var git = Path.Combine(Temp, "bin", "git");
        File.Delete(git);
        return ShimScript(git, body);
    }

    /// <summary>Where a shim named <paramref name="name"/> writes its marker lines.</summary>
    public string MarkerFile(string name) => Path.Combine(Temp, "markers", name);

    /// <summary>The names of the shims that ran at least once.</summary>
    public IReadOnlyList<string> Markers() =>
        Directory.Exists(Path.Combine(Temp, "markers")) ? [.. Directory.EnumerateFiles(Path.Combine(Temp, "markers")).Select(Path.GetFileName).Cast<string>().Order(StringComparer.Ordinal)] : [];

    /// <summary>Adds or replaces variables of <see cref="Environment"/>.</summary>
    public void WithVariables(params (string Name, string Value)[] variables)
    {
        foreach (var (name, value) in variables)
        {
            _variables[name] = value;
        }
    }

    /// <summary>Replaces the <c>PATH</c> of <see cref="Environment"/>, the only list <c>validate</c> resolves the configured executables against.</summary>
    public void UsePath(string path) => _variables["PATH"] = path;

    /// <summary>An empty file inside the scratch folder with mode 0755 (where modes exist). Returns its path.</summary>
    public string Executable(params string[] parts)
    {
        var path = _temp.File(string.Empty, parts);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, (UnixFileMode)Convert.ToInt32("755", 8));
        }

        return path;
    }

    private static async Task<string> GitAsync(string folder, params string[] arguments)
    {
        var attempt = await ProcessRunner.RunAsync("git", arguments, folder, GitBudget, TestContext.Current.CancellationToken);
        var done = attempt.Should().BeOfType<ProcessAttempt.Completed>().Subject;
        done.Result.ExitCode.Should().Be(0, $"git {arguments[0]} builds the fixture target: {done.Result.Output}");
        return done.Result.StandardOutput;
    }

    /// <summary>A file of the root with the given text (mode 0600 where modes exist); its folder is created with the root's mode.</summary>
    public string Write(string relative, string text)
    {
        var path = Path.Combine(Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, Closed);
        }

        return path;
    }

    /// <summary>Rewrites <c>experiment.json</c> after <paramref name="change"/> has edited its parsed tree (comments are dropped).</summary>
    public void Edit(Action<JsonNode> change)
    {
        var path = Path.Combine(Root, "experiment.json");
        var tree = JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip })!;
        change(tree);
        File.WriteAllText(path, tree.ToJsonString());
    }

    /// <summary>The command line <paramref name="args"/> plus <c>--experiment &lt;this root&gt;</c>, through the gate with a verb that only records its calls.</summary>
    public Task<GatePass> PassAsync(params string[] args) => ThroughAsync([.. args, "--experiment", Root], Environment);

    /// <summary>The same with the gate's host reads handed in.</summary>
    public Task<GatePass> PassAsync(ModelArmsApp.GateSeams seams, params string[] args) => ThroughAsync([.. args, "--experiment", Root], Environment, seams);

    /// <summary>Runs <paramref name="args"/> through <see cref="ModelArmsCommandLine.Split"/>, builds the verb's input as the console does and hands
    /// it to the gate with a verb that records its calls and returns 0. A line that does not split is a test error, not a gate answer.</summary>
    public static async Task<GatePass> ThroughAsync(string[] args, IReadOnlyDictionary<string, string> environment, ModelArmsApp.GateSeams? seams = null)
    {
        var line = ModelArmsCommandLine.Split(args).Ok();
        var output = new StringWriter();
        var error = new StringWriter();
        var calls = new List<ModelArmsApp.ExperimentInput>();
        var input = new ModelArmsApp.VerbInput(
            line.Verb, line.Positionals, CommandLine.Parse(line.Rest), output, error, environment, ModelArmsApp.ModuleFiles.Beside(AppContext.BaseDirectory), CancellationToken.None);

        var code = await ModelArmsApp.ExperimentGate.RunAsync(
            input,
            seen =>
            {
                calls.Add(seen);
                return Task.FromResult(0);
            },
            seams ?? ModelArmsApp.GateSeams.Real);
        return new GatePass(code, output.ToString(), error.ToString(), calls);
    }

    /// <summary>A second name for <paramref name="existing"/> (<c>ln</c> in a child process: .NET has no hard-link call).</summary>
    public static async Task HardLinkAsync(string existing, string link)
    {
        var start = new System.Diagnostics.ProcessStartInfo("ln") { UseShellExecute = false };
        start.ArgumentList.Add(existing);
        start.ArgumentList.Add(link);
        using var process = System.Diagnostics.Process.Start(start)!;
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        process.ExitCode.Should().Be(0, "ln makes the second name the test needs");
    }

    public void Dispose() => _temp.Dispose();

    private static Dictionary<string, string> Variables(string home)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
        {
            map[(string)entry.Key] = (string?)entry.Value ?? string.Empty;
        }

        map.Remove("MODEL_ARMS_EXPERIMENT");
        map["HOME"] = home;
        return map;
    }
}
