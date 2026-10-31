using System.Diagnostics;
using System.Text.Json.Nodes;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Ports;
using ModelArms.Verbs;
using Xunit;
using ModelArmsApp = ModelArms;

namespace Bench.Tests.ModelArms;

/// <summary>The detached start of an arm (E5.S3, D13): <c>run-all</c> launches the module's own RESOLVED path through the resolved <c>setsid</c>, started by <c>posix_spawn</c> as the leader of its own group so <c>setsid</c> forks and the runner is not a child of anything that outlives it; stdin is <c>/dev/null</c>, stdout and stderr the log opened by the kernel with <c>O_NOFOLLOW</c>, every signal at its default. The runner is the real console binary as a
/// process started by a separate <c>run-all</c> process — so nothing the test host owns is its parent — and the launcher itself is driven in process with a shell where the child's own state is the subject. Linux x86-64 only (the constants are measured there; skipped elsewhere with the reason).
/// <para>
/// What this does NOT prove: the Windows launcher (no Windows host here); macOS and FreeBSD (refused, <c>RunAllVerbTests</c>); a <c>setsid</c> that is not util-linux's (its fork-when-a-leader behaviour is measured on 2.41.3); that a runner SURVIVES a closed terminal (it is in its own session and group; no hang-up was sent).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class DetachedLauncherTests
{
    private static readonly TimeSpan Fast = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task An_arm_whose_lock_is_held_is_left_alone()
    {
        Assert.SkipUnless(File.Exists("/usr/bin/sleep"), "the holder's child is /usr/bin/sleep, started by its absolute path");
        await using var kit = await RunAllKit.MakeAsync("C1");
        Directory.CreateDirectory(kit.Layout.Pids);
        await using var holder = ModelArmsBinary.StartHolder(kit.Root, kit.Layout.RunnerLock("C1"), "/usr/bin/sleep", "60");
        await holder.LineAsync("child ", Fast);

        var run = await kit.RunAsync("C1");
        await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        run.Code.Should().Be(0, run.Error + run.Output);
        run.Output.Should().Contain("C1: already running");
        kit.Fake.Calls().Should().BeEmpty("a second runner would have started a step");
        kit.LogOf("C1").Should().BeEmpty("nothing was started, so nothing wrote the log — a second runner's exit 4 would be a line in it");
    }

    [Fact]
    public async Task A_started_arm_survives_its_launchers_exit()
    {
        await using var kit = await RunAllKit.MakeAsync("C1");
        kit.Script(new JsonObject { ["holdAfterLine"] = 1 });
        var clock = Stopwatch.StartNew();

        var run = await kit.RunAsync("C1");
        var returned = clock.Elapsed;

        run.Code.Should().Be(0, run.Error + run.Output);
        returned.Should().BeLessThan(TimeSpan.FromSeconds(10), "run-all returns once the operating system took the start: a runner holding run-all's stdout would keep it waiting");
        var pid = await kit.RunnerPidAsync("C1", Fast);
        (await IsolationKit.UntilAsync(() => kit.Fake.Calls().Count > 0, Fast)).Should().BeTrue("the runner started its first step");
        await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        Directory.Exists($"/proc/{pid}").Should().BeTrue("the runner outlived run-all by 3 s");
        RunnerLiveness.Of(kit.Layout, "C1").Should().BeOfType<RunnerState.Alive>();
        var parent = RunAllKit.ParentOf(pid);
        parent.Should().NotBe(Environment.ProcessId, "the runner is not a child of the test process");
        (Directory.Exists($"/proc/{parent}") && RunAllKit.CommandLineOf(parent).Contains("run-all")).Should().BeFalse("nor of a run-all: setsid forked and the runner was reparented");
        RunAllKit.SessionOf(pid).Should().Be(pid, "the runner leads a session of its own (setsid)");
    }

    [Fact]
    public async Task The_detached_arm_is_launched_by_the_modules_own_resolved_path()
    {
        await using var kit = await RunAllKit.MakeAsync("C1");
        kit.Script(new JsonObject { ["holdAfterLine"] = 1 });

        var run = await kit.RunAsync("C1");
        run.Code.Should().Be(0, run.Error + run.Output);
        var pid = await kit.RunnerPidAsync("C1", Fast);

        var real = RealPathOf(ExperimentRoot.ConsoleBinary);
        RunAllKit.CommandLineOf(pid).Should().Equal(real, "run-arm", "C1", "--experiment", kit.Root.Root);
        RealPathOf($"/proc/{pid}/exe").Should().Be(real, "the process IS the module's binary, not a test host and not dotnet");
        run.Output.Should().Contain($"model-arms → {real}").And.Contain($"setsid → {kit.Setsid}");
    }

    [Fact]
    public async Task The_pid_file_is_the_decimal_pid_status_and_checks_already_read()
    {
        await using var kit = await RunAllKit.MakeAsync("C1");
        kit.Script(new JsonObject { ["holdAfterLine"] = 1 });

        var run = await kit.RunAsync("C1");
        run.Code.Should().Be(0, run.Error + run.Output);
        var pid = await kit.RunnerPidAsync("C1", Fast);
        (await IsolationKit.UntilAsync(() => kit.Fake.Calls().Count > 0, Fast)).Should().BeTrue("the runner started its first step");

        File.ReadAllText(kit.Layout.PidFile("C1")).Should().MatchRegex(@"^[1-9][0-9]*\n\z");
        pid.Should().Be(RunAllKit.ParentOf(kit.Fake.Calls()[0].Pid), "the pid in the file is the runner's own — the parent of the step it started");
        var output = new StringWriter();
        var status = await ModelArmsApp.Program.RunAsync(["status", "--experiment", kit.Root.Root], output, new StringWriter(), kit.Root.Environment, TestContext.Current.CancellationToken);
        status.Should().Be(0, output.ToString());
        output.ToString().Should().Contain($"runner pid {pid} alive", "status reads the file as it always has");
    }

    [Fact]
    public async Task The_detached_runner_reads_the_null_device_and_writes_its_log_through_the_os_redirection()
    {
        await using var kit = await RunAllKit.MakeAsync("C1");
        kit.Script(new JsonObject { ["holdAfterLine"] = 1 });

        var run = await kit.RunWithStdinAsync("C1");
        run.Code.Should().Be(0, run.Error + run.Output);
        var pid = await kit.RunnerPidAsync("C1", Fast);

        var log = RealPathOf(kit.Layout.PidLog("C1"));
        Target($"/proc/{pid}/fd/0").Should().Be("/dev/null", "run-all's own stdin was a pipe; the runner reads the null device");
        Target($"/proc/{pid}/fd/1").Should().Be(log, "stdout is the log, opened by the operating system for the runner");
        Target($"/proc/{pid}/fd/2").Should().Be(log, "stderr is the same file as stdout");
        (await IsolationKit.UntilAsync(() => kit.LogOf("C1").Contains("resolved executables:", StringComparison.Ordinal), Fast)).Should().BeTrue("the runner's own output went to the log: " + kit.LogOf("C1"));
        (File.GetUnixFileMode(kit.Layout.PidLog("C1")) & (UnixFileMode)0x1FF).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite, "the log is created at 0600");
    }

    [Fact]
    public async Task Every_signal_is_default_and_unblocked_in_the_detached_child()
    {
        RunAllKit.Require();
        using var temp = new TempFolder();
        var log = temp.Combine("child.log");

        var started = new SetsidLauncher(RunAllKit.HostSetsid).Start("/bin/sh", ["-c", "grep -E 'SigIgn|SigBlk' /proc/$$/status"], log, ChildEnvironment(), temp.Path);

        started.Should().BeOfType<DetachedStart.Started>();
        var text = await LogAsync(log, "SigIgn");
        text.Should().MatchRegex(@"SigIgn:\s+0000000000000000", "SIGPIPE is ignored by this runtime and would cross exec without the defaulted set (M-3)");
        text.Should().MatchRegex(@"SigBlk:\s+0000000000000000");
    }

    [Fact]
    public async Task The_detached_child_is_not_a_child_of_its_launcher()
    {
        RunAllKit.Require();
        using var temp = new TempFolder();
        var log = temp.Combine("child.log");

        var started = new SetsidLauncher(RunAllKit.HostSetsid).Start("/bin/sh", ["-c", "echo $PPID; sleep 2"], log, ChildEnvironment(), temp.Path);

        started.Should().BeOfType<DetachedStart.Started>();
        var parent = int.Parse((await LogAsync(log, "\n")).Trim(), System.Globalization.CultureInfo.InvariantCulture);
        parent.Should().NotBe(Environment.ProcessId, "setsid forked, because the child leads its own group (M-2): without that it IS the runner and its parent is the launcher (M-1)");
        await Task.Delay(TimeSpan.FromSeconds(2.5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_log_that_is_a_link_is_refused_and_nothing_starts()
    {
        await using var kit = await RunAllKit.MakeAsync("C1");
        var elsewhere = Path.Combine(kit.Root.Temp, "elsewhere");
        File.WriteAllText(elsewhere, "unchanged\n");
        Directory.CreateDirectory(kit.Layout.Pids);
        File.CreateSymbolicLink(kit.Layout.PidLog("C1"), elsewhere);

        var run = await kit.RunAsync("C1");
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        run.Code.Should().Be(4, run.Error + run.Output);
        run.Error.Should().Contain($"{kit.Layout.PidLog("C1")} is a link — refused");
        File.ReadAllText(elsewhere).Should().Be("unchanged\n", "the link was not followed: no runner wrote through it");
        kit.Fake.Calls().Should().BeEmpty();
        kit.PidOf("C1").Should().BeNull("no runner started");
    }

    [Theory]
    [InlineData(true, false, System.Runtime.InteropServices.Architecture.X64, "Setsid")]
    [InlineData(true, false, System.Runtime.InteropServices.Architecture.Arm64, "Unmeasured")]
    [InlineData(false, true, System.Runtime.InteropServices.Architecture.X64, "Windows")]
    [InlineData(false, true, System.Runtime.InteropServices.Architecture.Arm64, "Windows")]
    [InlineData(false, false, System.Runtime.InteropServices.Architecture.X64, "Unmeasured")]
    [InlineData(false, false, System.Runtime.InteropServices.Architecture.Arm64, "Unmeasured")]
    public void The_road_is_an_explicit_table_with_no_catch_all(bool linux, bool windows, System.Runtime.InteropServices.Architecture architecture, string expected)
    {
        DetachedRoads.For(linux, windows, architecture).ToString().Should().Be(expected, "macOS, FreeBSD and Linux arm64 are not measured: they fail closed, they do not inherit Linux x86-64's constants (common.platform-limits)");
    }

    [Fact]
    public void Every_structure_libc_fills_fits_the_buffers_it_is_given()
    {
        PosixSpawn.Capacity.Should().BeGreaterThan(PosixSpawn.LargestMeasured, "80, 336 and 128 bytes measured with a C probe on this host (glibc 2.43, x86-64)");
        PosixSpawn.LargestMeasured.Should().Be(PosixSpawn.AttributesBytes);
    }

    private static Dictionary<string, string> ChildEnvironment() => new() { ["PATH"] = "/usr/bin:/bin" };

    private static string RealPathOf(string path) => (File.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName) ?? Path.GetFullPath(path);

    private static string Target(string link) => File.ResolveLinkTarget(link, returnFinalTarget: false)!.FullName;

    /// <summary>The log's text once it holds <paramref name="needle"/> (the child writes after <c>run-all</c>'s caller has long been told it started).</summary>
    private static async Task<string> LogAsync(string log, string needle)
    {
        var text = string.Empty;
        (await IsolationKit.UntilAsync(() => File.Exists(log) && (text = File.ReadAllText(log)).Contains(needle, StringComparison.Ordinal), TimeSpan.FromSeconds(15))).Should().BeTrue($"the detached child writes its log: {text}");
        return text;
    }
}
