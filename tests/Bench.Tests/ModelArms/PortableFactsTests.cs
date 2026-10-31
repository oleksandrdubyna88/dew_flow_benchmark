using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The portable trust reader gives what the runtime itself ships and tests on macOS and FreeBSD — the mode, the kind where the runtime tells it (a link, a folder), the length and the write time — and NEVER an owner, an inode or a change time:
/// those are the clauses the road's printed line names. The Mac's road is handed to <c>RootFacts.ReadFacts</c> on this Linux host.
/// <para>
/// What this does NOT prove: the reader on a real macOS or FreeBSD (the calls it makes are the runtime's, run here on Linux — E3 §8 item 25); that <c>File.GetUnixFileMode</c> reads the file the link judgement saw (a link made between the two is the road's residual); a
/// FIFO or a device (the runtime cannot tell them, by design).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class PortableFactsTests
{
    private static UnixFileMode Mode(string octal) => (UnixFileMode)Convert.ToInt32(octal, 8);

    [Fact]
    public void The_portable_reader_gives_the_mode_the_kind_and_the_witness_and_never_the_owner()
    {
        using var temp = new TempFolder();
        var file = temp.File("twelve bytes", "f.json");
        File.SetUnixFileMode(file, Mode("640"));
        var folder = temp.Folder("sub");
        File.SetUnixFileMode(folder, Mode("755"));

        var read = PortableFacts.Read(file).Should().BeOfType<FactsRead.Read>().Subject.Facts;
        var dir = PortableFacts.Read(folder).Should().BeOfType<FactsRead.Read>().Subject.Facts;

        read.Mode.Should().Be(Mode("640"));
        read.Identity.Kind.Should().Be(FileKind.Unknown, "the runtime cannot tell a regular file from a FIFO or a device");
        read.OwnerUid.Should().BeNull("the owner is not captured, and null is not zero");
        read.Source.Should().Be(FactsSource.Portable);
        read.Witness!.Length.Should().Be(12);
        new DateTime(read.Witness.WriteTicks, DateTimeKind.Utc).Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        dir.Mode.Should().Be(Mode("755"));
        dir.Identity.Kind.Should().Be(FileKind.Directory);
        dir.Witness!.Length.Should().Be(0, "a folder has no length to compare");
    }

    [Fact]
    public void A_link_is_reported_as_a_link_and_nothing_is_read_through_it()
    {
        using var temp = new TempFolder();
        var target = temp.File("{}", "target.json");
        File.SetUnixFileMode(target, Mode("777"));
        var link = temp.Combine("link.json");
        File.CreateSymbolicLink(link, target);

        var read = PortableFacts.Read(link).Should().BeOfType<FactsRead.Read>().Subject.Facts;
        var judged = RootFacts.ReadFacts(link, PlatformKit.MacRoad);

        read.Identity.Kind.Should().Be(FileKind.Link, "a link is a link: its target's mode is not the link's");
        read.Mode.Should().Be(UnixFileMode.None);
        judged.Should().BeOfType<FactsRead.Refused>().Which.Reason.Should().Contain("symbolic link").And.Contain(target);
    }

    [Fact]
    public void An_absent_path_and_a_folder_the_user_may_not_search_are_unread_with_words()
    {
        Assert.SkipWhen(RootFacts.CurrentUid() == 0, "root searches a mode 000 folder");
        using var temp = new TempFolder();
        var closed = temp.Folder("closed");
        temp.File("{}", "closed", "inside.json");
        File.SetUnixFileMode(closed, UnixFileMode.None);

        PortableFacts.Read(temp.Combine("absent")).Should().BeOfType<FactsRead.Unread>().Which.Reason.Should().Contain(temp.Combine("absent"));
        PortableFacts.Read(Path.Combine(closed, "inside.json")).Should().BeOfType<FactsRead.Unread>().Which.Reason.Should().Contain("denied");
    }
}
