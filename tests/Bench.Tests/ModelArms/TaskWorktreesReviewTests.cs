using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Steps;
using ModelArms.Core.Tasks;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>E4.S2's review round (E4 §8 items 27–30): a worktree registered but never populated, a session that cannot be resumed, an open intent that another hand's commit must not close. Real git, <see cref="WorktreeKit"/>.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed partial class TaskWorktreesTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_worktree_registered_but_never_populated_is_populated_on_reentry_never_kept_empty(bool resumes)
    {
        using var kit = await KitAsync();
        var sut = kit.Sut(kit.Recording());
        await kit.ReadyAsync(sut, "t1", "t2");
        await kit.BeforeAsync("t1");
        await kit.CommitInAsync(kit.Wt("t1"), "work.txt", "work\n", "t1 work");
        var buildsBefore = kit.Bench.Recorded().Count(call => call.Tool == "dotnet" && call.Cwd == kit.Wt("t1"));
        kit.Set("t1", resumes ? new StepEntry.Resume(Guid.NewGuid(), 2, 60) : new StepEntry.Skip());
        Directory.Delete(kit.Wt("t1"), recursive: true);
        await kit.GitAsync(kit.Clone, "worktree", "prune");
        await kit.GitAsync(kit.Clone, "worktree", "add", "-q", "--no-checkout", kit.Wt("t1"), "tmp/t1");
        Directory.GetFileSystemEntries(kit.Wt("t1")).Should().ContainSingle("a crash between the add and the populate: the folder holds .git alone");
        kit.ForgetCalls();
        kit.ClearLog();

        await kit.ReadyAsync(sut, "t1", "t2");

        File.Exists(Path.Combine(kit.Wt("t1"), "a.txt")).Should().BeTrue("the tree was populated, not kept empty");
        (await kit.GitAsync(kit.Wt("t1"), "status", "--porcelain", "--ignore-submodules=dirty")).Split('\n').Where(line => line.Length > 0 && !line.Contains("node_modules", StringComparison.Ordinal)).Should().BeEmpty();
        kit.Bench.Recorded().Count(call => call.Tool == "dotnet" && call.Cwd == kit.Wt("t1")).Should().Be(buildsBefore + (resumes ? 1 : 0), "a resumed task is built, a done one is not");
        var blocks = Sentinel.Blocks(kit.Marker);
        blocks.Should().Contain(block => block.Call.EndsWith("rev-parse --git-path index", StringComparison.Ordinal) && block.Cwd == kit.Wt("t1"));
        blocks.Should().Contain(block => block.Call.EndsWith("reset -q --hard", StringComparison.Ordinal) && block.Cwd == kit.Wt("t1"));
        kit.Questions().Should().NotContain(call => (call.StartsWith("worktree add", StringComparison.Ordinal) || call.StartsWith("worktree remove", StringComparison.Ordinal)) && call.Contains("/wt/t1", StringComparison.Ordinal));
        kit.Questions().Should().NotContain("branch -D tmp/t1");
        kit.LogLines.Should().Contain("t1: its worktree was registered but never populated (a crash before its checkout) — populated from tmp/t1");
        if (!resumes)
        {
            (await sut.PickBackAsync("t1", Ct)).Should().BeNull();
            kit.LogLines.Should().NotContain(line => line.Contains("uncommitted", StringComparison.Ordinal), "an unpopulated index lists every file as deleted: it must not be read as left behind");
        }
    }

    [Fact]
    public async Task A_task_whose_session_cannot_be_resumed_keeps_its_commits_and_is_refused_with_exit_4_naming_the_attempt()
    {
        using var kit = await KitAsync();
        var sut = kit.Sut(kit.Recording());
        await kit.ReadyAsync(sut, "t1", "t2");
        await kit.BeforeAsync("t1");
        await kit.CommitInAsync(kit.Wt("t1"), "work.txt", "work\n", "t1 work");
        var commit = await kit.HeadAsync(kit.Wt("t1"));
        var before = await File.ReadAllTextAsync(kit.Paths.TaskBefore("t1"), Ct);
        await File.WriteAllTextAsync(Path.Combine(kit.Wt("t2"), "stray.txt"), "stray\n", Ct);
        kit.Set("t1", new StepEntry.Fresh(2, 60));
        kit.ForgetCalls();

        var entry = await sut.EnterAsync(["t1", "t2"], kit.Recover, Ct);

        var stopped = entry.Should().BeOfType<BatchEntry.Stopped>().Subject;
        stopped.ExitCode.Should().Be(4);
        stopped.Why.Should().Contain("t1: its session cannot be resumed").And.Contain("attempt 2").And.Contain("1 commit(s)");
        (await kit.GitAsync(kit.Clone, "rev-parse", "tmp/t1")).Trim().Should().Be(commit);
        Directory.Exists(kit.Wt("t1")).Should().BeTrue();
        (await File.ReadAllTextAsync(kit.Paths.TaskBefore("t1"), Ct)).Should().Be(before);
        File.Exists(Path.Combine(kit.Wt("t2"), "stray.txt")).Should().BeTrue("read before act");
        kit.Questions().Should().NotContain(call => call.StartsWith("worktree remove", StringComparison.Ordinal) || call.StartsWith("branch -D", StringComparison.Ordinal) || call.StartsWith("worktree add", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_task_whose_session_cannot_be_resumed_and_left_no_commit_is_recreated_and_the_reason_said()
    {
        using var kit = await KitAsync();
        var sut = kit.Sut(kit.Recording());
        await kit.ReadyAsync(sut, "t1", "t2");
        await kit.BeforeAsync("t1");
        await File.WriteAllTextAsync(Path.Combine(kit.Wt("t1"), "untracked.txt"), "x\n", Ct);
        kit.Set("t1", new StepEntry.Fresh(2, 60));
        kit.ForgetCalls();
        kit.ClearLog();

        await kit.ReadyAsync(sut, "t1", "t2");

        File.Exists(Path.Combine(kit.Wt("t1"), "untracked.txt")).Should().BeFalse();
        AssertInOrder(kit.Questions(), "worktree remove --force --force " + kit.Wt("t1"), "worktree prune", "branch -D tmp/t1", $"worktree add -q --no-checkout {kit.Wt("t1")} -b tmp/t1 HEAD");
        kit.LogLines.Should().Contain("t1: recreated — attempt 1 left no commit on tmp/t1 (1 uncommitted change(s) in its worktree discarded)");
    }

    private Task OperatorCommitAsync(WorktreeKit kit, string message) =>
        kit.GitAsync(kit.Clone, "-c", "user.name=op", "-c", "user.email=op@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "--allow-empty", "-m", message);

    [Fact]
    public async Task An_open_intent_is_not_closed_by_a_commit_another_hand_made_on_the_arm_branch()
    {
        using var kit = await KitAsync();
        var recording = kit.Recording();
        var shas = await DoneWithCommitsAsync(kit, kit.Sut(recording), "c1.txt");
        var crash = () => kit.Sut(recording, new PickProbe { ThrowAt = TaskPoint.Picked }).PickBackAsync("t1", Ct);
        await crash.Should().ThrowAsync<InvalidOperationException>();
        await OperatorCommitAsync(kit, "a sibling's commit");
        var head = await kit.HeadAsync(kit.Clone);
        var record = File.ReadAllText(kit.Paths.PickRecord("t1"));
        kit.ForgetCalls();

        var stopped = await kit.Sut(recording).PickBackAsync("t1", Ct);

        stopped.Should().NotBeNull("one commit past headBefore is not proof that the pipeline made it");
        stopped!.ExitCode.Should().Be(4);
        stopped.Why.Should().Contain("intends to pick").And.Contain("is not that pick").And.Contain(shas[0]).And.Contain(head);
        File.ReadAllText(kit.Paths.PickRecord("t1")).Should().Be(record, "no completion is written for a commit the pipeline did not make");
        Picks(kit).Should().Be(0);
        File.Exists(kit.Paths.Picked("t1")).Should().BeFalse();
    }

    [Fact]
    public async Task An_operators_resolution_commit_after_a_conflict_stop_is_not_taken_for_the_pipelines_pick()
    {
        using var kit = await KitAsync();
        var recording = kit.Recording();
        var sut = kit.Sut(recording);
        await kit.ReadyAsync(sut, "t1", "t2");
        await kit.BeforeAsync("t1");
        await kit.CommitInAsync(kit.Wt("t1"), "a.txt", "t1\ntwo\nthree\n", "t1 changes line 1");
        var source = await kit.HeadAsync(kit.Wt("t1"));
        await kit.CommitInAsync(kit.Clone, "a.txt", "arm\ntwo\nthree\n", "the arm changes line 1");
        kit.Set("t1", new StepEntry.Skip());
        (await sut.PickBackAsync("t1", Ct)).Should().NotBeNull("the conflict stop");
        await kit.GitAsync(kit.Clone, "checkout", source, "--", "a.txt");
        await OperatorCommitAsync(kit, "resolve t1");
        var head = await kit.HeadAsync(kit.Clone);
        var record = File.ReadAllText(kit.Paths.PickRecord("t1"));
        kit.ForgetCalls();

        var stopped = await sut.PickBackAsync("t1", Ct);

        stopped.Should().NotBeNull();
        stopped!.ExitCode.Should().Be(4);
        stopped.Why.Should().Contain("is not that pick").And.Contain(source).And.Contain(head);
        File.ReadAllText(kit.Paths.PickRecord("t1")).Should().Be(record);
        File.Exists(kit.Paths.Picked("t1")).Should().BeFalse();
    }
}
