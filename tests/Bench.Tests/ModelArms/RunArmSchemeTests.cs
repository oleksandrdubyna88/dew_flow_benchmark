using System.Text.Json.Nodes;
using Bench.Cli;
using Bench.Tests.Probes.Driver;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;
using Xunit;
using static Bench.Tests.ModelArms.RunArmKit;
using ModelArmsApp = ModelArms;

namespace Bench.Tests.ModelArms;

/// <summary>Model arms E2.S8 — <c>run-arm</c> for every sequential scheme (C, CR, CHH), through the console's own entry point over a real experiment root: the scheme's steps in order, the exits, <c>arm-done</c> once, and M1 — a
/// fake arm of each scheme, resumed after an interruption, read by the pinned <c>metrics.mjs</c>. The CLI the arm launches is the fake (<c>tests/FakeCli</c>) linked as <c>claude</c>; git is the host's, by its real path; nothing paid
/// is called. The verb knows four kinds of step and nothing about C, CR or CHH by name.
/// <para>
/// What this does NOT prove: a real claude (the fake replays recorded streams and leaves an empty transcript); the gate lock against a second arm (the lock's own tests and the busy-lock scenario here); a real <c>make</c> (the
/// fixture makes the arm); anything off Linux.
/// </para></summary>
public sealed class RunArmSchemeTests
{
    private const string Ready = """{"verdict":"ready","findings":[]}""";

    private static string Folder(ExperimentRoot root, string arm) => root.PathsOf(arm).Folder;

    [Fact]
    public async Task Scheme_C_runs_its_four_steps_in_order_against_the_fake()
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("C1", fake);
        Replay(fake, "result-success.jsonl");

        var run = await RunAsync(root, "C1");

        run.Code.Should().Be(0, run.Error + run.Output);
        var calls = fake.Calls();
        calls.Select(call => BriefOf(call)).Should().Equal("1-code", "2-review", "3-fix", "gate");
        var paths = root.PathsOf("C1");
        calls[1].After("--permission-mode").Should().Be("default");
        calls[1].Group("--disallowedTools").Should().Contain(["Edit", "Write", "NotebookEdit"], "the review reads and never writes");
        calls[3].After("--mcp-config").Should().Be(paths.McpCoai, "the gate's config is the coai one");
        calls.Take(3).Select(call => call.After("--mcp-config")).Should().OnlyContain(config => config == paths.McpNone);
        foreach (var step in StepsOf("C"))
        {
            File.Exists(paths.Done(step)).Should().BeTrue($"{step} is done");
        }

        root.NotesText("C1", "notes/review.json").Trim().Should().Be(Ready);
        File.Exists(paths.Before("4-gate")).Should().BeTrue();
        File.Exists(paths.Diff("4-gate")).Should().BeTrue();
        root.TimingOf("C1").Last().Should().BeOfType<TimingLine.ArmDone>().Which.Arm.Should().Be("C1");
    }

    [Fact]
    public async Task Scheme_CR_inserts_the_fresh_review_and_fix()
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("CR1", fake);
        Replay(fake, "result-success.jsonl");

        var run = await RunAsync(root, "CR1");

        run.Code.Should().Be(0, run.Error + run.Output);
        fake.Calls().Select(call => BriefOf(call)).Should().Equal("1-code", "hr-review-fix", "2-review", "3-fix", "gate");
        var paths = root.PathsOf("CR1");
        File.Exists(paths.Before("2-hreview")).Should().BeTrue();
        File.Exists(paths.Diff("2-hreview")).Should().BeTrue();
        root.NotesText("CR1", "notes/review.json").Trim().Should().Be(Ready, "the verdict is from 3-review's last result");
        foreach (var step in StepsOf("CR"))
        {
            File.Exists(paths.Done(step)).Should().BeTrue($"{step} is done");
        }

        root.ArmDones("CR1").Should().ContainSingle();
    }

    [Fact]
    public async Task Scheme_CHH_runs_two_read_only_reviews_to_two_verdict_files_a_fix_that_reads_both_and_a_diff_from_the_first_snapshot()
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("CHH1", fake);
        var verdictA = SuccessEndingIn(root, "a.jsonl", """{"verdict":"ready","findings":[{"id":"A"}]}""");
        var verdictB = SuccessEndingIn(root, "b.jsonl", """{"verdict":"ready","findings":[{"id":"B"}]}""");
        Replay(fake, "result-success.jsonl", (2, verdictA), (3, verdictB));

        var run = await RunAsync(root, "CHH1");

        run.Code.Should().Be(0, run.Error + run.Output);
        var calls = fake.Calls();
        calls.Select(call => BriefOf(call)).Should().Equal("1-code", "hA-trace", "hB-tests", "hfix", "2-review", "3-fix", "gate");
        var paths = root.PathsOf("CHH1");
        calls[1].Group("--disallowedTools").Should().Contain(["Edit", "Write"], "the first fresh review is read-only");
        calls[2].Group("--disallowedTools").Should().Contain(["Edit", "Write"], "the second fresh review is read-only");
        root.NotesText("CHH1", "notes/hA.json").Trim().Should().Be("""{"verdict":"ready","findings":[{"id":"A"}]}""", "call 2's own last result, never one verdict read twice");
        root.NotesText("CHH1", "notes/hB.json").Trim().Should().Be("""{"verdict":"ready","findings":[{"id":"B"}]}""", "call 3's own last result");
        calls[3].Prompt.Should().Contain(Path.Combine(paths.Folder, "notes", "hA.json")).And.Contain(Path.Combine(paths.Folder, "notes", "hB.json"), "the fixer is told both verdict files");
        File.Exists(paths.Before("2-hA")).Should().BeTrue("the first review's snapshot");
        File.Exists(paths.Before("4-hfix")).Should().BeFalse("the fix's diff is from the first review's snapshot, not from a snapshot of its own");
        File.ReadAllText(paths.Diff("4-hfix")).Should().BeEmpty("the fake commits nothing: the diff from the snapshot to HEAD is empty — and it exists");
        File.Exists(paths.Diff("4-hfix")).Should().BeTrue();
        root.ArmDones("CHH1").Should().ContainSingle();
    }

    [Fact]
    public async Task An_unknown_arm_is_refused_with_exit_4()
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("XY1", fake);
        Replay(fake, "result-success.jsonl");

        var unknown = await RunAsync(root, "XY1");
        var unmade = await RunAsync(root, "C9");

        unknown.Code.Should().Be(4, unknown.Output);
        unknown.Error.Should().Contain("XY").And.Contain("C, CR, SRS");
        unmade.Code.Should().Be(4, unmade.Output);
        unmade.Error.Should().Contain("made-C9", "the gate's requirement names the arm that was never made");
        fake.Calls().Should().BeEmpty();
    }

    [Fact]
    public async Task A_finished_arm_run_again_launches_nothing_and_writes_one_arm_done()
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("C1", fake);
        Replay(fake, "result-success.jsonl");

        var first = await RunAsync(root, "C1");
        var second = await RunAsync(root, "C1");

        first.Code.Should().Be(0, first.Error);
        second.Code.Should().Be(0, second.Error);
        fake.Calls().Should().HaveCount(4, "the second run found every step done");
        root.ArmDones("C1").Should().ContainSingle("a re-run of a finished arm adds nothing; the shell appended one per run");
    }

    [Fact]
    public async Task An_srs_arm_is_refused_until_the_task_pipeline_exists()
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("SRS1", fake);
        Replay(fake, "result-success.jsonl");

        var run = await RunAsync(root, "SRS1");

        run.Code.Should().Be(4, run.Output);
        run.Error.Should().Contain("E4.S3");
        fake.Calls().Should().BeEmpty();
    }

    [Fact]
    public async Task Run_arm_prints_the_resolved_claude_and_git_before_the_first_launch()
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("C1", fake);
        Replay(fake, "result-success.jsonl");

        var run = await RunAsync(root, "C1");

        run.Code.Should().Be(0, run.Error);
        var claude = new FileInfo(Path.Combine(root.Temp, "bin", "claude")).ResolveLinkTarget(returnFinalTarget: true)!.FullName;
        var git = new FileInfo(Path.Combine(root.Temp, "bin", "git")).ResolveLinkTarget(returnFinalTarget: true)!.FullName;
        run.Output.Should().Contain("resolved executables:").And.Contain($"  claude → {claude}").And.Contain($"  git → {git}", "the REAL path of each, the one the harness launches (D17 c)");
        run.Output.IndexOf("claude → ", StringComparison.Ordinal).Should().BeLessThan(run.Output.IndexOf("[C1] 1-code", StringComparison.Ordinal), "the executables are said before the first step starts");
    }

    /// <summary>The floor at the VERB's admission, not only at <c>validate</c> (D23, E4.S0): <c>run-arm</c> without a prior <c>validate</c> (none ever ran on this root — the arm is made by hand) reads the resolved git's version before it counts <c>scenarios.json</c>, takes the gate lock or starts a step. The shim answers <c>--version</c> and logs every other call:
    /// an absent marker is "no repository operation ran". <c>Run_arm_prints_the_resolved_claude_and_git_before_the_first_launch</c> stays unedited and green for the admitted rows.</summary>
    [Theory]
    [InlineData("2.44.0", 3)]
    [InlineData("2.44.1", 0)]
    [InlineData("not a version", 3)]
    public async Task A_git_below_the_floor_is_refused_at_validate_and_at_run_arm_admission(string printed, int exit)
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("C1", fake);
        Replay(fake, "result-success.jsonl");
        var git = root.ReplaceGit(Sentinel.LogsAllButVersion(root.MarkerFile("git"), printed));

        var run = await RunAsync(root, "C1");

        run.Code.Should().Be(exit, run.Error + run.Output);
        if (exit == 0)
        {
            fake.Calls().Select(call => BriefOf(call)).Should().Equal("1-code", "2-review", "3-fix", "gate");
            return;
        }

        fake.Calls().Should().BeEmpty("no step started");
        File.Exists(root.MarkerFile("git")).Should().BeFalse("no repository operation ran: the shim logs every git but --version");
        File.Exists(root.PathsOf("C1").Timing).Should().BeFalse("nothing was timed: the arm never started");
        run.Error.Should().Contain(printed == "not a version" ? "not a version this harness can read" : GitFloor.Sentence);
        run.Error.Should().Contain(printed).And.Contain($"git at {git}: ", "the refusal names the git it is about, by its real path");
    }

    [Fact]
    public async Task A_limited_step_inside_an_arm_waits_and_the_arm_still_finishes()
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("C1", fake);
        root.Edit(top => top["limits"]!["waitSeconds"] = 1);
        Replay(fake, "result-success.jsonl", (2, ModelArmsFixtures.Path("result-429-usage-limit-reached.jsonl")));

        var run = await RunAsync(root, "C1");

        run.Code.Should().Be(0, run.Error + run.Output);
        var calls = fake.Calls();
        calls.Should().HaveCount(5);
        calls[2].After("--resume").Should().Be(calls[1].After("--session-id"), "the review resumed its own session after the limit");
        root.NotesText("C1", "notes/review.json").Trim().Should().Be(Ready, "from call 3's success");
        (await PinnedConsumer.MetricsAsync(root, "C1", Ct)).ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task Run_arm_launches_the_git_it_resolved_and_never_the_process_git()
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("C1", fake);
        Replay(fake, "result-success.jsonl");
        var shim = Path.Combine(root.Temp, "bin", "git");
        File.Delete(shim); // a link to the host's git: a write through it would be a write into the host's git
        // the arm's PATH is this folder alone, so the shim names env and grep by their paths
        root.ShimScript(shim, $"echo \"$0 $*\" >> '{root.MarkerFile("git")}'\n/usr/bin/env | /usr/bin/grep '^GIT_' >> '{root.MarkerFile("git")}'\nexec '{ExperimentRoot.RealGit}' \"$@\"\n");
        root.WithVariables(("GIT_DIR", "/nowhere"), ("GIT_WORK_TREE", "/nowhere"));

        var run = await RunAsync(root, "C1");

        run.Code.Should().Be(0, run.Error + run.Output);
        run.Output.Should().Contain($"  git → {Path.GetFullPath(shim)}", "the shim is a regular file: its real path is its own");
        var runs = File.Exists(root.MarkerFile("git")) ? File.ReadAllLines(root.MarkerFile("git")) : [];
        var asked = runs.Where(line => line.StartsWith(shim, StringComparison.Ordinal)).ToList();
        asked.Should().HaveCountGreaterThanOrEqualTo(3, "the git shim should have recorded the arm's git runs: the gate's .before, the heads and the diff");
        asked.Count(line => line.Contains("rev-parse HEAD", StringComparison.Ordinal)).Should().BeGreaterThanOrEqualTo(1, "the gate's .before");
        asked.Count(line => line.Contains("rev-parse --short HEAD", StringComparison.Ordinal)).Should().BeGreaterThanOrEqualTo(8, "every start and end line");
        asked.Count(line => line.Contains(" diff ", StringComparison.Ordinal)).Should().BeGreaterThanOrEqualTo(1, "the diff of the gate");
        var gitNames = runs.Where(line => line.StartsWith("GIT_", StringComparison.Ordinal)).Select(line => line.Split('=')[0]).Distinct().ToList();
        gitNames.Should().Contain(["GIT_NO_LAZY_FETCH", "GIT_ALLOW_PROTOCOL"]);
        gitNames.Except(["GIT_NO_LAZY_FETCH", "GIT_ALLOW_PROTOCOL"]).Where(name => !System.Text.RegularExpressions.Regex.IsMatch(name, @"^GIT_CONFIG_(COUNT|KEY_\d+|VALUE_\d+)$")).Should().BeEmpty("no GIT_* of the parent's reaches git; the rest are the harness's own driver neutralisation");
        File.ReadAllText(root.PathsOf("C1").Before("4-gate")).Trim().Should().Be(await HeadAsync(root.PathsOf("C1").CloneFolder));
    }

    private static async Task<string> HeadAsync(string clone)
    {
        var attempt = await Bench.Infrastructure.Process.ProcessRunner.RunAsync(ExperimentRoot.RealGit, ["rev-parse", "HEAD"], clone, TimeSpan.FromSeconds(30), string.Empty, null, Ct);
        return attempt.Should().BeOfType<Bench.Infrastructure.Process.ProcessAttempt.Completed>().Subject.Result.StandardOutput.Trim();
    }

    [Fact]
    public async Task The_opus_review_and_the_haiku_review_are_launched_without_the_agent_tool_fresh_and_resumed()
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("CR1", fake);
        root.Edit(top => top["limits"]!["waitSeconds"] = 1);
        var limited = ModelArmsFixtures.Path("result-429-usage-limit-reached.jsonl");
        Replay(fake, "result-success.jsonl", (2, limited), (4, limited));

        var run = await RunAsync(root, "CR1");

        run.Code.Should().Be(0, run.Error + run.Output);
        var calls = fake.Calls();
        calls.Should().HaveCount(7);
        var steps = new Dictionary<string, string> { ["1-code"] = "1-code", ["hr-review-fix"] = "2-hreview", ["2-review"] = "3-review", ["3-fix"] = "4-fix", ["gate"] = "5-gate" };
        string StepNamed(FakeCliCall call) => Named(call, calls).Split(' ') is [var brief, var how] ? $"{steps[brief]} {how}" : throw new InvalidOperationException("a call is named by its brief and how it started");
        calls.Select(StepNamed).Should().Equal("1-code fresh", "2-hreview fresh", "2-hreview resumed", "3-review fresh", "3-review resumed", "4-fix fresh", "5-gate fresh");
        foreach (var call in calls)
        {
            var name = StepNamed(call);
            call.Group("--disallowedTools").Take(2).Should().Equal(["Agent", "Task"], $"{name} starts no subagent unless its spec says so");
            call.Group("--allowedTools").Should().NotContain(["Agent", "Task"], $"{name} is not allowed one either");
        }

        calls[1].Argv.Should().Contain("--model").And.Contain(fake.Model, "the bench rewrites every model to the fake's id");
    }

    [Fact]
    public async Task A_sequential_arm_leaves_the_files_the_example_binds_and_the_consumers_read_them()
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("C1", fake);
        Replay(fake, "result-success.jsonl");

        var run = await RunAsync(root, "C1");

        run.Code.Should().Be(0, run.Error + run.Output);
        var paths = root.PathsOf("C1");
        foreach (var step in StepsOf("C"))
        {
            File.Exists(paths.Prompt(step, 1)).Should().BeTrue($"{step}'s prompt");
            File.Exists(paths.Stream(step, 1)).Should().BeTrue($"{step}'s stream");
            File.Exists(paths.Session(step)).Should().BeTrue($"{step}'s session id");
        }

        var metrics = await PinnedConsumer.MetricsAsync(root, "C1", Ct);
        metrics.ExitCode.Should().Be(0, metrics.Error);
        var report = metrics.Report!["steps"]!.AsObject();
        report.Select(step => step.Key).Should().Equal(StepsOf("C"), "the consumer names every step of the arm, in the order they started");
        foreach (var step in StepsOf("C"))
        {
            var attempts = report[step]!.AsArray();
            attempts.Should().HaveCount(1);
            attempts[0]!["complete"]!.GetValue<bool>().Should().BeTrue($"{step} ended in a result");
        }
    }

    [Theory]
    [InlineData("C", "C1", "2-review", 2)]
    [InlineData("CR", "CR1", "2-hreview", 2)]
    [InlineData("CHH", "CHH1", "4-hfix", 4)]
    public async Task An_arm_interrupted_mid_step_is_resumed_by_the_next_run_arm_and_completes_for_every_sequential_scheme(string scheme, string arm, string step, int call)
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync(arm, fake);
        root.Edit(top => top["limits"]!["minimumRemainingSeconds"] = 1);
        Replay(fake, "result-success.jsonl", (call, ModelArmsFixtures.Path("stream-no-result.jsonl")));
        var paths = root.PathsOf(arm);
        var steps = StepsOf(scheme);
        var earlier = steps.TakeWhile(name => name != step).ToList();

        var first = await RunAsync(root, arm);

        first.Code.Should().Be(5, first.Error + first.Output);
        first.Output.Should().Contain($"{step} INTERRUPTED");
        fake.Calls().Should().HaveCount(call, "run-arm does not repeat an exit 5 by itself: a relaunch of a verdictless attempt costs a paid attempt");
        earlier.Should().OnlyContain(name => File.Exists(paths.Done(name)), "every earlier step is done");
        File.Exists(paths.Done(step)).Should().BeFalse("an interrupted attempt is not done");
        File.ReadAllText(paths.Session(step)).Trim().Should().Be(fake.Calls()[call - 1].After("--session-id"));
        root.TimingOf(arm).Last().Should().BeOfType<TimingLine.Stopped>().Which.Exit.Should().Be(5);
        NothingDerivedYet(root, arm, scheme);
        var session = File.ReadAllText(paths.Session(step)).Trim();
        if (scheme == "CHH")
        {
            await root.CommitInCloneAsync(arm, "work.txt", "hfix's work\n");
        }

        var second = await RunAsync(root, arm);

        second.Code.Should().Be(0, second.Error + second.Output);
        var calls = fake.Calls();
        calls[call].After("--resume").Should().Be(session, "the next run resumes the cut session");
        var notice = ResumeNotice.For(root.ExperimentOf().Target.Base.Value, "@@").Split("@@");
        calls[call].Prompt.Should().StartWith(notice[0]).And.EndWith(notice[1], "a resume's prompt IS the resume notice, with the deadline of what is left, not the brief again");
        calls[call].Group("--allowedTools").Should().Equal(calls[call - 1].Group("--allowedTools"), "a resume is never wider than the launch it continues: the same allowed tools");
        calls[call].Group("--disallowedTools").Should().Equal(calls[call - 1].Group("--disallowedTools"), "and the same denied tools, Agent and Task first");
        var later = steps.Count - earlier.Count - 1;
        calls.Should().HaveCount(call + 1 + later, "one resume of the cut step, then one fresh launch for each step after it — the earlier steps launched nothing again");
        calls.Skip(call + 1).Should().OnlyContain(each => each.Argv.Contains("--session-id"), "the steps after the cut one start fresh");
        calls.Count(each => each.Argv.Contains("--resume")).Should().Be(1, "exactly one resume: the interrupted step's");
        steps.Should().OnlyContain(name => File.Exists(paths.Done(name)), "every step is done");
        File.ReadAllText(paths.Done(step)).Should().Contain("\"attempts\":2");
        var lines = root.TimingOf(arm).Where(line => line is TimingLine.Start or TimingLine.End or TimingLine.Stopped).Where(line => StepOf(line) == step).ToList();
        lines.Select(line => line.GetType().Name).Should().Equal("Start", "End", "Stopped", "Start", "End");
        var (a1, a1End, a2, a2End) = (lines[0].As<TimingLine.Start>(), lines[1].As<TimingLine.End>(), lines[3].As<TimingLine.Start>(), lines[4].As<TimingLine.End>());
        a1End.State.Should().Be("interrupted");
        a2.How.Should().Be("resume");
        a2.BudgetSeconds.Should().BeLessThanOrEqualTo(Math.Max(1, 60 - (int)a1End.WallSeconds.Value), "a resume gets what is left of the minute, never more");
        a2End.State.Should().Be("finished");
        root.ArmDones(arm).Should().ContainSingle();
        AfterResume(root, arm, scheme);

        var metrics = await PinnedConsumer.MetricsAsync(root, arm, Ct);

        metrics.ExitCode.Should().Be(0, metrics.Error);
        var report = metrics.Report!["steps"]!.AsObject();
        report.Select(entry => entry.Key).Should().Equal(steps, "the consumer names every step of the scheme");
        var attempts = report[step]!.AsArray();
        attempts.Should().HaveCount(2);
        attempts[0]!["complete"]!.GetValue<bool>().Should().BeFalse("the cut attempt had no result");
        attempts[0]!["cost"].Should().BeNull("an attempt without a result has no host cost — never guessed");
        attempts[1]!["complete"]!.GetValue<bool>().Should().BeTrue();
    }

    /// <summary>What the cut step would have derived, absent: nothing is derived from an attempt that did not end (C's verdict, CR's diff of the haiku review, CHH's diff of the fix).</summary>
    private static void NothingDerivedYet(ExperimentRoot root, string arm, string scheme)
    {
        var paths = root.PathsOf(arm);
        var derived = scheme switch
        {
            "C" => Path.Combine(paths.Folder, "notes", "review.json"),
            "CR" => paths.Diff("2-hreview"),
            _ => paths.Diff("4-hfix"),
        };
        File.Exists(derived).Should().BeFalse($"{derived} is derived from an attempt that did not end");
    }

    private static string StepOf(TimingLine line) =>
        line switch
        {
            TimingLine.Start start => start.Step,
            TimingLine.End end => end.Step,
            TimingLine.Stopped stopped => stopped.Step,
            _ => string.Empty,
        };

    private static void AfterResume(ExperimentRoot root, string arm, string scheme)
    {
        var paths = root.PathsOf(arm);
        switch (scheme)
        {
            case "C":
                root.NotesText(arm, "notes/review.json").Trim().Should().Be(Ready, "from call 3's result");
                break;
            case "CR":
                File.Exists(paths.Diff("2-hreview")).Should().BeTrue();
                break;
            default:
                File.ReadAllText(paths.Diff("4-hfix")).Should().Contain("work.txt", "the fix's diff is from the first review's snapshot, spanning three steps")
                    .And.Be(DiffFrom(root, arm, File.ReadAllText(paths.Before("2-hA")).Trim()).GetAwaiter().GetResult());
                break;
        }
    }

    private static async Task<string> DiffFrom(ExperimentRoot root, string arm, string from)
    {
        var attempt = await Bench.Infrastructure.Process.ProcessRunner.RunAsync(
            ExperimentRoot.RealGit, ["diff", "--no-ext-diff", from, "HEAD"], root.PathsOf(arm).CloneFolder, TimeSpan.FromSeconds(30), string.Empty, null, Ct);
        return attempt.Should().BeOfType<Bench.Infrastructure.Process.ProcessAttempt.Completed>().Subject.Result.StandardOutput;
    }

    [Fact]
    public async Task A_PATH_entry_under_the_experiment_root_stops_run_arm_with_exit_4_before_any_launch()
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("C1", fake);
        Replay(fake, "result-success.jsonl");
        root.UsePath(Path.Combine(root.Temp, "bin") + Path.PathSeparator + Path.Combine(root.Root, "tools"));

        var run = await RunAsync(root, "C1");

        run.Code.Should().Be(4, run.Output);
        run.Error.Should().Contain("PATH entry").And.Contain(Path.Combine(root.Root, "tools"));
        run.Output.Should().NotContain("resolved executables:");
        fake.Calls().Should().BeEmpty();
    }

    [Fact]
    public async Task A_claude_that_is_not_on_PATH_stops_run_arm_with_exit_3_naming_it()
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("C1", fake);
        File.Delete(Path.Combine(root.Temp, "bin", "claude"));

        var run = await RunAsync(root, "C1");

        run.Code.Should().Be(3, run.Output);
        run.Error.Should().Contain("claude");
        fake.Calls().Should().BeEmpty();
    }

    [Fact]
    public async Task An_exit_5_is_never_repeated_by_run_arm_itself()
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("C1", fake);
        Replay(fake, "stream-no-result.jsonl");

        var run = await RunAsync(root, "C1");

        run.Code.Should().Be(5, run.Error + run.Output);
        fake.Calls().Should().ContainSingle("a relaunch of a verdictless attempt costs one paid attempt (E2 §8 item 56): the operator decides, the verb does not");
        run.Error.Should().Contain("model-arms run-arm C1:").And.Contain("1-code INTERRUPTED");
        root.ArmDones("C1").Should().BeEmpty();
    }

    [Fact]
    public async Task The_gate_and_the_runner_share_one_clock_one_log_and_one_map()
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("C1", fake);
        Replay(fake, "result-success.jsonl");
        root.Edit(top => top["limits"]!["gateTries"] = 1);
        var token = root.PathsOf("C1").Token;
        Directory.CreateDirectory(Path.Combine(Folder(root, "C1"), "notes"));
        File.WriteAllText(Path.Combine(Folder(root, "C1"), "notes", "gate.json"), $$"""{"status":"blocked","why":"see r/{{token}}/arm"}""");
        var instant = new DateTimeOffset(2026, 10, 28, 21, 32, 38, TimeSpan.Zero);
        var waits = new RecordingWaits();
        var seams = new ModelArmsApp.Verbs.RunArmSeams(new BenchClock(instant, TimeZoneInfo.Utc), waits.Wait, null);
        var output = new StringWriter();
        var input = new ModelArmsApp.VerbInput("run-arm", ["C1"], CommandLine.Parse(["--experiment", root.Root]), output, new StringWriter(), root.Environment, ModelArmsApp.ModuleFiles.Beside(AppContext.BaseDirectory), Ct);

        var code = await ModelArmsApp.ExperimentGate.RunAsync(input, admitted => ModelArmsApp.Verbs.RunArmVerb.RunAsync(admitted, seams), ModelArmsApp.GateSeams.Real);

        code.Should().Be(0, output.ToString());
        var wait = root.TimingOf("C1").OfType<TimingLine.GateWait>().Should().ContainSingle().Subject;
        wait.AtUtc.Should().Be(instant, "the gate stamps its own lines with the clock the runner has");
        waits.Recorded.Should().Equal([TimeSpan.FromSeconds(1800)], "the gate's wait went through the wait the verb was given");
        output.ToString().Should().Contain("<token>", "the gate's log line went through the arm map").And.NotContain(token, "no token reaches the log, whichever step wrote the line");
        output.ToString().Should().Contain("the gate was blocked", "the gate writes to the verb's one log");
    }

    [Theory]
    [InlineData("HOME", "root")]
    [InlineData("XDG_CONFIG_HOME", "target")]
    public async Task A_config_folder_under_the_experiment_root_or_the_target_stops_run_arm_with_exit_4_before_any_launch(string variable, string under)
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("C1", fake);
        Replay(fake, "result-success.jsonl");
        var folder = under == "root" ? Path.Combine(root.Root, "h") : Path.Combine(root.Temp, "example", "target", "x");
        root.WithVariables((variable, folder));

        var run = await RunAsync(root, "C1");

        run.Code.Should().Be(4, run.Output + run.Error);
        run.Error.Should().Contain(variable).And.Contain(folder, "git reads its global config from there, and a claude its settings: an arm could write a file there (D26 c)");
        run.Output.Should().NotContain("resolved executables:", "nothing is printed once admission refuses");
        fake.Calls().Should().BeEmpty();
        root.TimingOf("C1").Should().BeEmpty("nothing was launched, not even a git");
    }

    [Fact]
    public async Task A_finished_arm_whose_rerun_is_stopped_at_the_busy_gate_lock_keeps_exactly_one_arm_done()
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("C1", fake);
        Replay(fake, "result-success.jsonl");
        (await RunAsync(root, "C1")).Code.Should().Be(0);
        using var stopping = new CancellationTokenSource();
        var seams = new ModelArmsApp.Verbs.RunArmSeams(TimeProvider.System, (span, ct) => Task.Delay(span, ct), new BusyGateLock());

        var stopped = await RunWithSeamsAsync(root, "C1", seams, new StopOnLine("waiting at most", stopping), stopping.Token);

        stopped.Should().Be(5, "the operator's stop while the gate waits for a lock somebody else holds is still exit 5");
        root.TimingOf("C1").Last().Should().BeOfType<TimingLine.ArmDone>("the arm stays done: a stop while waiting for the lock of a gate that is final writes no stopped line");
        var again = await RunAsync(root, "C1");

        again.Code.Should().Be(0, again.Error);
        root.ArmDones("C1").Should().ContainSingle("a rerun of a finished arm adds nothing, whatever a stop left after the arm-done");
        fake.Calls().Should().HaveCount(4);
    }

    [Fact]
    public async Task A_stop_during_the_gates_finalization_diff_is_a_stopped_line_because_the_arm_is_not_yet_done()
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("C1", fake);
        Replay(fake, "result-success.jsonl");
        (await RunAsync(root, "C1")).Code.Should().Be(0);
        var paths = root.PathsOf("C1");
        File.Delete(paths.Diff("4-gate"));
        File.WriteAllLines(paths.Timing, File.ReadAllLines(paths.Timing).Where(line => !line.Contains("\"arm-done\"", StringComparison.Ordinal)));
        var shim = Path.Combine(root.Temp, "bin", "git");
        File.Delete(shim); // a link to the host's git: a write through it would be a write into the host's git
        var hit = root.MarkerFile("diff");
        root.ShimScript(shim, $"case \" $* \" in *\" diff \"*) echo hit >> '{hit}'; /usr/bin/sleep 20;; esac\nexec '{ExperimentRoot.RealGit}' \"$@\"\n");
        using var stopping = new CancellationTokenSource();
        var watcher = Task.Run(
            async () =>
            {
                while (!File.Exists(hit))
                {
                    await Task.Delay(50, Ct);
                }

                await stopping.CancelAsync();
            },
            Ct);

        var stopped = await RunAsync(root, "C1", stopping.Token);
        await watcher;

        stopped.Code.Should().Be(5, stopped.Error);
        File.Exists(paths.Diff("4-gate")).Should().BeFalse("the stop came before the diff was written");
        root.TimingOf("C1").Last().Should().BeOfType<TimingLine.Stopped>("the arm is unfinished — no diff, no arm-done — so the last line must say a stop happened (D16)");
        root.ArmDones("C1").Should().BeEmpty();
    }

    [Fact]
    public async Task An_arm_in_no_run_sizes_the_gate_lock_ceiling_from_the_arms_of_the_map_and_says_so()
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("C4", fake);
        await root.MakeArmAsync("C5", fake);
        Replay(fake, "result-success.jsonl");
        using var stopping = new CancellationTokenSource();
        var output = new StopOnLine("waiting at most", stopping);
        var seams = new ModelArmsApp.Verbs.RunArmSeams(TimeProvider.System, (span, ct) => Task.Delay(span, ct), new BusyGateLock());

        var code = await RunWithSeamsAsync(root, "C4", seams, output, stopping.Token);

        code.Should().Be(5);
        output.ToString().Should().Contain("C4 is in no run").And.Contain("2 arms of arms.map.json", "the fallback is said in the log");
        output.ToString().Should().Contain("waiting at most 811 min", "one arm ahead: 1 minute + 600 s + 40 × 900 s + 40 × 300 s = 48 660 s");
    }

    [Fact]
    public async Task A_stopped_line_after_arm_done_does_not_earn_the_arm_a_second_arm_done()
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("C1", fake);
        Replay(fake, "result-success.jsonl");
        (await RunAsync(root, "C1")).Code.Should().Be(0);
        File.AppendAllText(root.PathsOf("C1").Timing, TimingLines.Write(new TimingLine.Stopped("C1", "4-gate", 5, "stopped by the operator", DateTimeOffset.UtcNow)) + "\n");

        var again = await RunAsync(root, "C1");

        again.Code.Should().Be(0, again.Error);
        root.ArmDones("C1").Should().ContainSingle("the dedupe is 'no arm-done after the last start or gate-wait line', not 'the last line is not an arm-done'");
    }

    [Fact]
    public async Task The_gate_lock_ceiling_is_the_one_sized_from_the_queue_and_not_the_default_six_hours()
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("C1", fake);
        await root.MakeArmAsync("C2", fake);
        await root.MakeArmAsync("C3", fake);
        await root.MakeArmAsync("C4", fake); // made, named by no run: it is not queued behind C1's round
        Replay(fake, "result-success.jsonl");
        using var stopping = new CancellationTokenSource();
        var output = new StopOnLine("waiting at most", stopping);
        var seams = new ModelArmsApp.Verbs.RunArmSeams(TimeProvider.System, (span, ct) => Task.Delay(span, ct), new BusyGateLock());

        var code = await RunWithSeamsAsync(root, "C1", seams, output, stopping.Token);

        code.Should().Be(5, "the operator's stop while the gate waits for its lock");
        output.ToString().Should().Contain("waiting at most 1622 min", "C1 is in a round of three in the example's run part6 (a fourth arm is made but named by no run): two can queue ahead of this one, each for the gate's 1 minute (the fixture) plus the 600 s kill grace plus 40 × 900 s of limit waits and 40 × 300 s of resume floors it may spend under the lock");
        output.ToString().Should().NotContain("is in no run", "the arm is named by a run: the size of its round is what counts");
        fake.Calls().Select(call => BriefOf(call)).Should().Equal("1-code", "2-review", "3-fix");
    }
}
