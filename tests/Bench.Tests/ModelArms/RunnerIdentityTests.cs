using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bench.Tests.Probes.Driver;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Verbs;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The runner's identity (E5.S1, D13): a <c>run-arm</c> process holds <c>pids/&lt;arm&gt;.lock</c> for its life, writes its OWN pid, and holds <c>.isolation.lock</c> shared; <see cref="RunnerLiveness"/> answers from the lock — a pid is believed only when its command line is this arm's runner (Linux) — and creates
/// nothing; every refusal after the locks releases them. The runner is the real console as a process where a real death matters, and in process where a refusal does. Linux only.
/// <para>
/// What this does NOT prove: a reused pid whose argv happens to read <c>… model-arms run-arm C1</c> (believed — the residual, said); the share mode on Windows; a runner killed by SIGSTOP (a wedged holder is the gate lock's residual).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class RunnerIdentityTests
{
    private static ExperimentRoot Root()
    {
        var root = new ExperimentRoot();
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the runner's locks, /proc and the fake are Linux's here");
        return root;
    }

    private static async Task<(ExperimentRoot Root, FakeCli Fake)> HangingArmAsync()
    {
        var root = Root();
        var fake = new FakeCli();
        await root.MakeArmAsync("C1", fake);
        fake.Rewrite(ExperimentRoot.Script(new JsonObject { ["hangOnCall"] = 1 }));
        return (root, fake);
    }

    private static RootPaths Layout(ExperimentRoot root) => new(root.Root);

    private static async Task AliveAsync(ExperimentRoot root, FakeCli fake, ModelArmsBinary.Child runner) =>
        (await IsolationKit.UntilAsync(() => File.Exists(Layout(root).PidFile("C1")) && fake.Calls().Count > 0, TimeSpan.FromSeconds(30))).Should().BeTrue("the runner writes its pid and starts its first step: " + runner.Printed());

    private static int ParentOf(int pid)
    {
        var stat = File.ReadAllText($"/proc/{pid}/stat");
        return int.Parse(stat[(stat.LastIndexOf(')') + 2)..].Split(' ')[1], CultureInfo.InvariantCulture);
    }

    private static int HolderPid(ModelArmsBinary.Child holder) => int.Parse(holder.Printed().Split('|')[0].Trim().Split(' ')[1], CultureInfo.InvariantCulture);

    private static int ChildPid(ModelArmsBinary.Child holder) => int.Parse(holder.Printed().Split('|')[1].Trim().Split(' ')[1], CultureInfo.InvariantCulture);

    [Fact]
    public async Task The_runner_holds_its_arm_lock_for_its_life_and_writes_its_own_pid()
    {
        var (root, fake) = await HangingArmAsync();
        using var rootScope = root;
        using var fakeScope = fake;
        await using var runner = ModelArmsBinary.Start(root, "run-arm", "C1");
        await AliveAsync(root, fake, runner);
        var text = File.ReadAllText(Layout(root).PidFile("C1"));
        var gate = FileGateLock.Create(TimeProvider.System, TimeSpan.FromMilliseconds(50)).Ok();

        text.Should().MatchRegex(@"^[1-9][0-9]*\n\z");
        var pid = RunnerPid.Parse(text.Trim())!.Value;
        pid.Should().Be(ParentOf(fake.Calls()[0].Pid), "the pid is the runner's own — the fake's parent — not a launcher's");
        pid.Should().NotBe(Environment.ProcessId);
        gate.Take(Layout(root).RunnerLock("C1"), create: false, note: false).Should().BeOfType<GateLockAnswer.Busy>();
        RunnerLiveness.Of(Layout(root), "C1").Should().Be(new RunnerState.Alive($"pid {pid}", pid));

        await runner.KillAsync();

        (await IsolationKit.UntilAsync(() => gate.Take(Layout(root).RunnerLock("C1"), create: false, note: false) is GateLockAnswer.Held held && Release(held), TimeSpan.FromSeconds(5))).Should().BeTrue("the kernel frees the lock the instant the runner dies");
        RunnerLiveness.Of(Layout(root), "C1").Should().BeOfType<RunnerState.Gone>("the pid file still names the dead runner: a pid never decides");
        RunnerPid.Parse(File.ReadAllText(Layout(root).PidFile("C1")).Trim()).Should().Be(pid);
    }

    private static bool Release(GateLockAnswer.Held held)
    {
        held.Handle.Dispose();
        return true;
    }

    [Fact]
    public async Task A_second_runner_of_the_same_arm_refuses_with_exit_4_naming_the_holder_before_any_step()
    {
        var (root, fake) = await HangingArmAsync();
        using var rootScope = root;
        using var fakeScope = fake;
        await using var first = ModelArmsBinary.Start(root, "run-arm", "C1");
        await AliveAsync(root, fake, first);
        var pid = int.Parse(File.ReadAllText(Layout(root).PidFile("C1")).Trim(), CultureInfo.InvariantCulture);
        root.ReplaceGit($"echo \"$0 $*\" >> '{root.MarkerFile("git-second")}'\nexit 0\n");

        var second = await ModelArmsBinary.RunAsync(root, "run-arm", "C1");

        second.Code.Should().Be(4, second.Output + second.Error);
        second.Error.Should().Contain($"another runner holds {Layout(root).RunnerLock("C1")} — by pid {pid} since");
        fake.Calls().Should().ContainSingle("the second runner started no step");
        root.Markers().Should().BeEmpty("the lock is taken before anything is read or run: no git ran");
        File.Exists(root.PathsOf("C1").Stream("1-code", 2)).Should().BeFalse();
    }

    [Theory]
    [InlineData("place")]
    [InlineData("equip")]
    [InlineData("floor")]
    public async Task Every_refusal_after_the_locks_releases_them_so_the_next_runner_can_take_them_at_once(string refusal)
    {
        using var root = Root();
        using var fake = new FakeCli();
        await root.MakeArmAsync("C1", fake);
        RunArmKit.Replay(fake, "result-success.jsonl");
        var map = File.ReadAllText(Layout(root).ArmMapFile);
        var bin = Path.Combine(root.Temp, "bin");
        var git = Path.Combine(bin, "git");
        switch (refusal)
        {
            case "place": File.WriteAllText(Layout(root).ArmMapFile, "{}"); break;
            case "equip": root.UsePath(bin + Path.PathSeparator + Path.Combine(root.Root, "tools")); break;
            default: root.ReplaceGit(Sentinel.Body(root.MarkerFile("git-calls"), "2.39.3")); break;
        }

        var refused = await RunArmKit.RunAsync(root, "C1");

        refused.Code.Should().BeOneOf(3, 4);
        refused.Code.Should().Be(refusal == "floor" ? 3 : 4, refused.Error);
        var gate = FileGateLock.Create(TimeProvider.System, TimeSpan.FromMilliseconds(50)).Ok();
        var runner = gate.Take(Layout(root).RunnerLock("C1"), create: false, note: false);
        var isolation = gate.Take(Layout(root).IsolationLock, create: false, note: false);
        runner.Should().BeOfType<GateLockAnswer.Held>("a refusal after the locks must release them at once, not at process exit");
        isolation.Should().BeOfType<GateLockAnswer.Held>();
        (runner as GateLockAnswer.Held)?.Handle.Dispose();
        (isolation as GateLockAnswer.Held)?.Handle.Dispose();

        File.WriteAllText(Layout(root).ArmMapFile, map);
        root.UsePath(bin);
        File.Delete(git);
        File.CreateSymbolicLink(git, ExperimentRoot.RealGit);
        var repaired = await RunArmKit.RunAsync(root, "C1");
        repaired.Code.Should().Be(0, repaired.Error + repaired.Output);
    }

    [Fact]
    public async Task An_arm_whose_lock_is_held_is_alive_with_or_without_a_pid_file()
    {
        using var root = Root();
        Directory.CreateDirectory(Layout(root).Pids);
        await using var holder = ModelArmsBinary.StartHolder(root, Layout(root).RunnerLock("C1"), FakeCli.Executable);
        await holder.LineAsync("child ", TimeSpan.FromSeconds(30));
        var pid = HolderPid(holder);

        var without = RunnerLiveness.Of(Layout(root), "C1");

        without.Should().BeOfType<RunnerState.Alive>().Which.Holder.Should().StartWith($"no pid file yet — the lock is held by pid {pid} since");
        ((RunnerState.Alive)without).BelievedPid.Should().BeNull();

        File.WriteAllText(Layout(root).PidFile("C1"), pid.ToString(CultureInfo.InvariantCulture) + "\n");
        var with = RunnerLiveness.Of(Layout(root), "C1");

        with.Should().BeOfType<RunnerState.Alive>().Which.Holder.Should().Contain($"pid {pid} is not this arm's runner (its command line is gate-holder").And.EndWith("— read as gone");
        ((RunnerState.Alive)with).BelievedPid.Should().BeNull();

        await holder.KillAsync();
        (await IsolationKit.UntilAsync(() => RunnerLiveness.Of(Layout(root), "C1") is RunnerState.Gone, TimeSpan.FromSeconds(5))).Should().BeTrue("a killed holder frees the lock: gone");
    }

    [Fact]
    public async Task A_live_pid_whose_command_line_is_not_this_arms_runner_reads_as_gone()
    {
        using var root = Root();
        Directory.CreateDirectory(Layout(root).Pids);
        await using var holder = ModelArmsBinary.StartHolder(root, Layout(root).RunnerLock("C1"), FakeCli.Executable);
        await holder.LineAsync("child ", TimeSpan.FromSeconds(30));
        var fakePid = ChildPid(holder);
        File.WriteAllText(Layout(root).PidFile("C1"), fakePid.ToString(CultureInfo.InvariantCulture) + "\n");

        var state = RunnerLiveness.Of(Layout(root), "C1");

        var alive = state.Should().BeOfType<RunnerState.Alive>().Subject;
        alive.BelievedPid.Should().BeNull($"pid {fakePid} is alive but it is the fake, not a runner of C1 — a stub believing any live pid answers {fakePid}");
        alive.Holder.Should().Contain("is not this arm's runner").And.Contain("fake-cli");
    }

    [Fact]
    public async Task A_runner_whose_lock_file_was_unlinked_is_still_alive_by_its_believed_pid()
    {
        using var kit = new IsolationKit();
        using var fake = new FakeCli();
        await kit.AddArmAsync("C1", fake);
        (await kit.LockAsync()).Code.Should().Be(0);
        await using var runner = kit.StartRunner("C1");
        await AliveAsync(kit.Root, fake, runner);
        var pid = int.Parse(File.ReadAllText(kit.Layout.PidFile("C1")).Trim(), CultureInfo.InvariantCulture);

        File.Delete(kit.Layout.RunnerLock("C1"));

        RunnerLiveness.Of(kit.Layout, "C1").Should().Be(new RunnerState.Alive($"pid {pid} — its lock file is gone or replaced (unlinked since it started); believed by its command line", pid));
        var unlock = await kit.UnlockAsync(clock: TimeProvider.System, wait: TimeSpan.FromSeconds(5));
        unlock.Code.Should().Be(4, unlock.Output + unlock.Error);
        unlock.Error.Should().Contain("C1 is alive");

        await runner.KillAsync();

        RunnerLiveness.Of(kit.Layout, "C1").Should().BeOfType<RunnerState.Gone>("the runner is dead: its pid is not believed");
    }

    [Fact]
    public async Task On_the_portable_road_the_pid_reuse_residual_is_printed()
    {
        using var kit = new IsolationKit();
        using var fake = new FakeCli();
        await kit.AddArmAsync("C1", fake, hang: false);
        (await kit.LockAsync()).Code.Should().Be(0);
        await using var holder = ModelArmsBinary.StartHolder(kit.Root, kit.Layout.RunnerLock("C1"), FakeCli.Executable);
        await holder.LineAsync("child ", TimeSpan.FromSeconds(30));
        var fakePid = ChildPid(holder);
        File.WriteAllText(kit.Layout.PidFile("C1"), fakePid.ToString(CultureInfo.InvariantCulture) + "\n");
        var gate = FileGateLock.Create(TimeProvider.System, TimeSpan.FromMilliseconds(50), PlatformKit.MacRoad).Ok();

        var state = RunnerLiveness.Of(kit.Layout, "C1", PlatformKit.MacRoad, gate);

        state.Should().Be(new RunnerState.Alive($"pid {fakePid} (not checked against its command line on {PlatformKit.MacRoad.Platform})", fakePid));
        RunnerLiveness.PortableLine(PlatformKit.MacRoad).Should().Contain("NOT checked against its command line");
        RunnerLiveness.PortableLine(PlatformFactsReader.CurrentRoad).Should().BeEmpty();

        var unlock = await kit.UnlockAsync(road: PlatformKit.MacRoad, clock: TimeProvider.System);

        unlock.Code.Should().Be(4, unlock.Output + unlock.Error);
        Regex.Matches(unlock.Output, "NOT checked against its command line").Should().ContainSingle("printed once, before the first liveness answer");
    }

    [Fact]
    public void RunnerLiveness_creates_nothing_and_releases_a_lock_it_gets()
    {
        using var root = Root();
        var layout = Layout(root);

        Directory.CreateDirectory(layout.Pids);
        RunnerLiveness.Of(layout, "C1").Should().BeOfType<RunnerState.Gone>();
        File.Exists(layout.RunnerLock("C1")).Should().BeFalse("no lock file is created by a probe");

        File.WriteAllText(layout.RunnerLock("C1"), string.Empty);
        RunnerLiveness.Of(layout, "C1").Should().BeOfType<RunnerState.Gone>();
        var gate = FileGateLock.Create(TimeProvider.System, TimeSpan.FromMilliseconds(50)).Ok();
        var after = gate.Take(layout.RunnerLock("C1"), create: false, note: false);
        after.Should().BeOfType<GateLockAnswer.Held>("a lock the probe got is released before it returns");
        ((GateLockAnswer.Held)after).Handle.Dispose();

        var elsewhere = Path.Combine(root.Temp, "elsewhere.lock");
        File.WriteAllText(elsewhere, string.Empty);
        File.Delete(layout.RunnerLock("C1"));
        File.CreateSymbolicLink(layout.RunnerLock("C1"), elsewhere);
        RunnerLiveness.Of(layout, "C1").Should().BeOfType<RunnerState.Refused>("a link where the lock file must be is refused");

        File.Delete(layout.RunnerLock("C1"));
        File.WriteAllText(layout.RunnerLock("C1"), string.Empty);
        var dishonoured = new FileGateLock(TimeProvider.System, TimeSpan.FromMilliseconds(50)) { SecondOpen = _ => new FileStream(elsewhere, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite) };
        RunnerLiveness.Of(layout, "C1", PlatformFactsReader.CurrentRoad, dishonoured).Should().BeOfType<RunnerState.Unread>("a file system that does not honour the lock cannot say");

        Directory.Delete(layout.Pids, recursive: true);
        RunnerLiveness.Of(layout, "C1").Should().BeOfType<RunnerState.Gone>();
        Directory.Exists(layout.Pids).Should().BeFalse("a probe creates nothing — pids/ is not made");
    }

    [Fact]
    public async Task The_runner_waits_a_moment_for_a_probes_momentary_hold_then_refuses()
    {
        using var root = Root();
        using var fake = new FakeCli();
        await root.MakeArmAsync("C1", fake);
        RunArmKit.Replay(fake, "result-success.jsonl");
        var layout = Layout(root);
        Directory.CreateDirectory(layout.Pids);
        foreach (var (path, sentence) in new[] { (layout.RunnerLock("C1"), "another runner holds"), (layout.IsolationLock, "the isolation is being changed") })
        {
            var gate = FileGateLock.Create(TimeProvider.System, TimeSpan.FromMilliseconds(50)).Ok();
            var brief = gate.Take(path, create: true, note: true).Should().BeOfType<GateLockAnswer.Held>().Subject;
            var release = Task.Delay(500, TestContext.Current.CancellationToken).ContinueWith(_ => brief.Handle.Dispose(), TaskScheduler.Default);

            var waited = await RunArmKit.RunAsync(root, "C1");

            waited.Code.Should().Be(0, $"a hold of 500 ms is a probe's: the runner waits it out ({waited.Error})");
            await release;
            File.Delete(layout.PidFile("C1"));
            var longHold = gate.Take(path, create: true, note: true).Should().BeOfType<GateLockAnswer.Held>().Subject;
            var started = DateTimeOffset.UtcNow;

            var refused = await RunArmKit.RunAsync(root, "C1");

            (DateTimeOffset.UtcNow - started).Should().BeLessThan(TimeSpan.FromSeconds(4), "the wait has a ceiling of 2 s");
            refused.Code.Should().Be(4, refused.Error);
            refused.Error.Should().Contain(sentence);
            longHold.Handle.Dispose();
        }
    }

    [Fact]
    public async Task The_runner_makes_pids_by_name_and_refuses_a_link_there()
    {
        using var root = Root();
        using var fake = new FakeCli();
        await root.MakeArmAsync("C1", fake);
        RunArmKit.Replay(fake, "result-success.jsonl");
        var layout = Layout(root);
        Directory.Exists(layout.Pids).Should().BeFalse("nothing before the first run-arm made it");

        var made = await RunArmKit.RunAsync(root, "C1");

        made.Code.Should().Be(0, made.Error + made.Output);
        Directory.Exists(layout.Pids).Should().BeTrue("the runner made pids/ by name");
        File.GetUnixFileMode(layout.Pids).Should().Be((UnixFileMode)0x1C0);
        File.Exists(layout.PidFile("C1")).Should().BeTrue();
        File.Exists(layout.RunnerLock("C1")).Should().BeTrue();

        var elsewhere = root.Scratch("elsewhere");
        Directory.Delete(layout.Pids, recursive: true);
        Directory.CreateSymbolicLink(layout.Pids, elsewhere);
        var linked = await RunArmKit.RunAsync(root, "C1");
        linked.Code.Should().Be(4, linked.Error);
        linked.Error.Should().Contain($"{layout.Pids} is a symbolic link — refused (D17)");
        Directory.EnumerateFileSystemEntries(elsewhere).Should().BeEmpty("nothing was made through the link");

        File.Delete(layout.Pids);
        File.WriteAllText(layout.Pids, "a file");
        var file = await RunArmKit.RunAsync(root, "C1");
        file.Code.Should().Be(4, file.Error);
        file.Error.Should().Contain("is a regular file");
    }
}
