using FluentAssertions;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>Where a pinned reader's scratch goes when the reader does not end in time: INSIDE the run's owned folder, under a plain name, so the run removes it with the folder and nothing of it lands in the temp root.
/// <para>
/// What this does NOT prove: that a real reader can time out (a timeout is never provoked here; <see cref="PinnedConsumer.RunAsync"/> calls the one function these facts pin).
/// </para></summary>
public sealed class PinnedConsumerTests
{
    [Fact]
    public void A_timed_out_readers_scratch_is_kept_inside_the_owned_folder()
    {
        using var temp = new TempFolder();
        var scratch = temp.Folder("readers", "x");
        File.WriteAllText(System.IO.Path.Combine(scratch, "out.txt"), "partial output");

        var kept = PinnedConsumer.Preserved(PinnedReaders.Metrics, scratch);

        kept.Should().StartWith(temp.Path + System.IO.Path.DirectorySeparatorChar, "the scratch is moved inside the owned folder, not beside it");
        System.IO.Path.GetFileName(kept).Should().StartWith("kept-reader-metrics-");
        Directory.Exists(scratch).Should().BeFalse("the scratch moved");
        File.ReadAllText(System.IO.Path.Combine(kept, "out.txt")).Should().Be("partial output", "its files moved with it");
    }

    [Fact]
    public void A_scratch_with_no_owned_folder_above_it_stays_where_it_is()
    {
        var scratch = System.IO.Path.Combine(System.IO.Path.DirectorySeparatorChar.ToString(), "srv", "no-owned-folder-above", "x");

        PinnedConsumer.Preserved(PinnedReaders.Metrics, scratch).Should().Be(scratch, "no walk reaches the temp root: the scratch is returned as it is, and nothing is moved");
    }

    [Fact]
    public void A_scratch_that_is_the_owned_folder_itself_stays_where_it_is()
    {
        using var temp = new TempFolder();

        PinnedConsumer.Preserved(PinnedReaders.Metrics, temp.Path).Should().Be(temp.Path);
        Directory.Exists(temp.Path).Should().BeTrue("a folder is never moved into itself");
    }
}
