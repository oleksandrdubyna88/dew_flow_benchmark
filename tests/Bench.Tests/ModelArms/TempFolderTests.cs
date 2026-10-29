using FluentAssertions;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The scratch folder every real-link and real-mode test uses must always go: a test run removes what it created (common.test-leftovers),
/// including a folder holding a link to its own parent and a folder closed to its owner.
/// <para>
/// What this does NOT prove: the same on Windows, where this shape cannot be made without a privilege.
/// </para></summary>
public sealed class TempFolderTests
{
    [Fact]
    public void Dispose_removes_a_folder_holding_a_link_to_its_own_parent_and_a_folder_closed_to_its_owner()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("links and modes are POSIX; the probe shape is measured on Linux");
            return;
        }

        for (var round = 0; round < 10; round++)
        {
            var temp = new TempFolder();
            var path = temp.Path;
            var probe = temp.Folder("probe");
            Directory.CreateSymbolicLink(System.IO.Path.Combine(probe, "probe-link"), probe);
            temp.File("x", "low", "inside.txt");
            File.SetUnixFileMode(temp.Combine("low"), UnixFileMode.UserRead | UnixFileMode.UserExecute);

            var dispose = temp.Dispose;

            dispose.Should().NotThrow($"round {round}: a link to the folder's own parent must not stop the walk");
            Directory.Exists(path).Should().BeFalse($"round {round}: the folder must be gone");
        }
    }
}
