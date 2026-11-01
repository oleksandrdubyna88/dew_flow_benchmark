using System.Globalization;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Isolation;
using Xunit;
using static Bench.Tests.ModelArms.IsolationSupport;

namespace Bench.Tests.ModelArms;

/// <summary><c>unlock</c> and the restores around it (E5.S1, D11): an entry restored after the block that named it was removed, a path added to the snapshot by hand refused, the entry gone, replaced or turned into a link skipped and said, the operator's own mode left as it is, a restore that fails keeping the snapshot, a snapshot with no device and inode on the kernel road, the two files'
/// moves reported as they came out, and two cycles in one second. Over <see cref="IsolationKit"/> on the kernel road. Linux only.
/// <para>
/// What this does NOT prove: root; a same-user <c>chmod</c> beside the module (parent §9); the opt-in Windows drive.
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class IsolationRestoreTests
{
    [Fact]
    public async Task Unlock_restores_a_standing_snapshot_after_the_block_was_removed_including_a_folder_outside_the_root_and_home()
    {
        using var kit = Kit();
        (await kit.LockAsync()).Code.Should().Be(0);
        kit.Root.Edit(top => top.AsObject().Remove("isolation"));

        var run = await kit.UnlockAsync();

        run.Code.Should().Be(0, Ctx(run));
        run.Output.Should().Contain("unlocked — restored 8 of 8", "the closure the snapshot recorded (old-experiment, outside the root and ~/.claude) is restored although experiment.json no longer names it");
        run.Error.Should().NotContain("InvalidOperationException");
        kit.Modes().Should().Equal(Original);
        kit.Files().Should().Equal(".locked-modes.restored-" + IsolationKit.Stamp, ".locked-modes.applied.restored-" + IsolationKit.Stamp);
        kit.Snapshot().Should().BeOfType<LockState.None>();
    }

    [Fact]
    public async Task A_path_added_to_the_snapshot_by_hand_is_not_restored_because_the_digest_does_not_cover_it()
    {
        using var kit = Kit();
        var elsewhere = kit.Root.Scratch("not-the-experiments");
        (await kit.LockAsync()).Code.Should().Be(0);
        File.SetUnixFileMode(elsewhere, UnixFileMode.None);
        var forged = File.ReadAllText(kit.Layout.LockedModes).TrimEnd().Replace("]}", $$""",{"path":"{{elsewhere}}","kind":"closed","mode":"755","source":"isolation.closed[9]"}]}""", StringComparison.Ordinal) + "\n";
        File.WriteAllText(kit.Layout.LockedModes, forged);
        kit.Root.Edit(top => top.AsObject().Remove("isolation"));

        var run = await kit.UnlockAsync();

        run.Code.Should().Be(3, Ctx(run));
        run.Error.Should().Contain($"{elsewhere}: not under any folder this experiment names — not restored (by hand: chmod 755 {elsewhere})");
        File.GetUnixFileMode(elsewhere).Should().Be(UnixFileMode.None, "a path the lock did not record is never given a mode");
        kit.Modes().Should().Equal(["0", "700", "755", "755", "755", "755", "755", "755"], "an edited snapshot no longer spells its digest, so none of its recorded sources is believed: the seven entries inside the root and ~/.claude are restored, old-experiment (named only by the removed block) is not");
        File.Exists(kit.Layout.LockedModes).Should().BeTrue("a failed restore keeps the snapshot");
    }

    [Fact]
    public async Task A_forged_entry_reached_through_a_link_is_refused_not_skipped()
    {
        using var kit = Kit();
        var target = kit.Root.Scratch("link-target");
        Directory.CreateDirectory(Path.Combine(target, "inner"));
        var linkFolder = Path.Combine(kit.Root.Temp, "a-link");
        var link = Path.Combine(linkFolder, "inner");
        var targetMode = File.GetUnixFileMode(target);
        Directory.CreateSymbolicLink(linkFolder, target);
        (await kit.LockAsync()).Code.Should().Be(0);
        File.WriteAllText(kit.Layout.LockedModes, File.ReadAllText(kit.Layout.LockedModes).TrimEnd().Replace("]}", $$""",{"path":"{{link}}","kind":"closed","mode":"755","source":"isolation.closed[9]"}]}""", StringComparison.Ordinal) + "\n");

        var run = await kit.UnlockAsync();

        run.Code.Should().Be(3, Ctx(run));
        run.Error.Should().Contain($"{link}: not under any folder this experiment names").And.NotContain("through a link now", "the bound is judged first: a path outside it is refused, whatever it is");
        File.GetUnixFileMode(target).Should().Be(targetMode, "nothing a link points at was touched");
    }

    [Fact]
    public async Task A_snapshot_with_no_device_and_inode_is_refused_on_the_kernel_road_because_a_replaced_entry_could_not_be_told_apart()
    {
        using var kit = Kit();
        (await kit.LockAsync(road: PlatformKit.MacRoad)).Code.Should().Be(0);

        var refused = await kit.UnlockAsync();

        refused.Code.Should().Be(3, Ctx(refused));
        refused.Error.Should().Contain("records no device and inode").And.Contain("not restored (by hand: chmod");
        File.Exists(kit.Layout.LockedModes).Should().BeTrue("the snapshot is kept");
        kit.Modes().Should().Equal(Locked, "nothing was restored without the identity check");

        var retry = await kit.UnlockAsync(road: PlatformKit.MacRoad);

        retry.Code.Should().Be(0, Ctx(retry));
        kit.Modes().Should().Equal(Original);
    }

    [Fact]
    public async Task A_move_aside_that_fails_is_reported_as_failed_not_as_done()
    {
        using var kit = Kit();
        (await kit.LockAsync()).Code.Should().Be(0);
        var root = kit.Root.Root;
        File.SetUnixFileMode(root, UnixFileMode.UserExecute);
        var r = Path.Combine(root, "r");

        var run = await kit.UnlockAsync(hands: new IsolationHands((_, path, mode) => path == r || path == root ? 1 : Chmod(path, mode), null, null, null));

        run.Code.Should().Be(3, Ctx(run));
        run.Error.Should().Contain("the applied file could not be moved aside").And.NotContain("the applied file was moved aside");
        File.Exists(kit.Layout.LockedModesApplied).Should().BeTrue();
        File.Exists(kit.Layout.LockedModes).Should().BeTrue("the snapshot is kept");
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
            .And.Contain($"{kit.OldExperiment}: not found by this name — if it was renamed, it may still stand at 0 elsewhere")
            .And.Contain("the record of what was locked is kept in .locked-modes.restored-" + IsolationKit.Stamp)
            .And.Contain($"{kit.Pids}: replaced since the lock (inode ")
            .And.Contain(") — its mode is left as it is");
        run.Output.Should().NotContain("<utc>", "the real name of the archive is printed, never its pattern");
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
        run.Output.Should().Contain($"unlocked — restored 7 of 8 (1 already at their saved mode) (unresolved: {r}: mode 700 is neither the saved 755 nor the locked 300 — changed by hand since the lock; left as it is)");
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
        retry.Output.Should().Contain("restored 8 of 8 (7 already at their saved mode), now locking afresh");
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
}
