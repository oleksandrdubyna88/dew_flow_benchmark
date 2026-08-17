using Bench.Application;
using Bench.Domain;
using Bench.Domain.Telemetry;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Telemetry;

/// <summary>Reading `telemetry/v0`. The lines here are the emitter's REAL output, copied from a spool
/// that `dew_flow_mcp`'s own tests produced — a hand-authored sample would only prove this codec agrees
/// with my idea of the other repository.</summary>
public sealed class TelemetryCodecTests
{
    [Fact]
    public void A_v0_line_from_the_real_emitter_reads_into_the_domain()
    {
        var record = TelemetryCodec.ReadLine(Fixture.CorrelatedLines[0]).Ok();

        record.Tool.Should().Be("rt_read_local_file");
        record.Scope.Should().Be("D:/work/repo");
        record.Outcome.Should().Be(ToolOutcome.Answered);
        record.ServerTime.TotalMilliseconds.Should().Be(13.4);
        record.Emitter.App.Should().Be("mcp-test");
        record.ArgumentsJson.Should().Contain("a.txt", "the emitter escapes quotes as \\u0022 and the reader must unescape them");
    }

    [Fact]
    public void The_three_outcomes_survive_the_wire_as_three_distinct_states()
    {
        var outcomes = Fixture.CorrelatedLines.Select(line => TelemetryCodec.ReadLine(line).Ok().Outcome);

        // A refused call and a broken one are different events with different remedies; a codec that
        // collapsed them would make both uncountable forever, and nothing downstream could recover it.
        outcomes.Should().Equal(ToolOutcome.Answered, ToolOutcome.Refused, ToolOutcome.Error);
    }

    [Fact]
    public void An_uncaptured_field_arrives_uncaptured_with_its_reason_intact()
    {
        var caller = TelemetryCodec.ReadLine(Fixture.CorrelatedLines[0]).Ok().Caller;

        caller.ClientName.WasCaptured.Should().BeTrue();
        caller.ClientName.Value.Should().Be("claude-code");

        // The field the whole captured/not-captured split exists for: no MCP revision carries the
        // caller's model, so reading this as an empty string — or worse, inferring it from the client
        // name — would turn a gap in instrumentation into a claim about which model was measured.
        caller.Model.WasCaptured.Should().BeFalse();
        caller.Model.Value.Should().BeEmpty();
        caller.Model.Reason.Should().Contain("model");
    }

    [Fact]
    public void An_unknown_schema_version_is_refused_by_name_rather_than_read_or_skipped()
    {
        var future = Fixture.CorrelatedLines[0].Replace("telemetry/v0", "telemetry/v9", StringComparison.Ordinal);

        var verdict = TelemetryCodec.ReadLine(future);

        // Both alternatives produce the same artefact — an ingest reporting success over data it did
        // not understand. The version must be named so the operator knows WHICH emitter to look at.
        //
        // And it is its own case, not a generic failure: this line is FINE and this build is older
        // than the emitter, so the file that carried it has to survive until a build that reads it
        // runs. A malformed line is the opposite, and the two must not share a verdict.
        var unknown = verdict.Should().BeOfType<LineVerdict.UnknownVersion>().Subject;
        unknown.Version.Should().Be("telemetry/v9");
        unknown.Reason.Should().Contain("telemetry/v9").And.Contain("telemetry/v0");
    }

    [Fact]
    public void A_malformed_line_is_refused_as_permanently_unreadable()
    {
        var verdict = TelemetryCodec.ReadLine("{not json");

        // Permanent: no future build will read these bytes, so the spool holding them may be retired.
        verdict.Should().BeOfType<LineVerdict.Unreadable>()
            .Which.Reason.Should().Contain("malformed");
    }

    [Fact]
    public void The_caller_key_renders_an_uncaptured_field_as_unknown_never_as_a_default()
    {
        var caller = TelemetryCodec.ReadLine(Fixture.CorrelatedLines[0]).Ok().Caller;

        // The key aggregates on caller and model precisely so a mid-day switch cannot blend two
        // populations into one row — and an unknown must be its own bucket, not the popular one.
        caller.Key.Should().Be("claude-code/?/stdio");
    }

    [Fact]
    public void One_record_has_one_fingerprint_and_two_different_records_do_not_share_it()
    {
        var first = TelemetryCodec.ReadLine(Fixture.CorrelatedLines[0]).Ok();
        var again = TelemetryCodec.ReadLine(Fixture.CorrelatedLines[0]).Ok();
        var other = TelemetryCodec.ReadLine(Fixture.CorrelatedLines[1]).Ok();

        // The whole idempotency guarantee rests on this: re-reading a spool must produce the same
        // identity, and two genuinely different calls must not collide into one.
        TelemetryCodec.Fingerprint(first).Should().Be(TelemetryCodec.Fingerprint(again));
        TelemetryCodec.Fingerprint(first).Should().NotBe(TelemetryCodec.Fingerprint(other));
    }
}
