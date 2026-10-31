using System.Text.Json.Nodes;
using Bench.Cli;
using Bench.Domain.Targets;
using Bench.Domain.Trace;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;
using ModelArms.Verbs;
using Xunit;
using ModelArmsApp = ModelArms;

namespace Bench.Tests.ModelArms;

/// <summary>What one run of <c>checks</c> left on the two writers.</summary>
internal sealed record ChecksRun(int Code, string Output, string Error)
{
    public IReadOnlyList<string> Lines => [.. Output.Split('\n', StringSplitOptions.RemoveEmptyEntries), .. Error.Split('\n', StringSplitOptions.RemoveEmptyEntries)];
}

/// <summary>A clock that moves on a minute every time it is asked the time: two runs of <c>checks</c> in one test stamp their earlier folders differently, as two runs a minute apart would. Elapsed time is the system's.</summary>
internal sealed class TickingClock : TimeProvider
{
    private int _asked;

    public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero).AddMinutes(++_asked);
}

/// <summary>The scratch target of <see cref="TargetBench"/> with one MADE arm in it: <c>arms.map.json</c>, <c>chain/made-&lt;arm&gt;</c>, a completed <c>baseline.json</c>, and the arm's clone — a real clone of the target at <c>base</c>, on a branch of its own with the rules submodule updated (so the target's own wrapper is
/// where the lowering expects it) and one commit that adds three files: <c>src_mcp/tests/ATests.cs</c> (it names the scenario <c>A_scenario</c>), <c>a b.cs</c> (a name with a space) and <c>docs/note.md</c> — the changes <c>checks</c> counts. The subject is never doubled: the verb runs through the gate with the real git and node, and the target's tools are the bench's recording shims.</summary>
internal sealed class ChecksKit : IDisposable
{
    public const string Arm = "C1";

    private ChecksKit(TargetBench bench, string arm)
    {
        Bench = bench;
        Name = arm;
    }

    public TargetBench Bench { get; }

    public string Name { get; }

    public ExperimentRoot Root => Bench.Root;

    public ArmPaths Paths => Root.PathsOf(Name);

    public RootPaths Layout => new(Root.Root);

    public string Clone => Paths.CloneFolder;

    public string Token => Paths.Token;

    /// <summary>The report as the verb wrote it.</summary>
    public JsonNode Report => JsonNode.Parse(File.ReadAllText(Paths.Objective))!;

    /// <summary>A made arm: <paramref name="changedCs"/> says whether the arm's one commit adds the <c>.cs</c> files (without it the clone is the base and nothing is committed).</summary>
    public static async Task<ChecksKit> CreateAsync(string arm = Arm, bool changedCs = true)
    {
        var bench = await TargetBench.CreateAsync();
        var kit = new ChecksKit(bench, arm);
        try
        {
            await kit.MakeArmAsync(changedCs);
            return kit;
        }
        catch
        {
            kit.Dispose();
            throw;
        }
    }

    /// <summary>The verb through the gate, as the console runs it, with the clock and wall handed in (a clock that ticks, no wall of its own, by default).</summary>
    public async Task<ChecksRun> RunAsync(bool parseOnly = false, ChecksSeams? seams = null, CancellationToken? stopping = null)
    {
        string[] args = ["checks", Name, "--experiment", Root.Root, .. parseOnly ? new[] { "--parse-only" } : []];
        var line = ModelArmsCommandLine.Split(args).Ok();
        var output = new StringWriter();
        var error = new StringWriter();
        var input = new ModelArmsApp.VerbInput(
            line.Verb, line.Positionals, CommandLine.Parse(line.Rest), output, error, Root.Environment, ModelArmsApp.ModuleFiles.Beside(AppContext.BaseDirectory), stopping ?? TestContext.Current.CancellationToken);
        var used = seams ?? new ChecksSeams(new TickingClock(), null);
        var code = await ModelArmsApp.ExperimentGate.RunAsync(input, seen => ChecksVerb.RunAsync(seen, used), ModelArmsApp.GateSeams.Real);
        return new ChecksRun(code, output.ToString(), error.ToString());
    }

    /// <summary>Commits one more file in the arm's clone.</summary>
    public async Task CommitAsync(string relative, string text)
    {
        var path = Path.Combine(Clone, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, text, TestContext.Current.CancellationToken);
        await TargetBench.GitAsync(Clone, "add", "--", relative);
        await Commit();
    }

    /// <summary>Every token of the map: what no output line may hold.</summary>
    public IReadOnlyList<string> Tokens => [.. ArmMap.Read(File.ReadAllText(Layout.ArmMapFile)).Ok().Entries.Select(entry => entry.Value)];

    /// <summary>The calls the target's tools received — everything the shims recorded but the harness's own git.</summary>
    public IReadOnlyList<RecordedCall> ToolCalls => [.. Bench.Recorded().Where(call => call.Tool != "git")];

    /// <summary>The calls in the order the shims were reached, each as a short label: <c>wrapper dotnet build</c>, <c>dotnet build</c>, <c>dotnet format whitespace</c>, <c>npm test</c>… — the lowering wrapper by the tool it was handed, a shim by its first argument.</summary>
    public IReadOnlyList<string> Order => [.. ToolCalls.Select(Label)];

    private static string Label(RecordedCall call) =>
        call.Tool == "wrapper"
            ? $"wrapper {Path.GetFileName(call.Arguments[0])} {call.Arguments[1]}"
            : $"{call.Tool} {call.Arguments[0]}{(call.Arguments[0] == "format" ? " " + call.Arguments[2] : string.Empty)}";

    public void Dispose() => Bench.Dispose();

    private async Task MakeArmAsync(bool changedCs)
    {
        byte token = 0xb1;
        var map = ArmMap.Extend(ArmMap.Empty, [Name], () => [0, 0, 0, 0, 0, 0, 0, token]);
        Root.Write("arms.map.json", map.ToJson());
        Root.Write($"chain/made-{Name}", "made\n");
        Root.Write("baseline.json", BaselineFile.Write(Baseline()));
        Directory.CreateDirectory(Paths.Folder);
        await TargetBench.GitAsync(Paths.Folder, "clone", "-q", Bench.Source, Clone);
        await TargetBench.GitAsync(Clone, "checkout", "-q", "-b", Bench.Experiment.Target.Branch);
        await TargetBench.GitAsync(Clone, "-c", "protocol.file.allow=always", "submodule", "update", "--init", "-q");
        Directory.CreateDirectory(Paths.Out);
        if (changedCs)
        {
            await Write("src_mcp/tests/ATests.cs", "class ATests { void A_scenario() { } }\n");
            await Write("a b.cs", "class AB { }\n");
            await Write("docs/note.md", "# a note\n");
            await TargetBench.GitAsync(Clone, "add", "-A");
            await Commit();
        }
    }

    private async Task Write(string relative, string text)
    {
        var path = Path.Combine(Clone, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, text, TestContext.Current.CancellationToken);
    }

    private Task Commit() => TargetBench.GitAsync(Clone, "-c", "user.name=test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", "the arm's work");

    private static Baseline Baseline() =>
        BaselineFile.Compose(CommitSha.Parse("6786582e30d3e771549d05ed719478396f8099cd").Ok(), new DateTimeOffset(2026, 10, 29, 12, 30, 15, TimeSpan.Zero), "wsl-ubuntu", new SuiteRun(true, 0, [], 10), new SuiteRun(true, 0, [], 5), "fixture").Ok();

    /// <summary>A <c>.done</c> marker of the arm's step, as the module's own writer spells it.</summary>
    public void Done(string step, string state) =>
        File.WriteAllText(Paths.Done(step), DoneMarkers.Write(new DoneMarker(1, CapturedCount.Number(0), Paths.Stream(step, 1), state, false)));
}
