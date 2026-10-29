using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The experiment file is the experiment's facts, read strictly: every refusal is named, all of them at once, and the
/// one worked example under <c>tools/model-arms/examples</c> is the source every other case is derived from.
/// <para>
/// What this does NOT prove: that a path exists, that an executable resolves on a host, or that a scenarios file is readable —
/// Core opens nothing. Those are the console's checks (<c>validate</c>).
/// </para></summary>
public sealed class ExperimentFileTests
{
    private static readonly ExperimentLocation Loc = new("/x/root", "/x/home", string.Empty, "/x/repo");

    private static string ExampleText() =>
        File.ReadAllText(Path.Combine(Cli.Repository.Root, "tools", "model-arms", "examples", "part6.experiment.json"));

    /// <summary>The example as a node tree, edited and written back — never string surgery, so an edit cannot break the JSON by accident.</summary>
    private static string Edited(Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(ExampleText(), null, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip })!.AsObject();
        edit(root);
        return root.ToJsonString();
    }

    /// <summary>The reason an edited example is refused. Asserts the refusal first, so a file that is wrongly accepted fails on an
    /// assertion about that, not on a helper's exception.</summary>
    private static string Refused(Action<JsonObject> edit, ExperimentLocation? location = null)
    {
        var outcome = ExperimentFile.Read(Edited(edit), location ?? Loc);
        outcome.Failed().Should().BeTrue("the edited file must be refused");
        return outcome.Reason();
    }

    /// <summary>The worked example and the location are written with POSIX absolute paths; on Windows <c>/example/target</c> is not
    /// fully qualified, so these cases cannot mean what they say. An explicit, reasoned skip, never a silent red.</summary>
    private static void PosixPaths() =>
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the worked example and the location use POSIX absolute paths");

    private static IReadOnlyList<string> Lines(string reason) => reason.Split('\n');

    [Fact]
    public void A_complete_file_reads_into_facts_with_every_command_as_exe_and_argv()
    {
        PosixPaths();
        var experiment = ExperimentFile.Read(ExampleText(), Loc).Ok();

        experiment.Root.Should().Be("/x/root");
        experiment.Target.Build.Should().Equal("dotnet", "build", "dew_flow_connect_other_ais.slnx", "-c", "Debug", "-m:4", "--disable-build-servers");
        experiment.Target.Suite.Command[0].Should().Be("dotnet");
        experiment.Target.Base.Value.Should().Be("6786582e30d3e771549d05ed719478396f8099cd");
        experiment.Steps[StepKind.Review].Should().Be(new StepSpec(StepKind.Review, "opus", 40, StepMode.Read));
        experiment.Schemes.Select(s => s.Name).Should().Equal("C", "CR", "SRS");
        experiment.Schemes[2].Entries[0].Should().BeOfType<SchemeEntry.Tasks>();
        experiment.Schemes[0].Entries[1].Should().Be(new SchemeEntry.Step("2-review", StepKind.Review));
        experiment.Rounds.Should().HaveCount(3);
        experiment.Rounds[1].Should().Equal("C2", "CR2", "SRS2");
        experiment.Limits.KillGraceSeconds.Should().Be(600);
        experiment.Tasks.Should().Be(new TaskLimits(4, 7, 4));
        experiment.Coai.Should().Be(new CoaiFacts("/example/coai/coai-mcp", "/example/vault/connectOtherAis/wsl-ubuntu/settings.json", "wsl-ubuntu"));
        experiment.Awake.Should().Be(AwakeChoice.Auto);
    }

    [Fact]
    public void An_unknown_member_is_refused_by_name()
    {
        PosixPaths();
        var reason = Refused(root =>
        {
            root["target"]!["bulid"] = new JsonArray();
            root["colour"] = 1;
        });

        var lines = Lines(reason);
        lines.Should().Contain(line => line.Contains("target.bulid", StringComparison.Ordinal), "the misspelt member is named with its path");
        lines.Should().Contain(line => line.Contains("colour", StringComparison.Ordinal), "the second unknown member is named on its own line, not hidden behind the first");
        reason.Should().Contain("refused rather than ignored");
    }

    [Fact]
    public void A_round_naming_an_arm_of_an_unknown_scheme_is_refused_naming_both()
    {
        PosixPaths();
        var reason = Refused(root => root["rounds"] = new JsonArray(new JsonArray("C1", "XY2")));

        reason.Should().Contain("XY2").And.Contain("'XY'").And.Contain("C, CR, SRS");
    }

    [Fact]
    public void A_root_inside_a_git_checkout_is_refused()
    {
        PosixPaths();
        var reason = ExperimentFile.Read(ExampleText(), Loc with { EnclosingGitTop = "/x" }).Reason();

        reason.Should().Contain("inside the git checkout /x");
        ExperimentFile.Placement(Loc with { EnclosingGitTop = "/x" }).Should().ContainSingle("the other placement rule does not fire for a root outside this repository");
    }

    [Fact]
    public void A_root_inside_this_repository_is_refused()
    {
        PosixPaths();
        var reason = ExperimentFile.Read(ExampleText(), Loc with { Root = "/x/repo/exp" }).Reason();

        reason.Should().Contain("inside this repository");
        ExperimentFile.Placement(Loc).Should().BeEmpty("a root beside the repository, outside every checkout, is the legal placement");
    }

    [Fact]
    public void The_root_and_home_placeholders_expand_in_isolation_paths()
    {
        PosixPaths();
        var isolation = ExperimentFile.Read(ExampleText(), Loc).Ok().Isolation;

        isolation.Unlisted.Should().Contain(
            [
                "/x/root",
                Path.Combine("/x/root", "r"),
                Path.Combine("/x/home", ".claude", "projects"),
            ]);
        isolation.Unlisted.Should().NotContain(path => path.Contains('<') || path.Contains('~'), "every placeholder is expanded");
        isolation.Closed.Should().Equal("/example/old-experiment");
        isolation.Configured.Should().BeTrue();
    }

    [Fact]
    public void An_executable_under_the_target_or_the_root_is_refused_and_may_only_be_an_argument()
    {
        PosixPaths();
        const string underTarget = "/example/target/src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests";
        const string relative = "./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests";

        var absolute = Refused(root => root["target"]!["suite"]!["command"] = new JsonArray(underTarget));
        absolute.Should().Contain(underTarget).And.Contain("D17");

        var relativeReason = Refused(root => root["target"]!["suite"]!["command"] = new JsonArray(relative));
        relativeReason.Should().Contain(relative).And.Contain("relative path");

        var underRoot = Refused(root => root["target"]!["build"] = new JsonArray("/x/root/r/C1/tool"));
        underRoot.Should().Contain("/x/root/r/C1/tool").And.Contain("under '/x/root'");

        var atRoot = Refused(root => root["target"]!["lowPriority"] = new JsonArray("/x/root/x"));
        atRoot.Should().Contain("/x/root/x").And.Contain("under '/x/root'").And.Contain("D17");

        ExperimentFile.Read(Edited(_ => { }), Loc).Ok().Target.Suite.Command.Should().HaveCount(3, "the example's `dotnet <tests>.dll` form is the legal one");
    }

    [Fact]
    public void A_nul_character_in_a_path_field_or_an_executable_is_refused_naming_the_field_never_thrown()
    {
        PosixPaths();

        Refused(root => root["target"]!["build"] = new JsonArray("/usr/bin/dotnet\u0000x")).Should().Contain("target.build[0]").And.Contain("NUL");
        Refused(root => root["target"]!["source"] = "/example/target\u0000x").Should().Contain("target.source").And.Contain("NUL");
        Refused(root => root["target"]!["plan"] = "todo/PLAN\u0000.md").Should().Contain("target.plan").And.Contain("NUL");
        Refused(root => root["coai"]!["binary"] = "/example/coai\u0000/coai-mcp").Should().Contain("coai.binary").And.Contain("NUL");
        Refused(root => root["isolation"]!["closed"] = new JsonArray("<root>/a\u0000b")).Should().Contain("isolation.closed[0]").And.Contain("NUL");
    }

    [Fact]
    public void A_nul_character_in_any_text_is_refused_once_naming_the_field()
    {
        PosixPaths();
        var reason = Refused(root =>
        {
            root["target"]!["suite"]!["ctrfFile"] = "ctrf\u0000.json";
            root["target"]!["planBranch"] = "plan\u0000branch";
            root["target"]!["build"]!.AsArray()[1] = "build\u0000";
            root["target"]!["source"] = "/example/target\u0000x";
        });

        var lines = Lines(reason);
        foreach (var field in new[] { "target.suite.ctrfFile", "target.planBranch", "target.build[1]", "target.source" })
        {
            lines.Count(l => l.Contains(field, StringComparison.Ordinal) && l.Contains("NUL", StringComparison.Ordinal)).Should().Be(1, $"{field} is named once");
        }
    }

    [Fact]
    public void The_suite_runs_as_dotnet_with_the_test_assembly_as_an_argument()
    {
        PosixPaths();
        var suite = ExperimentFile.Read(ExampleText(), Loc).Ok().Target.Suite;

        suite.Command.Should().Equal("dotnet", "src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests.dll", "--report-xunit-ctrf");
        ExecutableRule.Refusal("dotnet", ["/example/target", "/x/root/base", "/x/root/r"]).Should().BeEmpty();
        ExecutableRule.Refusal("/example/target", ["/example/target"]).Should().NotBeEmpty("a root is under itself");
        ExecutableRule.Refusal("/example/target-other/tool", ["/example/target"]).Should().BeEmpty("a sibling that merely shares the prefix is not under the root");
    }

    [Fact]
    public void The_schema_member_is_required_and_must_be_exactly_model_arms_experiment_v1()
    {
        PosixPaths();
        Refused(root => root.Remove("schema")).Should().Contain("schema is missing");
        Refused(root => root["schema"] = "model-arms/experiment-v2").Should().Contain("this build reads exactly 'model-arms/experiment-v1'");
    }

    [Fact]
    public void A_missing_required_member_is_refused_by_name()
    {
        PosixPaths();
        var reason = Refused(root =>
        {
            root["target"]!.AsObject().Remove("base");
            root.Remove("limits");
        });

        reason.Should().Contain("target.base is missing").And.Contain("limits is missing");
    }

    [Fact]
    public void A_step_kind_this_build_does_not_know_is_refused_naming_the_legal_ones()
    {
        PosixPaths();
        var reason = Refused(root => root["steps"]!["lint"] = new JsonObject { ["model"] = "sonnet", ["minutes"] = 5, ["mode"] = "read" });

        reason.Should().Contain("steps.lint is not a step kind");
        foreach (var kind in Enum.GetValues<StepKind>())
        {
            reason.Should().Contain(StepKinds.Key(kind));
        }
    }

    [Fact]
    public void Comments_in_the_file_are_skipped()
    {
        PosixPaths();
        ExampleText().Should().Contain("// ", "the example carries comments, so this test would pass vacuously on a file without any");

        ExperimentFile.Read(ExampleText(), Loc).Failed().Should().BeFalse();
    }

    [Fact]
    public void A_number_or_a_choice_outside_its_legal_values_is_refused_naming_the_legal_ones()
    {
        PosixPaths();
        var reason = Refused(root =>
        {
            root["limits"]!["tries"] = 0;
            root["tasks"]!["min"] = 9;
            root["awake"] = "sometimes";
            root["steps"]!["code"]!["mode"] = "edit";
            root["steps"]!["fix"]!["minutes"] = 0;
        });

        reason.Should().Contain("limits.tries").And.Contain("tasks.min").And.Contain("tasks.max");
        reason.Should().Contain("auto, windows, wsl, none").And.Contain("write, read, gate");
        reason.Should().Contain("steps.fix.minutes");
    }

    [Fact]
    public void A_path_that_is_rooted_or_climbs_where_a_relative_one_is_required_is_refused()
    {
        PosixPaths();
        var reason = Refused(root =>
        {
            root["scenarios"] = "../scenarios.json";
            root["target"]!["plan"] = "/etc/plan.md";
            root["target"]!["nodeModules"] = new JsonArray("a/../b");
            root["target"]!["source"] = "target";
        });

        var lines = Lines(reason);
        lines.Should().Contain(line => line.StartsWith("scenarios ", StringComparison.Ordinal));
        lines.Should().Contain(line => line.StartsWith("target.plan ", StringComparison.Ordinal));
        lines.Should().Contain(line => line.StartsWith("target.nodeModules[0] ", StringComparison.Ordinal));
        lines.Should().Contain(line => line.StartsWith("target.source ", StringComparison.Ordinal) && line.Contains("absolute", StringComparison.Ordinal));
    }

    [Fact]
    public void A_base_that_is_not_a_full_commit_sha_is_refused_quoting_the_reason()
    {
        PosixPaths();
        Refused(root => root["target"]!["base"] = "6786582").Should().Contain("target.base").And.Contain("abbreviations are ambiguous");
    }

    [Fact]
    public void A_scheme_entry_of_an_undefined_step_or_an_unreadable_shape_is_refused()
    {
        PosixPaths();
        var reason = Refused(root =>
        {
            root["steps"]!.AsObject().Remove("gate");
            root["schemes"]!["C"]!.AsArray().Add("5 - review");
            root["schemes"]!["C1"] = new JsonArray();
        });

        reason.Should().Contain("schemes.C[3]").And.Contain("gate").And.Contain("schemes.C[4]").And.Contain("schemes.C1");
    }

    [Fact]
    public void Text_that_is_not_a_JSON_object_is_one_problem_and_not_an_exception()
    {
        PosixPaths();
        ExperimentFile.Read("{ \"schema\": ", Loc).Reason().Should().StartWith("experiment.json is not JSON:");
        ExperimentFile.Read("[]", Loc).Reason().Should().Contain("must be a JSON object");
        ExperimentFile.Read("null", Loc).Reason().Should().Contain("must be a JSON object");
        ExperimentFile.Read("{\"schema\": 1, \"schema\": 2}", Loc).Reason().Should().Contain("experiment.json is not JSON");
    }

    [Fact]
    public void The_optional_members_default_when_absent()
    {
        PosixPaths();
        var experiment = ExperimentFile.Read(Edited(root =>
        {
            root.Remove("tasks");
            root.Remove("coai");
            root.Remove("isolation");
        }), Loc).Ok();

        experiment.Tasks.Should().Be(TaskLimits.Shell);
        experiment.Coai.Should().Be(CoaiFacts.None);
        experiment.Coai.Configured.Should().BeFalse();
        experiment.Isolation.Should().Be(IsolationFacts.None);
        experiment.Isolation.Configured.Should().BeFalse();
    }

    [Fact]
    public void A_JSON_null_where_an_object_belongs_is_refused_naming_the_member()
    {
        PosixPaths();
        var reason = Refused(root =>
        {
            root["limits"] = null;
            root["target"] = null;
            root["steps"]!["code"] = null;
        });

        reason.Should().Contain("limits must be a JSON object").And.Contain("target must be a JSON object").And.Contain("steps.code must be a JSON object");
    }

    [Theory]
    [InlineData("limits")]
    [InlineData("tools")]
    [InlineData("tasks")]
    [InlineData("coai")]
    [InlineData("isolation")]
    [InlineData("steps")]
    [InlineData("schemes")]
    [InlineData("target.suite")]
    [InlineData("target.format")]
    [InlineData("target.extension")]
    public void Every_object_valued_member_refuses_a_JSON_null(string path)
    {
        PosixPaths();
        var segments = path.Split('.');

        var reason = Refused(root =>
        {
            var parent = (JsonNode)root;
            foreach (var segment in segments[..^1])
            {
                parent = parent[segment]!;
            }

            parent[segments[^1]] = null;
        });

        reason.Should().Contain($"{string.Join('.', segments)} must be ");
    }

    [Fact]
    public void An_executable_is_compared_with_the_forbidden_roots_without_regard_to_case_and_a_colon_makes_it_a_path()
    {
        PosixPaths();
        ExecutableRule.Refusal("/Example/Target/tool", ["/example/target"]).Should().Contain("under '/example/target'", "the comparison fails closed on a case-insensitive host");
        ExecutableRule.Refusal("c:tool.exe", ["/example/target"]).Should().Contain("relative path", "a name with a colon is a drive-relative path on Windows, never a bare name");
        ExecutableRule.Refusal("tool.exe", ["/example/target"]).Should().BeEmpty("the control: a bare name stays legal");
    }

    [Fact]
    public void A_windows_shaped_executable_is_refused_by_drive_and_case()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "drive letters and a case-insensitive file system exist only on Windows");

        ExecutableRule.Refusal(@"C:\Example\Target\Tool.exe", [@"c:\example\target"]).Should().Contain("under");
        ExecutableRule.Refusal(@"C:tool.exe", [@"c:\example\target"]).Should().Contain("relative path");
        ExecutableRule.Refusal(@"D:\elsewhere\tool.exe", [@"c:\example\target"]).Should().BeEmpty();
    }

    [Fact]
    public void An_empty_rounds_steps_or_schemes_is_refused_rather_than_read_as_an_experiment_with_nothing_to_run()
    {
        PosixPaths();
        Refused(root => root["rounds"] = new JsonArray()).Should().Contain("rounds must name at least one round");
        Refused(root => root["schemes"] = new JsonObject()).Should().Contain("schemes is empty");
        Refused(root => root["steps"] = new JsonObject()).Should().Contain("steps is empty");
    }

    [Fact]
    public void A_member_written_twice_is_refused_at_any_depth()
    {
        PosixPaths();
        var nested = ExampleText().Replace("\"model\": \"opus\"", "\"model\": \"opus\", \"model\": \"haiku\"", StringComparison.Ordinal);
        nested.Should().NotBe(ExampleText(), "the control: the edit must have changed the text");

        ExperimentFile.Read(nested, Loc).Reason().Should().StartWith("experiment.json is not JSON:").And.Contain("model");
    }
}
