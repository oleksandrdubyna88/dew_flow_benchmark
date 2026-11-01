using FluentAssertions;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The helpers a scaffold's failure path removes its scratch with (<see cref="ScaffoldCleanup"/>): a locked folder is opened before it is listed and each child on its own, a broken link and an absent path are told apart from a file, and what cannot be removed comes back by name.
/// <para>
/// What this does NOT prove: a path owned by another user; a file system that refuses <c>chmod</c>; the scaffold's own failure path end to end (it is an explicit test, not run by the suite).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class ScaffoldCleanupTests
{
    [Fact]
    public void A_locked_folder_inside_a_folder_is_opened_and_removed_with_its_locked_children()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "modes and links are POSIX");
        using var temp = new TempFolder();
        var scratch = temp.Folder("scratch");
        var inner = temp.Folder("scratch", "locked", "deeper");
        temp.File("x", "scratch", "locked", "deeper", "file.txt");
        temp.File("y", "scratch", "locked", "other.txt");
        File.SetUnixFileMode(inner, UnixFileMode.None);
        File.SetUnixFileMode(Path.Combine(scratch, "locked"), UnixFileMode.None);

        var left = ScaffoldCleanup.Remove(scratch);

        left.Should().BeEmpty("a folder at mode 000 and the one inside it are opened by their owner first");
        Directory.Exists(scratch).Should().BeFalse();
    }

    [Fact]
    public void A_broken_link_and_an_absent_path_are_removed_or_nothing_without_being_taken_for_each_other()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "modes and links are POSIX");
        using var temp = new TempFolder();
        var link = temp.Combine("dangling");
        File.CreateSymbolicLink(link, temp.Combine("no-such-target"));

        ScaffoldCleanup.Remove(link).Should().BeEmpty();
        new FileInfo(link).Attributes.Should().Be((FileAttributes)(-1), "the broken link itself is gone");
        ScaffoldCleanup.Remove(temp.Combine("never-made")).Should().BeEmpty("an absent path is nothing to remove");
    }

    [Fact]
    public void A_path_that_cannot_be_removed_comes_back_by_name_and_not_as_an_exception()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows() || System.Environment.IsPrivilegedProcess, "root removes anything; modes are POSIX");
        using var temp = new TempFolder();
        var held = temp.Folder("held");
        var stuck = temp.Folder("held", "stuck");
        temp.File("z", "held", "stuck", "f.txt");
        File.SetUnixFileMode(held, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        var left = ScaffoldCleanup.Remove(stuck);

        left.Should().Contain(stuck, "its parent is read-only, so the folder itself cannot be deleted");
        left.Should().HaveCount(1, "only the folder that could not be deleted is reported, not what was removed inside it");
    }
}
