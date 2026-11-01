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

        var started = new SetsidLauncher(RunAllKit.HostSetsid).Start("/bin/sh", ["-c", "exec grep -E 'SigIgn|SigBlk' /proc/self/status"], log, ChildEnvironment(), temp.Path);

        started.Should().BeOfType<DetachedStart.Started>(started.ToString());
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

        started.Should().BeOfType<DetachedStart.Started>(started.ToString());
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
    [InlineData(false, true, System.Runtime.InteropServices.Architecture.X64, "2.43", "Setsid")]
    [InlineData(false, true, System.Runtime.InteropServices.Architecture.X64, "2.34", "Setsid")]
    [InlineData(false, true, System.Runtime.InteropServices.Architecture.X64, "2.33", "Unmeasured")]
    [InlineData(false, true, System.Runtime.InteropServices.Architecture.X64, "", "Unmeasured")]
    [InlineData(false, true, System.Runtime.InteropServices.Architecture.Arm64, "2.43", "Unmeasured")]
    [InlineData(false, false, System.Runtime.InteropServices.Architecture.X64, "", "Unmeasured")]
    [InlineData(false, false, System.Runtime.InteropServices.Architecture.Arm64, "", "Unmeasured")]
    [InlineData(true, false, System.Runtime.InteropServices.Architecture.X64, "", "Windows")]
    [InlineData(true, false, System.Runtime.InteropServices.Architecture.Arm64, "", "Windows")]
    public void The_road_is_an_explicit_table_keyed_on_system_cpu_and_c_library_with_no_catch_all(bool windows, bool linux, System.Runtime.InteropServices.Architecture architecture, string glibc, string expected)
    {
        DetachedRoads.For(windows, linux, architecture, glibc).ToString().Should().Be(expected, "musl (no glibc version), an old glibc, Linux arm64, macOS and FreeBSD are not measured: they fail closed, they do not inherit glibc x86-64's constants (common.platform-limits)");
    }

    [Fact]
    public void This_host_is_the_measured_one_and_names_it_in_the_sentence_every_other_host_reads()
    {
        RunAllKit.Require();
        PosixSpawn.GlibcVersion.Should().StartWith("2.", "glibc answers gnu_get_libc_version: 2.43 on the host it was measured on");
        PosixSpawn.CloseFromAvailable.Should().BeTrue("glibc 2.43 has posix_spawn_file_actions_addclosefrom_np; a libc without it is refused");
        DetachedRoads.Measured.Should().Contain("x86-64").And.Contain("glibc").And.Contain("Windows");
    }

    [Fact]
    public void Each_structure_libc_fills_is_as_large_as_the_calls_make_it_and_fits_the_buffers_it_is_given()
    {
        var (actions, attributes, signals) = PosixSpawn.BytesTouched();

        attributes.Should().Be(PosixSpawn.AttributesBytes, "posix_spawnattr_init clears the whole structure: the bytes it changed from a sentinel ARE its size (336 on glibc 2.43, x86-64)");
        signals.Should().BeInRange(8, PosixSpawn.SigsetBytes, "glibc's sigemptyset and sigaddset change only the 8 bytes the kernel's 64 signals need; the rest of the 128 is the library's padding, left alone");
        actions.Should().BeInRange(1, PosixSpawn.FileActionsBytes, "the file actions' fixed part (80 bytes) is all libc writes in the buffer; its list is allocated apart");
        new[] { actions, attributes, signals }.Should().OnlyContain(touched => touched < PosixSpawn.Capacity, "no call reached the end of its 512-byte buffer, let alone past it: the bytes beyond each size are untouched");
    }

    [Fact]
    public async Task A_descriptor_the_caller_left_open_does_not_reach_the_runner()
    {
        await using var kit = await RunAllKit.MakeAsync("C1");
        kit.Script(new JsonObject { ["holdAfterLine"] = 1 });
        var leaked = Path.Combine(kit.Root.Temp, "leaked.txt");
        File.WriteAllText(leaked, string.Empty);

        var run = await kit.RunWithLeakedDescriptorAsync(leaked, "C1");

        run.Code.Should().Be(0, run.Error + run.Output);
        var pid = await kit.RunnerPidAsync("C1", Fast);
        (await IsolationKit.UntilAsync(() => kit.Fake.Calls().Count > 0, Fast)).Should().BeTrue("the runner started its first step");
        var held = HeldBy(pid);
        held.Should().NotContain(leaked, "run-all's descriptor 3 (open without close-on-exec) is closed in the child: every descriptor from 3 up");
        var fake = kit.Fake.Calls()[0].Pid;
        HeldBy(fake).Should().NotContain(leaked, "nor does the CLI the runner starts hold it");
    }

    [Fact]
    public async Task A_log_that_is_a_fifo_is_refused_without_blocking_and_nothing_starts()
    {
        await using var kit = await RunAllKit.MakeAsync("C1");
        Directory.CreateDirectory(kit.Layout.Pids);
        MakeKit.Fifo(kit.Layout.PidLog("C1"));
        var clock = Stopwatch.StartNew();

        var run = await kit.RunAsync("C1");

        run.Code.Should().Be(4, run.Error + run.Output);
        run.Error.Should().Contain($"{kit.Layout.PidLog("C1")} is a FIFO with no reader — refused");
        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(20), "the open of a FIFO with no reader did not block the spawn");
        kit.Fake.Calls().Should().BeEmpty();
        kit.PidOf("C1").Should().BeNull("no runner started");
    }

    [Theory]
    [InlineData("actions", 12)]
    [InlineData("attributes", 22)]
    [InlineData("none", 0)]
    public void A_structure_is_destroyed_only_when_its_own_init_succeeded_and_every_allocation_is_freed(string failing, int error)
    {
        RunAllKit.Require();
        using var temp = new TempFolder();
        var events = new List<string>();
        var hooks = new SpawnHooks(
            actions => failing == "actions" ? error : PosixSpawn.InitActionsCall(actions),
            attributes => failing == "attributes" ? error : PosixSpawn.InitAttributesCall(attributes),
            events.Add);

        var result = PosixSpawn.Spawn(temp.Combine("no-such-program"), [temp.Combine("no-such-program")], ["A=b"], temp.Combine("child.log"), temp.Path, hooks);

        result.Pid.Should().Be(0);
        (failing == "none" ? result.Error != 0 : result.Error == error).Should().BeTrue($"the failing init's number is the result ({failing}); with none failing the start itself fails on the missing program: {result.Error}");
        events.Count(each => each == "alloc").Should().BeGreaterThanOrEqualTo(4);
        events.Last().Should().Be($"free {events.Count(each => each == "alloc")}", "every allocation, however far the start got, is freed — once");
        events.Contains("destroy actions").Should().Be(failing != "actions", "file actions are destroyed only if their init succeeded");
        events.Contains("destroy attributes").Should().Be(failing == "none", "attributes are destroyed only if their init succeeded — and not at all when the file actions failed first, which never reached them");
    }

    [Fact]
    public async Task A_log_keeps_one_earlier_run_not_every_one()
    {
        RunAllKit.Require();
        using var temp = new TempFolder();
        var log = temp.Combine("child.log");
        File.WriteAllText(log, "first run\n");
        var launcher = new SetsidLauncher(RunAllKit.HostSetsid);

        launcher.Start("/bin/sh", ["-c", "echo second run"], log, ChildEnvironment(), temp.Path).Should().BeOfType<DetachedStart.Started>();
        var second = await LogAsync(log, "second run");
        launcher.Start("/bin/sh", ["-c", "echo third run"], log, ChildEnvironment(), temp.Path).Should().BeOfType<DetachedStart.Started>();
        var third = await LogAsync(log, "third run");

        second.Should().NotContain("first run", "the earlier run moved aside before the new one opened the log");
        third.Should().NotContain("second run").And.NotContain("first run");
        File.ReadAllText(log + ".prev").Should().Be("second run\n", "the .prev is the run before, an older .prev replaced");
        Directory.GetFiles(temp.Path).Select(Path.GetFileName).Order().Should().Equal("child.log", "child.log.prev");
    }

    private static Dictionary<string, string> ChildEnvironment() => new() { ["PATH"] = "/usr/bin:/bin" };

    /// <summary>What a live process holds open: each <c>/proc/&lt;pid&gt;/fd</c> entry's real target. An entry the process closed between the listing
    /// and the read is skipped — the process is running, and a descriptor it closes in that window is not one it holds (seen once under the full suite's load).</summary>
    private static List<string> HeldBy(int pid) =>
        [.. Directory.EnumerateFileSystemEntries($"/proc/{pid}/fd").Select(TargetOrNone).Where(target => target.Length > 0)];

    private static string TargetOrNone(string entry)
    {
        try
        {
            return RealPathOf(entry);
        }
        catch (FileNotFoundException)
        {
            return string.Empty;
        }
        catch (DirectoryNotFoundException)
        {
            return string.Empty;
        }
    }

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
