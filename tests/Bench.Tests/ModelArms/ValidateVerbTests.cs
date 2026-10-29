using System.Text.Json.Nodes;
using Bench.Cli;
using Bench.Domain;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using Xunit;
using ArmsProgram = ModelArms.Program;
using ModelArmsApp = ModelArms;
using GateSeams = ModelArms.GateSeams;
using VerbInput = ModelArms.VerbInput;

namespace Bench.Tests.ModelArms;

/// <summary>The <c>validate</c> verb and <c>prep-base</c>'s guard (D16, D17 c, D18, D23): every check a root must pass on this host, printed once, with
/// every refused member named, and the executables printed as the REAL paths the harness will launch.
/// <para>
/// What this does NOT prove: that the fabricated <c>git</c>, <c>claude</c>, <c>dotnet</c>, <c>node</c> and <c>npm</c> of the fixture run (they are empty
/// files; only their resolution is exercised), that the git facts hold for a source owned by another user or one that git refuses as unsafe (git
/// answers 128, which this verb reads as "no" and names the fact), that the facts are read with the <c>git</c> that was resolved (the source is asked
/// through the process's own PATH, not the experiment's), any filtered PATH for the children at launch (E2), or any of it on Windows, where the
/// POSIX tests skip.
/// </para></summary>
public sealed class ValidateVerbTests
{
    private static async Task<(int Code, string Output, string Error)> RunAsync(ExperimentRoot root, string verb = "validate")
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await ArmsProgram.RunAsync([verb, "--experiment", root.Root], output, error, root.Environment, TestContext.Current.CancellationToken);
        return (code, output.ToString(), error.ToString());
    }

    private static string Join(params string[] entries) => string.Join(Path.PathSeparator, entries);

    private static string Bin(ExperimentRoot root) => Path.Combine(root.Temp, "bin");

    private static string TextOf(ExperimentRoot root, params string[] path) =>
        path.Aggregate(
            JsonNode.Parse(File.ReadAllText(Path.Combine(root.Root, "experiment.json")), documentOptions: new() { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })!,
            (node, key) => node[key]!).GetValue<string>();

    private static void Posix()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the fixture's executables, links and modes are POSIX");
    }

    private static void Link(string link, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Assert.Skip($"symbolic links cannot be made here: {ex.Message}");
        }
    }

    [Fact]
    public async Task Validate_names_every_refused_member_in_one_answer()
    {
        using var root = new ExperimentRoot();
        root.Edit(top =>
        {
            top["target"]!["bulid"] = 1;
            top["colour"] = 1;
            top.AsObject().Remove("limits");
        });

        var (code, _, error) = await RunAsync(root);

        code.Should().Be(4);
        error.Should().Contain("bulid").And.Contain("colour").And.Contain("limits");
    }

    [Fact]
    public async Task Validate_prints_the_resolved_executables_name_to_absolute_path()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();

        var (code, output, error) = await RunAsync(root);

        error.Should().BeEmpty("a valid root prints no refusal");
        code.Should().Be(0);
        foreach (var name in new[] { "claude", "git" })
        {
            var real = RootFacts.RealPathOf(Path.Combine(Bin(root), name)).Ok();
            output.Should().Contain($"  {name} → {real}", "the harness launches and prints the real path, never the configured name");
        }

        output.Should().Contain("resolved executables:").And.Contain("is valid on this host");
    }

    [Fact]
    public async Task Prep_base_is_refused_before_validate_passes_and_when_a_baseline_exists()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        var claude = Path.Combine(Bin(root), "claude");
        File.Delete(claude);

        var missing = await RunAsync(root, "prep-base");

        missing.Code.Should().Be(3);
        missing.Error.Should().Contain("claude is not on PATH");

        root.Executable("bin", "claude");
        var baseline = root.Write("baseline.json", "{}");
        var exists = await RunAsync(root, "prep-base");

        exists.Code.Should().Be(4);
        exists.Error.Should().Contain("baseline.json already exists");

        File.Delete(baseline);
        var neither = await RunAsync(root, "prep-base");

        neither.Code.Should().Be(4);
        neither.Error.Should().Contain("E4.S4");
        neither.Output.Should().Contain("resolved executables:", "the guard runs the same checks and prints them the same way");
    }

    [Fact]
    public async Task Every_configured_executable_resolves_on_this_host_or_is_refused_by_name()
    {
        Posix();
        using var absent = await new ExperimentRoot().WithTargetAsync();
        absent.Edit(top => top["target"]!["lowPriority"]![0] = "no-such-node-7f3a");

        var missing = await RunAsync(absent);

        missing.Code.Should().Be(3, "a CLI the host lacks is the environment (D16)");
        missing.Error.Should().Contain("no-such-node-7f3a");

        using var inside = await new ExperimentRoot().WithTargetAsync();
        var underTarget = inside.Executable("example", "target", "bin", "x");
        inside.Edit(top => top["target"]!["suite"]!["command"]![0] = underTarget);

        var refused = await RunAsync(inside);

        refused.Code.Should().Be(4, "an executable under the target is a wrong configuration");
        refused.Error.Should().Contain("D17");
    }

    [Fact]
    public async Task A_configured_executable_that_is_really_a_file_of_the_target_is_a_configuration_error_not_the_environment()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        var inTarget = root.Executable("example", "target", "bin", "claude");
        var linked = Path.Combine(root.Temp, "linked", "claude");
        Link(linked, inTarget);
        root.UsePath(Join(Path.GetDirectoryName(linked)!, Bin(root)));

        var (code, _, error) = await RunAsync(root);

        code.Should().Be(4, "claude is on PATH, as a link into the target: the operator's PATH is wrong, the host is not unready");
        error.Should().Contain("D17").And.Contain(inTarget);
    }

    [Fact]
    public async Task The_plan_file_must_exist_at_the_base_commit_of_the_source()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        var sha = TextOf(root, "target", "base");
        root.Edit(top => top["target"]!["plan"] = "todo/missing.md");

        var planMissing = await RunAsync(root);

        planMissing.Code.Should().Be(4);
        planMissing.Error.Should().Contain($"does not exist at {sha}").And.Contain("todo/missing.md");

        root.Edit(top =>
        {
            top["target"]!["plan"] = "todo/PLAN_a_spent_quota_cools_the_model_down.md";
            top["target"]!["base"] = new string('0', 40);
        });

        var baseMissing = await RunAsync(root);

        baseMissing.Code.Should().Be(4);
        baseMissing.Error.Should().Contain("is not in");
        baseMissing.Error.Should().NotContain("does not exist at", "the plan is only asked of a commit that exists");
    }

    [Fact]
    public async Task The_scenarios_file_must_hold_at_least_one_scenario_with_unique_ids()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        root.Write("scenarios.json", "[]");

        var empty = await RunAsync(root);

        empty.Code.Should().Be(4);
        empty.Error.Should().Contain("holds no scenario");

        root.Write("scenarios.json", """[{"id":"S1","kind":"cs","name":"A"},{"id":"S1","kind":"node","name":"B"}]""");

        var twice = await RunAsync(root);

        twice.Code.Should().Be(4);
        twice.Error.Should().Contain("S1");
    }

    [Fact]
    public async Task A_gate_step_requires_the_coai_binary_and_settings_to_exist()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        var settings = TextOf(root, "coai", "settings");
        File.Delete(settings);

        var gone = await RunAsync(root);

        gone.Code.Should().Be(4);
        gone.Error.Should().Contain(settings);

        root.Edit(top => top.AsObject().Remove("coai"));

        var unconfigured = await RunAsync(root);

        unconfigured.Code.Should().Be(4);
        unconfigured.Error.Should().Contain("coai is not configured");
    }

    [Fact]
    public async Task A_KNOWN_that_names_a_scheme_is_refused_before_the_chain_starts_naming_the_field_and_the_template()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        root.Edit(top => top["known"] = "Check the C suite before the rest (C is slow).");

        var (code, _, error) = await RunAsync(root);

        code.Should().Be(4);
        error.Should().Contain("KNOWN").And.Contain("1-code.template.md").And.Contain("'C'");
        error.Should().Contain("D15");
    }

    [Fact]
    public async Task An_override_template_that_names_a_scheme_is_refused_naming_it()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        var shipped = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "briefs", "1-code.template.md"));
        root.Write(Path.Combine("briefs", "1-code.template.md"), shipped + "\nDo the work (C).\n");

        var (code, _, error) = await RunAsync(root);

        code.Should().Be(4);
        error.Should().Contain("1-code.template.md").And.Contain("'C'");
    }

    [Fact]
    public async Task A_template_the_schemes_use_that_cannot_be_found_is_refused_naming_it()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        root.Write(Path.Combine("briefs", "gate.template.md"), "a brief with {{NO_SUCH_FIELD}}");

        var (code, _, error) = await RunAsync(root);

        code.Should().Be(4);
        error.Should().Contain("gate.template.md").And.Contain("{{NO_SUCH_FIELD}}");
    }

    [Fact]
    public async Task A_PATH_entry_under_the_target_is_refused_naming_the_entry_and_D17()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        var inTarget = root.Executable("example", "target", "bin", "mynode");
        var entry = Path.GetDirectoryName(inTarget)!;
        root.UsePath(Join(entry, Bin(root)));

        var (code, _, error) = await RunAsync(root);

        code.Should().Be(4);
        error.Should().Contain($"PATH entry '{entry}'").And.Contain("D17");
    }

    [Fact]
    public async Task A_PATH_entry_that_is_a_link_into_the_target_is_refused_by_its_real_path()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        var inTarget = Path.GetDirectoryName(root.Executable("example", "target", "bin", "mynode"))!;
        var entry = Path.Combine(root.Temp, "shortcut");
        Link(entry, inTarget);
        root.UsePath(Join(entry, Bin(root)));

        var (code, _, error) = await RunAsync(root);

        code.Should().Be(4);
        error.Should().Contain($"PATH entry '{entry}'").And.Contain(inTarget).And.Contain("D17");
    }

    [Theory]
    [InlineData("", "empty entry")]
    [InlineData("relative/bin", "relative")]
    public async Task A_PATH_with_an_empty_or_relative_entry_is_refused_naming_it(string entry, string kind)
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        root.UsePath(Join(entry, Bin(root)));

        var (code, _, error) = await RunAsync(root);

        code.Should().Be(4);
        error.Should().Contain("PATH").And.Contain(kind).And.Contain("D17");
        if (entry.Length > 0)
        {
            error.Should().Contain($"'{entry}'");
        }
    }

    [Fact]
    public async Task A_PATH_of_clean_absolute_entries_outside_the_roots_is_not_refused()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        var elsewhere = root.Executable("elsewhere", "tool");
        root.UsePath(Join(Path.GetDirectoryName(elsewhere)!, Path.Combine(root.Temp, "no-such-folder"), Bin(root)));

        var (code, _, error) = await RunAsync(root);

        error.Should().BeEmpty("an entry that does not exist is skipped by every shell, and one outside both roots is fine");
        code.Should().Be(0);
    }

    [Fact]
    public async Task A_scenarios_file_swapped_between_its_trust_check_and_its_read_is_refused_with_exit_4()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        var scenarios = Path.Combine(root.Root, "scenarios.json");
        var seams = GateSeams.Real with
        {
            AfterRead = path =>
            {
                if (path == scenarios && !OperatingSystem.IsWindows())
                {
                    var other = Path.Combine(root.Root, "other.json");
                    File.WriteAllText(other, """[{"id":"S9","kind":"cs","name":"Swapped"}]""");
                    File.SetUnixFileMode(other, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                    File.Move(other, scenarios, overwrite: true);
                }

                return Task.CompletedTask;
            },
        };

        var (code, error) = await ThroughValidateAsync(root, seams);

        code.Should().Be(4);
        error.Should().Contain("scenarios.json changed between its trust check and its read").And.Contain("inode");
    }

    [Fact]
    public async Task A_scenarios_file_that_stays_as_it_was_judged_is_read_once_and_validates()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        var reads = new List<string>();
        var seams = GateSeams.Real with { AfterRead = path => { reads.Add(Path.GetFileName(path)); return Task.CompletedTask; } };

        var (code, error) = await ThroughValidateAsync(root, seams);

        error.Should().BeEmpty();
        code.Should().Be(0);
        reads.Should().Contain("scenarios.json", "validate looks at scenarios.json again after reading it, through the gate's own seam");
    }

    [Fact]
    public async Task The_gate_hands_the_verb_the_facts_it_judged_scenarios_json_by()
    {
        using var root = new ExperimentRoot();

        var passed = await root.PassAsync("validate");

        passed.ExitCode.Should().Be(0);
        var judged = passed.Calls.Should().ContainSingle().Subject.JudgedScenarios;
        judged.Should().ContainSingle().Which.Path.Should().Be(Path.Combine(root.Root, "scenarios.json"));
    }

    [Fact]
    public async Task A_verb_handed_no_facts_for_scenarios_json_refuses_rather_than_read_it_unjudged()
    {
        using var root = new ExperimentRoot();
        var admitted = (await root.PassAsync("validate")).Calls.Single() with { JudgedScenarios = [] };

        var code = await ModelArmsApp.Verbs.ValidateVerb.RunAsync(admitted);

        code.Should().Be(3);
        ((StringWriter)admitted.Verb.Error).ToString().Should().Contain("scenarios.json").And.Contain("unjudged");
    }

    [Fact]
    public void Every_step_kind_the_example_runs_has_its_template_rehearsed()
    {
        var kinds = ModelArmsApp.Core.Briefs.BriefRehearsal.KindsUsed(Example(top => { }));

        kinds.Should().BeEquivalentTo(Enum.GetValues<StepKind>(), "the example's schemes between them run every kind; a kind added without a template fails here");
    }

    [Fact]
    public void The_host_kind_is_windows_wsl_or_other_posix_from_the_facts()
    {
        HostKinds.From(true, string.Empty, string.Empty).Should().Be(HostKind.Windows);
        HostKinds.From(false, "Ubuntu", string.Empty).Should().Be(HostKind.Wsl);
        HostKinds.From(false, string.Empty, "Linux version 6.18 (root@x) 5.15.0 #1 SMP microsoft-standard-WSL2").Should().Be(HostKind.Wsl);
        HostKinds.From(false, string.Empty, "Linux version 6.18 Microsoft").Should().Be(HostKind.Wsl, "the match ignores case");
        HostKinds.From(false, string.Empty, "Linux version 6.8 generic").Should().Be(HostKind.OtherPosix);
        HostKinds.From(true, "Ubuntu", "microsoft").Should().Be(HostKind.Windows, "Windows is decided first");
    }

    [Fact]
    public void A_host_with_every_fact_good_has_no_problem()
    {
        foreach (var host in Enum.GetValues<HostKind>().Where(host => host != HostKind.Windows))
        {
            var experiment = Example(top => { });

            HostChecks.Decide(experiment, Good(experiment, host)).Should().BeEmpty($"the fabricated facts of a {host} host are all good — the cases below change one");
        }
    }

    [Theory]
    [InlineData("windows-with-isolation", "POSIX only")]
    [InlineData("awake-wsl-off-wsl", "needs a Wsl host")]
    [InlineData("awake-windows-off-windows", "needs a Windows host")]
    [InlineData("tasks-without-splitfix", "splitFix")]
    [InlineData("arm-named-twice", "named twice")]
    public void An_unsupported_host_configuration_is_refused_naming_it(string name, string expected)
    {
        var (experiment, host) = name switch
        {
            "windows-with-isolation" => (Example(top => { }), HostKind.Windows),
            "awake-wsl-off-wsl" => (Example(top => top["awake"] = "wsl"), HostKind.OtherPosix),
            "awake-windows-off-windows" => (Example(top => top["awake"] = "windows"), HostKind.Wsl),
            "tasks-without-splitfix" => (Example(top => top["steps"]!.AsObject().Remove("splitFix")), HostKind.Wsl),
            "arm-named-twice" => (Example(top => top["rounds"] = new JsonArray(new JsonArray("C1", "C1"))), HostKind.Wsl),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "a case this theory does not build"),
        };

        var problems = HostChecks.Decide(experiment, Good(experiment, host));

        problems.Should().ContainSingle(problem => problem.Text.Contains(expected, StringComparison.Ordinal));
        problems.Should().OnlyContain(problem => !problem.Environment, "a configuration the host cannot run is a wrong configuration (exit 4)");
    }

    [Fact]
    public void The_executables_an_experiment_needs_are_distinct_in_the_order_the_harness_reaches_for_them()
    {
        var experiment = Example(top => { });

        HostChecks.NeededExecutables(experiment, HostKind.OtherPosix).Should().Equal(
            "git", "claude", "dotnet", "node", "npm", experiment.Coai.Binary);
        HostChecks.NeededExecutables(experiment, HostKind.Wsl).Should().Contain("pwsh.exe", "awake auto on WSL keeps the machine awake through PowerShell");
        HostChecks.NeededExecutables(Example(top => top["awake"] = "none"), HostKind.Wsl).Should().NotContain("pwsh.exe");
        HostChecks.NeededExecutables(Example(top => top["awake"] = "wsl"), HostKind.OtherPosix).Should().Contain("pwsh.exe");
        HostChecks.NeededExecutables(Example(top => top["steps"]!["gate"]!["mode"] = "write"), HostKind.OtherPosix)
            .Should().NotContain(experiment.Coai.Binary, "coai is needed only when a step is a gate step");
    }

    [Fact]
    public void Every_problem_is_returned_at_once_with_the_environment_ones_marked()
    {
        var experiment = Example(top => { });
        var good = Good(experiment, HostKind.Wsl);
        var facts = good with
        {
            Executables =
            [
                .. good.Executables.Select(
                    executable => executable.Name == "claude" ? executable with { Path = Outcome<string>.Failure("claude is not on PATH on this host") } : executable),
            ],
            SourceExists = false,
            SourceIsGit = false,
            Scenarios = Outcome<IReadOnlyList<Scenario>>.Failure("scenarios.json holds no scenario"),
            CoaiSettingsExists = false,
        };

        var problems = HostChecks.Decide(experiment, facts);

        problems.Should().Contain(p => p.Environment && p.Text == "claude is not on PATH on this host");
        problems.Should().Contain(p => !p.Environment && p.Text.Contains("does not exist", StringComparison.Ordinal) && p.Text.Contains(experiment.Target.Source, StringComparison.Ordinal));
        problems.Should().Contain(p => !p.Environment && p.Text == "scenarios.json holds no scenario");
        problems.Should().Contain(p => !p.Environment && p.Text.Contains(experiment.Coai.Settings, StringComparison.Ordinal));
        problems.Should().HaveCount(4, "the git facts of a source that does not exist are not asked, so they are not reported");
    }

    private static async Task<(int Code, string Error)> ThroughValidateAsync(ExperimentRoot root, GateSeams seams)
    {
        var line = ModelArmsCommandLine.Split(["validate", "--experiment", root.Root]).Ok();
        var error = new StringWriter();
        var input = new VerbInput(
            line.Verb, line.Positionals, CommandLine.Parse(line.Rest), new StringWriter(), error, root.Environment,
            ModelArmsApp.ModuleFiles.Beside(AppContext.BaseDirectory), TestContext.Current.CancellationToken);

        var code = await ModelArmsApp.ExperimentGate.RunAsync(input, admitted => ModelArmsApp.Verbs.ValidateVerb.RunAsync(admitted, seams), seams);
        return (code, error.ToString());
    }

    /// <summary>The worked example, edited, read through the real reader so the experiment is one the reader accepts.</summary>
    private static Experiment Example(Action<JsonNode> change)
    {
        var path = Path.Combine(Cli.Repository.Root, "tools", "model-arms", "examples", "part6.experiment.json");
        var moved = File.ReadAllText(path).Replace("/example/", Path.GetTempPath().Replace('\\', '/').TrimEnd('/') + "/example/", StringComparison.Ordinal);
        var tree = JsonNode.Parse(moved, documentOptions: new() { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })!;
        change(tree);
        var location = new ExperimentLocation(Path.Combine(Path.GetTempPath(), "example", "root"), Path.Combine(Path.GetTempPath(), "example", "home"), string.Empty, string.Empty);
        return ExperimentFile.Read(tree.ToJsonString(), location).Ok();
    }

    /// <summary>Facts in which everything is fine for <paramref name="host"/>: every needed executable resolved, the source a git checkout holding
    /// the branch, the base and the plan, one scenario, the coai files present.</summary>
    private static HostFacts Good(Experiment experiment, HostKind host) =>
        new(
            host,
            [.. HostChecks.NeededExecutables(experiment, host).Select(name => new ResolvedExecutable(name, Outcome<string>.Success("/usr/bin/" + name)))],
            SourceExists: true,
            SourceIsGit: true,
            PlanBranchExists: true,
            BaseExists: true,
            PlanAtBase: true,
            Scenarios: Outcome<IReadOnlyList<Scenario>>.Success([new Scenario("S1", "cs", "A")]),
            CoaiBinaryExists: true,
            CoaiSettingsExists: true);
}
