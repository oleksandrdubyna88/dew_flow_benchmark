using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Bench.Cli;
using Bench.Domain;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;
using ModelArms.Core.Tasks;
using ModelArms.Verbs;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary><c>status</c> (E3.S4, plan §4.1): every arm of the map, then the arms of the runs the map lacks, by NAME — the verb opens each file the layout spells and lists no folder, because the isolation leaves an unlisted folder at mode 300 (traversable, never listable, D11, F0). It
/// reports a step that ended on its deadline apart from a finished one, a gate that is continuing, a step that is limited or interrupted, and a marker it cannot trust as <i>unreadable</i>, never as done. It prints the isolation snapshot's state and restores nothing, prints no token
/// and creates nothing. The root is a real one (the worked example) holding the arms a test names; nothing is launched.
/// <para>
/// What this does NOT prove: a root a real <c>lock</c> isolated (E5.S1 writes the snapshot; the files here are written by hand to D11's shape), a runner's own files (the markers and lines are written by the module's writers), the task pipeline's steps (E4.S3 extends <c>status</c>), or
/// the Portable road on macOS or FreeBSD (BoundedFileTests runs it handed in).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class StatusVerbTests
{
    private static readonly TimeSpan Wall = TimeSpan.FromSeconds(10);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static void Posix() => Assert.SkipWhen(OperatingSystem.IsWindows(), "modes, links and FIFOs are POSIX");

    [DllImport("libc", SetLastError = true)]
    private static extern int mkfifo([MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint mode);

    [Fact]
    public async Task A_step_that_ended_on_its_deadline_is_listed_as_such_apart_from_a_finished_one()
    {
        using var kit = new StatusKit("C1");
        kit.Done("C1", "1-code", "finished");
        kit.Done("C1", "2-review", "hard-deadline");

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error);
        run.Lines.Should().Contain("  1-code: done (finished)");
        run.Lines.Should().Contain("  2-review: done — ended on its deadline");
        run.Lines.Should().Contain("  3-fix: not started", "the positive case: a step with nothing on disk is not started, so the two lines above are not a verb that says done for everything");
    }

    [Fact]
    public async Task The_console_dispatches_status_through_the_gate_and_prints_the_report()
    {
        using var kit = new StatusKit("C1");
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await global::ModelArms.Program.RunAsync(["status", "--experiment", kit.Root.Root], output, error, kit.Root.Environment, Ct);

        code.Should().Be(0, error.ToString());
        output.ToString().Should().StartWith($"model-arms status: {kit.Root.Root}\n").And.Contain("arm C1 (scheme C)");
    }

    [Fact]
    public async Task A_continuing_gate_is_listed_as_continuing()
    {
        using var kit = new StatusKit("C1");
        kit.Stream("C1", "4-gate", 1);
        kit.Put(kit.Paths("C1").Continue("4-gate"), ContinueMarkers.Write(new ContinueMarker(1, "every provider rate-limited")));

        var first = await kit.RunAsync();

        first.Lines.Should().Contain("  4-gate: continuing — blocked after attempt 1: every provider rate-limited");

        kit.Stream("C1", "4-gate", 2);
        var stale = await kit.RunAsync();

        stale.Output.Should().NotContain("continuing", "attempt 2 began after the marker: it names an older attempt and decides nothing (D7)");
        stale.Lines.Should().Contain("  4-gate: resumable — interrupted (no end line) at attempt 2");
    }

    [Fact]
    public async Task Status_opens_every_file_by_name_and_enumerates_no_folder()
    {
        using var kit = new StatusKit("C1", "SRS1");
        kit.Done("C1", "1-code", "finished");
        kit.Stream("C1", "2-review", 1);
        kit.Stream("C1", "2-review", 2);
        kit.Timing("C1", kit.Started("C1", "2-review", 2));
        kit.Put(kit.Layout.PidFile("C1"), "1");
        kit.Put(kit.Layout.Made("C1"), "made\n");
        var asked = new List<string>();
        var recording = new StatusReads(
            (path, limit) => Asked(asked, path, StatusReads.Real.Read(path, limit)),
            path => Asked(asked, path, StatusReads.Real.Present(path)));

        var run = await kit.RunAsync(recording);

        run.Code.Should().Be(0, run.Error);
        var expected = kit.NamesAskedFor(["C1", "SRS1"], attempts: 3);
        asked.Should().NotBeEmpty();
        asked.Where(path => !expected.Contains(path)).Should().BeEmpty("every path asked is one RootPaths or ArmPaths spells for the experiment's arms and schemes — derived from those members, never typed here");
        asked.Should().Contain([kit.Paths("C1").Done("1-code"), kit.Paths("C1").Stream("2-review", 3), kit.Layout.ArmMapFile, kit.Layout.LockedModes], "the scan is alive: the files the story names ARE asked for");
        asked.Should().NotContain(kit.Paths("C1").Stream("2-review", 4), "attempts are probed one after the other until one is absent, never past it");

        typeof(StatusReads).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(property => property.Name).Should().Equal(
            ["Read", "Present"], "the seam has two members, a read by name and a presence by name — and none that lists a folder");
        typeof(StatusReads).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(method => !method.IsSpecialName).Select(method => method.Name)
            .Should().OnlyContain(name => RecordPlumbing.Contains(name), "nothing but the record's own plumbing besides the two properties");
    }

    private static T Asked<T>(List<string> asked, string path, T answer)
    {
        lock (asked)
        {
            asked.Add(path);
        }

        return answer;
    }

    [Fact]
    public async Task Status_gives_the_same_answer_with_the_unlisted_folders_at_mode_300()
    {
        Assert.SkipWhen(!OperatingSystem.IsLinux(), "the live mode-300 check is Linux's: the other hosts' modes are not observed here");
        using var kit = new StatusKit("C1", "CR1");
        kit.Done("C1", "1-code", "hard-deadline");
        kit.Stream("C1", "2-review", 1);
        kit.Timing("C1", kit.Started("C1", "2-review", 1));
        kit.Put(kit.Layout.PidFile("C1"), "1");
        kit.Put(kit.Layout.Made("C1"), "made\n");
        kit.Put(kit.Layout.RunMarker("part6", RunStage.Locked), "locked\n");
        var open = await kit.RunAsync();
        // The three the isolation lists as unlisted, and the arm's own out/ besides: a verb that counted attempts by listing out/ would work under the real lock and fail here — the harder state.
        var folders = new[] { kit.Layout.Arms, kit.Layout.Pids, kit.Layout.Chain, kit.Paths("C1").Out };
        var closed = (UnixFileMode)Convert.ToInt32("300", 8);
        var normal = (UnixFileMode)Convert.ToInt32("755", 8);
        try
        {
            foreach (var folder in folders)
            {
                File.SetUnixFileMode(folder, closed);
            }

            Assert.SkipWhen(CanList(kit.Layout.Arms), "this user can list a mode-300 folder (root, or a capability that overrides it): the isolation would not hold either, so there is nothing to observe");

            var shut = await kit.RunAsync();

            shut.Code.Should().Be(0, shut.Error);
            shut.Output.Should().Be(open.Output, "the verb addresses every file by name, which a mode-300 folder allows");
            open.Output.Should().Contain("1-code: done — ended on its deadline").And.Contain("locked yes", "the positive case: the run read real files");
        }
        finally
        {
            foreach (var folder in folders)
            {
                File.SetUnixFileMode(folder, normal);
            }
        }
    }

    private static bool CanList(string folder)
    {
        try
        {
            _ = Directory.EnumerateFileSystemEntries(folder).ToList();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static readonly string[] RecordPlumbing = ["Equals", "GetHashCode", "ToString", "Deconstruct", "<Clone>$"];

    private static readonly string[] Forbidden = ["Enumerate", "GetFiles", "GetDirectories", "GetFileSystemEntries", "DirectoryInfo", "FileSystemInfo"];

    private static string Source(string relative) => File.ReadAllText(Path.Combine(Cli.Repository.Root, "tools", "model-arms", relative));

    [Fact]
    public void The_verbs_source_holds_no_way_to_list_a_folder_and_the_scan_still_finds_one_where_one_lives()
    {
        foreach (var file in new[]
        {
            "ModelArms/Verbs/Status.cs", "ModelArms/Verbs/ArmReport.cs", "ModelArms/Adapters/IsolationFiles.cs",
            "ModelArms/Verbs/Lock.cs", "ModelArms/Verbs/Unlock.cs", "ModelArms/Verbs/SnapshotAside.cs", "ModelArms/Verbs/RunnerLiveness.cs", "ModelArms/Verbs/IsolationLocking.cs",
            "ModelArms/Adapters/PosixFolderIsolation.cs", "ModelArms/Adapters/NoIsolation.cs", "ModelArms/Adapters/FolderIsolations.cs", "ModelArms/Adapters/RootFolders.cs", "ModelArms/Adapters/LockLog.cs",
            "ModelArms/Adapters/RootLogFile.cs", "ModelArms/Adapters/WslHostAwake.cs", "ModelArms/Adapters/WindowsAwake.cs", "ModelArms/Adapters/NoAwake.cs", "ModelArms/Adapters/HostAwakes.cs", "ModelArms/Verbs/RunAll.cs",
            "ModelArms/Verbs/Chain.cs", "ModelArms/Verbs/ChainSeams.cs", "ModelArms/Verbs/ChainRunLock.cs", "ModelArms/Verbs/ChainAdmission.cs", "ModelArms/Verbs/ChainGather.cs", "ModelArms/Verbs/ChainWalk.cs",
        })
        {
            Forbidden.Where(word => Source(file).Contains(word, StringComparison.Ordinal)).Should().BeEmpty($"{file}: a verb that lists an unlisted folder fails at mode 300, the one state the isolation exists for");
        }

        // The runtime road's VERIFICATION is the attempt to list a folder (it must fail before a name is read): the one place it is named, once.
        var runtime = Source("ModelArms/Adapters/RuntimeFolderIsolation.cs");
        Forbidden.Where(word => runtime.Contains(word, StringComparison.Ordinal)).Should().Equal(["Enumerate"], "RuntimeFolderIsolation lists nothing but the attempt that must fail");
        System.Text.RegularExpressions.Regex.Matches(runtime, "Enumerate").Should().ContainSingle("the attempt to list is made in exactly one place");

        var instance = Source("ModelArms/Adapters/BaseCopy.cs");
        instance.Should().Contain("EnumerateFileSystemInfos", "the live instance the scan must still find is BaseCopy's listing of the base's folders (E3.S3) — a scan that matches nothing passes forever");
        Forbidden.Where(word => instance.Contains(word, StringComparison.Ordinal)).Should().NotBeEmpty(
            "the scan's own words still match where a folder IS listed: a pattern that stopped matching would pass every verb above for ever");
    }

    [Theory]
    [InlineData("none", null, null, "isolation: no isolation snapshot")]
    [InlineData("applying", "snapshot", null, "isolation: a lock was interrupted — run `lock part7` or `unlock`")]
    [InlineData("applied", "snapshot", "applied", "isolation: isolation applied for part7 since 2026-10-30T10:00:05Z")]
    [InlineData("inconsistent", "snapshot", "other", "isolation: isolation snapshot inconsistent")]
    public async Task Status_reports_the_isolation_snapshots_state_and_restores_nothing(string state, string? snapshot, string? applied, string expected)
    {
        Posix();
        using var kit = new StatusKit("C1");
        var texts = new Dictionary<string, string>
        {
            ["snapshot"] = """{"run":"part7","digest":"d1","since":"2026-10-30T10:00:00Z","modes":[{"path":"/r","mode":"755"}]}""",
            ["applied"] = """{"run":"part7","digest":"d1","verifiedAt":"2026-10-30T10:00:05Z"}""",
            ["other"] = """{"run":"part7","digest":"d2","verifiedAt":"2026-10-30T10:00:05Z"}""",
        };
        if (snapshot is not null)
        {
            kit.Put(kit.Layout.LockedModes, texts[snapshot]);
        }

        if (applied is not null)
        {
            kit.Put(kit.Layout.LockedModesApplied, texts[applied]);
        }

        var before = kit.Tree();

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, $"{state}: {run.Error}");
        run.Lines.Should().Contain(line => line.StartsWith(expected, StringComparison.Ordinal), state);
        kit.Tree().Should().Be(before, "the files' bytes, names and modes are what they were: status reads the snapshot and never restores it");
        Directory.EnumerateFileSystemEntries(kit.Root.Root).Where(entry => entry.Contains(".restored-", StringComparison.Ordinal)).Should().BeEmpty();
    }

    [Fact]
    public async Task Status_never_prints_a_token()
    {
        Posix();
        using var kit = new StatusKit("C1", "CR1");
        var token = kit.Tokens[0];
        var stream = $"{kit.Root.Root}/r/{token}/out/1-code.a1.jsonl";
        kit.Stream("C1", "1-code", 1);
        kit.Timing("C1", $$"""{"kind":"start","arm":"C1","step":"1-code","attempt":1,"how":"fresh","session":"s","model":"m","start_utc":"2026-10-30T10:00:00Z","budget_s":600,"deadline":"23:12 UTC","head":"abc","stream":"{{stream}}"}""");
        kit.Put(kit.Paths("C1").Done("2-review"), "{\"attempts\":1,\"exit\":0,\"stream\":\"x\",\"end\":\"finished\"}");
        File.CreateSymbolicLink(kit.Paths("C1").Done("3-fix"), kit.Paths("C1").Done("2-review"));

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error);
        foreach (var each in kit.Tokens)
        {
            run.Lines.Should().NotContain(line => line.Contains(each, StringComparison.Ordinal), "no line holds a token of the map");
            run.Error.Should().NotContain(each);
        }

        run.Output.Should().Contain("/r/<token>/out/1-code.a1.jsonl", "the line is printed, with its token scrubbed");
        run.Output.Should().Contain("is a link", "the refusal that names a path under a token is printed too — the positive case for the second road a token could take");
    }

    [Fact]
    public async Task The_last_timing_line_is_printed_as_recorded()
    {
        using var kit = new StatusKit("C1");
        const string recorded = """{"kind": "start", "arm": "C1", "step": "2-review", "attempt": 1, "how": "fresh", "session": "s", "model": "m", "start_utc": "2026-10-30T10:00:00Z", "budget_s": 600, "deadline": "23:12 UTC (00:12 +01:00)", "head": "abc", "stream": "x"}""";
        kit.Stream("C1", "2-review", 1);
        kit.Timing("C1", """{"kind":"arm-done","arm":"C1","done_utc":"2026-10-29T10:00:00Z"}""", recorded);

        var run = await kit.RunAsync();

        run.Lines.Should().Contain($"  last line: {recorded}", "the deadline text, the spaces after the colons and the order are what the runner recorded — a parse-and-write would compact them and spell the offset again (E2 §8 item 32)");
        run.Output.Should().Contain("\"deadline\": \"23:12 UTC (00:12 +01:00)\"");
    }

    [Fact]
    public async Task A_limited_and_an_interrupted_step_are_listed_with_their_end_and_attempt()
    {
        using var kit = new StatusKit("C1");
        kit.Stream("C1", "1-code", 1);
        kit.Stream("C1", "1-code", 2);
        kit.Stream("C1", "2-review", 1);
        kit.Timing("C1", kit.Started("C1", "1-code", 2), kit.Ended("C1", "1-code", 2, "limited", limited: true), kit.Stopped("C1", "1-code", 3, "the account is out of usage"), kit.Started("C1", "2-review", 1));

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error);
        run.Lines.Should().Contain("  1-code: limited at attempt 2 — the account is out of usage");
        run.Lines.Should().Contain("  2-review: resumable — interrupted (no end line) at attempt 1");
    }

    [Fact]
    public async Task A_marker_that_is_a_link_a_fifo_or_too_big_is_unreadable_never_done()
    {
        Posix();
        using var kit = new StatusKit("C1");
        var paths = kit.Paths("C1");
        kit.Done("C1", "1-code", "finished");
        File.Move(paths.Done("1-code"), paths.Done("1-code") + ".valid");
        File.CreateSymbolicLink(paths.Done("1-code"), paths.Done("1-code") + ".valid");
        kit.Put(paths.Done("2-review"), new string(' ', 5 * 1024) + DoneJson());
        var fifo = paths.Done("3-fix");
        Directory.CreateDirectory(Path.GetDirectoryName(fifo)!);
        mkfifo(fifo, Convert.ToUInt32("600", 8)).Should().Be(0, "the fixture makes a FIFO");
        try
        {
            var running = Task.Run(() => kit.RunAsync(), Ct);

            (await Task.WhenAny(running, Task.Delay(Wall, Ct))).Should().BeSameAs(running, "the symptom of reading a FIFO as a file is the hang: the verb waits for a writer that never comes");
            var run = await running;

            run.Code.Should().Be(0, run.Error);
            run.Lines.Should().Contain(line => line.StartsWith("  1-code: unreadable — ", StringComparison.Ordinal) && line.Contains("is a link", StringComparison.Ordinal));
            run.Lines.Should().Contain(line => line.StartsWith("  2-review: unreadable — ", StringComparison.Ordinal) && line.Contains("bigger than 4096 bytes", StringComparison.Ordinal));
            run.Lines.Should().Contain(line => line.StartsWith("  3-fix: unreadable — ", StringComparison.Ordinal));
            run.Output.Should().NotContain("done (", "none of the three is done: a marker nobody can trust says nothing, and 'done' would let a step that did not finish pass");
        }
        finally
        {
            PlatformKit.ReleaseFifo(fifo);
        }
    }

    private static string DoneJson() => """{"attempts":1,"exit":0,"stream":"x","end":"finished"}""";

    [Fact]
    public async Task The_runner_is_reported_alive_gone_or_not_recorded()
    {
        Posix();
        using var kit = new StatusKit("C1", "C2", "C3", "CR1");
        using var live = await StatusKit.HeldProcess.StartAsync();
        var gone = await StatusKit.HeldProcess.ExitedPidAsync();
        kit.Put(kit.Layout.PidFile("C1"), $"{live.Pid}\n");
        kit.Put(kit.Layout.PidFile("C2"), $"{gone}\n");
        kit.Put(kit.Layout.PidFile("CR1"), "abc");

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error);
        run.Output.Should().Contain($"arm C1 (scheme C): made no, checked no, runner pid {live.Pid} alive");
        run.Output.Should().Contain($"arm C2 (scheme C): made no, checked no, runner pid {gone} gone");
        run.Output.Should().Contain("arm C3 (scheme C): made no, checked no, runner none recorded");
        run.Output.Should().Contain("arm CR1 (scheme CR): made no, checked no, runner unreadable — ");
        run.Output.Should().Contain("'abc'", "the unreadable text names what the file held");
    }

    [Fact]
    public async Task Each_runs_markers_and_each_arms_made_and_checked_markers_are_listed()
    {
        using var kit = new StatusKit("C1", "C2");
        kit.Put(kit.Layout.RunMarker("part6", RunStage.Locked), "locked\n");
        kit.Put(kit.Layout.Made("C1"), "made\n");
        kit.Put(kit.Layout.ObjectiveMarker("C1"), "checked\n");
        kit.Put(kit.Layout.Made("C2"), "made\n");

        var run = await kit.RunAsync();

        run.Lines[0].Should().Be($"model-arms status: {kit.Root.Root}");
        run.Lines[1].Should().Be("isolation: no isolation snapshot");
        run.Lines.Should().Contain("run part6: locked yes, unlocked no, arms done no");
        run.Lines.Should().Contain("run part7: locked no, unlocked no, arms done no");
        run.Output.Should().Contain("arm C1 (scheme C): made yes, checked yes, runner none recorded");
        run.Output.Should().Contain("arm C2 (scheme C): made yes, checked no, runner none recorded");
    }

    [Fact]
    public async Task Status_creates_nothing()
    {
        Posix();
        using var kit = new StatusKit("C1", "CR1");
        kit.Done("C1", "1-code", "finished");
        var bare = kit.Paths("CR1");
        Directory.Exists(bare.Out).Should().BeFalse("the fixture's second arm has no out/ — the case where a verb that prepared the arm would make one");
        var before = kit.Tree();

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error);
        kit.Tree().Should().Be(before, "the root's recursive listing, with each file's mode and bytes, is what it was");
        Directory.Exists(bare.Out).Should().BeFalse("status does not prepare an arm");
        run.Output.Should().Contain("arm CR1 (scheme CR)", "the verb did look at the bare arm — a run that printed nothing would also change nothing");
    }

    [Fact]
    public async Task An_arm_of_a_run_that_is_not_in_the_map_is_listed_as_not_made()
    {
        using var kit = new StatusKit("C1");

        var run = await kit.RunAsync();

        run.Lines.Should().Contain("arm C2 (scheme C): not made");
        run.Lines.Should().Contain("arm CHH3 (scheme CHH): not made", "every run's arms, in the order the runs name them");
        run.Output.Should().Contain("arm C1 (scheme C): made no", "the map's own arm is listed first, with its files");
        run.Output.IndexOf("arm C1 ", StringComparison.Ordinal).Should().BeLessThan(run.Output.IndexOf("arm C2 ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_arm_map_is_refused_as_a_map_or_by_the_road_with_exit_4_and_an_absent_map_is_every_arm_not_made()
    {
        Posix();
        using var kit = new StatusKit("C1");
        File.WriteAllText(kit.Layout.ArmMapFile, "[1,2]");

        var bad = await kit.RunAsync();

        bad.Code.Should().Be(4, bad.Error);
        bad.Error.Should().Contain("arm map");

        File.Delete(kit.Layout.ArmMapFile);
        var absent = await kit.RunAsync();

        absent.Code.Should().Be(0, absent.Error);
        absent.Lines.Should().Contain("arm C1 (scheme C): not made", "before make there is no map: every arm of every run is not made, and that is an answer, not an error");

        File.CreateSymbolicLink(kit.Layout.ArmMapFile, kit.Layout.ExperimentFile);
        var link = await kit.RunAsync();

        link.Code.Should().Be(4, link.Error);
        link.Error.Should().Contain("is a link");
    }

    [Fact]
    public async Task An_srs_arm_lists_its_split_its_fix_and_every_task_coder_by_name()
    {
        using var kit = new StatusKit("SRS1");
        var paths = kit.Paths("SRS1");
        var tasks = new[] { "t1", "t2", "t3" }.Select(id => new PlannedTask(id, [$"src/{id}.cs"], id + ".md", [], [])).ToList();
        kit.Put(paths.BatchesJson, BatchesFile.Write(new TaskBatches([["t1", "t2"], ["t3"]], tasks), paths.Work));
        kit.Done("SRS1", "1-split", "finished");
        kit.Done("SRS1", "2-t1-code", "finished");
        kit.Stream("SRS1", "2-t2-code", 1);
        var asked = new List<string>();
        var recording = new StatusReads(
            (path, limit) => Asked(asked, path, StatusReads.Real.Read(path, limit)),
            path => Asked(asked, path, StatusReads.Real.Present(path)));

        var run = await kit.RunAsync(recording);

        run.Code.Should().Be(0, run.Error);
        var stems = run.Lines.Where(line => line.StartsWith("  ", StringComparison.Ordinal) && !line.StartsWith("  last line", StringComparison.Ordinal)).Select(line => line.Trim().Split(':')[0]).ToList();
        stems.Should().Equal(["1-split", "2-t1-code", "2-t2-code", "2-t3-code", "3-hreview", "4-review", "5-fix", "6-gate"], "the split, then one line per task of batches.json in batch order, then the steps after the block — and no split fix that never ran");
        run.Lines.Should().Contain("  1-split: done (finished)").And.Contain("  2-t1-code: done (finished)").And.Contain("  2-t3-code: not started");
        run.Lines.Single(line => line.StartsWith("  2-t2-code:", StringComparison.Ordinal)).Should().NotContain("not started", "a stream with no end line is an attempt that began");
        var expected = kit.NamesAskedFor(["SRS1"], attempts: 3, taskIds: ["t1", "t2", "t3"]);
        asked.Where(path => !expected.Contains(path)).Should().BeEmpty("every name asked is one ArmPaths spells for the pipeline's stems — derived from those members, and nothing is listed");
        asked.Should().Contain([paths.BatchesJson, paths.Done("1-split"), paths.Stream("2-t3-code", 1), paths.Done("1b-split-fix")], "the scan is alive: batches.json, the split's marker, a coder's stream and the fix's marker (asked by name, found absent) are asked for");

        kit.Stream("SRS1", "1b-split-fix", 1);
        (await kit.RunAsync()).Lines.Should().Contain(line => line.StartsWith("  1b-split-fix: ", StringComparison.Ordinal), "the fix is listed once it has a stream");
        kit.Put(paths.BatchesJson, "{ not json");
        var refused = await kit.RunAsync();
        refused.Code.Should().Be(0, refused.Error);
        refused.Lines.Should().Contain(line => line.StartsWith("  tasks: unreadable — ", StringComparison.Ordinal), "batches.json that does not read is said so, never read as no tasks");
    }

    [Fact]
    public async Task A_scheme_the_experiment_lacks_and_a_task_entry_are_one_line_each()
    {
        using var kit = new StatusKit("C1", "SRS1", "X1");
        kit.Done("SRS1", "3-hreview", "finished");

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error);
        run.Lines.Should().Contain("arm X1: its scheme X is not among the experiment's schemes (C, CR, SRS, CHH)");
        run.Lines.Should().Contain("  1-split: not started", "the block's split is listed by its stem (E4.S3)");
        run.Lines.Should().Contain("  tasks: no batches yet", "no batches.json: the coders are not listed, and no listing stands in for them");
        run.Lines.Should().Contain("  3-hreview: done (finished)");
        Regex.Matches(run.Output, "^  tasks:", RegexOptions.Multiline).Should().ContainSingle();
    }

    [Fact]
    public async Task A_token_inside_a_why_or_a_pid_text_is_never_printed_even_in_part()
    {
        using var kit = new StatusKit("C1", "C2");
        var token = kit.Tokens[0];
        kit.Stream("C1", "4-gate", 1);
        kit.Put(kit.Paths("C1").Continue("4-gate"), ContinueMarkers.Write(new ContinueMarker(1, new string('x', 105) + $"/r/{token}/arm/gate.json is a link")));
        kit.Stream("C1", "1-code", 1);
        kit.Timing("C1", kit.Started("C1", "1-code", 1), kit.Ended("C1", "1-code", 1, "limited", limited: true), kit.Stopped("C1", "1-code", 3, new string('y', 105) + $"/r/{token}/out/1-code.a1.jsonl"));
        kit.Put(kit.Layout.PidFile("C2"), new string('p', 10) + token);

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error);
        run.Output.Should().Contain("4-gate: continuing", "the positive case: the lines were printed");
        run.Output.Should().Contain("1-code: limited at attempt 1").And.Contain("runner unreadable");
        foreach (var each in kit.Tokens)
        {
            for (var length = 8; length <= each.Length; length++)
            {
                run.Output.Should().NotContain(each[..length], $"a text cut at 120 or 20 characters inside a token leaves a part of it, and Scrub replaces only the whole ({each})");
            }
        }
    }

    [Fact]
    public async Task A_refused_timing_file_makes_every_step_a_marker_does_not_decide_unreadable()
    {
        Posix();
        using var kit = new StatusKit("C1");
        kit.Done("C1", "1-code", "finished");
        kit.Stream("C1", "2-review", 1);
        File.CreateSymbolicLink(kit.Paths("C1").Timing, kit.Paths("C1").Done("1-code"));

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error);
        run.Lines.Should().Contain("  1-code: done (finished)", "a marker decides this step whatever the timing file is");
        run.Lines.Should().Contain(line => line.StartsWith("  2-review: unreadable — timing.jsonl: ", StringComparison.Ordinal) && line.Contains("is a link", StringComparison.Ordinal), "an end line that cannot be read does not make an attempt look interrupted");
        run.Lines.Should().Contain(line => line.StartsWith("  3-fix: unreadable — timing.jsonl: ", StringComparison.Ordinal), "nor a step look not started");
        run.Lines.Should().Contain(line => line.StartsWith("  last line: unreadable — ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task What_a_file_or_a_kernel_said_is_printed_escaped_never_raw()
    {
        using var kit = new StatusKit("C1");
        File.WriteAllText(kit.Layout.ArmMapFile, """{"\u001b[31mC1": "0000000000000011"}""");

        var refused = await kit.RunAsync();

        refused.Code.Should().Be(4, refused.Error);
        refused.Error.Should().Contain("\\u001B").And.NotContain("\u001b", "an ESC in a map key must not reach a terminal");

        File.WriteAllText(kit.Layout.ArmMapFile, ArmMap.Extend(ArmMap.Empty, ["C1"], () => [0, 0, 0, 0, 0, 0, 0, 0x11]).ToJson());
        var reads = new StatusReads(StatusReads.Real.Read, path => path.EndsWith("made-C1", StringComparison.Ordinal) ? Outcome<bool>.Failure("a\u001b[0mb") : StatusReads.Real.Present(path));

        var unknown = await kit.RunAsync(reads);

        unknown.Output.Should().Contain("made unknown (a\\u001B[0mb)").And.NotContain("\u001b");
    }
}
