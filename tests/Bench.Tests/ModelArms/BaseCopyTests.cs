using System.Diagnostics;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary><c>BaseCopy</c> (E3.S3, C15, D26 a, e): the replacement of the shell's <c>cp -a</c> — a folder, its files with their bytes, modes and modification times (the warm build is incremental on them), and its links AS links: their text kept, never followed, a link to a folder never entered.
/// A FIFO, a socket or a device is refused naming its path. The base is the operator's own checkout, but a copy that followed a link would put whatever the link names inside an arm, and one that opened a FIFO would wait for ever; so the kind of every entry comes from the kernel (<c>statx</c>, no
/// follow) where the host has it, and on the portable road an open under a ceiling.
/// <para>
/// What this does NOT prove: a copy of a tree being written (the base is idle while <c>make</c> runs); the modification time of a LINK (the runtime has no no-follow setter — a link is made new); extended attributes, ACLs and ownership (<c>cp -a</c> kept what the user could; this keeps bytes, mode and time); the portable road
/// on macOS or FreeBSD (it runs here, handed in); a device file (the same refusal as a FIFO, never made by a test).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class BaseCopyTests
{
    private static readonly TimeSpan Wall = TimeSpan.FromSeconds(10);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static void Posix() => Assert.SkipWhen(OperatingSystem.IsWindows(), "links, modes and FIFOs are POSIX");

    [Fact]
    public void Links_are_copied_as_links_and_never_followed()
    {
        Posix();
        using var temp = new TempFolder();
        var outside = temp.File("the operator's file\n", "outside.txt");
        var folder = temp.Folder("outside-folder");
        temp.File("inside\n", "outside-folder", "sentinel.txt");
        temp.Folder("base", "sub");
        File.CreateSymbolicLink(temp.Combine("base", "l"), "../outside.txt");
        File.CreateSymbolicLink(temp.Combine("base", "d"), folder);
        File.CreateSymbolicLink(temp.Combine("base", "sub", "dangling"), "nothing-here");
        temp.File("real\n", "base", "sub", "real.txt");
        var before = MakeKit.Describe(temp.Path, name => name.StartsWith("copy", StringComparison.Ordinal));

        var copied = BaseCopy.Copy(temp.Combine("base"), temp.Combine("copy"));

        copied.Ok().Should().Be(4, "three links and one file are copied");
        new FileInfo(temp.Combine("copy", "l")).LinkTarget.Should().Be("../outside.txt", "a link's text is kept as it is, not resolved");
        new FileInfo(temp.Combine("copy", "d")).LinkTarget.Should().Be(folder);
        new FileInfo(temp.Combine("copy", "sub", "dangling")).LinkTarget.Should().Be("nothing-here");
        File.ReadAllText(temp.Combine("copy", "sub", "real.txt")).Should().Be("real\n");
        File.ReadAllText(outside).Should().Be("the operator's file\n", "the outside file's bytes are as they were");
        MakeKit.Describe(temp.Path, name => name.StartsWith("copy", StringComparison.Ordinal)).Should().Equal(before, "nothing outside the copy changed — the listing of the linked folder included");
        Directory.Exists(temp.Combine("copy", "d")).Should().BeTrue("the link resolves, as the operator's folder");
        new DirectoryInfo(temp.Combine("copy", "d")).LinkTarget.Should().NotBeNull("what stands at d is the LINK: a copy that followed it would have made a folder with the sentinel inside");
    }

    [Theory]
    [InlineData("into-base")]
    [InlineData("the-base-itself")]
    [InlineData("into-the-experiment-root")]
    public void An_absolute_link_that_leads_into_the_base_or_the_experiment_root_is_refused_naming_it(string shape)
    {
        Posix();
        using var temp = new TempFolder();
        temp.File("shared\n", "root", "base", "obj", "shared.txt");
        temp.Folder("root", "r", "other-arm");
        var target = shape switch
        {
            "into-base" => temp.Combine("root", "base", "obj"),
            "the-base-itself" => temp.Combine("root", "base"),
            _ => temp.Combine("root", "r", "other-arm"),
        };
        File.CreateSymbolicLink(temp.Combine("root", "base", "objlink"), target);

        var copied = BaseCopy.Copy(temp.Combine("root", "base"), temp.Combine("copy"), PlatformKit.LinuxRoad, [temp.Combine("root")]);

        copied.Should().BeOfType<Bench.Domain.Outcome<long>.Fail>("an arm that wrote through the link would write into the shared base, which every later make copies and builds").Which.Reason.Should().Contain("objlink");
        File.ReadAllText(temp.Combine("root", "base", "obj", "shared.txt")).Should().Be("shared\n");
    }

    [Fact]
    public void An_absolute_link_to_somewhere_else_and_a_relative_one_are_still_copied_as_links()
    {
        Posix();
        using var temp = new TempFolder();
        temp.File("x", "root", "base", "a.txt");
        File.CreateSymbolicLink(temp.Combine("root", "base", "elsewhere"), temp.Folder("elsewhere"));
        File.CreateSymbolicLink(temp.Combine("root", "base", "relative"), "a.txt");

        var copied = BaseCopy.Copy(temp.Combine("root", "base"), temp.Combine("copy"), PlatformKit.LinuxRoad, [temp.Combine("root")]);

        copied.Ok().Should().Be(3);
        new FileInfo(temp.Combine("copy", "elsewhere")).LinkTarget.Should().Be(temp.Combine("elsewhere"));
    }

    [Fact]
    public void Modes_and_modification_times_are_kept()
    {
        Posix();
        using var temp = new TempFolder();
        var old = new DateTime(2020, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var file = temp.File("#!/bin/sh\n", "base", "tools", "run.sh");
        File.SetUnixFileMode(file, (UnixFileMode)Convert.ToInt32("750", 8));
        File.SetLastWriteTimeUtc(file, old);
        var readOnly = temp.Folder("base", "closed");
        temp.File("inside\n", "base", "closed", "x.txt");
        File.SetUnixFileMode(readOnly, (UnixFileMode)Convert.ToInt32("550", 8));
        File.SetLastWriteTimeUtc(temp.Combine("base", "closed", "x.txt"), old);
        Directory.SetLastWriteTimeUtc(readOnly, old);
        Directory.SetLastWriteTimeUtc(temp.Combine("base", "tools"), old);
        Directory.SetLastWriteTimeUtc(temp.Combine("base"), old);

        var copied = BaseCopy.Copy(temp.Combine("base"), temp.Combine("copy"));

        copied.Ok().Should().Be(2);
        File.GetUnixFileMode(temp.Combine("copy", "tools", "run.sh")).Should().Be((UnixFileMode)Convert.ToInt32("750", 8), "a mode is copied as it is, whatever the umask");
        File.GetLastWriteTimeUtc(temp.Combine("copy", "tools", "run.sh")).Should().Be(old, "the warm build is incremental on modification times");
        File.GetUnixFileMode(temp.Combine("copy", "closed")).Should().Be((UnixFileMode)Convert.ToInt32("550", 8), "a folder that cannot be written is made writable only while it is filled");
        File.ReadAllText(temp.Combine("copy", "closed", "x.txt")).Should().Be("inside\n");
        Directory.GetLastWriteTimeUtc(temp.Combine("copy", "closed")).Should().Be(old, "a folder's time is set after its children are made, or they would move it");
        Directory.GetLastWriteTimeUtc(temp.Combine("copy", "tools")).Should().Be(old);
        Directory.GetLastWriteTimeUtc(temp.Combine("copy")).Should().Be(old);
    }

    [Fact]
    public void Hidden_files_and_folders_are_copied_whole()
    {
        Posix();
        using var temp = new TempFolder();
        temp.File("x", "base", ".dotfile");
        temp.File("HEAD\n", "base", ".git", "HEAD");
        temp.File("y", "base", ".git", "objects", "ab", "cdef");

        var copied = BaseCopy.Copy(temp.Combine("base"), temp.Combine("copy"));

        copied.Ok().Should().Be(3);
        MakeKit.Describe(temp.Combine("copy")).Select(line => line.Split('|')[0]).Should().Equal(".dotfile", ".git", ".git/HEAD", ".git/objects", ".git/objects/ab", ".git/objects/ab/cdef");
    }

    [Fact]
    public async Task A_fifo_in_the_base_is_refused_naming_it()
    {
        Posix();
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the kernel road tells a FIFO by its kind; the portable road has its own test");
        using var temp = new TempFolder();
        temp.File("x", "base", "keep.txt");
        var fifo = MakeKit.Fifo(temp.Combine("base", "run", "p"));
        try
        {
            var running = Task.Run(() => BaseCopy.Copy(temp.Combine("base"), temp.Combine("copy"), PlatformKit.LinuxRoad), Ct);

            (await Task.WhenAny(running, Task.Delay(Wall, Ct))).Should().BeSameAs(running, "a copy that opens a FIFO waits for a writer that never comes");
            var copied = await running;

            copied.Should().BeOfType<Bench.Domain.Outcome<long>.Fail>().Which.Reason.Should().Contain("run/p").And.Contain("FIFO");
        }
        finally
        {
            PlatformKit.ReleaseFifo(fifo);
        }
    }

    [Fact]
    public async Task On_the_portable_road_a_fifo_is_told_by_an_open_that_does_not_return()
    {
        Posix();
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the fixture FIFO's release is Linux's");
        using var temp = new TempFolder();
        temp.File("x", "base", "keep.txt");
        var fifo = MakeKit.Fifo(temp.Combine("base", "p"));
        try
        {
            var watch = Stopwatch.StartNew();
            var running = Task.Run(() => BaseCopy.Copy(temp.Combine("base"), temp.Combine("copy"), PlatformKit.MacRoad, TimeSpan.FromMilliseconds(500)), Ct);

            (await Task.WhenAny(running, Task.Delay(Wall, Ct))).Should().BeSameAs(running, "the ceiling answers where the kind cannot");
            var copied = await running;

            copied.Should().BeOfType<Bench.Domain.Outcome<long>.Fail>().Which.Reason.Should().Contain("p").And.Contain("did not open");
            watch.Elapsed.Should().BeLessThan(Wall);
        }
        finally
        {
            PlatformKit.ReleaseFifo(fifo);
        }
    }

    [Fact]
    public void A_destination_that_exists_is_never_adopted()
    {
        Posix();
        using var temp = new TempFolder();
        temp.File("x", "base", "a.txt");
        temp.File("kept", "copy", "mine.txt");

        var copied = BaseCopy.Copy(temp.Combine("base"), temp.Combine("copy"));

        copied.Should().BeOfType<Bench.Domain.Outcome<long>.Fail>().Which.Reason.Should().Contain("already exists");
        File.ReadAllText(temp.Combine("copy", "mine.txt")).Should().Be("kept");
        File.Exists(temp.Combine("copy", "a.txt")).Should().BeFalse();
    }
}
