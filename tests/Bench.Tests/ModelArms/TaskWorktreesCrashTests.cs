using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Steps;
using ModelArms.Core.Tasks;
using Xunit;

namespace Bench.Tests.ModelArms;

[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed partial class TaskWorktreesTests
{
    private static async Task CrashThenAsync(WorktreeKit kit, TaskPoint at, int on, TaskTools tools)
    {
        var crash = () => kit.Sut(tools, new PickProbe { ThrowAt = at, ThrowOn = on }).PickBackAsync("t1", Ct);
        await crash.Should().ThrowAsync<InvalidOperationException>();
        kit.ForgetCalls();
    }

    private static int Picks(WorktreeKit kit) => kit.Calls().Count(call => call.Contains("cherry-pick --no-edit", StringComparison.Ordinal));

    [Fact]
    public async Task A_crash_after_a_pick_and_before_the_marker_skips_the_carried_commits_on_reentry()
    {
        using var kit = await KitAsync();
        var recording = kit.Recording();
        var shas = await DoneWithCommitsAsync(kit, kit.Sut(recording), "c1.txt", "c2.txt", "c3.txt");
        var start = kit.BeforeOf("t1");
        await CrashThenAsync(kit, TaskPoint.Committed, 2, recording);
        File.ReadAllLines(kit.Paths.PickRecord("t1")).Should().HaveCount(3, "c1's pair and c2's OPEN intent");
        File.Exists(kit.Paths.Picked("t1")).Should().BeFalse();

        (await kit.Sut(recording).PickBackAsync("t1", Ct)).Should().BeNull();

        Picks(kit).Should().Be(1, "only c3 is picked on re-entry");
        (await kit.CountAsync(kit.Clone, start)).Should().Be(3, "not 5");
        var second = await kit.HeadAsync(kit.Clone) is var head ? (await kit.GitAsync(kit.Clone, "rev-parse", "HEAD~1")).Trim() : string.Empty;
        kit.LogLines.Should().Contain($"pick-t1.json: {shas[1]} was picked as {second} before the crash — recorded");
        kit.LogLines.Should().Contain("t1: 1 commit(s) carried over (2 already carried by the record, 0 by patch-id)");
        File.ReadAllLines(kit.Paths.PickRecord("t1")).Should().HaveCount(6);
        File.ReadAllText(kit.Paths.Picked("t1")).Should().Be(File.ReadAllText(kit.Paths.PickRecord("t1")));
        head.Should().NotBeEmpty();
    }

    [Fact]
    public async Task A_commit_already_in_the_arm_history_by_patch_id_is_skipped_without_a_record()
    {
        using var kit = await KitAsync();
        var recording = kit.Recording();
        var shas = await DoneWithCommitsAsync(kit, kit.Sut(recording), "c1.txt");
        await kit.GitAsync(kit.Clone, "cherry-pick", shas[0]);
        kit.ForgetCalls();

        (await kit.Sut(recording).PickBackAsync("t1", Ct)).Should().BeNull();

        Picks(kit).Should().Be(0);
        kit.LogLines.Should().Contain("t1: 0 commit(s) carried over (0 already carried by the record, 1 by patch-id)");
        kit.RecordLines("t1").Should().BeEmpty();
        File.Exists(kit.Paths.Picked("t1")).Should().BeTrue();
        new FileInfo(kit.Paths.Picked("t1")).Length.Should().Be(0);
    }

    [Fact]
    public async Task A_merge_commit_in_a_task_branch_stops_the_pick_with_exit_4_and_leaves_no_cherry_pick_in_progress()
    {
        using var kit = await KitAsync();
        var recording = kit.Recording();
        var shas = await DoneWithCommitsAsync(kit, kit.Sut(recording), "c1.txt", "c2.txt");
        var tree = (await kit.GitAsync(kit.Wt("t1"), "rev-parse", "HEAD^{tree}")).Trim();
        var merge = (await kit.GitAsync(kit.Wt("t1"), "-c", "user.name=test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit-tree", tree, "-p", shas[1], "-p", shas[0], "-m", "merge")).Trim();
        await kit.GitAsync(kit.Wt("t1"), "reset", "-q", "--hard", merge);
        var start = kit.BeforeOf("t1");

        var stopped = await kit.Sut(recording).PickBackAsync("t1", Ct);

        stopped.Should().NotBeNull();
        stopped!.ExitCode.Should().Be(4);
        File.ReadAllText(kit.Paths.PickLog("t1")).Should().Contain("is a merge but no -m");
        kit.RecordLines("t1").Should().HaveCount(5, "two pairs and the merge's open intent");
        kit.InGit("CHERRY_PICK_HEAD").Should().BeFalse();
        File.Exists(kit.Paths.Picked("t1")).Should().BeFalse();
        kit.ForgetCalls();

        var again = await kit.Sut(recording).PickBackAsync("t1", Ct);

        again.Should().NotBeNull();
        again!.ExitCode.Should().Be(4);
        Picks(kit).Should().Be(1, "the two commits are skipped by the record; the merge is tried and stops the same way");
        (await kit.CountAsync(kit.Clone, start)).Should().Be(2);
    }

    [Fact]
    public async Task An_empty_or_redundant_commit_is_carried_as_one_empty_commit_so_the_record_stays_one_line_per_source()
    {
        using var kit = await KitAsync();
        var sut = kit.Sut();
        await kit.ReadyAsync(sut, "t1", "t2");
        await kit.BeforeAsync("t1");
        var start = kit.BeforeOf("t1");
        await kit.GitAsync(kit.Wt("t1"), "-c", "user.name=test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "--allow-empty", "-m", "empty");
        var empty = await kit.HeadAsync(kit.Wt("t1"));
        await kit.CommitInAsync(kit.Wt("t1"), "b.txt", "xy\n", "redundant");
        var redundant = await kit.HeadAsync(kit.Wt("t1"));
        await kit.CommitInAsync(kit.Clone, "b.txt", "x\n", "the arm adds b");
        await kit.CommitInAsync(kit.Clone, "b.txt", "xy\n", "the arm extends b");
        kit.Set("t1", new StepEntry.Skip());

        (await sut.PickBackAsync("t1", Ct)).Should().BeNull();

        (await kit.SubjectsAsync(kit.Clone, start)).Should().Equal("the arm adds b", "the arm extends b", "empty", "redundant");
        (await kit.GitAsync(kit.Clone, "show", "--stat", "--format=", "HEAD")).Trim().Should().BeEmpty();
        (await kit.GitAsync(kit.Clone, "show", "--stat", "--format=", "HEAD~1")).Trim().Should().BeEmpty();
        var entries = PickRecord.Parse(File.ReadAllText(kit.Paths.PickRecord("t1"))).Should().BeOfType<PickRecordRead.Lines>().Subject.Valid;
        entries.Should().HaveCount(4);
        entries.OfType<PickEntry.Picked>().First(line => line.Source == empty).PatchId.Should().BeEmpty("an empty commit has no patch-id");
        entries.OfType<PickEntry.Picked>().First(line => line.Source == redundant).PatchId.Should().NotBeEmpty("a redundant one has a patch-id, but not one the branch holds");
        File.ReadAllText(kit.Paths.Picked("t1")).Should().Be(File.ReadAllText(kit.Paths.PickRecord("t1")));
    }

    [Fact]
    public async Task An_abort_that_cannot_clear_the_clone_stops_with_exit_3_naming_what_remains()
    {
        using var kit = await KitAsync();
        var sut = kit.Sut(kit.Recording());
        await kit.ConflictInCloneAsync(two: true);
        File.Delete(Path.Combine(kit.Clone, ".git", "sequencer", "abort-safety"));
        kit.ForgetCalls();

        var entry = await sut.EnterAsync(["t1", "t2"], kit.Recover, Ct);

        var stopped = entry.Should().BeOfType<BatchEntry.Stopped>().Subject;
        stopped.ExitCode.Should().Be(3);
        stopped.Why.Should().Contain("CHERRY_PICK_HEAD is still there after cherry-pick --abort");
        kit.Calls().Should().NotContain(call => call.StartsWith("rev-parse --verify", StringComparison.Ordinal), "nothing is read, nothing removed");
    }

    [Fact]
    public async Task A_worktree_not_on_its_tmp_branch_is_warned_and_only_the_branch_is_carried()
    {
        using var kit = await KitAsync();
        var sut = kit.Sut();
        await kit.ReadyAsync(sut, "t1", "t2");
        await kit.BeforeAsync("t1");
        var start = kit.BeforeOf("t1");
        await kit.CommitInAsync(kit.Wt("t1"), "c1.txt", "c1\n", "c1");
        await kit.GitAsync(kit.Wt("t1"), "checkout", "-q", "-b", "other");
        await kit.CommitInAsync(kit.Wt("t1"), "c2.txt", "c2\n", "c2");
        kit.Set("t1", new StepEntry.Skip());

        (await sut.PickBackAsync("t1", Ct)).Should().BeNull();

        kit.LogLines.Should().Contain("WARNING: t1's worktree is on other, not tmp/t1 — only tmp/t1 is carried");
        (await kit.SubjectsAsync(kit.Clone, start)).Should().Equal("c1");
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("redundant")]
    public async Task An_empty_source_picked_before_a_crash_is_not_picked_twice_on_reentry(string kind)
    {
        using var kit = await KitAsync();
        var recording = kit.Recording();
        var sut = kit.Sut(recording);
        await kit.ReadyAsync(sut, "t1", "t2");
        await kit.BeforeAsync("t1");
        var start = kit.BeforeOf("t1");
        if (kind == "empty")
        {
            await kit.CommitStagedAsync(kit.Wt("t1"), "empty");
        }
        else
        {
            await kit.CommitInAsync(kit.Wt("t1"), "b.txt", "xy\n", "redundant");
            await kit.CommitInAsync(kit.Clone, "b.txt", "x\n", "the arm adds b");
            await kit.CommitInAsync(kit.Clone, "b.txt", "xy\n", "the arm extends b");
        }

        var source = await kit.HeadAsync(kit.Wt("t1"));
        kit.Set("t1", new StepEntry.Skip());
        var armBefore = await kit.CountAsync(kit.Clone, start);
        var crash = () => kit.Sut(recording, new PickProbe { ThrowAt = TaskPoint.Committed }).PickBackAsync("t1", Ct);
        await crash.Should().ThrowAsync<InvalidOperationException>();

        (await kit.Sut(recording).PickBackAsync("t1", Ct)).Should().BeNull();

        (await kit.CountAsync(kit.Clone, start)).Should().Be(armBefore + 1, "exactly ONE commit for the source, never two");
        var head = await kit.HeadAsync(kit.Clone);
        var lines = PickRecord.Parse(File.ReadAllText(kit.Paths.PickRecord("t1"))).Should().BeOfType<PickRecordRead.Lines>().Subject.Valid;
        lines.Should().HaveCount(2);
        lines[0].Should().BeOfType<PickEntry.Intent>();
        lines[1].Should().BeOfType<PickEntry.Picked>().Which.PickedSha.Should().Be(head);
        File.ReadAllText(kit.Paths.Picked("t1")).Should().Be(File.ReadAllText(kit.Paths.PickRecord("t1")));
        kit.LogLines.Should().Contain($"pick-t1.json: {source} was picked as {head} before the crash — recorded");
        Picks(kit).Should().Be(1, "one cherry-pick across both runs");
    }

    [Fact]
    public async Task An_open_intent_is_redone_when_head_did_not_move_and_stops_with_exit_4_when_it_moved_by_more_than_one()
    {
        using var kit = await KitAsync();
        var recording = kit.Recording();
        var shas = await DoneWithCommitsAsync(kit, kit.Sut(recording), "c1.txt");
        var start = kit.BeforeOf("t1");
        var crash = () => kit.Sut(recording, new PickProbe { ThrowAt = TaskPoint.Picked }).PickBackAsync("t1", Ct);
        await crash.Should().ThrowAsync<InvalidOperationException>();
        File.ReadAllLines(kit.Paths.PickRecord("t1")).Should().HaveCount(1);
        var stuck = File.ReadAllText(kit.Paths.PickRecord("t1"));
        kit.ForgetCalls();

        (await kit.Sut(recording).PickBackAsync("t1", Ct)).Should().BeNull();

        Picks(kit).Should().Be(1, "the pick had not happened: it is made once, now");
        var lines = PickRecord.Parse(File.ReadAllText(kit.Paths.PickRecord("t1"))).Should().BeOfType<PickRecordRead.Lines>().Subject.Valid;
        lines.Select(line => line.GetType().Name).Should().Equal("Intent", "Intent", "Picked");
        (await kit.CountAsync(kit.Clone, start)).Should().Be(1);

        File.Delete(kit.Paths.Picked("t1"));
        await File.WriteAllTextAsync(kit.Paths.PickRecord("t1"), stuck, Ct);
        await kit.GitAsync(kit.Clone, "reset", "-q", "--hard", start);
        await kit.CommitInAsync(kit.Clone, "m1.txt", "1\n", "m1");
        await kit.CommitInAsync(kit.Clone, "m2.txt", "2\n", "m2");
        kit.ForgetCalls();

        var stopped = await kit.Sut(recording).PickBackAsync("t1", Ct);

        stopped.Should().NotBeNull();
        stopped!.ExitCode.Should().Be(4);
        stopped.Why.Should().Contain("intends to pick").And.Contain("2 commit(s) past it");
        Picks(kit).Should().Be(0);
        File.Exists(kit.Paths.Picked("t1")).Should().BeFalse();
        shas.Should().HaveCount(1);
    }
}
