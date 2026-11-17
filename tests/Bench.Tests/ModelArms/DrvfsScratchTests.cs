using Bench.Tests.Leftovers;
using FluentAssertions;
using ModelArms.Adapters;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The check of the folder the 9p tests may use (<see cref="DrvfsScratch"/>), proved off the mount: a folder BELOW a 9p mount point that holds nothing but this repository's owner-form entries and their claims is accepted, and anything else is refused naming why: a foreign entry, a quarantine sibling, another owner or repository, a mount that is not 9p, the string-prefix trap, a relative or missing path, a link that points outside the mount, a mount point written with escapes.
/// <para>
/// What this does NOT prove: the 9p mount itself (the one real run of the 9p tests does, by the owner's word).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class DrvfsScratchTests
{
    private static string Real(string path) => RootFacts.RealPathOf(path).Ok();

    private static IEnumerable<string> Mounts(params (string Point, string Type)[] mounts) =>
        mounts.Select(mount => $"source {mount.Point.Replace("\\", "\\134", StringComparison.Ordinal).Replace(" ", "\\040", StringComparison.Ordinal).Replace("\t", "\\011", StringComparison.Ordinal)} {mount.Type} rw 0 0");

    [Fact]
    public void The_drvfs_check_accepts_an_empty_folder_below_a_9p_mount_point_and_a_folder_holding_what_a_killed_run_leaves()
    {
        using var mount = new TempFolder();
        var empty = mount.Folder("scratch");
        var left = mount.Folder("left");
        using var owned = OwnedDir.Create("drvfs", left);
        using var spaced = new TempFolder();
        var below = spaced.Folder("a b", "scratch");

        DrvfsScratch.Check(empty, Mounts(("/", "ext4"), (mount.Path, "9p")), RootFacts.RealPathOf).Ok().Should().Be(Real(empty));
        DrvfsScratch.Check(left, Mounts(("/", "ext4"), (mount.Path, "9p")), RootFacts.RealPathOf).Ok().Should().Be(Real(left), "an owner-form folder and its claim are what a killed run leaves");
        DrvfsScratch.Check(below, Mounts(("/", "ext4"), (spaced.Combine("a b"), "9p")), RootFacts.RealPathOf).Ok().Should().Be(Real(below), "a space in a mount point is written \\040 in /proc/mounts");
    }

    [Fact]
    public void The_drvfs_check_refuses_a_foreign_entry_a_quarantine_sibling_and_another_owners_or_repositorys_name_naming_each()
    {
        var ours = Owned.NameFor("drvfs");
        foreach (var name in new[] { "notes.txt", "." + ours + ".teststo" + "delete-quarantine-12-ab", ours.Replace(OwnerToken.This, "u4242", StringComparison.Ordinal), ours.Replace("-bench-", "-mcp-", StringComparison.Ordinal) })
        {
            using var folder = new TempFolder();
            var scratch = folder.Folder("scratch");
            File.WriteAllText(Path.Combine(scratch, name), "x");

            DrvfsScratch.Check(scratch, Mounts(("/", "ext4"), (folder.Path, "9p")), RootFacts.RealPathOf).Reason().Should().Contain(name, $"{name} is not an owner-form entry of this repository");
        }
    }

    [Fact]
    public void The_drvfs_check_refuses_a_mount_that_is_not_9p_the_string_prefix_trap_and_a_relative_or_missing_path()
    {
        using var mount = new TempFolder();
        var trap = mount.Folder("cx", "scratch");
        var nested = mount.Folder("c", "x", "scratch");
        mount.Folder("c");

        DrvfsScratch.Check(nested, Mounts(("/", "ext4"), (mount.Path, "9p"), (mount.Combine("c", "x"), "ext4")), RootFacts.RealPathOf).Reason().Should().Contain("ext4", "the LONGEST mount point above decides");
        DrvfsScratch.Check(trap, Mounts(("/", "ext4"), (mount.Combine("c"), "9p")), RootFacts.RealPathOf).Reason().Should().Contain("ext4", "cx is not below c: the prefix is compared by segment");
        DrvfsScratch.Check("scratch", Mounts(("/", "9p")), RootFacts.RealPathOf).Reason().Should().Contain(DrvfsScratch.Variable);
        DrvfsScratch.Check(mount.Combine("not-made"), Mounts(("/", "9p")), RootFacts.RealPathOf).Reason().Should().Contain("not-made");
    }

    [Fact]
    public void The_drvfs_check_judges_a_link_below_the_mount_where_it_points_not_by_its_spelling()
    {
        using var mount = new TempFolder();
        using var outside = new TempFolder();
        mount.RequireLinks();
        var real = mount.Folder("real");
        var target = outside.Folder("outside");
        var link = mount.Combine("link");
        Directory.CreateSymbolicLink(link, target);
        var lines = Mounts(("/", "ext4"), (mount.Path, "9p"));

        DrvfsScratch.Check(real, lines, RootFacts.RealPathOf).Ok().Should().Be(Real(real));
        DrvfsScratch.Check(link, lines, RootFacts.RealPathOf).Reason().Should().Contain(Real(target), "the link points outside the 9p mount: the real path is judged, and named").And.Contain("ext4");
    }

    [Fact]
    public void The_drvfs_check_decodes_a_backslash_and_a_tab_in_a_mount_point_as_proc_mounts_writes_them()
    {
        using var mount = new TempFolder();
        var backslash = mount.Folder("a\\b", "scratch");
        var tab = mount.Folder("a\tb", "scratch");

        DrvfsScratch.Check(backslash, Mounts(("/", "ext4"), (mount.Combine("a\\b"), "9p")), RootFacts.RealPathOf).Ok().Should().Be(Real(backslash), "a backslash is written \\134");
        DrvfsScratch.Check(tab, Mounts(("/", "ext4"), (mount.Combine("a\tb"), "9p")), RootFacts.RealPathOf).Ok().Should().Be(Real(tab), "a tab is written \\011");
    }

    [Fact]
    public void A_folder_the_child_reports_is_accepted_only_when_it_lies_directly_in_the_scratch_folder()
    {
        DrvfsScratch.IsDirectChild("/mnt/c/scratch", "/mnt/c/scratch/owned-folder").Should().BeTrue();
        DrvfsScratch.IsDirectChild("/mnt/c/scratch/", "/mnt/c/scratch/owned-folder").Should().BeTrue("a trailing separator on the folder changes nothing");
        DrvfsScratch.IsDirectChild("/mnt/c/scratch", "/mnt/c/scratch/owned-folder/deeper").Should().BeFalse("not below a folder of its own");
        DrvfsScratch.IsDirectChild("/mnt/c/scratch", "/mnt/c/scratchx/owned-folder").Should().BeFalse("a sibling with the same prefix");
        DrvfsScratch.IsDirectChild("/mnt/c/scratch", "/home/jinx/owned-folder").Should().BeFalse("another place altogether");
    }
}
