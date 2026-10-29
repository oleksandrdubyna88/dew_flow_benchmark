using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Cli;
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

    private readonly TempFolder _temp = new();

    public ExperimentRoot()
    {
        Root = _temp.Folder("example", "root");
        Environment = Variables(Path.Combine(Temp, "example", "home"));
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
    public IReadOnlyDictionary<string, string> Environment { get; }

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

    /// <summary>Runs <paramref name="args"/> through <see cref="ModelArmsCommandLine.Split"/>, builds the verb's input as the console does and hands
    /// it to the gate with a verb that records its calls and returns 0. A line that does not split is a test error, not a gate answer.</summary>
    public static async Task<GatePass> ThroughAsync(string[] args, IReadOnlyDictionary<string, string> environment)
    {
        var line = ModelArmsCommandLine.Split(args).Ok();
        var output = new StringWriter();
        var error = new StringWriter();
        var calls = new List<ModelArmsApp.ExperimentInput>();
        var input = new ModelArmsApp.VerbInput(
            line.Verb, line.Positionals, CommandLine.Parse(line.Rest), output, error, environment, ModelArmsApp.ModuleFiles.Beside(AppContext.BaseDirectory), CancellationToken.None);

        var code = await ModelArmsApp.ExperimentGate.RunAsync(input, seen =>
        {
            calls.Add(seen);
            return Task.FromResult(0);
        });
        return new GatePass(code, output.ToString(), error.ToString(), calls);
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
