using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using Bench.Cli;
using Bench.Tests.Probes.Driver;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Ports;
using ModelArms.Verbs;
using Xunit;
using ModelArmsApp = ModelArms;

namespace Bench.Tests.ModelArms;

/// <summary>What one in-process <c>run-all</c> left on the two writers.</summary>
internal sealed record RunAllRun(int Code, string Output, string Error);

/// <summary>A launcher that records every start it is asked for and starts nothing: the in-process rows' stand-in for <c>SetsidLauncher</c>. <see cref="Seen"/> is called with each start, so a test can look at what the verb had PRINTED when it was asked.</summary>
internal sealed class RecordingLauncher(Action<string>? seen = null) : IDetachedLauncher
{
    /// <summary>What the launcher says about keeping the log (the port's default is true).</summary>
    public bool WritesLog { get; init; } = true;

    public List<(string Executable, IReadOnlyList<string> Arguments, string Log, IReadOnlyDictionary<string, string> Environment, string WorkingDirectory)> Starts { get; } = [];

    public DetachedStart Start(string executable, IReadOnlyList<string> arguments, string log, IReadOnlyDictionary<string, string> environment, string workingDirectory)
    {
        Starts.Add((executable, arguments, log, environment, workingDirectory));
        seen?.Invoke(arguments.Count > 1 ? arguments[1] : string.Empty);
        return new DetachedStart.Started();
    }
}

/// <summary>What the <c>run-all</c> scenarios share (E5.S3): an experiment root whose arms are made the way <c>make</c> leaves them, the fake CLI as <c>claude</c>, the host's <c>setsid</c> by its real path and LINKED into <c>&lt;temp&gt;/bin</c> (as <c>git</c> is, <c>ExperimentRoot.RealGit</c>), and the test's two ways to run the verb — as a SEPARATE
/// process (<see cref="RunAsync"/>: the console's own binary, so the detached runners are not children of the test host) and in process through the gate with the seams of the test's choosing (<see cref="InProcessAsync"/>).
/// <para>
/// <b>No process outlives the test.</b> A detached runner is NOT a child of anything the test started (it was reparented away from <c>run-all</c> on purpose), so the shared launcher's tree kill cannot reach it: <see cref="DisposeAsync"/> kills every runner whose lock is still held and every fake the calls recorded — each only after its command line says it is one — and waits for the locks to be free.
/// The kit's experiment carries no <c>isolation</c> block (<c>MakeArmAsync</c>'s default), so a runner's log holds no warning of that kind and these rows are about the launch, not the lock. Linux x86-64 only (<see cref="Require"/>).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
internal sealed class RunAllKit : IAsyncDisposable
{
    private static readonly TimeSpan GoneWithin = TimeSpan.FromSeconds(15);

    private readonly string[] _arms;

    private RunAllKit(ExperimentRoot root, FakeCli fake, string[] arms, string setsid)
    {
        Root = root;
        Fake = fake;
        _arms = arms;
        Setsid = setsid;
    }

    public ExperimentRoot Root { get; }

    public FakeCli Fake { get; }

    /// <summary>The REAL path of the host's <c>setsid</c> — what <c>run-all</c> prints and launches.</summary>
    public string Setsid { get; }

    public RootPaths Layout => new(Root.Root);

    /// <summary>The host's <c>setsid</c> found through the module's own resolution against the test process's PATH; empty where there is none.</summary>
    public static string HostSetsid { get; } = ExecutableResolution
        .Resolve("setsid", (System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator), [], RootFacts.Host, OperatingSystem.IsWindows())
        is ExecutableAnswer.Found found ? found.Real : string.Empty;

    /// <summary>Skips the test, saying why, anywhere the detached launch is not the measured one.</summary>
    public static void Require()
    {
        Assert.SkipUnless(DetachedRoads.Current == DetachedRoad.Setsid, "the detached launch is measured on Linux x86-64 only (E5 plan M-1..M-5)");
        Assert.SkipWhen(HostSetsid.Length == 0, "util-linux's setsid must resolve on the test process's PATH");
    }

    /// <summary>A root with each of <paramref name="arms"/> made, the fake replaying a success on every call, and <c>setsid</c> linked beside <c>claude</c> and <c>git</c>.</summary>
    public static async Task<RunAllKit> MakeAsync(params string[] arms)
    {
        Require();
        var root = new ExperimentRoot();
        var fake = new FakeCli();
        foreach (var arm in arms)
        {
            await root.MakeArmAsync(arm, fake);
        }

        var link = Path.Combine(root.Temp, "bin", "setsid");
        File.CreateSymbolicLink(link, HostSetsid);
        RunArmKit.Replay(fake, "result-success.jsonl");
        root.Edit(top => top["limits"]!["minimumRemainingSeconds"] = 1);
        return new RunAllKit(root, fake, arms, HostSetsid);
    }

    /// <summary>The fake's script with <paramref name="more"/> added (<c>transcripts: true</c> and the success replay stay).</summary>
    public void Script(JsonObject more)
    {
        var script = new JsonObject(more.Select(property => KeyValuePair.Create(property.Key, property.Value?.DeepClone())));
        script["stdoutFile"] ??= ModelArmsFixtures.Path("result-success.jsonl");
        Fake.Rewrite(ExperimentRoot.Script(script));
    }

    /// <summary><c>run-all &lt;arms&gt; --experiment &lt;root&gt;</c> as the console's own binary, a separate process run to its end.</summary>
    public Task<(int Code, string Output, string Error)> RunAsync(params string[] arms) => ModelArmsBinary.RunAsync(Root, ["run-all", .. arms]);

    /// <summary><c>run-all</c> started through <c>/bin/sh</c> after the shell opened <paramref name="leaked"/> on descriptor 3 WITHOUT close-on-exec — what a parent that leaks a handle looks like to the verb. Run to its end.</summary>
    public Task<(int Code, string Output, string Error)> RunWithLeakedDescriptorAsync(string leaked, params string[] arms) =>
        ModelArmsBinary.RunShellAsync(Root, "exec 3>>\"$0\"; exec \"$@\"", [leaked, ExperimentRoot.ConsoleBinary, "run-all", .. arms]);

    /// <summary>The number of arms of the map whose runner holds its lock now.</summary>
    public int AliveCount() => _arms.Count(arm => RunnerLiveness.Of(Layout, arm) is RunnerState.Alive);

    /// <summary>The same with a line on the verb's stdin, so the verb's descriptor 0 is a pipe.</summary>
    public Task<(int Code, string Output, string Error)> RunWithStdinAsync(params string[] arms) => ModelArmsBinary.RunWithInputAsync(Root, "not for the runner\n", ["run-all", .. arms]);

    /// <summary>The verb in process through the gate, with the seams handed in; the writers are the test's.</summary>
    public async Task<RunAllRun> InProcessAsync(RunAllSeams seams, params string[] arms)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        return await InProcessAsync(seams, output, error, arms);
    }

    public async Task<RunAllRun> InProcessAsync(RunAllSeams seams, StringWriter output, StringWriter error, params string[] arms)
    {
        var input = new ModelArmsApp.VerbInput("run-all", arms, CommandLine.Parse(["--experiment", Root.Root]), output, error, Root.Environment, ModelArmsApp.ModuleFiles.Beside(AppContext.BaseDirectory), TestContext.Current.CancellationToken);
        var code = await ModelArmsApp.ExperimentGate.RunAsync(input, admitted => RunAllVerb.StartAsync(admitted, admitted.Verb.Positionals, seams), ModelArmsApp.GateSeams.Real);
        return new RunAllRun(code, output.ToString(), error.ToString());
    }

    /// <summary>The seams of an in-process row: the recording launcher and the liveness MAP (arm → holder words); an arm absent from it is not running.</summary>
    public static RunAllSeams Seams(RecordingLauncher launcher, IReadOnlyDictionary<string, string> alive, DetachedRoad road = DetachedRoad.Setsid) =>
        new(_ => launcher, (_, arm) => alive.TryGetValue(arm, out var holder) ? new RunnerState.Alive(holder, null) : new RunnerState.Gone(), road, TimeSpan.Zero);

    /// <summary>Waits for the runner of <paramref name="arm"/> to be alive by its LOCK and returns the pid its own pid file names.</summary>
    public async Task<int> RunnerPidAsync(string arm, TimeSpan within)
    {
        (await IsolationKit.UntilAsync(() => RunnerLiveness.Of(Layout, arm) is RunnerState.Alive && PidOf(arm) is not null, within)).Should().BeTrue($"{arm}'s runner takes its lock and writes its pid within {within.TotalSeconds:0} s (its log: {LogOf(arm)})");
        return PidOf(arm)!.Value;
    }

    public int? PidOf(string arm)
    {
        var file = Layout.PidFile(arm);
        return File.Exists(file) ? RunnerPid.Parse(File.ReadAllText(file).Trim()) : null;
    }

    public string LogOf(string arm) => File.Exists(Layout.PidLog(arm)) ? File.ReadAllText(Layout.PidLog(arm)) : string.Empty;

    /// <summary>The parent pid of a live process (field 4 of <c>/proc/&lt;pid&gt;/stat</c>, read after the last <c>)</c> because the name may hold anything).</summary>
    public static int ParentOf(int pid) => int.Parse(Stat(pid)[1], CultureInfo.InvariantCulture);

    /// <summary>The session id of a live process (field 6).</summary>
    public static int SessionOf(int pid) => int.Parse(Stat(pid)[3], CultureInfo.InvariantCulture);

    private static string[] Stat(int pid)
    {
        var stat = File.ReadAllText($"/proc/{pid}/stat");
        return stat[(stat.LastIndexOf(')') + 2)..].Split(' ');
    }

    /// <summary>The words of a live process's command line, split on NUL.</summary>
    public static string[] CommandLineOf(int pid) => File.ReadAllText($"/proc/{pid}/cmdline").TrimEnd('\0').Split('\0');

    public async ValueTask DisposeAsync()
    {
        try
        {
            (await IsolationKit.UntilAsync(Reaped, GoneWithin)).Should().BeTrue("no runner outlives the test that started it");
        }
        finally
        {
            Fake.Dispose();
            Root.Dispose();
        }
    }

    /// <summary>One round of cleanup, repeated until it finds nothing: a runner is killed again each round because it may not have written its pid file when the first round looked (a <c>run-all</c> returns before its runner has), and a scan of <c>/proc/*/cmdline</c> for <c>--experiment &lt;this root&gt;</c> finds the runner whatever its pid file says. True when no runner is alive by its lock and the scan finds none.</summary>
    private bool Reaped()
    {
        KillRunners();
        return _arms.All(arm => RunnerLiveness.Of(Layout, arm) is not RunnerState.Alive) && RunnersOfThisRoot().Count == 0;
    }

    /// <summary>Every runner named by a pid file or by the scan, and every fake that was called, killed with its tree — each only when its command line says it is the module's or the fake's.</summary>
    private void KillRunners()
    {
        foreach (var pid in _arms.Select(PidOf).Concat(Fake.Calls().Select(call => (int?)call.Pid)).OfType<int>().Concat(RunnersOfThisRoot()).Distinct())
        {
            Kill(pid);
        }
    }

    /// <summary>The pids of module processes whose command line carries <c>--experiment &lt;this root&gt;</c>.</summary>
    private List<int> RunnersOfThisRoot()
    {
        var found = new List<int>();
        foreach (var entry in Directory.EnumerateDirectories("/proc").Select(Path.GetFileName).Where(name => name is { Length: > 0 } && name.All(char.IsAsciiDigit)))
        {
            var pid = int.Parse(entry!, CultureInfo.InvariantCulture);
            try
            {
                var words = CommandLineOf(pid);
                if (words.Any(word => Path.GetFileName(word) is "model-arms") && Array.IndexOf(words, "--experiment") is >= 0 and var at && at + 1 < words.Length && words[at + 1] == Root.Root)
                {
                    found.Add(pid);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // the process ended, or is another user's: not ours to look at
            }
        }

        return found;
    }

    private static void Kill(int pid)
    {
        try
        {
            var words = File.Exists($"/proc/{pid}/cmdline") ? CommandLineOf(pid) : [];
            if (words.Any(word => Path.GetFileName(word) is "model-arms" or "fake-cli"))
            {
                using var process = Process.GetProcessById(pid);
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            // it ended on its own between the look and the kill: nothing left to end
        }
    }
}
