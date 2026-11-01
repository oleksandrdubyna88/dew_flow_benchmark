using Bench.Domain.Targets;
using Bench.Domain.Trace;
using Bench.Infrastructure.Process;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;
using ModelArms.Core.Steps;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The scratch arm of the E4.S2 scenarios: <see cref="TargetBench"/>'s target cloned (by the host's git) into <c>r/&lt;token&gt;/arm</c> with the rules submodule updated, on a branch <c>arm</c> that holds <c>a.txt</c> (three lines), identity <c>test</c> in the clone's config (a real arm's clone carries the model's: the pick's committer
/// pin must beat it) and one <c>node_modules</c> folder for each entry of <c>target.nodeModules</c>. The task worktrees are made and removed by the code under test, never by the kit; the kit only COMMITS in them, writes a <c>.before</c> and plants. Everything lives under two <see cref="TempFolder"/>s (the bench's, and <see cref="Temp"/> for shims, plants and
/// markers) removed on dispose.
/// <para>
/// What this does NOT prove: Windows (POSIX shims and links); that the target's real build behaves as the bench's shims do.
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
internal sealed class WorktreeKit : IDisposable
{
    public const string Arm = "C1";

    private const string Pins = "-c core.fsmonitor=false -c core.hooksPath= -c submodule.recurse=false ";

    private readonly Dictionary<string, StepEntry> _recovery = new(StringComparer.Ordinal);
    private string? _recording;

    private WorktreeKit(TargetBench bench)
    {
        Bench = bench;
    }

    public TargetBench Bench { get; }

    public TempFolder Temp { get; } = new();

    public ExperimentRoot Root => Bench.Root;

    public ArmPaths Paths => Root.PathsOf(Arm);

    public string Clone => Paths.CloneFolder;

    public TargetFacts Target => Bench.Experiment.Target;

    public StringWriter Said { get; } = new();

    public BenchClock Clock { get; } = new(new DateTimeOffset(2026, 10, 28, 21, 32, 38, TimeSpan.Zero), TimeZoneInfo.Utc);

    public ArmFiles Files => new(Paths);

    public StepLog Log => new(Said, ArmMap.Empty, Clock, Arm, Files);

    public string Wt(string id) => Paths.Worktree(id);

    public static async Task<WorktreeKit> CreateAsync()
    {
        var bench = await TargetBench.CreateAsync();
        var kit = new WorktreeKit(bench);
        try
        {
            await kit.MakeArmAsync();
            return kit;
        }
        catch
        {
            kit.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        Bench.Dispose();
        Temp.Dispose();
    }

    /// <summary>What recovery answers for <paramref name="id"/>: <c>Fresh(1, 60)</c> unless the test set another.</summary>
    public StepEntry Recover(string id) => _recovery.TryGetValue(id, out var entry) ? entry : new StepEntry.Fresh(1, 60);

    public void Set(string id, StepEntry entry) => _recovery[id] = entry;

    /// <summary>The tools of the pipeline: the host's git (or <paramref name="git"/>), the bench's shims as the build, the root's environment (or <paramref name="environment"/>).</summary>
    public TaskTools Tools(string? git = null, IReadOnlyDictionary<string, string>? environment = null) =>
        new(git ?? ExperimentRoot.RealGit, ToolPaths.Of(Target, name => Path.Combine(Root.Temp, "bin", name)), environment ?? Root.Environment);

    public TaskWorktrees Sut(TaskTools? tools = null, TaskProbe? probe = null, TargetFacts? target = null) =>
        new(Paths, target ?? Target, tools ?? Tools(), Log, probe ?? TaskProbe.None);

    /// <summary>The pipeline's git, recording: every call is a block in <see cref="Marker"/>.</summary>
    public TaskTools Recording(IReadOnlyDictionary<string, string>? environment = null)
    {
        _recording ??= Sentinel.RecordingShim(Temp, Marker);
        return Tools(_recording, environment);
    }

    public string Marker => Temp.Combine("git-calls");

    public void ForgetCalls() => File.Delete(Marker);

    /// <summary>The recorded calls' argv with the harness's three pins taken off the front (a call without them keeps its text).</summary>
    public IReadOnlyList<string> Calls() =>
        File.Exists(Marker) ? [.. Sentinel.Blocks(Marker).Select(block => block.Call.StartsWith(Pins, StringComparison.Ordinal) ? block.Call[Pins.Length..] : block.Call)] : [];

    public static bool IsEnumeration(string call) => call.StartsWith("config --name-only -z --get-regexp ", StringComparison.Ordinal);

    /// <summary>The calls that are questions of the pipeline (the enumerations that precede every neutralised question left out).</summary>
    public IReadOnlyList<string> Questions() => [.. Calls().Where(call => !IsEnumeration(call))];

    public async Task<BatchEntry.Ready> ReadyAsync(TaskWorktrees sut, params string[] ids)
    {
        var entry = await sut.EnterAsync(ids, Recover, TestContext.Current.CancellationToken);
        entry.Should().BeOfType<BatchEntry.Ready>(entry is BatchEntry.Stopped stopped ? stopped.Why : string.Empty);
        return (BatchEntry.Ready)entry;
    }

    public Task<string> GitAsync(string folder, params string[] arguments) => TargetBench.GitAsync(folder, arguments);

    public async Task<string> HeadAsync(string folder) => (await GitAsync(folder, "rev-parse", "HEAD")).Trim();

    public async Task CommitInAsync(string folder, string relative, string text, string message)
    {
        var path = Path.Combine(folder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, text, TestContext.Current.CancellationToken);
        await GitAsync(folder, "add", "--", relative);
        await CommitStagedAsync(folder, message);
    }

    public Task CommitStagedAsync(string folder, string message) =>
        GitAsync(folder, "-c", "user.name=test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "--allow-empty", "-m", message);

    /// <summary><c>task-&lt;id&gt;.before</c> from the worktree's HEAD, as E4.S3 writes it before the task's first attempt.</summary>
    public async Task BeforeAsync(string id)
    {
        Directory.CreateDirectory(Paths.Out);
        await File.WriteAllTextAsync(Paths.TaskBefore(id), await HeadAsync(Wt(id)) + "\n", TestContext.Current.CancellationToken);
    }

    public string BeforeOf(string id) => File.ReadAllText(Paths.TaskBefore(id)).Trim();

    public async Task<IReadOnlyList<string>> SubjectsAsync(string folder, string from, string to = "HEAD") =>
        [.. (await GitAsync(folder, "log", "--reverse", "--format=%s", $"{from}..{to}")).Split('\n', StringSplitOptions.RemoveEmptyEntries)];

    public async Task<int> CountAsync(string folder, string from, string to = "HEAD") =>
        int.Parse((await GitAsync(folder, "rev-list", "--count", $"{from}..{to}")).Trim(), System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The log's lines, the stamp and arm taken off.</summary>
    public IReadOnlyList<string> LogLines =>
        [.. Said.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line[(line.IndexOf("] ", StringComparison.Ordinal) + 2)..])];

    public IReadOnlyList<string> RecordLines(string id) =>
        File.Exists(Paths.PickRecord(id)) ? [.. File.ReadAllText(Paths.PickRecord(id)).Split('\n', StringSplitOptions.RemoveEmptyEntries)] : [];

    /// <summary>The host's patch-id of <paramref name="sha"/>: <c>diff-tree -p</c> piped into <c>patch-id --stable</c>.</summary>
    public async Task<string> PatchIdAsync(string folder, string sha)
    {
        var patch = await GitAsync(folder, "diff-tree", "-p", sha);
        var attempt = await ProcessRunner.RunAsync(ExperimentRoot.RealGit, ["patch-id", "--stable"], folder, TimeSpan.FromSeconds(60), patch, Root.Environment, TestContext.Current.CancellationToken);
        var done = attempt.Should().BeOfType<ProcessAttempt.Completed>(attempt.Describe).Subject;
        return done.Result.StandardOutput.Split(' ')[0].Trim();
    }

    /// <summary>The host's git with its exit code IGNORED under the root's environment: what a crash, a conflict or a plant leaves.</summary>
    public Task HostGitAsync(string folder, params string[] arguments) => Sentinel.HostGitAsync(folder, HostEnvironment, TestContext.Current.CancellationToken, arguments);

    /// <summary>The environment the host's own git is asked under in a live proof: the root's, with no inherited <c>GIT_*</c> (a <c>GIT_EDITOR</c> of the test process would hide a <c>core.editor</c> plant).</summary>
    public IReadOnlyDictionary<string, string> HostEnvironment => GitEnvironment.Of(Root.Environment);

    public void ClearLog() => Said.GetStringBuilder().Clear();

    /// <summary>A real cherry-pick conflict in the clone, made by the host's git: <c>a.txt</c> line 1 changed on the arm branch and on a side branch cut BEFORE it; with <paramref name="two"/> the side branch holds a second commit (a two-commit pick leaves <c>sequencer/</c> too, M-s). Returns the arm's HEAD before the pick.</summary>
    public async Task<string> ConflictInCloneAsync(bool two)
    {
        var side = (await GitAsync(Clone, "rev-parse", "HEAD")).Trim();
        await GitAsync(Clone, "branch", "side", side);
        await CommitInAsync(Clone, "a.txt", "arm\ntwo\nthree\n", "the arm changes line 1");
        var armHead = await HeadAsync(Clone);
        await GitAsync(Clone, "checkout", "-q", "side");
        await CommitInAsync(Clone, "a.txt", "side\ntwo\nthree\n", "the side changes line 1");
        if (two)
        {
            await CommitInAsync(Clone, "side-two.txt", "two\n", "the side's second");
        }

        var tip = await HeadAsync(Clone);
        await GitAsync(Clone, "checkout", "-q", "arm");
        var range = two ? new[] { (await GitAsync(Clone, "rev-parse", "side~1")).Trim(), tip } : [tip];
        await HostGitAsync(Clone, ["cherry-pick", .. range]);
        return armHead;
    }

    public bool InGit(string name) => Path.Exists(Path.Combine(Clone, ".git", name));

    private async Task MakeArmAsync()
    {
        byte token = 0xc7;
        var map = ArmMap.Extend(ArmMap.Empty, [Arm], () => [0, 0, 0, 0, 0, 0, 0, token]);
        Root.Write("arms.map.json", map.ToJson());
        Root.Write($"chain/made-{Arm}", "made\n");
        Root.Write("baseline.json", BaselineFile.Write(BaselineFile.Compose(CommitSha.Parse("6786582e30d3e771549d05ed719478396f8099cd").Ok(), new DateTimeOffset(2026, 10, 29, 12, 30, 15, TimeSpan.Zero), "wsl-ubuntu", new SuiteRun(true, 0, [], 10), new SuiteRun(true, 0, [], 5), "fixture").Ok()));
        Directory.CreateDirectory(Paths.Folder);
        await GitAsync(Paths.Folder, "clone", "-q", Bench.Source, Clone);
        await GitAsync(Clone, "checkout", "-q", "-b", "arm");
        await GitAsync(Clone, "-c", "protocol.file.allow=always", "submodule", "update", "--init", "-q");
        await GitAsync(Clone, "config", "user.name", "test");
        await GitAsync(Clone, "config", "user.email", "test@example.invalid");
        Directory.CreateDirectory(Paths.Out);
        await CommitInAsync(Clone, "a.txt", "one\ntwo\nthree\n", "the arm's first");
        foreach (var entry in Target.NodeModules)
        {
            Directory.CreateDirectory(Path.Combine(Clone, entry));
            File.WriteAllText(Path.Combine(Clone, entry, "marker"), "node_modules\n");
        }
    }
}
