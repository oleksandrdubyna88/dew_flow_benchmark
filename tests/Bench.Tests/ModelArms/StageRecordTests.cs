using Bench.Domain.Trace;
using FluentAssertions;
using ModelArms.Core.Checks;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary><c>stages.json</c> (E3.S5, C17): how each of the four stages ended, as the verb writes it into <c>out/objective/</c> and <c>--parse-only</c> reads it back. Nothing here reads a file — the record is a function over text.
/// <para>
/// What this does NOT prove: that the verb writes the file where <c>--parse-only</c> reads it (<c>ChecksVerbTests</c> does, by running both).
/// </para></summary>
public sealed class StageRecordTests
{
    private static StageRun Ran(long exit, long seconds) => new(CapturedCount.Number(exit), seconds, Killed: false);

    private static readonly StageRuns Runs = new(
        Ran(0, 65),
        new StageRun(CapturedCount.Unavailable("killed at its wall of 1 s"), 30, Killed: true),
        Ran(1, 2),
        Ran(2, 0));

    [Fact]
    public void A_stage_record_round_trips_and_a_missing_exit_is_unavailable()
    {
        var text = StageRecord.Write(Runs);

        text.Should().Be("""{"build":{"exit":0,"seconds":65,"killed":false},"suite":{"exit":null,"seconds":30,"killed":true},"format":{"exit":1,"seconds":2,"killed":false},"extension":{"exit":2,"seconds":0,"killed":false}}""");
        var read = StageRecord.Read(text).Ok();
        read.Build.Should().Be(Runs.Build);
        read.Format.Should().Be(Runs.Format);
        read.Extension.Should().Be(Runs.Extension);
        read.Suite.Exit.WasCaptured.Should().BeFalse("a tool killed at its wall has no exit code — never a zero");
        read.Suite.Killed.Should().BeTrue();
        read.Suite.Seconds.Should().Be(30);

        var noExit = StageRecord.Read("""{"build":{"seconds":1,"killed":false},"suite":{"exit":null,"seconds":1,"killed":false},"format":{"exit":0,"seconds":1,"killed":false},"extension":{"exit":0,"seconds":1,"killed":false}}""").Ok();

        noExit.Build.Exit.WasCaptured.Should().BeFalse("an exit the file does not carry is not captured, never 0");
        StageRecord.Write(noExit).Should().Contain("\"build\":{\"exit\":null", "and it is written back as null");
    }

    [Fact]
    public void The_exits_are_the_four_the_report_reads()
    {
        var exits = StageRecord.Exits(Runs);

        exits.Build.Value.Should().Be(0);
        exits.Suite.WasCaptured.Should().BeFalse();
        exits.Format.Value.Should().Be(1);
        exits.Extension.Value.Should().Be(2);
        StageRecord.Exits(StageRecord.Unrecorded).Should().Match<StageExits>(e => !e.Build.WasCaptured && !e.Suite.WasCaptured && !e.Format.WasCaptured && !e.Extension.WasCaptured, "no stages.json is four exits nobody recorded");
    }

    [Theory]
    [InlineData("", "not JSON")]
    [InlineData("[]", "JSON object")]
    [InlineData("""{"build":{"exit":0,"seconds":1,"killed":false}}""", "'suite'")]
    [InlineData("""{"build":{"exit":"0","seconds":1,"killed":false},"suite":{},"format":{},"extension":{}}""", "'build'")]
    [InlineData("""{"build":{"exit":0,"seconds":"x","killed":false},"suite":{},"format":{},"extension":{}}""", "'build'")]
    [InlineData("""{"build":{"exit":0,"seconds":1,"killed":"no"},"suite":{},"format":{},"extension":{}}""", "'build'")]
    [InlineData("""{"build":3,"suite":{},"format":{},"extension":{}}""", "'build'")]
    public void A_record_that_does_not_read_is_refused_naming_what_is_wrong(string text, string says)
    {
        var read = StageRecord.Read(text);

        read.Should().BeOfType<Bench.Domain.Outcome<StageRuns>.Fail>().Which.Reason.Should().Contain(says.Replace("not JSON", "JSON"));
    }
}
