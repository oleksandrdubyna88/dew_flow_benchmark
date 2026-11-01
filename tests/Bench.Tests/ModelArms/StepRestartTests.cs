using System.Text.Json.Nodes;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Briefs;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>Model arms E2.S4 — what the NEXT entry does with what a dead runner left (the restart matrix of the story), the continue marker's runner half, the derived outputs a restart writes, and what a step that finished without writing a bound output says. A
/// runner killed at a point is a probe that throws there; the second runner reads the same folders.
/// <para>
/// What this does NOT prove: a kill at a point no <see cref="StepPoint"/> names (the gate's own windows are E2.S7's); a power cut mid-write (the writes are staged and renamed, not fsynced across a crash by a test); a real claude's own session file.
/// </para></summary>
public sealed class StepRestartTests
{
    private const string Gate = "4-gate";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A finished gate step, its marker asking for its session to go on, and its <c>.done</c> removed or left.</summary>
    private static async Task<StepContext> FinishedGateWithMarkerAsync(StepBench bench, bool keepDone)
    {
        bench.Replays("result-success.jsonl");
        var context = bench.Context("C", 3) with { DeriveOutputs = false };
        (await bench.Runner().RunAsync(context, Ct)).IsDone();
        File.Exists(bench.Paths.Done(Gate)).Should().BeTrue("the first run finished the step");
        File.WriteAllText(bench.Paths.Continue(Gate), ContinueMarkers.Write(new ContinueMarker(1, "gate blocked: test")));
        if (!keepDone)
        {
            File.Delete(bench.Paths.Done(Gate));
        }

        return context;
    }

    private static async Task KilledAtPreparedAsync(StepBench bench, StepContext context)
    {
        var killed = bench.Runner(new ProbeAt(StepPoint.Prepared, () => throw new IOException("killed")));
        await FluentActions.Awaiting(() => killed.RunAsync(context, Ct)).Should().ThrowAsync<IOException>().WithMessage("killed");
    }

    [Fact]
    public async Task A_continue_marker_makes_the_runner_resume_the_finished_session_and_remove_a_leftover_done()
    {
        using var bench = new StepBench();
        var context = await FinishedGateWithMarkerAsync(bench, keepDone: true);
        bench.Fake.Calls().Should().HaveCount(1, "the first run launched once");

        var outcome = await bench.Runner().RunAsync(context, Ct);

        var done = outcome.IsDone();
        (done.Skipped, done.Recovered).Should().Be((false, false));
        var calls = bench.Fake.Calls();
        calls.Should().HaveCount(2);
        calls[1].After("--resume").Should().Be(calls[0].After("--session-id"));
        calls[1].Prompt.Should().StartWith("This session was interrupted");
        DoneMarkers.Read(File.ReadAllText(bench.Paths.Done(Gate))).Ok().Attempts.Should().Be(2);
        File.Exists(bench.Paths.Continue(Gate)).Should().BeFalse("the step is done: its marker is removed after .done");
        bench.Log.ToString().Should().Contain("continuing its finished session as the gate asked after attempt 1");
    }

    [Fact]
    public async Task A_continue_marker_that_cannot_be_read_is_logged_once_and_ignored()
    {
        using var bench = new StepBench();
        var context = await FinishedGateWithMarkerAsync(bench, keepDone: true);
        File.WriteAllText(bench.Paths.Continue(Gate), "{ not json");

        var outcome = await bench.Runner().RunAsync(context, Ct);

        outcome.IsDone().Skipped.Should().BeTrue("a marker nobody can read orders nothing, and .done skips");
        bench.Fake.Calls().Should().HaveCount(1);
        bench.LogLines.Count(line => line.Contains("4-gate.continue could not be read", StringComparison.Ordinal)).Should().Be(1);
    }

    [Fact]
    public async Task A_restart_after_the_prompt_but_before_the_stream_launches_the_attempt_once_without_an_overwrite_and_leaves_one_start_line()
    {
        using var bench = new StepBench();
        var context = await FinishedGateWithMarkerAsync(bench, keepDone: false);
        await KilledAtPreparedAsync(bench, context);

        var prompt = bench.Paths.Prompt(Gate, 2);
        File.Exists(prompt).Should().BeTrue("the resume notice is on disk before the launch");
        var first = File.ReadAllText(prompt);
        File.Exists(bench.Paths.Stream(Gate, 2)).Should().BeFalse("nothing launched");
        bench.Timing().OfType<TimingLine.Start>().Count(start => start.Attempt == 2).Should().Be(0, "the start line waits for the stream");
        bench.Fake.Calls().Should().HaveCount(1);
        File.Exists(bench.Paths.Continue(Gate)).Should().BeTrue();

        bench.Clock.Advance(TimeSpan.FromMinutes(10));
        var outcome = await bench.Runner().RunAsync(context, Ct);

        outcome.IsDone().End.Should().BeOfType<StepEnd.Finished>();
        var calls = bench.Fake.Calls();
        calls.Should().HaveCount(2);
        calls[1].After("--resume").Should().Be(calls[0].After("--session-id"));
        var second = File.ReadAllText(prompt);
        second.Should().Be(calls[1].Prompt).And.NotBe(first);
        (first, second).Should().Match<(string First, string Second)>(prompts => prompts.First.Contains("21:33 UTC", StringComparison.Ordinal) && prompts.Second.Contains("21:43 UTC", StringComparison.Ordinal));
        bench.Timing().Where(line => line is TimingLine.Start or TimingLine.End).Select(line => line is TimingLine.Start start ? $"start {start.Attempt}" : $"end {((TimingLine.End)line).Attempt}")
            .Should().Equal("start 1", "end 1", "start 2", "end 2");
        DoneMarkers.Read(File.ReadAllText(bench.Paths.Done(Gate))).Ok().Attempts.Should().Be(2);
        File.Exists(bench.Paths.Continue(Gate)).Should().BeFalse();
        bench.Log.ToString().Should().Contain("attempt 2 was prepared by an earlier run and never launched — its prompt is rewritten");
    }

    [Fact]
    public async Task A_restart_between_the_prompt_and_the_stream_leaves_the_pinned_consumer_one_row_per_attempt()
    {
        using var bench = new StepBench();
        var context = await FinishedGateWithMarkerAsync(bench, keepDone: false);
        await KilledAtPreparedAsync(bench, context);
        bench.Clock.Advance(TimeSpan.FromMinutes(10));
        (await bench.Runner().RunAsync(context, Ct)).IsDone();

        var run = await PinnedConsumer.MetricsAsync(bench.Combine("metrics"), bench.Paths.Timing, "C", Ct);

        run.ExitCode.Should().Be(0, run.Error);
        var attempts = run.Report!["steps"]![Gate]!.AsArray().Select(row => row!["attempt"]!.GetValue<int>()).ToList();
        attempts.Should().Equal([1, 2], "each attempt is one row: the pinned consumer counts every start line it meets");
    }

    [Fact]
    public async Task A_start_line_written_twice_for_one_attempt_is_counted_twice_by_the_pinned_consumer_and_that_is_why_the_start_waits_for_the_stream()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        var context = bench.Context("C", 3) with { DeriveOutputs = false };
        (await bench.Runner().RunAsync(context, Ct)).IsDone();
        var lines = bench.Timing();
        lines.Should().HaveCount(2, "a start and an end");
        var start = lines.OfType<TimingLine.Start>().Single();

        // the refuted order of the story's first draft: the start line before the stream, a restart in between, so two lines name ONE stream
        var twice = new TimingLine.Start(start.Arm, start.Step, 2, "resume", start.Session, start.Model, start.StartUtc, start.BudgetSeconds, start.Deadline, start.Head, start.Stream);
        File.WriteAllLines(bench.Paths.Timing, [.. lines.Select(TimingLines.Write), TimingLines.Write(twice), TimingLines.Write(twice)]);

        var run = await PinnedConsumer.MetricsAsync(bench.Combine("metrics"), bench.Paths.Timing, "C", Ct);

        run.ExitCode.Should().Be(0, run.Error);
        run.Report!["steps"]![Gate]!.AsArray().Count(row => row!["attempt"]!.GetValue<int>() == 2).Should().Be(2, "metrics.mjs reads every start line as an attempt: one stream is counted twice");
    }

    [Fact]
    public async Task A_runner_killed_between_the_result_line_and_review_json_writes_the_review_on_the_next_entry()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        var context = bench.Context("C", 1);
        var review = bench.NotesFile("notes/review.json");

        var killed = bench.Runner(new ProbeAt(StepPoint.Classified, () => throw new IOException("killed")));
        await FluentActions.Awaiting(() => killed.RunAsync(context, Ct)).Should().ThrowAsync<IOException>();
        File.Exists(review).Should().BeFalse("the runner died before it derived the verdict");
        File.Exists(bench.Paths.Done("2-review")).Should().BeFalse();

        var outcome = await bench.Runner().RunAsync(context, Ct);

        var done = outcome.IsDone();
        (done.Skipped, done.Recovered).Should().Be((false, true));
        bench.Fake.Calls().Should().HaveCount(1, "the finished stream is recovered, never resumed");
        using var clean = new StepBench();
        clean.Replays("result-success.jsonl");
        (await clean.Runner().RunAsync(clean.Context("C", 1), Ct)).IsDone();
        File.ReadAllText(review).Should().Be("""{"verdict":"ready","findings":[]}""").And.Be(File.ReadAllText(clean.NotesFile("notes/review.json")), "the bytes a clean run writes");
        DoneMarkers.Read(File.ReadAllText(bench.Paths.Done("2-review"))).Ok().Recovered.Should().BeTrue();
    }

    [Fact]
    public async Task A_task_coder_whose_head_and_diff_are_missing_writes_them_on_the_next_entry_from_its_folder()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");
        var folder = await bench.NewRepositoryAsync("task-folder");
        var before = await bench.HeadAsync(folder);
        await bench.CommitFileAsync("t1.txt", "the task's work\n", "the coder's commit", folder);
        Directory.CreateDirectory(bench.Paths.Out);
        File.WriteAllText(bench.Paths.TaskBefore("t1"), before + "\n");
        var context = bench.Context("SRS", 0) with
        {
            Placed = new SchemeEntry.Step("2-t1-code", bench.Experiment.Tasks.TaskCode),
            WorkingDirectory = folder,
            Task = new TaskFacts("t1", Path.Combine(bench.Paths.Work, "t1.md"), "`t1.txt`", before),
        };

        var killed = bench.Runner(new ProbeAt(StepPoint.Classified, () => throw new IOException("killed")));
        await FluentActions.Awaiting(() => killed.RunAsync(context, Ct)).Should().ThrowAsync<IOException>().WithMessage("killed");
        File.Exists(bench.Paths.CoderHead("t1")).Should().BeFalse("the runner died before it derived the coder's head and diff");
        File.Exists(bench.Paths.Done("2-t1-code")).Should().BeFalse();

        var outcome = await bench.Runner().RunAsync(context, Ct);

        var done = outcome.IsDone();
        (done.Skipped, done.Recovered).Should().Be((false, true));
        bench.Fake.Calls().Should().HaveCount(1, "the finished stream is recovered, never resumed — no launch on the next entry");
        var head = await bench.HeadAsync(folder);
        head.Should().NotBe(before, "the folder's HEAD is the coder's commit, not the base");
        File.ReadAllText(bench.Paths.CoderHead("t1")).Should().Be(head + "\n", "the HEAD of the task's own folder, not of the arm's clone");
        File.ReadAllText(bench.Paths.CoderDiff("t1")).Should().Be(await GitOutAsync(folder, "diff", before, "HEAD")).And.Contain("+the task's work");
        File.Exists(bench.Paths.Done("2-t1-code")).Should().BeTrue();
    }

    private static async Task<string> GitOutAsync(string folder, params string[] arguments)
    {
        var attempt = await Bench.Infrastructure.Process.ProcessRunner.RunAsync(ExperimentRoot.RealGit, arguments, folder, TimeSpan.FromSeconds(30), Ct);
        return attempt.Should().BeOfType<Bench.Infrastructure.Process.ProcessAttempt.Completed>().Subject.Result.StandardOutput;
    }

    [Fact]
    public async Task A_step_done_without_writing_a_bound_output_says_so_in_its_log_and_end_line_and_the_arm_goes_on()
    {
        using var bench = new StepBench();
        bench.Replays("result-success.jsonl");

        (await bench.Runner().RunAsync(bench.Context("C", 0), Ct)).IsDone().End.Should().BeOfType<StepEnd.Finished>();

        File.Exists(bench.Paths.Done("1-code")).Should().BeTrue("a skipped note is recorded, never required");
        bench.Log.ToString().Should().Contain("1-code done without writing CODE_NOTES (notes/code.md)");
        bench.Timing().OfType<TimingLine.End>().Last().Missing.Should().Equal("CODE_NOTES");
        File.ReadAllLines(bench.Paths.Timing).Last().Should().Contain("\"missing\":[\"CODE_NOTES\"]");

        using (var written = new StepBench())
        {
            written.Replays("result-success.jsonl");
            written.Write("notes\n", "r", written.Token, "notes", "code.md");
            (await written.Runner().RunAsync(written.Context("C", 0), Ct)).IsDone();
            written.Timing().OfType<TimingLine.End>().Last().Missing.Should().BeEmpty();
            File.ReadAllLines(written.Paths.Timing).Last().Should().NotContain("missing");
            written.Log.ToString().Should().NotContain("done without writing");
        }

        bench.Write("""{"verdict":"ready","findings":[]}""", "r", bench.Token, "notes", "review.json");
        (await bench.Runner().RunAsync(bench.Context("C", 2), Ct)).IsDone();
        bench.Fake.Calls().Should().HaveCount(2);
        bench.Fake.Calls()[1].Prompt.Should().Contain(bench.NotesFile("notes/code.md"), "the path is handed whether or not the file exists, as the shell did");
        var log = bench.Log.ToString();
        log.Should().Contain("3-fix: CODE_NOTES (notes/code.md) was not written by step 1-code — the brief names a file that does not exist");
        log.Should().NotContain("REVIEW (notes/review.json) was not written");
    }

    [Fact]
    public async Task A_limited_attempt_whose_transcript_is_gone_starts_over_with_a_new_session_id()
    {
        using var bench = new StepBench();
        bench.Script(new JsonObject { ["stdoutByCall"] = new JsonObject { ["1"] = StepBench.Replay("result-429-usage-limit-reached.jsonl"), ["2"] = StepBench.Replay("result-success.jsonl") } });
        bench.Transcripts.Answer = false;

        // claude left no transcript for the session: the limited attempt is started over under a new id, not resumed
        await bench.Runner().RunAsync(bench.Context("C", 0), Ct);

        var calls = bench.Fake.Calls();
        calls.Should().HaveCount(2);
        calls[1].Argv.Should().Contain("--session-id").And.NotContain("--resume");
        calls[1].After("--session-id").Should().NotBe(calls[0].After("--session-id"));
        bench.SessionOf("1-code").Should().Be(calls[1].After("--session-id"), "the fresh id replaced the lost transcript's");
    }
}
