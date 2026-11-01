using System.Text.Json.Nodes;
using Bench.Cli;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;
using ModelArms.Core.Ports;
using ModelArms.Core.Steps;
using ModelArms.Verbs;
using Xunit;
using ModelArmsApp = ModelArms;

namespace Bench.Tests.ModelArms;

/// <summary>What one in-process <c>chain</c> left on the two writers.</summary>
internal sealed record ChainRun(int Code, string Output, string Error);

/// <summary>An awake scope that records its start and its end in the kit's one ordered list.</summary>
internal sealed class RecordedAwake(List<string> record) : IHostAwake
{
    public string Describe => "recorded keep-awake";

    public ValueTask DisposeAsync()
    {
        record.Add("awake-dispose");
        return ValueTask.CompletedTask;
    }
}

/// <summary>The chain's world, in process (E5.S4): an experiment root the gate admits for <c>chain</c> (the example, run <c>part6</c> and <c>part7</c>, every arm of both in <c>arms.map.json</c> with a token), the executables the chain resolves on the root's <c>PATH</c>, and a <see cref="ChainSeams"/> whose every member appends <c>"&lt;stage&gt; &lt;args&gt;"</c> to ONE ordered list and acts as the real one would ON DISK: <c>Make</c> writes <c>chain/made-&lt;arm&gt;</c>, <c>Check</c> writes
/// <c>chain/objective-&lt;arm&gt;</c>, <c>Lock</c> returns the row's outcome and, for <c>Applied</c>, writes the two snapshot files in the reader's shape with the digest <see cref="LockVerb.PlanOf"/> gives, <c>Unlock</c> removes them, and <c>Start</c> marks the arms ALIVE — their runner's lock held and <c>pids/&lt;arm&gt;.pid</c> written — after <see cref="StartupTicks"/> ticks (0: at the next gather; <see cref="Never"/>: never), or, with <see cref="RealRunAll"/>, hands the arms to the REAL
/// <c>RunAllVerb.StartAsync</c> over a recording launcher and the same liveness. Time is a <see cref="BenchClock"/>: <c>Tick</c> records its span, advances the clock BY that span, then moves the world one tick — an alive arm finishes (an <c>arm-done</c> line) and leaves after <see cref="ExitTicks"/> more, an arm in <see cref="StartsAfter"/> becomes alive — and runs the row's <see cref="AfterTick"/>; it fails the test after 200 ticks (the startup poll alone may take 60).
/// <para>
/// The markers, the map, the liveness and the clock are the only things faked; the files are real, the chain's run lock is the real <see cref="FileGateLock"/> and every executable the chain resolves is a real file on a real <c>PATH</c>. Linux only (<see cref="ExperimentRoot.MakeArmAsync"/>'s own condition).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
internal sealed class ChainKit : IAsyncDisposable
{
    public const int Never = int.MaxValue;
    private const int TickGuard = 200;

    private readonly HashSet<string> _alive = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _comesAlive = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _leaves = new(StringComparer.Ordinal);
    private readonly HashSet<string> _stuck = new(StringComparer.Ordinal);
    private byte _token = 0x10;
    private int _ticks;

    private ChainKit(ExperimentRoot root) => Root = root;

    public ExperimentRoot Root { get; }

    public RootPaths Layout => new(Root.Root);

    public BenchClock Clock { get; } = new(IsolationKit.Instant, TimeZoneInfo.Utc);

    /// <summary>Every stage, in order: <c>awake-start</c>, <c>make …</c>, <c>lock part6</c>, <c>start …</c>, <c>tick 00:01:00</c>, <c>unlock</c>, <c>check C1</c>, <c>awake-dispose</c>.</summary>
    public List<string> Record { get; } = [];

    public List<TimeSpan> Spans { get; } = [];

    public List<HostKind> AwakeAsked { get; } = [];

    public HostKind Host { get; set; } = HostKind.OtherPosix;

    /// <summary>The platform road the chain chooses its folder isolation by; null is this host's.</summary>
    public PlatformRoad? Road { get; set; }

    public DetachedRoad Detached { get; set; } = DetachedRoad.Setsid;

    public int StartupTicks { get; set; }

    public int ExitTicks { get; set; }

    /// <summary>Whether an alive arm finishes at the next tick (the default) or stays alive and unfinished until the test ends.</summary>
    public bool FinishArms { get; set; } = true;

    /// <summary>The run the last <see cref="RunAsync"/> was asked for.</summary>
    public string CurrentRun { get; private set; } = "part6";

    public Func<LockOutcome>? LockResult { get; set; }

    public Func<UnlockOutcome>? UnlockResult { get; set; }

    public Func<string, Task<int>>? CheckResult { get; set; }

    /// <summary>Run after each tick has moved the world; a row's scripted mutation (the clock moved past a ceiling, the operator's stop).</summary>
    public Action<int>? AfterTick { get; set; }

    /// <summary>Called once, at the first stage the chain carries out (before it is recorded).</summary>
    public Action? AtFirstStage { get; set; }

    /// <summary>When set, <c>Start</c> goes to the real <c>run-all</c> with this launcher; each start it is asked for makes the arm alive as the fake <c>Start</c> would.</summary>
    public RecordingLauncher? RealRunAll { get; set; }

    /// <summary>Whether an arm that is not in the kit's alive set is asked of its REAL lock (a row that holds <c>pids/&lt;arm&gt;.lock</c> with <c>tests/GateHolder</c>).</summary>
    public bool RealLiveness { get; set; }

    public string Output => _output.ToString();

    public string Error => _error.ToString();

    private StringWriter _output = new();
    private StringWriter _error = new();
    private bool _first = true;

    public static async Task<ChainKit> CreateAsync()
    {
        Assert.SkipUnless(DetachedRoads.Current == DetachedRoad.Setsid, "the chain's setsid road is measured on Linux x86-64 only (E5 plan M-1..M-5)");
        Assert.SkipWhen(RunAllKit.HostSetsid.Length == 0, "util-linux's setsid must resolve on the test process's PATH");
        var root = await new ExperimentRoot().WithTargetAsync();
        File.CreateSymbolicLink(Path.Combine(root.Temp, "bin", "setsid"), RunAllKit.HostSetsid);
        root.Write("baseline.json", BaselineFile.Write(ExperimentRoot.CompletedBaseline()));
        Directory.CreateDirectory(Path.Combine(root.Root, "base"));
        var kit = new ChainKit(root);
        kit.WriteMap();
        return kit;
    }

    /// <summary>The executables the chain resolves, as empty files on <c>&lt;temp&gt;/bin</c> and the coai binary, for a root built some other way (<see cref="RunAllKit"/>); what exists is left alone.</summary>
    public static void Provide(ExperimentRoot root)
    {
        foreach (var name in new[] { "claude", "git", "dotnet", "node", "npm", "pwsh.exe" })
        {
            if (!File.Exists(Path.Combine(root.Temp, "bin", name)))
            {
                root.Executable("bin", name);
            }
        }

        var coai = Path.Combine(root.Temp, "example", "coai", "coai-mcp");
        if (!File.Exists(coai))
        {
            root.Executable("example", "coai", "coai-mcp");
        }
    }

    private void WriteMap()
    {
        var arms = Root.ExperimentOf().Runs.SelectMany(run => run.Rounds.SelectMany(round => round)).Distinct().ToList();
        Root.Write("arms.map.json", ArmMap.Extend(ArmMap.Empty, arms, () => [0, 0, 0, 0, 0, 0, 0, ++_token]).ToJson());
    }

    public ArmPaths PathsOf(string arm) => Root.PathsOf(arm);

    public string Rounds(string run) => string.Join(" | ", Root.ExperimentOf().Runs.Single(each => each.Name == run).Rounds.Select(round => string.Join(',', round)));

    /// <summary>The seams the chain runs on; the kit's fields are read when each member runs, so a row may change them between runs.</summary>
    public ChainSeams Seams() =>
        new(
            (input, arms) => Task.FromResult(MakeArms(arms)),
            LockAsync,
            UnlockAsync,
            StartAsync,
            CheckAsync,
            (_, arm) => _alive.Contains(arm) ? new RunnerState.Alive("pid 4242", 4242) : RealLiveness ? RunnerLiveness.Of(Layout, arm) : new RunnerState.Gone(),
            (_, host, _) =>
            {
                Record.Add("awake-start");
                AwakeAsked.Add(host);
                return Task.FromResult<ModelArmsApp.Passage<IHostAwake>>(new ModelArmsApp.Passage<IHostAwake>.Through(new RecordedAwake(Record)));
            },
            _ => Task.FromResult(Host),
            TickAsync,
            Clock,
            Road ?? PlatformFactsReader.CurrentRoad,
            Detached);

    /// <summary><c>chain &lt;run&gt;</c> through the gate with the kit's seams (or the ones handed in); an exception travels to the test with the writers' text kept in <see cref="Output"/> and <see cref="Error"/>.</summary>
    public async Task<ChainRun> RunAsync(string run = "part6", CancellationToken? stopping = null, ChainSeams? seams = null)
    {
        CurrentRun = run;
        _output = new StringWriter();
        _error = new StringWriter();
        var input = new ModelArmsApp.VerbInput("chain", [run], CommandLine.Parse(["chain", "--experiment", Root.Root]), _output, _error, Root.Environment, ModelArmsApp.ModuleFiles.Beside(AppContext.BaseDirectory), stopping ?? TestContext.Current.CancellationToken);
        var code = await ModelArmsApp.ExperimentGate.RunAsync(input, admitted => ChainVerb.RunAsync(admitted, seams ?? Seams()), ModelArmsApp.GateSeams.Real);
        return new ChainRun(code, _output.ToString(), _error.ToString());
    }

    private void Stage(string entry)
    {
        if (_first)
        {
            _first = false;
            AtFirstStage?.Invoke();
        }

        Record.Add(entry);
    }

    private int MakeArms(IReadOnlyList<string> arms)
    {
        Stage("make " + string.Join(',', arms));
        arms.ToList().ForEach(MarkMade);
        return 0;
    }

    public void MarkMade(string arm) => Root.Write($"chain/made-{arm}", "made\n");

    public void MarkStage(string run, RunStage stage)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Layout.RunMarker(run, stage))!);
        File.WriteAllText(Layout.RunMarker(run, stage), string.Empty);
    }

    public bool Marked(string run, RunStage stage) => File.Exists(Layout.RunMarker(run, stage));

    private Task<LockOutcome> LockAsync(ModelArmsApp.ExperimentInput input, string run, CancellationToken stopping)
    {
        Stage($"lock {run}");
        Marked(run, RunStage.Locked).Should().BeFalse("the verb writes no marker: the chain writes `locked` after the verb returns");
        if (LockResult is { } forced)
        {
            return Task.FromResult(forced());
        }

        return Task.FromResult<LockOutcome>(LockVerb.PlanOf(input, run) is ModelArmsApp.Passage<IsolationPlan?>.Through { Value: { } plan } ? Applied(plan) : new LockOutcome.NoIsolation("no isolation configured — nothing to lock"));
    }

    /// <summary>The two snapshot files in the reader's shape, for the plan's digest.</summary>
    private LockOutcome Applied(IsolationPlan plan)
    {
        WriteSnapshot(plan.Run, plan.Digest);
        return new LockOutcome.Applied($"isolation applied for {plan.Run}");
    }

    public void WriteSnapshot(string run, string digest)
    {
        ArmFiles.ReplaceFile(Layout.LockedModes, $$"""{"run":"{{run}}","digest":"{{digest}}","since":"2026-10-31T10:00:00Z","modes":[{"path":"{{Root.Root}}","mode":"700"}]}""");
        ArmFiles.ReplaceFile(Layout.LockedModesApplied, $$"""{"run":"{{run}}","digest":"{{digest}}","verifiedAt":"2026-10-31T10:00:05Z"}""");
    }

    /// <summary>The digest the chain will compute for <paramref name="run"/> over the root as it stands.</summary>
    public string DigestOf(string run)
    {
        var experiment = Root.ExperimentOf();
        var map = ArmMap.Read(File.ReadAllText(Layout.ArmMapFile)).Ok();
        return IsolationPlans.From(experiment.Isolation, experiment.Runs.Single(each => each.Name == run), map, experiment.Root, [experiment.Root]).Ok().Digest;
    }

    private Task<UnlockOutcome> UnlockAsync(ModelArmsApp.ExperimentInput input, CancellationToken stopping)
    {
        Stage("unlock");
        Marked(CurrentRun, RunStage.Unlocked).Should().BeFalse("the verb writes no marker: the chain writes `unlocked` after the verb returns");
        if (UnlockResult is { } forced)
        {
            return Task.FromResult(forced());
        }

        File.Delete(Layout.LockedModes);
        File.Delete(Layout.LockedModesApplied);
        return Task.FromResult<UnlockOutcome>(new UnlockOutcome.Restored(1, "unlocked: restored 1 entries"));
    }

    private async Task<int> StartAsync(ModelArmsApp.ExperimentInput input, IReadOnlyList<string> arms)
    {
        Stage("start " + string.Join(',', arms));
        if (RealRunAll is not { } launcher)
        {
            arms.ToList().ForEach(Launched);
            return 0;
        }

        var seams = new RunAllSeams(_ => launcher, (root, arm) => Seams().Liveness(root, arm), DetachedRoad.Setsid, TimeSpan.Zero);
        return await RunAllVerb.StartAsync(input, arms, seams);
    }

    /// <summary>A runner of <paramref name="arm"/> was started: its lock is held after <see cref="StartupTicks"/> ticks.</summary>
    public void Launched(string arm)
    {
        if (StartupTicks == 0)
        {
            MarkAlive(arm);
        }
        else if (StartupTicks != Never)
        {
            _comesAlive[arm] = _ticks + StartupTicks;
        }
    }

    public void MarkAlive(string arm)
    {
        _alive.Add(arm);
        ArmFiles.ReplaceFile(Layout.PidFile(arm), "4242\n");
    }

    /// <summary>An arm that stays alive and never writes <c>arm-done</c>.</summary>
    public void Stick(params string[] arms) => _stuck.UnionWith(arms);

    public void WritePid(string arm)
    {
        Directory.CreateDirectory(Layout.Pids);
        ArmFiles.ReplaceFile(Layout.PidFile(arm), "4242\n");
    }

    public void FinishNow(string arm)
    {
        if (TimingOf(arm).OfType<TimingLine.ArmDone>().Any())
        {
            return;
        }

        new ArmFiles(PathsOf(arm)).AppendTiming(new TimingLine.ArmDone(arm, Clock.GetUtcNow()));
    }

    public IReadOnlyList<TimingLine> TimingOf(string arm) => File.Exists(PathsOf(arm).Timing) ? TimingLines.Read(File.ReadAllText(PathsOf(arm).Timing)) : [];

    private Task<int> CheckAsync(ModelArmsApp.ExperimentInput input, string arm)
    {
        Stage($"check {arm}");
        if (CheckResult is { } forced)
        {
            return forced(arm);
        }

        Root.Write($"chain/objective-{arm}", "checked\n");
        return Task.FromResult(0);
    }

    private Task TickAsync(TimeSpan span, CancellationToken stopping)
    {
        Record.Add($"tick {span}");
        Spans.Add(span);
        Spans.Count.Should().BeLessThan(TickGuard + 1, "a chain that has ticked more than 200 times is stuck in a wait with no bound the row expected");
        Clock.Advance(span);
        MoveTheWorld();
        AfterTick?.Invoke(_ticks);
        stopping.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    /// <summary>One tick of the world: arms that were alive finish (unless stuck) and leave <see cref="ExitTicks"/> later; then the arms whose runner takes its lock now become alive.</summary>
    private void MoveTheWorld()
    {
        _ticks++;
        foreach (var arm in _leaves.Where(each => each.Value <= _ticks).Select(each => each.Key).ToList())
        {
            _alive.Remove(arm);
            _leaves.Remove(arm);
        }

        foreach (var arm in _alive.Where(arm => FinishArms && !_stuck.Contains(arm) && !_leaves.ContainsKey(arm)).ToList())
        {
            FinishNow(arm);
            _leaves[arm] = _ticks + ExitTicks;
            if (ExitTicks == 0)
            {
                _alive.Remove(arm);
                _leaves.Remove(arm);
            }
        }

        foreach (var arm in _comesAlive.Where(each => each.Value <= _ticks).Select(each => each.Key).ToList())
        {
            _comesAlive.Remove(arm);
            MarkAlive(arm);
        }
    }

    /// <summary>The record without its ticks.</summary>
    public IReadOnlyList<string> Stages => [.. Record.Where(entry => !entry.StartsWith("tick ", StringComparison.Ordinal))];

    public ValueTask DisposeAsync()
    {
        Root.Dispose();
        return ValueTask.CompletedTask;
    }
}
