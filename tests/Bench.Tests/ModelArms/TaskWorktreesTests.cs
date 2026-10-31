using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Steps;
using ModelArms.Core.Tasks;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>E4.S2, D14 and §4.6 over REAL git: how a batch of task worktrees is entered, kept, recreated, reattached, refused and dropped; the clone's own state at entry (a cherry-pick left in progress, a stale <c>index.lock</c>, the model's unfinished operations); the worktree's dressing (the rules submodule from the arm's own
/// checkout, the <c>node_modules</c> links, the capped warm build). The pick-back, the pick record and the road every git takes are in the same class (<c>TaskWorktreesPickTests.cs</c>, <c>TaskWorktreesRoadTests.cs</c>). The arm is <see cref="WorktreeKit"/>; the git is the host's; the build is the bench's shim.
/// <para>
/// What this does NOT prove: Windows (every test links, plants a program or starts a shell shim); a git other than the host's here (the pilot's log in the module doc runs the class on 2.39.4 and 2.45.0); the target's real build; LFS (git-lfs is absent here).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed partial class TaskWorktreesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<WorktreeKit> KitAsync()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the scenarios link folders and start shell shims");
        return await WorktreeKit.CreateAsync();
    }

    private static IEnumerable<string> Folders(BatchEntry.Ready ready) => ready.Places.OfType<BatchPlace.Run>().Select(run => run.Folder);

    [Fact]
    public async Task A_worktree_is_added_on_a_tmp_branch_from_head_with_the_submodule_from_the_arms_own_checkout()
    {
        using var kit = await KitAsync();

        var ready = await kit.ReadyAsync(kit.Sut(), "t1", "t2");

        ready.Places.Should().Equal(new BatchPlace.Run("t1", kit.Wt("t1")), new BatchPlace.Run("t2", kit.Wt("t2")));
        var clone = await kit.HeadAsync(kit.Clone);
        foreach (var id in new[] { "t1", "t2" })
        {
            var wt = kit.Wt(id);
            File.Exists(Path.Combine(wt, ".git")).Should().BeTrue($"{id}'s worktree has a .git FILE");
            (await kit.GitAsync(wt, "symbolic-ref", "--short", "HEAD")).Trim().Should().Be($"tmp/{id}");
            (await kit.HeadAsync(wt)).Should().Be(clone);
            File.Exists(Path.Combine(wt, ".agents", "conventions", "tools", "low-priority.mjs")).Should().BeTrue("the rules submodule is updated in the worktree");
            File.ReadAllText(Path.Combine(wt, ".agents", "conventions", ".git")).Should().Contain($"/.git/worktrees/{id}/modules/", "the submodule is a fresh clone whose gitdir lives under the worktree's admin folder");
            File.ReadAllText(kit.Paths.WorktreeBuildLog(id)).Should().Contain("Build succeeded.", "the shim's output is the build log");
        }

        (await kit.GitAsync(kit.Clone, "config", "--get", "submodule..agents/conventions.url")).Trim().Should().Be(Path.Combine(kit.Clone, ".agents/conventions"));
        var builds = kit.Bench.Recorded().Where(call => call.Tool == "dotnet" && call.Arguments[0] == "build").ToList();
        builds.Select(call => call.Cwd).Should().BeEquivalentTo([kit.Wt("t1"), kit.Wt("t2")], "one build per task, started in its worktree");
        builds.Should().OnlyContain(call => call.Executable == Path.Combine(kit.Root.Temp, "bin", "dotnet"), "by the shim's path the verb resolved");
        kit.LogLines.Should().Contain("worktree for t1 ready");
    }

    [Fact]
    public async Task A_stranded_worktree_of_a_task_neither_done_nor_picked_is_removed_and_recreated_on_reentry()
    {
        using var kit = await KitAsync();
        var sut = kit.Sut(kit.Recording());
        await kit.ReadyAsync(sut, "t1", "t2");
        await kit.CommitInAsync(kit.Wt("t1"), "work.txt", "work\n", "t1 work");
        await File.WriteAllTextAsync(Path.Combine(kit.Wt("t1"), "stray.txt"), "stray\n", Ct);
        var head = await kit.HeadAsync(kit.Clone);
        kit.ForgetCalls();

        await kit.ReadyAsync(sut, "t1", "t2");

        File.Exists(Path.Combine(kit.Wt("t1"), "stray.txt")).Should().BeFalse("the worktree is fresh");
        (await kit.GitAsync(kit.Clone, "rev-parse", "tmp/t1")).Trim().Should().Be(head, "the stranded commit is gone with the branch");
        (await kit.GitAsync(kit.Clone, "worktree", "list", "--porcelain")).Split('\n').Count(line => line == $"worktree {kit.Wt("t1")}").Should().Be(1);
        AssertInOrder(kit.Questions(), "worktree remove --force --force " + kit.Wt("t1"), "worktree prune", "branch -D tmp/t1", $"worktree add -q --no-checkout {kit.Wt("t1")} -b tmp/t1 HEAD");
        kit.LogLines.Should().Contain("t1: recreated — its worktree and branch were removed (not done, not picked)");
    }

    /// <summary>Each of <paramref name="expected"/> is the start of a call after the one that matched the one before.</summary>
    internal static void AssertInOrder(IReadOnlyList<string> calls, params string[] expected)
    {
        var at = -1;
        foreach (var start in expected)
        {
            var next = -1;
            for (var index = at + 1; index < calls.Count && next < 0; index++)
            {
                next = calls[index].StartsWith(start, StringComparison.Ordinal) ? index : -1;
            }

            next.Should().BeGreaterThan(at, $"'{start}' must follow the previous call; calls were:\n{string.Join('\n', calls)}");
            at = next;
        }
    }

    [Fact]
    public async Task A_task_whose_step_is_recovered_done_is_picked_back_from_its_branch_never_recreated()
    {
        using var kit = await KitAsync();
        var sut = kit.Sut(kit.Recording());
        await kit.ReadyAsync(sut, "t1", "t2");
        await kit.BeforeAsync("t1");
        await kit.CommitInAsync(kit.Wt("t1"), "work.txt", "work\n", "t1 work");
        var commit = await kit.HeadAsync(kit.Wt("t1"));
        await File.WriteAllTextAsync(Path.Combine(kit.Wt("t2"), "stray.txt"), "stray\n", Ct);
        kit.Set("t1", new StepEntry.RecoveredDone(new StepEnd.Finished(), 1));
        kit.ForgetCalls();

        var ready = await kit.ReadyAsync(sut, "t1", "t2");

        ready.Places.Should().Equal(new BatchPlace.Run("t1", kit.Wt("t1")), new BatchPlace.Run("t2", kit.Wt("t2")));
        (await kit.GitAsync(kit.Clone, "rev-parse", "tmp/t1")).Trim().Should().Be(commit, "the finished task keeps its branch");
        File.Exists(Path.Combine(kit.Wt("t2"), "stray.txt")).Should().BeFalse("t2 never started and is recreated");
        var questions = kit.Questions();
        var lastRead = questions.Select((call, index) => (call, index)).Where(pair => pair.call.StartsWith("rev-parse --verify", StringComparison.Ordinal)).Max(pair => pair.index);
        var firstRemoval = questions.Select((call, index) => (call, index)).First(pair => pair.call.StartsWith("worktree remove", StringComparison.Ordinal)).index;
        lastRead.Should().BeLessThan(firstRemoval, "every task is read before any is cleaned (D14)");
        (await sut.PickBackAsync("t1", Ct)).Should().BeNull();
        (await kit.SubjectsAsync(kit.Clone, kit.BeforeOf("t1"))).Should().Equal("t1 work");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_cherry_pick_left_in_progress_is_aborted_on_entry_before_the_record_is_read_and_is_never_a_conflict(bool two)
    {
        using var kit = await KitAsync();
        var sut = kit.Sut(kit.Recording());
        await kit.ReadyAsync(sut, "t1", "t2");
        await kit.BeforeAsync("t1");
        await kit.CommitInAsync(kit.Wt("t1"), "t1.txt", "t1\n", "t1 work");
        kit.Set("t1", new StepEntry.Skip());
        var armHead = await kit.ConflictInCloneAsync(two);
        kit.InGit("CHERRY_PICK_HEAD").Should().BeTrue("the fixture left a real cherry-pick in progress");
        kit.InGit("sequencer").Should().Be(two);
        kit.ForgetCalls();
        kit.ClearLog();

        await kit.ReadyAsync(sut, "t1", "t2");

        kit.LogLines[0].Should().StartWith("the clone had a cherry-pick in progress (").And.EndWith(") — aborted; not a conflict");
        kit.InGit("CHERRY_PICK_HEAD").Should().BeFalse();
        kit.InGit("sequencer").Should().BeFalse();
        (await kit.HeadAsync(kit.Clone)).Should().Be(armHead, "the abort restores HEAD");
        var calls = kit.Questions();
        var abort = calls.ToList().FindIndex(call => call == "cherry-pick --abort");
        abort.Should().BeGreaterThanOrEqualTo(0);
        calls.Take(abort).Should().NotContain(call => call.StartsWith("rev-list", StringComparison.Ordinal) || call.Contains("diff-tree", StringComparison.Ordinal));
        (await sut.PickBackAsync("t1", Ct)).Should().BeNull("a recovery is never a conflict (exit 4)");
    }

    public enum Row
    {
        LockedAndInitialised,
        FolderGoneStillRegistered,
        BranchWithoutWorktree,
        LockedThenFolderGone,
    }

    [Theory]
    [InlineData(Row.LockedAndInitialised)]
    [InlineData(Row.FolderGoneStillRegistered)]
    [InlineData(Row.BranchWithoutWorktree)]
    [InlineData(Row.LockedThenFolderGone)]
    public async Task Recreation_removes_by_force_prunes_and_deletes_the_branch_before_adding_it_again(Row row)
    {
        using var kit = await KitAsync();
        var sut = kit.Sut(kit.Recording());
        await kit.ReadyAsync(sut, "t1", "t2");
        var head = await kit.HeadAsync(kit.Clone);
        switch (row)
        {
            case Row.LockedAndInitialised:
                await kit.GitAsync(kit.Clone, "worktree", "lock", kit.Wt("t1"));
                break;
            case Row.FolderGoneStillRegistered:
                Directory.Delete(kit.Wt("t1"), recursive: true);
                break;
            case Row.LockedThenFolderGone:
                await kit.GitAsync(kit.Clone, "worktree", "lock", kit.Wt("t1"));
                Directory.Delete(kit.Wt("t1"), recursive: true);
                break;
            default:
                await kit.GitAsync(kit.Clone, "worktree", "remove", "--force", kit.Wt("t1"));
                break;
        }

        kit.ForgetCalls();
        await kit.ReadyAsync(sut, "t1", "t2");

        (await kit.GitAsync(kit.Wt("t1"), "symbolic-ref", "--short", "HEAD")).Trim().Should().Be("tmp/t1");
        (await kit.GitAsync(kit.Clone, "rev-parse", "tmp/t1")).Trim().Should().Be(head);
        (await kit.GitAsync(kit.Clone, "worktree", "list", "--porcelain")).Split('\n').Count(line => line == $"worktree {kit.Wt("t1")}").Should().Be(1);
        var calls = kit.Questions();
        AssertInOrder(calls, "worktree prune", "branch -D tmp/t1", $"worktree add -q --no-checkout {kit.Wt("t1")} -b tmp/t1 HEAD");
        calls.Any(call => call == "worktree remove --force --force " + kit.Wt("t1")).Should().Be(row is Row.LockedAndInitialised or Row.LockedThenFolderGone or Row.FolderGoneStillRegistered, "a remove wherever the worktree is registered (a locked registration whose folder is gone is neither prunable nor deletable otherwise, M-ac)");
        if (row == Row.LockedThenFolderGone)
        {
            AssertInOrder(calls, "worktree remove --force --force " + kit.Wt("t1"), "worktree prune", "branch -D tmp/t1");
        }
    }

    [Fact]
    public async Task A_recreated_task_leaves_no_before_behind()
    {
        using var kit = await KitAsync();
        var sut = kit.Sut();
        await kit.ReadyAsync(sut, "t1", "t2");
        await kit.BeforeAsync("t1");
        await kit.BeforeAsync("t2");
        var kept = await File.ReadAllTextAsync(kit.Paths.TaskBefore("t2"), Ct);
        kit.Set("t2", new StepEntry.Skip());

        await kit.ReadyAsync(sut, "t1", "t2");

        File.Exists(kit.Paths.TaskBefore("t1")).Should().BeFalse("a recreated task starts from the current HEAD: the old .before would widen the pick range");
        (await File.ReadAllTextAsync(kit.Paths.TaskBefore("t2"), Ct)).Should().Be(kept);
    }

    [Fact]
    public async Task A_done_task_whose_branch_is_gone_is_refused_with_exit_4_naming_it()
    {
        using var kit = await KitAsync();
        var sut = kit.Sut(kit.Recording());
        await kit.ReadyAsync(sut, "t2", "t3");
        await File.WriteAllTextAsync(Path.Combine(kit.Wt("t2"), "stray.txt"), "stray\n", Ct);
        kit.Set("t1", new StepEntry.Skip());
        kit.ForgetCalls();

        var entry = await sut.EnterAsync(["t1", "t2"], kit.Recover, Ct);

        var stopped = entry.Should().BeOfType<BatchEntry.Stopped>().Subject;
        stopped.ExitCode.Should().Be(4);
        stopped.Why.Should().Contain("t1: its step is done but tmp/t1 is gone — its commits exist nowhere else");
        File.Exists(Path.Combine(kit.Wt("t2"), "stray.txt")).Should().BeTrue("read before clean: nothing of t2 is removed");
        kit.Questions().Should().NotContain(call => call.StartsWith("worktree remove", StringComparison.Ordinal) || call.StartsWith("branch -D", StringComparison.Ordinal));
        File.ReadAllText(kit.Paths.Timing).Should().Contain("\"2-t1-code\"", "a refusal that names a task writes the task's stop line");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_done_task_whose_worktree_is_gone_is_reattached_to_its_branch_without_a_build(bool locked)
    {
        using var kit = await KitAsync();
        var sut = kit.Sut(kit.Recording());
        await kit.ReadyAsync(sut, "t1", "t2");
        await kit.BeforeAsync("t1");
        await kit.CommitInAsync(kit.Wt("t1"), "work.txt", "work\n", "t1 work");
        var commit = await kit.HeadAsync(kit.Wt("t1"));
        if (locked)
        {
            await kit.GitAsync(kit.Clone, "worktree", "lock", kit.Wt("t1"));
        }

        Directory.Delete(kit.Wt("t1"), recursive: true);
        kit.Set("t1", new StepEntry.Skip());
        var buildsBefore = kit.Bench.Recorded().Count(call => call.Tool == "dotnet" && call.Cwd == kit.Wt("t1"));
        kit.ForgetCalls();

        var ready = await kit.ReadyAsync(sut, "t1", "t2");

        ready.Places.Should().Contain(new BatchPlace.Run("t1", kit.Wt("t1")));
        (await kit.GitAsync(kit.Wt("t1"), "symbolic-ref", "--short", "HEAD")).Trim().Should().Be("tmp/t1");
        (await kit.HeadAsync(kit.Wt("t1"))).Should().Be(commit);
        File.Exists(Path.Combine(kit.Wt("t1"), ".agents", "conventions", "tools", "low-priority.mjs")).Should().BeTrue("the submodule is updated again");
        new DirectoryInfo(Path.Combine(kit.Wt("t1"), "src_vs_code", "node_modules")).LinkTarget.Should().NotBeNull("the links are made again");
        kit.Bench.Recorded().Count(call => call.Tool == "dotnet" && call.Cwd == kit.Wt("t1")).Should().Be(buildsBefore, "a done task's worktree is not built");
        var calls = kit.Questions();
        AssertInOrder(calls, "worktree remove --force --force " + kit.Wt("t1"), "worktree prune", $"worktree add -q --no-checkout {kit.Wt("t1")} tmp/t1");
        calls.Should().NotContain(call => call == "branch -D tmp/t1" || call.Contains("-b tmp/t1", StringComparison.Ordinal));
        kit.LogLines.Should().Contain("t1: reattached to tmp/t1 (its worktree was gone)");
        (await sut.PickBackAsync("t1", Ct)).Should().BeNull();
        (await kit.SubjectsAsync(kit.Clone, kit.BeforeOf("t1"))).Should().Equal("t1 work");
    }

    [Theory]
    [InlineData("both")]
    [InlineData("branch")]
    [InlineData("neither")]
    [InlineData("locked-folder-gone")]
    public async Task A_picked_task_whose_worktree_or_branch_is_still_there_is_dropped_on_reentry_and_nothing_else_is_touched(string left)
    {
        using var kit = await KitAsync();
        var sut = kit.Sut(kit.Recording());
        await kit.ReadyAsync(sut, "t1", "t2");
        await File.WriteAllTextAsync(kit.Paths.Picked("t1"), string.Empty, Ct);
        if (left is "branch" or "neither")
        {
            await kit.GitAsync(kit.Clone, "worktree", "remove", "--force", kit.Wt("t1"));
        }

        if (left == "neither")
        {
            await kit.GitAsync(kit.Clone, "branch", "-D", "tmp/t1");
        }

        if (left == "locked-folder-gone")
        {
            await kit.GitAsync(kit.Clone, "worktree", "lock", kit.Wt("t1"));
            Directory.Delete(kit.Wt("t1"), recursive: true);
        }

        kit.ForgetCalls();
        var ready = await kit.ReadyAsync(sut, "t1", "t2");

        ready.Places.Should().Equal(new BatchPlace.Picked("t1"), new BatchPlace.Run("t2", kit.Wt("t2")));
        Directory.Exists(kit.Wt("t1")).Should().BeFalse();
        (await kit.GitAsync(kit.Clone, "branch", "--list", "tmp/t1")).Trim().Should().BeEmpty();
        var writes = kit.Questions().Where(call => (call.Contains("tmp/t1", StringComparison.Ordinal) || call.Contains("/wt/t1", StringComparison.Ordinal)) && !call.StartsWith("rev-parse --verify", StringComparison.Ordinal)).ToList();
        if (left == "neither")
        {
            writes.Should().BeEmpty("nothing to do for t1 but the reads");
        }
    }

    [Theory]
    [InlineData("MERGE_HEAD", false)]
    [InlineData("REVERT_HEAD", false)]
    [InlineData("rebase-merge", true)]
    [InlineData("rebase-apply", true)]
    public async Task The_clones_own_unfinished_merge_or_revert_is_reported_and_left(string name, bool folder)
    {
        using var kit = await KitAsync();
        var sut = kit.Sut(kit.Recording());
        var path = Path.Combine(kit.Clone, ".git", name);
        if (folder)
        {
            Directory.CreateDirectory(path);
        }
        else
        {
            await File.WriteAllTextAsync(path, await kit.HeadAsync(kit.Clone) + "\n", Ct);
        }

        await kit.ReadyAsync(sut, "t1", "t2");

        kit.LogLines.Should().Contain($"the clone has .git/{name} — left as the model left it");
        Path.Exists(path).Should().BeTrue();
        kit.Calls().Should().NotContain(call => call.Contains("cherry-pick --abort", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_warm_build_that_fails_stops_with_exit_3_and_names_the_log()
    {
        using var kit = await KitAsync();
        kit.Bench.Say("build.rc", "1\n");
        var sut = kit.Sut();

        var entry = await sut.EnterAsync(["t1", "t2"], kit.Recover, Ct);

        var stopped = entry.Should().BeOfType<BatchEntry.Stopped>().Subject;
        stopped.ExitCode.Should().Be(3);
        stopped.Why.Should().Contain("wt-build-t1.log");
        File.ReadAllText(kit.Paths.WorktreeBuildLog("t1")).Should().Contain("Build succeeded.");
        Directory.Exists(kit.Wt("t1")).Should().BeTrue("the worktree is left for inspection");
        kit.Bench.Say("build.rc", "0\n");
        kit.Bench.Say("build.out", "second build\n");

        await kit.ReadyAsync(sut, "t1", "t2");

        File.ReadAllText(kit.Paths.WorktreeBuildLog("t1")).Should().Be("second build\n", "the old log is deleted by name and the new one created exclusively");
    }

    [Fact]
    public async Task A_warm_build_that_prints_past_the_cap_keeps_one_mebibyte_and_says_how_much_was_dropped()
    {
        using var kit = await KitAsync();
        var line = new string('x', 1023);
        kit.Bench.Say("build.out", string.Concat(Enumerable.Repeat(line + "\n", 3 * 1024)));

        await kit.ReadyAsync(kit.Sut(), "t1", "t2");

        var log = await File.ReadAllBytesAsync(kit.Paths.WorktreeBuildLog("t1"), Ct);
        var text = System.Text.Encoding.UTF8.GetString(log);
        var marker = text.LastIndexOf("[model-arms:", StringComparison.Ordinal);
        marker.Should().Be(1_048_576, "the first 1 MiB is kept, then the one line");
        text[marker..].Should().Be($"[model-arms: the build's output past 1 MiB was not kept — {3L * 1024 * 1024 - 1_048_576} bytes dropped]\n");
    }

    [Fact]
    public async Task A_node_modules_link_is_made_only_for_a_folder_the_clone_has_and_points_inside_the_clone()
    {
        using var kit = await KitAsync();
        Directory.Delete(Path.Combine(kit.Clone, ".agents", "conventions", "node_modules"), recursive: true);

        await kit.ReadyAsync(kit.Sut(), "t1", "t2");

        var link = new DirectoryInfo(Path.Combine(kit.Wt("t1"), "src_vs_code", "node_modules"));
        link.LinkTarget.Should().Be(Path.Combine(kit.Clone, "src_vs_code", "node_modules"));
        new DirectoryInfo(Path.Combine(kit.Wt("t1"), ".agents", "conventions", "node_modules")).LinkTarget.Should().BeNull();
        kit.LogLines.Count(line => line == "no .agents/conventions/node_modules in the clone — not linked").Should().Be(2, "once per worktree");

        var climbing = kit.Target with { NodeModules = [.. kit.Target.NodeModules, "../x"] };
        var entry = await kit.Sut(target: climbing).EnterAsync(["t3", "t4"], kit.Recover, Ct);
        var stopped = entry.Should().BeOfType<BatchEntry.Stopped>().Subject;
        stopped.ExitCode.Should().Be(4);
        stopped.Why.Should().Contain("../x");
    }

    [Fact]
    public async Task A_task_whose_session_resumes_keeps_its_worktree_branch_and_uncommitted_edits_on_reentry()
    {
        using var kit = await KitAsync();
        var sut = kit.Sut(kit.Recording());
        await kit.ReadyAsync(sut, "t1", "t2");
        await kit.BeforeAsync("t1");
        var before = await File.ReadAllTextAsync(kit.Paths.TaskBefore("t1"), Ct);
        await kit.CommitInAsync(kit.Wt("t1"), "work.txt", "work\n", "t1 work");
        var commit = await kit.HeadAsync(kit.Wt("t1"));
        await File.WriteAllTextAsync(Path.Combine(kit.Wt("t1"), "a.txt"), "edited\ntwo\nthree\n", Ct);
        await kit.GitAsync(kit.Wt("t1"), "add", "a.txt");
        await File.WriteAllTextAsync(Path.Combine(kit.Wt("t1"), "new.txt"), "new\n", Ct);
        kit.Set("t1", new StepEntry.Resume(Guid.NewGuid(), 2, 60));
        kit.ForgetCalls();

        var ready = await kit.ReadyAsync(sut, "t1", "t2");

        ready.Places.Should().Equal(new BatchPlace.Run("t1", kit.Wt("t1")), new BatchPlace.Run("t2", kit.Wt("t2")));
        (await kit.GitAsync(kit.Clone, "rev-parse", "tmp/t1")).Trim().Should().Be(commit);
        var status = (await kit.GitAsync(kit.Wt("t1"), "status", "--porcelain")).Split('\n');
        status.Should().Contain("M  a.txt").And.Contain("?? new.txt");
        (await File.ReadAllTextAsync(kit.Paths.TaskBefore("t1"), Ct)).Should().Be(before);
        var calls = kit.Questions();
        calls.Where(call => call.Contains("tmp/t1", StringComparison.Ordinal) || call.Contains("/wt/t1", StringComparison.Ordinal)).Should().OnlyContain(
            call => call.StartsWith("rev-parse --verify", StringComparison.Ordinal) || call.StartsWith("merge-base --is-ancestor", StringComparison.Ordinal) || call.StartsWith("rev-list", StringComparison.Ordinal),
            "the kept task's calls are reads");
        calls.Should().NotContain(call => (call.StartsWith("worktree remove", StringComparison.Ordinal) && call.Contains("/wt/t1", StringComparison.Ordinal)) || call.StartsWith("branch -D tmp/t1", StringComparison.Ordinal) || call.Contains("add -q " + kit.Wt("t1"), StringComparison.Ordinal));
        kit.LogLines.Should().Contain("t1: kept — its session resumes in its worktree (1 commit(s) on tmp/t1 past its .before)");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_resuming_task_whose_worktree_folder_is_gone_is_pruned_reattached_and_rebuilt_and_the_lost_edits_are_said(bool locked)
    {
        using var kit = await KitAsync();
        var sut = kit.Sut(kit.Recording());
        await kit.ReadyAsync(sut, "t1", "t2");
        await kit.BeforeAsync("t1");
        await kit.CommitInAsync(kit.Wt("t1"), "work.txt", "work\n", "t1 work");
        var commit = await kit.HeadAsync(kit.Wt("t1"));
        var before = await File.ReadAllTextAsync(kit.Paths.TaskBefore("t1"), Ct);
        if (locked)
        {
            await kit.GitAsync(kit.Clone, "worktree", "lock", kit.Wt("t1"));
        }

        Directory.Delete(kit.Wt("t1"), recursive: true);
        kit.Set("t1", new StepEntry.Resume(Guid.NewGuid(), 2, 60));
        var buildsBefore = kit.Bench.Recorded().Count(call => call.Tool == "dotnet" && call.Cwd == kit.Wt("t1"));
        kit.ForgetCalls();

        await kit.ReadyAsync(sut, "t1", "t2");

        (await kit.GitAsync(kit.Wt("t1"), "symbolic-ref", "--short", "HEAD")).Trim().Should().Be("tmp/t1");
        (await kit.HeadAsync(kit.Wt("t1"))).Should().Be(commit);
        File.Exists(Path.Combine(kit.Wt("t1"), ".agents", "conventions", "tools", "low-priority.mjs")).Should().BeTrue();
        kit.Bench.Recorded().Count(call => call.Tool == "dotnet" && call.Cwd == kit.Wt("t1")).Should().Be(buildsBefore + 1, "the resumed session expects a built tree");
        AssertInOrder(kit.Questions(), "worktree remove --force --force " + kit.Wt("t1"), "worktree prune", $"worktree add -q --no-checkout {kit.Wt("t1")} tmp/t1");
        kit.Questions().Should().NotContain(call => call == "branch -D tmp/t1");
        (await File.ReadAllTextAsync(kit.Paths.TaskBefore("t1"), Ct)).Should().Be(before);
        kit.LogLines.Should().Contain("t1: reattached to tmp/t1 (its worktree folder was gone — its uncommitted edits are lost; its 1 commit(s) are on the branch)");
    }

    public enum Damage
    {
        Detached,
        OtherBranch,
        NotDescending,
        BeforeMissing,
        BothGone,
        Stray,
    }

    [Theory]
    [InlineData(Damage.Detached, "its worktree is on another branch or a detached HEAD, not tmp/t1")]
    [InlineData(Damage.OtherBranch, "its worktree is on another branch or a detached HEAD, not tmp/t1")]
    [InlineData(Damage.NotDescending, "tmp/t1 does not descend from its out/task-t1.before")]
    [InlineData(Damage.BeforeMissing, "out/task-t1.before is missing")]
    [InlineData(Damage.BothGone, "tmp/t1 is gone")]
    [InlineData(Damage.Stray, "wt/t1 is not a registered worktree")]
    public async Task A_resuming_task_whose_worktree_branch_or_before_is_inconsistent_is_refused_with_exit_4_naming_the_recovery(Damage damage, string clause)
    {
        using var kit = await KitAsync();
        var sut = kit.Sut(kit.Recording());
        await kit.ReadyAsync(sut, "t1", "t2");
        await kit.BeforeAsync("t1");
        await kit.CommitInAsync(kit.Wt("t1"), "work.txt", "work\n", "t1 work");
        await File.WriteAllTextAsync(Path.Combine(kit.Wt("t2"), "stray.txt"), "stray\n", Ct);
        var wt = kit.Wt("t1");
        switch (damage)
        {
            case Damage.Detached:
                await kit.GitAsync(wt, "checkout", "-q", "--detach");
                break;
            case Damage.OtherBranch:
                await kit.GitAsync(wt, "checkout", "-q", "-b", "other");
                break;
            case Damage.NotDescending:
                var own = await kit.HeadAsync(wt);
                await File.WriteAllTextAsync(kit.Paths.TaskBefore("t1"), own + "\n", Ct);
                await kit.GitAsync(wt, "reset", "--hard", "-q", own + "^");
                break;
            case Damage.BeforeMissing:
                File.Delete(kit.Paths.TaskBefore("t1"));
                break;
            case Damage.BothGone:
                Directory.Delete(wt, recursive: true);
                await kit.GitAsync(kit.Clone, "worktree", "prune");
                await kit.GitAsync(kit.Clone, "branch", "-D", "tmp/t1");
                break;
            default:
                Directory.Delete(Path.Combine(kit.Clone, ".git", "worktrees", "t1"), recursive: true);
                break;
        }

        kit.Set("t1", new StepEntry.Resume(Guid.NewGuid(), 2, 60));
        kit.ForgetCalls();

        var entry = await sut.EnterAsync(["t1", "t2"], kit.Recover, Ct);

        var stopped = entry.Should().BeOfType<BatchEntry.Stopped>().Subject;
        stopped.ExitCode.Should().Be(4);
        stopped.Why.Should().Contain("t1: its session resumes but " + clause);
        File.Exists(Path.Combine(kit.Wt("t2"), "stray.txt")).Should().BeTrue("read before act: t2's stranded worktree is untouched");
        kit.Questions().Should().NotContain(call => call.StartsWith("worktree remove", StringComparison.Ordinal) || call.StartsWith("branch -D", StringComparison.Ordinal) || call.StartsWith("worktree add", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_node_modules_link_is_refused_when_a_component_of_its_parent_in_the_worktree_is_a_link()
    {
        using var kit = await KitAsync();
        var outside = kit.Temp.Folder("outside");
        var link = await WriteBlobAsync(kit, outside);
        await kit.GitAsync(kit.Clone, "rm", "-r", "--cached", "-q", "src_vs_code");
        await kit.GitAsync(kit.Clone, "update-index", "--add", "--cacheinfo", $"120000,{link},src_vs_code");
        await kit.CommitStagedAsync(kit.Clone, "src_vs_code becomes a link");
        Directory.Exists(Path.Combine(kit.Clone, "src_vs_code", "node_modules")).Should().BeTrue("the clone's working tree still holds the real folder");

        var entry = await kit.Sut().EnterAsync(["t1", "t2"], kit.Recover, Ct);

        var stopped = entry.Should().BeOfType<BatchEntry.Stopped>().Subject;
        stopped.ExitCode.Should().Be(4);
        stopped.Why.Should().Contain("t1: src_vs_code in wt/t1 is a symbolic link");
        Directory.GetFileSystemEntries(outside).Should().BeEmpty("no link landed outside the worktree");
        Directory.Exists(kit.Wt("t1")).Should().BeTrue("the worktree is left for inspection");
    }

    private static async Task<string> WriteBlobAsync(WorktreeKit kit, string target)
    {
        var attempt = await Bench.Infrastructure.Process.ProcessRunner.RunAsync(ExperimentRoot.RealGit, ["hash-object", "-w", "--stdin"], kit.Clone, TimeSpan.FromSeconds(60), target, kit.Root.Environment, Ct);
        return ((Bench.Infrastructure.Process.ProcessAttempt.Completed)attempt).Result.StandardOutput.Trim();
    }

    [Fact]
    public async Task An_enumeration_that_cannot_run_stops_with_exit_3_before_any_write_of_the_clone()
    {
        using var kit = await KitAsync();
        await kit.ReadyAsync(kit.Sut(), "t1", "t2");
        await kit.BeforeAsync("t1");
        await kit.CommitInAsync(kit.Wt("t1"), "work.txt", "work\n", "t1 work");
        kit.Set("t1", new StepEntry.Skip());
        var shim = ShimFiles.Write(
            kit.Temp.Combine("bin", "failing-enumeration-git"),
            "#!/bin/sh\n" + Sentinel.Body(kit.Marker).Replace("exec '", "case \"$*\" in *\"config --name-only\"*) echo 'fatal: planted' >&2; exit 128;; esac\nexec '", StringComparison.Ordinal));
        var sut = kit.Sut(kit.Tools(shim));
        File.Delete(kit.Paths.Timing);

        var entry = await sut.EnterAsync(["t1", "t2"], kit.Recover, Ct);

        var stopped = entry.Should().BeOfType<BatchEntry.Stopped>().Subject;
        stopped.ExitCode.Should().Be(3);
        stopped.Why.Should().Contain("batch").And.Contain("cannot be enumerated");
        kit.Calls().Should().NotContain(call => call.StartsWith("worktree add", StringComparison.Ordinal), "no write of the clone");
        File.Exists(kit.Paths.Timing).Should().BeFalse("a stop that names no task writes no timing line");

        var pick = await sut.PickBackAsync("t1", Ct);

        pick.Should().NotBeNull();
        pick!.ExitCode.Should().Be(3);
        pick.Why.Should().Contain("t1:");
        kit.Calls().Should().NotContain(call => call.Contains("cherry-pick", StringComparison.Ordinal));
    }
}
