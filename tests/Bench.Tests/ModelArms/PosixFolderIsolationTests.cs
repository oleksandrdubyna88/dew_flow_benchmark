using System.Globalization;
using System.Text.Json.Nodes;
using Bench.Tests.Probes.Driver;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;
using ModelArms.Verbs;
using Xunit;
using ModelArmsApp = ModelArms;

namespace Bench.Tests.ModelArms;

/// <summary>A hook that stands in for a process that dies half-way: it throws, and the test sees where the lock stopped.</summary>
internal sealed class TestCrash : Exception;

/// <summary><c>lock</c> and <c>unlock</c> on the KERNEL road (Linux x86-64, E5.S1; D11): the modes set through descriptors that pin the entries, the snapshot durable before the first change, the recovery of a lock killed half-way — in this process and by a REAL <c>SIGKILL</c> in another — the observation that the isolation holds
/// before it is recorded, the roll-back, the restore judged entry by entry, and the writers' lock. Every test runs over <see cref="IsolationKit"/>: its own HOME, its own root, every closed folder opened again by the temp folder's dispose. Linux only; the rows skip on a road that is not Kernel/Kernel, with the road's line.
/// <para>
/// What this does NOT prove: root or <c>CAP_DAC_OVERRIDE</c> (refused before anything, never observed here — the verification is the observation, with a stand-in); a Windows drive's ignoring of modes except through the opt-in row (<c>MODEL_ARMS_DRVFS_SCRATCH</c>); a power loss between the snapshot's rename and the journal; a same-user <c>chmod</c> beside the module (parent §9).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class PosixFolderIsolationTests
{
    private static readonly string[] Original = ["755", "700", "755", "755", "755", "755", "755", "755"];
    private static readonly string[] Locked = ["0", "300", "300", "300", "300", "300", "300", "300"];

    private static IsolationKit Kit()
    {
        var kit = new IsolationKit();
        var road = PlatformFactsReader.CurrentRoad;
        Assert.SkipUnless(road is { Trust: TrustRoad.Kernel, Bounded: BoundedRoad.Kernel }, $"the kernel road is Linux x86-64 with statx; this host's road is: {road.Platform}");
        return kit;
    }

    private static IsolationHands Crash(int after) => new(null, null, (index, _) => ThrowAfter(index, after), null);

    private static void ThrowAfter(int index, int after)
    {
        if (index == after)
        {
            throw new TestCrash();
        }
    }

    private static int Chmod(string path, int mode)
    {
        File.SetUnixFileMode(path, (UnixFileMode)mode);
        return 0;
    }

    private static string Ctx(IsolationRun run) => $"stdout: {run.Output} stderr: {run.Error}";

    [Fact]
    public async Task Modes_are_saved_before_the_first_change_and_restored_in_reverse()
    {
        using var kit = Kit();

        var locked = await kit.LockAsync();

        locked.Code.Should().Be(0, Ctx(locked));
        var saved = kit.SavedOf();
        saved.Modes.Should().HaveCount(8);
        saved.Modes.Select(mode => mode.Mode).Should().Equal(Original, "the saved modes are what stat -c %a reads before the lock");
        saved.Modes.Select(mode => mode.Path).Should().Equal(kit.Entries);
        saved.Modes.Should().OnlyContain(mode => mode.Identity != null, "the kernel road records the device and the inode");
        saved.Modes.Select(mode => mode.Kind).Should().Equal([IsolationKind.Closed, .. Enumerable.Repeat(IsolationKind.Unlisted, 7)]);
        kit.Modes().Should().Equal(Locked);
        kit.Events.Clear();

        var unlocked = await kit.UnlockAsync();

        unlocked.Code.Should().Be(0, Ctx(unlocked));
        kit.Modes().Should().Equal(Original);
        kit.Events.Where(each => each.StartsWith("restore ", StringComparison.Ordinal)).Should().Equal(kit.Entries.Reverse().Select(path => "restore " + path), "unlock restores in the reverse of the lock's order");
        kit.Files().Should().Equal(".locked-modes.restored-" + IsolationKit.Stamp, ".locked-modes.applied.restored-" + IsolationKit.Stamp);
        kit.Snapshot().Should().BeOfType<LockState.None>();
    }

    [Fact]
    public async Task The_snapshot_is_durable_before_the_first_mode_changes()
    {
        using var kit = Kit();
        IReadOnlyList<string>? modesThen = null;
        LockState? stateThen = null;
        var existed = false;
        var hands = new IsolationHands(null, null, null, () =>
        {
            existed = File.Exists(kit.Layout.LockedModes);
            stateThen = kit.Snapshot();
            modesThen = kit.Modes();
            throw new TestCrash();
        });

        var crashed = async () => await kit.LockAsync(hands: hands);

        await crashed.Should().ThrowAsync<TestCrash>();
        existed.Should().BeTrue(".locked-modes must exist before the first mode changes");
        stateThen.Should().BeOfType<LockState.Applying>().Which.Run.Should().Be("part6");
        modesThen.Should().Equal(Original, "no mode had been changed when the snapshot was already on disk");
        kit.Modes().Should().Equal(Original);
        kit.Snapshot().Should().BeOfType<LockState.Applying>();
    }

    [Fact]
    public async Task A_lock_killed_half_way_is_restored_and_applied_afresh_by_the_next_lock()
    {
        using var kit = Kit();
        var crashed = async () => await kit.LockAsync(hands: Crash(after: 1));
        await crashed.Should().ThrowAsync<TestCrash>();
        kit.Modes().Should().Equal(["0", "300", "755", "755", "755", "755", "755", "755"], "entries 0-1 were changed, 2-7 were not");
        kit.Snapshot().Should().BeOfType<LockState.Applying>();
        kit.Events.Clear();

        var again = await kit.LockAsync();

        again.Code.Should().Be(0, Ctx(again));
        again.Output.Should().Contain("a lock was interrupted (snapshot of part6 since").And.Contain("restored 8 of 8, now locking afresh").And.Contain("locked — closed 1, unlisted 7");
        again.Output.IndexOf("interrupted", StringComparison.Ordinal).Should().BeLessThan(again.Output.IndexOf("locked — closed", StringComparison.Ordinal));
        kit.Modes().Should().Equal(Locked);
        kit.Files().Should().Contain(".locked-modes.restored-" + IsolationKit.Stamp);
        kit.Events.Should().Contain(each => each.StartsWith("restore ", StringComparison.Ordinal), "the recovery restored through the saved record before it locked afresh");
        kit.Snapshot().Should().BeOfType<LockState.Applied>().Which.Digest.Should().Be(kit.Plan().Digest);
    }

    [Fact]
    public async Task A_lock_killed_in_another_process_is_recovered_by_a_new_process_without_its_descriptors()
    {
        using var kit = Kit();
        var crash = await ModelArmsBinary.RunCrasherAsync(kit.Root, "part6");
        crash.Code.Should().NotBe(9, "the helper must have been killed half-way — it finished instead: " + crash.Output + crash.Error);
        crash.Code.Should().NotBe(0).And.Be(128 + 9, "a process that died by SIGKILL exits 137 as the runtime reports it");
        kit.Modes().Should().Equal(["0", "300", "755", "755", "755", "755", "755", "755"], "the dead process had applied the first two entries");
        kit.Snapshot().Should().BeOfType<LockState.Applying>();
        var crashedText = File.ReadAllText(kit.Layout.LockedModes);

        var retry = await ModelArmsBinary.RunAsync(kit.Root, "lock", "part6");

        retry.Code.Should().Be(0, retry.Output + retry.Error);
        retry.Output.Should().Contain("a lock was interrupted (snapshot of part6 since").And.Contain("restored 8 of 8, now locking afresh").And.Contain("locked — closed 1, unlisted 7");
        kit.Snapshot().Should().BeOfType<LockState.Applied>();
        kit.SavedOf().Modes.Select(mode => mode.Mode).Should().Equal(Original, "the modes were original again before the fresh apply");
        var unlocked = await ModelArmsBinary.RunAsync(kit.Root, "unlock");
        unlocked.Code.Should().Be(0, unlocked.Output + unlocked.Error);
        kit.Modes().Should().Equal(Original);
        Archived(kit).Should().Contain(crashedText, "the interrupted lock's record is kept, byte for byte, in .locked-modes.restored-<utc>");

        // (b) the entries are not what they were: a link where a folder was, a folder made again
        await ModelArmsBinary.RunCrasherAsync(kit.Root, "part6");
        var elsewhere = kit.Root.Scratch("elsewhere");
        File.SetUnixFileMode(elsewhere, IsolationKit.Mode755);
        var r = Path.Combine(kit.Root.Root, "r");
        Directory.Delete(r);
        Directory.CreateSymbolicLink(r, elsewhere);
        var projects = kit.ClaudeFolders[0];
        Directory.Delete(projects);
        Directory.CreateDirectory(projects);
        File.SetUnixFileMode(projects, IsolationKit.Mode755);

        var refused = await ModelArmsBinary.RunAsync(kit.Root, "lock", "part6");

        refused.Code.Should().Be(4, refused.Output + refused.Error);
        refused.Output.Should().Contain($"{r}: a link now — not restored").And.Contain($"{projects}: replaced since the lock (inode");
        refused.Error.Should().Contain($"{r} is a symbolic link");
        File.Exists(kit.Layout.LockedModes).Should().BeFalse("the old snapshot was archived and no fresh one was written");
        Archived(kit).Should().NotBeEmpty();
        File.GetUnixFileMode(elsewhere).Should().Be(IsolationKit.Mode755, "nothing a link points at is ever changed");
        File.GetUnixFileMode(kit.Root.Root).Should().Be((UnixFileMode)0x1C0, "the recovery put the root back at 700");
        File.GetUnixFileMode(projects).Should().Be(IsolationKit.Mode755, "the new folder at a locked name is not the one that was locked, so it is left alone");
    }

    private static string Archived(IsolationKit kit) =>
        string.Concat(Directory.EnumerateFiles(kit.Root.Root, ".locked-modes.restored-*").Order(StringComparer.Ordinal).Select(File.ReadAllText));

    [Fact]
    public async Task A_lock_on_an_applied_snapshot_of_the_same_run_and_set_re_verifies_and_re_applies_what_was_opened()
    {
        using var kit = Kit();
        (await kit.LockAsync()).Code.Should().Be(0);
        var snapshot = File.ReadAllBytes(kit.Layout.LockedModes);
        var applied = File.ReadAllBytes(kit.Layout.LockedModesApplied);
        kit.Events.Clear();

        var second = await kit.LockAsync();

        second.Code.Should().Be(0, Ctx(second));
        second.Output.Should().Contain($"already locked for part6 since {IsolationKit.Instant.ToString(SnapshotFiles.Utc, CultureInfo.InvariantCulture)}");
        kit.Events.Should().Equal("verify");
        File.ReadAllBytes(kit.Layout.LockedModes).Should().Equal(snapshot);
        File.ReadAllBytes(kit.Layout.LockedModesApplied).Should().Equal(applied);

        File.SetUnixFileMode(kit.Pids, IsolationKit.Mode755);
        kit.Events.Clear();
        var opened = await kit.LockAsync();

        opened.Code.Should().Be(0, Ctx(opened));
        opened.Output.Should().Contain("already locked for part6 since").And.Contain($"— re-applied 1 entries that were open ({kit.Pids})");
        kit.Modes().Should().Equal(Locked, "the opened entry was closed again");
        kit.Events.Should().Equal("verify", "reapply " + kit.Pids, "verify");
        File.ReadAllBytes(kit.Layout.LockedModes).Should().Equal(snapshot, "the standing snapshot stays the authority: no new one");
        File.ReadAllBytes(kit.Layout.LockedModesApplied).Should().Equal(applied, "and no new applied file");

        Directory.Delete(kit.Pids);
        Directory.CreateDirectory(kit.Pids);
        File.SetUnixFileMode(kit.Pids, IsolationKit.Mode755);
        var replaced = await kit.LockAsync();

        replaced.Code.Should().Be(4, Ctx(replaced));
        replaced.Error.Should().Contain(kit.Pids).And.Contain("replaced since the lock").And.Contain("unlock");
        File.GetUnixFileMode(kit.Pids).Should().Be(IsolationKit.Mode755, "a folder that is not the one that was locked is never locked as if it were");
    }

    [Fact]
    public async Task A_lock_for_another_run_or_closed_set_over_an_applied_snapshot_is_refused_asking_for_unlock()
    {
        using var kit = Kit();
        using var fake = new FakeCli();
        await kit.AddArmAsync("C1", fake, hang: false);
        kit.UsePart7WithOneClosedArm();
        (await kit.LockAsync()).Code.Should().Be(0);
        var before = kit.Modes();
        kit.Events.Clear();

        var other = await kit.LockAsync("part7");

        other.Code.Should().Be(4, Ctx(other));
        other.Error.Should().Contain("locked for part6 with a different closed set — unlock first");
        kit.Modes().Should().Equal(before);
        kit.Events.Should().BeEmpty();

        kit.Root.Edit(top => top["runs"]!["part6"]!.AsObject()["closed"] = new JsonArray("<root>/chain/part6"));
        var set = await kit.LockAsync();

        set.Code.Should().Be(4, Ctx(set));
        set.Error.Should().Contain("locked for part6 with a different closed set — unlock first");
        kit.Modes().Should().Equal(before);
    }

    [Fact]
    public async Task The_applied_state_is_a_second_immutable_file_and_a_crash_before_it_reads_as_applying()
    {
        using var kit = Kit();
        var crashed = async () => await kit.LockAsync(hands: Crash(after: 7));
        await crashed.Should().ThrowAsync<TestCrash>();

        var state = kit.Snapshot();

        state.Should().BeOfType<LockState.Applying>("every mode was applied but nothing yet said the isolation held");
        IsolationLine.Of(state).Should().Contain("a lock was interrupted — run `lock part6` or `unlock`");
        File.Exists(kit.Layout.LockedModesApplied).Should().BeFalse();
        kit.Modes().Should().Equal(Locked);

        var again = await kit.LockAsync();

        again.Code.Should().Be(0, Ctx(again));
        kit.Snapshot().Should().BeOfType<LockState.Applied>();
        LockSnapshots.Saved(File.ReadAllText(kit.Layout.LockedModes)).Should().BeOfType<Bench.Domain.Outcome<SavedSnapshot>.Ok>();
        File.ReadAllText(kit.Layout.LockedModesApplied).Should().Contain("\"verifiedAt\"");
    }

    [Fact]
    public async Task Without_an_isolation_block_lock_and_unlock_are_logged_no_ops()
    {
        using var kit = Kit();
        using var fake = new FakeCli();
        await kit.AddArmAsync("C1", fake, hang: false);
        kit.Root.Edit(top =>
        {
            top.AsObject().Remove("isolation");
            top["runs"]!["part7"]!.AsObject().Remove("closedArms");
            top["runs"]!["part7"]!.AsObject().Remove("closed");
        });

        var lockRun = await kit.LockAsync();

        lockRun.Code.Should().Be(0, Ctx(lockRun));
        lockRun.Output.Should().Contain("no isolation configured — nothing to lock");
        File.Exists(kit.Layout.LockedModes).Should().BeFalse();
        File.ReadAllLines(kit.Layout.LockLog).Last().Should().Contain("no isolation configured — nothing to lock");
        var unlockRun = await kit.UnlockAsync();
        unlockRun.Code.Should().Be(0, Ctx(unlockRun));
        unlockRun.Output.Should().Contain("no isolation configured — nothing to unlock");

        kit.UsePart7WithOneClosedArm();
        var closure = await kit.LockAsync("part7");

        closure.Code.Should().Be(0, Ctx(closure));
        closure.Output.Should().Contain("locked — closed 2, unlisted 0", "a run with closures and no block is locked (§8 item 33)");
        IsolationKit.ModesOf([kit.Root.PathsOf("C1").Folder, Path.Combine(kit.Root.Root, "chain", "part6")]).Should().Equal("0", "0");
    }

    [Fact]
    public async Task Unlock_restores_a_standing_snapshot_after_the_block_was_removed()
    {
        using var kit = Kit();
        (await kit.LockAsync()).Code.Should().Be(0);
        kit.Root.Edit(top => top.AsObject().Remove("isolation"));

        var first = await kit.UnlockAsync();

        first.Code.Should().Be(3, Ctx(first));
        first.Error.Should().Contain($"{kit.OldExperiment}: not under any folder this experiment names — not restored (by hand: chmod 755 {kit.OldExperiment})").And.Contain("restored 7 of 8").And.Contain("the applied file was moved aside");
        first.Error.Should().NotContain("InvalidOperationException");
        File.Exists(kit.Layout.LockedModes).Should().BeTrue("a failed restore keeps the snapshot");
        kit.Files().Should().Contain(".locked-modes.applied.restored-" + IsolationKit.Stamp);
        kit.Snapshot().Should().BeOfType<LockState.Applying>();
        kit.Modes().Should().Equal(["0", "700", "755", "755", "755", "755", "755", "755"], "the seven root-and-home entries were restored; the configured closure outside both was not");

        File.SetUnixFileMode(kit.OldExperiment, IsolationKit.Mode755);
        var second = await kit.UnlockAsync();

        second.Code.Should().Be(0, Ctx(second));
        second.Output.Should().Contain("unlocked — restored 8 of 8");
        kit.Files().Should().Equal(".locked-modes.restored-" + IsolationKit.Stamp, ".locked-modes.applied.restored-" + IsolationKit.Stamp);
        kit.Snapshot().Should().BeOfType<LockState.None>();
    }

    [Fact]
    public async Task A_verification_failure_restores_before_refusing()
    {
        using var kit = Kit();
        var nothingApplied = new IsolationHands((_, _, _) => 0, null, null, null);

        var run = await kit.LockAsync(hands: nothingApplied);

        run.Code.Should().Be(3, Ctx(run));
        run.Error.Should().Contain("the lock could not be verified").And.Contain($"{kit.Root.Root} is still listable by this user").And.Contain("the modes were restored");
        kit.Events.Should().Equal(
            ["pin", "snapshot", .. kit.Entries.Select(path => "apply " + path), "verify", .. kit.Entries.Reverse().Select(path => "rollback " + path), "aside .locked-modes.restored-" + IsolationKit.Stamp],
            "the restore precedes the refusal, and the snapshot is moved aside only after it");
        kit.Snapshot().Should().BeOfType<LockState.None>();
        kit.Modes().Should().Equal(Original);
    }

    [Fact]
    public async Task A_roll_back_that_fails_keeps_the_snapshot_and_the_retry_restores_the_originals()
    {
        using var kit = Kit();
        var r = Path.Combine(kit.Root.Root, "r");
        var rCalls = 0;
        var hands = new IsolationHands((_, path, mode) => path == kit.Pids ? 0 : path == r && ++rCalls == 2 ? 1 : Chmod(path, mode), null, null, null);

        var run = await kit.LockAsync(hands: hands);

        run.Code.Should().Be(3, Ctx(run));
        run.Error.Should().Contain("the lock could not be verified").And.Contain($"{kit.Pids} is still listable").And.Contain("restored 7 of 8").And.Contain($"{r}: Operation not permitted").And.Contain("the snapshot is kept");
        File.Exists(kit.Layout.LockedModes).Should().BeTrue("a roll-back that failed for an entry keeps the snapshot");
        kit.Files().Should().Equal(".locked-modes");
        File.GetUnixFileMode(r).Should().Be((UnixFileMode)0xC0, "the entry that could not be restored is still locked");
        kit.Snapshot().Should().BeOfType<LockState.Applying>();

        var retry = await kit.UnlockAsync();

        retry.Code.Should().Be(0, Ctx(retry));
        kit.Modes().Should().Equal(Original);
        kit.Files().Should().Equal(".locked-modes.restored-" + IsolationKit.Stamp);
    }

    [Fact]
    public async Task A_locked_folder_cannot_be_listed_by_this_user_and_is_still_traversed_by_name()
    {
        using var kit = Kit();
        var child = Path.Combine(kit.OldExperiment, "child");
        File.WriteAllText(child, "kept");
        (await kit.LockAsync()).Code.Should().Be(0);

        var listing = () => Directory.EnumerateFileSystemEntries(kit.Pids).ToList();

        listing.Should().Throw<UnauthorizedAccessException>("mode 300 removes listing");
        var byName = Path.Combine(kit.Pids, "by-name");
        File.WriteAllText(byName, "x");
        File.ReadAllText(byName).Should().Be("x", "mode 300 keeps traversal and creation by name");
        File.GetUnixFileMode(kit.Pids).Should().Be((UnixFileMode)0xC0);
        Directory.Exists(kit.OldExperiment).Should().BeTrue();
        File.Exists(child).Should().BeFalse("a child of a 000 folder cannot be stat-ed");
        var length = () => new FileInfo(child).Length;
        length.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Verification_refuses_when_the_user_can_still_list_it()
    {
        using var kit = Kit();
        var honoursAllButPids = new IsolationHands((_, path, mode) => path == kit.Pids ? 0 : Chmod(path, mode), null, null, null);

        var run = await kit.LockAsync(hands: honoursAllButPids);

        run.Code.Should().Be(3, Ctx(run));
        run.Error.Should().Contain($"{kit.Pids} is still listable by this user");
        System.Text.RegularExpressions.Regex.Matches(run.Error, "is still listable").Should().ContainSingle("one entry ignored its mode, and the others are not blamed: EVERY entry is checked");
        kit.Modes().Should().Equal(Original);
        kit.Snapshot().Should().BeOfType<LockState.None>();
    }

    [Fact]
    public async Task Verification_refuses_a_listable_folder_on_the_windows_drive_when_one_is_named()
    {
        var scratch = System.Environment.GetEnvironmentVariable("MODEL_ARMS_DRVFS_SCRATCH") ?? string.Empty;
        Assert.SkipWhen(scratch.Length == 0, "MODEL_ARMS_DRVFS_SCRATCH names no writable folder on a 9p mount (the Windows drive under WSL) — no test writes the operator's Windows drive on its own");
        Assert.SkipUnless(File.ReadLines("/proc/mounts").Any(line => line.Split(' ')[1] == scratch.TrimEnd('/') && line.Split(' ')[2] == "9p"), $"{scratch} is not a 9p mount");
        using var kit = Kit();
        var folder = Path.Combine(scratch, "testsToDelete-modelarms-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        try
        {
            kit.Root.Edit(top =>
            {
                top["isolation"]!["closed"] = new JsonArray();
                top["isolation"]!["unlisted"] = new JsonArray(folder);
            });

            var run = await kit.LockAsync();

            run.Code.Should().Be(3, Ctx(run));
            run.Error.Should().Contain(folder).And.Contain("is still listable");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Unlock_is_idempotent_and_says_so_without_a_snapshot()
    {
        using var kit = Kit();
        var fresh = await kit.UnlockAsync();
        fresh.Code.Should().Be(0, Ctx(fresh));
        fresh.Output.Should().Contain("nothing locked");

        (await kit.LockAsync()).Code.Should().Be(0);
        (await kit.UnlockAsync()).Code.Should().Be(0);
        var third = await kit.UnlockAsync();

        third.Code.Should().Be(0, Ctx(third));
        third.Output.Should().Contain("nothing locked");
        kit.Files().Should().Equal(".locked-modes.restored-" + IsolationKit.Stamp, ".locked-modes.applied.restored-" + IsolationKit.Stamp);
    }

    [Fact]
    public async Task Lock_and_unlock_run_under_an_exclusive_file_lock()
    {
        using var kit = Kit();
        await using var holder = ModelArmsBinary.StartHolder(kit.Root, kit.Layout.IsolationLock, FakeCli.Executable);
        await holder.LineAsync("held ", TimeSpan.FromSeconds(30));
        var holderPid = int.Parse(holder.Printed().Split('|')[0].Trim().Split(' ')[1], CultureInfo.InvariantCulture);

        var refused = await kit.LockAsync(clock: TimeProvider.System, wait: TimeSpan.FromSeconds(2));

        refused.Code.Should().Be(3, Ctx(refused));
        refused.Error.Should().Contain($".isolation.lock is held by pid {holderPid} since").And.Contain("another lock or unlock is running; it was not free within 2 s");
        File.Exists(kit.Layout.LockedModes).Should().BeFalse();
        kit.Modes().Should().Equal(Original);

        var started = DateTimeOffset.UtcNow;
        var waiting = kit.LockAsync(clock: TimeProvider.System, wait: TimeSpan.FromSeconds(20));
        await Task.Delay(1000, TestContext.Current.CancellationToken);
        var killedAt = DateTimeOffset.UtcNow;
        await holder.KillAsync();
        var locked = await waiting;

        locked.Code.Should().Be(0, Ctx(locked));
        (DateTimeOffset.UtcNow - started).Should().BeGreaterThan(TimeSpan.FromMilliseconds(900), "the first WAITED for the holder");
        var verified = DateTimeOffset.ParseExact(locked.Output.Split("verified at ")[1].Trim(), SnapshotFiles.Utc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        verified.Should().BeOnOrAfter(new DateTimeOffset(killedAt.Year, killedAt.Month, killedAt.Day, killedAt.Hour, killedAt.Minute, killedAt.Second, TimeSpan.Zero), "locked after the holder was gone");

        await using var again = ModelArmsBinary.StartHolder(kit.Root, kit.Layout.IsolationLock, FakeCli.Executable);
        await again.LineAsync("held ", TimeSpan.FromSeconds(30));
        var unlockRefused = await kit.UnlockAsync(clock: TimeProvider.System, wait: TimeSpan.FromSeconds(2));
        unlockRefused.Code.Should().Be(3, Ctx(unlockRefused));
        unlockRefused.Error.Should().Contain("another lock or unlock is running");
        kit.Modes().Should().Equal(Locked);
        await again.KillAsync();
        (await kit.UnlockAsync(clock: TimeProvider.System)).Code.Should().Be(0);
        kit.Modes().Should().Equal(Original);
    }

    [Fact]
    public async Task A_runner_holds_the_isolation_lock_shared_and_a_writer_is_refused_at_once_naming_it()
    {
        using var kit = Kit();
        using var fake = new FakeCli();
        await kit.AddArmAsync("C1", fake);
        kit.UsePart7WithOneClosedArm();
        (await kit.LockAsync()).Code.Should().Be(0);
        await using var runner = kit.StartRunner("C1");
        (await IsolationKit.UntilAsync(() => File.Exists(kit.Layout.PidFile("C1")) && fake.Calls().Count > 0, TimeSpan.FromSeconds(30))).Should().BeTrue("the runner writes its pid and starts its first step: " + runner.Printed());
        var pid = int.Parse(File.ReadAllText(kit.Layout.PidFile("C1")).Trim(), CultureInfo.InvariantCulture);
        var probe = FileGateLock.Create(TimeProvider.System, TimeSpan.FromMilliseconds(50)).Ok();

        probe.Take(kit.Layout.IsolationLock, create: false, note: false).Should().BeOfType<GateLockAnswer.Busy>("a runner holds .isolation.lock shared for its life");
        File.ReadAllLines("/proc/locks").Should().Contain(line => line.Contains("FLOCK", StringComparison.Ordinal) && line.Contains("ADVISORY", StringComparison.Ordinal) && line.Contains("READ", StringComparison.Ordinal) && line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[4] == pid.ToString(CultureInfo.InvariantCulture),
            "the effect observed in /proc/locks: a shared (READ) flock held by the runner's pid");
        var started = DateTimeOffset.UtcNow;

        var unlock = await kit.UnlockAsync(clock: TimeProvider.System, wait: TimeSpan.FromSeconds(20));

        (DateTimeOffset.UtcNow - started).Should().BeLessThan(TimeSpan.FromSeconds(2), "no wait: a runner alive will not free the lock");
        unlock.Code.Should().Be(4, Ctx(unlock));
        unlock.Error.Should().Contain($".isolation.lock is held shared by a running arm (C1 is alive: pid {pid})");
        var other = await kit.LockAsync("part7", clock: TimeProvider.System, wait: TimeSpan.FromSeconds(20));
        other.Code.Should().Be(4, Ctx(other));
        other.Error.Should().Contain("is held shared by a running arm");
        kit.Modes().Should().Equal(Locked);

        var forced = await kit.UnlockAsync(force: true, clock: TimeProvider.System);

        forced.Code.Should().Be(0, Ctx(forced));
        forced.Error.Should().Contain("WARNING: --force skipped the isolation lock and the liveness check");
        File.ReadAllText(kit.Layout.LockLog).Should().Contain("WARNING: --force skipped the isolation lock and the liveness check");
        kit.Modes().Should().Equal(Original);
        RunnerLiveness.Of(kit.Layout, "C1").Should().BeOfType<RunnerState.Alive>("--force is the operator's choice; the runner lives on");
    }

    [Fact]
    public async Task No_runner_starts_between_the_last_liveness_probe_and_the_restore()
    {
        using var kit = Kit();
        using var fake = new FakeCli();
        await kit.AddArmAsync("C1", fake, hang: false);
        (await kit.LockAsync()).Code.Should().Be(0);

        var unlock = await kit.UnlockAsync(clock: TimeProvider.System, liveness: (_, arm, _) => kit.StartRunnerAndAwaitItsExit(arm));

        unlock.Code.Should().Be(0, Ctx(unlock));
        kit.Exited.Should().Be(4, "the runner started exactly after the last probe: unlock holds .isolation.lock exclusive, so its admission is refused");
        File.Exists(kit.Layout.PidFile("C1")).Should().BeFalse("a refused runner writes no pid");
        fake.Calls().Should().BeEmpty("no step ran");
        kit.Modes().Should().Equal(Original, "the restore completed");
    }

    [Theory]
    [InlineData("snapshot-not-json")]
    [InlineData("applied-alone")]
    [InlineData("two-runs")]
    [InlineData("two-digests")]
    [InlineData("snapshot-not-utf8")]
    [InlineData("snapshot-is-a-link")]
    public async Task An_inconsistent_snapshot_is_never_read_as_none_by_a_writer(string row)
    {
        using var kit = Kit();
        var modes = $$"""[{"path":{{JsonValue.Create(kit.Pids)!.ToJsonString()}},"kind":"unlisted","mode":"755"}]""";
        string Snapshot(string run, string digest) => $$"""{"run":"{{run}}","digest":"{{digest}}","since":"2026-10-31T10:00:00Z","modes":{{modes}}}""";
        string Applied(string run, string digest) => $$"""{"run":"{{run}}","digest":"{{digest}}","verifiedAt":"2026-10-31T10:00:05Z"}""";
        switch (row)
        {
            case "snapshot-not-json": File.WriteAllText(kit.Layout.LockedModes, "not json"); break;
            case "applied-alone": File.WriteAllText(kit.Layout.LockedModesApplied, Applied("part6", "d1")); break;
            case "two-runs":
                File.WriteAllText(kit.Layout.LockedModes, Snapshot("part6", "d1"));
                File.WriteAllText(kit.Layout.LockedModesApplied, Applied("part7", "d1"));
                break;
            case "two-digests":
                File.WriteAllText(kit.Layout.LockedModes, Snapshot("part6", "d1"));
                File.WriteAllText(kit.Layout.LockedModesApplied, Applied("part6", "d2"));
                break;
            case "snapshot-not-utf8": File.WriteAllBytes(kit.Layout.LockedModes, [0xFF, 0xFE, .. System.Text.Encoding.ASCII.GetBytes("{}")]); break;
            default:
                File.WriteAllText(Path.Combine(kit.Root.Temp, "real-snapshot"), Snapshot("part6", "d1"));
                File.CreateSymbolicLink(kit.Layout.LockedModes, Path.Combine(kit.Root.Temp, "real-snapshot"));
                break;
        }

        File.SetUnixFileMode(kit.Pids, (UnixFileMode)0xC0);
        var before = FilesBytes(kit);
        kit.Events.Clear();

        var lockRun = await kit.LockAsync();

        lockRun.Code.Should().Be(4, Ctx(lockRun));
        lockRun.Error.Should().Contain("inconsistent");
        kit.Events.Should().BeEmpty();
        File.GetUnixFileMode(kit.Pids).Should().Be((UnixFileMode)0xC0);
        FilesBytes(kit).Should().Equal(before);

        var unlockRun = await kit.UnlockAsync();

        var restores = row is "two-runs" or "two-digests";
        var alone = row == "applied-alone";
        unlockRun.Code.Should().Be(restores || alone ? 0 : 4, Ctx(unlockRun));
        (restores ? File.GetUnixFileMode(kit.Pids) == IsolationKit.Mode755 : !alone || kit.Events.All(each => !each.StartsWith("restore ", StringComparison.Ordinal))).Should().BeTrue("the parsing snapshot's modes are restored; an applied file alone restores nothing");
        (alone ? unlockRun.Output.Contains("nothing to restore — .locked-modes.applied stood alone, moved aside", StringComparison.Ordinal) : restores || unlockRun.Error.Contains(".locked-modes", StringComparison.Ordinal)).Should().BeTrue(Ctx(unlockRun));
        (restores || alone ? kit.Files().Count >= 1 && !File.Exists(kit.Layout.LockedModes) : FilesBytes(kit).SequenceEqual(before)).Should().BeTrue("moved aside when usable, both kept when not");
    }

    private static IReadOnlyList<byte> FilesBytes(IsolationKit kit) =>
        [.. new[] { kit.Layout.LockedModes, kit.Layout.LockedModesApplied }.SelectMany(path => new FileInfo(path).LinkTarget is not null ? [(byte)'L'] : File.Exists(path) ? File.ReadAllBytes(path) : [(byte)0])];

    [Fact]
    public async Task The_writers_files_are_the_shapes_the_built_reader_parses()
    {
        using var kit = Kit();
        (await kit.LockAsync()).Code.Should().Be(0);
        var at = IsolationKit.Instant.ToString(SnapshotFiles.Utc, CultureInfo.InvariantCulture);

        IsolationFiles.State(kit.Layout).Should().Be(new LockState.Applied("part6", at, kit.Plan().Digest));

        var output = new StringWriter();
        var code = await ModelArmsApp.Program.RunAsync(["status", "--experiment", kit.Root.Root], output, new StringWriter(), kit.Root.Environment, TestContext.Current.CancellationToken);
        code.Should().Be(0);
        output.ToString().Should().Contain($"isolation: isolation applied for part6 since {at}");
        DateTimeOffset.TryParseExact(at, SnapshotFiles.Utc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("pids-link")]
    [InlineData("pids-file")]
    [InlineData("through-a-link")]
    public async Task An_entry_that_is_absent_a_link_a_file_where_a_folder_is_needed_or_reached_through_a_link_is_refused_before_anything_is_written(string row)
    {
        using var kit = Kit();
        var elsewhere = kit.Root.Scratch("elsewhere");
        switch (row)
        {
            case "absent": Directory.Delete(kit.OldExperiment); break;
            case "pids-link":
                Directory.Delete(kit.Pids);
                Directory.CreateSymbolicLink(kit.Pids, elsewhere);
                break;
            case "pids-file":
                Directory.Delete(kit.Pids);
                File.WriteAllText(kit.Pids, "a file");
                break;
            default:
                var claude = Path.Combine(kit.Home, ".claude");
                var real = Path.Combine(kit.Root.Temp, "real-claude");
                Directory.Move(claude, real);
                Directory.CreateSymbolicLink(claude, real);
                break;
        }

        var run = await kit.LockAsync();

        run.Code.Should().Be(4, Ctx(run));
        run.Error.Should().Contain(row switch
        {
            "absent" => $"{kit.OldExperiment} does not exist — named by isolation.closed[0]",
            "pids-link" => $"{kit.Pids} is a symbolic link",
            "pids-file" => $"{kit.Pids} is a regular file, not a directory",
            _ => "reached through a link",
        });
        File.Exists(kit.Layout.LockedModes).Should().BeFalse("a refusal comes before anything is written");
        File.GetUnixFileMode(kit.Root.Root).Should().Be((UnixFileMode)0x1C0);
        Directory.EnumerateFileSystemEntries(elsewhere).Should().BeEmpty("nothing was made through a link");
    }

    [Fact]
    public async Task A_closed_regular_file_is_locked_to_000_and_an_absent_pids_folder_is_made_by_name()
    {
        using var kit = Kit();
        Directory.Delete(kit.OldExperiment);
        File.WriteAllText(kit.OldExperiment, "an old experiment's file");
        Directory.Delete(kit.Pids);

        var run = await kit.LockAsync();

        run.Code.Should().Be(0, Ctx(run));
        File.GetUnixFileMode(kit.OldExperiment).Should().Be(UnixFileMode.None, "closed may be a file, and the file is 000 after");
        File.GetUnixFileMode(kit.Pids).Should().Be((UnixFileMode)0xC0);
        kit.SavedOf().Modes.Single(mode => mode.Path == kit.Pids).Mode.Should().Be("700", "the folder was made by name at 0700 and locked from there (finding 6)");
    }

    [Fact]
    public async Task A_link_planted_after_the_check_never_has_its_target_closed()
    {
        using var kit = Kit();
        var elsewhere = kit.Root.Scratch("elsewhere");
        File.SetUnixFileMode(elsewhere, IsolationKit.Mode755);
        var moved = Path.Combine(kit.Root.Temp, "pids-moved");
        UnixFileMode? elsewhereWhenPidsWasLocked = null;
        UnixFileMode? movedWhenPidsWasLocked = null;
        var hands = new IsolationHands(
            null,
            null,
            (index, _) =>
            {
                if (index == 3)
                {
                    elsewhereWhenPidsWasLocked = File.GetUnixFileMode(elsewhere);
                    movedWhenPidsWasLocked = File.GetUnixFileMode(moved);
                }
            },
            () =>
            {
                Directory.Move(kit.Pids, moved);
                Directory.CreateSymbolicLink(kit.Pids, elsewhere);
            });

        var run = await kit.LockAsync(hands: hands);

        run.Code.Should().Be(3, Ctx(run));
        movedWhenPidsWasLocked.Should().Be((UnixFileMode)0xC0, "the pinned folder — now at pids-moved — is the one that was locked");
        elsewhereWhenPidsWasLocked.Should().Be(IsolationKit.Mode755, "the modes went through the pinned descriptors: a link planted at the leaf changes nothing it points at, not even while the lock stands");
        File.GetUnixFileMode(elsewhere).Should().Be(IsolationKit.Mode755, "nor does the roll-back");
        File.GetUnixFileMode(moved).Should().Be(IsolationKit.Mode755, "the roll-back restored the pinned folder through its held descriptor");
        run.Error.Should().Contain("could not be verified").And.Contain(kit.Pids);
    }

    [Fact]
    public async Task Unlock_skips_an_entry_gone_or_replaced_since_the_lock_and_says_so()
    {
        using var kit = Kit();
        (await kit.LockAsync()).Code.Should().Be(0);
        Directory.Delete(kit.OldExperiment);
        Directory.Delete(kit.Pids);
        Directory.CreateDirectory(kit.Pids);
        File.SetUnixFileMode(kit.Pids, IsolationKit.Mode755);

        var run = await kit.UnlockAsync();

        run.Code.Should().Be(0, Ctx(run));
        run.Output.Should().Contain("unlocked — restored 6 of 8 (skipped: ")
            .And.Contain($"{kit.OldExperiment}: not found by this name — if it was renamed, it may still stand at 0 elsewhere; the record is kept in .locked-modes.restored-<utc>")
            .And.Contain($"{kit.Pids}: replaced since the lock (inode ")
            .And.Contain(") — its mode is left as it is");
        File.GetUnixFileMode(kit.Pids).Should().Be(IsolationKit.Mode755);
        kit.Files().Should().Equal(".locked-modes.restored-" + IsolationKit.Stamp, ".locked-modes.applied.restored-" + IsolationKit.Stamp);
        File.ReadAllText(kit.Layout.LockedModesRestored(IsolationKit.Stamp)).Should().Contain(kit.OldExperiment);
    }

    [Fact]
    public async Task An_operators_own_mode_change_survives_unlock_and_is_reported()
    {
        using var kit = Kit();
        (await kit.LockAsync()).Code.Should().Be(0);
        var r = Path.Combine(kit.Root.Root, "r");
        File.SetUnixFileMode(r, (UnixFileMode)0x1C0);
        File.SetUnixFileMode(kit.Pids, IsolationKit.Mode755);
        kit.Events.Clear();

        var run = await kit.UnlockAsync();

        run.Code.Should().Be(0, Ctx(run));
        run.Output.Should().Contain($"unlocked — restored 7 of 8 (unresolved: {r}: mode 700 is neither the saved 755 nor the locked 300 — changed by hand since the lock; left as it is)");
        File.GetUnixFileMode(r).Should().Be((UnixFileMode)0x1C0, "the operator's own mode is left as it is");
        kit.Events.Should().NotContain("restore " + kit.Pids, "an entry already at its saved mode is counted without a call");
        kit.Files().Should().Equal(".locked-modes.restored-" + IsolationKit.Stamp, ".locked-modes.applied.restored-" + IsolationKit.Stamp);
    }

    [Fact]
    public async Task A_restore_that_fails_half_way_keeps_the_snapshot_moves_the_applied_file_aside_and_exits_3()
    {
        using var kit = Kit();
        (await kit.LockAsync()).Code.Should().Be(0);
        var r = Path.Combine(kit.Root.Root, "r");

        var failed = await kit.UnlockAsync(hands: new IsolationHands((_, path, mode) => path == r ? 1 : Chmod(path, mode), null, null, null));

        failed.Code.Should().Be(3, Ctx(failed));
        failed.Error.Should().Contain($"restored 7 of 8 — {r}: Operation not permitted — the applied file was moved aside; run unlock again, or lock part6");
        File.Exists(kit.Layout.LockedModes).Should().BeTrue();
        kit.Files().Should().Equal(".locked-modes", ".locked-modes.applied.restored-" + IsolationKit.Stamp);
        kit.Snapshot().Should().BeOfType<LockState.Applying>();
        (await kit.UnlockAsync()).Code.Should().Be(0);
        kit.Modes().Should().Equal(Original);

        // the same through lock's own recovery of an interrupted lock
        var interrupted = async () => await kit.LockAsync(hands: Crash(after: 1));
        await interrupted.Should().ThrowAsync<TestCrash>();
        var old = File.ReadAllBytes(kit.Layout.LockedModes);
        var recovery = await kit.LockAsync(hands: new IsolationHands((_, path, mode) => path == kit.OldExperiment ? 1 : Chmod(path, mode), null, null, null));

        recovery.Code.Should().Be(3, Ctx(recovery));
        recovery.Error.Should().Contain("restored 7 of 8").And.Contain($"{kit.OldExperiment}: Operation not permitted").And.Contain("the snapshot is kept");
        File.ReadAllBytes(kit.Layout.LockedModes).Should().Equal(old, "no fresh snapshot is ever written over a failed restore");
        kit.Snapshot().Should().BeOfType<LockState.Applying>();
        var retry = await kit.LockAsync();
        retry.Code.Should().Be(0, Ctx(retry));
        retry.Output.Should().Contain("restored 8 of 8, now locking afresh");
    }

    [Fact]
    public async Task Two_cycles_in_one_second_keep_both_records()
    {
        using var kit = Kit();
        for (var cycle = 0; cycle < 2; cycle++)
        {
            (await kit.LockAsync()).Code.Should().Be(0);
            (await kit.UnlockAsync()).Code.Should().Be(0);
        }

        kit.Files().Should().Equal(
            ".locked-modes.restored-" + IsolationKit.Stamp,
            ".locked-modes.applied.restored-" + IsolationKit.Stamp,
            ".locked-modes.restored-" + IsolationKit.Stamp + "-2",
            ".locked-modes.applied.restored-" + IsolationKit.Stamp + "-2");
    }

    [Fact]
    public async Task Running_as_root_is_refused_before_any_mode()
    {
        using var kit = Kit();

        var run = await kit.LockAsync(hands: new IsolationHands(null, () => true, null, null));

        run.Code.Should().Be(3, Ctx(run));
        run.Error.Should().Contain("running as root (uid 0)");
        kit.Events.Should().BeEmpty();
        File.Exists(kit.Layout.LockedModes).Should().BeFalse("a euid check after the snapshot would leave one");
        kit.Modes().Should().Equal(Original);
    }
}
