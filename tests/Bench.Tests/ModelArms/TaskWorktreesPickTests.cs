using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Steps;
using ModelArms.Core.Tasks;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The probe of the pick-back tests: it runs <see cref="Action"/> at a point (a snapshot) or throws there — a probe that throws is the crash a test stands in for.</summary>
internal sealed class PickProbe : TaskProbe
{
    private readonly Dictionary<TaskPoint, int> _seen = [];

    public Func<TaskPoint, int, string>? Snapshot { get; init; }

    public List<string> Snapshots { get; } = [];

    /// <summary>The point at which, on its <see cref="ThrowOn"/>th reaching, the probe throws.</summary>
    public TaskPoint? ThrowAt { get; init; }

    public int ThrowOn { get; init; } = 1;

    public override void Reached(TaskPoint point, string id)
    {
        _seen[point] = _seen.GetValueOrDefault(point) + 1;
        if (Snapshot is not null)
        {
            Snapshots.Add($"{point} {_seen[point]}: {Snapshot(point, _seen[point])}");
        }

        if (point == ThrowAt && _seen[point] == ThrowOn)
        {
            throw new InvalidOperationException($"the runner is killed at {point}");
        }
    }
}

[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed partial class TaskWorktreesTests
{
    /// <summary>t1 and t2 entered, <c>.before</c> of t1 written, one commit per entry of <paramref name="files"/> in t1's worktree (subject = file name), t1 done.</summary>
    private static async Task<string[]> DoneWithCommitsAsync(WorktreeKit kit, TaskWorktrees sut, params string[] files)
    {
        await kit.ReadyAsync(sut, "t1", "t2");
        await kit.BeforeAsync("t1");
        var shas = new List<string>();
        foreach (var file in files)
        {
            await kit.CommitInAsync(kit.Wt("t1"), file, file + "\n", file);
            shas.Add(await kit.HeadAsync(kit.Wt("t1")));
        }

        kit.Set("t1", new StepEntry.Skip());
        return [.. shas];
    }

    [Fact]
    public async Task Pick_back_cherry_picks_each_tasks_commits_in_task_order()
    {
        using var kit = await KitAsync();
        var sut = kit.Sut();
        await kit.ReadyAsync(sut, "t1", "t2");
        await kit.BeforeAsync("t1");
        await kit.BeforeAsync("t2");
        var start = await kit.HeadAsync(kit.Clone);
        string[] t1 = [], t2 = [];
        foreach (var (id, files) in new[] { ("t1", new[] { "t1a.txt", "t1b.txt" }), ("t2", new[] { "t2a.txt", "t2b.txt" }) })
        {
            var shas = new List<string>();
            foreach (var file in files)
            {
                await kit.CommitInAsync(kit.Wt(id), file, file + "\n", file);
                shas.Add(await kit.HeadAsync(kit.Wt(id)));
            }

            if (id == "t1")
            {
                t1 = [.. shas];
            }
            else
            {
                t2 = [.. shas];
            }
        }

        (await sut.PickBackAsync("t1", Ct)).Should().BeNull();
        (await sut.PickBackAsync("t2", Ct)).Should().BeNull();

        (await kit.SubjectsAsync(kit.Clone, start)).Should().Equal("t1a.txt", "t1b.txt", "t2a.txt", "t2b.txt");
        (await kit.GitAsync(kit.Clone, "log", "--format=%an|%cn|%ce", $"{start}..HEAD")).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Should().OnlyContain(line => line == "test|model-arms|model-arms@model-arms.invalid", "the author is the source's, the committer the harness's");
        var lines = kit.RecordLines("t1");
        lines.Should().HaveCount(4);
        var parsed = PickRecord.Parse(File.ReadAllText(kit.Paths.PickRecord("t1"))).Should().BeOfType<PickRecordRead.Lines>().Subject.Valid;
        var picked = (await kit.GitAsync(kit.Clone, "rev-list", "--reverse", $"{start}..HEAD")).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        parsed[0].Should().Be(new PickEntry.Intent(t1[0], start));
        parsed[1].Should().Be(new PickEntry.Picked(t1[0], picked[0], await kit.PatchIdAsync(kit.Wt("t1"), t1[0])));
        parsed[2].Should().Be(new PickEntry.Intent(t1[1], picked[0]));
        parsed[3].Should().Be(new PickEntry.Picked(t1[1], picked[1], await kit.PatchIdAsync(kit.Wt("t1"), t1[1])));
        File.ReadAllText(kit.Paths.Picked("t1")).Should().Be(File.ReadAllText(kit.Paths.PickRecord("t1")));
        (await kit.SubjectsAsync(kit.Clone, start)).Should().HaveCount(t1.Length + t2.Length);
    }

    [Fact]
    public async Task A_conflict_aborts_the_pick_and_stops_with_exit_4()
    {
        using var kit = await KitAsync();
        var sut = kit.Sut();
        await kit.ReadyAsync(sut, "t1", "t2");
        await kit.BeforeAsync("t1");
        await kit.CommitInAsync(kit.Wt("t1"), "a.txt", "t1\ntwo\nthree\n", "t1 changes line 1");
        await kit.CommitInAsync(kit.Clone, "a.txt", "arm\ntwo\nthree\n", "the arm changes line 1");
        var head = await kit.HeadAsync(kit.Clone);

        var stopped = await sut.PickBackAsync("t1", Ct);

        stopped.Should().NotBeNull();
        stopped!.ExitCode.Should().Be(4);
        stopped.Why.Should().Contain("t1: its commits conflict").And.Contain("pick-t1.log");
        File.ReadAllText(kit.Paths.PickLog("t1")).Should().Contain("CONFLICT");
        kit.InGit("CHERRY_PICK_HEAD").Should().BeFalse();
        (await kit.HeadAsync(kit.Clone)).Should().Be(head);
        File.Exists(kit.Paths.Picked("t1")).Should().BeFalse();
        kit.RecordLines("t1").Should().ContainSingle("the intent was written before the pick and is the record's only line").Which.Should().Contain("\"headBefore\":\"" + head + "\"");
    }

    [Fact]
    public async Task Uncommitted_changes_left_in_a_worktree_are_warned_and_not_carried()
    {
        using var kit = await KitAsync();
        var sut = kit.Sut();
        await kit.ReadyAsync(sut, "t1", "t2");
        await kit.BeforeAsync("t1");
        var start = await kit.HeadAsync(kit.Clone);
        await kit.CommitInAsync(kit.Wt("t1"), "work.txt", "work\n", "t1 work");
        await File.WriteAllTextAsync(Path.Combine(kit.Wt("t1"), "left.txt"), "left\n", Ct);
        await File.WriteAllTextAsync(Path.Combine(kit.Wt("t1"), "a.txt"), "edited\ntwo\nthree\n", Ct);

        (await sut.PickBackAsync("t1", Ct)).Should().BeNull();

        kit.LogLines.Should().Contain("WARNING: t1 left 2 uncommitted change(s) in its worktree — not carried over");
        (await kit.SubjectsAsync(kit.Clone, start)).Should().Equal("t1 work");
        File.ReadAllText(Path.Combine(kit.Wt("t1"), "left.txt")).Should().Be("left\n");
    }

    [Fact]
    public async Task A_torn_last_line_of_the_pick_record_is_ignored_and_reported()
    {
        using var kit = await KitAsync();
        var recording = kit.Recording();
        var crash = new PickProbe { ThrowAt = TaskPoint.Recorded };
        var shas = await DoneWithCommitsAsync(kit, kit.Sut(recording, crash), "c1.txt", "c2.txt");

        var first = async () => await kit.Sut(recording, crash).PickBackAsync("t1", Ct);
        await first.Should().ThrowAsync<InvalidOperationException>();
        File.AppendAllText(kit.Paths.PickRecord("t1"), "{\"source\":\"");
        kit.ForgetCalls();

        (await kit.Sut(recording).PickBackAsync("t1", Ct)).Should().BeNull();

        kit.LogLines.Should().Contain("pick-t1.json: the last line is torn — ignored and removed");
        var record = File.ReadAllText(kit.Paths.PickRecord("t1"));
        record.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(4);
        PickRecord.Parse(record).Should().BeOfType<PickRecordRead.Lines>().Which.Valid.OfType<PickEntry.Picked>().Select(line => line.Source).Should().Equal(shas);
        kit.Calls().Count(call => call.Contains("cherry-pick --no-edit", StringComparison.Ordinal)).Should().Be(1, "only the second commit is picked on re-entry");
        File.ReadAllText(kit.Paths.Picked("t1")).Should().Be(record);

        await kit.BeforeAsync("t2");
        await File.WriteAllTextAsync(kit.Paths.PickRecord("t2"), "{\"source\":\"x\"}\n" + PickRecord.Line(new PickEntry.Intent(shas[0], shas[0])) + "\n", Ct);
        kit.ForgetCalls();
        var stopped = await kit.Sut(recording).PickBackAsync("t2", Ct);
        stopped.Should().NotBeNull();
        stopped!.ExitCode.Should().Be(4);
        stopped.Why.Should().Contain("pick-t2.json line 1");
        kit.Calls().Should().NotContain(call => call.Contains("cherry-pick", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_record_line_whose_picked_commit_is_not_on_the_arm_branch_is_not_believed()
    {
        using var kit = await KitAsync();
        var sut = kit.Sut();
        var start = (await kit.HeadAsync(kit.Clone));
        var shas = await DoneWithCommitsAsync(kit, sut, "c1.txt");
        var id = await kit.PatchIdAsync(kit.Wt("t1"), shas[0]);
        var unbelieved = PickRecord.Line(new PickEntry.Picked(shas[0], shas[0], id)) + "\n";
        await File.WriteAllTextAsync(kit.Paths.PickRecord("t1"), unbelieved, Ct);

        (await sut.PickBackAsync("t1", Ct)).Should().BeNull();

        kit.LogLines.Should().Contain($"pick-t1.json line 1: {shas[0]} is not on the arm branch — not believed");
        (await kit.SubjectsAsync(kit.Clone, start)).Should().Equal("c1.txt");
        var record = File.ReadAllText(kit.Paths.PickRecord("t1"));
        record.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(3);
        record.Should().StartWith(unbelieved);
        File.ReadAllText(kit.Paths.Picked("t1")).Should().Be(record);
    }

    /// <summary>Since E6.S4 (b, ledger 3.3) a patch-id no longer skips a pick: the pins now keep the RECORDED patch-id honest (the completion line's <c>patchId</c> is read by whoever audits the record), and the plumbing runs no textconv. The skip they once guarded is gone.</summary>
    [Fact]
    public async Task The_patch_id_input_carries_the_diff_pins_so_an_arm_written_textconv_cannot_make_commits_equal()
    {
        using var kit = await KitAsync();
        var recording = kit.Recording();
        var sut = kit.Sut(recording);
        var textconv = Sentinel.PlantProgram(kit.Temp, "textconv", "echo CONST");
        await kit.CommitInAsync(kit.Clone, ".gitattributes", "*.txt diff=d\n", "attributes");
        await kit.ReadyAsync(sut, "t1", "t2");
        await kit.BeforeAsync("t1");
        var start = await kit.HeadAsync(kit.Clone);
        await kit.CommitInAsync(kit.Wt("t1"), "x.txt", "the task's text\n", "the task adds x");
        var task = await kit.HeadAsync(kit.Wt("t1"));
        await kit.CommitInAsync(kit.Clone, "x.txt", "the arm's text\n", "the arm adds x");
        var arm = await kit.HeadAsync(kit.Clone);
        await kit.GitAsync(kit.Clone, "config", "diff.d.textconv", textconv);
        kit.Set("t1", new StepEntry.Skip());
        var ids = new List<string>();
        foreach (var sha in new[] { task, arm })
        {
            var patch = await Bench.Infrastructure.Process.ProcessRunner.RunAsync(ExperimentRoot.RealGit, ["diff", sha + "^", sha], kit.Clone, TimeSpan.FromSeconds(60), string.Empty, kit.HostEnvironment, Ct);
            var porcelain = ((Bench.Infrastructure.Process.ProcessAttempt.Completed)patch).Result.StandardOutput;
            var id = await Bench.Infrastructure.Process.ProcessRunner.RunAsync(ExperimentRoot.RealGit, ["patch-id", "--stable"], kit.Clone, TimeSpan.FromSeconds(60), porcelain, kit.HostEnvironment, Ct);
            ids.Add(((Bench.Infrastructure.Process.ProcessAttempt.Completed)id).Result.StandardOutput.Split(' ')[0]);
        }

        Sentinel.Ran(kit.Temp).Should().BeTrue("the plant is live: the porcelain diff ran the textconv");
        ids[0].Should().NotBeEmpty().And.Be(ids[1], "the attack: a textconv printing a constant makes two different commits' porcelain patch-ids EQUAL — the id the completion line RECORDS would be a lie, and (before E6.S4) the arm's commit would 'already carry' the task's");
        File.Delete(Sentinel.Marker(kit.Temp));
        kit.ForgetCalls();

        var stopped = await sut.PickBackAsync("t1", Ct);

        stopped.Should().NotBeNull("the task's commit is picked and honestly conflicts with the arm's (add/add)");
        stopped!.Why.Should().Contain("t1: its commits conflict");
        Sentinel.Ran(kit.Temp).Should().BeFalse("the plumbing under the diff pins ran no textconv");
        kit.Calls().Should().Contain(call => call.StartsWith("-c diff.external= diff-tree --stdin -p --no-ext-diff --no-textconv --submodule=short", StringComparison.Ordinal));
        kit.RecordLines("t1").Should().ContainSingle("only the intent: the task's commit conflicted, so no completion is recorded");
        (await kit.CountAsync(kit.Clone, start)).Should().Be(1);
    }

    [Fact]
    public async Task Pick_back_records_the_source_and_picked_shas_after_each_cherry_pick_and_the_marker_last()
    {
        using var kit = await KitAsync();
        var probe = new PickProbe
        {
            Snapshot = (_, _) => $"{kit.RecordLines("t1").Count} lines, marker {File.Exists(kit.Paths.Picked("t1"))}",
        };
        var sut = kit.Sut(probe: probe);
        var shas = await DoneWithCommitsAsync(kit, sut, "c1.txt", "c2.txt", "c3.txt");
        var start = kit.BeforeOf("t1");
        probe.Snapshots.Clear();

        (await sut.PickBackAsync("t1", Ct)).Should().BeNull();

        var at = probe.Snapshots.Where(line => line.StartsWith("Intended", StringComparison.Ordinal) || line.StartsWith("Recorded", StringComparison.Ordinal) || line.StartsWith("Marked", StringComparison.Ordinal)).ToList();
        at.Should().HaveCount(7);
        for (var k = 1; k <= 3; k++)
        {
            at[((k - 1) * 2)].Should().Contain($"Intended {k}: {(2 * k) - 1} lines, marker False", "the intent is on disk before the pick, the marker is not");
            at[((k - 1) * 2) + 1].Should().Contain($"Recorded {k}: {2 * k} lines, marker False");
        }

        at[6].Should().Contain("Marked 1: 6 lines, marker True", "the marker is written last, and reported after it exists");
        File.ReadAllText(kit.Paths.Picked("t1")).Should().Be(File.ReadAllText(kit.Paths.PickRecord("t1")));
        var parsed = PickRecord.Parse(File.ReadAllText(kit.Paths.PickRecord("t1"))).Should().BeOfType<PickRecordRead.Lines>().Subject.Valid;
        var picked = (await kit.GitAsync(kit.Clone, "rev-list", "--reverse", $"{start}..HEAD")).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        parsed.OfType<PickEntry.Picked>().Select(line => (line.Source, line.PickedSha)).Should().Equal(shas.Zip(picked));
        parsed.OfType<PickEntry.Intent>().First().HeadBefore.Should().Be(start);
    }
}
