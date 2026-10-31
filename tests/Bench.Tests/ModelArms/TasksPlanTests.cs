using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Domain;
using Bench.Infrastructure.Process;
using FluentAssertions;
using ModelArms.Core.Experiment;
using ModelArms.Core.Tasks;
using Xunit;
using static Bench.Tests.ModelArms.TasksPlanKit;

namespace Bench.Tests.ModelArms;

/// <summary>E4.S1: the checker the shell ran over the split's <c>work/tasks.json</c> (<c>tasks-plan.mjs</c>), as a pure function — every problem at once, in the shell's order, one <c>- &lt;sentence&gt;</c> line each — and the batches it computes. The
/// shell's own script, copied verbatim under <c>Fixtures/model-arms/tasks</c>, is the oracle (<c>The_shells_own_checker_says_the_same_on_every_list</c>).
/// <para>
/// What this does NOT prove: that a real model writes a <c>tasks.json</c> the checker accepts (the pilot, M2); a list whose ids the shell's JavaScript re-orders (an object key that reads as an array index); the sentences for the shell's crashes (a <c>null</c> in <c>tasks</c> made the
/// shell throw before printing a word); the byte shape of <c>batches.json</c> for a file name outside printable BMP text (parsed equal, not byte equal — the writer's residual, <c>McpConfig</c>'s).
/// </para></summary>
public sealed class TasksPlanTests
{
    private const string OracleSha256 = "b5f0bcfb8e2b78668286d1ce24d348689f1d49575efe73101a0f553bdef382c8";

    private static string Dump(PlanChecked checkedList) => string.Join('\n', checkedList.Lines);

    [Theory]
    [InlineData(3)]
    [InlineData(8)]
    public void Fewer_than_four_or_more_than_seven_tasks_are_refused(int count)
    {
        var result = Check(Valid(count));

        result.Lines.Should().Equal($"- there are {count} tasks; the brief asks for 4 to 7");
    }

    [Fact]
    public void A_task_id_used_twice_is_refused()
    {
        var result = Check(Edit(Valid(), tasks => tasks[1]["id"] = "t1"));

        result.Lines.Count(line => line == "- task id t1 is used twice").Should().Be(1, Dump(result));
    }

    [Theory]
    [InlineData("\"T1\"", "\"T1\"")]
    [InlineData("\"t1\\n\"", "\"t1\\n\"")]
    [InlineData("\"x\"", "\"x\"")]
    [InlineData("1", "1")]
    [InlineData("", "undefined")]
    public void An_id_not_shaped_t_n_is_refused(string idLiteral, string printed)
    {
        var result = Check(Edit(Valid(), tasks =>
        {
            if (idLiteral.Length == 0)
            {
                tasks[1].Remove("id");
            }
            else
            {
                tasks[1]["id"] = JsonNode.Parse(idLiteral);
            }
        }));

        result.Lines.Should().Contain($"- a task id must look like t1, t2…: {printed}");
    }

    [Fact]
    public void A_task_with_no_files_is_refused()
    {
        Check(Edit(Valid(), tasks => tasks[1]["files"] = new JsonArray())).Lines.Should().Contain("- t2 lists no files");
        Check(Edit(Valid(), tasks => tasks[1].Remove("files"))).Lines.Should().Contain("- t2 lists no files");
    }

    [Fact]
    public void A_file_owned_by_two_tasks_is_refused_naming_both()
    {
        var result = Check(Edit(Valid(), tasks => ((JsonArray)tasks[2]["files"]!).Add("src/F1.cs")));

        result.Lines.Should().Equal("- src/F1.cs belongs to both t1 and t3");
    }

    [Fact]
    public void A_dependency_that_does_not_exist_is_refused()
    {
        var result = Check(Edit(Valid(), tasks => tasks[1]["depends"] = Texts("t9")));

        result.Lines.Should().Contain("- t2 depends on t9, which does not exist");
    }

    [Fact]
    public void A_cycle_is_refused()
    {
        var result = Check(Edit(Valid(), tasks =>
        {
            tasks[0]["depends"] = Texts("t2");
            tasks[1]["depends"] = Texts("t1");
        }));

        result.Lines.Should().Equal("- t1 depends on itself through a cycle", "- t2 depends on itself through a cycle");
    }

    [Fact]
    public void No_task_owning_the_plan_is_refused()
    {
        var result = Check(Edit(Valid(), tasks => tasks[4]["files"] = Texts("src/F5.cs")));

        result.Lines.Should().Equal("- no task owns todo/PLAN_x.md (the last task must)");
    }

    [Fact]
    public void The_plan_owner_must_depend_on_every_other_task()
    {
        var result = Check(Edit(Valid(), tasks => tasks[4]["depends"] = Texts("t1", "t2")));

        result.Lines.Should().Equal("- t5 owns the plan but does not depend on t3, t4");
    }

    [Fact]
    public void A_scenario_nobody_delivers_is_refused()
    {
        var result = Check(Edit(Valid(), tasks => tasks[1]["scenarios"] = new JsonArray()));

        result.Lines.Should().Equal("- no task delivers S2");
    }

    [Fact]
    public void A_missing_brief_file_is_refused()
    {
        var result = Check(Text(Valid()), missing: ["t3.md"]);

        result.Lines.Should().Equal($"- t3's brief \"t3.md\" does not exist in {result.Work}");
    }

    [Theory]
    [InlineData("../x.md")]
    [InlineData("/etc/passwd")]
    [InlineData("a/b.md")]
    [InlineData("a\\b.md")]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("")]
    public void A_brief_that_is_not_one_file_name_is_refused_and_never_probed(string brief)
    {
        var json = Text(Edit(Valid(), tasks => tasks[2]["brief"] = brief));

        TasksPlan.BriefNames(json).Should().NotContain(brief, "a name that is not one file name is never handed to the caller to probe");
        TasksPlan.BriefNames(json).Should().Contain("t1.md", "the other briefs are still asked for");
        var result = Check(json, alsoThere: [brief]);
        result.Lines.Should().Equal([$"- t3's brief {JsonValue.Create(brief).ToJsonString()} does not exist in {result.Work}"], "even a caller that lists the name as present is not believed");
    }

    [Fact]
    public void A_member_that_is_not_a_list_is_one_problem_not_one_per_character()
    {
        var files = Check(Edit(Valid(), tasks => tasks[0]["files"] = "ab"));
        files.Lines.Should().Equal("- t1 lists no files", "- t1's files is not a list");

        var depends = Check(Edit(Valid(), tasks => tasks[1]["depends"] = "t1"));
        depends.Lines.Should().Equal("- t2's depends is not a list");

        var scenarios = Check(Edit(Valid(), tasks => tasks[0]["scenarios"] = "S1"));
        scenarios.Lines.Should().Equal("- t1's scenarios is not a list", "- no task delivers S1");
    }

    [Fact]
    public void A_list_holding_a_non_text_item_is_refused_because_the_batches_hold_texts()
    {
        var files = Check(Edit(Valid(), tasks => ((JsonArray)tasks[0]["files"]!).Add(7)));
        files.Lines.Should().Equal("- t1's files holds 7, which is not a text");

        var depends = Check(Edit(Valid(), tasks => tasks[1]["depends"] = new JsonArray(JsonNode.Parse("{\"a\":[1,null]}"))));
        depends.Lines.Should().Contain("- t2's depends holds {\"a\":[1,null]}, which is not a text");

        var id = Check(Edit(Valid(), tasks => tasks[1]["id"] = Texts("t2")));
        id.Lines.Should().Contain("- a task id must look like t1, t2…: [\"t2\"]", "an id the pipeline names files by is a text; the shell let [\"t2\"] pass because JavaScript prints it as t2");
    }

    [Fact]
    public void A_tasks_json_that_is_not_json_is_the_only_problem()
    {
        var result = Check("{\"tasks\": [");

        result.Lines.Should().ContainSingle().Which.Should().StartWith("- tasks.json is missing or not JSON: ");
    }

    [Fact]
    public void Every_problem_is_reported_at_once_not_the_first()
    {
        var result = Check(FiveProblems());

        result.Lines.Should().Equal(
            "- there are 3 tasks; the brief asks for 4 to 7",
            "- task id t1 is used twice",
            "- t1 lists no files",
            "- t3 depends on t9, which does not exist",
            "- no task delivers S3");
    }

    [Fact]
    public void Batches_are_ready_tasks_in_order_at_most_four_at_a_time()
    {
        var seven = Check(Valid(7)).Batches;

        seven.Batches.Should().BeEquivalentTo<IReadOnlyList<string>>([["t1", "t2", "t3", "t4"], ["t5", "t6"], ["t7"]], options => options.WithStrictOrdering());
        seven.TaskIds.Should().Equal("t1", "t2", "t3", "t4", "t5", "t6", "t7");
        TasksPlan.Ok(seven).Should().Be("ok: 7 tasks in 3 batches: [t1 t2 t3 t4] → [t5 t6] → [t7]");
        seven.Task("t7").Depends.Should().Equal("t1", "t2", "t3", "t4", "t5", "t6");
        seven.Task("t7").Files.Should().Equal("src/F7.cs", Plan);
        seven.Task("t3").Brief.Should().Be("t3.md");

        var two = Check(Valid(7), TaskLimits.Shell with { MaxParallel = 2 }).Batches;
        two.Batches.Should().BeEquivalentTo<IReadOnlyList<string>>([["t1", "t2"], ["t3", "t4"], ["t5", "t6"], ["t7"]], options => options.WithStrictOrdering());

        var chained = Check(Edit(Valid(), tasks => tasks[1]["depends"] = Texts("t1"))).Batches;
        chained.Batches.Should().BeEquivalentTo<IReadOnlyList<string>>([["t1", "t3", "t4"], ["t2"], ["t5"]], options => options.WithStrictOrdering(), "t2 waits for t1; the batch fills with the READY tasks in list order");
    }

    [Fact]
    public void The_limits_come_from_the_tasks_block()
    {
        var narrow = TaskLimits.Shell with { Max = 5 };

        Check(Valid(6), narrow).Lines.Should().Equal("- there are 6 tasks; the brief asks for 4 to 5");
        Check(Valid(5), narrow).Outcome.Should().BeOfType<Outcome<TaskBatches>.Ok>("five tasks are within 4 to 5");
        Check(Valid(4), TaskLimits.Shell with { Min = 5 }).Lines.Should().Equal("- there are 4 tasks; the brief asks for 5 to 7");
    }

    [Fact]
    public async Task The_shells_own_checker_says_the_same_on_every_list()
    {
        Assert.False(PinnedConsumer.NodePath.Length == 0, "a node must resolve on the test process's PATH to run the shell's checker");
        var fixture = ModelArmsFixtures.Path("tasks/tasks-plan.mjs");
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(fixture))).Should().Be(OracleSha256, "the oracle is the shell's script, byte for byte");

        var rows = OracleRows().ToList();
        rows.Select(row => row.Name).Should().OnlyHaveUniqueItems();
        var accepted = 0;
        foreach (var row in rows)
        {
            accepted += await CompareAsync(fixture, row) ? 1 : 0;
        }

        accepted.Should().BeGreaterThanOrEqualTo(3, "the oracle must also have accepted lists, or the byte comparison of batches.json compared nothing");
        rows.Count.Should().BeGreaterThan(25, "a theory that enumerates nothing passes forever");
    }

    /// <summary>Runs the shell's script over the row and compares: its lines with <c>Validate</c>'s, or its <c>ok:</c> line and its <c>batches.json</c> with <c>Ok</c> and <c>BatchesFile.Write</c>. Returns whether the shell accepted the list.</summary>
    private static async Task<bool> CompareAsync(string fixture, PlanRow row)
    {
        using var temp = new TempFolder();
        var root = temp.Folder("exp");
        File.WriteAllText(Path.Combine(root, "exp.env"), $"A=1\nPLAN={Plan}\nB=2\n");
        File.WriteAllText(Path.Combine(root, "scenarios.json"), JsonSerializer.Serialize(Scenarios.Select(id => new { id, name = "n " + id })));
        var work = temp.Folder("work");
        File.WriteAllText(Path.Combine(work, "tasks.json"), row.Json);
        foreach (var name in TasksPlan.BriefNames(row.Json).Except(row.MissingBriefs))
        {
            File.WriteAllText(Path.Combine(work, name), "brief");
        }

        var environment = new Dictionary<string, string> { ["COAI_EXP_ROOT"] = root, ["HOME"] = temp.Path };
        var attempt = await ProcessRunner.RunAsync(PinnedConsumer.NodePath, [fixture, work], work, TimeSpan.FromSeconds(60), string.Empty, environment, CancellationToken.None);
        var shell = attempt.Should().BeOfType<ProcessAttempt.Completed>(attempt.Describe).Subject.Result;
        var workFiles = TasksPlan.BriefNames(row.Json).Except(row.MissingBriefs).ToHashSet(StringComparer.Ordinal);
        var ours = TasksPlan.Validate(row.Json, Plan, Scenarios, TaskLimits.Shell, workFiles, work);
        var said = shell.StandardOutput;

        if (shell.ExitCode == 0)
        {
            var batches = ours.Should().BeOfType<Outcome<TaskBatches>.Ok>($"[{row.Name}] the shell accepted the list").Subject.Value;
            said.Should().Be(TasksPlan.Ok(batches), $"[{row.Name}] the shell's own line");
            File.ReadAllText(Path.Combine(work, "batches.json")).Should().Be(BatchesFile.Write(batches, work), $"[{row.Name}] batches.json, byte for byte");
            return true;
        }

        shell.ExitCode.Should().Be(1, $"[{row.Name}] {shell.Output}");
        File.Exists(Path.Combine(work, "batches.json")).Should().BeFalse($"[{row.Name}] the shell writes no batches for a refused list");
        var expected = ours.Should().BeOfType<Outcome<TaskBatches>.Fail>($"[{row.Name}] the shell refused the list").Subject.Reason;
        const string NotJson = "tasks.json is missing or not JSON: ";
        if (said.StartsWith(NotJson, StringComparison.Ordinal))
        {
            expected.Should().StartWith("- " + NotJson, $"[{row.Name}] the reason is the reader's own and differs between engines").And.NotContain("\n");
        }
        else
        {
            expected.TrimEnd().Should().Be(said.TrimEnd(), $"[{row.Name}] the shell's lines, character for character (the runner trims what ends its output: an id that is null joins as nothing and leaves a trailing space)");
        }

        return false;
    }
}
