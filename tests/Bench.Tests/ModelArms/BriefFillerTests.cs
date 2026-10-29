using System.Globalization;
using System.Security.Cryptography;
using Bench.Domain;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Briefs;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The brief filler and the eight templates it fills. The templates are read from the repository's
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
    private const string Own = "00000000000000c1";
    private const string Other = "00000000000000c2";
    private const string Root = "/e";

    private static readonly string[] Schemes = ["C", "CR"];

    private static readonly ArmPaths Paths = new(Root, Own);

    private static readonly BriefLeakGuard Guard = new(
        ArmMap.Read($"{{\"C1\":\"{Own}\",\"CR1\":\"{Other}\"}}").Match(map => map, reason => throw new InvalidOperationException(reason)),
        "C1",
        Schemes,
        Root);

    private static readonly BriefFacts Facts = new(
        Plan: "/e/base/todo/PLAN_x.md",
        Base: "1111111111111111111111111111111111111111",
        Branch: "feat/x",
        ScenarioCount: 12,
        Deadline: "12:30",
        Known: "the known failures are listed in the plan",
        Task: new TaskFacts("t3", "/e/r/" + Own + "/work/t3.md", "src/A.cs src/B.cs", "2222222222222222222222222222222222222222"),
        Problems: "the plan splits one file across two tasks");

    /// <summary>What the eight files must hash to — typed out here on purpose, once, from the part-6 harness, so the
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
    };

    public static TheoryData<StepKind> Kinds => [.. Enum.GetValues<StepKind>()];

    private static string ShippedFolder => System.IO.Path.Combine(Cli.Repository.Root, "tools", "model-arms", "briefs");

    private static string ShippedTemplate(StepKind kind) => File.ReadAllText(System.IO.Path.Combine(ShippedFolder, BriefTemplates.NameOf(kind)));

    private static string TextOf(Outcome<string> outcome) =>
        outcome.Match(text => text, reason => throw new Xunit.Sdk.XunitException($"expected a filled brief but it was refused: {reason}"));

    private static string ReasonOf(Outcome<string> outcome) =>
        outcome.Match(text => throw new Xunit.Sdk.XunitException($"expected a refusal but got a brief: {text}"), reason => reason);

    private static IReadOnlyDictionary<string, string> With(string name, string value) =>
        new Dictionary<string, string>(BriefFields.For(StepKind.Code, Paths, Facts)) { [name] = value };

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Every_shipped_template_fills_completely_with_its_steps_fields(StepKind kind)
    {
        var fields = BriefFields.For(kind, Paths, Facts);

        var text = TextOf(BriefFiller.Fill(ShippedTemplate(kind), fields, Guard));

        text.Should().NotContain("{{", $"the {kind} template must have a value for every placeholder it carries");
        if (kind == StepKind.Code)
        {
            text.Should().Contain(Facts.Plan).And.Contain(System.IO.Path.Combine(Root, "r", Own, "notes", "code.md"));
        }
    }

    [Fact]
    public void An_unfilled_field_is_refused_naming_it()
    {
        var reason = ReasonOf(BriefFiller.Fill("x {{PLAN}} {{NOPE}} {{NOPE}}", BriefFields.For(StepKind.Code, Paths, Facts), Guard));

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

        var text = TextOf(BriefFiller.Fill(ShippedTemplate(StepKind.Code), BriefFields.For(StepKind.Code, paths, facts), guard));

        text.Should().Contain(paths.CodeNotes, "every path field begins with the root, and the root is the operator's own");
        var known = new Dictionary<string, string>(BriefFields.For(StepKind.Code, paths, facts)) { ["KNOWN"] = "see C:" };
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
        var reason = ReasonOf(BriefFiller.Fill("compare with the CR scheme: {{PLAN}}", BriefFields.For(StepKind.Code, Paths, Facts), Guard, "2-review.template.md"));

        reason.Should().Contain("2-review.template.md").And.Contain("'CR'");
        TextOf(BriefFiller.Fill("plain text {{PLAN}}", BriefFields.For(StepKind.Code, Paths, Facts), Guard, "x.template.md")).Should().Contain(Facts.Plan);
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

            var reason = ReasonOf(new BriefTemplateFiles(shipped, overrides).Read(StepKind.Code));

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

            ReasonOf(new BriefTemplateFiles(shipped, overrides).Read(StepKind.Code)).Should().Contain(locked);
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

            TextOf(files.Read(StepKind.Code)).Should().Be("B");
            TextOf(files.Read(StepKind.Review)).Should().Be("only shipped — é");
            ReasonOf(files.Read(StepKind.Gate)).Should().Contain("gate.template.md").And.Contain(shipped).And.Contain(overrides);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void Each_step_kind_gets_its_notes_file()
    {
        BriefFields.For(StepKind.Review, Paths, Facts)["NOTES"].Should().Be("-", "the shell passed a dash for the review step");
        BriefFields.For(StepKind.SplitFix, Paths, Facts)["NOTES"].Should().Be(Paths.SplitNotes);
        BriefFields.For(StepKind.TaskCode, Paths, Facts)["NOTES"].Should().Be(Paths.TaskNotes("t3"));
        BriefFields.For(StepKind.Code, Paths, Facts)["NOTES"].Should().Be(Paths.CodeNotes);
        BriefFields.For(StepKind.HReview, Paths, Facts)["NOTES"].Should().Be(Paths.HReviewNotes);
        BriefFields.For(StepKind.Fix, Paths, Facts)["NOTES"].Should().Be(Paths.FixNotes);
        BriefFields.For(StepKind.Gate, Paths, Facts)["NOTES"].Should().Be(Paths.GateNotes);
        BriefFields.For(StepKind.Split, Paths, Facts)["NOTES"].Should().Be(Paths.SplitNotes);

        foreach (var kind in Enum.GetValues<StepKind>())
        {
            var fields = BriefFields.For(kind, Paths, Facts);
            fields.Should().ContainKeys("PLAN", "BASE", "BRANCH", "SCEN_COUNT", "DEADLINE", "KNOWN", "REVIEW", "CODE_NOTES", "HREVIEW_NOTES",
                "FIX_NOTES", "WORK", "GATE_JSON", "NOTES");
            fields.ContainsKey("PROBLEMS").Should().Be(kind == StepKind.SplitFix, $"PROBLEMS belongs to SplitFix alone, not {kind}");
            new[] { "TASK", "TASK_BRIEF", "TASK_FILES", "BEFORE" }.Select(fields.ContainsKey).Should().AllBeEquivalentTo(kind == StepKind.TaskCode);
        }

        var common = BriefFields.For(StepKind.Code, Paths, Facts);
        common["SCEN_COUNT"].Should().Be(Facts.ScenarioCount.ToString(CultureInfo.InvariantCulture));
        common["REVIEW"].Should().Be(Paths.ReviewJson);
        common["WORK"].Should().Be(Paths.Work);
        common["GATE_JSON"].Should().Be(Paths.GateJson);
        common["HREVIEW_NOTES"].Should().Be(Paths.HReviewNotes);
        BriefFields.For(StepKind.TaskCode, Paths, Facts).Should().Contain("TASK", "t3").And.Contain("BEFORE", Facts.Task.Before)
            .And.Contain("TASK_BRIEF", Facts.Task.Brief).And.Contain("TASK_FILES", Facts.Task.Files);
        BriefFields.For(StepKind.SplitFix, Paths, Facts).Should().Contain("PROBLEMS", Facts.Problems);
    }

    [Fact]
    public void The_eight_shipped_templates_are_the_shells_byte_for_byte()
    {
        BriefTemplates.All.Should().BeEquivalentTo(ShellHashes.Keys, "one template per step kind, and no kind without a hash here");
        Directory.EnumerateFiles(ShippedFolder, "*.template.md").Select(System.IO.Path.GetFileName).Should().BeEquivalentTo(ShellHashes.Keys,
            "the scan must still find the eight, and nothing beside them");

        foreach (var (name, expected) in ShellHashes)
        {
            var actual = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(System.IO.Path.Combine(ShippedFolder, name))));
            actual.Should().Be(expected, $"{name} must be the part-6 harness's file, not an edited copy");
        }
    }
}
