using System.Text.Json.Nodes;
using Bench.Cli;
using Bench.Tests.Probes.Driver;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;
using ModelArms.Verbs;
using Xunit;
using ModelArmsApp = ModelArms;

namespace Bench.Tests.ModelArms;

/// <summary>What one in-process <c>lock</c> or <c>unlock</c> left on the two writers.</summary>
internal sealed record IsolationRun(int Code, string Output, string Error);

/// <summary>An experiment root whose example paths EXIST (E5.S1): <c>&lt;temp&gt;/example/old-experiment</c> (a folder at 755), the four <c>~/.claude/*</c> folders under <c>&lt;temp&gt;/example/home/.claude/</c> (the test's own HOME — never the operator's), <c>&lt;root&gt;/r</c> and
/// <c>&lt;root&gt;/pids</c> (the root is 0700 from the fixture, the rest 755) — and whose <c>isolation</c> block is KEPT. <c>lock</c> and <c>unlock</c> run in process through the gate with <see cref="IsolationSeams.Real"/> edited: a frozen clock (so every stamp is
/// <see cref="Instant"/> and the files' names are known), a short exclusive wait, the kit's <see cref="Events"/> list and, when asked, the test's hands, road and liveness. Every folder a test closes is opened again by <see cref="TempFolder"/>'s dispose before it deletes
/// (common.test-leftovers), and nothing here ever names a path outside the kit's own temp folder.
/// <para>
/// By NAME, never a listing: while the isolation stands the root is at 300, so <see cref="Files"/> asks for the six spellings one by one.
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
internal sealed class IsolationKit : IDisposable
{
    public static readonly DateTimeOffset Instant = new(2026, 10, 31, 10, 0, 0, TimeSpan.Zero);
    public const string Stamp = "20261031T100000Z";

    private static readonly string[] Spellings =
    [
        ".locked-modes", ".locked-modes.applied",
        ".locked-modes.restored-" + Stamp, ".locked-modes.applied.restored-" + Stamp,
        ".locked-modes.restored-" + Stamp + "-2", ".locked-modes.applied.restored-" + Stamp + "-2",
    ];

    public IsolationKit()
    {
        Root = new ExperimentRoot();
        Assert.SkipWhen(OperatingSystem.IsWindows(), "file modes, /proc and O_PATH are measured on Linux");
        foreach (var folder in (string[])[OldExperiment, Path.Combine(Root.Root, "r"), Path.Combine(Root.Root, "pids"), Path.Combine(Root.Root, "chain", "part6"), .. ClaudeFolders])
        {
            Directory.CreateDirectory(folder);
            File.SetUnixFileMode(folder, Mode755);
        }
    }

    public const UnixFileMode Mode755 = (UnixFileMode)0x1ED;

    public ExperimentRoot Root { get; }

    public RootPaths Layout => new(Root.Root);

    /// <summary>The recorded doings of the adapter and the verbs, in order.</summary>
    public List<string> Events { get; } = [];

    public string OldExperiment => Path.Combine(Root.Temp, "example", "old-experiment");

    public string Home => Path.Combine(Root.Temp, "example", "home");

    public IReadOnlyList<string> ClaudeFolders => [.. new[] { "projects", "file-history", "session-env", "shell-snapshots" }.Select(name => Path.Combine(Home, ".claude", name))];

    public string Pids => Path.Combine(Root.Root, "pids");

    /// <summary>The entries of run <c>part6</c>'s plan, in plan order: the eight paths of the worked example.</summary>
    public IReadOnlyList<string> Entries => [OldExperiment, Root.Root, Path.Combine(Root.Root, "r"), Pids, .. ClaudeFolders];

    /// <summary>The 12 permission bits of each entry as <c>stat -c %a</c> spells them, in plan order.</summary>
    public IReadOnlyList<string> Modes() => ModesOf(Entries);

    public static IReadOnlyList<string> ModesOf(IEnumerable<string> paths) => [.. paths.Select(path => SnapshotFiles.ModeText((int)File.GetUnixFileMode(path)))];

    /// <summary>Which of the six spellings are present, by NAME.</summary>
    public IReadOnlyList<string> Files() => [.. Spellings.Where(name => File.Exists(Path.Combine(Root.Root, name)) || new FileInfo(Path.Combine(Root.Root, name)).LinkTarget is not null)];

    /// <summary>The plan of <paramref name="run"/> as the experiment and the map hold them now.</summary>
    public IsolationPlan Plan(string run = "part6")
    {
        var experiment = Root.ExperimentOf();
        var map = File.Exists(Layout.ArmMapFile) ? ArmMap.Read(File.ReadAllText(Layout.ArmMapFile)).Ok() : ArmMap.Empty;
        return IsolationPlans.From(experiment.Isolation, experiment.Runs.Single(each => each.Name == run), map, experiment.Root, [experiment.Root, Home, experiment.Target.Source]).Ok();
    }

    /// <summary>Run <c>part7</c> as the one-arm version the rows use: it closes the one made arm <c>C1</c> and <c>&lt;root&gt;/chain/part6</c> (nine arms would be slow for no extra proof).</summary>
    public void UsePart7WithOneClosedArm() =>
        Root.Edit(top =>
        {
            top["runs"]!["part7"]!["closedArms"] = new JsonArray("C1");
            top["runs"]!["part7"]!["closed"] = new JsonArray("<root>/chain/part6");
        });

    /// <summary>Starts a runner of <paramref name="arm"/> and waits (at most 5 s) for it to end; whatever it did, the answer is <c>Gone</c> — the liveness hand of the race test, which runs the runner exactly when the last probe has been made.</summary>
    public RunnerState StartRunnerAndAwaitItsExit(string arm)
    {
        var child = StartRunner(arm);
        Exited = child.ExitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        return new RunnerState.Gone();
    }

    /// <summary>The exit code of the runner <see cref="StartRunnerAndAwaitItsExit"/> started, or -1 before it.</summary>
    public int Exited { get; private set; } = -1;

    public LockState Snapshot() => IsolationFiles.State(Layout);

    public SavedSnapshot SavedOf() => LockSnapshots.Saved(File.ReadAllText(Layout.LockedModes)).Ok();

    public IsolationSeams Seams(IsolationHands? hands = null, PlatformRoad? road = null, TimeProvider? clock = null, TimeSpan? wait = null, Func<RootPaths, string, PlatformRoad, RunnerState>? liveness = null) =>
        IsolationSeams.Real with
        {
            Clock = clock ?? new BenchClock(Instant, TimeZoneInfo.Utc),
            ExclusiveWait = wait ?? TimeSpan.FromSeconds(10),
            Hands = hands,
            Events = Events,
            Road = road ?? PlatformFactsReader.CurrentRoad,
            Liveness = liveness ?? IsolationSeams.Real.Liveness,
        };

    public Task<IsolationRun> LockAsync(string run = "part6", IsolationHands? hands = null, PlatformRoad? road = null, TimeProvider? clock = null, TimeSpan? wait = null, CancellationToken? stopping = null) =>
        RunAsync("lock", [run], exp => LockVerb.RunAsync(exp, Seams(hands, road, clock, wait)), stopping);

    public Task<IsolationRun> UnlockAsync(bool force = false, IsolationHands? hands = null, PlatformRoad? road = null, TimeProvider? clock = null, TimeSpan? wait = null, Func<RootPaths, string, PlatformRoad, RunnerState>? liveness = null) =>
        RunAsync("unlock", [], exp => UnlockVerb.RunAsync(exp, Seams(hands, road, clock, wait, liveness)), null, force ? ["--force"] : []);

    private async Task<IsolationRun> RunAsync(string verb, string[] positionals, Func<ModelArmsApp.ExperimentInput, Task<int>> body, CancellationToken? stopping, params string[] flags)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var input = new ModelArmsApp.VerbInput(verb, positionals, CommandLine.Parse([verb, .. flags, "--experiment", Root.Root]), output, error, Root.Environment, ModelArmsApp.ModuleFiles.Beside(AppContext.BaseDirectory), stopping ?? TestContext.Current.CancellationToken);
        var code = await ModelArmsApp.ExperimentGate.RunAsync(input, body, ModelArmsApp.GateSeams.Real);
        return new IsolationRun(code, output.ToString(), error.ToString());
    }

    /// <summary>Makes <paramref name="arm"/> runnable with the experiment's <c>isolation</c> block KEPT, and its first call hung — so a started runner lives until the test kills it.</summary>
    public async Task<IsolationKit> AddArmAsync(string arm, FakeCli fake, bool hang = true)
    {
        await Root.MakeArmAsync(arm, fake, keepIsolation: true);
        if (hang)
        {
            fake.Rewrite(ExperimentRoot.Script(new JsonObject { ["hangOnCall"] = 1 }));
        }

        return this;
    }

    /// <summary>The real console as a process, running <c>run-arm &lt;arm&gt;</c> over this root.</summary>
    public ModelArmsBinary.Child StartRunner(string arm) => ModelArmsBinary.Start(Root, "run-arm", arm);

    /// <summary>Waits (at most <paramref name="within"/>) until <paramref name="ready"/> holds; a test that never sees its condition fails with <paramref name="what"/>.</summary>
    public static async Task<bool> UntilAsync(Func<bool> ready, TimeSpan within)
    {
        var limit = DateTimeOffset.UtcNow + within;
        while (DateTimeOffset.UtcNow < limit)
        {
            if (ready())
            {
                return true;
            }

            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        return ready();
    }

    public void Dispose() => Root.Dispose();
}
