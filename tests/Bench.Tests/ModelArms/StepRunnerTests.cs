using System.Diagnostics;
using System.Text.Json.Nodes;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Steps;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>Model arms E2.S4 — one step, end to end, through the real runner: the fake CLI replays recorded streams, the files are real and the git is the host's (<see cref="StepBench"/>). The scenarios are the parent's §7; each starts from a fresh bench.
/// <para>
/// What this does NOT prove: that a real claude behaves as its recorded streams do, or that a limit really lifts after <c>waitSeconds</c> (the pilot); that two arms running at once do not interfere (E2.S8); the gate step's lock and loop (E2.S7).
/// The wall is a real second or two: <c>The_wall_kills_the_tree_and_marks_the_hard_deadline_done</c> and <c>Stderr_is_on_disk_before_the_process_ends</c> are the only tests here that wait.
/// </para></summary>
public sealed class StepRunnerTests
{
    private const string Code = "1-code";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static JsonObject ByCall(params (int Call, string Fixture)[] calls) =>
        new() { ["stdoutByCall"] = new JsonObject(calls.Select(call => KeyValuePair.Create(call.Call.ToString(), (JsonNode?)StepBench.Replay(call.Fixture)))) };

    private static void HardDeadlineSetUp(StepBench bench)
    {
        bench.SpendWall(Code, 59);
        bench.Replays("stream-no-result.jsonl", new JsonObject { ["holdAfterLine"] = 1 });
    }

    private static bool IsAlive(int pid)
    {
        try
        {
            return !Process.GetProcessById(pid).HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    [Fact]
    public async Task A_limited_attempt_waits_then_resumes_the_same_session_and_the_wait_is_not_counted()
    {
        using var bench = new StepBench();
        bench.Script(ByCall((1, "result-429-usage-limit-reached.jsonl"), (2, "result-success.jsonl")));

        var outcome = await bench.Runner().RunAsync(bench.Context("C", 0), Ct);

        outcome.IsDone().End.Should().BeOfType<StepEnd.Finished>();
        var calls = bench.Fake.Calls();
        calls.Should().HaveCount(2);
        var session = bench.SessionOf(Code);
        calls[0].After("--session-id").Should().Be(session);
        calls[1].After("--resume").Should().Be(session, "the second attempt continues the first one's session");
        calls[1].Argv.Should().NotContain("--session-id");
        calls[1].Prompt.Should().StartWith("This session was interrupted", "a resumed session is given the resume notice, not its brief");
        bench.Waits.Recorded.Should().Equal(TimeSpan.FromSeconds(900));

        var timing = bench.Timing();
        timing.Select(line => line.GetType().Name).Should().Equal("Start", "End", "Start", "End");
        timing[0].Should().BeOfType<TimingLine.Start>().Which.BudgetSeconds.Should().Be(60);
        timing[1].Should().BeOfType<TimingLine.End>().Which.Limited.Should().BeTrue();
        timing[2].Should().BeOfType<TimingLine.Start>().Which.BudgetSeconds.Should().Be(60, "the limited attempt's wall is not counted");
        timing[3].Should().BeOfType<TimingLine.End>().Which.State.Should().Be("finished");
    }

    [Fact]
    public async Task An_errored_attempt_stops_the_arm_with_exit_5_and_no_done_marker()
    {
        using var bench = new StepBench();
        bench.Replays("result-400-model-unsupported.jsonl");

        var outcome = await bench.Runner().RunAsync(bench.Context("C", 0), Ct);

        outcome.IsStopped(5);
        File.Exists(bench.Paths.Done(Code)).Should().BeFalse("an error result is never done");
        var stopped = bench.LastStopped();
        (stopped.Exit, stopped.Why).Should().Match<(int Exit, string Why)>(line => line.Exit == 5 && line.Why.Contains("ERROR result that is not a limit", StringComparison.Ordinal));
        bench.Fake.Calls().Should().HaveCount(1);
    }

    [Fact]
    public async Task A_finished_attempt_writes_done_the_stream_the_stderr_the_prompt_and_both_timing_lines()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl", new JsonObject { ["stderrBeforeLines"] = new JsonArray("warn 1") });

        var outcome = await bench.Runner().RunAsync(bench.Context("C", 0), Ct);

        outcome.IsDone().End.Should().BeOfType<StepEnd.Finished>();
        var call = bench.Fake.Calls().Should().ContainSingle().Subject;
        File.Exists(bench.Paths.Stream(Code, 1)).Should().BeTrue();
        var cwdAsJson = System.Text.Json.JsonSerializer.Serialize(call.Cwd)[1..^1];
        File.ReadAllText(bench.Paths.Stream(Code, 1)).Should().Be(ModelArmsFixtures.Read("result-success.jsonl").Replace("__CWD__", cwdAsJson, StringComparison.Ordinal));
        File.ReadAllText(bench.Paths.Stderr(Code, 1)).Should().Be("warn 1\n");
        var prompt = File.ReadAllText(bench.Paths.Prompt(Code, 1));
        prompt.Should().Contain(bench.Experiment.Target.Plan).And.NotContain("{{", "the brief is filled");
        prompt.Should().Be(call.Prompt, "the file is the prompt the launcher sent");
        bench.SessionOf(Code).Should().Be(call.After("--session-id"));

        var timing = bench.Timing();
        timing.Select(line => line.GetType().Name).Should().Equal("Start", "End");
        timing[1].Should().BeOfType<TimingLine.End>().Which.Should().Match<TimingLine.End>(end => end.Result && !end.Limited && end.State == "finished");
        File.ReadAllText(bench.Paths.Done(Code)).Trim().Should().Be($$"""{"attempts":1,"exit":0,"stream":"{{bench.Paths.Stream(Code, 1)}}","end":"finished"}""");
    }

    [Fact]
    public async Task The_wall_kills_the_tree_and_marks_the_hard_deadline_done()
    {
        using var bench = new StepBench();
        HardDeadlineSetUp(bench);
        var clock = Stopwatch.StartNew();

        var outcome = await bench.Runner().RunAsync(bench.Context("C", 0), Ct);

        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30), "the wall is 2 s: the one-second floor of the budget and the one second of grace");
        outcome.IsDone().End.Should().BeOfType<StepEnd.HardDeadline>();
        IsAlive(bench.Fake.Calls().Should().ContainSingle().Subject.Pid).Should().BeFalse("the wall kills the process tree");
        var done = DoneMarkers.Read(File.ReadAllText(bench.Paths.Done(Code))).Ok();
        done.State.Should().Be("hard-deadline");
        done.Exit.WasCaptured.Should().BeFalse("a killed process has no exit code");
        bench.Timing().OfType<TimingLine.End>().Last().Exit.WasCaptured.Should().BeFalse();
    }

    [Theory]
    [InlineData("finished")]
    [InlineData("hard-deadline")]
    public async Task The_done_marker_and_the_end_line_carry_the_classifiers_state_finished_or_hard_deadline(string state)
    {
        using var bench = new StepBench();
        if (state == "finished")
        {
            bench.Replays("result-success.jsonl");
        }
        else
        {
            HardDeadlineSetUp(bench);
        }

        await bench.Runner().RunAsync(bench.Context("C", 0), Ct);

        File.Exists(bench.Paths.Done(Code)).Should().BeTrue();
        DoneMarkers.Read(File.ReadAllText(bench.Paths.Done(Code))).Ok().State.Should().Be(state);
        bench.Timing().OfType<TimingLine.End>().Last().State.Should().Be(state);
    }

    [Fact]
    public async Task Stderr_is_on_disk_before_the_process_ends()
    {
        using var bench = new StepBench();
        bench.SpendWall(Code, 55);
        bench.Replays("stream-no-result.jsonl", new JsonObject { ["stderrBeforeLines"] = new JsonArray("warn 1", "warn 2"), ["holdAfterLine"] = 2 });
        var run = Task.Run(() => bench.Runner().RunAsync(bench.Context("C", 0), Ct), Ct);

        var seen = false;
        for (var waited = 0; waited < 160 && !seen && !run.IsCompleted; waited++)
        {
            seen = HoldsWarningWhileRunning(bench);
            await Task.Delay(50, Ct);
        }

        var outcome = await run;
        seen.Should().BeTrue("the .err file should hold 'warn 2' while the CLI runs, but it was empty until the process ended");
        outcome.IsDone().End.Should().BeOfType<StepEnd.HardDeadline>();
    }

    private static bool HoldsWarningWhileRunning(StepBench bench)
    {
        var held = Directory.Exists(bench.Fake.Home) ? Directory.EnumerateFiles(bench.Fake.Home, "held-*").FirstOrDefault() : null;
        var errPath = bench.Paths.Stderr(Code, 1);
        if (held is null || !File.Exists(errPath) || !IsAlive(int.Parse(Path.GetFileName(held)["held-".Length..], System.Globalization.CultureInfo.InvariantCulture)))
        {
            return false;
        }

        using var stream = new FileStream(errPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Contains("warn 2", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Still_limited_after_every_wait_exits_3()
    {
        using var bench = new StepBench();
        bench.Replays("result-429-usage-limit-reached.jsonl");

        var outcome = await bench.Runner().RunAsync(bench.Context("C", 0), Ct);

        outcome.IsStopped(3);
        bench.Fake.Calls().Should().HaveCount(3);
        bench.Waits.Recorded.Should().Equal(TimeSpan.FromSeconds(900), TimeSpan.FromSeconds(900));
        var stopped = bench.LastStopped();
        (stopped.Exit, stopped.Why).Should().Match<(int Exit, string Why)>(line => line.Exit == 3 && line.Why.Contains("still limited after 2 waits", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ctrl_c_during_the_wait_exits_5_resumable()
    {
        using var bench = new StepBench();
        bench.Replays("result-429-usage-limit-reached.jsonl");
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        bench.Waits.OnWait = (_, token) =>
        {
            stopping.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        };

        var outcome = await bench.Runner().RunAsync(bench.Context("C", 0), stopping.Token);

        outcome.IsStopped(5);
        File.Exists(bench.Paths.Done(Code)).Should().BeFalse();
        var session = bench.SessionOf(Code);
        session.Should().Be(bench.Fake.Calls()[0].After("--session-id"), ".session is intact");
        bench.LastStopped().Why.Should().Contain("stopped by the operator");

        bench.Replays("result-success.jsonl");
        bench.Waits.OnWait = null;
        var resumed = await bench.Runner().RunAsync(bench.Context("C", 0), Ct);

        resumed.IsDone().End.Should().BeOfType<StepEnd.Finished>();
        bench.Fake.Calls()[1].After("--resume").Should().Be(session, "the next run resumes the session the interrupted one started");
    }

    [Fact]
    public async Task The_resume_notice_names_the_new_deadline_and_the_base_commit()
    {
        using var bench = new StepBench();
        bench.Script(ByCall((1, "result-429-usage-limit-reached.jsonl"), (2, "result-success.jsonl")));

        await bench.Runner().RunAsync(bench.Context("C", 0), Ct);

        bench.Fake.Calls().Should().HaveCount(2);
        bench.Fake.Calls()[1].Prompt.Should().Be(ResumeNotice.For(bench.Experiment.Target.Base.Value, "21:33 UTC (22:33 +01:00)"), "60 s from the clock's 21:32:38 UTC, in Paris at +01:00");
    }

    [Fact]
    public async Task The_session_file_is_written_before_the_launch()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        var sessionAtLaunch = "(the probe was not reached)";
        var probe = new ProbeAt(StepPoint.Launched, () => sessionAtLaunch = File.Exists(bench.Paths.Session(Code)) ? File.ReadAllText(bench.Paths.Session(Code)).Trim() : "(no .session yet)");

        await bench.Runner(probe).RunAsync(bench.Context("C", 0), Ct);

        sessionAtLaunch.Should().Be(bench.Fake.Calls().Should().ContainSingle().Subject.After("--session-id"), ".session names the session the launch starts, and is on disk before the launch");
    }

    [Fact]
    public async Task Every_launch_carries_the_arms_environment_and_no_scout_flag()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");

        await bench.Runner().RunAsync(bench.Context("C", 0), Ct);

        var call = bench.Fake.Calls().Should().ContainSingle().Subject;
        call.EnvironmentNames.Should().Contain(["TMPDIR", "HOME", "PATH", "FAKE_CLI_HOME", "PWD"]);
        call.EnvironmentNames.Should().NotContain(["BENCH_DB", "SUBAGENT_MODEL_GUARD", "CLAUDE_CODE_DISABLE_BACKGROUND_TASKS", "OLDPWD"]);
        call.SubagentModelGuard.Should().BeEmpty();
    }

    [Fact]
    public async Task A_rerun_after_done_launches_nothing()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        await bench.Runner().RunAsync(bench.Context("C", 0), Ct);
        bench.Fake.Calls().Should().HaveCount(1);
        var timing = File.ReadAllText(bench.Paths.Timing);

        var again = await bench.Runner().RunAsync(bench.Context("C", 0), Ct);

        var done = again.IsDone();
        done.Skipped.Should().BeTrue();
        done.End.Should().BeOfType<StepEnd.Finished>();
        bench.Fake.Calls().Should().HaveCount(1);
        File.ReadAllText(bench.Paths.Timing).Should().Be(timing, "a step that is done writes nothing");
    }

    [Fact]
    public async Task An_unfilled_brief_stops_with_exit_4_before_any_launch()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        bench.Write("{{NOPE}}", "briefs", "1-code.template.md");

        var outcome = await bench.Runner().RunAsync(bench.Context("C", 0), Ct);

        outcome.IsStopped(4).Why.Should().Contain("{{NOPE}}");
        bench.Fake.Calls().Should().BeEmpty();
        bench.Timing().Should().NotContain(line => line is TimingLine.Start);
        bench.LastStopped().Exit.Should().Be(4);
    }

    [Fact]
    public async Task A_missing_claude_is_not_started_and_exits_3()
    {
        using var bench = new StepBench();
        var context = bench.Context("C", 0) with { ClaudeExecutable = bench.Combine("no-such-claude") };

        var outcome = await bench.Runner().RunAsync(context, Ct);

        outcome.IsStopped(3);
        bench.Timing().OfType<TimingLine.End>().Last().State.Should().Be("not-started");
        File.Exists(bench.Paths.Done(Code)).Should().BeFalse();
    }

    [Fact]
    public async Task Log_lines_carry_no_token()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");

        await bench.Runner().RunAsync(bench.Context("C", 0), Ct);

        var log = bench.Log.ToString();
        log.Should().Contain("[C1]").And.Contain("1-code done (finished)");
        log.Should().NotContain(bench.Token, "the arm map's token never reaches a log line");
        log.Should().Contain("<token>", "the token stands in its place: the attempt's line names the stream's path");
    }

    [Fact]
    public async Task Recover_answers_the_entry_a_run_would_take_and_launches_nothing()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        var runner = bench.Runner();

        runner.Recover(bench.Context("C", 0)).Should().Be(new StepEntry.Fresh(1, 60), "nothing on disk: a first attempt with the whole budget");
        await runner.RunAsync(bench.Context("C", 0), Ct);
        runner.Recover(bench.Context("C", 0)).Should().BeOfType<StepEntry.Skip>("the step is done");
        bench.Fake.Calls().Should().HaveCount(1, "asking recovery launched nothing");
    }
}
