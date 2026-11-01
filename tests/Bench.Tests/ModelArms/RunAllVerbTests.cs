using System.Text.Json.Nodes;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;
using ModelArms.Core.Steps;
using ModelArms.Verbs;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary><c>run-all</c> (E5.S3, D13, parent §4.7): which arms it starts, which it leaves, and what it refuses before it starts any. The refusal rows are in process through the gate with a RECORDING launcher and a liveness map (nothing is started, so the queue is arithmetic over made arms); the rows about an arm that ends, and about two arms at one gate lock, are the real console as a separate process starting real detached runners over the
/// fake CLI. Linux x86-64 only.
/// <para>
/// What this does NOT prove: a real <c>claude</c>; a runner that outlives a closed terminal; the wedged gate-lock holder (parent §4.7, §9: told apart only when a gate step's ceiling runs out); the chain's half of <i>never repeats an exit 5</i> beyond the one arm the first row hands it.
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class RunAllVerbTests
{
    private static readonly TimeSpan Fast = TimeSpan.FromSeconds(30);
    private static readonly string[] MadeArms = ["C1", "CR1", "C2", "CR2", "CHH1"];

    [Fact]
    public async Task A_runner_that_ended_without_arm_done_is_reported_and_never_started_again_by_run_all_or_chain()
    {
        await using var kit = await RunAllKit.MakeAsync("C1");
        RunArmKit.Replay(kit.Fake, "result-success.jsonl", (1, ModelArmsFixtures.Path("stream-no-result.jsonl")));

        var first = await kit.RunAsync("C1");
        first.Code.Should().Be(0, first.Error + first.Output);
        (await IsolationKit.UntilAsync(() => kit.PidOf("C1") is not null && RunnerLiveness.Of(kit.Layout, "C1") is RunnerState.Gone && kit.Fake.Calls().Count > 0, TimeSpan.FromSeconds(30))).Should().BeTrue("the runner started, hit a stream with no result and ended (exit 5): " + kit.LogOf("C1"));
        await Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        kit.Fake.Calls().Should().ContainSingle("run-all neither waited for its runner nor started it again when it ended: a relaunch of a verdictless attempt costs a paid attempt");
        kit.Root.TimingOf("C1").Last().Should().BeOfType<TimingLine.Stopped>().Which.Exit.Should().Be(5);
        kit.Root.ArmDones("C1").Should().BeEmpty();
        var session = File.ReadAllText(kit.Root.PathsOf("C1").Session("1-code")).Trim();

        // The chain's half (E5.S4, E5 §8 item 13): a chain over the same arm finds a runner that ran and is gone without arm-done, names the resume command and starts nothing.
        ChainKit.Provide(kit.Root);
        Directory.CreateDirectory(Path.Combine(kit.Root.Root, "base"));
        kit.Root.Edit(top =>
        {
            top["awake"] = "none";
            top["runs"] = new JsonObject { ["part6"] = new JsonObject { ["rounds"] = new JsonArray(new JsonArray("C1")) } };
        });
        var chain = await ModelArmsBinary.RunAsync(kit.Root, "chain", "part6");
        chain.Code.Should().Be(5, chain.Error + chain.Output);
        chain.Error.Should().Contain("C1 stopped without finishing").And.Contain("model-arms run-all C1");
        kit.Fake.Calls().Should().ContainSingle("the chain did not start the arm again: the operator's own run-all is the ask");
        kit.Root.ArmDones("C1").Should().BeEmpty();

        var second = await kit.RunAsync("C1");

        second.Code.Should().Be(0, second.Error + second.Output);
        second.Output.Should().Contain("C1: started").And.Contain("this is the resume you asked for");
        (await IsolationKit.UntilAsync(() => kit.Root.ArmDones("C1").Count == 1, TimeSpan.FromSeconds(90))).Should().BeTrue("the operator's own run-all resumed the arm to its end: " + kit.LogOf("C1"));
        kit.Fake.Calls()[1].After("--resume").Should().Be(session, "the second runner resumed the cut session");
    }

    [Fact]
    public async Task Two_arms_started_by_run_all_take_the_gate_lock_one_at_a_time()
    {
        await using var kit = await RunAllKit.MakeAsync("C1", "CR1");
        var slow = $"slow-{Guid.NewGuid():N}";
        kit.Script(new JsonObject { ["subjects"] = new JsonObject { [slow] = new JsonObject { ["lineDelayMs"] = 1500 } } });
        kit.Root.Edit(top => top["steps"]!["gate"]!["model"] = slow);

        var run = await kit.RunAsync("C1", "CR1");

        run.Code.Should().Be(0, run.Error + run.Output);
        (await IsolationKit.UntilAsync(() => kit.Root.ArmDones("C1").Count == 1 && kit.Root.ArmDones("CR1").Count == 1, TimeSpan.FromSeconds(180))).Should().BeTrue("both runners reach arm-done: " + kit.LogOf("C1") + kit.LogOf("CR1"));
        var gates = new[] { "C1", "CR1" }.Select(arm => Gate(kit, arm)).ToList();
        (gates[0].End <= gates[1].Start || gates[1].End <= gates[0].Start).Should().BeTrue($"the gate steps ran under the one gate lock, one at a time: {gates[0]} and {gates[1]}");
        (kit.LogOf("C1") + kit.LogOf("CR1")).Should().Contain("the gate lock is held by pid", "one of the two waited for the other's gate, and said so in its log");
    }

    private static (DateTimeOffset Start, DateTimeOffset End) Gate(RunAllKit kit, string arm)
    {
        var timing = kit.Root.TimingOf(arm);
        var start = timing.OfType<TimingLine.Start>().Single(line => line.Step.EndsWith("gate", StringComparison.Ordinal));
        var end = timing.OfType<TimingLine.End>().Single(line => line.Step.EndsWith("gate", StringComparison.Ordinal));
        return (start.StartUtc, end.EndUtc);
    }

    [Fact]
    public async Task A_second_run_all_straight_after_the_first_counts_the_arms_the_first_started_and_two_at_once_never_leave_four_alive()
    {
        await using var kit = await RunAllKit.MakeAsync("C1", "CR1", "C2", "CR2");
        kit.Script(new JsonObject { ["holdAfterLine"] = 1 });

        var first = await kit.RunAsync("C1", "CR1", "C2");
        var second = await kit.RunAsync("CR2");

        first.Code.Should().Be(0, first.Error + first.Output);
        second.Code.Should().Be(4, "the first call returned only when its three runners held their locks, so the second counts them: 3 + 1 > a queue of 3 — " + second.Error + second.Output);
        second.Error.Should().Contain("4 arms would be alive at once");
        kit.AliveCount().Should().Be(3);
        kit.PidOf("CR2").Should().BeNull("the refused arm started nothing");
    }

    [Fact]
    public async Task Run_all_holds_its_root_lock_until_each_runner_it_started_has_taken_its_own()
    {
        await using var kit = await RunAllKit.MakeAsync("C1", "CR1", "C2", "CR2");
        var started = new System.Collections.Concurrent.ConcurrentDictionary<string, long>();
        var launcher = new RecordingLauncher(arm => started[arm] = TimeProvider.System.GetTimestamp());
        var delay = TimeSpan.FromMilliseconds(800);
        RunnerState Liveness(RootPaths _, string arm) =>
            started.TryGetValue(arm, out var at) && TimeProvider.System.GetElapsedTime(at) >= delay ? new RunnerState.Alive("pid 4242", 4242) : new RunnerState.Gone();
        var seams = new RunAllSeams(_ => launcher, Liveness, DetachedRoad.Setsid, TimeSpan.FromSeconds(10));

        var first = Task.Run(() => kit.InProcessAsync(seams, "C1", "CR1", "C2"), TestContext.Current.CancellationToken);
        (await IsolationKit.UntilAsync(() => started.Count == 3, Fast)).Should().BeTrue("the first call started its three arms");
        var second = await kit.InProcessAsync(seams, "CR2");
        var done = await first;

        second.Code.Should().Be(4, "the first call is still waiting for its runners to take their locks and holds the root lock: " + second.Error + second.Output);
        second.Error.Should().Contain("another run-all holds");
        done.Code.Should().Be(0, done.Error + done.Output);
        done.Error.Should().NotContain("did not take its lock");
        started.Keys.Should().BeEquivalentTo("C1", "CR1", "C2");
        started.Keys.Select(arm => Liveness(kit.Layout, arm)).Should().AllBeOfType<RunnerState.Alive>("the call returned only once every runner it started held its lock");
    }

    [Fact]
    public async Task A_launcher_that_cannot_keep_the_log_makes_run_all_say_so_and_one_that_can_does_not()
    {
        await using var kit = await RunAllKit.MakeAsync("C1");

        var silent = await kit.InProcessAsync(RunAllKit.Seams(new RecordingLauncher { WritesLog = false }, new Dictionary<string, string>()), "C1");
        var logged = await kit.InProcessAsync(RunAllKit.Seams(new RecordingLauncher(), new Dictionary<string, string>()), "C1");

        silent.Code.Should().Be(0, silent.Error);
        silent.Output.Should().Contain("the runners' output is not kept in pids/<arm>.log").And.Contain("not observed");
        logged.Output.Should().NotContain("not kept in pids");
    }

    [Fact]
    public async Task Run_all_says_it_is_waiting_stops_waiting_for_a_runner_that_has_already_ended_and_points_at_its_log()
    {
        await using var kit = await RunAllKit.MakeAsync("C1");
        Directory.CreateDirectory(kit.Layout.Pids);
        var launcher = new RecordingLauncher(_ => File.WriteAllText(kit.Layout.PidFile("C1"), "4242\n"));
        var seams = new RunAllSeams(_ => launcher, (_, _) => new RunnerState.Gone(), DetachedRoad.Setsid, TimeSpan.FromSeconds(10));
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var run = await kit.InProcessAsync(seams, "C1");

        run.Code.Should().Be(0, run.Error + run.Output);
        run.Output.Should().Contain("waiting up to 15 s for the runners to take their locks (C1)");
        run.Error.Should().Contain("C1: its runner started and has already ended — see pids/C1.log");
        run.Error.Should().NotContain("did not take its lock");
        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5), "an arm whose runner ended is not waited for until the bound");
    }

    [Fact]
    public async Task A_pid_file_rewritten_in_the_same_tick_by_a_new_runner_is_still_seen_and_its_ending_flushed_to_the_error_writer()
    {
        await using var kit = await RunAllKit.MakeAsync("C1");
        Directory.CreateDirectory(kit.Layout.Pids);
        var pidFile = kit.Layout.PidFile("C1");
        File.WriteAllText(pidFile, "4242\n");
        var tick = File.GetLastWriteTimeUtc(pidFile);
        var launcher = new RecordingLauncher(_ =>
        {
            File.WriteAllText(pidFile, "4343\n");
            File.SetLastWriteTimeUtc(pidFile, tick);
        });
        var seams = new RunAllSeams(_ => launcher, (_, _) => new RunnerState.Gone(), DetachedRoad.Setsid, TimeSpan.FromSeconds(10));
        var error = new FlushSpy();
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var run = await kit.InProcessAsync(seams, new StringWriter(), error, "C1");

        File.GetLastWriteTimeUtc(pidFile).Should().Be(tick, "the rewrite kept the file's time: only the content tells");
        run.Error.Should().Contain("C1: its runner started and has already ended — see pids/C1.log");
        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5), "the ended runner was seen at once, not at the bound");
        error.FlushedAfterLastLine.Should().BeTrue("the notice is flushed to the error writer as soon as it is written");
    }

    [Fact]
    public async Task A_runner_that_took_its_lock_between_the_two_looks_is_alive_not_ended()
    {
        await using var kit = await RunAllKit.MakeAsync("C1");
        Directory.CreateDirectory(kit.Layout.Pids);
        var launcher = new RecordingLauncher(_ => File.WriteAllText(kit.Layout.PidFile("C1"), "4343\n"));
        var asked = 0;
        var seams = new RunAllSeams(_ => launcher, (_, _) => launcher.Starts.Count == 0 || Interlocked.Increment(ref asked) == 1 ? new RunnerState.Gone() : new RunnerState.Alive("pid 4343", 4343), DetachedRoad.Setsid, TimeSpan.FromSeconds(10));

        var run = await kit.InProcessAsync(seams, "C1");

        run.Code.Should().Be(0, run.Error + run.Output);
        run.Error.Should().NotContain("already ended", "the lock was held on the second look");
        run.Error.Should().NotContain("did not take its lock");
    }

    [Fact]
    public async Task The_wait_polls_with_a_backoff_from_50_ms_to_500_ms_and_a_runner_that_never_shows_is_pointed_at_its_log()
    {
        await using var kit = await RunAllKit.MakeAsync("C1");
        var launcher = new RecordingLauncher();
        var polls = new List<long>();
        var seams = new RunAllSeams(_ => launcher, (_, _) =>
        {
            if (launcher.Starts.Count > 0)
            {
                polls.Add(TimeProvider.System.GetTimestamp());
            }

            return new RunnerState.Gone();
        }, DetachedRoad.Setsid, TimeSpan.FromSeconds(3));

        var run = await kit.InProcessAsync(seams, "C1");

        run.Code.Should().Be(0, run.Error + run.Output);
        run.Error.Should().Contain("C1: its runner did not take its lock within 15 s — see pids/C1.log");
        var gaps = polls.Zip(polls.Skip(1), (a, b) => TimeProvider.System.GetElapsedTime(a, b).TotalMilliseconds).ToList();
        gaps.Count.Should().BeGreaterThan(5);
        gaps[0].Should().BeInRange(30, 200, "the first pause is about 50 ms");
        gaps.Should().OnlyContain(gap => gap < 900, "no pause is longer than the 500 ms cap");
        gaps.Skip(3).Max().Should().BeGreaterThan(400, "the pause doubled up to the cap");
        polls.Count.Should().BeLessThan(20, "3 s at a fixed 50 ms would be 60 polls");
    }

    [Fact]
    public async Task Two_run_all_calls_at_the_same_moment_over_a_queue_of_three_never_start_four_arms()
    {
        await using var kit = await RunAllKit.MakeAsync("C1", "CR1", "C2", "CR2");
        kit.Script(new JsonObject { ["holdAfterLine"] = 1 });

        var both = await Task.WhenAll(kit.RunAsync("C1", "CR1", "C2"), kit.RunAsync("CR2"));
        await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        both.Select(run => run.Code).Order().Should().Equal([0, 4], "one call wins whichever starts first; the other is refused — by the lock while the first counts, by the count (3 + 1 > 3) once the first has returned");
        kit.AliveCount().Should().BeLessThanOrEqualTo(3, string.Join(" | ", both.Select(run => run.Error + run.Output)));
    }

    [Fact]
    public async Task A_run_all_that_finds_another_run_all_holding_the_root_lock_is_refused_naming_it_and_waits_for_nothing()
    {
        await using var kit = await RunAllKit.MakeAsync("C1");
        Directory.CreateDirectory(kit.Layout.Pids);
        var gate = new FileGateLock(TimeProvider.System, TimeSpan.FromMilliseconds(50));
        using var held = gate.TryAcquire(kit.Layout.RunAllLock);
        held.Should().NotBeNull();
        var launcher = new RecordingLauncher();
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var run = await kit.InProcessAsync(RunAllKit.Seams(launcher, new Dictionary<string, string>()), "C1");

        run.Code.Should().Be(4, run.Error + run.Output);
        run.Error.Should().Contain(kit.Layout.RunAllLock).And.Contain("another run-all holds");
        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5), "the lock is never waited for");
        launcher.Starts.Should().BeEmpty();
    }

    public static TheoryData<string, string, bool, int, string, int> Queues() => new()
    {
        { "C1 CR1 C2 CR2", "", false, 4, "4 arms would be alive at once", 0 },
        { "C1 CR1", "CHH1", true, 4, "CHH1 was sized for a gate queue of 2", 0 },
        { "C1", "CHH1", true, 0, "", 1 },
        { "C1 CR1 C2", "", false, 0, "", 3 },
    };

    [Theory]
    [MemberData(nameof(Queues))]
    public async Task Run_all_refuses_more_live_arms_than_the_smallest_queue_any_of_them_was_sized_from(string given, string alive, bool shrinkPart7, int code, string sentence, int starts)
    {
        await using var kit = await RunAllKit.MakeAsync(MadeArms);
        if (shrinkPart7)
        {
            kit.Root.Edit(top => top["runs"]!["part7"]!["rounds"] = new JsonArray(new JsonArray("CHH1", "CHH2")));
        }

        var launcher = new RecordingLauncher();
        var liveness = alive.Length == 0 ? new Dictionary<string, string>() : alive.Split(' ').ToDictionary(arm => arm, _ => "pid 4242");

        var run = await kit.InProcessAsync(RunAllKit.Seams(launcher, liveness), given.Split(' '));

        run.Code.Should().Be(code, run.Error + run.Output);
        launcher.Starts.Should().HaveCount(starts, run.Error + run.Output);
        if (sentence.Length > 0)
        {
            run.Error.Should().Contain(sentence).And.Contain("start at most");
        }
    }

    [Fact]
    public async Task Run_all_prints_its_resolved_executables_and_the_isolation_line_before_it_starts_anything()
    {
        await using var kit = await RunAllKit.MakeAsync("C1");
        var output = new StringWriter();
        var printedAtStart = string.Empty;
        var launcher = new RecordingLauncher(_ => printedAtStart = output.ToString());

        var run = await kit.InProcessAsync(RunAllKit.Seams(launcher, new Dictionary<string, string>()), output, new StringWriter(), "C1");

        run.Code.Should().Be(0, run.Error + run.Output);
        var module = Path.GetFullPath(ExperimentRoot.ConsoleBinary);
        launcher.Starts.Single().Executable.Should().Be(module, "the launcher was handed exactly the module's own real path — the one printed; not the test host's, not a bare name");
        var expected = new[] { "resolved executables:", $"  setsid → {kit.Setsid}", $"  model-arms → {module}", IsolationLine.Of(IsolationFiles.State(kit.Layout)) };
        expected[^1].Should().Contain("no isolation snapshot", "the root has no snapshot, and the line says so");
        var at = expected.Select(line => printedAtStart.IndexOf(line, StringComparison.Ordinal)).ToList();
        at.Should().OnlyContain(index => index >= 0, "each line stood before the first start: " + printedAtStart);
        at.Should().BeInAscendingOrder();
        run.Output.IndexOf("C1: started", StringComparison.Ordinal).Should().BeGreaterThan(at[^1]);
        launcher.Starts.Single().Arguments.Should().Equal("run-arm", "C1", "--experiment", kit.Root.Root);
        launcher.Starts.Single().Log.Should().Be(kit.Layout.PidLog("C1"));
        launcher.Starts.Single().WorkingDirectory.Should().Be(kit.Root.Root);
        File.Exists(kit.Layout.LockedModes).Should().BeFalse("run-all reads the snapshot and changes nothing");
        File.Exists(kit.Layout.LockedModesApplied).Should().BeFalse();
    }

    [Theory]
    [InlineData("X1", "belongs to scheme X")]
    [InlineData("C9", "C9")]
    [InlineData("C1 C1", "C1 is named twice")]
    public async Task An_unknown_an_unmade_or_a_twice_named_arm_is_refused_with_exit_4_and_nothing_started(string given, string sentence)
    {
        await using var kit = await RunAllKit.MakeAsync("C1");
        kit.Root.Write("chain/made-X1", "made\n");
        var launcher = new RecordingLauncher();

        var run = await kit.InProcessAsync(RunAllKit.Seams(launcher, new Dictionary<string, string>()), given.Split(' '));

        run.Code.Should().Be(4, run.Error + run.Output);
        run.Error.Should().Contain(sentence);
        launcher.Starts.Should().BeEmpty();
    }

    [Fact]
    public async Task An_srs_arm_is_started_like_any_other_since_run_arm_walks_the_task_pipeline()
    {
        await using var kit = await RunAllKit.MakeAsync("SRS1");
        var launcher = new RecordingLauncher();

        var run = await kit.InProcessAsync(RunAllKit.Seams(launcher, new Dictionary<string, string>()), "SRS1");

        run.Code.Should().Be(0, run.Error + run.Output);
        launcher.Starts.Single().Arguments.Should().Equal("run-arm", "SRS1", "--experiment", kit.Root.Root);
    }

    [Fact]
    public async Task A_pids_folder_that_is_a_link_is_refused_naming_it_before_anything_starts()
    {
        await using var kit = await RunAllKit.MakeAsync("C1");
        var elsewhere = kit.Root.Scratch("elsewhere");
        Directory.CreateSymbolicLink(kit.Layout.Pids, elsewhere);
        var launcher = new RecordingLauncher();

        var run = await kit.InProcessAsync(RunAllKit.Seams(launcher, new Dictionary<string, string>()), "C1");

        run.Code.Should().Be(4, run.Error + run.Output);
        run.Error.Should().Contain($"model-arms run-all: {kit.Layout.Pids} is a symbolic link");
        launcher.Starts.Should().BeEmpty();
        Directory.EnumerateFileSystemEntries(elsewhere).Should().BeEmpty("nothing was made through the link");
    }

    [Fact]
    public async Task A_host_without_measured_constants_is_exit_3_naming_the_manual_road()
    {
        await using var kit = await RunAllKit.MakeAsync("C1");
        var launcher = new RecordingLauncher();

        var run = await kit.InProcessAsync(RunAllKit.Seams(launcher, new Dictionary<string, string>(), DetachedRoad.Unmeasured), "C1");

        run.Code.Should().Be(3, run.Error + run.Output);
        run.Error.Should().Contain("setsid").And.Contain("run-arm C1").And.Contain("--experiment " + kit.Root.Root).And.Contain("pids/C1.log");
        launcher.Starts.Should().BeEmpty();
    }
}
