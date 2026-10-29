using System.Globalization;
using Bench.Infrastructure.Process;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The facts of D17's trust check come from the kernel — <c>statx(2)</c> and <c>geteuid(2)</c> through libc, no process, no text, no locale — because a
/// program started by name is looked up in the current directory before PATH (measured: an arm's <c>stat</c> and <c>id</c> in the target answered the check that exists
/// to keep arms' files from running). The tools are used here only as an independent witness: they are not the subject.
/// <para>
/// What this does NOT prove: any file system but the ones this suite ran on (ext4 and the scratch tmpfs here; a file system that omits a basic field is refused, not
/// observed), a libc older than glibc 2.28 or musl 1.1.24 (no <c>statx</c>: refused, not observed), macOS or FreeBSD (refused as unprobed; the platform test skips
/// here), or Windows (the trust check is declared not performed).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class KernelFactsTests
{
    private const uint Basic = 0x7ff;
    private static readonly StatxAnswer File644 = new(Basic, 1000, 0x81a4, 131077, 1761768000, 123456789, 8, 48);

    private static void Linux() => Assert.SkipUnless(OperatingSystem.IsLinux(), "statx and /proc are Linux");

    private static async Task<string[]> StatToolAsync(string path)
    {
        var attempt = await ProcessRunner.RunAsync("stat", ["-c", "%u %f %Hd %Ld %i %.9Z", path], Path.GetTempPath(), TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var done = attempt.Should().BeOfType<ProcessAttempt.Completed>().Subject.Result;
        done.ExitCode.Should().Be(0, "the witness answers: " + done.Output);
        return done.StandardOutput.Trim().Split(' ');
    }

    [Fact]
    public async Task A_files_facts_come_from_the_kernel_and_equal_the_stat_tools()
    {
        Linux();
        using var temp = new TempFolder();
        var file = temp.File("{}", "f.json");
        File.SetUnixFileMode(file, (UnixFileMode)Convert.ToInt32("640", 8));
        var folder = temp.Folder("sub");
        File.SetUnixFileMode(folder, (UnixFileMode)Convert.ToInt32("755", 8));
        var dangling = temp.Combine("dangling");
        File.CreateSymbolicLink(dangling, temp.Combine("nowhere"));

        foreach (var (path, kind, mode) in new[] { (file, FileKind.Regular, "640"), (folder, FileKind.Directory, "755"), (dangling, FileKind.Link, "777") })
        {
            var read = KernelFacts.Read(path).Should().BeOfType<FactsRead.Read>(path).Subject.Facts;
            var tool = await StatToolAsync(path);

            read.OwnerUid.Should().Be(KernelFacts.EffectiveUid(), path);
            read.Identity.Kind.Should().Be(kind, path);
            ((int)read.Mode).Should().Be(Convert.ToInt32(mode, 8), path);
            ((int)read.Mode).Should().Be(Convert.ToInt32(tool[1], 16) & 0xFFF, $"{path}: the permission bits equal the tool's");
            read.OwnerUid.Should().Be(int.Parse(tool[0], CultureInfo.InvariantCulture), path);
            read.Identity.Device.Should().Be((ulong.Parse(tool[2], CultureInfo.InvariantCulture) << 32) | ulong.Parse(tool[3], CultureInfo.InvariantCulture), path);
            read.Identity.Inode.Should().Be(ulong.Parse(tool[4], CultureInfo.InvariantCulture), path);
            var toolCtime = decimal.Parse(tool[5], CultureInfo.InvariantCulture);
            decimal.Floor(read.Identity.Ctime).Should().Be(decimal.Floor(toolCtime), $"{path}: the whole seconds of the change time");
            Math.Abs(read.Identity.Ctime - toolCtime).Should().BeLessThan(0.000001m, $"{path}: the kernel's nanoseconds, within the tool's microsecond rounding");
        }
    }

    [Fact]
    public void Present_is_true_for_a_dangling_link_false_for_nothing_and_a_file_used_as_a_folder()
    {
        Linux();
        using var temp = new TempFolder();
        var file = temp.File("x", "f");
        var dangling = temp.Combine("dangling");
        File.CreateSymbolicLink(dangling, temp.Combine("nowhere"));

        KernelFacts.Present(dangling).Ok().Should().BeTrue("a dangling link is a directory entry");
        KernelFacts.Present(file).Ok().Should().BeTrue();
        KernelFacts.Present(temp.Combine("absent")).Ok().Should().BeFalse("ENOENT: nothing by that name");
        KernelFacts.Present(file + "/x").Ok().Should().BeFalse("ENOTDIR: a file used as a folder has no entries");
    }

    [Fact]
    public void Present_cannot_say_for_a_folder_the_user_may_not_search_and_names_the_operating_systems_words()
    {
        Linux();
        using var temp = new TempFolder();
        var closed = temp.Folder("closed");
        temp.File("x", "closed", "x");
        File.SetUnixFileMode(closed, UnixFileMode.None);
        Assert.SkipWhen(KernelFacts.EffectiveUid() == 0, "root searches a mode 000 folder");

        KernelFacts.Present(Path.Combine(closed, "x")).Reason().Should().Contain("Permission denied");
    }

    [Fact]
    public void A_file_system_that_does_not_report_a_fact_is_unread_naming_it()
    {
        KernelFacts.Decode("/r/f", File644 with { Mask = Basic & ~0x80u }).Should().BeOfType<FactsRead.Unread>().Which.Reason
            .Should().Contain("ctime").And.NotContain("inode").And.Contain("/r/f");
        KernelFacts.Decode("/r/f", File644 with { Mask = 0 }).Should().BeOfType<FactsRead.Unread>().Which.Reason
            .Should().Contain("type, mode, owner, inode, ctime");
        KernelFacts.Decode("/r/f", File644).Should().BeOfType<FactsRead.Read>("a file system that reports every basic field is read");
    }

    [Fact]
    public void The_effective_uid_is_the_one_in_proc_self_status()
    {
        Linux();
        var status = File.ReadLines("/proc/self/status").First(line => line.StartsWith("Uid:", StringComparison.Ordinal)).Split('\t', StringSplitOptions.RemoveEmptyEntries);

        KernelFacts.EffectiveUid().Should().Be(int.Parse(status[2], CultureInfo.InvariantCulture), "the second field of Uid: is the effective uid");
    }

    [Fact]
    public void The_device_the_inode_the_owner_and_the_change_time_come_out_of_the_seven_fields()
    {
        var facts = KernelFacts.Decode("/r/f", File644).Should().BeOfType<FactsRead.Read>().Subject.Facts;

        facts.Path.Should().Be("/r/f");
        facts.OwnerUid.Should().Be(1000);
        facts.Identity.Should().Be(new FileIdentity((8UL << 32) | 48, 131077, FileKind.Regular, 1761768000.123456789m));
        ((int)facts.Mode).Should().Be(Convert.ToInt32("644", 8));
    }

    [Fact]
    public void Two_reads_that_name_two_inodes_or_two_change_times_are_changes_a_nanosecond_apart()
    {
        var judged = KernelFacts.Decode("/r/f", File644).Should().BeOfType<FactsRead.Read>().Subject.Facts;
        var swapped = KernelFacts.Decode("/r/f", File644 with { Inode = 131078 }).Should().BeOfType<FactsRead.Read>().Subject.Facts;
        var touched = KernelFacts.Decode("/r/f", File644 with { CtimeNsec = 123456790 }).Should().BeOfType<FactsRead.Read>().Subject.Facts;

        RootTrust.Changes(judged, swapped).Should().Equal("inode");
        RootTrust.Changes(judged, touched).Should().Equal("ctime");
        RootTrust.Changes(judged, KernelFacts.Decode("/r/f", File644).Should().BeOfType<FactsRead.Read>().Subject.Facts).Should().BeEmpty();
    }

    [Fact]
    public void The_largest_inode_and_device_are_kept_whole()
    {
        var facts = KernelFacts.Decode("/r/f", File644 with { Inode = ulong.MaxValue, DevMajor = uint.MaxValue, DevMinor = uint.MaxValue }).Should().BeOfType<FactsRead.Read>().Subject.Facts;

        facts.Identity.Inode.Should().Be(ulong.MaxValue);
        facts.Identity.Device.Should().Be(ulong.MaxValue);
    }
}
