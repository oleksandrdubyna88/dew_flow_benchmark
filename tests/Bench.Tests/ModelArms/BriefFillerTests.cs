using System.Globalization;
using System.Security.Cryptography;
using Bench.Domain;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Briefs;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The brief filler and the eleven templates it fills. The templates are read from the repository's
/// <c>tools/model-arms/briefs</c> folder, the one the console ships beside its binary, so a template edited without
/// its fields (or a field renamed without its template) fails here and not in the first live step.
/// <para>
/// What this does NOT prove: that a brief reads well to a model, that the deadline string has the format the
/// launcher will print (the launcher owns it), or that the console writes the filled text anywhere. Only that every
/// placeholder of every template has a value, that no value names an arm, a scheme or another arm's token, and that
/// the files are the part-6 harness's bytes.
/// </para></summary>
public sealed class BriefFillerTests
{
    private const string Own = BriefExample.Own;
    private const string Other = BriefExample.Other;
    private const string Root = BriefExample.Root;

    private static readonly string[] Schemes = ["C", "CR"];

    private static readonly ArmPaths Paths = BriefExample.Paths;

    private static readonly BriefLeakGuard Guard = new(
        ArmMap.Read($"{{\"C1\":\"{Own}\",\"CR1\":\"{Other}\"}}").Match(map => map, reason => throw new InvalidOperationException(reason)),
        "C1",
        Schemes,
        Root);

    private static readonly BriefFacts Facts = BriefExample.Facts;

    /// <summary>What the eleven files must hash to — typed out here on purpose, once, from the part-6 harness, so the
    /// copies in the repository cannot quietly agree with themselves.</summary>
    private static readonly Dictionary<string, string> ShellHashes = new()
    {
        ["1-code.template.md"] = "65b6b7aaa7733e1afc50e04ed9e6136f1c5a262998cd85adc5f23bee04bccacb",
        ["2-review.template.md"] = "abfdcce74ae696ba9d0689e158932f262df27d60bd28e622a8fcc4e1dd335631",
        ["3-fix.template.md"] = "4396dea47812456887a0bf47ad464dc773a914a20c86e3a274e7b832ef028cb4",
        ["gate.template.md"] = "7f4711f156df1d029298f2c113ca36b8e1b0e033fe5c686794163024ee437e0c",
        ["hr-review-fix.template.md"] = "04ecdfff5e8fc1baeef828544df91c01c29235303afffb40e3bdf75f4cac844d",
        ["t2-coder.template.md"] = "43136bc01d61a3fc920abbbff7b7c2b9cc3c1f16fbccfeacd1f484f8d1861984",
        ["x-split-fix.template.md"] = "ce256a919da26ae3c6bcb41aa1fe903e1d6cf768f24ef951ee9adb998a8b1e2b",
        ["x-split.template.md"] = "b4790d61b816265085dc9600439582e6f7bebfa5c16fb5785d81348caa294918",
        ["hA-trace.template.md"] = "971c664b5b29f1e4cc7778e7f5e658319ef2e8a944439cdcb5a26e595a8b862b",
        ["hB-tests.template.md"] = "a34fbe71a0e91cc005adb464f7cc14a9a91ce4507d00af60a2275fd989662cbc",
        ["hfix.template.md"] = "fb2ecc7c2c9611b2452cc64244ca7ecf0a9b2e6b155f292e6fe8942ba67a4b09",
    };

    /// <summary>Every (scheme, position, step id) the example places: 4 + 5 + (1 × 3 + 4) + 7 = 23 fills over the eleven stems.</summary>
    public static TheoryData<string, int, string> Placements => [.. BriefExample.Placements().Select(place => (place.Scheme, place.Position, place.Id))];

    private static string ShippedFolder => BriefExample.ShippedFolder;

    private static string ShippedTemplate(string stem) => File.ReadAllText(System.IO.Path.Combine(ShippedFolder, BriefTemplates.NameOf(stem).Ok()));

    private static string TextOf(Outcome<string> outcome) =>
        outcome.Match(text => text, reason => throw new Xunit.Sdk.XunitException($"expected a filled brief but it was refused: {reason}"));

    private static string ReasonOf(Outcome<string> outcome) =>
        outcome.Match(text => throw new Xunit.Sdk.XunitException($"expected a refusal but got a brief: {text}"), reason => reason);

    private static IReadOnlyDictionary<string, string> With(string name, string value) =>
        new Dictionary<string, string>(BriefExample.Fields("C", 0, "code")) { [name] = value };

    private static string Brief(string id) => BriefExample.Experiment.Steps[new StepId(id)].Brief;

    [Theory]
    [MemberData(nameof(Placements))]
    public void Every_shipped_template_fills_completely_with_its_steps_fields(string scheme, int position, string id)
    {
        var fields = BriefExample.Fields(scheme, position, id);

        var text = TextOf(BriefFiller.Fill(ShippedTemplate(Brief(id)), fields, Guard));

        text.Should().NotContain("{{", $"the {id} template at {scheme}[{position}] must have a value for every placeholder it carries");
        if (id == "code")
        {
            text.Should().Contain(Facts.Plan).And.Contain(System.IO.Path.Combine(Root, "r", Own, "notes", "code.md"));
        }

        if (id == "gate")
        {
            text.Should().Contain(System.IO.Path.Combine(Root, "r", Own, "notes", "gate.json"), "the gate's own status file is filled");
        }
    }

    [Fact]
    public void An_unfilled_field_is_refused_naming_it()
    {
        var reason = ReasonOf(BriefFiller.Fill("x {{PLAN}} {{NOPE}} {{NOPE}}", BriefExample.Fields("C", 0, "code"), Guard));

        reason.Should().EndWith("{{NOPE}}").And.Contain("unfilled");
        reason.Split("{{NOPE}}").Should().HaveCount(2, "the missing field is named once however often the template uses it");
    }

    [Fact]
    public void A_value_carrying_an_arm_name_a_scheme_or_a_token_is_refused()
    {
        ReasonOf(BriefFiller.Fill("{{KNOWN}}", With("KNOWN", "as C1 did"), Guard)).Should().Contain("KNOWN").And.Contain("'C1'");
        ReasonOf(BriefFiller.Fill("{{KNOWN}}", With("KNOWN", "the CR scheme"), Guard)).Should().Contain("'CR'");

        var intoOther = System.IO.Path.Combine(Root, "r", Other, "notes", "code.md");
        ReasonOf(BriefFiller.Fill("{{NOTES}}", With("NOTES", intoOther), Guard)).Should().Contain("NOTES").And.Contain($"'{Other}'");

        var intoOwn = System.IO.Path.Combine(Root, "r", Own, "notes", "code.md");
        TextOf(BriefFiller.Fill("{{NOTES}}", With("NOTES", intoOwn), Guard)).Should().Be(intoOwn, "an arm's own token is in every path field");

        TextOf(BriefFiller.Fill("no fields here", With("KNOWN", "as C1 did"), Guard)).Should().Be("no fields here",
            "a value that is never substituted is not checked");
    }

    [Fact]
    public void A_native_windows_root_does_not_make_its_drive_letter_a_scheme_name()
    {
        const string WindowsRoot = @"C:\e";
        var guard = Guard with { Root = WindowsRoot };
        var paths = new ArmPaths(WindowsRoot, Own);
        var facts = Facts with { Plan = WindowsRoot + @"\base\todo\PLAN_x.md" };

        var code = BriefExample.Fields("C", 0, "code", paths, facts);
        code.Should().ContainKey("CODE_NOTES");
        var text = TextOf(BriefFiller.Fill(ShippedTemplate("1-code"), code, guard));

        text.Should().Contain(code["CODE_NOTES"], "every path field begins with the root, and the root is the operator's own");
        var known = new Dictionary<string, string>(code) { ["KNOWN"] = "see C:" };
        ReasonOf(BriefFiller.Fill("{{KNOWN}}", known, guard)).Should().Contain("'C'", "only the exact root prefix is exempt, not the letter");
    }

    [Fact]
    public void A_value_carrying_a_placeholder_never_reaches_the_brief_and_is_never_expanded()
    {
        var reason = ReasonOf(BriefFiller.Fill("note: {{KNOWN}}", With("KNOWN", "see {{PLAN}} and {{NOPE}}"), Guard));

        reason.Should().Contain("{{PLAN}}").And.Contain("{{NOPE}}", "the result still holds placeholders, whichever field carried them");
    }

    [Fact]
    public void A_template_naming_a_scheme_is_refused_naming_the_template()
    {
        var reason = ReasonOf(BriefFiller.Fill("compare with the CR scheme: {{PLAN}}", BriefExample.Fields("C", 0, "code"), Guard, "2-review.template.md"));

        reason.Should().Contain("2-review.template.md").And.Contain("'CR'");
        TextOf(BriefFiller.Fill("plain text {{PLAN}}", BriefExample.Fields("C", 0, "code"), Guard, "x.template.md")).Should().Contain(Facts.Plan);
    }

    [Fact]
    public void An_override_that_exists_but_cannot_be_read_is_a_refusal_naming_it_never_the_shipped_file()
    {
        var folder = Directory.CreateTempSubdirectory("testsToDelete-briefs-");
        try
        {
            var shipped = Directory.CreateDirectory(System.IO.Path.Combine(folder.FullName, "shipped")).FullName;
            var overrides = Directory.CreateDirectory(System.IO.Path.Combine(folder.FullName, "overrides")).FullName;
            File.WriteAllText(System.IO.Path.Combine(shipped, "1-code.template.md"), "shipped text");
            var broken = System.IO.Path.Combine(overrides, "1-code.template.md");
            File.WriteAllBytes(broken, [0x41, 0xFF, 0xFE, 0x42]);

            var reason = ReasonOf(new BriefTemplateFiles(shipped, overrides).Read("1-code"));

            reason.Should().Contain(broken).And.Contain("UTF-8");
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void An_override_with_no_permission_to_read_it_is_a_refusal_naming_it()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("file modes are a POSIX notion");
            return;
        }

        var folder = Directory.CreateTempSubdirectory("testsToDelete-briefs-");
        try
        {
            var shipped = Directory.CreateDirectory(System.IO.Path.Combine(folder.FullName, "shipped")).FullName;
            var overrides = Directory.CreateDirectory(System.IO.Path.Combine(folder.FullName, "overrides")).FullName;
            File.WriteAllText(System.IO.Path.Combine(shipped, "1-code.template.md"), "shipped text");
            var locked = System.IO.Path.Combine(overrides, "1-code.template.md");
            File.WriteAllText(locked, "operator text");
            File.SetUnixFileMode(locked, UnixFileMode.None);
            if (CanRead(locked))
            {
                Assert.Skip("this account reads files whatever their mode (root), so the permission failure cannot be provoked");
            }

            ReasonOf(new BriefTemplateFiles(shipped, overrides).Read("1-code")).Should().Contain(locked);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void The_templates_are_exempt_from_line_ending_conversion_because_their_bytes_are_pinned()
    {
        var attributes = System.IO.Path.Combine(Cli.Repository.Root, ".gitattributes");

        File.Exists(attributes).Should().BeTrue("a checkout with autocrlf would otherwise change the bytes the hashes pin");
        File.ReadAllLines(attributes).Should().Contain(line => line.Trim() == "tools/model-arms/briefs/*.template.md -text");
    }

    private static bool CanRead(string path)
    {
        try
        {
            File.ReadAllBytes(path);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    [Fact]
    public void An_experiment_root_overrides_a_template_by_name()
    {
        var folder = Directory.CreateTempSubdirectory("testsToDelete-briefs-");
        try
        {
            var shipped = Directory.CreateDirectory(System.IO.Path.Combine(folder.FullName, "shipped")).FullName;
            var overrides = Directory.CreateDirectory(System.IO.Path.Combine(folder.FullName, "overrides")).FullName;
            File.WriteAllText(System.IO.Path.Combine(shipped, "1-code.template.md"), "A");
            File.WriteAllText(System.IO.Path.Combine(overrides, "1-code.template.md"), "B");
            File.WriteAllText(System.IO.Path.Combine(shipped, "2-review.template.md"), "only shipped — é");
            var files = new BriefTemplateFiles(shipped, overrides);

            TextOf(files.Read("1-code")).Should().Be("B");
            TextOf(files.Read("2-review")).Should().Be("only shipped — é");
            ReasonOf(files.Read("gate")).Should().Contain("gate.template.md").And.Contain(shipped).And.Contain(overrides);
            File.WriteAllText(System.IO.Path.Combine(overrides, "hC-extra.template.md"), "an addition");
            TextOf(files.Read("hC-extra")).Should().Be("an addition", "an experiment root's briefs folder may ADD a template a step names");
            BriefTemplates.NameOf("../x").Failed().Should().BeTrue();
            BriefTemplates.NameOf("a/b").Failed().Should().BeTrue();
            BriefTemplates.NameOf("hA-trace").Ok().Should().Be("hA-trace.template.md");
            BriefTemplates.NameOf("../x").Reason().Should().Be("'../x' is not a template stem — letters, digits and dashes, no separator and no '..' (hA-trace names hA-trace.template.md)");
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void Each_placed_step_gets_its_notes_path_and_every_visible_output()
    {
        string At(string relative) => Paths.NotesFile(new OutputBinding("X", relative));
        var expectedNotes = new Dictionary<string, string>
        {
            ["code"] = At("notes/code.md"),
            ["hreview"] = At("notes/hreview.md"),
            ["review"] = "-",
            ["fix"] = At("notes/fix.md"),
            ["gate"] = At("notes/gate.md"),
            ["split"] = At("notes/split.md"),
            ["splitFix"] = At("notes/split.md"),
            ["taskCode"] = Paths.TaskNotes("t3"),
            ["hA"] = "-",
            ["hB"] = "-",
            ["hfix"] = At("notes/hfix.md"),
        };

        var placements = BriefExample.Placements();
        placements.Should().HaveCount(23, "the scan must still reach every placement, or it proves nothing");
        foreach (var (scheme, position, id) in placements)
        {
            var fields = BriefExample.Fields(scheme, position, id);

            fields["NOTES"].Should().Be(expectedNotes[id], $"{id} at {scheme}[{position}]");
            fields.Should().ContainKeys("PLAN", "BASE", "BRANCH", "SCEN_COUNT", "DEADLINE", "KNOWN", "WORK");
            fields.ContainsKey("PROBLEMS").Should().Be(id == "splitFix", $"PROBLEMS belongs to the split fix alone, not {id}");
            new[] { "TASK", "TASK_BRIEF", "TASK_FILES", "BEFORE" }.Select(fields.ContainsKey).Should().AllBeEquivalentTo(id == "taskCode");
            fields["SCEN_COUNT"].Should().Be(Facts.ScenarioCount.ToString(CultureInfo.InvariantCulture));
            fields["WORK"].Should().Be(Paths.Work);
        }

        BriefExample.Fields("SRS", 0, "taskCode").Should().Contain("TASK", "t3").And.Contain("BEFORE", Facts.Task.Before)
            .And.Contain("TASK_BRIEF", Facts.Task.Brief).And.Contain("TASK_FILES", Facts.Task.Files);
        BriefExample.Fields("SRS", 0, "splitFix").Should().Contain("PROBLEMS", Facts.Problems);
    }

    [Fact]
    public void The_eleven_shipped_templates_are_the_shells_byte_for_byte()
    {
        BriefTemplates.Shipped.Select(stem => BriefTemplates.NameOf(stem).Ok()).Should().BeEquivalentTo(ShellHashes.Keys, "one template per shipped stem, and no stem without a hash here");
        Directory.EnumerateFiles(ShippedFolder, "*.template.md").Select(System.IO.Path.GetFileName).Should().BeEquivalentTo(ShellHashes.Keys,
            "the scan must still find the eleven, and nothing beside them");

        foreach (var (name, expected) in ShellHashes)
        {
            var actual = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(System.IO.Path.Combine(ShippedFolder, name))));
            actual.Should().Be(expected, $"{name} must be the part-6 harness's file, not an edited copy");
        }
    }
}
