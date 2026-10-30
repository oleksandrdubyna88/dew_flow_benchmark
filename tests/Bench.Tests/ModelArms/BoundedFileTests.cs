using System.Diagnostics;
using System.Runtime.InteropServices;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>C16: the one road every reader of a model-written file takes — no link followed, no FIFO waited on past a ceiling, never more than a stated limit. The Kernel road (Linux x86-64) opens with <c>O_NOFOLLOW | O_NONBLOCK</c> and judges the descriptor;
/// the Portable road is the runtime's open-and-read on a thread under a ceiling, and says so: a link swapped in between the path's check and the open is followed there (the runtime has no no-follow open), and a FIFO is told by the ceiling, not by its kind.
/// <para>
/// What this does NOT prove: the Portable road on macOS or FreeBSD (it runs here, on Linux, handed in as the road), a device file (refused past the limit or read as its bytes, never told), a thread left behind by the ceiling until its open returns (the tests release the FIFOs they
/// plant), or the swap window the Portable road names as its residual.
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class BoundedFileTests
{
    private static readonly TimeSpan Wall = TimeSpan.FromSeconds(20);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [DllImport("libc", SetLastError = true)]
    private static extern int mkfifo([MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint mode);

    private static void Linux() => Assert.SkipUnless(OperatingSystem.IsLinux() && System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == Architecture.X64, "the Kernel road is Linux x86-64's");

    private static string Fifo(TempFolder temp)
    {
        var path = temp.Combine("fifo");
        mkfifo(path, Convert.ToUInt32("600", 8)).Should().Be(0, "the fixture makes a FIFO");
        return path;
    }

    /// <summary>Frees the thread a Portable read left in its open: a writer arriving makes the open return, and its close ends the read.</summary>
    private static void Release(string fifo) => Task.Run(() => new FileStream(fifo, FileMode.Open, FileAccess.Write).Dispose());

    [Theory]
    [InlineData(BoundedRoad.Kernel)]
    [InlineData(BoundedRoad.Portable)]
    public void A_link_a_fifo_a_folder_and_a_file_past_the_limit_are_refused_and_an_absent_file_is_absent(BoundedRoad road)
    {
        if (road == BoundedRoad.Kernel)
        {
            Linux();
        }

        using var temp = new TempFolder();
        var target = temp.File("0123456789", "target");
        var link = temp.Combine("link");
        File.CreateSymbolicLink(link, target);
        var folder = temp.Folder("folder");
        var fifo = Fifo(temp);
        var big = temp.File(new string('x', 101), "big");

        BoundedFile.Read(link, 100, road).Should().BeOfType<BoundedRead.Refused>().Which.Reason.Should().Contain("link").And.Contain(link);
        BoundedFile.Read(folder, 100, road).Should().BeOfType<BoundedRead.Refused>().Which.Reason.Should().Contain("not a regular file").And.Contain(folder);
        BoundedFile.Read(big, 100, road).Should().BeOfType<BoundedRead.Refused>().Which.Reason.Should().Contain("bigger than 100 bytes");
        BoundedFile.Read(temp.Combine("nothing"), 100, road).Should().BeOfType<BoundedRead.Absent>();
        BoundedFile.Read(target, 100, road).Should().BeOfType<BoundedRead.Bytes>().Which.Value.Should().Equal("0123456789"u8.ToArray(), "a file inside the limit is its bytes — the positive case that makes the refusals above mean something");
        var fifoAnswer = BoundedFile.Read(fifo, 100, road, TimeSpan.FromSeconds(1));
        Release(fifo);
        fifoAnswer.Should().BeOfType<BoundedRead.Refused>().Which.Reason.Should().ContainAny("not a regular file", "was not read within").And.Contain(fifo);
    }

    [Fact]
    public async Task The_portable_road_answers_a_fifo_with_no_writer_within_its_ceiling_instead_of_waiting()
    {
        using var temp = new TempFolder();
        var silent = Fifo(temp);
        var watch = Stopwatch.StartNew();

        var noWriter = Task.Run(() => BoundedFile.Read(silent, 4096, BoundedRoad.Portable), Ct);
        (await Task.WhenAny(noWriter, Task.Delay(Wall, Ct))).Should().BeSameAs(noWriter, "the symptom of a missing ceiling IS the hang: the read waits for a writer that never comes");
        var refused = (await noWriter).Should().BeOfType<BoundedRead.Refused>().Subject;
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(8));
        refused.Reason.Should().Contain("5 s").And.Contain(silent);
        Release(silent);

        var other = temp.Combine("quiet");
        mkfifo(other, Convert.ToUInt32("600", 8)).Should().Be(0);
        var reader = Task.Run(() => BoundedFile.Read(other, 4096, BoundedRoad.Portable), Ct);
        using var writer = await Task.Run(() => new FileStream(other, FileMode.Open, FileAccess.Write), Ct);
        (await Task.WhenAny(reader, Task.Delay(Wall, Ct))).Should().BeSameAs(reader, "a writer that sends nothing holds the read, and the ceiling ends the wait");
        (await reader).Should().BeOfType<BoundedRead.Refused>().Which.Reason.Should().Contain("5 s");

        BoundedFile.Read(temp.File("plain", "plain"), 4096, BoundedRoad.Portable).Should().BeOfType<BoundedRead.Bytes>("a regular file is read at once");
    }

    [Fact]
    public void A_portable_read_follows_no_link_and_reads_at_most_the_limit()
    {
        using var temp = new TempFolder();
        var target = temp.File("0123456789", "target");
        var link = temp.Combine("link");
        File.CreateSymbolicLink(link, target);
        var over = temp.File(new string('a', 11), "over");
        var exact = temp.File(new string('b', 10), "exact");

        BoundedFile.Read(link, 10, BoundedRoad.Portable).Should().BeOfType<BoundedRead.Refused>().Which.Reason.Should().Contain("link");
        BoundedFile.Read(over, 10, BoundedRoad.Portable).Should().BeOfType<BoundedRead.Refused>().Which.Reason.Should().Contain("bigger");
        BoundedFile.Read(exact, 10, BoundedRoad.Portable).Should().BeOfType<BoundedRead.Bytes>().Which.Value.Should().HaveCount(10);
    }
}
