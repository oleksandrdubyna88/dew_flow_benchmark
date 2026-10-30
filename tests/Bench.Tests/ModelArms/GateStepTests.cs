using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Steps;
using Xunit;
using static Bench.Tests.ModelArms.GateStepKit;

namespace Bench.Tests.ModelArms;

/// <summary>Model arms E2.S7 — the gate step: the lock, the step, <c>gate.json</c> judged on EVERY entry, a blocked round decided and written down BEFORE the marker, the marker and the deleted <c>.done</c> in the order that cannot lose the round, and the
/// diff once. A gate killed at a point is a probe that throws there; the second gate reads the same folders through a new runner. The lock is a recording double here (E2.S6 proves the real one); the one test that needs the real lock holds it from a second process.
/// <para>
/// What this does NOT prove: a real gate model that writes <c>gate.json</c> (the test writes it — the fake cannot); that the real lock and the real wait compose across a 30-minute round (the pilot); a gate killed at a point no <see cref="GatePoint"/> names.
/// </para></summary>
public sealed class GateStepTests
{
    [Fact]
    public async Task A_blocked_status_removes_done_waits_and_resumes_up_to_the_tries()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        bench.WriteStatus("blocked", "the review is not answered");
        var rig = new GateRig();
        rig.WhenWaiting(bench, round =>
        {
            File.Exists(bench.Paths.Done(GateName)).Should().BeFalse($"the .done is removed before wait {round}");
            ContinueMarkers.Read(File.ReadAllText(bench.Paths.Continue(GateName))).Ok().After.Should().Be(bench.Fake.Calls().Count, $"the marker names the attempt that finished before wait {round}");
            bench.GateWaits().Should().HaveCount(round, $"wait {round} was written down before it was waited");
            if (round == 2)
            {
                bench.WriteStatus("proceed");
            }
        });

        var outcome = await rig.Gate(bench).RunAsync(bench.GateContext(), Ct);

        outcome.IsDone();
        bench.Fake.Calls().Should().HaveCount(3);
        bench.OneSession();
        bench.Waits.Recorded.Should().Equal(GateWait, GateWait);
        File.Exists(bench.Paths.Done(GateName)).Should().BeTrue();
        File.Exists(bench.Paths.Continue(GateName)).Should().BeFalse("the finished step removes its marker");
        bench.GateWaits().Select(wait => (wait.After, wait.Wait, wait.Why)).Should().Equal((1, 1, "the review is not answered"), (2, 2, "the review is not answered"));
        bench.LogLines.Last().Should().EndWith("gate status proceed");
    }

    [Theory]
    [InlineData(GatePoint.StatusRead, 1, 2, 1, 1, false)]
    [InlineData(GatePoint.StatusRead, 2, 3, 1, 2, false)]
    [InlineData(GatePoint.WaitRecorded, 1, 2, 1, 2, false)]
    [InlineData(GatePoint.WaitRecorded, 2, 2, 0, 2, true)]
    public async Task A_gate_killed_after_done_and_before_its_round_was_recorded_or_ordered_is_judged_again_on_restart(GatePoint point, int round, int calls, int waitsInGateTwo, int linesInAll, bool skipped)
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        bench.WriteStatus("blocked", "tests are red");
        var context = bench.GateContext();

        await FluentActions.Awaiting(() => new GateRig().Gate(bench, new GateKilledAt(point, round)).RunAsync(context, Ct)).Should().ThrowAsync<IOException>().WithMessage("killed");
        File.Exists(bench.Paths.Done(GateName)).Should().BeTrue("the runner's .done stands: the kill came after it");
        bench.Attempts().Should().Be(round);
        File.Exists(bench.Paths.Continue(GateName)).Should().BeFalse("no marker was written yet");
        bench.GateWaits().Should().HaveCount(point == GatePoint.StatusRead ? round - 1 : round);

        var second = new GateRig();
        second.WhenWaiting(bench, _ => bench.WriteStatus("proceed"));
        var outcome = await second.Gate(bench).RunAsync(context, Ct);

        outcome.IsDone().Skipped.Should().Be(skipped);
        bench.Fake.Calls().Should().HaveCount(calls, "a gate that was blocked is judged again whatever the runner's .done says");
        bench.Waits.Recorded.Should().HaveCount(waitsInGateTwo, skipped ? "the spent tries are counted from the file: nothing is waited again" : "one wait, then the gate proceeds");
        bench.GateWaits().Should().HaveCount(linesInAll);
        File.Exists(bench.Paths.Continue(GateName)).Should().BeFalse();
        File.Exists(bench.Paths.Diff(GateName)).Should().BeTrue();
        bench.Attempts().Should().Be(calls);
        if (skipped)
        {
            bench.Fake.Calls().Last().After("--resume").Should().Be(bench.Fake.Calls()[0].After("--session-id"));
            bench.LogLines.Should().Contain(line => line.EndsWith("gate status blocked — after 2 waits, finished as the shell did", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task A_gate_finished_still_blocked_after_its_tries_is_not_revived_by_a_restart_unless_the_tries_are_raised()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        bench.WriteStatus("blocked", "still red");
        var rig = new GateRig();
        rig.WhenWaiting(bench, _ => { });

        (await rig.Gate(bench).RunAsync(bench.GateContext(), Ct)).IsDone();
        bench.Fake.Calls().Should().HaveCount(3);
        bench.Waits.Recorded.Should().Equal(GateWait, GateWait);
        bench.GateWaits().Should().HaveCount(2);
        bench.LogLines.Last().Should().EndWith("gate status blocked — after 2 waits, finished as the shell did");

        var again = await new GateRig().Gate(bench).RunAsync(bench.GateContext(), Ct);

        again.IsDone().Skipped.Should().BeTrue("the gate finished; a restart does not give it fresh tries");
        bench.Fake.Calls().Should().HaveCount(3);
        bench.Waits.Recorded.Should().HaveCount(2);
        bench.LogLines.Last().Should().EndWith("gate status blocked — after 2 waits, finished as the shell did");
        File.Exists(bench.Paths.Diff(GateName)).Should().BeTrue();

        bench.RereadExperiment(root => root["limits"]!["gateTries"] = 3);
        var raised = await new GateRig().Gate(bench).RunAsync(bench.GateContext(), Ct);

        raised.IsDone();
        bench.Fake.Calls().Should().HaveCount(4, "the raised tries bought one more round");
        bench.Fake.Calls()[3].After("--resume").Should().Be(bench.Fake.Calls()[0].After("--session-id"));
        bench.Waits.Recorded.Should().HaveCount(3);
        bench.GateWaits().Should().HaveCount(3);
        bench.LogLines.Last().Should().EndWith("gate status blocked — after 3 waits, finished as the shell did");
    }

    [Theory]
    [InlineData(GatePoint.ContinueWritten)]
    [InlineData(GatePoint.DoneRemoved)]
    public async Task A_runner_killed_between_the_continue_marker_and_the_resume_resumes_on_the_next_entry(GatePoint point)
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        bench.WriteStatus("blocked", "needs work");
        var context = bench.GateContext();

        await FluentActions.Awaiting(() => new GateRig().Gate(bench, new GateKilledAt(point, 1)).RunAsync(context, Ct)).Should().ThrowAsync<IOException>().WithMessage("killed");
        File.ReadAllText(bench.Paths.Continue(GateName)).Should().Be(ContinueMarkers.Write(new ContinueMarker(1, "gate blocked: needs work")));
        File.Exists(bench.Paths.Done(GateName)).Should().Be(point == GatePoint.ContinueWritten);
        bench.Fake.Calls().Should().HaveCount(1);

        bench.WriteStatus("proceed");
        var second = new GateRig();
        var outcome = await second.Gate(bench).RunAsync(context, Ct);

        outcome.IsDone();
        var calls = bench.Fake.Calls();
        calls.Should().HaveCount(2);
        calls[1].After("--resume").Should().Be(calls[0].After("--session-id"));
        bench.Attempts().Should().Be(2);
        File.Exists(bench.Paths.Continue(GateName)).Should().BeFalse();
        second.Events.Should().Equal("acquire", "launch", "release");
    }

    [Fact]
    public async Task A_finished_gate_removes_its_continue_marker_and_a_stale_one_is_ignored()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        bench.WriteStatus("blocked", "once");
        var rig = new GateRig();
        rig.WhenWaiting(bench, _ => bench.WriteStatus("proceed"));
        (await rig.Gate(bench).RunAsync(bench.GateContext(), Ct)).IsDone();
        File.Exists(bench.Paths.Continue(GateName)).Should().BeFalse();
        bench.Attempts().Should().Be(2);

        File.WriteAllText(bench.Paths.Continue(GateName), ContinueMarkers.Write(new ContinueMarker(1, "gate blocked: stale")));
        var third = await new GateRig().Gate(bench).RunAsync(bench.GateContext(), Ct);

        third.IsDone().Skipped.Should().BeTrue("the marker names attempt 1, which has started: it is stale and orders nothing");
        bench.Fake.Calls().Should().HaveCount(2);
    }

    [Fact]
    public async Task The_lock_is_released_during_the_wait()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        bench.WriteStatus("blocked", "twice");
        var rig = new GateRig();
        rig.WhenWaiting(bench, round =>
        {
            if (round == 2)
            {
                bench.WriteStatus("proceed");
            }
        });

        (await rig.Gate(bench).RunAsync(bench.GateContext(), Ct)).IsDone();

        rig.Events.Should().Equal("acquire", "launch", "release", "wait", "acquire", "launch", "release", "wait", "acquire", "launch", "release");
        rig.Lock.Paths.Should().OnlyContain(path => path == Path.Combine(bench.Root, ".gate.lock"), "the one lock of the experiment root, whatever the arm");
    }

    [Fact]
    public async Task The_before_snapshot_is_taken_before_the_first_attempt_and_the_diff_only_from_a_finished_step()
    {
        using var bench = new StepBench();
        bench.ReplaysByCall((1, "result-429-usage-limit-reached.jsonl"), (2, "result-success.jsonl"));
        bench.WriteStatus("proceed");
        var head = await bench.HeadAsync();
        var rig = new GateRig();
        bench.Waits.OnWait = async (_, _) =>
        {
            File.Exists(bench.Paths.Diff(GateName)).Should().BeFalse("a limited attempt is not a finished step");
            await bench.CommitFileAsync("g.txt", "work", "the gate's work");
        };

        (await rig.Gate(bench).RunAsync(bench.GateContext(), Ct)).IsDone();

        (await File.ReadAllTextAsync(bench.Paths.Before(GateName), Ct)).Trim().Should().Be(head, "the snapshot is HEAD before the first attempt");
        File.Exists(bench.Paths.Diff(GateName)).Should().BeTrue("a finished gate has its diff");
        (await File.ReadAllTextAsync(bench.Paths.Diff(GateName), Ct)).Should().Contain("g.txt");

        using var review = new StepBench();
        review.ReplaysByCall((1, "result-429-usage-limit-reached.jsonl"), (2, "result-success.jsonl"));
        var reviewHead = await review.HeadAsync();
        review.Waits.OnWait = async (_, _) =>
        {
            File.Exists(review.Paths.Diff("2-hreview")).Should().BeFalse();
            (await File.ReadAllTextAsync(review.Paths.Before("2-hreview"), Ct)).Trim().Should().Be(reviewHead, "the snapshot is taken before call 1");
            await review.CommitFileAsync("h.txt", "work", "the reviewer's work");
        };

        (await review.Runner().RunAsync(review.Context("CR", 1), Ct)).IsDone();

        File.Exists(review.Paths.Diff("2-hreview")).Should().BeTrue("a finished reviewer has its diff");
        (await File.ReadAllTextAsync(review.Paths.Diff("2-hreview"), Ct)).Should().Contain("h.txt");
    }

    [Fact]
    public async Task Still_blocked_after_every_wait_finishes_the_step_with_its_status()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        bench.WriteStatus("blocked", "never");
        var rig = new GateRig();
        rig.WhenWaiting(bench, _ => { });

        var outcome = await rig.Gate(bench).RunAsync(bench.GateContext(), Ct);

        outcome.IsDone().Skipped.Should().BeFalse();
        bench.Fake.Calls().Should().HaveCount(3);
        bench.Waits.Recorded.Should().HaveCount(2);
        bench.GateWaits().Should().HaveCount(2);
        File.Exists(bench.Paths.Done(GateName)).Should().BeTrue();
        File.Exists(bench.Paths.Diff(GateName)).Should().BeTrue();
        bench.LogLines.Last().Should().EndWith("gate status blocked — after 2 waits, finished as the shell did");
    }

    [Fact]
    public async Task A_gate_step_that_is_done_regenerates_a_missing_diff_and_launches_nothing()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        bench.WriteStatus("proceed");
        (await new GateRig().Gate(bench).RunAsync(bench.GateContext(), Ct)).IsDone();
        File.Delete(bench.Paths.Diff(GateName));

        var outcome = await new GateRig().Gate(bench).RunAsync(bench.GateContext(), Ct);

        outcome.IsDone().Skipped.Should().BeTrue();
        bench.Fake.Calls().Should().HaveCount(1);
        File.Exists(bench.Paths.Diff(GateName)).Should().BeTrue("the finalization writes a missing diff from the .before");
    }

    [Fact]
    public async Task A_blocked_round_leaves_no_diff_behind()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        bench.WriteStatus("blocked", "once");
        var rig = new GateRig();
        rig.WhenWaiting(bench, _ =>
        {
            File.Exists(bench.Paths.Diff(GateName)).Should().BeFalse("the diff belongs to a finished gate, not to a round");
            bench.WriteStatus("proceed");
        });

        (await rig.Gate(bench).RunAsync(bench.GateContext(), Ct)).IsDone();

        bench.Waits.Recorded.Should().HaveCount(1, "the round was waited, so the assertion in the wait ran");
        File.Exists(bench.Paths.Diff(GateName)).Should().BeTrue();
    }

    [Fact]
    public async Task A_gate_that_wrote_no_status_is_judged_missing_and_finishes_without_a_wait()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");

        var outcome = await new GateRig().Gate(bench).RunAsync(bench.GateContext(), Ct);

        outcome.IsDone();
        bench.Waits.Recorded.Should().BeEmpty();
        bench.GateWaits().Should().BeEmpty();
        bench.LogLines.Last().Should().EndWith("gate status missing");
    }

    [Fact]
    public async Task A_stopped_step_is_returned_at_once_and_never_repeated_inside_the_gate()
    {
        using var bench = new StepBench();
        bench.Replays("result-400-model-unsupported.jsonl");
        bench.WriteStatus("blocked", "irrelevant");
        var rig = new GateRig();

        var outcome = await rig.Gate(bench).RunAsync(bench.GateContext(), Ct);

        outcome.IsStopped(5);
        bench.Fake.Calls().Should().HaveCount(1, "an exit 5 relaunch costs one paid attempt: the next entry decides, not this loop");
        bench.GateWaits().Should().BeEmpty();
        bench.Waits.Recorded.Should().BeEmpty();
        rig.Events.Should().Equal("acquire", "launch", "release");
    }

    [Fact]
    public async Task A_stop_during_a_blocked_round_leaves_the_marker_and_ends_with_a_stopped_line()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        bench.WriteStatus("blocked", "wait for me");
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        bench.Waits.OnWait = async (_, token) =>
        {
            await stop.CancelAsync();
            token.ThrowIfCancellationRequested();
        };

        var outcome = await new GateRig().Gate(bench).RunAsync(bench.GateContext(), stop.Token);

        outcome.IsStopped(5);
        bench.LastStopped().Exit.Should().Be(5);
        File.Exists(bench.Paths.Continue(GateName)).Should().BeTrue("the intent is on disk: the next entry resumes the session");
        File.Exists(bench.Paths.Done(GateName)).Should().BeFalse();
        bench.GateWaits().Should().HaveCount(1);
    }

    [Fact]
    public async Task A_finished_gate_whose_before_snapshot_is_gone_stops_resumable_instead_of_finishing_without_a_diff()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        bench.WriteStatus("proceed");
        (await new GateRig().Gate(bench).RunAsync(bench.GateContext(), Ct)).IsDone();
        File.Delete(bench.Paths.Diff(GateName));
        File.Delete(bench.Paths.Before(GateName));

        var outcome = await new GateRig().Gate(bench).RunAsync(bench.GateContext(), Ct);

        outcome.IsStopped(5).Why.Should().Contain("gate.before is missing");
        File.Exists(bench.Paths.Diff(GateName)).Should().BeFalse();
        bench.LastStopped().Exit.Should().Be(5);
    }

    [Fact]
    public async Task A_lock_held_by_another_process_is_waited_for_at_most_the_ceiling_the_wait_is_said_and_the_stop_names_the_holder()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the holder helper and its note are Linux's");
        Assert.SkipUnless(File.Exists("/usr/bin/sleep"), "the holder's child is /usr/bin/sleep, started by its absolute path");
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        bench.WriteStatus("proceed");
        var lockPath = Path.Combine(bench.Root, ".gate.lock");
        await using var holder = new ModelArmsBinary.Child(ModelArmsBinary.HolderExecutable, [lockPath, "/usr/bin/sleep", "40"], bench.Root, bench.Parent);
        await holder.LineAsync("held", TimeSpan.FromSeconds(30));
        var real = new FileGateLock(TimeProvider.System, TimeSpan.FromMilliseconds(50));

        var started = TimeProvider.System.GetTimestamp();
        var outcome = await new GateRig().Gate(bench, gateLock: real, ceiling: TimeSpan.FromMilliseconds(400)).RunAsync(bench.GateContext(), Ct);
        var waited = TimeProvider.System.GetElapsedTime(started);

        outcome.IsStopped(5).Why.Should().Contain("held by pid").And.Contain("400 ms");
        waited.Should().BeLessThan(TimeSpan.FromSeconds(20), "the wait ended at its ceiling, not at the holder's end");
        bench.Fake.Calls().Should().BeEmpty("nothing was launched without the lock");
        File.Exists(bench.Paths.Done(GateName)).Should().BeFalse();
        bench.LastStopped().Why.Should().Contain("held by pid");
        bench.LogLines.Should().Contain(line => line.Contains("[C1] 4-gate: the gate lock is held by pid", StringComparison.Ordinal) && line.Contains("waiting at most 400 ms", StringComparison.Ordinal),
            "the operator is told, before the wait, who has the lock and for how long this gate waits");
    }

    [Fact]
    public async Task A_stop_while_waiting_for_the_lock_is_the_operators_stop_not_the_ceiling()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the holder helper and its note are Linux's");
        Assert.SkipUnless(File.Exists("/usr/bin/sleep"), "the holder's child is /usr/bin/sleep, started by its absolute path");
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        var lockPath = Path.Combine(bench.Root, ".gate.lock");
        await using var holder = new ModelArmsBinary.Child(ModelArmsBinary.HolderExecutable, [lockPath, "/usr/bin/sleep", "40"], bench.Root, bench.Parent);
        await holder.LineAsync("held", TimeSpan.FromSeconds(30));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        stop.CancelAfter(TimeSpan.FromMilliseconds(300));
        var real = new FileGateLock(TimeProvider.System, TimeSpan.FromMilliseconds(50));

        var outcome = await new GateRig().Gate(bench, gateLock: real, ceiling: TimeSpan.FromMinutes(30)).RunAsync(bench.GateContext(), stop.Token);

        outcome.IsStopped(5).Why.Should().Contain("stopped by the operator");
        bench.Fake.Calls().Should().BeEmpty();
    }
}
