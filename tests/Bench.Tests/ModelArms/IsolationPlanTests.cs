using System.Security.Cryptography;
using System.Text;
using Bench.Domain;
using FluentAssertions;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;
using ModelArms.Core.Steps;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The isolation's pure half (D11, D27, E5.S1): the plan a run is locked with and its digest, the snapshot's two files as bytes and their one reader, the restore order, the judgement of one entry at <c>unlock</c>, and <c>lock</c>'s and <c>unlock</c>'s decisions over every state. Nothing here touches a file.
/// <para>
/// What this does NOT prove: that a path exists, what kind it is or that a link leads to it (the adapter's, <c>PosixFolderIsolationTests</c>); that a decision is carried out in the right order (the verbs' tests).
/// </para></summary>
public sealed class IsolationPlanTests
{
    private const string RootPath = "/exp/root";
    private const string Home = "/exp/home";

    private static IsolationFacts Facts(string[] closed, string[] unlisted) => new(closed, unlisted);

    private static RunSpec Run(string[]? closedArms = null, string[]? closed = null) => new("part6", [["C1"]], closedArms ?? [], closed ?? []);

    private static ArmMap MapOf(params string[] arms)
    {
        var counter = 0;
        return ArmMap.Extend(ArmMap.Empty, arms, () => [0, 0, 0, 0, 0, 0, 0, (byte)++counter]);
    }

    private static Outcome<IsolationPlan> Plan(IsolationFacts facts, RunSpec run, ArmMap map, params string[] neverClosed) =>
        IsolationPlans.From(facts, run, map, RootPath, [RootPath, Home, .. neverClosed]);

    private static string Failure(Outcome<IsolationPlan> outcome) => outcome.Should().BeOfType<Outcome<IsolationPlan>.Fail>().Subject.Reason;

    private static IsolationPlan Ok(Outcome<IsolationPlan> outcome) => outcome.Should().BeOfType<Outcome<IsolationPlan>.Ok>(outcome.ToString()).Subject.Value;

    [Fact]
    public void The_closed_and_unlisted_folders_are_the_shells_in_the_shells_order()
    {
        using var root = new ExperimentRoot();
        var experiment = root.ExperimentOf();

        var plan = Ok(IsolationPlans.From(experiment.Isolation, experiment.Runs.Single(run => run.Name == "part6"), ArmMap.Empty, experiment.Root, [experiment.Root]));

        plan.Entries.Select(entry => entry.Kind).Should().Equal([IsolationKind.Closed, .. Enumerable.Repeat(IsolationKind.Unlisted, 7)], "the shell closed the old experiment first and made the rest unlistable (lock.sh:12-14)");
        plan.Entries.Select(entry => entry.Path).Should().Equal(
            Path.Combine(root.Temp, "example", "old-experiment"),
            root.Root,
            Path.Combine(root.Root, "r"),
            Path.Combine(root.Root, "pids"),
            Path.Combine(root.Temp, "example", "home", ".claude", "projects"),
            Path.Combine(root.Temp, "example", "home", ".claude", "file-history"),
            Path.Combine(root.Temp, "example", "home", ".claude", "session-env"),
            Path.Combine(root.Temp, "example", "home", ".claude", "shell-snapshots"));
        plan.Run.Should().Be("part6");
        plan.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void A_runs_closed_arms_are_resolved_through_the_map_and_closed_after_the_configured_paths()
    {
        using var root = new ExperimentRoot();
        var experiment = root.ExperimentOf();
        var part7 = experiment.Runs.Single(run => run.Name == "part7");
        var arms = new[] { "C1", "CR1", "SRS1", "C2", "CR2", "SRS2", "C3", "CR3", "SRS3" };
        var map = MapOf(arms);

        var plan = Ok(IsolationPlans.From(experiment.Isolation, part7, map, experiment.Root, [experiment.Root]));

        var armFolders = arms.Select(arm => new RootPaths(experiment.Root).Arm(map.TokenOf(arm).Ok()).Folder).ToList();
        plan.Entries.Take(1 + 9 + 3).Select(entry => entry.Path).Should().Equal(
            [Path.Combine(root.Temp, "example", "old-experiment"), .. armFolders, Path.Combine(root.Root, "judge-abs"), Path.Combine(root.Root, "judge-coai"), Path.Combine(root.Root, "chain", "part6")],
            "the configured closed path, then the run's closedArms through the map in the run's order, then the run's own closed paths");
        plan.Entries.Take(13).Should().OnlyContain(entry => entry.Kind == IsolationKind.Closed);
        plan.Entries.Skip(13).Should().OnlyContain(entry => entry.Kind == IsolationKind.Unlisted).And.HaveCount(7);

        var missing = Failure(IsolationPlans.From(experiment.Isolation, part7, MapOf(arms.Skip(1).ToArray()), experiment.Root, [experiment.Root]));
        missing.Should().Contain("runs.part7.closedArms[0]").And.Contain("'C1'").And.Contain("never made", "an arm the map lacks is refused, never skipped: a closure the operator wrote and the module ignored would be a silent lie");
    }

    [Fact]
    public void Restore_order_is_the_reverse_of_lock_order()
    {
        SavedMode Mode(string path) => new(path, IsolationKind.Closed, "755", null);

        IsolationRestoreOrder.Of([Mode("a"), Mode("b"), Mode("c")]).Select(mode => mode.Path).Should().Equal("c", "b", "a");
        IsolationRestoreOrder.Of([]).Should().BeEmpty();
    }

    [Fact]
    public void The_digest_is_the_sha256_of_the_kind_and_path_lines_in_plan_order()
    {
        IReadOnlyList<IsolationEntry> entries = [new("/a", IsolationKind.Closed), new("/b", IsolationKind.Unlisted)];
        var expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("closed\t/a\nunlisted\t/b\n")));

        IsolationPlans.DigestOf(entries).Should().Be(expected);
        IsolationPlans.DigestOf([entries[1], entries[0]]).Should().NotBe(expected, "the order is part of what was locked");
        IsolationPlans.DigestOf([new("/a", IsolationKind.Unlisted), entries[1]]).Should().NotBe(expected, "the kind is part of it");
        expected.Should().MatchRegex("^[0-9a-f]{64}$");

        var facts = Facts(["/a"], ["/b"]);
        Ok(Plan(facts, Run(), ArmMap.Empty)).Digest.Should().Be(expected).And.Be(Ok(Plan(facts, Run(), ArmMap.Empty)).Digest);
    }

    [Theory]
    [InlineData(new[] { "/a", "/a/" }, new string[0], "isolation.closed[1]", "is named twice")]
    [InlineData(new[] { "/a" }, new[] { "/a/b" }, "isolation.unlisted[0]", "is inside closed /a")]
    [InlineData(new[] { "/a", "/a/b" }, new string[0], "isolation.closed[1]", "is inside closed /a")]
    [InlineData(new[] { "/a/b", "/a" }, new string[0], "isolation.closed[0]", "is inside closed /a")]
    [InlineData(new[] { RootPath }, new string[0], "isolation.closed[0]", "is, or contains, the experiment root — it cannot be closed")]
    [InlineData(new[] { "/exp" }, new string[0], "isolation.closed[0]", "is, or contains, the experiment root — it cannot be closed")]
    [InlineData(new[] { Home }, new string[0], "isolation.closed[0]", $"is, or contains, {Home} — it cannot be closed")]
    [InlineData(new[] { "/src" }, new string[0], "isolation.closed[0]", "is, or contains, /src/target — it cannot be closed")]
    [InlineData(new[] { "/x" }, new string[0], "isolation.closed[0]", "is, or contains, /x/arm — it cannot be closed")]
    [InlineData(new[] { "/a\0b" }, new string[0], "isolation.closed[0]", "NUL")]
    public void A_plan_refuses_a_duplicate_a_nested_closed_entry_a_forbidden_closure_and_a_nul_naming_the_entry(string[] closed, string[] unlisted, string entry, string sentence)
    {
        var reason = Failure(Plan(Facts(closed, unlisted), Run(), ArmMap.Empty, "/src/target", "/x/arm"));

        reason.Should().Contain(entry).And.Contain(sentence);
    }

    [Theory]
    [InlineData(new string[0], new[] { "/a" })]
    [InlineData(new[] { "/a/b" }, new[] { "/a" })]
    [InlineData(new string[0], new[] { "/a", "/a/b" })]
    public void An_entry_inside_an_unlisted_one_is_allowed_because_a_folder_at_300_is_still_traversed_by_name(string[] closed, string[] unlisted)
    {
        Ok(Plan(Facts(closed, unlisted), Run(), ArmMap.Empty)).Entries.Should().HaveCount(closed.Length + unlisted.Length);
    }

    public static TheoryData<string, LockState, string, Type> LockRows
    {
        get
        {
            const string Snapshot = """{"run":"part7","digest":"d1","since":"2026-10-30T10:00:00Z","modes":[{"path":"/a","kind":"closed","mode":"755"}]}""";
            const string NoKind = """{"run":"part7","digest":"d1","since":"2026-10-30T10:00:00Z","modes":[{"path":"/a","mode":"755"}]}""";
            return new()
            {
                { "nothing stands", new LockState.None(), string.Empty, typeof(LockStep.Fresh) },
                { "an interrupted lock of another run is recovered", new LockState.Applying("part7", "t"), Snapshot, typeof(LockStep.Recover) },
                { "an interrupted lock whose modes do not parse is refused", new LockState.Applying("part7", "t"), NoKind, typeof(LockStep.RefuseInconsistent) },
                { "the same run and set", new LockState.Applied("part6", "t", "d1"), string.Empty, typeof(LockStep.AlreadyLocked) },
                { "the same run, another set", new LockState.Applied("part6", "t", "d2"), string.Empty, typeof(LockStep.RefuseOtherSet) },
                { "another run", new LockState.Applied("part7", "t", "d1"), string.Empty, typeof(LockStep.RefuseOtherSet) },
                { "inconsistent", new LockState.Inconsistent("two files of two runs"), string.Empty, typeof(LockStep.RefuseInconsistent) },
            };
        }
    }

    [Theory]
    [MemberData(nameof(LockRows))]
    public void Lock_decision_follows_d11_over_every_state(string row, LockState state, string snapshot, Type expected)
    {
        var plan = new IsolationPlan("part6", "d1", [new("/a", IsolationKind.Closed)]);
        Read<string> text = snapshot.Length == 0 ? new Read<string>.Absent() : new Read<string>.Present(snapshot);

        var step = LockDecision.Of(state, plan, text);

        step.Should().BeOfType(expected, row);
        if (step is LockStep.RefuseOtherSet other)
        {
            other.LockedRun.Should().Be(state is LockState.Applied applied ? applied.Run : string.Empty);
        }

        if (step is LockStep.Recover recover)
        {
            recover.Old.Run.Should().Be("part7");
        }
    }

    public static TheoryData<string, LockState, Read<string>, Read<string>, Type> UnlockRows
    {
        get
        {
            const string Snapshot = """{"run":"part7","digest":"d1","since":"t","modes":[{"path":"/a","kind":"unlisted","mode":"755","dev":1,"ino":2}]}""";
            const string Applied = """{"run":"part7","digest":"d1","verifiedAt":"t"}""";
            return new()
            {
                { "nothing", new LockState.None(), new Read<string>.Absent(), new Read<string>.Absent(), typeof(UnlockStep.Nothing) },
                { "applying", new LockState.Applying("part7", "t"), new Read<string>.Present(Snapshot), new Read<string>.Absent(), typeof(UnlockStep.Restore) },
                { "applied", new LockState.Applied("part7", "t", "d1"), new Read<string>.Present(Snapshot), new Read<string>.Present(Applied), typeof(UnlockStep.Restore) },
                { "two runs, the snapshot parses", new LockState.Inconsistent("two runs"), new Read<string>.Present(Snapshot), new Read<string>.Present(Applied.Replace("part7", "part6", StringComparison.Ordinal)), typeof(UnlockStep.Restore) },
                { "the applied file alone", new LockState.Inconsistent("alone"), new Read<string>.Absent(), new Read<string>.Present(Applied), typeof(UnlockStep.RenameAppliedAlone) },
                { "a refused applied file alone", new LockState.Inconsistent("alone"), new Read<string>.Absent(), new Read<string>.Refused("a link"), typeof(UnlockStep.RenameAppliedAlone) },
                { "the snapshot is a link", new LockState.Inconsistent("a link"), new Read<string>.Refused("a link"), new Read<string>.Absent(), typeof(UnlockStep.Refuse) },
                { "the snapshot is not JSON", new LockState.Inconsistent("not json"), new Read<string>.Present("not json"), new Read<string>.Absent(), typeof(UnlockStep.Refuse) },
            };
        }
    }

    [Theory]
    [MemberData(nameof(UnlockRows))]
    public void Unlock_decision_restores_from_a_parsing_snapshot_renames_an_applied_file_alone_and_refuses_one_that_does_not_parse(string row, LockState state, Read<string> snapshot, Read<string> applied, Type expected)
    {
        var step = UnlockDecision.Of(state, snapshot, applied);

        step.Should().BeOfType(expected, row);
        if (step is UnlockStep.Restore restore)
        {
            restore.AppliedPresent.Should().Be(applied is not Read<string>.Absent, row);
            restore.Saved.Modes.Should().ContainSingle().Which.Identity.Should().Be(new NodeIdentity(1, 2));
        }
    }

    [Fact]
    public void The_snapshot_and_applied_texts_round_trip_through_the_one_reader()
    {
        var saved = new SavedSnapshot("part6", new string('a', 64), "2026-10-31T10:00:00Z", [new("/a", IsolationKind.Closed, "755", new NodeIdentity(64769, 1234567)), new("/b c", IsolationKind.Unlisted, "2755", null)]);

        var text = SnapshotFiles.Snapshot(saved);

        text.Should().EndWith("}\n").And.Contain("\"kind\":\"closed\"").And.Contain("\"dev\":64769,\"ino\":1234567");
        text.Split("\"path\":\"/b c\"")[1].Should().NotContain("dev", "the runtime road records no device and no inode");
        var back = LockSnapshots.Saved(text).Ok();
        back.Run.Should().Be(saved.Run);
        back.Digest.Should().Be(saved.Digest);
        back.Since.Should().Be(saved.Since);
        back.Modes.Should().Equal(saved.Modes);

        LockSnapshots.Saved(text.Replace(",\"ino\":1234567", string.Empty, StringComparison.Ordinal)).Should().BeOfType<Outcome<SavedSnapshot>.Fail>("a device without its inode is refused");

        var applied = SnapshotFiles.Applied("part6", saved.Digest, "2026-10-31T10:00:05Z");
        applied.Should().EndWith("}\n");
        LockSnapshots.State(new Read<string>.Present(text), new Read<string>.Present(applied)).Should().Be(new LockState.Applied("part6", "2026-10-31T10:00:05Z", saved.Digest));

        foreach (var (bits, spelled) in new[] { (0, "0"), (0x0C0, "300"), (0x1ED, "755"), (0x5ED, "2755"), (0x3FF, "1777"), (0x9ED, "4755") })
        {
            SnapshotFiles.ModeText(bits).Should().Be(spelled);
            SnapshotFiles.ModeBits(spelled).Ok().Should().Be(bits);
        }

        foreach (var bad in new[] { "8", "77777", string.Empty, "-1", "7 5" })
        {
            SnapshotFiles.ModeBits(bad).Should().BeOfType<Outcome<int>.Fail>(bad);
        }

        SnapshotFiles.ModeText(0x0C0).Should().Be("300", "the kind of the 300 mode, spelled once");
    }

    private static SavedMode Saved(IsolationKind kind, string mode, NodeIdentity? identity = null) => new("/x/y", kind, mode, identity);

    private static PinnedFacts Now(int mode, NodeIdentity? identity = null, bool link = false, bool folder = true, bool file = false) => new(link, folder, file, mode, identity);

    private const int Mode000 = 0;
    private const int Mode300 = 0x0C0;
    private const int Mode755 = 0x1ED;
    private const int Mode700 = 0x1C0;
    private const int Mode500 = 0x140;

    public static TheoryData<string, SavedMode, PinnedFacts?, bool, Type, string> JudgementRows => new()
    {
        { "a closed folder still locked", Saved(IsolationKind.Closed, "755", new NodeIdentity(1, 2)), Now(Mode000, new NodeIdentity(1, 2)), true, typeof(EntryVerdict.Restore), string.Empty },
        { "already at its saved mode", Saved(IsolationKind.Closed, "755", new NodeIdentity(1, 2)), Now(Mode755, new NodeIdentity(1, 2)), true, typeof(EntryVerdict.AlreadyRestored), string.Empty },
        { "the operator's own mode", Saved(IsolationKind.Closed, "755", new NodeIdentity(1, 2)), Now(Mode700, new NodeIdentity(1, 2)), true, typeof(EntryVerdict.Unresolved), "mode 700 is neither the saved 755 nor the locked 0" },
        { "an unlisted folder still locked", Saved(IsolationKind.Unlisted, "755"), Now(Mode300), true, typeof(EntryVerdict.Restore), string.Empty },
        { "an unlisted folder the operator opened to 500", Saved(IsolationKind.Unlisted, "755"), Now(Mode500), true, typeof(EntryVerdict.Unresolved), "neither the saved 755 nor the locked 300" },
        { "a link now", Saved(IsolationKind.Closed, "755"), Now(Mode755, link: true, folder: false), true, typeof(EntryVerdict.Skip), "a link now" },
        { "another inode", Saved(IsolationKind.Closed, "755", new NodeIdentity(1, 2)), Now(Mode000, new NodeIdentity(1, 3)), true, typeof(EntryVerdict.Skip), "replaced since the lock (inode 2 → 3)" },
        { "gone", Saved(IsolationKind.Closed, "755"), null, true, typeof(EntryVerdict.Skip), "may still stand at 0 elsewhere" },
        { "gone, unlisted", Saved(IsolationKind.Unlisted, "755"), null, true, typeof(EntryVerdict.Skip), "may still stand at 300 elsewhere" },
        { "outside the bound before every other verdict", Saved(IsolationKind.Closed, "755"), null, false, typeof(EntryVerdict.Fail), "not under any folder this experiment names — not restored (by hand: chmod 755 /x/y)" },
        { "outside the bound and still locked", Saved(IsolationKind.Closed, "755"), Now(Mode000), false, typeof(EntryVerdict.Fail), "not under any folder this experiment names" },
        { "outside the bound but already at its saved mode (the operator did it by hand)", Saved(IsolationKind.Closed, "755"), Now(Mode755), false, typeof(EntryVerdict.AlreadyRestored), string.Empty },
        { "outside the bound and a link", Saved(IsolationKind.Closed, "755"), Now(Mode755, link: true, folder: false), false, typeof(EntryVerdict.Fail), "not under any folder this experiment names" },
        { "no identity on either side (the runtime road)", Saved(IsolationKind.Closed, "755"), Now(Mode000), true, typeof(EntryVerdict.Restore), string.Empty },
        { "a closed regular file", Saved(IsolationKind.Closed, "644"), Now(Mode000, folder: false, file: true), true, typeof(EntryVerdict.Restore), string.Empty },
        { "an unlisted entry that is a file now", Saved(IsolationKind.Unlisted, "755"), Now(Mode300, folder: false, file: true), true, typeof(EntryVerdict.Skip), "not the folder" },
    };

    [Theory]
    [MemberData(nameof(JudgementRows))]
    public void A_restored_entry_is_judged_by_its_kind_identity_and_mode_against_the_record(string row, SavedMode saved, PinnedFacts? now, bool inBound, Type verdict, string sentence)
    {
        var judged = RestoreJudgement.Of(saved, now, inBound);

        judged.Should().BeOfType(verdict, row);
        var words = judged switch
        {
            EntryVerdict.Skip skip => skip.Sentence,
            EntryVerdict.Unresolved unresolved => unresolved.Sentence,
            EntryVerdict.Fail fail => fail.Sentence,
            _ => string.Empty,
        };
        (sentence.Length == 0 ? words.Length == 0 : words.Contains(sentence, StringComparison.Ordinal)).Should().BeTrue($"{row}: the sentence was '{words}'");
    }

    [Theory]
    [InlineData("/exp/root", true)]
    [InlineData("/exp/root/pids", true)]
    [InlineData("/exp/home/.claude/projects", true)]
    [InlineData("/x/y", true)]
    [InlineData("/x/y/z", true)]
    [InlineData("/x", false)]
    [InlineData("/etc/passwd", false)]
    [InlineData("/exp/home/.ssh", false)]
    [InlineData("/exp/root-evil", false)]
    public void The_bound_of_what_unlock_may_chmod_is_the_root_the_homes_claude_folder_and_what_experiment_json_names(string path, bool inside)
    {
        IsolationPlans.InBound(path, RootPath, Home, ["/x/y"]).Should().Be(inside);
    }

    [Theory]
    [InlineData(AwakeChoice.Auto, HostKind.Windows, AwakeKind.Windows, "")]
    [InlineData(AwakeChoice.Auto, HostKind.Wsl, AwakeKind.Wsl, "")]
    [InlineData(AwakeChoice.Auto, HostKind.OtherPosix, AwakeKind.None, "")]
    [InlineData(AwakeChoice.Windows, HostKind.Windows, AwakeKind.Windows, "")]
    [InlineData(AwakeChoice.Wsl, HostKind.Wsl, AwakeKind.Wsl, "")]
    [InlineData(AwakeChoice.None, HostKind.Windows, AwakeKind.None, "")]
    [InlineData(AwakeChoice.None, HostKind.Wsl, AwakeKind.None, "")]
    [InlineData(AwakeChoice.None, HostKind.OtherPosix, AwakeKind.None, "")]
    [InlineData(AwakeChoice.Windows, HostKind.Wsl, AwakeKind.None, "awake \"windows\" needs a Windows host; this host is Wsl")]
    [InlineData(AwakeChoice.Windows, HostKind.OtherPosix, AwakeKind.None, "awake \"windows\" needs a Windows host; this host is OtherPosix")]
    [InlineData(AwakeChoice.Wsl, HostKind.Windows, AwakeKind.None, "awake \"wsl\" needs a Wsl host; this host is Windows")]
    [InlineData(AwakeChoice.Wsl, HostKind.OtherPosix, AwakeKind.None, "awake \"wsl\" needs a Wsl host; this host is OtherPosix")]
    public void HostAwakeSelection_picks_windows_wsl_or_none_from_the_facts(AwakeChoice choice, HostKind host, AwakeKind expected, string refusal)
    {
        var picked = HostAwakeSelection.For(choice, host);

        if (refusal.Length == 0)
        {
            picked.Should().BeOfType<Outcome<AwakeKind>.Ok>($"{choice} on {host}").Which.Value.Should().Be(expected);
        }
        else
        {
            picked.Should().BeOfType<Outcome<AwakeKind>.Fail>($"{choice} on {host}").Which.Reason.Should().Be(refusal, "validate prints this very sentence (HostChecks.AwakeProblem)");
        }
    }
}
