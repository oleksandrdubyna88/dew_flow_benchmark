using System.Text.Json.Nodes;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Launch;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary><c>make</c> (E3.S3, plan §4.1, D4, D5, D26): the arms of the runs, each made WHOLE and the marker LAST — copied from <c>base/</c> byte for byte, branched, warm-built through the target's own wrapper, given its two MCP files and the coai settings — and the arm map extended, never
/// re-minted. The verb DELETES a folder, so most of this class is the guard: a marker-less arm folder is removed only on positive evidence that it never ran, and what cannot be told is left alone. The subject is never doubled: a real <c>git</c>, a real <c>node</c> for the target's wrapper, recording
/// shims for <c>dotnet</c>; <c>base/</c> is what <c>prep-base</c> made over the scratch target of <see cref="TargetBench"/>.
/// <para>
/// What this does NOT prove: that a real <c>dotnet build</c> leaves what the shim leaves (the pilot, M2, is the first real run); that claude hands the stdio server the <c>env</c> member of <c>mcp-coai.json</c> (M2's key check); a Windows host (POSIX shims, modes and links); the portable road on
/// macOS or FreeBSD (BaseCopyTests hands it in); an arm folder whose <c>out</c> is a link to a folder with no timing (a link in a PARENT folder is the bounded road's stated residual).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class MakeVerbTests
{
    private static readonly TimeSpan Wall = TimeSpan.FromSeconds(20);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly string[] Volatile = [".git/HEAD", ".git/index", ".git/logs", ".git/refs"];

    [Fact]
    public async Task An_arm_is_a_copy_of_the_base_on_the_one_branch_with_its_mcp_files_and_coai_settings()
    {
        MakeKit.Posix();
        using var kit = await MakeKit.CreateAsync("C1");
        var tool = MakeKit.Put(Path.Combine(kit.Bench.BaseFolder, "tools", "run.sh"), "#!/bin/sh\n");
        File.SetUnixFileMode(tool, (UnixFileMode)Convert.ToInt32("750", 8));
        File.CreateSymbolicLink(Path.Combine(kit.Bench.BaseFolder, "link-to-plan"), "todo/PLAN_a_spent_quota_cools_the_model_down.md");

        var run = await kit.MakeAsync();

        run.Code.Should().Be(0, run.Everything);
        File.Exists(kit.Layout.ArmMapFile).Should().BeTrue("make mints the arm map before its first folder");
        var paths = kit.Paths("C1");
        Directory.Exists(paths.CloneFolder).Should().BeTrue("make copies base/ to the arm's clone");
        (await TargetBench.GitAsync(paths.CloneFolder, "rev-parse", "--abbrev-ref", "HEAD")).Trim().Should().Be(kit.Branch, "every arm commits to the one branch");
        (await TargetBench.GitAsync(paths.CloneFolder, "rev-parse", "HEAD")).Trim().Should().Be(kit.Bench.BaseSha, "the branch starts at base");
        bool Skipped(string relative) => Volatile.Any(name => relative == name || relative.StartsWith(name + "/", StringComparison.Ordinal));
        MakeKit.Describe(paths.CloneFolder, Skipped).Should().Equal(MakeKit.Describe(kit.Bench.BaseFolder, Skipped), "the clone holds base's files — bytes, modes, links — apart from what a branch moves (HEAD, the index, refs and their logs)");
        File.GetUnixFileMode(Path.Combine(paths.CloneFolder, "tools", "run.sh")).Should().Be((UnixFileMode)Convert.ToInt32("750", 8));
        new FileInfo(Path.Combine(paths.CloneFolder, "link-to-plan")).LinkTarget.Should().Be("todo/PLAN_a_spent_quota_cools_the_model_down.md");

        File.ReadAllText(paths.McpNone).Should().Be(McpConfig.None() + "\n");
        var coai = kit.Root.ExperimentOf().Coai;
        JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(paths.McpCoai)), JsonNode.Parse(McpConfig.Coai(coai, paths, kit.Root.Environment))).Should().BeTrue("mcp-coai.json is Coai(…) for this arm");
        File.ReadAllBytes(paths.CoaiSettings(coai.Side)).Should().Equal(MakeKit.SettingsBytes, "the settings are copied byte for byte");
        File.Exists(kit.Layout.Made("C1")).Should().BeTrue("the marker is written last, after everything above");
        File.ReadAllText(paths.PrepBuildLog).Should().Contain("Build succeeded.", "the build shim's line is in the warm build's log");
        Directory.Exists(paths.Notes).Should().BeTrue();
        Directory.Exists(paths.Work).Should().BeTrue();
        Directory.Exists(paths.Tmp).Should().BeTrue();
        run.Output.Should().Contain("arm C1 made and built");
    }

    [Fact]
    public async Task A_crashed_make_is_redone_whole()
    {
        MakeKit.Posix();
        using var kit = await MakeKit.CreateAsync("C1");
        kit.WriteMap("C1");
        var half = kit.Leftover("C1");

        var run = await kit.MakeAsync();

        run.Code.Should().Be(0, run.Everything);
        File.Exists(half).Should().BeFalse("a folder with no marker and no sign that it ever ran is removed whole and made again");
        var paths = kit.Paths("C1");
        File.Exists(Path.Combine(paths.CloneFolder, ".git", "HEAD")).Should().BeTrue("the clone is complete");
        File.Exists(kit.Layout.Made("C1")).Should().BeTrue();
        run.Output.Should().Contain("removed").And.Contain("C1");
    }

    [Fact]
    public async Task The_map_is_extended_for_new_arms_and_existing_tokens_are_kept()
    {
        MakeKit.Posix();
        using var kit = await MakeKit.CreateAsync("C1", "C2");
        var before = kit.WriteMap("C1");
        var first = before.TokenOf("C1").Ok();
        MakeKit.Put(kit.Layout.Made("C1"), "made\n");

        var run = await kit.MakeAsync();

        run.Code.Should().Be(0, run.Everything);
        var map = kit.ReadMap();
        map.Entries.Select(entry => entry.Key).Should().Equal("C1", "C2");
        map.TokenOf("C1").Ok().Should().Be(first, "an arm that has a token keeps it — a re-minted token would orphan its folder");
        map.TokenOf("C2").Ok().Should().NotBe(first).And.MatchRegex("^[0-9a-f]{16}$");
        Directory.Exists(kit.Layout.Arm(first).Folder).Should().BeFalse("C1 has its marker: it is not made again");
        File.Exists(kit.Layout.Made("C2")).Should().BeTrue();
        var bytes = File.ReadAllBytes(kit.Layout.ArmMapFile);

        var again = await kit.MakeAsync();

        again.Code.Should().Be(0, again.Everything);
        again.Output.Should().Contain("every arm of every run is made");
        File.ReadAllBytes(kit.Layout.ArmMapFile).Should().Equal(bytes, "nothing to make: the map is not written");
    }

    [Fact]
    public async Task Mcp_coai_json_carrying_a_key_is_mode_0600()
    {
        MakeKit.Posix();
        using var kit = await MakeKit.CreateAsync("C1");
        const string key = "vault-key-9f3a77c2";
        kit.Root.WithVariables(("COAI_CREDS_KEY", key));

        var run = await kit.MakeAsync();

        run.Code.Should().Be(0, run.Everything);
        File.Exists(kit.Layout.ArmMapFile).Should().BeTrue("make mints the arm map before its first folder");
        var paths = kit.Paths("C1");
        File.ReadAllText(paths.McpCoai).Should().Contain(key, "the key travels in the server's env member, the one place the harness writes it");
        const UnixFileMode Private = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        File.GetUnixFileMode(paths.McpCoai).Should().Be(Private, "a key written with the umask would be readable by other users");
        File.GetUnixFileMode(paths.CoaiSettings(kit.Root.ExperimentOf().Coai.Side)).Should().Be(Private);
        File.GetUnixFileMode(kit.Layout.ArmMapFile).Should().Be(Private);
        run.Everything.Should().NotContain(key, "the key is in that one file and in no output line");
    }

    [Fact]
    public async Task An_arm_that_has_run_is_never_remade_without_its_marker()
    {
        MakeKit.Posix();
        using var kit = await MakeKit.CreateAsync("C1");
        kit.WriteMap("C1");
        var paths = kit.Paths("C1");
        kit.Leftover("C1");
        MakeKit.Put(paths.Timing, MakeKit.TimingText("C1", start: true) + "\n");
        var before = MakeKit.Describe(paths.Folder);

        var run = await kit.MakeAsync();

        run.Code.Should().Be(4, run.Everything);
        run.Error.Should().Contain("C1 has run").And.Contain("chain/made-C1").And.Contain("never remakes");
        MakeKit.Describe(paths.Folder).Should().Equal(before, "an arm that ran is somebody's finished work: nothing of it is removed or changed");
        File.Exists(kit.Layout.Made("C1")).Should().BeFalse();
    }

    [Theory]
    [InlineData("link")]
    [InlineData("fifo")]
    [InlineData("two-megabytes")]
    [InlineData("not-json")]
    public async Task An_arm_whose_timing_cannot_be_read_is_never_removed(string shape)
    {
        MakeKit.Posix();
        using var kit = await MakeKit.CreateAsync("C1");
        kit.WriteMap("C1");
        var paths = kit.Paths("C1");
        var work = kit.Leftover("C1", "work.txt");
        var outside = MakeKit.Put(Path.Combine(kit.Root.Scratch("outside"), "timing.jsonl"), "{\"kind\":\"start\"}\n");
        Directory.CreateDirectory(paths.Out);
        switch (shape)
        {
            case "link":
                File.CreateSymbolicLink(paths.Timing, outside);
                break;
            case "fifo":
                MakeKit.Fifo(paths.Timing);
                break;
            case "two-megabytes":
                File.WriteAllText(paths.Timing, new string('x', 2 * 1024 * 1024));
                break;
            default:
                File.WriteAllText(paths.Timing, "{not json\n");
                break;
        }

        try
        {
            var running = Task.Run(() => kit.MakeAsync(), Ct);

            (await Task.WhenAny(running, Task.Delay(Wall, Ct))).Should().BeSameAs(running, "the symptom of reading a FIFO as a file is the hang: the verb waits for a writer that never comes");
            var run = await running;

            run.Code.Should().Be(4, run.Everything);
            run.Error.Should().Contain("cannot tell whether C1 ran").And.Contain("move its folder away by hand");
            File.ReadAllText(work).Should().Be("left by a make that was killed\n", "what cannot be told is left exactly as it was");
            Path.Exists(paths.Timing).Should().BeTrue();
            new FileInfo(paths.Timing).LinkTarget.Should().Be(shape == "link" ? outside : null);
            if (shape == "two-megabytes")
            {
                new FileInfo(paths.Timing).Length.Should().Be(2 * 1024 * 1024);
            }

            File.Exists(kit.Layout.Made("C1")).Should().BeFalse();
        }
        finally
        {
            if (shape == "fifo")
            {
                PlatformKit.ReleaseFifo(paths.Timing);
            }
        }
    }

    [Fact]
    public async Task Removing_a_half_made_arm_follows_no_link_and_refuses_an_arm_folder_that_is_a_link()
    {
        MakeKit.Posix();
        using var kit = await MakeKit.CreateAsync("C1");
        kit.WriteMap("C1");
        var paths = kit.Paths("C1");
        var outside = kit.Root.Scratch("outside");
        var sentinel = MakeKit.Put(Path.Combine(outside, "folder", "sentinel.txt"), "the operator's file\n");
        var file = MakeKit.Put(Path.Combine(outside, "file.txt"), "another file of the operator\n");
        kit.Leftover("C1");
        File.CreateSymbolicLink(Path.Combine(paths.CloneFolder, "to-folder"), Path.Combine(outside, "folder"));
        MakeKit.Put(Path.Combine(paths.CloneFolder, "nested", "deeper", "keep"), "x");
        File.CreateSymbolicLink(Path.Combine(paths.CloneFolder, "nested", "to-file"), file);
        File.CreateSymbolicLink(Path.Combine(paths.CloneFolder, "dangling"), Path.Combine(outside, "nothing-here"));
        var listing = MakeKit.Describe(outside);

        var run = await kit.MakeAsync();

        run.Code.Should().Be(0, run.Everything);
        Directory.Exists(paths.CloneFolder).Should().BeTrue("the arm is made again");
        File.Exists(Path.Combine(paths.CloneFolder, "nested", "to-file")).Should().BeFalse("the old links are gone");
        File.Exists(sentinel).Should().BeTrue("removing a folder never descends through a link: a delete that followed to-folder would have removed the operator's file");
        MakeKit.Describe(outside).Should().Equal(listing, "every outside byte and listing is as it was");

        File.Delete(kit.Layout.Made("C1"));
        Directory.Delete(paths.Folder, recursive: true);
        var elsewhere = MakeKit.Put(Path.Combine(kit.Root.Scratch("elsewhere"), "precious.txt"), "not an arm\n");
        File.CreateSymbolicLink(paths.Folder, Path.GetDirectoryName(elsewhere)!);
        var before = MakeKit.Describe(Path.GetDirectoryName(elsewhere)!);

        var linked = await kit.MakeAsync();

        linked.Code.Should().Be(4, linked.Everything);
        linked.Error.Should().Contain("C1").And.Contain("is a link").And.Contain("r/<token>");
        MakeKit.Describe(Path.GetDirectoryName(elsewhere)!).Should().Equal(before, "an arm folder that is a link is neither removed nor entered");
        new FileInfo(paths.Folder).LinkTarget.Should().Be(Path.GetDirectoryName(elsewhere));
        File.Exists(kit.Layout.Made("C1")).Should().BeFalse();
    }

    [Fact]
    public async Task An_arm_that_has_its_marker_is_never_touched_even_when_ArmMaker_is_asked_for_it_directly()
    {
        MakeKit.Posix();
        using var kit = await MakeKit.CreateAsync("C1");
        kit.WriteMap("C1");
        var paths = kit.Paths("C1");
        kit.Leftover("C1", "finished-work.txt");
        MakeKit.Put(kit.Layout.Made("C1"), "made\n");
        var before = MakeKit.Describe(paths.Folder);
        var said = new List<string>();
        var maker = new ArmMaker(kit.Root.ExperimentOf(), kit.Layout, kit.ReadMap(), new ToolPaths(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty), new TargetTools(kit.Root.Environment), kit.Root.Environment, PlatformKit.LinuxRoad, said.Add);

        var outcome = await maker.MakeAsync("C1", Ct);

        outcome.Should().BeOfType<MakeOutcome.Stopped>().Which.Should().Match<MakeOutcome.Stopped>(stopped => stopped.ExitCode == 4 && stopped.Why.Contains("made already", StringComparison.Ordinal));
        MakeKit.Describe(paths.Folder).Should().Equal(before, "the chain's Make(arm) calls this unit: an arm with its marker is whole, and nothing of it is removed — the verb's own filter is not the only guard");
        said.Should().BeEmpty();
    }

    [Fact]
    public async Task A_failed_warm_build_exits_3_and_leaves_no_marker()
    {
        MakeKit.Posix();
        using var kit = await MakeKit.CreateAsync("C1");
        kit.Bench.Say("build.rc", "2\n");

        var run = await kit.MakeAsync();

        run.Code.Should().Be(3, run.Everything);
        File.Exists(kit.Layout.Made("C1")).Should().BeFalse("an arm whose warm build failed is not made: the marker is the last thing written");
        run.Error.Should().Contain("warm build of C1 failed").And.Contain("out/prep-build.log");
        kit.Tokens.Should().OnlyContain(token => !run.Everything.Contains(token, StringComparison.Ordinal));
        File.ReadAllText(kit.Paths("C1").PrepBuildLog).Should().Contain("Build succeeded.", "the failed build's log is what the operator reads");
    }

    [Fact]
    public async Task Every_tool_of_make_is_launched_by_its_resolved_path_under_a_built_environment()
    {
        MakeKit.Posix();
        using var kit = await MakeKit.CreateAsync("C1");
        kit.Root.WithVariables(("BENCH_DB", "Host=db;Password=x"), ("GIT_DIR", "/nonexistent/.git"), ("GITHUB_TOKEN", "ghp_value"), ("CLAUDE_CODE_OAUTH_TOKEN", "oauth-value"), ("COAI_CREDS_KEY", "vault-key"), ("KEEP_ME", "yes"));

        var run = await kit.MakeAsync();

        run.Code.Should().Be(0, run.Everything);
        var printed = TargetBench.Printed(run.Output);
        printed.Keys.Should().Contain(["git", "node", "dotnet"], "the resolved executables are printed before the first launch");
        var calls = kit.Bench.Recorded();
        var script = Path.Combine(kit.Paths("C1").CloneFolder, ".agents", "conventions", "tools", "low-priority.mjs");
        calls.Where(call => call.Tool == "dotnet").Select(call => call.Executable).Should().ContainSingle().Which.Should().Be(printed["dotnet"], "the build is started by the REAL path that was printed, never by a name");
        var wrapped = calls.Where(call => call.Tool == "wrapper").ToList();
        wrapped.Should().ContainSingle("only the build is lowered");
        wrapped[0].NodeStartedAs.Should().Be(printed["node"], "the node that runs the target's wrapper is the printed one");
        wrapped[0].Executable.Should().Be(script, "the wrapper is the target's own script under the arm's clone");
        wrapped[0].Arguments[0].Should().Be(printed["dotnet"], "the wrapper is handed the resolved executable, not its name");
        wrapped[0].Cwd.Should().Be(kit.Paths("C1").CloneFolder);
        foreach (var call in calls.Where(call => call.Tool is "wrapper" or "dotnet"))
        {
            call.EnvironmentNames.Should().NotContain(["BENCH_DB", "GIT_DIR", "GITHUB_TOKEN", "CLAUDE_CODE_OAUTH_TOKEN", "COAI_CREDS_KEY"], $"{call.Tool}'s environment is built").And.Contain(["KEEP_ME", "PATH", "HOME"]);
        }

        var git = kit.Bench.HarnessGit();
        git.Should().NotBeEmpty("the branch is made by git");
        git.Select(call => call.Executable).Should().OnlyContain(path => path == printed["git"]);
        git.Should().OnlyContain(call => call.Arguments.Take(2).SequenceEqual(new[] { "-c", "core.fsmonitor=false" }));
        git.Should().Contain(call => call.Arguments.Contains("checkout") && call.Arguments.Contains("-b") && call.Arguments.Contains(kit.Branch));
        foreach (var call in git)
        {
            call.EnvironmentNames.Should().NotContain(["BENCH_DB", "GITHUB_TOKEN", "CLAUDE_CODE_OAUTH_TOKEN", "COAI_CREDS_KEY"]);
            call.EnvironmentNames.Where(name => name.StartsWith("GIT_", StringComparison.Ordinal)).Except(["GIT_NO_LAZY_FETCH"])
                .Where(name => !System.Text.RegularExpressions.Regex.IsMatch(name, @"^GIT_CONFIG_(COUNT|KEY_\d+|VALUE_\d+)$")).Should().BeEmpty("no GIT_* of the parent's reaches git; the rest are the harness's own driver neutralisation");
        }
    }

    [Theory]
    [InlineData("PATH", "root")]
    [InlineData("HOME", "root")]
    [InlineData("XDG_CONFIG_HOME", "target")]
    public async Task A_PATH_HOME_or_XDG_CONFIG_HOME_under_a_forbidden_root_is_refused_before_anything_is_made(string variable, string under)
    {
        MakeKit.Posix();
        using var kit = await MakeKit.CreateAsync("C1");
        var folder = under == "root" ? Path.Combine(kit.Root.Root, "h") : kit.Bench.Source;
        kit.Root.WithVariables((variable, variable == "PATH" ? string.Join(Path.PathSeparator, kit.Root.Environment["PATH"], folder) : folder));

        var run = await kit.MakeAsync();

        run.Code.Should().Be(4, run.Everything);
        run.Error.Should().Contain(variable is "PATH" ? folder : variable);
        File.Exists(kit.Layout.ArmMapFile).Should().BeFalse("nothing is written before the environment is judged");
        Directory.Exists(kit.Layout.Arms).Should().BeFalse("no arm folder is made");
        kit.Bench.Recorded().Should().BeEmpty("nothing is launched");
    }

    [Fact]
    public async Task Make_never_prints_a_token()
    {
        MakeKit.Posix();
        using var kit = await MakeKit.CreateAsync("C1", "C2");
        kit.WriteMap("C1");
        kit.Leftover("C1");

        var first = await kit.MakeAsync();

        first.Code.Should().Be(0, first.Everything);
        first.Output.Should().Contain("removed", "the removal names its folder — and a folder's name is a token");
        kit.Tokens.Should().HaveCount(2).And.OnlyContain(token => !first.Everything.Contains(token, StringComparison.Ordinal), "the console writes every line through the map's Scrub");

        File.Delete(kit.Layout.Made("C1"));
        var paths = kit.Paths("C1");
        Directory.Delete(paths.Folder, recursive: true);
        MakeKit.Put(Path.Combine(paths.CloneFolder, "x"), "x");
        Directory.CreateDirectory(paths.Out);
        File.CreateSymbolicLink(paths.Timing, "/nowhere");
        var refused = await kit.MakeAsync();

        refused.Code.Should().Be(4, refused.Everything);
        refused.Error.Should().Contain("cannot tell whether C1 ran").And.Contain("r/<token>", "the refusal names the path the bounded road named, scrubbed");
        kit.Tokens.Should().OnlyContain(token => !refused.Everything.Contains(token, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Make_prints_the_isolation_state_and_restores_nothing()
    {
        MakeKit.Posix();
        using var kit = await MakeKit.CreateAsync("C1");
        const string snapshot = """{"run":"part7","digest":"d1","since":"2026-10-30T10:00:00Z","modes":[{"path":"/r","mode":"755"}]}""";
        MakeKit.Put(kit.Layout.LockedModes, snapshot);

        var run = await kit.MakeAsync();

        run.Code.Should().Be(0, run.Everything);
        run.Output.Should().Contain("a lock was interrupted — run `lock part7` or `unlock`");
        File.ReadAllText(kit.Layout.LockedModes).Should().Be(snapshot, "make reads the snapshot and changes nothing: only lock and unlock touch a mode");
        Directory.EnumerateFileSystemEntries(kit.Root.Root).Select(Path.GetFileName).Should().NotContain(name => name!.Contains(".restored-", StringComparison.Ordinal));
        File.Exists(kit.Layout.LockedModesApplied).Should().BeFalse();
    }

    [Fact]
    public async Task A_made_arm_reads_back_by_status()
    {
        MakeKit.Posix();
        using var kit = await MakeKit.CreateAsync("C1");

        var made = await kit.MakeAsync();
        var status = await kit.StatusAsync();

        made.Code.Should().Be(0, made.Everything);
        status.Code.Should().Be(0, status.Error);
        status.Output.Should().Contain("arm C1 (scheme C): made yes, checked no, runner none recorded");
        foreach (var step in new[] { "1-code", "2-review", "3-fix", "4-gate" })
        {
            status.Output.Should().Contain($"  {step}: not started");
        }
    }
}
