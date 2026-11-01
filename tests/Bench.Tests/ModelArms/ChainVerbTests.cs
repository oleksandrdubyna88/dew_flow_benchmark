using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bench.Cli;
using Bench.Tests.Probes.Driver;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Chain;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;
using ModelArms.Verbs;
using Xunit;
using ModelArmsApp = ModelArms;

namespace Bench.Tests.ModelArms;

/// <summary><c>chain &lt;run&gt;</c> (E5.S4, parent §4.7, D11, D27) in process over <see cref="ChainKit"/>: the stages in their order with the markers written after each verb returns, the run's own lock, the startup poll, the waits that sleep what remains, the bounded exit wait before every start, and — the point of the story — the failure path, which leaves the isolation applied, stops no runner and prints the recovery. The files are real and the verbs
/// the chain calls are recorded (except where a row says the real <c>lock</c>, <c>unlock</c>, <c>make</c> or <c>run-all</c> is called).
/// <para>
/// What this does NOT prove: that a started runner is detached and frees the run's lock when the chain dies (<c>ChainProcessTests</c>); the facts' reading against a REAL runner past the one non-runner holder; a Windows host (the host seam says Windows, the process is Linux); the SRS arms (E4.S3 — <c>run-all</c> refuses their scheme).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class ChainVerbTests
{
    private static readonly TimeSpan Short = TimeSpan.FromSeconds(30);

    private static readonly string[] Part6Arms = ["C1", "CR1", "SRS1", "C2", "CR2", "SRS2", "C3", "CR3", "SRS3"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("died", 5)]
    [InlineData("stop", -1)]
    [InlineData("refusal", 4)]
    [InlineData("exception", -2)]
    [InlineData("ceiling", 5)]
    public async Task A_chain_that_fails_leaves_the_isolation_applied_stops_no_runner_and_prints_the_recovery(string path, int code)
    {
        await using var kit = await ChainKit.CreateAsync();
        Directory.CreateDirectory(kit.Layout.Pids);
        await using var holder = ModelArmsBinary.StartHolder(kit.Root, kit.Layout.RunnerLock("CR1"), FakeCli.Executable);
        await holder.LineAsync("held ", Short);
        kit.RealLiveness = true;
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        Prepare(kit, path, stopping);

        ChainRun? run = null;
        Exception? thrown = null;
        try
        {
            run = await kit.RunAsync(stopping: stopping.Token);
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        switch (code)
        {
            case -1: thrown.Should().BeAssignableTo<OperationCanceledException>("the operator's stop travels to Program.Main, which maps it to exit 5"); break;
            case -2: thrown.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Contain("boom"); break;
            default: (thrown, run!.Code).Should().Be((null, code), kit.Error); break;
        }

        kit.Record.Should().NotContain(entry => entry.StartsWith("unlock", StringComparison.Ordinal), "the chain unlocks only through its Unlock stage — never on a failure path");
        kit.Record[^1].Should().Be("awake-dispose", "the scope ends on every path");
        kit.Error.TrimEnd().Should().Contain("isolation: ").And.EndWith("to abandon the run, model-arms unlock once no arm is alive", "the recovery is the last thing said");
        kit.Error.Should().Contain("no runner was stopped");
        PidsOf(holder).Should().OnlyContain(pid => Directory.Exists($"/proc/{pid}"), "the non-runner holding pids/CR1.lock is alive afterwards: the chain signals no process");
        Detail(path, kit);
    }

    private static void Prepare(ChainKit kit, string path, CancellationTokenSource stopping)
    {
        switch (path)
        {
            case "died": kit.WritePid("C1"); break;
            case "stop": kit.AfterTick = _ => stopping.Cancel(); break;
            case "refusal": kit.LockResult = () => new LockOutcome.Stopped(ExitCodes.Configuration, "the refusal of the row"); break;
            case "exception": PrepareChecking(kit); break;
            default:
                kit.FinishArms = false;
                kit.AfterTick = tick => kit.Clock.Advance(tick == 1 ? ChainWaitCeiling.OfRound(kit.Root.ExperimentOf(), ["C1", "CR1", "SRS1"], 12, GateStep.MaxLockCeiling) : TimeSpan.Zero);
                break;
        }
    }

    /// <summary>Every arm made, the run locked and unlocked by an earlier chain: this one goes straight to the checks, and the first of them throws.</summary>
    private static void PrepareChecking(ChainKit kit)
    {
        Part6Arms.ToList().ForEach(kit.MarkMade);
        kit.MarkStage("part6", RunStage.Locked);
        kit.MarkStage("part6", RunStage.Unlocked);
        kit.WriteSnapshot("part6", kit.DigestOf("part6"));
        kit.CheckResult = _ => throw new InvalidOperationException("boom");
    }

    private static void Detail(string path, ChainKit kit)
    {
        switch (path)
        {
            case "died": kit.Error.Should().Contain("C1 stopped without finishing").And.Contain("model-arms run-all C1"); break;
            case "refusal": kit.Error.Should().Contain("the refusal of the row"); kit.Marked("part6", RunStage.Locked).Should().BeFalse(); break;
            case "ceiling": kit.Error.Should().Contain("C1 is still running").And.Contain("pids/C1.log").And.Contain("no runner was stopped"); break;
            default: break;
        }

        File.Exists(kit.Layout.LockedModesApplied).Should().Be(path != "refusal", "the snapshot a lock left stands — the isolation is left applied; a lock that was refused left none");
    }

    private static IEnumerable<int> PidsOf(ModelArmsBinary.Child holder) => Regex.Matches(holder.Printed(), @"\d+").Select(match => int.Parse(match.Value, CultureInfo.InvariantCulture));

    [Fact]
    public async Task An_unlock_refused_at_the_unlock_stage_ends_the_chain_with_exit_5_and_writes_no_unlocked_marker()
    {
        await using var kit = await ChainKit.CreateAsync();
        Part6Arms.ToList().ForEach(kit.MarkMade);
        Part6Arms.ToList().ForEach(kit.FinishNow);
        kit.UnlockResult = () => new UnlockOutcome.RefusedAlive("C2", "refused — C2 is alive");

        var run = await kit.RunAsync();

        run.Code.Should().Be(5, run.Error);
        run.Error.Should().Contain("unlock refused — C2 is alive");
        kit.Marked("part6", RunStage.Unlocked).Should().BeFalse("the marker is written only after the verb returned 0");
        kit.Marked("part6", RunStage.Locked).Should().BeTrue();
        kit.Record.Should().NotContain(entry => entry.StartsWith("check", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_root_level_chain_markers_are_gone_and_every_stage_marker_is_a_run_marker()
    {
        foreach (var gone in new[] { "Locked", "Unlocked", "ArmsDone" })
        {
            typeof(RootPaths).GetMember(gone).Should().BeEmpty($"RootPaths.{gone} was E1's spelling before D27; the stage markers are RunMarker(run, stage)");
        }

        await using var kit = await ChainKit.CreateAsync();

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error + run.Output);
        var chain = kit.Layout.Chain;
        var found = Directory.EnumerateFiles(chain, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(chain, path).Replace('\\', '/')).Order(StringComparer.Ordinal).ToList();
        string[] expected =
        [
            "executables.txt", "part6/.chain.lock", "part6/arms-done", "part6/locked", "part6/unlocked",
            .. Part6Arms.Select(arm => "made-" + arm), .. Part6Arms.Select(arm => "objective-" + arm),
        ];
        found.Should().Equal(expected.Order(StringComparer.Ordinal), "every file the pass left under chain/ is a run marker, the run's lock, a per-arm marker or executables.txt — the holder note is gone after the release");
    }

    [Fact]
    public async Task The_awake_scope_spans_the_whole_chain()
    {
        await using var kit = await ChainKit.CreateAsync();

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error + run.Output);
        kit.Stages.Should().Equal(
            [
                "awake-start",
                "make C1,CR1,SRS1,C2,CR2,SRS2,C3,CR3,SRS3",
                "lock part6",
                "start C1,CR1,SRS1",
                "start C2,CR2,SRS2",
                "start C3,CR3,SRS3",
                "unlock",
                .. Part6Arms.Select(arm => "check " + arm),
                "awake-dispose",
            ]);
        kit.AwakeAsked.Should().Equal(HostKind.OtherPosix);
    }

    [Fact]
    public async Task A_chain_entered_over_an_applying_snapshot_calls_lock_which_recovers_it()
    {
        await using var kit = await ChainKit.CreateAsync();
        Part6Arms.ToList().ForEach(kit.MarkMade);
        File.WriteAllText(kit.Layout.LockedModes, """{"run":"part6","digest":"old","since":"2026-10-31T10:00:00Z","modes":[]}""");
        LockSnapshotsOf(kit).Should().BeOfType<LockState.Applying>();

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error + run.Output);
        kit.Stages[1].Should().Be("lock part6", "the first stage after the makes — here none — is the lock that recovers the interrupted one, not a refusal");
        kit.Marked("part6", RunStage.Locked).Should().BeTrue();
    }

    private static LockState LockSnapshotsOf(ChainKit kit) => IsolationFiles.State(kit.Layout);

    [Fact]
    public async Task The_chain_writes_locked_and_unlocked_after_the_verbs_return_and_the_verbs_write_no_marker()
    {
        await using (var kit = await ChainKit.CreateAsync())
        {
            var run = await kit.RunAsync();

            run.Code.Should().Be(0, run.Error + run.Output);
            kit.Marked("part6", RunStage.Locked).Should().BeTrue("written after `lock` returned (the recording Lock asserted the marker absent when it ran)");
            kit.Marked("part6", RunStage.Unlocked).Should().BeTrue("written after `unlock` returned");
        }

        // The REAL verbs over E5.S1's kit, its isolation block kept and every entry a folder of the test's own temp (E5 §8 items 8 and 50, M-7).
        using var isolation = new IsolationKit();
        var original = isolation.Modes();
        var locked = await RealAsync(isolation, async (input, kit) =>
        {
            (await LockVerb.LockAsync(input, "part6", Ct, kit.Seams())).Should().BeOfType<LockOutcome.Applied>();
            return 0;
        });
        locked.Should().Be(0);
        isolation.Modes().Should().NotEqual(original, "the real lock closed the entries");
        File.Exists(isolation.Layout.LockedModes).Should().BeTrue();
        File.Exists(isolation.Layout.LockedModesApplied).Should().BeTrue();
        AbsentMarkers(isolation);

        var unlocked = await RealAsync(isolation, async (input, kit) =>
        {
            (await UnlockVerb.UnlockAsync(input, force: false, Ct, kit.Seams())).Should().BeOfType<UnlockOutcome.Restored>().Which.Count.Should().Be(8);
            return 0;
        });
        unlocked.Should().Be(0);
        isolation.Modes().Should().Equal(original, "unlock restored the modes the lock found");
        AbsentMarkers(isolation);
    }

    private static void AbsentMarkers(IsolationKit isolation)
    {
        foreach (var stage in new[] { RunStage.Locked, RunStage.Unlocked, RunStage.ArmsDone })
        {
            File.Exists(isolation.Layout.RunMarker("part6", stage)).Should().BeFalse($"neither verb writes chain/part6/{stage}: the chain is the markers' one writer");
        }
    }

    private static async Task<int> RealAsync(IsolationKit isolation, Func<ModelArmsApp.ExperimentInput, IsolationKit, Task<int>> body)
    {
        var input = new ModelArmsApp.VerbInput("lock", ["part6"], CommandLine.Parse(["lock", "--experiment", isolation.Root.Root]), new StringWriter(), new StringWriter(), isolation.Root.Environment, ModelArmsApp.ModuleFiles.Beside(AppContext.BaseDirectory), Ct);
        return await ModelArmsApp.ExperimentGate.RunAsync(input, admitted => body(admitted, isolation), ModelArmsApp.GateSeams.Real);
    }

    [Theory]
    [InlineData("wsl without a block", HostKind.Wsl, false)]
    [InlineData("windows", HostKind.Windows, true)]
    [InlineData("a host whose road has no folder isolation, the block kept", HostKind.Wsl, true)]
    public async Task On_windows_or_without_isolation_the_chain_runs_under_no_isolation_and_the_hosts_awake_adapter(string row, HostKind host, bool noAdapter)
    {
        await using var kit = await ChainKit.CreateAsync();
        kit.Host = host;
        if (noAdapter)
        {
            kit.Road = PlatformKit.WindowsRoad;
            kit.LockResult = () => new LockOutcome.NoIsolation("isolation by file modes is POSIX only — nothing to lock on Windows");
        }
        else
        {
            kit.Root.Edit(top => top.AsObject().Remove("isolation"));
        }

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, $"{row}: {run.Error}{run.Output}");
        kit.AwakeAsked.Should().Equal(host);
        kit.Marked("part6", RunStage.Locked).Should().BeTrue("`locked` is written over NoIsolation and stands over no snapshot");
        File.Exists(kit.Layout.LockedModes).Should().BeFalse();
        kit.Marked("part6", RunStage.ArmsDone).Should().BeTrue();
    }

    [Fact]
    public async Task A_runner_that_finishes_between_the_reads_of_its_arm_is_a_finished_arm_never_a_died_one()
    {
        await using var kit = await ChainKit.CreateAsync();
        Part6Arms.ToList().ForEach(kit.MarkMade);
        kit.MarkStage("part6", RunStage.Locked);
        kit.WriteSnapshot("part6", kit.DigestOf("part6"));
        kit.WritePid("C1");
        var flipped = false;
        var inner = kit.Seams();
        var seams = inner with
        {
            Liveness = (root, arm) =>
            {
                if (arm != "C1" || flipped)
                {
                    return inner.Liveness(root, arm);
                }

                flipped = true;
                kit.FinishNow("C1");
                return new RunnerState.Gone();
            },
        };

        var run = await kit.RunAsync(seams: seams);

        flipped.Should().BeTrue();
        run.Error.Should().NotContain("stopped without finishing", "the runner appended arm-done and exited between the chain's reads: the lock is read BEFORE the timing, so the finished line is seen");
        run.Code.Should().Be(0, run.Error + run.Output);
        kit.Record.Should().NotContain("start C1", "a finished arm is not started again").And.Contain("start CR1,SRS1");
    }

    [Theory]
    [InlineData("make and lock")]
    [InlineData("checks")]
    public async Task A_plan_that_cannot_be_made_stops_neither_the_makes_nor_the_lock_nor_the_checks(string stage)
    {
        await using var kit = await ChainKit.CreateAsync();
        var experiment = kit.Root.ExperimentOf();
        var arms = experiment.Runs.SelectMany(run => run.Rounds.SelectMany(round => round)).Where(arm => arm != "C1").Distinct().ToList();
        var token = (byte)0x70;
        kit.Root.Write("arms.map.json", ArmMap.Extend(ArmMap.Empty, arms, () => [0, 0, 0, 0, 0, 0, 0, ++token]).ToJson());
        ArmMap.Read(File.ReadAllText(kit.Layout.ArmMapFile)).Ok().TokenOf("C1").Should().BeOfType<Bench.Domain.Outcome<string>.Fail>("part7 closes C1 and the map has no C1: its plan cannot be made");
        if (stage == "checks")
        {
            new[] { "CHH1", "CHH2", "CHH3" }.ToList().ForEach(kit.MarkMade);
            kit.MarkStage("part7", RunStage.Locked);
            kit.MarkStage("part7", RunStage.Unlocked);
        }
        else
        {
            kit.LockResult = () => new LockOutcome.Stopped(ExitCodes.Configuration, "the lock of the row");
        }

        var run = await kit.RunAsync("part7");

        if (stage == "checks")
        {
            run.Code.Should().Be(0, run.Error + run.Output);
            kit.Stages.Should().Contain(["check CHH1", "check CHH2", "check CHH3"]);
            return;
        }

        run.Code.Should().Be(4);
        run.Error.Should().Contain("the lock of the row", "the chain reached its own make and its own lock before anything needed the plan: " + run.Error);
        kit.Stages.Should().Equal("awake-start", "make CHH1,CHH2,CHH3", "lock part7", "awake-dispose");
    }

    [Fact]
    public async Task A_host_where_the_detached_launch_is_not_measured_is_refused_before_make_and_lock()
    {
        await using var kit = await ChainKit.CreateAsync();
        kit.Detached = DetachedRoad.Unmeasured;

        var run = await kit.RunAsync();

        run.Code.Should().Be(3, run.Error);
        run.Error.Should().Contain("starting a detached arm is").And.Contain("by hand").And.Contain("run-arm C1");
        kit.Record.Should().BeEmpty("nothing was made, locked or kept awake on a host the chain cannot launch on");
        File.Exists(kit.Layout.Executables).Should().BeFalse();
        kit.Marked("part6", RunStage.Locked).Should().BeFalse();
    }

    [Fact]
    public async Task Resolved_executables_are_printed_and_written_before_the_first_launch()
    {
        await using var kit = await ChainKit.CreateAsync();
        var written = string.Empty;
        kit.AtFirstStage = () => written = File.ReadAllText(kit.Layout.Executables);

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error + run.Output);
        var lines = written.TrimEnd().Split('\n');
        lines[0].Should().Be("resolved executables:");
        lines.Skip(1).Should().OnlyContain(line => Regex.IsMatch(line, @"^  \S.* → /\S+$"), "one `name → real path` line per resolved executable");
        lines.Skip(1).Select(line => line.Split(" → ")[0].Trim()).Should().Contain(["git", "claude", "dotnet", "node", "npm", "setsid", "model-arms"]);
        var firstStage = run.Output.IndexOf("part6: make ", StringComparison.Ordinal);
        firstStage.Should().BeGreaterThan(0);
        foreach (var line in lines)
        {
            run.Output.IndexOf(line, StringComparison.Ordinal).Should().BeInRange(0, firstStage, $"`{line}` stood in the output before the first stage line");
        }

        File.ReadAllText(kit.Layout.Executables).Should().Be(written, "the file is replaced whole before the first stage and not touched again");
    }

    [Fact]
    public async Task A_run_the_experiment_does_not_name_is_refused_with_exit_4()
    {
        await using var kit = await ChainKit.CreateAsync();

        var run = await kit.RunAsync("part9");

        run.Code.Should().Be(4);
        run.Error.Should().Contain("there is no run part9").And.Contain("part6, part7");
        kit.Record.Should().BeEmpty();
        Directory.Exists(kit.Layout.Chain).Should().BeFalse("the run is checked before its lock's folder is made");
        Directory.Exists(kit.Layout.Pids).Should().BeFalse();
    }

    [Fact]
    public async Task The_chain_makes_only_its_runs_arms()
    {
        await using (var kit = await ChainKit.CreateAsync())
        {
            var run = await kit.RunAsync("part7");

            run.Code.Should().Be(0, run.Error + run.Output);
            kit.Record.Should().Contain("make CHH1,CHH2,CHH3", "the chain asks for its own run's arms, not part6's nine");
            File.Exists(kit.Layout.Made("C1")).Should().BeFalse();
        }

        // The scoped overload itself, over a made-for-real base: it makes CHH1 and no arm it was not asked for.
        MakeKit.Posix();
        using var make = await MakeKit.CreateAsync("CHH1", "C1");
        var made = new StringWriter();
        var input = new ModelArmsApp.VerbInput("make", [], CommandLine.Parse(["make", "--experiment", make.Root.Root]), made, made, make.Root.Environment, ModelArmsApp.ModuleFiles.Beside(AppContext.BaseDirectory), Ct);

        var code = await ModelArmsApp.ExperimentGate.RunAsync(input, admitted => MakeVerb.RunAsync(admitted, ["CHH1"]), ModelArmsApp.GateSeams.Real);

        code.Should().Be(0, made.ToString());
        File.Exists(make.Layout.Made("CHH1")).Should().BeTrue();
        File.Exists(make.Layout.Made("C1")).Should().BeFalse("C1 was not in the arms handed to make");
        Directory.Exists(make.Paths("CHH1").CloneFolder).Should().BeTrue();
        File.Exists(make.Layout.ArmMapFile).Should().BeTrue();
    }

    [Fact]
    public async Task A_second_chain_of_the_same_run_exits_4_naming_the_holder_and_starts_nothing()
    {
        await using var kit = await ChainKit.CreateAsync();
        var lockPath = kit.Layout.ChainLock("part6");
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        await using var holder = ModelArmsBinary.StartHolder(kit.Root, lockPath, FakeCli.Executable);
        await holder.LineAsync("held ", Short);
        var holderPid = PidsOf(holder).First();
        var clock = Stopwatch.StartNew();

        var run = await kit.RunAsync();

        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5), "the lock is never waited for");
        run.Code.Should().Be(4, run.Error);
        run.Error.Should().Contain("chain/part6/.chain.lock").And.Contain("nothing was started");
        Regex.IsMatch(run.Error, $@"by pid {holderPid} since|\(holder unknown\)").Should().BeTrue("the holder is named, or `(holder unknown)` — a holder may not have written its note yet: " + run.Error);
        kit.Record.Should().BeEmpty();
        File.Exists(kit.Layout.Executables).Should().BeFalse("the lock is taken before the executables are written");
        Directory.Exists(kit.Layout.Pids).Should().BeFalse("and before pids/ is made");

        var beside = await kit.RunAsync("part7");
        beside.Code.Should().Be(0, "a chain of another run is not refused by this run's lock: " + beside.Error);
        beside.Error.Should().NotContain("holds chain/part6/.chain.lock");

        await holder.KillAsync();
        File.Exists(lockPath).Should().BeTrue("the lock file is kept: a deleted lock file lets two holders hold two files");
    }

    [Theory]
    [InlineData(3)]
    [InlineData(ChainKit.Never)]
    public async Task A_started_arm_is_polled_until_its_runner_holds_its_lock_and_is_never_read_as_died(int startupTicks)
    {
        await using var kit = await ChainKit.CreateAsync();
        kit.StartupTicks = startupTicks;

        var run = await kit.RunAsync();

        if (startupTicks != ChainKit.Never)
        {
            run.Code.Should().Be(0, run.Error + run.Output);
            kit.Record.SkipWhile(entry => entry != "start C1,CR1,SRS1").Skip(1).Take(4).Should().Equal("tick 00:00:01", "tick 00:00:01", "tick 00:00:01", "tick 00:01:00");
            return;
        }

        run.Code.Should().Be(5, run.Error);
        kit.Spans.Should().HaveCount(60).And.OnlyContain(span => span == TimeSpan.FromSeconds(1));
        (kit.Clock.GetUtcNow() - IsolationKit.Instant).Should().Be(TimeSpan.FromSeconds(60), "exit 5 at exactly 60 s by the clock");
        foreach (var arm in new[] { "C1", "CR1", "SRS1" })
        {
            run.Error.Should().Contain($"{arm} was started 60 s ago").And.Contain($"pids/{arm}.log").And.Contain($"model-arms run-all {arm}");
        }

        run.Error.Should().NotContain("stopped without finishing", "an arm whose runner has not yet taken its lock is not dead");
    }

    [Fact]
    public async Task The_next_round_is_started_only_after_the_last_rounds_runners_have_exited()
    {
        await using var kit = await ChainKit.CreateAsync();
        kit.Root.Edit(top => top["runs"] = JsonNode.Parse("""{"part6":{"rounds":[["C1","CR1","C2"],["CR2","C3","CR3"]]}}"""));
        kit.ExitTicks = 2;
        kit.RealRunAll = new RecordingLauncher(arm =>
        {
            kit.Record.Add($"launched {arm}");
            kit.Launched(arm);
        });

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, "run-all's queue refusal (4) would have ended the chain at the second round: " + run.Error + run.Output);
        run.Error.Should().NotContain("would be alive at once");
        run.Output.Should().NotContain("would be alive at once");
        var firstOfRound1 = kit.Record.IndexOf("launched C2");
        var firstOfRound2 = kit.Record.IndexOf("launched CR2");
        firstOfRound1.Should().BeGreaterThan(0);
        kit.Record.Skip(firstOfRound1).Take(firstOfRound2 - firstOfRound1).Count(entry => entry.StartsWith("tick ", StringComparison.Ordinal)).Should().BeGreaterThanOrEqualTo(3, "round 2 was handed to run-all only after round 1's done runners had left (they stayed alive two ticks after arm-done)");
        kit.RealRunAll.Starts.Select(start => start.Arguments[1]).Should().Equal("C1", "CR1", "C2", "CR2", "C3", "CR3");
    }

    [Fact]
    public async Task The_bounded_wait_sleeps_what_remains_and_expires_at_exactly_the_grace()
    {
        // (a) the wait for a done runner to exit: a grace of 90 s is checked at 0, 60 and 90 s.
        await using (var kit = await ChainKit.CreateAsync())
        {
            kit.Root.Edit(top => top["limits"]!["killGraceSeconds"] = 90);
            Part6Arms.ToList().ForEach(kit.MarkMade);
            Part6Arms.ToList().ForEach(kit.FinishNow);
            kit.MarkStage("part6", RunStage.Locked);
            kit.WriteSnapshot("part6", kit.DigestOf("part6"));
            kit.MarkAlive("C1");
            kit.Stick("C1");

            var run = await kit.RunAsync();

            run.Code.Should().Be(5, run.Error);
            kit.Spans.Should().Equal(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(30));
            (kit.Clock.GetUtcNow() - IsolationKit.Instant).Should().Be(TimeSpan.FromSeconds(90), "expired at the grace, not at the next whole minute");
            run.Error.Should().Contain("C1 wrote arm-done but the runner has not exited within limits.killGraceSeconds (90 s)");
        }

        // (b) the startup poll ends at +60 s.
        await using (var kit = await ChainKit.CreateAsync())
        {
            kit.StartupTicks = ChainKit.Never;

            (await kit.RunAsync()).Code.Should().Be(5);

            kit.Spans.Sum(span => span.TotalSeconds).Should().Be(60);
        }

        // (c) the round's wait: a ceiling that is not a whole number of minutes ends at exactly the ceiling, the last tick what remained.
        await using (var kit = await ChainKit.CreateAsync())
        {
            kit.Root.Edit(top =>
            {
                foreach (var name in new[] { "waitSeconds", "tries", "gateWaitSeconds", "gateTries", "minimumRemainingSeconds", "killGraceSeconds" })
                {
                    top["limits"]![name] = 1;
                }

                top["steps"]!.AsObject().ToList().ForEach(step => step.Value!["minutes"] = 1);
            });
            kit.FinishArms = false;
            var ceiling = ChainWaitCeiling.OfRound(kit.Root.ExperimentOf(), ["C1", "CR1", "SRS1"], 12, GateStep.MaxLockCeiling);
            (ceiling.TotalSeconds % 60).Should().NotBe(0, "the row is only a proof when the ceiling is not a multiple of the period");

            var run = await kit.RunAsync();

            run.Code.Should().Be(5, run.Error);
            kit.Spans.Sum(span => span.TotalSeconds).Should().Be(ceiling.TotalSeconds);
            kit.Spans.Should().OnlyContain(span => span <= TimeSpan.FromSeconds(60));
            kit.Spans[^1].Should().Be(TimeSpan.FromSeconds(ceiling.TotalSeconds % 60), "the last tick is what remained");
        }
    }

    [Fact]
    public async Task The_chain_makes_pids_by_name_before_its_first_stage_and_refuses_a_link_there()
    {
        await using (var kit = await ChainKit.CreateAsync())
        {
            Directory.Exists(kit.Layout.Pids).Should().BeFalse();
            var mode = UnixFileMode.None;
            kit.AtFirstStage = () =>
            {
                Directory.Exists(kit.Layout.Pids).Should().BeTrue("pids/ is made by name before the first stage");
                mode = File.GetUnixFileMode(kit.Layout.Pids);
            };

            var run = await kit.RunAsync();

            run.Code.Should().Be(0, run.Error + run.Output);
            mode.Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        await using var linked = await ChainKit.CreateAsync();
        var elsewhere = linked.Root.Scratch("elsewhere");
        File.CreateSymbolicLink(linked.Layout.Pids, elsewhere);

        var refused = await linked.RunAsync();

        refused.Code.Should().Be(4, refused.Error);
        refused.Error.Should().Contain(linked.Layout.Pids).And.Contain("symbolic link");
        linked.Record.Should().BeEmpty();
        Directory.EnumerateFileSystemEntries(elsewhere).Should().BeEmpty("nothing was written through the link");
    }
}
