using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;
using Bench.Cli;
using Bench.Infrastructure.Process;
using ModelArms.Core.Experiment;
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
        var target = _temp.Folder("example", "target");
        await GitAsync(target, "init", "--quiet", "--initial-branch", PlanBranch);
        _temp.File("a plan\n", "example", "target", "todo", "PLAN_a_spent_quota_cools_the_model_down.md");
        await GitAsync(target, "add", PlanFile);
        await GitAsync(target, "-c", "user.name=test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", "the plan");
        var sha = (await GitAsync(target, "rev-parse", "HEAD")).Trim();
        Edit(top => top["target"]!["base"] = sha);

        Executable("example", "coai", "coai-mcp");
        _temp.File("{}", "example", "vault", "connectOtherAis", "wsl-ubuntu", "settings.json");
        foreach (var name in new[] { "git", "claude", "dotnet", "node", "npm", "pwsh.exe" })
        {
            Executable("bin", name);
        }

        _variables["PATH"] = Path.Combine(Temp, "bin");
        return this;
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
