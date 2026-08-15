using Bench.Application;
using Bench.Domain.Telemetry;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Telemetry;

/// <summary>Correlation — what lets a server-side record be attributed to the leg, and the phase, that
/// caused it. Added while the contract was still <c>telemetry/v0</c>, so nothing had to be broken.</summary>
public sealed class TelemetryCorrelationTests
{
    [Fact]
    public void A_line_written_before_correlation_existed_still_reads_and_reads_as_unattributed()
    {
        // The fixture is a verbatim copy of what the emitter in the other repository actually wrote,
        // produced before this field existed. That makes it real evidence of backward compatibility
        // rather than a line authored to agree with the reader under test.
        Fixture.Lines.Should().NotBeEmpty();

        foreach (var line in Fixture.Lines)
        {
            var record = TelemetryCodec.ReadLine(line).Ok();

            record.Correlation.IsAttributed.Should().BeFalse(
                "refusing these lines would discard every record an emitter wrote yesterday, to gain a field it could not have known about");
            record.Correlation.Leg.Reason.Should().NotBeEmpty("an absence has a reason, and the reason is what a reader needs");
        }
    }

    [Fact]
    public void An_unattributed_record_says_so_rather_than_carrying_an_empty_leg()
    {
        var none = TelemetryCorrelation.None;

        none.IsAttributed.Should().BeFalse();
        none.Leg.WasCaptured.Should().BeFalse();
        none.Leg.Reason.Should().Contain("declared no leg");
    }

    [Fact]
    public void Two_identical_calls_from_different_legs_are_different_records()
    {
        var first = Call(TelemetryCorrelation.Of("cell-a", "Investigate"));
        var second = Call(TelemetryCorrelation.Of("cell-b", "Investigate"));

        TelemetryCodec.Fingerprint(second).Should().NotBe(
            TelemetryCodec.Fingerprint(first),
            "the idempotency guard must not merge two legs' work into one row just because the call looked the same");
    }

    [Fact]
    public void Two_identical_calls_from_different_phases_of_one_leg_are_different_records()
    {
        var investigate = Call(TelemetryCorrelation.Of("cell-a", "Investigate"));
        var verify = Call(TelemetryCorrelation.Of("cell-a", "Verify"));

        TelemetryCodec.Fingerprint(verify).Should().NotBe(TelemetryCodec.Fingerprint(investigate));
    }

    [Fact]
    public void The_same_call_from_the_same_leg_and_phase_is_still_one_record()
    {
        var once = Call(TelemetryCorrelation.Of("cell-a", "Fix"));
        var again = Call(TelemetryCorrelation.Of("cell-a", "Fix"));

        TelemetryCodec.Fingerprint(again).Should().Be(
            TelemetryCodec.Fingerprint(once),
            "re-ingesting a spool must still insert nothing the second time");
    }

    [Fact]
    public void An_unattributed_call_and_an_attributed_one_are_not_the_same_record()
    {
        TelemetryCodec.Fingerprint(Call(TelemetryCorrelation.Of("cell-a", "Fix")))
            .Should().NotBe(TelemetryCodec.Fingerprint(Call(TelemetryCorrelation.None)));
    }

    internal static ToolTelemetry Call(
        TelemetryCorrelation correlation,
        string tool = "rag_search",
        ToolOutcome outcome = ToolOutcome.Answered,
        long tokens = -1,
        double serverMs = 120) =>
        new(
            new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero),
            new TelemetryEmitter("mcp", 1234, "box"),
            new TelemetryCaller(
                Captured.Text("claude-code"),
                Captured.Text("2.1.0"),
                Captured.Unavailable("no MCP revision tells a server which model drives it"),
                "stdio"),
            tool,
            "DewFlow",
            "{}",
            0,
            outcome,
            string.Empty,
            512,
            "body",
            0,
            tokens >= 0 ? CapturedCount.Number(tokens) : CapturedCount.Unavailable("this tool reports no tokens"),
            TimeSpan.FromMilliseconds(serverMs),
            correlation);
}
