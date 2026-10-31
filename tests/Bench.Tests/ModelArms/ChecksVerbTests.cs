using System.Text.RegularExpressions;
using FluentAssertions;
using ModelArms.Core.Checks;
using ModelArms.Verbs;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary><c>checks</c> (plan §4.9, D17 c/d, D26; E3.S5): the objective report from the target's own tools, over the scratch target of <see cref="TargetBench"/> and one made arm (<see cref="ChecksKit"/>) — a real git, a real node for the target's own wrapper, RECORDING shims for <c>dotnet</c> and <c>npm</c>, the arm's own code played by
/// shell text the shims run (<c>build.hook</c>, <c>suite.hook</c>). The subject is never doubled.
/// <para>
/// What this does NOT prove: that a real <c>dotnet</c>, <c>npm</c> or test runner prints and writes what the shims do (the attended pilot, M2, is the first real run); that the report is TRUE — it is read from what the arm's own code wrote, under the operator's rights, and is not authenticated against that code (E3 §8 item 17); a Windows host (POSIX shims,
/// FIFOs and links); the portable road's folder check (a link swapped in for <c>out/objective</c> is observed on the Linux road only); and a tool that prints gigabytes (item 18).
/// </para></summary>
public sealed class ChecksVerbTests
{
    private static readonly TimeSpan Wall = TimeSpan.FromSeconds(60);

    private static string Sh(string path) => $"'{path}'";

    private static string Ctrf(ChecksKit kit) => Path.Combine(kit.Paths.ObjectiveFolder, "mcp.ctrf.json");

    private static string SuiteTotal12 => "Test run summary: Passed! - Tgt.Tests.dll (net10.0|x64)\n  total: 12\n  failed: 0\n  succeeded: 12\n  skipped: 0\n  duration: 3s 120ms\n";

    private static bool IsSuite(RecordedCall call) => call.Tool == "dotnet" && call.Arguments.Contains("--results-directory");

    [Fact]
    public async Task Checks_runs_build_suite_format_and_extension_in_order_by_their_resolved_paths()
    {
        using var kit = await ChecksKit.CreateAsync();
        kit.Bench.Say("suite.hook", $$$"""printf '%s' '{"results":{"tests":[{"name":"Tgt.Tests.ATests.A_scenario","status":"passed"}]}}' > {{{Sh(Ctrf(kit))}}}""" + "\n");

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, $"{run.Error}\n{run.Output}");
        File.Exists(kit.Paths.Objective).Should().BeTrue("the report is written");
        File.Exists(kit.Layout.ObjectiveMarker(kit.Name)).Should().BeTrue("and the marker after it");
        string[] suiteDll = ["src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests.dll"];
        kit.Order.Should().Equal(
            ["wrapper dotnet build", "dotnet build", $"wrapper dotnet {suiteDll[0]}", $"dotnet {suiteDll[0]}", "dotnet format whitespace", "dotnet format style", "wrapper npm test", "npm test"],
            "the shell's order: the lowered build, the lowered suite, the formatter's two verbs (not lowered), the lowered extension");

        var printed = TargetBench.Printed(run.Output);
        printed.Should().ContainKeys("git", "dotnet", "node", "npm");
        var script = Path.Combine(kit.Clone, ".agents", "conventions", "tools", "low-priority.mjs");
        foreach (var call in kit.ToolCalls.Where(call => call.Tool is "dotnet" or "npm"))
        {
            call.Executable.Should().Be(printed[call.Tool], $"{call.Tool} is started by the REAL path the verb printed, never by a name");
        }

        var wrapped = kit.ToolCalls.Where(call => call.Tool == "wrapper").ToList();
        wrapped.Select(call => call.Executable).Should().OnlyContain(path => path == script, "the wrapper is the target's own script, absolute under the arm's clone");
        wrapped.Select(call => call.NodeStartedAs).Should().OnlyContain(path => path == printed["node"], "the node that runs it is the real path the verb printed");
        wrapped.Select(call => call.Arguments[0]).Should().Equal([printed["dotnet"], printed["dotnet"], printed["npm"]], "the wrapper is handed the resolved executable, not its name");
        wrapped.Select(call => call.Cwd).Should().Equal([kit.Clone, kit.Clone, Path.Combine(kit.Clone, "src_vs_code")]);
        wrapped[1].Arguments.TakeLast(4).Should().Equal(["--results-directory", kit.Paths.ObjectiveFolder, "--report-xunit-ctrf-filename", "mcp.ctrf.json"], "the suite's two options: where its results go and the CTRF's name");
        var formats = kit.ToolCalls.Where(call => call.Tool == "dotnet" && call.Arguments[0] == "format").ToList();
        formats.Select(call => call.Cwd).Should().OnlyContain(cwd => cwd == kit.Clone);
        formats[0].Arguments.Should().Equal(["format", "dew_flow_connect_other_ais.slnx", "whitespace", "--verify-no-changes", "--no-restore", "--include", "a b.cs", "src_mcp/tests/ATests.cs"], "every changed .cs file, each its own argument; the markdown note is not one");

        var report = kit.Report;
        report["build"]!.GetValue<bool>().Should().BeTrue();
        report["mcp"]!["exit"]!.GetValue<int>().Should().Be(2);
        report["mcp"]!["total"]!.GetValue<int>().Should().Be(12);
        report["ts"]!["exit"]!.GetValue<int>().Should().Be(1);
        report["ts"]!["total"]!.GetValue<int>().Should().Be(2);
        report["commits"]!.GetValue<int>().Should().Be(1);
        report["uncommittedFiles"]!.GetValue<int>().Should().Be(0);
        report["scenarios"]!["S1"]!["present"]!.GetValue<bool>().Should().BeTrue("the clone's tests name the scenario (git grep -F under the test globs)");
        report["scenarios"]!["S1"]!["passing"]!.GetValue<string>().Should().Be("passed", "the CTRF at out/objective names it passed — the positive that makes the absent cases below mean something");
        report["diff"]!["changedLines"]!["docs"]!.GetValue<int>().Should().Be(1);
        File.ReadAllText(kit.Paths.ObjectiveLog).Should().Contain("\"arm\":\"C1\"").And.Be(run.Output.Split('\n').Last(line => line.StartsWith("{\"arm\"", StringComparison.Ordinal)) + "\n", "the shell's one-line summary is on stdout and in out/objective.out");
        run.Error.Split('\n').Count(line => line.Contains("arm-done", StringComparison.Ordinal)).Should().Be(1, "an arm with no arm-done line is one warning, and the checks go on");
    }

    [Fact]
    public async Task Every_target_tool_of_checks_is_launched_under_a_built_environment()
    {
        using var kit = await ChecksKit.CreateAsync();
        string[] stripped = ["BENCH_DB", "GIT_DIR", "GITHUB_TOKEN", "CLAUDE_CODE_OAUTH_TOKEN", "COAI_CREDS_KEY", "SSH_AUTH_SOCK"];
        kit.Root.WithVariables(("BENCH_DB", "Host=db;Password=x"), ("GIT_DIR", "/nonexistent/.git"), ("GITHUB_TOKEN", "ghp_value"), ("CLAUDE_CODE_OAUTH_TOKEN", "oauth-value"), ("COAI_CREDS_KEY", "vault-key"), ("SSH_AUTH_SOCK", "/run/agent.sock"), ("KEEP_ME", "yes"));

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error);
        kit.ToolCalls.Should().NotBeEmpty();
        foreach (var call in kit.ToolCalls)
        {
            call.EnvironmentNames.Should().NotContain(stripped, $"{call.Tool}'s environment is built: no secret, no agent socket, no GIT_* of the parent's — the arm's own code runs here").And.Contain(["KEEP_ME", "PATH", "HOME"]);
            call.EnvironmentNames.Should().NotContain(name => name.StartsWith("GIT_", StringComparison.Ordinal));
        }

        run.Output.Should().NotContain("vault-key").And.NotContain("ghp_value").And.NotContain("oauth-value");
    }

    [Fact]
    public async Task Parse_only_rebuilds_the_report_from_logs_already_there()
    {
        using var kit = await ChecksKit.CreateAsync();
        var first = await kit.RunAsync();
        first.Code.Should().Be(0, first.Error);
        var report = File.ReadAllBytes(kit.Paths.Objective);
        kit.Report["build"]!.GetValue<bool>().Should().BeTrue("a full run records the build's exit: the comparison below is not over zeros");
        kit.Report["mcp"]!["exit"]!.GetValue<int>().Should().Be(2);
        File.Delete(kit.Bench.Calls);

        var again = await kit.RunAsync(parseOnly: true);

        again.Code.Should().Be(0, again.Error);
        kit.ToolCalls.Should().BeEmpty("no stage runs: no wrapper, no dotnet, no npm");
        File.ReadAllBytes(kit.Paths.Objective).Should().Equal(report, "the same logs and the same git say the same report, byte for byte");

        File.Delete(kit.Paths.ObjectiveStages);
        var unrecorded = await kit.RunAsync(parseOnly: true);

        unrecorded.Code.Should().Be(0, unrecorded.Error);
        var node = kit.Report;
        node["build"]!.GetValue<bool>().Should().BeFalse("an exit nobody recorded is not a pass — the shell's PARSE_ONLY wrote zeros and claimed one");
        node["format"]!.GetValue<bool>().Should().BeFalse();
        node["mcp"]!["exit"].Should().BeNull("null in the JSON, never 0");
        node["ts"]!["exit"].Should().BeNull();
        node["mcp"]!["total"]!.GetValue<int>().Should().Be(12, "the logs are still read");
    }

    [Theory]
    [InlineData("a link to a folder holding forged logs")]
    [InlineData("a plain file")]
    public async Task Parse_only_refuses_an_objective_folder_that_is_a_link_or_not_a_folder(string kind)
    {
        using var kit = await ChecksKit.CreateAsync();
        (await kit.RunAsync()).Code.Should().Be(0);
        var report = File.ReadAllBytes(kit.Paths.Objective);
        var forged = kit.Root.Scratch("forged-objective");
        File.WriteAllText(Path.Combine(forged, "mcp-tests.log"), "  total: 99\n  failed: 0\n");
        File.WriteAllText(Path.Combine(forged, "mcp.ctrf.json"), """{"results":{"tests":[{"name":"Tgt.Tests.Forged.A_scenario","status":"passed"}]}}""");
        Directory.Move(kit.Paths.ObjectiveFolder, kit.Paths.ObjectiveFolder + ".aside");
        if (kind.Contains("link", StringComparison.Ordinal))
        {
            File.CreateSymbolicLink(kit.Paths.ObjectiveFolder, forged);
        }
        else
        {
            File.WriteAllText(kit.Paths.ObjectiveFolder, "not a folder");
        }

        var run = await kit.RunAsync(parseOnly: true);

        run.Code.Should().Be(4, $"{kind}: BoundedFile judges the LAST component only, so a link as a parent folder would have been followed\n{run.Error}");
        run.Error.Should().Contain("out/objective").And.Contain("--parse-only");
        File.ReadAllBytes(kit.Paths.Objective).Should().Equal(report, "the report a full run wrote is not rewritten from what the arm's code put at the folder's name");
    }

    [Fact]
    public async Task Format_runs_only_over_the_cs_files_changed_since_base_and_passes_when_none_changed()
    {
        using var kit = await ChecksKit.CreateAsync(changedCs: false);

        var none = await kit.RunAsync();

        none.Code.Should().Be(0, none.Error);
        kit.Order.Should().NotContain(label => label.StartsWith("dotnet format", StringComparison.Ordinal), "no .cs file changed: the formatter is not started");
        kit.Report["format"]!.GetValue<bool>().Should().BeTrue("none changed is a pass, as the shell had it");
        File.ReadAllText(kit.Paths.ObjectiveStages).Should().Contain("\"format\":{\"exit\":0,");

        await kit.CommitAsync("a b.cs", "class AB { }\n");
        var added = await kit.RunAsync();

        added.Code.Should().Be(0, added.Error);
        var include = kit.ToolCalls.Where(call => call.Tool == "dotnet" && call.Arguments[0] == "format").Select(call => call.Arguments.SkipWhile(argument => argument != "--include").Skip(1).ToList()).ToList();
        include.Should().HaveCount(2, "whitespace, then style");
        include.Should().OnlyContain(files => files.SequenceEqual(new[] { "a b.cs" }), "a name with a space is ONE argument");
    }

    [Fact]
    public async Task A_failed_build_is_recorded_and_the_report_is_still_written()
    {
        using var kit = await ChecksKit.CreateAsync();
        kit.Bench.Say("build.rc", "1\n");

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, "a failing build is a RESULT for checks, not a failure of the verb");
        kit.Report["build"]!.GetValue<bool>().Should().BeFalse();
        kit.Order.Should().Contain("dotnet format whitespace", "the shell ran every stage whatever the one before did").And.Contain("npm test");
        kit.Report["mcp"]!["exit"]!.GetValue<int>().Should().Be(2, "the suite still ran");
        File.ReadAllText(kit.Paths.ObjectiveStages).Should().StartWith("{\"build\":{\"exit\":1,");
    }

    [Fact]
    public async Task A_link_planted_where_a_log_will_be_written_is_never_followed()
    {
        using var kit = await ChecksKit.CreateAsync();
        var outside = kit.Root.Scratch("outside");
        var first = Path.Combine(outside, "first.txt");
        var second = Path.Combine(outside, "second.txt");
        File.WriteAllText(first, "the operator's own file");
        File.WriteAllText(second, "the operator's other file");
        kit.Bench.Say("build.hook", $"ln -s {Sh(first)} {Sh(kit.Paths.ObjectiveSuiteLog)} 2>/dev/null || true\nln -s {Sh(second)} {Sh(kit.Paths.ObjectiveStages)} 2>/dev/null || true\n");
        kit.Bench.Say("suite.out", SuiteTotal12);

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error);
        File.ReadAllText(first).Should().Be("the operator's own file", "a log the harness writes is never opened through a link the arm's code planted");
        File.ReadAllText(second).Should().Be("the operator's other file", "and neither is the stage record");
        new FileInfo(kit.Paths.ObjectiveSuiteLog).LinkTarget.Should().BeNull("the log was made BEFORE the first child, exclusively: the arm's ln found the name taken");
        File.ReadAllText(kit.Paths.ObjectiveSuiteLog).Should().Contain("total: 12", "the log is a regular file holding the suite's output");
        new FileInfo(kit.Paths.ObjectiveStages).LinkTarget.Should().BeNull("the record replaced the planted link as a file; it did not write through it");
        File.ReadAllText(kit.Paths.ObjectiveStages).Should().StartWith("{\"build\":");
    }

    [Fact]
    public async Task A_log_the_arms_code_unlinks_and_forges_is_not_what_the_report_reads()
    {
        using var kit = await ChecksKit.CreateAsync();
        kit.Bench.Say("suite.out", SuiteTotal12);
        kit.Bench.Say("suite.rc", "0\n");
        kit.Bench.Say("suite.hook", $"rm -f {Sh(kit.Paths.ObjectiveSuiteLog)}\nprintf 'failed Tgt.Tests.Forged.Fails (1ms)\\n  total: 99\\n  failed: 1\\n  succeeded: 98\\n' > {Sh(kit.Paths.ObjectiveSuiteLog)}\n");

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error);
        File.ReadAllText(kit.Paths.ObjectiveSuiteLog).Should().Contain("total: 99", "the forgery is what the name holds now — the test is about what the report read");
        var mcp = kit.Report["mcp"]!;
        mcp["total"]!.GetValue<int>().Should().Be(12, "the report reads the suite's output from the handle the harness made the log with, never the file the arm's code put at its name");
        mcp["failedNew"]!.AsArray().Should().BeEmpty();
    }

    [Theory]
    [InlineData("a link to a folder holding a CTRF")]
    [InlineData("another real folder")]
    public async Task An_objective_folder_swapped_for_a_link_is_refused_and_nothing_is_written_through_it(string swap)
    {
        using var kit = await ChecksKit.CreateAsync();
        var fixture = kit.Root.Scratch("fixture-objective");
        File.WriteAllText(Path.Combine(fixture, "mcp.ctrf.json"), """{"results":{"tests":[{"name":"Tgt.Tests.Forged.A_scenario","status":"passed"}]}}""");
        var before = Listing(fixture);
        var objective = Sh(kit.Paths.ObjectiveFolder);
        var put = swap.StartsWith("a link", StringComparison.Ordinal) ? $"ln -s {Sh(fixture)} {objective}" : $"mkdir {objective}";
        kit.Bench.Say("suite.hook", $"mv {objective} {Sh(kit.Paths.ObjectiveFolder + ".aside")}\n{put}\n");

        var run = await kit.RunAsync();

        run.Code.Should().Be(3, $"{run.Error}\n{run.Output}");
        run.Error.Should().Contain("replaced").And.Contain("no report");
        Listing(fixture).Should().Be(before, "the fixture's listing and bytes are what they were: nothing is written through the swapped name — no stages.json, no log");
        if (!swap.StartsWith("a link", StringComparison.Ordinal))
        {
            Directory.EnumerateFileSystemEntries(kit.Paths.ObjectiveFolder).Should().BeEmpty("a folder the arm's code put at the name holds nothing the harness wrote: not stages.json, not a log");
        }

        File.Exists(kit.Paths.Objective).Should().BeFalse("no report");
        File.Exists(kit.Layout.ObjectiveMarker(kit.Name)).Should().BeFalse("and no marker");
    }

    private static string Listing(string folder) =>
        string.Join('\n', Directory.EnumerateFileSystemEntries(folder).Order(StringComparer.Ordinal).Select(entry => $"{Path.GetFileName(entry)} {File.ReadAllText(entry)}"));

    [Theory]
    [InlineData("a link")]
    [InlineData("a fifo")]
    [InlineData("too big")]
    public async Task A_ctrf_that_is_a_link_a_fifo_or_too_big_reads_as_no_tests_and_says_why(string kind)
    {
        using var kit = await ChecksKit.CreateAsync();
        var ctrf = Ctrf(kit);
        var outside = Path.Combine(kit.Root.Scratch("outside"), "ctrf.json");
        File.WriteAllText(outside, """{"results":{"tests":[{"name":"Tgt.Tests.ATests.A_scenario","status":"passed"}]}}""");
        kit.Bench.Say("suite.hook", $"rm -f {Sh(ctrf)}\n" + kind switch
        {
            "a link" => $"ln -s {Sh(outside)} {Sh(ctrf)}\n",
            "a fifo" => $"mkfifo {Sh(ctrf)}\n",
            _ => $"truncate -s 67108865 {Sh(ctrf)}\n",
        });
        var running = Task.Run(() => kit.RunAsync(), TestContext.Current.CancellationToken);

        try
        {
            (await Task.WhenAny(running, Task.Delay(Wall, TestContext.Current.CancellationToken))).Should().BeSameAs(running, "reading a FIFO as a file is a hang: the verb waits for a writer that never comes");
            var run = await running;

            run.Code.Should().Be(0, run.Error);
            kit.Report["scenarios"]!["S1"]!["passing"]!.GetValue<string>().Should().Be("absent", $"{kind}: what is not a plain small file is read as no tests");
            run.Lines.Should().Contain(line => line.Contains("mcp.ctrf.json", StringComparison.Ordinal) && line.Contains("no tests", StringComparison.Ordinal), "a line names the file and says it was read as no tests");
        }
        finally
        {
            PlatformKit.ReleaseFifo(ctrf);
        }
    }

    [Fact]
    public async Task An_earlier_objective_folder_is_kept_aside_not_overwritten()
    {
        using var kit = await ChecksKit.CreateAsync();
        kit.Bench.Say("build.out", "the first run's build\n");
        (await kit.RunAsync()).Code.Should().Be(0);
        kit.Bench.Say("build.out", "the second run's build\n");

        var second = await kit.RunAsync();

        second.Code.Should().Be(0, second.Error);
        var kept = Directory.EnumerateDirectories(kit.Paths.Out, "objective.prev-*").ToList();
        kept.Should().ContainSingle();
        kept[0].Should().MatchRegex(@"objective\.prev-\d{8}T\d{6}Z$");
        File.ReadAllText(Path.Combine(kept[0], "build.log")).Should().Contain("the first run's build").And.NotContain("second", "the first run's evidence is kept");
        File.ReadAllText(kit.Paths.ObjectiveBuildLog).Should().Contain("the second run's build");
        second.Output.Should().Contain("objective.prev-", "the verb says where the earlier folder went");
    }

    [Fact]
    public async Task Objective_folders_moved_aside_within_one_second_each_get_their_own_numbered_folder_in_order()
    {
        using var kit = await ChecksKit.CreateAsync();
        var seams = new ChecksSeams(new BenchClock(new DateTimeOffset(2026, 10, 30, 12, 30, 15, TimeSpan.Zero), TimeZoneInfo.Utc), null);
        var runs = new List<ChecksRun>();
        foreach (var attempt in new[] { 1, 2, 3, 4 })
        {
            kit.Bench.Say("build.out", $"run {attempt}'s build\n");
            runs.Add(await kit.RunAsync(seams: seams));
        }

        runs.Select(run => run.Code).Should().Equal([0, 0, 0, 0], string.Join(" | ", runs.Select(run => run.Error)));
        string[] expected = ["objective.prev-20261030T123015Z", "objective.prev-20261030T123015Z-2", "objective.prev-20261030T123015Z-3"];
        Directory.EnumerateFileSystemEntries(kit.Paths.Out, "objective.prev-*").Select(Path.GetFileName).Order(StringComparer.Ordinal).Should().Equal(expected, "the first move takes the stamp, the next the stamp and -2, -3: unique, in the order they were made");
        foreach (var (folder, attempt) in expected.Zip(new[] { 1, 2, 3 }))
        {
            File.ReadAllText(Path.Combine(kit.Paths.Out, folder, "build.log")).Should().Contain($"run {attempt}'s build", "each run's own logs are kept, none overwritten by a later one");
        }

        File.ReadAllText(kit.Paths.ObjectiveBuildLog).Should().Contain("run 4's build");
        runs[3].Output.Should().Contain("out/" + expected[2], "the verb names the folder it really used (the arm's token is masked in what it prints)");
    }

    [Fact]
    public async Task A_link_at_the_objective_folders_name_is_moved_aside_as_a_link_and_never_followed()
    {
        using var kit = await ChecksKit.CreateAsync();
        var outside = kit.Root.Scratch("outside-objective");
        File.WriteAllText(Path.Combine(outside, "sentinel.txt"), "the operator's own");
        File.CreateSymbolicLink(kit.Paths.ObjectiveFolder, outside);

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error);
        Directory.EnumerateFileSystemEntries(outside).Select(Path.GetFileName).Should().Equal(["sentinel.txt"], "nothing was written through the link: no log, no stages.json, no CTRF");
        File.ReadAllText(Path.Combine(outside, "sentinel.txt")).Should().Be("the operator's own");
        var kept = Directory.EnumerateFileSystemEntries(kit.Paths.Out, "objective.prev-*").ToList();
        kept.Should().ContainSingle();
        new FileInfo(kept[0]).LinkTarget.Should().Be(outside, "the link was renamed as the link it is");
        new FileInfo(kit.Paths.ObjectiveFolder).LinkTarget.Should().BeNull("the folder the run made is a real one");
        File.Exists(kit.Paths.ObjectiveStages).Should().BeTrue();
    }

    [Fact]
    public async Task Checks_refuses_an_arm_whose_runner_is_alive()
    {
        using var kit = await ChecksKit.CreateAsync();
        using var live = await StatusKit.HeldProcess.StartAsync();
        kit.Root.Write($"pids/{kit.Name}.pid", $"{live.Pid}\n");

        var run = await kit.RunAsync();

        run.Code.Should().Be(4);
        run.Error.Should().Contain($"runner (pid {live.Pid}) is alive").And.Contain("finished or stopped arm");
        kit.Bench.Recorded().Should().BeEmpty("nothing is launched — not a stage, not even a question to git");
        Directory.Exists(kit.Paths.ObjectiveFolder).Should().BeFalse();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("12 34")]
    public async Task A_pid_file_that_holds_no_pid_is_refused_because_liveness_cannot_be_told(string text)
    {
        using var kit = await ChecksKit.CreateAsync();
        kit.Root.Write($"pids/{kit.Name}.pid", text);

        var run = await kit.RunAsync();

        run.Code.Should().Be(4, text);
        run.Error.Should().Contain("holds no pid");
        kit.Bench.Recorded().Should().BeEmpty("the arm's code is not run under a runner that may still be writing the clone");
    }

    [Fact]
    public async Task The_objective_marker_is_written_after_objective_json_and_not_on_a_stop()
    {
        using var kit = await ChecksKit.CreateAsync();
        var marker = kit.Layout.ObjectiveMarker(kit.Name);
        kit.Bench.Say("suite.sleep", "30\n");
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var running = kit.RunAsync(stopping: stop.Token);
        try
        {
            var started = DateTime.UtcNow;
            while (!kit.Bench.Recorded().Any(IsSuite) && DateTime.UtcNow - started < TimeSpan.FromSeconds(120))
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }

            kit.Bench.Recorded().Should().Contain(call => IsSuite(call), "the suite must have started before the stop, or the test proves another stop");
        }
        finally
        {
            await stop.CancelAsync();
            await Task.WhenAny(running, Task.Delay(Wall, TestContext.Current.CancellationToken));
        }

        running.IsCompleted.Should().BeTrue("the stopped verb ends within the wall — it is awaited below, so a verb that ignored the stop must not hang the suite");
        var stopped = () => running;
        await stopped.Should().ThrowAsync<OperationCanceledException>("the operator's stop travels up to Main, which maps it to exit 5");
        File.Exists(kit.Paths.Objective).Should().BeFalse("a stopped run has no report");
        File.Exists(marker).Should().BeFalse("and no marker: the chain would read a stopped run as checked");

        kit.Bench.Say("suite.sleep", "0\n");
        var seen = new List<(bool Report, bool Marker)>();
        var normal = await kit.RunAsync(seams: new ChecksSeams(new TickingClock(), null, () => seen.Add((File.Exists(kit.Paths.Objective), File.Exists(marker)))));

        normal.Code.Should().Be(0, normal.Error);
        seen.Should().Equal([(true, false)], "at the moment the report is on disk the marker is not: the marker is LAST, so a marker always means a report");
        File.Exists(marker).Should().BeTrue();
        File.GetLastWriteTimeUtc(marker).Should().BeOnOrAfter(File.GetLastWriteTimeUtc(kit.Paths.Objective));
    }

    [Fact]
    public async Task Every_git_of_checks_is_the_resolved_one_hardened_with_no_GIT_or_secret_variable()
    {
        using var kit = await ChecksKit.CreateAsync();
        await TargetBench.GitAsync(kit.Clone, "config", "filter.planted.clean", "cat");
        kit.Root.WithVariables(("GITHUB_TOKEN", "ghp_value"), ("GIT_DIR", "/nonexistent/.git"), ("GIT_CONFIG_COUNT", "1"), ("COAI_CREDS_KEY", "vault-key"), ("SSH_AUTH_SOCK", "/run/agent.sock"), ("BENCH_DB", "Host=db"));

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error);
        var printed = TargetBench.Printed(run.Output);
        var git = kit.Bench.Recorded().Where(call => call.Tool == "git").ToList();
        git.Should().HaveCountGreaterThanOrEqualTo(6, "the head, the counts, the numstat and shortstat, the grep, the changed files, the filter enumeration — the scan must still find git's calls");
        git.Select(call => call.Executable).Should().OnlyContain(path => path == printed["git"], "every git is the resolved one, by its real path");
        git.Select(call => call.Cwd).Should().OnlyContain(cwd => cwd == kit.Clone);
        foreach (var call in git)
        {
            call.Arguments.Take(8).Should().Equal(["-c", "core.fsmonitor=false", "-c", "core.hooksPath=", "-c", "submodule.recurse=false", "-c", "protocol.allow=never"], "no hook and no filesystem monitor of the arm's clone runs on the harness's behalf, no checkout recurses into a submodule, and no transport program starts for a lazy fetch");
            var names = call.EnvironmentNames.Where(name => name.StartsWith("GIT_", StringComparison.Ordinal)).ToList();
            names.Should().Contain("GIT_NO_LAZY_FETCH");
            names.Except(["GIT_NO_LAZY_FETCH"]).Where(name => !Regex.IsMatch(name, @"^GIT_CONFIG_(COUNT|KEY_\d+|VALUE_\d+)$")).Should().BeEmpty("no GIT_* of the parent's: only the harness's own driver neutralisation");
            call.EnvironmentNames.Should().NotContain(["GITHUB_TOKEN", "COAI_CREDS_KEY", "SSH_AUTH_SOCK", "BENCH_DB", "GIT_DIR"]);
        }

        git.Should().Contain(call => call.EnvironmentNames.Contains("GIT_CONFIG_COUNT"), "the planted filter driver was found and neutralised: the allowance for GIT_CONFIG_* is exercised");
        var diffs = git.Where(call => call.Arguments.Contains("diff")).ToList();
        diffs.Should().NotBeEmpty();
        diffs.Should().OnlyContain(call => call.Arguments.Contains("--no-ext-diff") && call.Arguments.Contains("--no-textconv") && call.Arguments.Contains("--submodule=short") && call.Arguments.Contains("diff.external="));
        git.Where(call => call.Arguments.Contains("status")).Should().ContainSingle().Which.Arguments.Should().Contain("--ignore-submodules=dirty");
        git.Where(call => call.Arguments.Contains("grep")).Should().NotBeEmpty().And.OnlyContain(call => call.Arguments.Contains("-e"));
    }

    [Fact]
    public async Task A_tool_killed_at_its_wall_is_recorded_with_a_null_exit()
    {
        using var kit = await ChecksKit.CreateAsync();
        kit.Bench.Say("suite.sleep", "30\n");

        var run = await kit.RunAsync(seams: new ChecksSeams(new TickingClock(), TimeSpan.FromSeconds(1)));

        run.Code.Should().Be(0, run.Error);
        var stages = File.ReadAllText(kit.Paths.ObjectiveStages);
        stages.Should().Contain("\"suite\":{\"exit\":null,").And.Contain("\"killed\":true}");
        kit.Report["mcp"]!["exit"].Should().BeNull("a suite killed at its wall has no exit code — never a zero");
        kit.Report["ts"]!["exit"]!.GetValue<int>().Should().Be(1, "the extension still ran after the suite was killed");
        StageRecord.Read(stages).Ok().Suite.Killed.Should().BeTrue();
    }

    [Fact]
    public async Task Steps_that_ended_on_their_deadline_reach_the_report()
    {
        using var kit = await ChecksKit.CreateAsync();
        kit.Done("1-code", "finished");
        kit.Done("2-review", "hard-deadline");
        File.WriteAllText(kit.Paths.Timing, "{\"kind\":\"arm-done\",\"arm\":\"C1\",\"done_utc\":\"2026-10-30T10:00:00Z\"}\n");

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error);
        kit.Report["stepsOnDeadline"]!.AsArray().Select(step => step!.GetValue<string>()).Should().Equal(["2-review"], "the step whose .done says hard-deadline, and only it");
        run.Error.Should().NotContain("arm-done", "an arm that finished its scheme is not warned about");
    }

    [Fact]
    public async Task A_scheme_with_a_tasks_block_is_checked_like_any_other()
    {
        using var kit = await ChecksKit.CreateAsync("SRS1");
        kit.Done("3-hreview", "hard-deadline");

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, $"{run.Error}\n(run-arm refuses an SRS arm until the task pipeline lands; the objective checks do not need it)");
        kit.Report["arm"]!.GetValue<string>().Should().Be("SRS1");
        kit.Report["stepsOnDeadline"]!.AsArray().Select(step => step!.GetValue<string>()).Should().Equal(["3-hreview"]);
    }

    [Theory]
    [InlineData("a baseline that is a link")]
    [InlineData("a baseline that is not JSON")]
    public async Task A_baseline_that_cannot_be_read_ends_the_verb_with_4_before_anything_runs(string which)
    {
        using var kit = await ChecksKit.CreateAsync();
        var baseline = Path.Combine(kit.Root.Root, "baseline.json");
        if (which.Contains("link", StringComparison.Ordinal))
        {
            var real = Path.Combine(kit.Root.Scratch("outside"), "baseline.json");
            File.Move(baseline, real);
            File.CreateSymbolicLink(baseline, real);
        }
        else
        {
            File.WriteAllText(baseline, "{ not json");
        }

        var run = await kit.RunAsync();

        run.Code.Should().Be(4, which);
        run.Error.Should().Contain("baseline.json");
        kit.Bench.Recorded().Where(call => call.Tool != "git").Should().BeEmpty("nothing of the target runs against a baseline that does not read");
        Directory.Exists(kit.Paths.ObjectiveFolder).Should().BeFalse();
    }

    [Theory]
    [InlineData("PATH")]
    [InlineData("HOME")]
    [InlineData("XDG_CONFIG_HOME")]
    public async Task A_PATH_HOME_or_XDG_CONFIG_HOME_under_a_forbidden_root_is_refused_before_anything_runs(string variable)
    {
        using var kit = await ChecksKit.CreateAsync();
        var under = Directory.CreateDirectory(Path.Combine(kit.Root.Root, "trap")).FullName;
        kit.Root.WithVariables(variable == "PATH" ? (variable, string.Join(Path.PathSeparator, under, kit.Root.Environment["PATH"])) : (variable, under));

        var run = await kit.RunAsync();

        run.Code.Should().Be(4, variable);
        run.Error.Should().Contain(under);
        kit.Bench.Recorded().Should().BeEmpty("no program is looked up and none is started");
        Directory.Exists(kit.Paths.ObjectiveFolder).Should().BeFalse();
    }

    [Fact]
    public async Task Checks_prints_the_isolation_state_and_restores_nothing()
    {
        using var kit = await ChecksKit.CreateAsync();
        var snapshot = Path.Combine(kit.Root.Root, ".locked-modes");
        var text = """{"run":"part7","digest":"d1","since":"2026-10-30T10:00:00Z","modes":[{"path":"/r","mode":"755"}]}""";
        kit.Root.Write(".locked-modes", text);

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error);
        run.Output.Should().Contain("a lock was interrupted — run `lock part7` or `unlock`");
        File.ReadAllText(snapshot).Should().Be(text, "checks reads the snapshot and never restores it");
        Directory.EnumerateFileSystemEntries(kit.Root.Root).Where(entry => entry.Contains(".restored-", StringComparison.Ordinal)).Should().BeEmpty();
        File.Exists(Path.Combine(kit.Root.Root, ".locked-modes.applied")).Should().BeFalse("and writes none");
    }

    [Fact]
    public async Task Checks_never_prints_a_token()
    {
        using var kit = await ChecksKit.CreateAsync();
        var outside = Path.Combine(kit.Root.Scratch("outside"), "ctrf.json");
        File.WriteAllText(outside, "{}");
        kit.Bench.Say("suite.hook", $"rm -f {Sh(Ctrf(kit))}\nln -s {Sh(outside)} {Sh(Ctrf(kit))}\n");

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error);
        run.Lines.Should().Contain(line => line.Contains("<token>", StringComparison.Ordinal), "the CTRF line names a path under the arm's folder, so the scrub has something to scrub — the check is not vacuous");
        foreach (var line in run.Lines)
        {
            foreach (var token in kit.Tokens)
            {
                line.Should().NotContain(token, "no line the verb prints names an arm's token");
            }
        }

        File.ReadAllText(kit.Paths.ObjectiveLog).Should().NotContain(kit.Token);
    }
}
