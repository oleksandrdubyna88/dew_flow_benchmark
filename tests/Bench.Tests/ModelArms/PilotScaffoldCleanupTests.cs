using FluentAssertions;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>A scaffold removes only what IT made. A path of the run (a target, a root or a child) that already exists under the id it drew refuses the run before anything is made, and is left as it was; the root, which a scaffold only names, is never among the paths it removes; and a failed removal of any child keeps the manifest, naming it, so the operator's teardown still knows every path.
/// <para>
/// What this does NOT prove: a failure half-way through a real scaffold run (the catalogue's created list is what the teardown reads; the kill tests stop a scaffold after its first child).
/// </para></summary>
[System.Runtime.Versioning.SupportedOSPlatform("linux")]
public sealed class PilotScaffoldCleanupTests
{
    private const string Id = "aaaa0001";

    [Theory]
    [InlineData("target")]
    [InlineData("root")]
    public async Task The_M2_scaffold_refuses_a_path_that_already_exists_and_leaves_it_as_it_was(string name)
    {
        using var output = new TempFolder();
        var existing = output.Folder($"bench-m2-{name}-{Id}");
        var marker = output.File("not made by the scaffold", $"bench-m2-{name}-{Id}", "mine.txt");

        var run = () => M2PilotScaffold.WriteAsync(output.Path, () => Id);

        (await run.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain(existing);
        File.ReadAllText(marker).Should().Be("not made by the scaffold", "a path the scaffold did not make is never removed");
        Directory.GetFileSystemEntries(output.Path).Should().Equal([existing], "no manifest, no other path");
    }

    [Theory]
    [InlineData("target")]
    [InlineData("root")]
    [InlineData("home")]
    [InlineData("keep")]
    public async Task The_M3_scaffold_refuses_a_path_that_already_exists_and_leaves_it_as_it_was(string name)
    {
        using var output = new TempFolder();
        var existing = output.Folder($"bench-m3-{name}-{Id}");
        var marker = output.File("not made by the scaffold", $"bench-m3-{name}-{Id}", "mine.txt");

        var run = () => M3PilotScaffold.WriteAsync(output.Path, () => Id);

        (await run.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain(existing);
        File.ReadAllText(marker).Should().Be("not made by the scaffold");
        Directory.GetFileSystemEntries(output.Path).Should().Equal([existing]);
    }

    [Fact]
    public async Task The_root_is_named_in_the_manifest_but_is_never_among_the_paths_a_run_removes()
    {
        using var output = new TempFolder();

        var m2 = await PilotManifest.StartAsync(output.Path, "m2", M2PilotScaffold.Catalogue, () => Id);
        var m3 = await PilotManifest.StartAsync(output.Path, "m3", M3PilotScaffold.Catalogue, () => Id);

        m2.CreatedPaths.Should().Equal(m2.PathOf("target"));
        m3.CreatedPaths.Should().NotContain(m3.PathOf("root")).And.HaveCount(M3PilotScaffold.Catalogue.Count - 1);
        File.ReadAllText(m2.Manifest).Should().Contain(m2.PathOf("root"), "the manifest still NAMES the root");
    }

    [Fact]
    public async Task A_child_that_cannot_be_removed_keeps_the_manifest_and_names_it_and_the_manifest_goes_last_otherwise()
    {
        using var output = new TempFolder();
        var run = await PilotManifest.StartAsync(output.Path, "m3", M3PilotScaffold.Catalogue, () => Id);
        var stuck = run.PathOf("home");
        var calls = new List<string>();

        var refused = PilotManifest.CleanUp(run, path =>
        {
            calls.Add(path);
            return path == stuck ? [path] : [];
        });

        refused.Should().Contain(stuck).And.Contain(run.Manifest);
        File.Exists(run.Manifest).Should().BeTrue("a child is left, so the manifest that names it stays");
        calls.Should().NotContain(run.Manifest, "the manifest is not even tried while a child is left");

        calls.Clear();
        PilotManifest.CleanUp(run, path =>
        {
            calls.Add(path);
            return ScaffoldCleanup.Remove(path);
        }).Should().BeNull();
        calls.Last().Should().Be(run.Manifest, "the manifest goes after every child");
        File.Exists(run.Manifest).Should().BeFalse();
    }
}
