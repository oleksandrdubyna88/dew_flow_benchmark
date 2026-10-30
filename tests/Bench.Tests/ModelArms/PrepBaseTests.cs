using Bench.Domain.Targets;
using FluentAssertions;
using ModelArms.Core.Experiment;
using Xunit;
using ModelArmsApp = ModelArms;
using PrepBaseSeams = ModelArms.Verbs.PrepBaseSeams;

namespace Bench.Tests.ModelArms;

/// <summary><c>prep-base</c> (plan D17 c, d; D18; D24; E3.S2): the baseline from both suites at <c>base</c>, over the scratch target of <see cref="TargetBench"/> — a real git, a real node for the target's own wrapper, recording shims for <c>dotnet</c> and <c>npm</c>. The subject is never doubled.
/// <para>
/// What this does NOT prove: that a real <c>dotnet</c> or <c>npm</c> prints what the shims print (the pilot, M2, is the first real run); that a tool the target's code starts under the shim is launched by a path (the shims start nothing); a Windows host (POSIX shims); or that the baseline is TRUE of the target — it is
/// measured, under the operator's rights, never authenticated (E3 §8 item 17).
/// </para></summary>
public sealed class PrepBaseTests
{
    private static readonly string[] Stripped = ["BENCH_DB", "GIT_DIR", "GITHUB_TOKEN", "CLAUDE_CODE_OAUTH_TOKEN", "COAI_CREDS_KEY"];

    private static void Secrets(TargetBench bench) =>
        bench.Root.WithVariables(
            ("BENCH_DB", "Host=db;Password=x"), ("GIT_DIR", "/nonexistent/.git"), ("GITHUB_TOKEN", "ghp_value"), ("CLAUDE_CODE_OAUTH_TOKEN", "oauth-value"), ("COAI_CREDS_KEY", "vault-key"), ("KEEP_ME", "yes"));

    private static string Last(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries)[^1];

    private static bool IsBuild(RecordedCall call) => call.Tool == "dotnet" && call.Arguments is ["build", ..];

    private static bool IsSuite(RecordedCall call) => call.Tool == "dotnet" && call.Arguments.Contains("--results-directory");

    [Fact]
    public async Task Baseline_json_carries_the_base_the_host_the_failures_and_the_totals_of_both_suites()
    {
        using var bench = await TargetBench.CreateAsync();
        var now = new DateTimeOffset(2026, 10, 30, 12, 30, 15, TimeSpan.Zero);

        var (code, output, error) = await bench.PrepBaseAsync(new PrepBaseSeams(new BenchClock(now, TimeZoneInfo.Utc), null));

        code.Should().Be(0, $"{error}\n{output}");
        File.Exists(bench.BaselineFile).Should().BeTrue("a baseline exists only from two suites that completed");
        var baseline = BaselineFile.Read(File.ReadAllText(bench.BaselineFile)).Ok();
        baseline.Base.Value.Should().Be(bench.Experiment.Target.Base.Value).And.Be(bench.BaseSha);
        baseline.Host.Should().StartWith(System.Environment.MachineName).And.EndWith(")", "the machine and its kind");
        baseline.MeasuredAt.Should().Be(now);
        baseline.McpFailed.Should().Equal(["ATests.Fails"], "the prefix Tgt.Tests. is taken off");
        baseline.TsFailed.Should().Equal(["b"]);
        baseline.SuiteTotal.Should().Be(12);
        baseline.ExtensionTotal.Should().Be(2);
        output.Should().Contain("baseline written");
    }

    [Fact]
    public async Task Every_tool_has_its_own_log_and_the_stage_log_has_the_shells_shape()
    {
        using var bench = await TargetBench.CreateAsync();
        var root = bench.Root.Root;

        var (code, _, error) = await bench.PrepBaseAsync();

        code.Should().Be(0, error);
        File.Exists(Path.Combine(root, "prep-base.log")).Should().BeTrue("a line per stage is kept in the shell's file");
        var stages = File.ReadAllLines(Path.Combine(root, "prep-base.log"));
        stages.Select(line => line.Split(' ')[0]).Should().Equal("npm", "build", "npm", "mcp", "ts", "DONE");
        stages[0].Should().MatchRegex(@"^npm ci rc=0 s=\d+$");
        stages[1].Should().MatchRegex($@"^build rc=0 s=\d+ head={bench.BaseSha[..7]}\w*$");
        stages[3].Should().MatchRegex(@"^mcp tests rc=2 s=\d+$");
        stages[4].Should().MatchRegex(@"^ts tests rc=1 s=\d+$");
        File.ReadAllText(Path.Combine(root, "base-build.log")).Should().Contain("Build succeeded.");
        File.ReadAllText(Path.Combine(root, "base-test.log")).Should().Contain("total: 12");
        File.ReadAllText(Path.Combine(root, "base-ts-test.log")).Should().Contain("ℹ tests 2");
        File.Exists(Path.Combine(root, "base-npm-1.log")).Should().BeTrue();
        File.Exists(Path.Combine(root, "base-npm-2.log")).Should().BeTrue("one log per npm ci, numbered across both phases");
        File.Exists(Path.Combine(root, "base-results", "mcp.ctrf.json")).Should().BeTrue("the suite wrote its CTRF where --results-directory said");
    }

    [Theory]
    [InlineData("a main suite that printed no summary", "main suite")]
    [InlineData("a main suite that outlives its wall", "main suite")]
    [InlineData("an extension that printed no summary", "extension suite")]
    [InlineData("an extension that exits 1 and names nothing", "extension suite")]
    public async Task A_suite_that_did_not_run_to_completion_refuses_the_baseline_instead_of_writing_an_empty_list(string which, string named)
    {
        using var bench = await TargetBench.CreateAsync();
        var seams = PrepBaseSeams.Real;
        switch (which)
        {
            case "a main suite that printed no summary":
                bench.Say("suite.out", "Build succeeded.\nno summary here\n");
                bench.Say("suite.rc", "0\n");
                break;
            case "a main suite that outlives its wall":
                bench.Say("suite.sleep", "30\n");
                seams = seams with { SuiteWall = TimeSpan.FromSeconds(1) };
                break;
            case "an extension that printed no summary":
                bench.Say("ext.out", "✔ a (1ms)\n");
                bench.Say("ext.rc", "0\n");
                break;
            default:
                bench.Say("ext.out", "ℹ tests 3\n");
                bench.Say("ext.rc", "1\n");
                break;
        }

        var (code, _, error) = await bench.PrepBaseAsync(seams);

        File.Exists(bench.BaselineFile).Should().BeFalse("a baseline with an empty failure list would claim the suite passed (absent is not zero)");
        code.Should().Be(3, error);
        error.Should().Contain($"the {named}").And.Contain("no baseline");
        if (which.Contains("wall", StringComparison.Ordinal))
        {
            File.ReadAllText(Path.Combine(bench.Root.Root, "prep-base.log")).Should().Contain("mcp tests rc=killed", "a tool killed at its wall has no exit code, never a zero");
        }
    }

    [Fact]
    public async Task Prep_base_requires_validate_and_an_absent_baseline()
    {
        using var bench = await TargetBench.CreateAsync();
        var branch = bench.Experiment.Target.PlanBranch;

        bench.Root.Write("baseline.json", "{}");
        var present = await bench.PrepBaseAsync();

        present.Code.Should().Be(4);
        present.Error.Should().Contain("baseline.json already exists");
        bench.Recorded().Should().BeEmpty("a root the gate refuses launches nothing, not even a question to git");
        File.Delete(bench.BaselineFile);

        bench.Root.Edit(top => top["target"]!["planBranch"] = "<fill me>");
        var unfilled = await bench.PrepBaseAsync();

        unfilled.Code.Should().Be(4);
        unfilled.Error.Should().Contain("<fill me>");
        bench.Recorded().Should().BeEmpty("an unfilled template is refused by the gate before any launch");

        bench.Root.Edit(top =>
        {
            top["target"]!["planBranch"] = branch;
            top["target"]!["base"] = new string('0', 40);
        });
        var problem = await bench.PrepBaseAsync();

        problem.Code.Should().Be(4, "validate's own finding ends the verb");
        problem.Error.Should().Contain("is not in");
        problem.Output.Should().Contain("resolved executables:");
        bench.Recorded().Where(call => call.Tool != "git").Should().BeEmpty("no build, no npm, no wrapper");
        bench.Recorded().Where(call => call.Arguments.Contains("clone")).Should().BeEmpty("nothing is cloned for a root validate refused");
        Directory.Exists(bench.BaseFolder).Should().BeFalse();
    }

    [Fact]
    public async Task Every_target_tool_is_launched_by_its_resolved_path_under_a_built_environment()
    {
        using var bench = await TargetBench.CreateAsync();
        Secrets(bench);

        var (code, output, error) = await bench.PrepBaseAsync();

        code.Should().Be(0, error);
        var printed = TargetBench.Printed(output);
        var calls = bench.Recorded();
        var script = Path.Combine(bench.BaseFolder, ".agents", "conventions", "tools", "low-priority.mjs");
        foreach (var tool in new[] { "dotnet", "npm" })
        {
            calls.Where(call => call.Tool == tool).Should().NotBeEmpty();
            calls.Where(call => call.Tool == tool).Select(call => call.Executable).Should().OnlyContain(path => path == printed[tool], $"every {tool} is started by the REAL path validate printed, never by a name");
        }

        var wrapped = calls.Where(call => call.Tool == "wrapper").ToList();
        wrapped.Should().HaveCount(3, "the build, the suite and the extension are lowered; npm and git are not");
        wrapped.Select(call => call.NodeStartedAs).Should().OnlyContain(path => path == printed["node"], "the node that runs the target's wrapper is started by the REAL path validate printed, never by the name `node` (the runtime would look that up itself)");
        wrapped.Select(call => call.NodeExecPath).Should().OnlyContain(path => path == printed["node"]);
        wrapped.Select(call => call.Executable).Should().OnlyContain(path => path == script, "the wrapper is the target's own script, absolute under the clone");
        wrapped.Select(call => call.Arguments[0]).Should().OnlyContain(path => path == printed["dotnet"] || path == printed["npm"], "the wrapper is handed the resolved executable, not its name");
        foreach (var call in calls.Where(call => call.Tool is "wrapper" or "dotnet" or "npm"))
        {
            call.EnvironmentNames.Should().NotContain(Stripped, $"{call.Tool}'s environment is built: no secret and no GIT_* of the parent's").And.Contain(["KEEP_ME", "PATH", "HOME"]);
            call.EnvironmentNames.Should().NotContain(name => name.StartsWith("GIT_", StringComparison.Ordinal));
        }

        var git = bench.HarnessGit();
        git.Should().HaveCountGreaterThanOrEqualTo(5, "clone, detach, head, submodule paths, url, update and the short head");
        git.Select(call => call.Executable).Should().OnlyContain(path => path == printed["git"]);
        foreach (var call in git)
        {
            call.EnvironmentNames.Where(name => name.StartsWith("GIT_", StringComparison.Ordinal)).Should().Equal(["GIT_NO_LAZY_FETCH"], "git runs without any GIT_* of the parent's; the one it is given is the harness's own");
            call.EnvironmentNames.Should().NotContain(["BENCH_DB", "GITHUB_TOKEN", "CLAUDE_CODE_OAUTH_TOKEN", "COAI_CREDS_KEY"]);
        }
    }

    [Fact]
    public async Task The_base_is_the_configured_commit_and_its_rules_submodule_comes_from_the_local_source()
    {
        using var bench = await TargetBench.CreateAsync(planBranchAhead: true);
        bench.HeadSha.Should().NotBe(bench.BaseSha, "the plan branch is ahead of base, or the test cannot tell a detach from a clone");

        var (code, _, error) = await bench.PrepBaseAsync();

        code.Should().Be(0, error);
        (await TargetBench.GitAsync(bench.BaseFolder, "rev-parse", "HEAD")).Trim().Should().Be(bench.BaseSha, "the base is the configured commit, not the branch's head");
        File.Exists(Path.Combine(bench.BaseFolder, ".agents", "conventions", "tools", "low-priority.mjs")).Should().BeTrue("the rules submodule is updated");
        File.Exists(Path.Combine(bench.BaseFolder, "ahead.txt")).Should().BeFalse();
        var urls = await TargetBench.GitAsync(bench.BaseFolder, "config", "--get-regexp", @"^submodule\..*\.url$");
        urls.Trim().Should().EndWith($" {bench.Source}/.agents/conventions", "the url is the operator's own checkout of the rules, never the one .gitmodules names");
    }

    [Fact]
    public async Task A_base_that_does_not_build_writes_no_baseline_and_exits_3()
    {
        using var bench = await TargetBench.CreateAsync();
        bench.Say("build.rc", "1\n");

        var (code, _, error) = await bench.PrepBaseAsync();

        code.Should().Be(3);
        error.Should().Contain($"the base does not build at {bench.BaseSha}").And.Contain("base-build.log").And.Contain("no baseline");
        File.Exists(bench.BaselineFile).Should().BeFalse();
        bench.Recorded().Should().NotContain(call => IsSuite(call), "nothing after the build runs");
        bench.Recorded().Where(call => call.Tool == "npm" && call.Arguments.Contains("test")).Should().BeEmpty();
        Last(File.ReadAllText(Path.Combine(bench.Root.Root, "prep-base.log"))).Should().StartWith("build rc=1 ");
    }

    [Fact]
    public async Task A_failed_attempts_logs_are_moved_aside_and_the_retry_writes_fresh_ones()
    {
        using var bench = await TargetBench.CreateAsync();
        var root = bench.Root.Root;
        var clock = new BenchClock(new DateTimeOffset(2026, 10, 30, 12, 30, 15, TimeSpan.Zero), TimeZoneInfo.Utc);
        bench.Say("build.out", "the first attempt's build\n");
        bench.Say("build.rc", "1\n");

        var first = await bench.PrepBaseAsync(new PrepBaseSeams(clock, null));

        first.Code.Should().Be(3);
        var firstBuild = File.ReadAllBytes(Path.Combine(root, "base-build.log"));
        File.Exists(Path.Combine(root, "prep-base.log")).Should().BeTrue();
        firstBuild.Should().NotBeEmpty();

        bench.Say("build.out", "the retry's build\n");
        bench.Say("build.rc", "0\n");
        var retry = await bench.PrepBaseAsync(new PrepBaseSeams(clock, null));

        retry.Code.Should().Be(0, $"{retry.Error}\n(without the move the exclusive create refuses the retry)");
        var previous = Path.Combine(root, "prep-base.prev-20261030T123015Z");
        File.ReadAllBytes(Path.Combine(previous, "base-build.log")).Should().Equal(firstBuild, "the first attempt's bytes are kept");
        File.Exists(Path.Combine(previous, "prep-base.log")).Should().BeTrue();
        File.ReadAllText(Path.Combine(root, "base-build.log")).Should().Contain("the retry's build").And.NotContain("first attempt");
        File.ReadAllText(Path.Combine(root, "prep-base.log")).Should().EndWith("DONE\n");
        retry.Output.Should().Contain("moved the last attempt's");
        Directory.EnumerateFileSystemEntries(root, "prep-base.prev-*").Should().ContainSingle();
    }

    [Fact]
    public async Task A_leftover_that_is_a_link_is_moved_as_a_link_and_never_followed()
    {
        using var bench = await TargetBench.CreateAsync();
        var root = bench.Root.Root;
        var outside = bench.Root.Scratch("outside");
        var sentinel = Path.Combine(outside, "sentinel.txt");
        File.WriteAllText(sentinel, "the operator's own");
        File.CreateSymbolicLink(Path.Combine(root, "base-results"), outside);
        File.CreateSymbolicLink(Path.Combine(root, "base-test.log"), sentinel);
        File.CreateSymbolicLink(Path.Combine(root, "prep-base.log"), Path.Combine(outside, "dangling"));

        var (code, _, error) = await bench.PrepBaseAsync();

        code.Should().Be(0, error);
        File.ReadAllText(sentinel).Should().Be("the operator's own", "a link in the way is renamed, never written through");
        Directory.GetFileSystemEntries(outside).Should().BeEquivalentTo([sentinel], "nothing was written into the folder a link pointed at");
        var moved = Directory.EnumerateDirectories(root, "prep-base.prev-*").ToList();
        moved.Should().ContainSingle("the leftovers were moved aside, links as links");
        var previous = moved[0];
        new FileInfo(Path.Combine(previous, "base-results")).LinkTarget.Should().Be(outside);
        new FileInfo(Path.Combine(previous, "base-test.log")).LinkTarget.Should().Be(sentinel);
        new FileInfo(Path.Combine(previous, "prep-base.log")).LinkTarget.Should().NotBeNull("a dangling link moves as a link too");
        new FileInfo(Path.Combine(root, "base-test.log")).LinkTarget.Should().BeNull("the new log is a file of its own");
        Directory.Exists(Path.Combine(root, "base-results")).Should().BeTrue();
        new DirectoryInfo(Path.Combine(root, "base-results")).LinkTarget.Should().BeNull();
    }

    [Fact]
    public async Task A_half_made_base_is_redone_whole()
    {
        using var bench = await TargetBench.CreateAsync();
        var outside = bench.Root.Scratch("outside");
        var kept = Path.Combine(outside, "kept.txt");
        var folderFile = Path.Combine(outside, "folder", "inner.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(folderFile)!);
        File.WriteAllText(kept, "the operator's file");
        File.WriteAllText(folderFile, "a file in the operator's folder");
        var stale = Path.Combine(bench.BaseFolder, "stale.txt");
        Directory.CreateDirectory(Path.Combine(bench.BaseFolder, "deeper"));
        File.WriteAllText(stale, "from a crashed run");
        File.CreateSymbolicLink(Path.Combine(bench.BaseFolder, "to-file"), kept);
        File.CreateSymbolicLink(Path.Combine(bench.BaseFolder, "deeper", "to-folder"), Path.GetDirectoryName(folderFile)!);
        File.CreateSymbolicLink(Path.Combine(bench.BaseFolder, "dangling"), Path.Combine(outside, "nowhere"));

        var (code, output, error) = await bench.PrepBaseAsync();

        code.Should().Be(0, error);
        File.Exists(stale).Should().BeFalse("a half-made base is removed whole and made again");
        File.Exists(kept).Should().BeTrue("a link inside is removed, never followed: the file it pointed at is the operator's");
        File.Exists(folderFile).Should().BeTrue("and so is a file in the folder a link pointed at");
        File.ReadAllText(kept).Should().Be("the operator's file");
        File.ReadAllText(folderFile).Should().Be("a file in the operator's folder");
        Directory.GetFileSystemEntries(outside).Should().HaveCount(2);
        File.Exists(bench.BaselineFile).Should().BeTrue();
        output.Should().Contain("half-made base");
    }

    [Fact]
    public async Task Npm_ci_runs_in_every_node_modules_folder_and_ignores_scripts_only_under_the_rules_submodule()
    {
        using var bench = await TargetBench.CreateAsync();

        var (code, _, error) = await bench.PrepBaseAsync();

        code.Should().Be(0, error);
        var calls = bench.Recorded().ToList();
        var installs = calls.Where(call => call.Tool == "npm" && call.Arguments.Contains("ci")).ToList();
        var build = calls.FindIndex(call => IsBuild(call));
        installs.Should().HaveCount(2);
        installs[0].Arguments.Should().Equal("ci", "--no-audit", "--no-fund", "--ignore-scripts");
        installs[0].Cwd.Should().Be(Path.Combine(bench.BaseFolder, ".agents", "conventions"));
        calls.IndexOf(installs[0]).Should().BeLessThan(build, "the rules submodule's modules are restored before the build");
        installs[1].Arguments.Should().Equal("ci", "--no-audit", "--no-fund");
        installs[1].Cwd.Should().Be(Path.Combine(bench.BaseFolder, "src_vs_code"));
        calls.IndexOf(installs[1]).Should().BeGreaterThan(build, "the extension's modules are restored after it");
    }

    [Fact]
    public async Task A_tool_that_cannot_be_started_is_exit_3_naming_it_and_nothing_after_it_runs()
    {
        using var bench = await TargetBench.CreateAsync(extensionFolder: false);

        var (code, _, error) = await bench.PrepBaseAsync();

        code.Should().Be(3);
        error.Should().Contain("could not be started").And.Contain("npm ci src_vs_code");
        File.Exists(bench.BaselineFile).Should().BeFalse();
        bench.Recorded().Should().Contain(call => IsBuild(call), "the build before it did run");
        bench.Recorded().Where(call => IsSuite(call)).Should().BeEmpty("the npm of src_vs_code had no folder to start in, and nothing after it ran");
        File.Exists(Path.Combine(bench.Root.Root, "base-test.log")).Should().BeFalse("a log is made only when its tool is about to start");
        Last(File.ReadAllText(Path.Combine(bench.Root.Root, "prep-base.log"))).Should().Be("npm ci rc=not-started s=0");
    }

    [Fact]
    public async Task A_rules_submodule_that_is_not_one_is_a_wrong_configuration_naming_it_and_the_base()
    {
        using var bench = await TargetBench.CreateAsync();
        bench.Root.Edit(top => top["target"]!["rulesSubmodule"] = "tools/other");

        var (code, _, error) = await bench.PrepBaseAsync();

        code.Should().Be(4);
        error.Should().Contain("target.rulesSubmodule tools/other is not a submodule of the target at " + bench.BaseSha);
        bench.Recorded().Where(call => call.Tool != "git").Should().BeEmpty();
        File.Exists(bench.BaselineFile).Should().BeFalse();
    }

    [Fact]
    public async Task A_stop_the_operator_asks_for_ends_the_verb_resumable_and_writes_no_baseline()
    {
        using var bench = await TargetBench.CreateAsync();
        bench.Say("suite.sleep", "30\n");
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var running = ModelArmsApp.Program.RunAsync(["prep-base", "--experiment", bench.Root.Root], new StringWriter(), new StringWriter(), bench.Root.Environment, stop.Token);

        // The stop lands when the suite shim has STARTED (it recorded itself, then sleeps), not on a timer a loaded machine can beat.
        var started = DateTime.UtcNow;
        while (!bench.Recorded().Any(call => IsSuite(call)) && DateTime.UtcNow - started < TimeSpan.FromSeconds(120))
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        bench.Recorded().Should().Contain(call => IsSuite(call), "the suite must have started before the stop, or the test proves another stop");
        await stop.CancelAsync();
        var act = () => running;

        await act.Should().ThrowAsync<OperationCanceledException>("the stop travels up to Main, which says to run the same command again");
        File.Exists(bench.BaselineFile).Should().BeFalse("a baseline exists only from two suites that completed");

        bench.Say("suite.sleep", "0\n");
        var again = await bench.PrepBaseAsync();

        again.Code.Should().Be(0, $"the same command redoes it: {again.Error}");
        Directory.EnumerateDirectories(bench.Root.Root, "prep-base.prev-*").Should().ContainSingle("the stopped attempt's half-made base and logs were put aside, not mixed with the new ones");
    }
}
