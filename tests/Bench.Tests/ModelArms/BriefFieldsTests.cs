using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Domain;
using FluentAssertions;
using ModelArms.Core.Briefs;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The fields of a brief at a scheme position: the facts, the step's own <c>NOTES</c>, and every output a brief may see under the field name it was declared with —
/// the outputs of earlier positions and the step's own, never a later step's. The example is the one source, read at the location the other suites use.
/// <para>
/// What this does NOT prove: that a template uses what it is given (the shipped eleven are pinned by <c>BriefFillerTests</c>' theory), or that a file at any of these paths exists.
/// </para></summary>
public sealed class BriefFieldsTests
{
    private static readonly ArmPaths Paths = BriefExample.Paths;

    private static string At(string relative) => Paths.NotesFile(new OutputBinding("X", relative));

    private static IReadOnlyDictionary<string, string> Fields(string scheme, int position, string id) => BriefExample.Fields(scheme, position, id);

    [Fact]
    public void At_CHH_hfix_the_fields_are_both_verdicts_the_code_notes_and_its_own_notes_and_no_review()
    {
        var fields = Fields("CHH", 3, "hfix");

        fields.Should().ContainKeys("HA_JSON", "HB_JSON", "CODE_NOTES", "HFIX_NOTES");
        fields["HA_JSON"].Should().Be(At("notes/hA.json"));
        fields["HB_JSON"].Should().Be(At("notes/hB.json"));
        fields["HA_JSON"].Should().NotBe(fields["HB_JSON"], "two review steps with two verdict bindings are two files");
        fields["CODE_NOTES"].Should().Be(At("notes/code.md"));
        fields["HFIX_NOTES"].Should().Be(At("notes/hfix.md"));
        fields["NOTES"].Should().Be(fields["HFIX_NOTES"]);
        fields.Should().NotContainKeys("REVIEW", "FIX_NOTES", "GATE_JSON");
        fields.Should().ContainKeys("PLAN", "BASE", "BRANCH", "SCEN_COUNT", "DEADLINE", "KNOWN", "WORK");
        fields["SCEN_COUNT"].Should().Be("12");
    }

    [Fact]
    public void At_C_review_the_fields_are_the_code_notes_and_its_own_verdict_and_none_of_a_later_steps()
    {
        var review = Fields("C", 1, "review");

        review.Should().ContainKeys("CODE_NOTES", "REVIEW");
        review["CODE_NOTES"].Should().Be(At("notes/code.md"));
        review["REVIEW"].Should().Be(At("notes/review.json"));
        review["NOTES"].Should().Be("-");
        review.Should().NotContainKeys("FIX_NOTES", "GATE_NOTES", "GATE_JSON", "HREVIEW_NOTES", "HA_JSON");

        var gate = Fields("C", 3, "gate");

        gate.Should().ContainKeys("GATE_JSON", "GATE_NOTES", "CODE_NOTES", "REVIEW", "FIX_NOTES");
        gate["GATE_JSON"].Should().Be(At("notes/gate.json"), "gate.template.md names its own status file");
        gate["NOTES"].Should().Be(At("notes/gate.md"));
    }

    [Fact]
    public void A_tasks_entry_gives_its_three_steps_their_own_fields_and_later_steps_the_blocks_outputs()
    {
        var split = Fields("SRS", 0, "split");
        split.Should().ContainKey("NOTES");
        split["NOTES"].Should().Be(At("notes/split.md"));
        split.Should().ContainKey("WORK").And.NotContainKeys("PROBLEMS", "CODE_NOTES");

        var splitFix = Fields("SRS", 0, "splitFix");
        splitFix.Should().ContainKey("PROBLEMS");
        splitFix["NOTES"].Should().Be(At("notes/split.md"), "the split fix appends to the split's notes");
        splitFix["PROBLEMS"].Should().Be(BriefExample.Facts.Problems);

        var coder = Fields("SRS", 0, "taskCode");
        coder["NOTES"].Should().Be(Paths.TaskNotes(BriefExample.Facts.Task.Id));
        coder.Should().ContainKeys("TASK", "TASK_BRIEF", "TASK_FILES", "BEFORE");

        var reviewer = Fields("SRS", 1, "hreview");
        reviewer["CODE_NOTES"].Should().Be(At("notes/code.md"), "the block's own output, handed to the steps after it");
        reviewer.Should().ContainKey("SPLIT_NOTES");
    }

    [Fact]
    public void A_scheme_that_places_a_fix_before_the_reviews_it_reads_is_refused_by_the_rehearsal_naming_the_position_and_the_fields()
    {
        // hfix2 is hfix without its diffFrom: the file must still READ (hfix's diff comes from hA, which scheme X places after it), because placement of a brief's fields is the rehearsal's.
        var text = BriefExample.Edited(root =>
        {
            var hfix2 = root["steps"]!["hfix"]!.DeepClone().AsObject();
            hfix2.Remove("diffFrom");
            hfix2["outputs"] = new JsonObject { ["HFIXTWO_NOTES"] = "notes/hfix2.md" };
            hfix2["notes"] = "HFIXTWO_NOTES";
            root["steps"]!["hfix2"] = hfix2;
            root["schemes"]!["X"] = new JsonArray("1-code:code", "4-hfix:hfix2", "2-hA:hA", "3-hB:hB");
        });
        var experiment = ExperimentFile.Read(text, BriefExample.Location).Ok();
        var templates = BriefExample.Templates();

        var problems = BriefRehearsal.Problems(experiment, new RootPaths("/e"), 1, templates);

        problems.Should().ContainSingle().Which.Should().Be("hfix.template.md at schemes.X[1] (4-hfix): the brief has unfilled fields: {{HA_JSON}} {{HB_JSON}}");
        BriefRehearsal.Problems(BriefExample.Experiment, new RootPaths("/e"), 1, templates).Should().BeEmpty("the example places every field before the step that reads it");
        BriefRehearsal.StemsUsed(BriefExample.Experiment).Should().BeEquivalentTo(BriefTemplates.Shipped);
    }

    [Fact]
    public void The_fixed_names_are_exactly_the_fields_a_brief_is_filled_with_besides_its_bindings()
    {
        var filled = BriefExample.Fields("SRS", 0, "taskCode").Keys.Concat(BriefExample.Fields("SRS", 0, "splitFix").Keys).ToHashSet(StringComparer.Ordinal);
        var bound = BriefExample.Experiment.Steps.Values.SelectMany(step => step.Outputs).Select(binding => binding.Field).ToHashSet(StringComparer.Ordinal);

        filled.Except(bound).Should().BeEquivalentTo(BriefFields.FixedNames, "the list is derived from what the filler really builds, task fields and PROBLEMS included");
    }
}
