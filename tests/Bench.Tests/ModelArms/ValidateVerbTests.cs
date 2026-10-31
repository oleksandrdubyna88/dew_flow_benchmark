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

/// <summary>The <c>validate</c> verb and the checks <c>prep-base</c> runs first (D16, D17 c, D18, D23): every check a root must pass on this host, printed once, with
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

        neither.Code.Should().Be(4, "the checks pass and prep-base goes on: the fixture's target has no rules submodule, a wrong configuration (the baseline itself is PrepBaseTests')");
        neither.Error.Should().Contain("target.rulesSubmodule .agents/conventions is not a submodule of the target at");
        neither.Output.Should().Contain("resolved executables:", "prep-base runs the same checks and prints them the same way");
    }

    /// <summary>D23 names <c>npm</c> and <c>prep-base</c> runs <c>npm ci</c> whenever <c>nodeModules</c> is set — whatever the extension's own command is (E3 §8 item 7).</summary>
    [Fact]
    public async Task Npm_is_needed_when_node_modules_are_configured()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        root.Edit(top => top["target"]!["extension"]!["command"] = new JsonArray("node", "--test"));
        File.Delete(Path.Combine(Bin(root), "npm"));

        var missing = await RunAsync(root);

        missing.Code.Should().Be(3, "an npm the host lacks is the environment (D16)");
        missing.Error.Should().Contain("target.nodeModules npm");

        root.Executable("bin", "npm");
        root.Edit(top => top["target"]!["nodeModules"] = new JsonArray());
        File.Delete(Path.Combine(Bin(root), "npm"));

        var unneeded = await RunAsync(root);

        unneeded.Code.Should().Be(0, "no node_modules to restore, no npm to resolve");
        unneeded.Error.Should().BeEmpty();
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
        error.Should().Contain("KNOWN").And.Contain("1-code.template.md at schemes.C[0] (1-code):").And.Contain("'C'");
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
        error.Should().Contain("1-code.template.md at schemes.").And.Contain("'C'");
    }

    [Fact]
    public async Task An_override_with_a_field_no_step_provides_is_refused_naming_it_and_its_position()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        root.Write(Path.Combine("briefs", "gate.template.md"), "a brief with {{NO_SUCH_FIELD}}");

        var (code, _, error) = await RunAsync(root);

        code.Should().Be(4);
        error.Should().Contain("gate.template.md at schemes.C[3] (4-gate):").And.Contain("{{NO_SUCH_FIELD}}");
    }

    [Fact]
    public async Task A_step_whose_brief_stem_exists_nowhere_is_refused_naming_the_template_the_step_and_its_position()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        root.Edit(top => top["steps"]!["code"]!["brief"] = "nowhere");

        var (code, _, error) = await RunAsync(root);

        code.Should().Be(4);
        error.Should().Contain("nowhere.template.md at schemes.C[0] (1-code): step code:").And.Contain("no brief template 'nowhere.template.md'");
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
    public async Task A_target_config_that_names_programs_is_read_but_none_of_them_runs()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        var target = Path.Combine(root.Temp, "example", "target");
        var cfg = root.Shim(Path.Combine(root.Temp, "shims", "cfg"), "cfg");
        var hooks = Path.Combine(target, "hooks");
        foreach (var hook in new[]
        {
            "applypatch-msg", "pre-applypatch", "post-applypatch", "pre-commit", "pre-merge-commit", "prepare-commit-msg", "commit-msg", "post-commit", "pre-rebase",
            "post-checkout", "post-merge", "pre-push", "pre-receive", "update", "post-receive", "post-update", "push-to-checkout", "pre-auto-gc", "post-rewrite",
            "fsmonitor-watchman", "reference-transaction", "proc-receive",
        })
        {
            root.Shim(Path.Combine(hooks, hook), "cfg");
        }

        File.AppendAllText(
            Path.Combine(target, ".git", "config"),
            $"""

            [core]
                fsmonitor = {cfg}
                pager = {cfg}
                editor = {cfg}
                sshCommand = {cfg}
                askPass = {cfg}
                alternateRefsCommand = {cfg}
                hooksPath = {hooks}
            [diff]
                external = {cfg}
            [filter "x"]
                clean = {cfg}
                smudge = {cfg}
            [credential]
                helper = {cfg}
            [gpg]
                program = {cfg}
            [sequence]
                editor = {cfg}
            [mergetool "x"]
                cmd = {cfg}
            [difftool "x"]
                cmd = {cfg}
            [uploadpack]
                packObjectsHook = {cfg}
            [alias]
                rev-parse = !{cfg}
            [pager]
                rev-parse = {cfg}

            """);

        var (code, output, error) = await RunAsync(root);

        error.Should().BeEmpty();
        code.Should().Be(0, "the config is read and the source is asked: " + output);
        root.Markers().Should().BeEmpty("no program a key of the target's config names ran for rev-parse");
    }

    [Fact]
    public async Task The_source_is_asked_through_the_git_that_resolved_without_the_parents_GIT_variables()
    {
        Posix();
        Assert.SkipUnless(File.Exists("/usr/bin/env"), "the shim dumps its environment with /usr/bin/env, by absolute path (PATH is the test's only entry)");
        using var root = await new ExperimentRoot().WithTargetAsync();
        var git = Path.Combine(Bin(root), "git");
        File.Delete(git);
        root.ShimScript(
            git,
            $"echo \"$1\" >> '{root.MarkerFile("git")}'\n/usr/bin/env >> '{root.MarkerFile("git.env")}'\nexec '{ExperimentRoot.RealGit}' \"$@\"\n");
        root.WithVariables(("GIT_DIR", "/nowhere"), ("GIT_CEILING_DIRECTORIES", "/"), ("GIT_CONFIG_GLOBAL", "/nowhere"));

        var (code, output, error) = await RunAsync(root);

        error.Should().BeEmpty();
        code.Should().Be(0);
        File.ReadAllLines(root.MarkerFile("git")).Should().Equal("-c", "rev-parse", "rev-parse", "rev-parse", "rev-parse");
        var seen = File.ReadAllLines(root.MarkerFile("git.env"));
        seen.Should().Contain(line => line.StartsWith("PATH=", StringComparison.Ordinal), "the dump saw git's environment at all — a test that saw nothing would pass vacuously");
        seen.Where(line => line.StartsWith("GIT_", StringComparison.Ordinal) && line != "GIT_NO_LAZY_FETCH=1" && line != "GIT_ALLOW_PROTOCOL=" && !System.Text.RegularExpressions.Regex.IsMatch(line, @"^GIT_CONFIG_(COUNT|KEY_\d+|VALUE_\d+)=")).Should().BeEmpty("no GIT_ variable of the parent reaches git: the version question carries the harness's own switch and allow-list, as MakeVerbTests asserts");
        output.Should().Contain($"  git → {git}");
    }

    [Fact]
    public async Task A_git_that_cannot_answer_is_the_environment_naming_its_words()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        var git = Path.Combine(Bin(root), "git");
        File.Delete(git);
        root.ShimScript(git, $"for a; do :; done\n[ \"$a\" = --version ] && exec '{ExperimentRoot.RealGit}' \"$@\"\necho 'fatal: detected dubious ownership in repository' >&2\nexit 128\n");

        var (code, _, error) = await RunAsync(root);

        code.Should().Be(3);
        error.Should().Contain("git could not answer about target.source").And.Contain("dubious ownership").And.NotContain("is not a git repository");
    }

    [Fact]
    public async Task An_absent_git_leaves_the_source_unchecked_and_says_so()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        File.Delete(Path.Combine(Bin(root), "git"));

        var (code, _, error) = await RunAsync(root);

        code.Should().Be(3);
        error.Should().Contain("git is not on PATH").And.Contain("the source was not checked: git is absent").And.NotContain("is not a git repository");
    }

    [Fact]
    public async Task The_source_is_not_asked_while_PATH_is_refused()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        var git = Path.Combine(Bin(root), "git");
        File.Delete(git);
        root.Shim(git, "git", thenRealGit: true);
        root.UsePath(Join(Bin(root), "relative"));

        var (code, _, error) = await RunAsync(root);

        code.Should().Be(4);
        error.Should().Contain("PATH entry 'relative'").And.Contain("the source was not checked: PATH is refused");
        root.Markers().Should().NotContain("git", "git looks its helpers up in PATH, so it is not started while PATH is refused");
    }

    [Theory]
    [InlineData("HOME", "h/.gitconfig", "HOME")]
    [InlineData("XDG_CONFIG_HOME", "x/git/config", "XDG_CONFIG_HOME")]
    public async Task A_home_the_target_could_write_is_refused_and_git_is_not_asked_there(string variable, string configFile, string named)
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        var git = Path.Combine(Bin(root), "git");
        File.Delete(git);
        root.Shim(git, "git", thenRealGit: true);
        var trace = root.MarkerFile("trace2");
        var config = Path.Combine(root.Temp, "example", "target", configFile);
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        File.WriteAllText(config, $"[trace2]\n\teventTarget = {trace}\n");
        root.WithVariables((variable, Path.Combine(root.Temp, "example", "target", configFile.Split('/')[0])));

        var (code, _, error) = await RunAsync(root);

        code.Should().Be(4);
        error.Should().Contain($"{named} '").And.Contain("D17").And.Contain($"the source was not checked: {named} is refused");
        File.Exists(trace).Should().BeFalse("git reads its global config from there — here it would append trace2 events to a file the target chose");
        root.Markers().Should().NotContain("git");
    }

    [Fact]
    public async Task A_refused_PATH_is_named_before_a_refused_home()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        root.UsePath(Join(Bin(root), "relative"));
        root.WithVariables(("HOME", Path.Combine(root.Temp, "example", "target", "h")));

        var (code, _, error) = await RunAsync(root);

        code.Should().Be(4);
        error.Should().Contain("the source was not checked: PATH is refused").And.Contain("HOME '").And.NotContain("HOME is refused");
    }

    [Fact]
    public void A_refused_environment_folder_is_a_finding_of_the_configuration()
    {
        var experiment = Example(top => { });
        var facts = Good(experiment, HostKind.OtherPosix) with { EnvironmentRefusals = ["HOME '/t/h' is under '/t' — … (D17)"] };

        HostChecks.Decide(experiment, facts).Should().ContainSingle().Which.Should().Be(new HostProblem("HOME '/t/h' is under '/t' — … (D17)", Environment: false));
    }

    [Fact]
    public async Task A_source_that_is_a_plain_folder_inside_a_checkout_is_not_a_git_repository()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        var sub = Path.Combine(root.Temp, "example", "target", "sub");
        Directory.CreateDirectory(sub);
        root.Edit(top => top["target"]!["source"] = sub);

        var (code, _, error) = await RunAsync(root);

        code.Should().Be(4);
        error.Should().Contain("is not a git repository").And.Contain(sub);
    }

    [Fact]
    public void An_absent_executable_is_the_environment_and_a_refused_one_the_configuration_by_its_kind_not_its_words()
    {
        var experiment = Example(top => { });
        var good = Good(experiment, HostKind.OtherPosix);
        ResolvedExecutable Named(string name, ExecutableAnswer answer) => new(new NeededExecutable(string.Empty, name), answer);

        var absent = HostChecks.Decide(experiment, good with { Executables = [Named("x", new ExecutableAnswer.Absent("x (D17) y"))] });
        var refused = HostChecks.Decide(experiment, good with { Executables = [Named("x", new ExecutableAnswer.Refused("plain words"))] });

        absent.Should().ContainSingle().Which.Should().Be(new HostProblem("x (D17) y", Environment: true), "the host lacks it, whatever the sentence says");
        refused.Should().ContainSingle().Which.Should().Be(new HostProblem("plain words", Environment: false), "the configuration names it, whatever the sentence says");
    }

    [Fact]
    public async Task A_missing_coai_binary_is_named_by_its_field_with_exit_3()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        var missing = Path.Combine(root.Temp, "example", "coai", "no-such");
        root.Edit(top => top["coai"]!["binary"] = missing);

        var (code, _, error) = await RunAsync(root);

        code.Should().Be(3);
        error.Should().Contain($"coai.binary {missing} does not exist").And.NotContain("cannot be resolved");
    }

    [Fact]
    public async Task Every_absent_executable_is_named_by_its_role()
    {
        Posix();
        using var root = await new ExperimentRoot().WithTargetAsync();
        root.Edit(top => top["target"]!["suite"]!["command"]![0] = "/opt/no-such-dir/tool");
        File.Delete(Path.Combine(Bin(root), "claude"));

        var (code, _, error) = await RunAsync(root);

        code.Should().Be(3);
        error.Should().Contain("  - target.suite.command[0] /opt/no-such-dir/tool does not exist");
        error.Should().Contain("  - claude is not on PATH on this host", "the three the harness always needs name themselves, with no role");
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
    public void Every_template_the_example_names_is_rehearsed_at_every_position()
    {
        var stems = ModelArmsApp.Core.Briefs.BriefRehearsal.StemsUsed(Example(top => { }));

        stems.Should().BeEquivalentTo(ModelArmsApp.Core.Briefs.BriefTemplates.Shipped, "the example's schemes between them place a step of every shipped template; a template added without a step fails here");
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
    public void An_unsupported_host_configuration_is_refused_naming_it(string name, string expected)
    {
        var (experiment, host) = name switch
        {
            "windows-with-isolation" => (Example(top => { }), HostKind.Windows),
            "awake-wsl-off-wsl" => (Example(top => top["awake"] = "wsl"), HostKind.OtherPosix),
            "awake-windows-off-windows" => (Example(top => top["awake"] = "windows"), HostKind.Wsl),
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

        static IEnumerable<string> Names(Experiment e, HostKind host) => HostChecks.NeededExecutables(e, host).Select(needed => needed.Name);

        Names(experiment, HostKind.OtherPosix).Should().Equal("git", "claude", "dotnet", "node", "npm", experiment.Coai.Binary);
        Names(experiment, HostKind.Wsl).Should().Contain("pwsh.exe", "awake auto on WSL keeps the machine awake through PowerShell");
        Names(Example(top => top["awake"] = "none"), HostKind.Wsl).Should().NotContain("pwsh.exe");
        Names(Example(top => top["awake"] = "wsl"), HostKind.OtherPosix).Should().Contain("pwsh.exe");
        Names(Example(WithoutGateSteps), HostKind.OtherPosix)
            .Should().NotContain(experiment.Coai.Binary, "coai is needed only when a scheme places a gate step");
        HostChecks.NeededExecutables(experiment, HostKind.OtherPosix).Select(needed => needed.Role).Should().Equal(
            string.Empty, string.Empty, string.Empty, "target.lowPriority[0]", "target.extension.command[0]", "coai.binary");
    }

    /// <summary>The example with the gate step taken out of every scheme: a gate step defined under <c>steps</c> that no scheme places needs no coai.</summary>
    private static void WithoutGateSteps(JsonNode top)
    {
        foreach (var scheme in top["schemes"]!.AsObject().Select(member => member.Value!.AsArray()))
        {
            foreach (var entry in scheme.Where(entry => entry!.GetValue<string>().EndsWith(":gate", StringComparison.Ordinal)).ToList())
            {
                scheme.Remove(entry);
            }
        }
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
                    executable => executable.Needed.Name == "claude" ? executable with { Answer = new ExecutableAnswer.Absent("claude is not on PATH on this host") } : executable),
            ],
            Source = new SourceAnswer.Missing(),
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

    internal static async Task<(int Code, string Error)> ThroughValidateAsync(ExperimentRoot root, GateSeams seams)
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
            [.. HostChecks.NeededExecutables(experiment, host).Select(needed => new ResolvedExecutable(needed, new ExecutableAnswer.Found("/usr/bin/" + needed.Name)))],
            Outcome<IReadOnlyList<Scenario>>.Success([new Scenario("S1", "cs", "A")]),
            CoaiSettingsExists: true)
        {
            Source = new SourceAnswer.Asked(true, true, true, true),
            Version = new GitVersionAnswer.Read("git version 2.45.0"), // the floor itself: a floor off by one is seen here too
            PathRefusals = [],
            EnvironmentRefusals = [],
            Templates = new Dictionary<string, Outcome<string>>(),
        };
}
