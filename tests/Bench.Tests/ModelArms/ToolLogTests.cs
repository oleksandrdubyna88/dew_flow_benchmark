using FluentAssertions;
using ModelArms.Adapters;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The log a verb holds for one tool (E3.S2, E3.S5): created exclusively and read back through the SAME handle. The read is bounded by the limit, not by a length looked at once — the arm's code can still be appending to the file while it is read.
/// <para>
/// What this does NOT prove: a concurrent writer in the real race window — the test grows the file at the one moment the old read was unbounded, through a seam.
/// </para></summary>
public sealed class ToolLogTests
{
    [Fact]
    public void A_log_that_grows_past_the_limit_after_its_length_was_checked_is_refused_not_read_whole()
    {
        using var folder = new TempFolder();
        var created = ToolLog.Create(folder.Combine("a.log"));
        using var log = created.Ok();
        log.WriteLine("ten bytes!");

        var read = log.ReadBack(100, () => log.WriteLine(new string('x', 5000)));

        read.Should().BeOfType<Bench.Domain.Outcome<string>.Fail>("a log that is past the limit by the time it is read is not read whole — the memory the read takes is bounded by the limit, whatever the writer does")
            .Which.Reason.Should().Contain("bigger than 100 bytes");
    }

    [Fact]
    public void A_log_within_the_limit_reads_back_whole_and_the_handle_still_appends()
    {
        using var folder = new TempFolder();
        using var log = ToolLog.Create(folder.Combine("a.log")).Ok();
        log.WriteLine("first");

        log.ReadBack(100).Ok().Should().Be("first\n");
        log.WriteLine("second");

        log.ReadBack(100).Ok().Should().Be("first\nsecond\n", "the read left the handle at the end: what follows is appended, not written over");
        log.ReadBack(13).Ok().Should().Be("first\nsecond\n", "a log exactly at the limit is read");
        log.ReadBack(12).Should().BeOfType<Bench.Domain.Outcome<string>.Fail>("and one byte past it is not");
    }
}
