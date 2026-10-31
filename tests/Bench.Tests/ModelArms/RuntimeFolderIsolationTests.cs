using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Isolation;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary><c>lock</c> and <c>unlock</c> on every POSIX road that is NOT the kernel road (macOS, FreeBSD, Linux arm64, no <c>statx</c>; E5.S1 question 19, ledger 2.1): the runtime's facts, modes set by path, ONE line saying what is not verified. The road is handed through <see cref="IsolationSeams"/> (<c>PlatformKit.MacRoad</c>), which selects
/// <c>RuntimeFolderIsolation</c> on THIS host — so the composition is observed here and the Mac itself stays unobserved (parent §9). Linux only, any architecture.
/// <para>
/// What this does NOT prove: that a Mac's file system answers as this one does (a FIFO or a device reading as a file; the <c>EACCES</c> of a child of a 000 folder); a link planted between the check and the chmod (followed — the line says so); a replaced entry at <c>unlock</c> (not told apart — the line says so).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class RuntimeFolderIsolationTests
{
    private static readonly string[] Original = ["755", "700", "755", "755", "755", "755", "755", "755"];
    private static readonly string[] Locked = ["0", "300", "300", "300", "300", "300", "300", "300"];

    private static string Line => $"isolation on {PlatformKit.MacRoad.Platform}: modes are set by path — a link planted between the check and the chmod is followed; a replaced entry is not told apart at unlock; a FIFO or a device reads as a file (ledger 2.1)";

    [Fact]
    public async Task On_a_road_without_the_kernel_reader_lock_and_unlock_run_by_path_and_say_what_is_not_verified()
    {
        using var kit = new IsolationKit();

        var locked = await kit.LockAsync(road: PlatformKit.MacRoad);

        locked.Code.Should().Be(0, locked.Output + locked.Error);
        locked.Output.Split('\n')[0].Should().Contain(Line);
        locked.Output.Should().Contain("locked — closed 1, unlisted 7").And.Contain("verified at");
        kit.Modes().Should().Equal(Locked);
        var saved = kit.SavedOf();
        saved.Modes.Should().HaveCount(8).And.OnlyContain(mode => mode.Identity == null, "the runtime road records no device and no inode");
        File.ReadAllText(kit.Layout.LockedModes).Should().NotContain("\"dev\"");
        var listing = () => Directory.EnumerateFileSystemEntries(kit.Pids).ToList();
        listing.Should().Throw<UnauthorizedAccessException>();
        IsolationFiles.State(kit.Layout).Should().BeOfType<LockState.Applied>("the kernel road's reader reads both files: one reader, two writers");

        var unlocked = await kit.UnlockAsync(road: PlatformKit.MacRoad);

        unlocked.Code.Should().Be(0, unlocked.Output + unlocked.Error);
        System.Text.RegularExpressions.Regex.Matches(unlocked.Output, "modes are set by path").Should().ContainSingle("the line is printed once");
        kit.Modes().Should().Equal(Original);
    }

    [Fact]
    public async Task The_runtime_road_verifies_every_entry_and_rolls_back_on_a_listable_one()
    {
        using var kit = new IsolationKit();
        var honoursAllButPids = new IsolationHands((_, path, mode) =>
        {
            if (path != kit.Pids)
            {
                File.SetUnixFileMode(path, (UnixFileMode)mode);
            }

            return 0;
        }, null, null, null);

        var run = await kit.LockAsync(hands: honoursAllButPids, road: PlatformKit.MacRoad);

        run.Code.Should().Be(3, run.Output + run.Error);
        run.Error.Should().Contain($"{kit.Pids} is still listable by this user");
        System.Text.RegularExpressions.Regex.Matches(run.Error, "is still listable").Should().ContainSingle();
        kit.Modes().Should().Equal(Original);
        kit.Snapshot().Should().BeOfType<LockState.None>();
    }

    [Theory]
    [InlineData("pids-link", 4, "is a symbolic link")]
    [InlineData("pids-file", 4, "is a regular file, not a directory")]
    [InlineData("old-absent", 4, "does not exist")]
    [InlineData("root", 3, "running as root (uid 0)")]
    public async Task The_runtime_road_refuses_a_link_a_wrong_kind_and_root_before_anything_is_written(string row, int code, string sentence)
    {
        using var kit = new IsolationKit();
        var elsewhere = kit.Root.Scratch("elsewhere");
        switch (row)
        {
            case "pids-link":
                Directory.Delete(kit.Pids);
                Directory.CreateSymbolicLink(kit.Pids, elsewhere);
                break;
            case "pids-file":
                Directory.Delete(kit.Pids);
                File.WriteAllText(kit.Pids, "a file");
                break;
            case "old-absent":
                Directory.Delete(kit.OldExperiment);
                break;
        }

        var run = await kit.LockAsync(hands: row == "root" ? new IsolationHands(null, () => true, null, null) : null, road: PlatformKit.MacRoad);

        run.Code.Should().Be(code, run.Output + run.Error);
        run.Error.Should().Contain(sentence);
        File.Exists(kit.Layout.LockedModes).Should().BeFalse("nothing is written before a refusal");
    }

    [Fact]
    public async Task The_runtime_road_skips_a_link_and_reports_an_operators_mode_at_unlock()
    {
        using var kit = new IsolationKit();
        var elsewhere = kit.Root.Scratch("elsewhere");
        File.SetUnixFileMode(elsewhere, IsolationKit.Mode755);
        (await kit.LockAsync(road: PlatformKit.MacRoad)).Code.Should().Be(0);
        Directory.Delete(kit.Pids);
        Directory.CreateSymbolicLink(kit.Pids, elsewhere);
        var r = Path.Combine(kit.Root.Root, "r");
        File.SetUnixFileMode(r, (UnixFileMode)0x1C0);
        Directory.Delete(kit.OldExperiment);
        Directory.CreateDirectory(kit.OldExperiment);
        File.SetUnixFileMode(kit.OldExperiment, IsolationKit.Mode755);

        var run = await kit.UnlockAsync(road: PlatformKit.MacRoad);

        run.Code.Should().Be(0, run.Output + run.Error);
        run.Output.Should().Contain($"{kit.Pids}: a link now — not restored").And.Contain($"{r}: mode 700 is neither the saved 755 nor the locked 300 — changed by hand since the lock; left as it is");
        File.GetUnixFileMode(elsewhere).Should().Be(IsolationKit.Mode755, "a link is never followed by a restore");
        File.GetUnixFileMode(r).Should().Be((UnixFileMode)0x1C0);
        File.GetUnixFileMode(kit.OldExperiment).Should().Be(IsolationKit.Mode755, "a replaced entry is not told apart on this road: restored to 755 as a harmless retry (the printed line said so)");
    }
}
