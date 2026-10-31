using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The one exclusive folder maker (<c>init</c>'s root, <c>prep-base</c>'s results and previous-attempt folders). On a road with libc's <c>mkdir</c> the kernel refuses a taken name; on a road without it (Windows) the maker itself refuses a taken name — a check then a create, best effort.
/// <para>
/// What this does NOT prove: that a folder made between the check and the create is refused on the runtime's road (it is adopted; the doc says so), or anything on a real Windows host — the Windows road is handed in on this host.
/// </para></summary>
public sealed class ExclusiveFolderTests
{
    private static readonly PlatformRoad WindowsRoad = PlatformRoads.Of(new PlatformFacts(true, false, false, false, false, false, "Windows test road"));

    [Fact]
    public void A_name_that_is_taken_is_never_adopted_on_a_road_without_libc_mkdir()
    {
        WindowsRoad.LibcMkdir.Should().BeFalse("the test must take the runtime's road");
        using var temp = new TempFolder();
        var taken = temp.Folder("taken");
        File.WriteAllText(Path.Combine(taken, "mine.txt"), "kept");
        var fresh = temp.Combine("fresh");

        ExclusiveFolder.Make(taken, WindowsRoad).Should().BeOfType<FolderMade.AlreadyThere>("a folder that is there is not the caller's to adopt");
        File.ReadAllText(Path.Combine(taken, "mine.txt")).Should().Be("kept");
        ExclusiveFolder.Make(fresh, WindowsRoad).Should().BeOfType<FolderMade.Made>();
        Directory.Exists(fresh).Should().BeTrue();
        ExclusiveFolder.Make(fresh, WindowsRoad).Should().BeOfType<FolderMade.AlreadyThere>("the second make finds the first");
        ExclusiveFolder.Make(temp.Combine("no-parent", "child"), WindowsRoad).Should().BeOfType<FolderMade.Made>("the runtime creates the parents; the maker does not forbid it");
    }
}
