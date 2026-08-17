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
        Fixture.PreCorrelationLines.Should().NotBeEmpty();

        foreach (var line in Fixture.PreCorrelationLines)
        {
            var record = TelemetryCodec.ReadLine(line).Ok();

            record.Correlation.IsAttributed.Should().BeFalse(
                "refusing these lines would discard every record an emitter wrote yesterday, to gain a field it could not have known about");
            record.Correlation.Leg.Reason.Should().NotBeEmpty("an absence has a reason, and the reason is what a reader needs");
        }
    }

    /// <summary>The other half, and the one that was missing: the field is not merely tolerated, it is
    /// READ. Until this fixture existed, every assertion in this class ran against a line that could not
    /// carry a correlation — so a reader that silently discarded the field would have been green.</summary>
    [Fact]
    public void A_line_the_emitter_writes_today_carries_its_leg_and_phase_through_the_reader()
    {
        Fixture.CorrelatedLines.Should().NotBeEmpty();

        foreach (var line in Fixture.CorrelatedLines)
        {
            var record = TelemetryCodec.ReadLine(line).Ok();

            record.Correlation.IsAttributed.Should().BeTrue("the emitter stamped this run with --correlation");
            record.Correlation.Leg.Value.Should().Be("cell-17");
            record.Correlation.Phase.Value.Should().Be("verify");
        }
    }

    /// <summary>Both shapes are the SAME schema version, which is the claim `correlation` being additive
    /// rests on. A reader that needed a version bump to accept the new field would have made every spool
    /// already on disk unreadable to gain one member.</summary>
    [Fact]
    public void Both_wire_shapes_are_telemetry_v0_and_agree_on_everything_but_the_correlation()
    {
        foreach (var (shape, lines) in Fixture.BothShapes)
        {
            var record = TelemetryCodec.ReadLine(lines[0]).Ok();

            record.Tool.Should().Be("rt_read_local_file", $"the {shape} fixture records the same call");
            record.Outcome.Should().Be(ToolOutcome.Answered);
            record.Emitter.App.Should().Be("mcp-test");
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
