using FluentAssertions;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>What a pinned reader that does not end in time says: the scratch folder is removed with the run's owned folder, so the failure message QUOTES what the diagnosis needs (what the scratch held, what the reader had written to its pipes) and never names the folder as something kept.
/// <para>
/// What this does NOT prove: that a real reader can time out (a timeout is never provoked here; <see cref="PinnedConsumer.RunAsync"/> calls the one function these facts pin).
/// </para></summary>
public sealed class PinnedConsumerTests
{
    [Fact]
    public void A_timed_out_readers_message_quotes_what_its_scratch_held_and_its_pipes_and_names_no_kept_folder()
    {
        using var temp = new TempFolder();
        var scratch = temp.Folder("readers", "x");
        File.WriteAllText(System.IO.Path.Combine(scratch, "metrics.json"), "partial output");
        temp.File("deeper", "readers", "x", "sub", "tables.md");

        var message = PinnedConsumer.TimeoutMessage(PinnedReaders.Metrics, "killed after 30 s", "out so far", "err so far", scratch);

        message.Should().Contain("metrics.json (14 bytes)").And.Contain("sub").And.Contain("tables.md (6 bytes)");
        message.Should().Contain("stdout so far: out so far").And.Contain("stderr so far: err so far").And.Contain("metrics.mjs").And.Contain("30");
        message.Should().NotContain(scratch, "the folder is removed with the run: its path is no help").And.NotContain("kept at");
        Directory.Exists(scratch).Should().BeTrue("building the message moves and removes nothing");
    }

    [Fact]
    public void A_scratch_that_is_missing_or_empty_is_said_so_and_a_long_listing_is_cut()
    {
        using var temp = new TempFolder();
        var empty = temp.Folder("empty");
        var many = temp.Folder("many");
        foreach (var number in Enumerable.Range(0, 25))
        {
            File.WriteAllText(System.IO.Path.Combine(many, $"f{number:00}.txt"), "x");
        }

        PinnedConsumer.TimeoutMessage(PinnedReaders.Metrics, "d", string.Empty, string.Empty, temp.Combine("gone")).Should().Contain("(nothing)");
        PinnedConsumer.TimeoutMessage(PinnedReaders.Metrics, "d", string.Empty, string.Empty, empty).Should().Contain("(no files)");
        PinnedConsumer.TimeoutMessage(PinnedReaders.Metrics, "d", string.Empty, string.Empty, many).Should().Contain("f19.txt (1 bytes)").And.NotContain("f20.txt").And.Contain("and 5 more");
    }
}
