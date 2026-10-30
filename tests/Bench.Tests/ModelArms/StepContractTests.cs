using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FluentAssertions;
using ModelArms.Core.Briefs;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The step contract (parent D25 and D27, schema v2) read strictly: <c>steps</c> keyed by identity with a closed kind, the bindings between a brief's fields and the
/// files of an arm, the placement of those bindings in a scheme, and <c>runs</c>. Every refusal is named, all of them at once, and the worked example under
/// <c>tools/model-arms/examples</c> is the one source every case is derived from. Every sentence about a step, a scheme, a run or the tasks block starts with its dotted path and
/// then a space or a colon — <c>UnfilledValues.Refuse</c> relies on it — and every expectation here asserts that prefix, not only the words after it.
/// <para>
/// What this does NOT prove: that a path exists, that a template fills (<c>BriefFieldsTests</c> and <c>BriefFillerTests</c>), or that a model writes what it is briefed to — the
/// runner records that (E2.S4).
/// </para></summary>
public sealed class StepContractTests
{
    private static readonly ExperimentLocation Loc = new("/x/root", "/x/home", string.Empty, "/x/repo");

    private static readonly string[] ExampleIds = ["code", "hreview", "review", "fix", "gate", "split", "splitFix", "taskCode", "hA", "hB", "hfix"];

    private const string TaskCodeSentence = "a taskCode step has no bindings: the task pipeline names its notes, snapshot and diff per task (E4)";

    private static string ExampleText() =>
        File.ReadAllText(Path.Combine(Cli.Repository.Root, "tools", "model-arms", "examples", "part6.experiment.json"));

    /// <summary>The example as a node tree, edited and written back — never string surgery, so an edit cannot break the JSON by accident.</summary>
    private static string Edited(Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(ExampleText(), null, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip })!.AsObject();
        edit(root);
        return root.ToJsonString();
    }

    /// <summary>The reason an edited example is refused. Asserts the refusal first, so a file that is wrongly accepted fails on an assertion about that, not on a helper's
    /// exception.</summary>
    private static string Refused(Action<JsonObject> edit)
    {
        var outcome = ExperimentFile.Read(Edited(edit), Loc);
        outcome.Failed().Should().BeTrue("the edited file must be refused");
        return outcome.Reason();
    }

    private static Experiment Accepted(Action<JsonObject> edit) => ExperimentFile.Read(Edited(edit), Loc).Ok();

    private static Experiment Example() => ExperimentFile.Read(ExampleText(), Loc).Ok();

    private static void PosixPaths() =>
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the worked example and the location use POSIX absolute paths");

    private static IReadOnlyList<string> Lines(string reason) => reason.Split('\n');

    private static JsonObject Step(JsonObject root, string id) => root["steps"]![id]!.AsObject();

    private static JsonArray Scheme(JsonObject root, string name) => root["schemes"]![name]!.AsArray();

    private static Scheme SchemeOf(Experiment experiment, string name) => experiment.Schemes.Single(scheme => scheme.Name == name);

    /// <summary>The prefix rule, asserted on every refusal: a line about <paramref name="path"/> starts with it, then a space or a colon, and holds <paramref name="rest"/>.</summary>
    private static void HasLine(string reason, string path, string rest) =>
        Lines(reason).Any(line => Names(line, path) && line.Contains(rest, StringComparison.Ordinal)).Should().BeTrue($"a line about {path} holding '{rest}' is expected, and the reason was:\n{reason}");

    private static bool Names(string line, string path) =>
        line.StartsWith(path, StringComparison.Ordinal) && line.Length > path.Length && line[path.Length] is ' ' or ':';

    [Fact]
    public void Steps_are_keyed_by_identity_and_a_kind_is_one_of_four()
    {
        PosixPaths();
        var experiment = Example();

        experiment.Steps.Keys.Should().BeEquivalentTo(ExampleIds.Select(id => new StepId(id)), "the eleven steps of parent §4.2, keyed by identity");
        experiment.Steps[new("hA")].Should().Be(new StepSpec(
            new("hA"), StepKind.Review, "haiku", 40, StepMode.Read, false, "hA-trace", [new("HA_JSON", "notes/hA.json")], "", "HA_JSON", "", true, ""));
        experiment.Steps[new("splitFix")].Outputs.Should().BeEmpty();
        experiment.Steps[new("splitFix")].Notes.Should().Be("SPLIT_NOTES");

        HasLine(Refused(root => Step(root, "code")["kind"] = "lint"), "steps.code.kind", "is 'lint' — legal: session, review, gate, taskCode");
        HasLine(Refused(root => root["steps"]!["2bad"] = Step(root, "code").DeepClone()), "steps.2bad", "'2bad' is not a step id — letters and digits, starting with a letter (hA, splitFix)");
        HasLine(Refused(root => Step(root, "code").Remove("kind")), "steps.code.kind", "is missing");
        HasLine(Refused(root => Step(root, "code")["subagents"] = "no"), "steps.code.subagents", "must be true or false");
    }

    [Fact]
    public void A_scheme_entry_names_a_step_id_the_file_defines_or_is_refused_naming_both()
    {
        PosixPaths();

        HasLine(
            Refused(root => Scheme(root, "C")[1] = "2-review:reviewer"),
            "schemes.C[1]",
            $"is '2-review:reviewer' — step id 'reviewer' is not defined under steps (defined: {string.Join(", ", ExampleIds)})");
        HasLine(Refused(root => Scheme(root, "C")[1] = "2 review:review"), "schemes.C[1]", "an entry is \"tasks\" or \"<file name>:<step id>\"");
        HasLine(Refused(root => Scheme(root, "C").Add("2-review:review")), "schemes.C", "'2-review' is placed twice");
        HasLine(Refused(root => Scheme(root, "SRS").Add("tasks")), "schemes.SRS", "tasks is placed twice");

        Example().Schemes[3].Entries[1].Should().Be(new SchemeEntry.Step("2-hA", new("hA")));
    }

    [Theory]
    [InlineData("/notes/x.md")]
    [InlineData("notes/../x.md")]
    [InlineData("tmp/x.md")]
    [InlineData("notes")]
    [InlineData("notes\\x.md")]
    [InlineData("notes//x.md")]
    [InlineData("notes/./x.md")]
    [InlineData("C:/notes/x.md")]
    [InlineData("Notes/x.md")]
    [InlineData("NOTES/x.md")]
    [InlineData("Work/x.md")]
    public void Every_output_is_a_relative_path_under_notes_or_work(string path)
    {
        PosixPaths();

        var reason = Refused(root => Step(root, "code")["outputs"]!["CODE_NOTES"] = path);

        HasLine(reason, "steps.code.outputs.CODE_NOTES", $"'{path}' is not an output path — relative, under notes/ or work/, forward slashes, no '..' (notes/hA.json)");
    }

    [Theory]
    [InlineData("code_notes")]
    [InlineData("CODE-NOTES")]
    [InlineData("CODE_NOTES1")]
    public void Every_output_field_is_a_name_in_capitals_as_a_templates_placeholder(string field)
    {
        PosixPaths();

        var reason = Refused(root => Step(root, "code")["outputs"]![field] = "notes/x.md");

        HasLine(reason, $"steps.code.outputs.{field}", $"{field} is not a field name — capitals and underscores, as a template's {{{{NAME}}}}");
    }

    [Fact]
    public void Two_fields_of_one_step_naming_one_path_are_refused_without_regard_to_case_after_the_folder()
    {
        PosixPaths();
        OutputBinding.Parse("X", "work/a/b.md").Ok().RelativePath.Should().Be("work/a/b.md", "the value is kept as written, and it is the destination");

        var reason = Refused(root => Step(root, "code")["outputs"]!["CODE_TWO"] = "notes/Code.md");

        HasLine(reason, "steps.code.outputs", "CODE_NOTES and CODE_TWO both name notes/Code.md");
    }

    [Fact]
    public void A_review_without_a_verdict_and_a_gate_without_a_status_are_refused_and_so_is_either_on_a_session()
    {
        PosixPaths();

        HasLine(Refused(root => Step(root, "review").Remove("verdict")), "steps.review.verdict", "is missing — a review step names the output its last result's JSON block is written to");
        HasLine(Refused(root => Step(root, "gate").Remove("status")), "steps.gate.status", "is missing — a gate step names the output it reads status from");
        HasLine(Refused(root => Step(root, "code")["verdict"] = "CODE_NOTES"), "steps.code.verdict", "only a review step has a verdict (this step's kind is session)");
        HasLine(Refused(root => Step(root, "code")["status"] = "CODE_NOTES"), "steps.code.status", "only a gate step has a status (this step's kind is session)");
        HasLine(Refused(root => Step(root, "review")["verdict"] = "NOPE"), "steps.review.verdict", "is 'NOPE' — not one of the step's outputs (REVIEW)");
        HasLine(Refused(root => Step(root, "gate")["status"] = "NOPE"), "steps.gate.status", "is 'NOPE' — not one of the step's outputs (GATE_NOTES, GATE_JSON)");
        HasLine(Refused(root => Step(root, "gate")["mode"] = "write"), "steps.gate.mode", "is 'write' — a gate step's mode is gate");
        HasLine(Refused(root => Step(root, "code")["mode"] = "gate"), "steps.code.mode", "is 'gate' — only a gate step runs in gate mode (this step's kind is session)");
    }

    [Fact]
    public void Diff_from_names_a_snapshotting_step_at_or_before_this_one_in_every_scheme_that_uses_it()
    {
        PosixPaths();

        HasLine(Refused(root => Step(root, "hfix")["diffFrom"] = "nope"), "steps.hfix.diffFrom", $"is 'nope' — not a step id defined under steps ({string.Join(", ", ExampleIds)})");
        HasLine(Refused(root => Step(root, "hfix")["diffFrom"] = "review"), "schemes.CHH[3] (4-hfix):", "diffFrom review is not placed at or before this position");
        HasLine(Refused(root => Step(root, "hfix")["diffFrom"] = "hB"), "schemes.CHH[3] (4-hfix):", "diffFrom hB has no snapshot");

        var experiment = Example();
        experiment.Steps[new("hreview")].DiffFrom.Should().Be("hreview", "\"at or before\" includes the step itself");
        experiment.Steps[new("hfix")].DiffFrom.Should().Be("hA");
    }

    [Fact]
    public void The_tasks_block_names_three_ids_of_the_right_kinds()
    {
        PosixPaths();

        HasLine(Refused(root => root["tasks"]!["split"] = "nope"), "tasks.split", $"is 'nope' — not a step id defined under steps ({string.Join(", ", ExampleIds)})");
        HasLine(Refused(root => root["tasks"]!["splitFix"] = "review"), "tasks.splitFix", "is 'review' — its kind is review; the split fix is a session step");
        HasLine(Refused(root => root["tasks"]!["taskCode"] = "code"), "tasks.taskCode", "is 'code' — its kind is session; the task coder is a taskCode step");
        HasLine(Refused(root => root["tasks"]!["split"] = "review"), "tasks.split", "is 'review' — its kind is review; the split is a session step");
        Accepted(root => root.Remove("tasks")).Tasks.Should().Be(TaskLimits.Shell, "an absent block is the shell's 4 / 7 / 4 and its three step names");
        TaskLimits.Shell.Should().Be(new TaskLimits(4, 7, 4, new("split"), new("splitFix"), new("taskCode"), []));

        var withoutSplit = Refused(root =>
        {
            root.Remove("tasks");
            root["steps"]!.AsObject().Remove("split");
        });
        HasLine(withoutSplit, "tasks.split", "is 'split' — not a step id defined under steps");
    }

    [Fact]
    public void A_v1_file_is_refused_naming_the_three_moves()
    {
        PosixPaths();
        var v1 = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "model-arms", "experiment-v1-431e759.json"));

        var reason = ExperimentFile.Read(v1, Loc).Reason();

        var lines = Lines(reason);
        lines.Where(line => line.StartsWith("schema is 'model-arms/experiment-v1' — this build reads 'model-arms/experiment-v2'", StringComparison.Ordinal)).Should().ContainSingle()
            .Which.Should().Contain("steps are keyed by a step id and carry kind, brief and outputs")
            .And.Contain("the notes files are bindings")
            .And.Contain("rounds moved to runs.<name>.rounds");
        lines.Should().Contain("rounds — moved to runs.<name>.rounds (schema v2)");
        lines.Should().Contain("runs is missing");
        lines.Should().NotContain(line => line.Contains("is not a member this build knows", StringComparison.Ordinal) && line.StartsWith("rounds", StringComparison.Ordinal));
    }

    [Fact]
    public void Runs_are_named_and_no_arm_appears_in_two_runs()
    {
        PosixPaths();
        var runs = Example().Runs;

        runs.Select(run => run.Name).Should().Equal("part6", "part7");
        runs[0].Rounds.Should().HaveCount(3);
        runs[0].Rounds[1].Should().Equal("C2", "CR2", "SRS2");
        runs[0].ClosedArms.Should().BeEmpty();
        runs[1].Rounds.Should().ContainSingle().Which.Should().Equal("CHH1", "CHH2", "CHH3");
        runs[1].ClosedArms.Should().HaveCount(9);
        runs[1].Closed.Should().Equal("/x/root/judge-abs", "/x/root/judge-coai", "/x/root/chain/part6");

        HasLine(Refused(root => root["runs"]!["part 8"] = root["runs"]!["part7"]!.DeepClone()), "runs.part 8", "a run name is letters, digits and dashes: it names the folder chain/part 8/");
        HasLine(Refused(root => root["runs"]!["part7"]!["rounds"]![0]!.AsArray().Add("C1")), "runs.part7", "arm C1 is already in run part6");
        HasLine(Refused(root => root["runs"]!["part6"]!["rounds"]![1]!.AsArray().Add("C1")), "runs.part6.rounds", "arm C1 is named twice");
        HasLine(
            Refused(root => root["runs"]!["part6"]!["rounds"]![1]!.AsArray().Add("XY2")),
            "runs.part6.rounds[1][3]",
            "names 'XY2', whose scheme 'XY' is not among the schemes (C, CR, SRS, CHH)");
        HasLine(Refused(root => root["runs"] = new JsonObject()), "runs", "is empty — it needs at least one member");
        HasLine(Refused(root => root["runs"]!["part6"]!.AsObject().Remove("rounds")), "runs.part6.rounds", "is missing");
    }

    [Fact]
    public void Closed_arms_of_a_run_must_be_arms_of_some_run()
    {
        PosixPaths();

        HasLine(Refused(root => root["runs"]!["part7"]!["closedArms"]!.AsArray().Add("C9")), "runs.part7.closedArms[9]", "names 'C9', which no run names");
        HasLine(
            Refused(root => root["runs"]!["part7"]!["closed"]!.AsArray().Add("relative/x")),
            "runs.part7.closed[3]",
            "is 'relative/x' — it must be <root>, start with <root>/ or ~/, or be an absolute path");
        Accepted(root => root["runs"]!["part7"]!["closed"]!.AsArray().Add("~/judges")).Runs[1].Closed.Last().Should().Be(Path.Combine("/x/home", "judges"));
    }

    [Fact]
    public void The_tasks_block_contributes_its_split_steps_outputs_and_its_own_to_later_steps()
    {
        PosixPaths();
        var experiment = Example();
        var srs = SchemeOf(experiment, "SRS");

        var placed = SchemePositions.Outputs(srs, experiment.Steps, experiment.Tasks);

        placed.Should().HaveCountGreaterThanOrEqualTo(3);
        placed.Take(3).Should().Equal(
            new PlacedOutput(0, "split", new("SPLIT_NOTES", "notes/split.md")),
            new PlacedOutput(0, "tasks", new("CODE_NOTES", "notes/code.md")),
            new PlacedOutput(1, "3-hreview", new("HREVIEW_NOTES", "notes/hreview.md")));
        Fields(srs, 1, "hreview", experiment).Should().Equal("SPLIT_NOTES", "CODE_NOTES", "HREVIEW_NOTES");
        Fields(srs, 0, "split", experiment).Should().Equal("SPLIT_NOTES");
        Fields(srs, 0, "splitFix", experiment).Should().Equal("SPLIT_NOTES");
        Fields(srs, 0, "taskCode", experiment).Should().Equal("SPLIT_NOTES");
    }

    private static IReadOnlyList<string> Fields(Scheme scheme, int position, string id, Experiment experiment) =>
        [.. SchemePositions.Visible(scheme, position, new StepId(id), experiment.Steps, experiment.Tasks).Select(binding => binding.Field)];

    [Fact]
    public void Notes_may_name_an_earlier_steps_output_and_is_refused_when_nothing_before_binds_it()
    {
        PosixPaths();
        var experiment = Example();
        experiment.Steps[new("splitFix")].Notes.Should().Be("SPLIT_NOTES", "the split fix appends to the split's notes, and the file is accepted");
        experiment.Steps[new("splitFix")].Outputs.Should().BeEmpty();

        var late = Refused(root => Step(root, "code")["notes"] = "FIX_NOTES");

        foreach (var scheme in new[] { "C", "CR", "CHH" })
        {
            HasLine(late, $"schemes.{scheme}[0] (1-code):", "notes FIX_NOTES is bound by no step at or before this position (visible: CODE_NOTES)");
        }

        var nowhere = Refused(root => Step(root, "code")["notes"] = "NOPE");

        HasLine(nowhere, "steps.code.notes", "is 'NOPE' — no step binds it");
        Lines(nowhere).Should().NotContain(line => line.StartsWith("schemes.", StringComparison.Ordinal), "one cause, one sentence");
    }

    [Fact]
    public void An_output_field_is_bound_by_at_most_one_step_and_once_within_a_step()
    {
        PosixPaths();

        HasLine(Refused(root => Step(root, "fix")["outputs"]!["CODE_NOTES"] = "notes/fix2.md"), "steps.fix.outputs", "CODE_NOTES is already bound by step code");

        var shared = Refused(root => root["tasks"]!["outputs"]!["HREVIEW_NOTES"] = "notes/hreview.md");
        HasLine(shared, "schemes.SRS", "HREVIEW_NOTES is bound by step hreview and by the tasks block");
        Lines(shared).Should().NotContain(line => line.StartsWith("schemes.C ", StringComparison.Ordinal) || line.StartsWith("schemes.CR ", StringComparison.Ordinal)
            || line.StartsWith("schemes.CHH ", StringComparison.Ordinal), "only SRS places a tasks entry");

        ExperimentFile.Read(ExampleText(), Loc).Failed().Should().BeFalse("the example's tasks.outputs.CODE_NOTES beside code's is accepted: no scheme places both");

        const string once = "\"outputs\": { \"FIX_NOTES\": \"notes/fix.md\" }";
        var twice = ExampleText().Replace(once, "\"outputs\": { \"FIX_NOTES\": \"notes/fix.md\", \"FIX_NOTES\": \"notes/fix2.md\" }", StringComparison.Ordinal);
        twice.Should().NotBe(ExampleText(), "the control: the surgery must have changed the text — a JsonObject cannot hold the duplicate");
        var parsed = ExperimentFile.Read(twice, Loc);
        parsed.Failed().Should().BeTrue();
        parsed.Reason().Should().StartWith("experiment.json is not JSON:").And.Contain("FIX_NOTES", "the parser's own sentence names the member");
    }

    [Fact]
    public void Two_fields_never_bind_one_file_among_the_producers_a_scheme_places()
    {
        PosixPaths();

        ExperimentFile.Read(Edited(root => Step(root, "hB")["outputs"]!["HB_JSON"] = "notes/hA.json"), Loc).Failed().Should().BeTrue(
            "two producers of one file would make the fixer read one verdict twice");
        HasLine(Refused(root => Step(root, "hB")["outputs"]!["HB_JSON"] = "notes/hA.json"), "schemes.CHH", "notes/hA.json is written by two of its producers: 2-hA (HA_JSON) and 3-hB (HB_JSON)");
        HasLine(Refused(root => Step(root, "hB")["outputs"]!["HB_JSON"] = "notes/HA.json"), "schemes.CHH", "notes/hA.json is written by two of its producers: 2-hA (HA_JSON) and 3-hB (HB_JSON)");
        HasLine(
            Refused(root => root["tasks"]!["outputs"]!["TASK_NOTES"] = "notes/hreview.md"),
            "schemes.SRS",
            "notes/hreview.md is written by two of its producers: tasks (TASK_NOTES) and 3-hreview (HREVIEW_NOTES)");

        Accepted(root => root["steps"]!["spare"] = JsonNode.Parse(
            """{ "kind": "session", "model": "sonnet", "minutes": 5, "mode": "write", "brief": "1-code", "outputs": { "SPARE": "notes/hA.json" }, "notes": "SPARE" }"""))
            .Steps.Should().ContainKey(new StepId("spare"), "coexistence is per scheme: a step no scheme places may bind a file another step binds");
        Accepted(root => Step(root, "splitFix")["notes"] = "SPLIT_NOTES").Steps[new("splitFix")].Notes.Should().Be("SPLIT_NOTES", "appending is not producing");
    }

    [Fact]
    public void The_verdict_is_the_runners_output_and_every_other_binding_is_the_models()
    {
        PosixPaths();
        var steps = Example().Steps;

        steps[new("review")].ModelWritten.Should().BeEmpty();
        steps[new("hA")].ModelWritten.Should().BeEmpty();
        steps[new("code")].ModelWritten.Should().Equal(new OutputBinding("CODE_NOTES", "notes/code.md"));
        steps[new("gate")].ModelWritten.Select(binding => binding.Field).Should().Equal("GATE_NOTES", "GATE_JSON");
        steps[new("review")].OutputPath("REVIEW").Ok().Should().Be("notes/review.json");
        steps[new("review")].OutputPath("NOPE").Reason().Should().Be("NOPE is not an output of step review (outputs: REVIEW)");
        steps[new("taskCode")].OutputPath("X").Reason().Should().Be("X is not an output of step taskCode (outputs: none)");
    }

    public static TheoryData<string, string> TaskCodeBindings => new()
    {
        { "outputs", """{ "X": "notes/x.md" }""" },
        { "notes", "\"X\"" },
        { "verdict", "\"X\"" },
        { "status", "\"X\"" },
        { "snapshot", "true" },
        { "diffFrom", "\"code\"" },
    };

    [Theory]
    [MemberData(nameof(TaskCodeBindings))]
    public void A_taskCode_step_carries_no_bindings_the_pipeline_names_them_per_task(string member, string value)
    {
        PosixPaths();

        var reason = Refused(root => Step(root, "taskCode")[member] = JsonNode.Parse(value));

        HasLine(reason, $"steps.taskCode.{member}", TaskCodeSentence);
    }

    [Fact]
    public void An_empty_outputs_map_and_a_false_snapshot_are_no_binding_on_a_taskCode_step()
    {
        PosixPaths();

        Accepted(root => Step(root, "taskCode")["outputs"] = new JsonObject()).Steps[new("taskCode")].Outputs.Should().BeEmpty();
        Accepted(root => Step(root, "taskCode")["snapshot"] = false).Steps[new("taskCode")].Snapshot.Should().BeFalse();
    }

    [Fact]
    public void The_split_fix_is_a_step_of_its_own_not_the_split()
    {
        PosixPaths();

        var reason = Refused(root => root["tasks"]!["splitFix"] = "split");

        HasLine(reason, "tasks.splitFix", "is 'split' — the split fix is a step of its own, not the split");
        Lines(reason).Should().ContainSingle("one cause, one sentence: Visible would hold SPLIT_NOTES twice");
    }

    [Fact]
    public void A_notes_never_names_a_verdict_the_runner_writes()
    {
        PosixPaths();

        HasLine(Refused(root => Step(root, "fix")["notes"] = "REVIEW"), "steps.fix.notes", "is 'REVIEW' — the verdict of step review, which the runner writes; notes are a file the model writes");
        HasLine(Refused(root => Step(root, "hfix")["notes"] = "HA_JSON"), "steps.hfix.notes", "is 'HA_JSON' — the verdict of step hA, which the runner writes; notes are a file the model writes");
        var own = Refused(root => Step(root, "review")["notes"] = "REVIEW");
        HasLine(own, "steps.review.notes", "is 'REVIEW' — the verdict of step review, which the runner writes; notes are a file the model writes");
        Lines(own).Should().ContainSingle("one cause, one sentence: no second line from the scheme positions");
    }

    [Fact]
    public void A_binding_cannot_replace_a_field_every_brief_is_filled_with()
    {
        PosixPaths();
        var names = BriefFields.FixedNames;
        names.Should().Contain(["PLAN", "NOTES", "WORK", "TASK_BRIEF", "PROBLEMS"], "the scan must still reach the list, or it proves nothing");

        foreach (var name in names)
        {
            HasLine(Refused(root => Step(root, "code")["outputs"]![name] = "notes/p.md"), $"steps.code.outputs.{name}", $"{name} is a field every brief is filled with — a binding cannot replace it");
            HasLine(Refused(root => root["tasks"]!["outputs"]![name] = "notes/p.md"), $"tasks.outputs.{name}", $"{name} is a field every brief is filled with — a binding cannot replace it");
        }
    }

    [Fact]
    public void Diff_from_inside_one_tasks_entry_follows_the_blocks_order()
    {
        PosixPaths();

        var reason = Refused(root =>
        {
            Step(root, "split")["diffFrom"] = "splitFix";
            Step(root, "splitFix")["snapshot"] = true;
        });

        HasLine(reason, "schemes.SRS[0] (split):", "diffFrom splitFix is not placed at or before this position");
        Accepted(root =>
        {
            Step(root, "splitFix")["diffFrom"] = "split";
            Step(root, "split")["snapshot"] = true;
        }).Steps[new("splitFix")].DiffFrom.Should().Be("split", "the split runs before its fix");
    }

    [Fact]
    public void A_name_that_ends_in_a_newline_is_not_a_run_a_scheme_or_an_entry()
    {
        PosixPaths();

        Refused(root => root["runs"]!["part9\n"] = root["runs"]!["part7"]!.DeepClone()).Should().Contain("a run name is letters, digits and dashes");
        Refused(root => root["schemes"]!["Z\n"] = new JsonArray("1-code:code")).Should().Contain("a scheme name is letters only");
        HasLine(Refused(root => Scheme(root, "C")[1] = "2-review:review\n"), "schemes.C[1]", "an entry is \"tasks\" or \"<file name>:<step id>\"");
    }

    [Theory]
    [InlineData("notes/a\nb.md", "notes/a\\u000Ab.md")]
    [InlineData("notes/a\u200Bb.md", "notes/a\\u200Bb.md")]
    [InlineData("notes/ x.md", "notes/ x.md")]
    [InlineData("notes/x.md ", "notes/x.md ")]
    [InlineData("notes/a\tb.md", "notes/a\\u0009b.md")]
    [InlineData("notes/a\u2028b.md", "notes/a\\u2028b.md")]
    [InlineData("notes/a\u2029b.md", "notes/a\\u2029b.md")]
    [InlineData("notes/a\u00A0b.md", "notes/a\\u00A0b.md")]
    public void An_output_path_segment_with_a_control_or_format_character_or_an_edge_space_is_refused_and_echoed_escaped(string path, string echoed)
    {
        PosixPaths();

        var reason = Refused(root => Step(root, "code")["outputs"]!["CODE_NOTES"] = path);

        HasLine(reason, "steps.code.outputs.CODE_NOTES", $"'{echoed}' is not an output path");
        Lines(reason).Should().ContainSingle("the control character is escaped, so the sentence stays one line");
    }

    [Fact]
    public void A_closed_arm_is_an_arm_of_an_earlier_run_never_of_this_one_or_a_later_one()
    {
        PosixPaths();

        HasLine(Refused(root => root["runs"]!["part7"]!["closedArms"]!.AsArray().Add("CHH1")), "runs.part7.closedArms[9]", "names 'CHH1', an arm of this run");
        HasLine(Refused(root => root["runs"]!["part6"]!["closedArms"] = new JsonArray("CHH1")), "runs.part6.closedArms[0]", "names 'CHH1', an arm of a later run part7");
    }

    [Fact]
    public void A_taskCode_step_runs_only_inside_a_tasks_entry()
    {
        PosixPaths();

        HasLine(Refused(root => Scheme(root, "C").Add("5-coder:taskCode")), "schemes.C[4] (5-coder):", "a taskCode step runs only inside a tasks entry");
        Accepted(_ => { }).Schemes.Single(scheme => scheme.Name == "SRS").Entries[0].Should().BeOfType<SchemeEntry.Tasks>("the example places its task coder through the block");
    }

    [Theory]
    [InlineData("work/tasks.json")]
    [InlineData("work/batches.json")]
    [InlineData("work/t1.md")]
    [InlineData("notes/t1-code.md")]
    [InlineData("notes/t12-code.md")]
    [InlineData("notes/T1-code.md")]
    public void A_binding_cannot_name_a_file_the_task_pipeline_writes_in_a_scheme_that_runs_it(string path)
    {
        PosixPaths();

        var reason = Refused(root => Step(root, "hreview")["outputs"]!["X"] = path);

        HasLine(reason, "schemes.SRS", $"{path} is a file of the task pipeline");
        Lines(reason).Should().NotContain(line => line.StartsWith("schemes.CR ", StringComparison.Ordinal), "CR runs no pipeline");
    }

    [Theory]
    [InlineData("work/summary.md")]
    [InlineData("notes/hfix-code.md")]
    [InlineData("work/t1/x.md")]
    public void A_binding_that_only_looks_like_a_task_file_is_accepted(string path)
    {
        PosixPaths();

        var outcome = ExperimentFile.Read(Edited(root => Step(root, "hreview")["outputs"]!["X"] = path), Loc);

        outcome.Failed().Should().BeFalse($"{path} is no file of the pipeline: its ids are t<n>{(outcome.Failed() ? ", and the reason was: " + outcome.Reason() : string.Empty)}");
        outcome.Ok().Steps[new("hreview")].Outputs.Should().Contain(new OutputBinding("X", path));
    }

    [Theory]
    [InlineData("t1", true)]
    [InlineData("t12", true)]
    [InlineData("T1", false)]
    [InlineData("t", false)]
    [InlineData("t1\n", false)]
    [InlineData("summary", false)]
    public void A_task_id_is_t_and_a_number(string id, bool expected)
    {
        TaskIds.IsTaskId(id).Should().Be(expected);
    }

    [Fact]
    public void A_binding_that_does_not_parse_is_one_sentence_and_the_names_that_depend_on_it_say_nothing_more()
    {
        PosixPaths();

        var gate = Refused(root => Step(root, "gate")["outputs"]!["GATE_JSON"] = "tmp/x");
        HasLine(gate, "steps.gate.outputs.GATE_JSON", "is not an output path");
        Lines(gate).Should().ContainSingle("gate.status names the refused field: no second line says it is not one of the outputs");

        var notes = Refused(root => Step(root, "hreview")["outputs"]!["HREVIEW_NOTES"] = "tmp/x");
        Lines(notes).Should().ContainSingle("hreview.notes names the refused field: no second line says no step binds it, and no scheme line says it is not visible");
    }

    private static readonly Regex DottedPath = new(@"^(schema|rounds|steps|schemes|tasks|runs)\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>One edit per sentence family of the table, run through one helper. A sentence that is not produced is not judged here — each scenario above pins that its edit IS
    /// refused — so this test is the property, in one place: no refusal of the contract ever starts with a prose word.</summary>
    private static readonly Action<JsonObject>[] RefusedEdits =
    [
        root => Step(root, "code")["kind"] = "lint",
        root => root["steps"]!["2bad"] = Step(root, "code").DeepClone(),
        root => Step(root, "code").Remove("kind"),
        root => Step(root, "code")["subagents"] = "no",
        root => Step(root, "code")["brief"] = "a/b",
        root => Scheme(root, "C")[1] = "2-review:reviewer",
        root => Scheme(root, "C")[1] = "2 review:review",
        root => Scheme(root, "C").Add("2-review:review"),
        root => Scheme(root, "SRS").Add("tasks"),
        root => Step(root, "code")["outputs"]!["CODE_NOTES"] = "tmp/x.md",
        root => Step(root, "code")["outputs"]!["code_notes"] = "notes/x.md",
        root => Step(root, "code")["outputs"]!["CODE_TWO"] = "notes/Code.md",
        root => Step(root, "review").Remove("verdict"),
        root => Step(root, "gate").Remove("status"),
        root => Step(root, "code")["verdict"] = "CODE_NOTES",
        root => Step(root, "code")["status"] = "CODE_NOTES",
        root => Step(root, "review")["verdict"] = "NOPE",
        root => Step(root, "gate")["mode"] = "write",
        root => Step(root, "code")["mode"] = "gate",
        root => Step(root, "hfix")["diffFrom"] = "nope",
        root => Step(root, "hfix")["diffFrom"] = "review",
        root => Step(root, "hfix")["diffFrom"] = "hB",
        root => Step(root, "code")["notes"] = "NOPE",
        root => Step(root, "code")["notes"] = "FIX_NOTES",
        root => Step(root, "fix")["notes"] = "REVIEW",
        root => root["tasks"]!["splitFix"] = "split",
        root => Step(root, "code")["outputs"]!["PLAN"] = "notes/p.md",
        root => Step(root, "split")["diffFrom"] = "splitFix",
        root => root["runs"]!["part6"]!["closedArms"] = new JsonArray("CHH1"),
        root => Scheme(root, "C").Add("5-coder:taskCode"),
        root => Step(root, "hreview")["outputs"]!["X"] = "work/tasks.json",
        root => Step(root, "code")["outputs"]!["CODE_NOTES"] = "notes/a\nb.md",
        root => root["schemes"]!["Z\n"] = new JsonArray("1-code:code"),
        root => root["runs"]!["part9\n"] = root["runs"]!["part7"]!.DeepClone(),
        root => Step(root, "fix")["outputs"]!["CODE_NOTES"] = "notes/fix2.md",
        root => root["tasks"]!["outputs"]!["HREVIEW_NOTES"] = "notes/hreview.md",
        root => Step(root, "hB")["outputs"]!["HB_JSON"] = "notes/hA.json",
        root => root["tasks"]!["outputs"]!["TASK_NOTES"] = "notes/hreview.md",
        root => Step(root, "taskCode")["notes"] = "X",
        root => root["tasks"]!["split"] = "nope",
        root => root["tasks"]!["splitFix"] = "review",
        root => root["tasks"]!["taskCode"] = "code",
        root => root["schema"] = "model-arms/experiment-v1",
        root => root["rounds"] = new JsonArray(new JsonArray("C1")),
        root => root["runs"]!["part 8"] = root["runs"]!["part7"]!.DeepClone(),
        root => root["runs"]!["part7"]!["rounds"]![0]!.AsArray().Add("C1"),
        root => root["runs"]!["part6"]!["rounds"]![1]!.AsArray().Add("C1"),
        root => root["runs"]!["part6"]!["rounds"]![1]!.AsArray().Add("XY2"),
        root => root["runs"] = new JsonObject(),
        root => root["runs"]!["part6"]!.AsObject().Remove("rounds"),
        root => root["runs"]!["part7"]!["closedArms"]!.AsArray().Add("C9"),
        root => root["runs"]!["part7"]!["closed"]!.AsArray().Add("relative/x"),
    ];

    [Fact]
    public void Every_refusal_of_the_step_contract_starts_with_its_dotted_path()
    {
        PosixPaths();
        var lines = RefusedEdits
            .Select(edit => ExperimentFile.Read(Edited(edit), Loc))
            .Where(outcome => outcome.Failed())
            .SelectMany(outcome => Lines(outcome.Reason()))
            .ToList();

        lines.Where(line => !DottedPath.IsMatch(line)).Should().BeEmpty("a refusal that starts with a prose word is not found by UnfilledValues.Refuse's prefix rule");
        lines.Where(line => !FollowsItsPath(line)).Should().BeEmpty("the character after the longest dotted prefix is a space or a colon");
    }

    /// <summary>The longest run of letters, digits, dots, brackets, dashes and spaces inside a name is a path; what follows it is a space or a colon. A line of one word is a
    /// path with nothing after it and fails.</summary>
    private static bool FollowsItsPath(string line)
    {
        var path = Regex.Match(line, @"^[A-Za-z0-9_.\[\]\\]+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Value;
        return path.Length > 0 && path.Length < line.Length && line[path.Length] is ' ' or ':';
    }
}
