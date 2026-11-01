using System.Text.Json.Nodes;
using Bench.Domain.Trace;
using Bench.Tests.Probes.Driver;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Steps;
using ModelArms.Core.Tasks;
using ModelArms.Verbs;
using Xunit;
using static Bench.Tests.ModelArms.RunArmKit;

namespace Bench.Tests.ModelArms;

/// <summary>Model arms E4.S3 — <c>run-arm</c> for a scheme with a <c>tasks</c> block (D14, D25, D21), through the console's own entry point over <see cref="SrsKit"/>: a MADE arm (a real clone with the rules submodule and the target's wrapper), the fake CLI as <c>claude</c> behind a wrapper that commits one file per task — the split, one fix round, the batches
/// fixed once, the tasks of a batch at once in their own worktrees, the pick-back, a stop that waits for every sibling, a re-entry that resumes only the interrupted task, and the task coders' stems in <c>stepsOnDeadline</c> and <c>status</c>.
/// <para>
/// What this does NOT prove: a real claude splitting a real plan and coding a real task (the fake replays, the wrapper commits one file); a cherry-pick of a real multi-hour task; an LFS target's worktree; Windows; a git other than the host's.
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class RunArmSrsTests
{
    private const string Success = "result-success.jsonl";
    private const string NoResult = "stream-no-result.jsonl";
    private const string Limited = "result-429-usage-limit-reached.jsonl";

    /// <summary>A healthy sibling is still streaming (three lines, this long between them) when the task that stops has already ended: what a cancel of siblings would cut.</summary>
    private const int Slow = 400;

    private static int CountOf(string text, string needle) => text.Split(needle).Length - 1;

    private static string Briefs(IEnumerable<FakeCliCall> calls) => string.Join(", ", calls.Select(BriefOf));

    /// <summary>Run 1 of an interrupted batch: <c>[[t1 t2 t3 t4], [t5]]</c>, t2's coder ends with no result. Returns the kit's calls so far.</summary>
    private static async Task<ArmRun> InterruptedAsync(SrsKit kit)
    {
        kit.Plan(kit.Five());
        kit.Replay(Success);
        kit.ReplayFor("t2", NoResult);
        foreach (var id in new[] { "t1", "t3", "t4" })
        {
            kit.ReplayFor(id, Success, lineDelayMs: Slow);
        }

        var first = await kit.RunAsync();
        first.Code.Should().Be(5, first.Error + first.Output);
        return first;
    }

    [Fact]
    public async Task A_refused_split_gets_one_fix_round_then_stops_with_exit_4()
    {
        using var kit = await SrsKit.CreateAsync();
        kit.Plan(kit.Five().Take(3));
        kit.Replay(Success);

        var run = await kit.RunAsync();

        run.Code.Should().Be(4, run.Error + run.Output);
        run.Error.Should().Contain("still invalid").And.Contain("there are 3 tasks");
        var calls = kit.Fake.Calls();
        Briefs(calls).Should().Be("x-split, x-split-fix", "the split, then ONE fix round, and no coder");
        calls.Select(HowOf).Should().Equal("fresh", "fresh");
        calls[1].Prompt.Should().Contain("- there are 3 tasks; the brief asks for 4 to 7", "the fix is told the checker's own sentences");
        File.ReadAllText(kit.Paths.TasksPlanLog).Should().Contain("- there are 3 tasks; the brief asks for 4 to 7");
        File.Exists(kit.Paths.BatchesJson).Should().BeFalse("batches.json is written only from a list the check passed");
        CountOf(run.Output, "one fix round").Should().Be(1, "the line is printed once, when the fix round starts — not again by the check that follows it");

        var again = await kit.RunAsync();

        again.Code.Should().Be(4, again.Error);
        again.Error.Should().Contain("still invalid");
        again.Output.Should().NotContain("one fix round", "the round is spent: no fix runs, so no line says one will");
        kit.Fake.Calls().Should().HaveCount(2, "the round is spent: a re-entry whose split and fix are both done launches nothing");
    }

    [Fact]
    public async Task A_split_fix_that_repairs_the_list_lets_the_batches_run()
    {
        using var kit = await SrsKit.CreateAsync();
        kit.Plan(kit.Five().Take(3));
        kit.Stage(kit.Five());
        kit.Replay(Success);

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error + run.Output);
        var calls = kit.Fake.Calls();
        Briefs(calls.Take(2)).Should().Be("x-split, x-split-fix");
        calls.Count(call => call.Prompt.Contains("ONE task, **", StringComparison.Ordinal)).Should().Be(5, "the repaired list's five tasks ran");
        File.ReadAllText(kit.Paths.TasksPlanLog).Trim().Should().Be("ok: 5 tasks in 2 batches: [t1 t2 t3 t4] → [t5]");
        BatchesFile.Read(File.ReadAllText(kit.Paths.BatchesJson), kit.Paths.Work).Ok().Batches.Select(batch => string.Join(' ', batch)).Should().Equal("t1 t2 t3 t4", "t5");
        File.Exists(kit.Paths.Done("1b-split-fix")).Should().BeTrue();
    }

    [Fact]
    public async Task A_batch_of_many_runs_its_tasks_at_once_each_in_its_worktree()
    {
        using var kit = await SrsKit.CreateAsync();
        kit.Plan(kit.Five());
        kit.Replay(Success);
        foreach (var id in new[] { "t1", "t2", "t3", "t4" })
        {
            kit.ReplayFor(id, Success, lineDelayMs: 1000);
        }

        var armBefore = await kit.HeadAsync(kit.Clone);

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error + run.Output);
        BatchesFile.Read(File.ReadAllText(kit.Paths.BatchesJson), kit.Paths.Work).Ok().Batches.Select(batch => string.Join(' ', batch)).Should().Equal("t1 t2 t3 t4", "t5");
        var coders = kit.Coders();
        coders.Should().HaveCount(5);
        foreach (var id in new[] { "t1", "t2", "t3", "t4" })
        {
            coders.Single(call => call.Prompt.Contains($"ONE task, **{id}**", StringComparison.Ordinal)).Cwd.Should().Be(kit.Paths.Worktree(id), $"{id} ran in its own worktree");
        }

        coders.Single(call => call.Prompt.Contains("ONE task, **t5**", StringComparison.Ordinal)).Cwd.Should().Be(kit.Clone, "a batch of one runs in the clone itself, with no worktree");
        var timing = kit.Timing();
        var stems = new[] { "2-t1-code", "2-t2-code", "2-t3-code", "2-t4-code" };
        var firstEnd = timing.Select((line, index) => (Line: line, Index: index)).Where(each => each.Line is TimingLine.End end && stems.Contains(end.Step)).Min(each => each.Index);
        foreach (var stem in stems)
        {
            timing.Select((line, index) => (Line: line, Index: index)).Single(each => each.Line is TimingLine.Start start && start.Step == stem).Index
                .Should().BeLessThan(firstEnd, $"{stem} started before any of the four ended: the tasks of a batch run at once");
        }

        (await kit.SubjectsAsync(armBefore)).Where(subject => subject.StartsWith("task t", StringComparison.Ordinal)).Should().Equal(["task t1", "task t2", "task t3", "task t4", "task t5"], "carried back in task order");
        (await kit.TrackedAsync(kit.Clone)).Should().Contain(["t1.txt", "t2.txt", "t3.txt", "t4.txt", "t5.txt"]);
        foreach (var id in new[] { "t1", "t2", "t3", "t4" })
        {
            File.Exists(kit.Paths.Picked(id)).Should().BeTrue($"{id} was picked back");
            Directory.Exists(kit.Paths.Worktree(id)).Should().BeFalse($"{id}'s worktree was dropped");
        }

        File.Exists(kit.Paths.Picked("t5")).Should().BeFalse("a batch of one is never picked");
        (await kit.TmpBranchesAsync()).Should().BeEmpty();
        File.Exists(kit.Paths.BatchDone(0)).Should().BeTrue();
        File.Exists(kit.Paths.BatchDone(1)).Should().BeTrue();
    }

    [Fact]
    public async Task The_task_coders_brief_carries_its_task_its_brief_its_files_and_its_notes()
    {
        using var kit = await SrsKit.CreateAsync();
        kit.Plan(kit.Five());
        kit.Replay(Success);
        var armBefore = await kit.HeadAsync(kit.Clone);

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error + run.Output);
        var t1 = kit.Coders().Single(call => call.Prompt.Contains("ONE task, **t1**", StringComparison.Ordinal)).Prompt;
        t1.Should().Contain("**t1**").And.Contain($"{kit.Paths.Work}/t1.md", "the task's brief by its absolute path")
            .And.Contain("`src/A.cs`, `src/ATests.cs`", "TASK_FILES is each file in back quotes joined by a comma, as the shell spelled it")
            .And.Contain(kit.Paths.TaskNotes("t1"), "the coder's notes are notes/<id>-code.md");
        File.ReadAllText(kit.Paths.TaskBefore("t1")).Should().Be(armBefore + "\n", "the .before is the HEAD the batch started from, written before the coder's launch (the wrapper's commit comes after it)");
        var t5 = kit.Coders().Single(call => call.Prompt.Contains("ONE task, **t5**", StringComparison.Ordinal)).Prompt;
        t5.Should().Contain("`src/E.cs`, `todo/PLAN_a_spent_quota_cools_the_model_down.md`");
        File.ReadAllText(kit.Paths.TaskBefore("t5")).Trim().Should().NotBe(armBefore, "t5's batch started from the arm branch with the first batch carried back");
    }

    [Fact]
    public async Task An_interrupted_task_stops_the_arm_and_reentry_resumes_only_it()
    {
        using var kit = await SrsKit.CreateAsync();

        var first = await InterruptedAsync(kit);

        (first.Output + first.Error).Should().Contain("a task of batch 0 was interrupted").And.Contain("run me again");
        foreach (var id in new[] { "t1", "t3", "t4" })
        {
            File.Exists(kit.Paths.Done($"2-{id}-code")).Should().BeTrue($"{id} was not cancelled because t2 stopped: a healthy sibling is left to finish");
        }

        File.Exists(kit.Paths.Done("2-t2-code")).Should().BeFalse();
        foreach (var id in new[] { "t1", "t2", "t3", "t4" })
        {
            File.Exists(kit.Paths.Picked(id)).Should().BeFalse("nothing is carried back from a batch that stopped");
        }

        File.Exists(kit.Paths.BatchDone(0)).Should().BeFalse();
        var tip = (await kit.GitAsync(kit.Clone, "rev-parse", "refs/heads/tmp/t2")).Trim();
        (await kit.TrackedAsync(kit.Clone, "tmp/t2")).Should().Contain("t2.txt", "the wrapper's fresh commit is on t2's branch");
        var session = File.ReadAllText(kit.Paths.Session("2-t2-code")).Trim();
        var callsBefore = kit.Fake.Calls().Count;
        var wrapperBefore = kit.Wrapper().Count;
        kit.Replay(Success);

        var second = await kit.RunAsync();

        second.Code.Should().Be(0, second.Error + second.Output);
        var later = kit.Fake.Calls().Skip(callsBefore).ToList();
        var batchZero = later.Where(call => call.Cwd.StartsWith(kit.Paths.Worktrees, StringComparison.Ordinal)).ToList();
        batchZero.Should().ContainSingle("only the interrupted task launches again — batch 0's coders are exactly one");
        batchZero[0].After("--resume").Should().Be(session, "the SAME session, run 1's --session-id for t2");
        batchZero[0].Cwd.Should().Be(kit.Paths.Worktree("t2"), "in its kept worktree (ledger 3.1), not a new one");
        var resumed = kit.Wrapper().Skip(wrapperBefore).Single(call => call.Cwd == kit.Paths.Worktree("t2"));
        (resumed.How, resumed.Head, resumed.Has).Should().Be(("resume", tip, "yes"), "its commit and its files were still there when the session went on");
        later.Where(call => call.Prompt.Contains("ONE task, **t5**", StringComparison.Ordinal)).Should().ContainSingle("batch 1 runs: t5 depends on every other task").Which.Cwd.Should().Be(kit.Clone);
        foreach (var id in new[] { "t1", "t2", "t3", "t4" })
        {
            File.Exists(kit.Paths.Picked(id)).Should().BeTrue($"{id} was carried back");
        }

        (await kit.GitAsync(kit.Clone, "log", "--format=%h", "--", "t2.txt")).Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().ContainSingle("ONE commit adds t2.txt to the arm branch");
    }

    [Fact]
    public async Task Every_task_coder_is_launched_without_the_agent_tool_fresh_and_resumed()
    {
        using var kit = await SrsKit.CreateAsync();
        await InterruptedAsync(kit);
        kit.Replay(Success);
        (await kit.RunAsync()).Code.Should().Be(0);

        var coders = kit.Fake.Calls().Where(call => call.Prompt.Contains("ONE task, **", StringComparison.Ordinal) || call.Argv.Contains("--resume")).ToList();

        coders.Count(call => call.Argv.Contains("--resume")).Should().BeGreaterThanOrEqualTo(1, "the resume of the interrupted coder is among them");
        coders.Count(call => call.Argv.Contains("--session-id")).Should().BeGreaterThanOrEqualTo(5, "and the fresh launches of the five coders");
        foreach (var call in coders)
        {
            call.Group("--disallowedTools").Take(2).Should().Equal(["Agent", "Task"], $"a coder ({HowOf(call)}) starts no subagent unless its spec says so");
            call.Group("--allowedTools").Should().NotContain(["Agent", "Task"]);
        }
    }

    [Fact]
    public async Task The_task_pipelines_steps_are_the_three_the_tasks_block_names()
    {
        using var kit = await SrsKit.CreateAsync();
        kit.Edit(top =>
        {
            var steps = top["steps"]!.AsObject();
            foreach (var (from, to, minutes) in new[] { ("split", "cut", 2), ("splitFix", "cutFix", 3), ("taskCode", "coder", 4) })
            {
                steps.Remove(from, out var spec);
                spec!["minutes"] = minutes;
                steps[to] = spec;
                top["tasks"]![from] = to;
            }
        });
        kit.Plan(kit.Five().Take(3));
        kit.Stage(kit.Five());
        kit.Replay(Success);

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error + run.Output);
        var starts = kit.Timing().OfType<TimingLine.Start>().ToList();
        starts.First(start => start.Step == "1-split").BudgetSeconds.Should().Be(120, "the step the block calls split is 'cut' here: 2 minutes");
        starts.First(start => start.Step == "1b-split-fix").BudgetSeconds.Should().Be(180);
        starts.First(start => start.Step == "2-t1-code").BudgetSeconds.Should().Be(240);
        var calls = kit.Fake.Calls();
        BriefOf(calls[0]).Should().Be("x-split");
        BriefOf(calls[1]).Should().Be("x-split-fix");
        BriefOf(calls.First(call => call.Prompt.Contains("ONE task, **", StringComparison.Ordinal))).Should().Be("t2-coder", "each step's brief is the one its renamed spec names");
    }

    [Fact]
    public async Task A_batch_whose_tasks_stop_differently_ends_with_the_most_severe_code_after_every_sibling_ended()
    {
        using var kit = await SrsKit.CreateAsync();
        kit.Plan(kit.Five());
        kit.Replay(Success);
        kit.ReplayFor("t1", Limited, resumedFixture: Limited);
        kit.ReplayFor("t2", NoResult);
        kit.ReplayFor("t3", Success, lineDelayMs: Slow);
        kit.ReplayFor("t4", Success, lineDelayMs: Slow);

        var run = await kit.RunAsync();

        run.Code.Should().Be(3, "a limit still on after its one wait (the resume replays the limit too) is the host (3), an interruption only 'run me again' (5): 3 outranks 5");
        foreach (var id in new[] { "t3", "t4" })
        {
            File.Exists(kit.Paths.Done($"2-{id}-code")).Should().BeTrue($"{id} ended healthy: no sibling was cancelled because another stopped");
        }

        var said = run.Output + run.Error;
        said.Should().Contain("2-t1-code stopped (exit 3)").And.Contain("2-t2-code stopped (exit 5)", "every stopped task of the batch is named");
        File.Exists(kit.Paths.BatchDone(0)).Should().BeFalse();
    }

    [Theory]
    [InlineData("a task gains a dependency: still valid, other batches")]
    [InlineData("a task drops a dependency: invalid")]
    public async Task Tasks_json_changed_after_the_batches_were_fixed_stops_the_arm_with_exit_4(string row)
    {
        using var kit = await SrsKit.CreateAsync();
        await InterruptedAsync(kit);
        var fixedText = File.ReadAllText(kit.Paths.BatchesJson);
        var tasks = kit.Five().ToList();
        var gained = row.Contains("gains", StringComparison.Ordinal);
        tasks[gained ? 3 : 4] = gained ? tasks[3] with { Depends = ["t1"] } : tasks[4] with { Depends = ["t1", "t2", "t3"] };
        File.WriteAllText(kit.Paths.TasksJson, SrsKit.TasksText(tasks));
        var callsBefore = kit.Fake.Calls().Count;

        var second = await kit.RunAsync();

        second.Code.Should().Be(4, second.Error + second.Output);
        second.Error.Should().Contain("work/tasks.json").And.Contain("work/batches.json").And.Contain("changed after");
        second.Output.Should().NotContain("one fix round", "batches.json exists and no fix follows: the line would promise a round that never runs");
        kit.Fake.Calls().Should().HaveCount(callsBefore, "no launch: not a coder, and in the invalid row not the split fix either — the comparison stands before the fix round");
        File.ReadAllText(kit.Paths.BatchesJson).Should().Be(fixedText, "batches.json is written once and never rewritten");
        var log = File.ReadAllText(kit.Paths.TasksPlanLog);
        if (gained)
        {
            log.Should().StartWith("ok: 5 tasks in 3 batches: [t1 t2 t3] → [t4] → [t5]", "the new list is VALID — its batches are not the fixed ones");
        }
        else
        {
            log.Should().Contain("t5 owns the plan but does not depend on t4", "the new list is invalid, and said so");
        }
    }

    [Fact]
    public async Task An_interrupted_split_fix_whose_list_already_passes_is_resumed_before_the_batches()
    {
        using var kit = await SrsKit.CreateAsync();
        kit.Plan(kit.Five().Take(3));
        kit.Stage(kit.Five());
        kit.Replay(Success);
        kit.ReplayFix(NoResult);
        var first = await kit.RunAsync();
        first.Code.Should().Be(5, "the fix wrote the repaired list and was cut before its result line: " + first.Error + first.Output);
        File.Exists(kit.Paths.Done("1b-split-fix")).Should().BeFalse();
        File.Exists(kit.Paths.BatchesJson).Should().BeFalse();
        var session = File.ReadAllText(kit.Paths.Session("1b-split-fix")).Trim();
        var callsBefore = kit.Fake.Calls().Count;

        var second = await kit.RunAsync();

        second.Code.Should().Be(0, second.Error + second.Output);
        var later = kit.Fake.Calls().Skip(callsBefore).ToList();
        second.Output.Should().Contain("one fix round", "the fix runs again (resumed) with a list that passes: the line says so as well");
        later[0].After("--resume").Should().Be(session, "the list passes already, but the fix began and is not done: its session is resumed first, before any batch");
        later[0].Cwd.Should().Be(kit.Clone);
        File.Exists(kit.Paths.Done("1b-split-fix")).Should().BeTrue();
        later.Count(call => call.Prompt.Contains("ONE task, **", StringComparison.Ordinal)).Should().Be(5);
    }

    [Fact]
    public async Task A_batches_json_that_holds_no_batch_is_refused_with_exit_4_naming_it()
    {
        using var kit = await SrsKit.CreateAsync();
        kit.Plan(kit.Five());
        kit.Replay(Success);
        Directory.CreateDirectory(kit.Paths.Work);
        File.WriteAllText(kit.Paths.BatchesJson, """{"batches":[],"tasks":{}}""");

        var run = await kit.RunAsync();

        run.Code.Should().Be(4, "no batch is not 'every batch is done': " + run.Error + run.Output);
        run.Error.Should().Contain("work/batches.json").And.Contain("no batch");
        kit.Coders().Should().BeEmpty();
        File.Exists(kit.Paths.BatchDone(0)).Should().BeFalse();
    }

    [Fact]
    public async Task A_tasks_json_edited_after_every_batch_is_done_does_not_stop_the_arm()
    {
        using var kit = await SrsKit.CreateAsync();
        kit.Plan(kit.Five());
        kit.Replay(Success);
        (await kit.RunAsync()).Code.Should().Be(0);
        var tasks = kit.Five().ToList();
        tasks[3] = tasks[3] with { Depends = ["t1"] };
        File.WriteAllText(kit.Paths.TasksJson, SrsKit.TasksText(tasks));
        var callsBefore = kit.Fake.Calls().Count;

        var again = await kit.RunAsync();

        again.Code.Should().Be(0, "nothing is left to run, so a later edit of tasks.json (a fixer's) changes nothing: " + again.Error);
        kit.Fake.Calls().Should().HaveCount(callsBefore);
    }

    [Fact]
    public void The_most_severe_code_of_a_batch_ranks_4_then_3_then_5_and_a_code_none_of_them_is_outranks_all()
    {
        TaskPipeline.MostSevere([5, 3]).Should().Be(3);
        TaskPipeline.MostSevere([5, 4, 3]).Should().Be(4);
        TaskPipeline.MostSevere([5]).Should().Be(5);
        TaskPipeline.MostSevere([3, 1, 5]).Should().Be(1, "an unranked code is the most severe of all, and the batch still stops with it instead of throwing");
        TaskPipeline.MostSevere([9, 7, 4]).Should().Be(7, "two unranked codes: the lower value, whatever the order the tasks were listed in");
        TaskPipeline.MostSevere([7, 9, 4]).Should().Be(7);
    }

    [Fact]
    public async Task A_done_task_without_its_before_is_refused_with_exit_4_and_its_branch_is_kept()
    {
        using var kit = await SrsKit.CreateAsync();
        await InterruptedAsync(kit);
        var tip = (await kit.GitAsync(kit.Clone, "rev-parse", "refs/heads/tmp/t3")).Trim();
        File.Delete(kit.Paths.TaskBefore("t3"));
        var armTip = await kit.HeadAsync(kit.Clone);
        var callsBefore = kit.Fake.Calls().Count;
        kit.Replay(Success);

        var second = await kit.RunAsync();

        second.Code.Should().Be(4, second.Error + second.Output);
        second.Error.Should().Contain("t3: out/task-t3.before is missing — the pick has no range");
        File.Exists(kit.Paths.TaskBefore("t3")).Should().BeFalse("a done task never gets a .before written: its folder's HEAD is the tip of its branch, and an empty range would lose its commits in silence");
        (await kit.GitAsync(kit.Clone, "rev-parse", "refs/heads/tmp/t3")).Trim().Should().Be(tip, "tmp/t3 is kept at the tip");
        (await kit.TrackedAsync(kit.Clone, "tmp/t3")).Should().Contain("t3.txt");
        File.Exists(kit.Paths.Picked("t3")).Should().BeFalse();
        File.Exists(kit.Paths.Picked("t1")).Should().BeTrue("the pick-back runs in task order and stops at t3");
        File.Exists(kit.Paths.Picked("t2")).Should().BeTrue();
        (await kit.TrackedAsync(kit.Clone)).Should().NotContain("t3.txt").And.Contain("t1.txt");
        (await kit.HeadAsync(kit.Clone)).Should().NotBe(armTip, "t1 and t2 were carried");
        File.Exists(kit.Paths.BatchDone(0)).Should().BeFalse();
        kit.Fake.Calls().Skip(callsBefore).Where(call => call.Prompt.Contains("ONE task, **t5**", StringComparison.Ordinal)).Should().BeEmpty("batch 1 never started");
    }

    [Fact]
    public async Task A_task_coder_that_ended_on_its_deadline_is_in_stepsOnDeadline_and_in_status()
    {
        using var kit = await SrsKit.CreateAsync();
        kit.Plan(kit.Five());
        kit.Replay(Success);
        kit.ReplayFor("t1", Success, holdAfterLine: 1);
        Directory.CreateDirectory(kit.Paths.Out);
        new ArmFiles(kit.Paths).AppendTiming(new TimingLine.End(SrsKit.Arm, "2-t1-code", 1, DateTimeOffset.UtcNow, CapturedCount.Number(59), CapturedCount.Number(0), true, false, "abc1234", "finished", false));

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error + run.Output);
        File.Exists(kit.Paths.CoderHead("t1")).Should().BeTrue("a coder that ended on its deadline is DONE (D8): its head is derived (D21)");
        File.Exists(kit.Paths.CoderDiff("t1")).Should().BeTrue();

        var checks = await kit.ChecksAsync();

        checks.Code.Should().Be(0, checks.Output + checks.Error);
        kit.Objective()["stepsOnDeadline"]!.AsArray().Select(step => step!.GetValue<string>()).Should().Equal(["2-t1-code"], "the coder cut by its budget is named, and only it");
        var status = await StatusKit.RunAsync(kit.Root);
        status.Code.Should().Be(0, status.Error);
        status.Lines.Should().Contain("  2-t1-code: done — ended on its deadline");
        status.Lines.Should().Contain("  2-t2-code: done (finished)");
    }
}
