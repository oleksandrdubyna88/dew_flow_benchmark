using Bench.Infrastructure.Scouts;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>S5 — the access check's planting on disk. Found red on the first live run (2026-10-13): a work root spelled with forward slashes
/// (what a bash caller passes on Windows) reached <c>cmd /c mklink /J</c> verbatim, and cmd read <c>/Users</c> as a switch — the check refused
/// before any launch. A link is made from either spelling.</summary>
public sealed class ScoutAccessPlantingTests : IDisposable
{
    private readonly TempRoot _root = GateStoreFixtures.NewRoot();

    public void Dispose()
    {
        foreach (var link in new DirectoryInfo(_root.Path).EnumerateDirectories("*", SearchOption.AllDirectories).Where(d => d.LinkTarget is not null).ToList())
        {
            link.Delete();
        }

        _root.Dispose();
    }

    [Fact]
    public async Task A_directory_link_is_made_from_a_forward_slash_path_as_well_as_a_backslash_one()
    {
        var outside = Directory.CreateDirectory(Path.Combine(_root.Path, "outside")).FullName;
        File.WriteAllText(Path.Combine(outside, "canary.txt"), "canary\n");
        var forward = Path.Combine(_root.Path, "tree").Replace('\\', '/') + "/linked";
        Directory.CreateDirectory(Path.Combine(_root.Path, "tree"));

        var linked = await ScoutAccessPlanting.LinkAsync(forward, outside.Replace('\\', '/'), TestContext.Current.CancellationToken);

        linked.Ok().Should().BeTrue();
        File.Exists(Path.Combine(_root.Path, "tree", "linked", "canary.txt")).Should().BeTrue("the link leads to the outside folder");
        new DirectoryInfo(Path.Combine(_root.Path, "tree", "linked")).LinkTarget.Should().NotBeNull("it is a link, not a copy");
    }
}
