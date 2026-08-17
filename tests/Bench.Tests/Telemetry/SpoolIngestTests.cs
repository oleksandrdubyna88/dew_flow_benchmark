using Bench.Application;
using Bench.Domain.Telemetry;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Telemetry;

/// <summary>What a spool file MEANS, decided without a disk or a database.</summary>
public sealed class SpoolIngestTests
{
    [Fact]
    public void Every_line_of_a_real_spool_file_reads()
    {
        var (records, refused) = SpoolIngest.Read(Fixture.CorrelatedText);

        records.Should().HaveCount(3);
        refused.Should().BeEmpty();
    }

    [Fact]
    public void A_half_written_last_line_costs_that_line_and_nothing_before_it()
    {
        // The normal shape of a killed emitter: the file ends mid-record. A reader that aborted on the
        // first failure would discard a whole run's telemetry over its final byte.
        var truncated = Fixture.CorrelatedText + """{"schema":"telemetry/v0","at":"2026-08""";

        var (records, refused) = SpoolIngest.Read(truncated);

        records.Should().HaveCount(3, "everything written before the interruption is still evidence");

        var loss = refused.Should().ContainSingle().Subject;
        loss.ToString().Should().Contain("line 4").And.Contain("malformed");

        // Permanent, and that decides the FILE's fate: nothing will ever read a half-written line, so
        // keeping the spool for a retry that cannot succeed would make every ordinary spool immortal.
        loss.Retryable.Should().BeFalse();
    }

    [Fact]
    public void A_refused_line_names_its_position_so_an_operator_can_find_it()
    {
        var mixed = string.Join('\n', Fixture.CorrelatedLines[0], """{"schema":"telemetry/v9"}""", Fixture.CorrelatedLines[1]);

        var (records, refused) = SpoolIngest.Read(mixed);

        records.Should().HaveCount(2, "a refusal is per line, never per file");

        var refusal = refused.Should().ContainSingle().Subject;
        refusal.ToString().Should().Contain("line 2").And.Contain("telemetry/v9");

        // Retryable, and that is the opposite decision: the line is fine and this build is simply
        // older than the emitter, so the file must survive until a build that reads it runs.
        refusal.Retryable.Should().BeTrue();
    }

    [Fact]
    public void Blank_lines_are_not_records_and_are_not_refusals_either()
    {
        var padded = "\n\n" + Fixture.CorrelatedText + "\n\n";

        var (records, refused) = SpoolIngest.Read(padded);

        // A trailing newline is how every writer ends a file; counting it as a broken record would put
        // a refusal in every clean ingest and make the number meaningless.
        records.Should().HaveCount(3);
        refused.Should().BeEmpty();
    }

    [Fact]
    public void An_ingest_report_adds_up_across_files_without_losing_a_category()
    {
        var first = new IngestReport(3, 1, 0, 0, []);
        var second = new IngestReport(2, 0, 1, 1, ["file.jsonl line 9: malformed"]);

        var total = first.Plus(second);

        // Refused stays its own number: "we ingested 5 of 7" is a materially different report from
        // "we ingested 5", and folding the two would erase the difference.
        //
        // Field by field rather than by value equality: this type carries a list, and a record
        // struct compares a list by REFERENCE — see the note on IngestReport.Reasons.
        total.Ingested.Should().Be(5);
        total.Duplicate.Should().Be(1);
        total.Refused.Should().Be(1);
        total.Retained.Should().Be(1);
        total.Reasons.Should().Equal("file.jsonl line 9: malformed");
    }

    [Fact]
    public void The_records_carry_the_three_outcomes_the_emitter_wrote()
    {
        var (records, _) = SpoolIngest.Read(Fixture.CorrelatedText);

        records.Select(r => r.Outcome).Should()
            .Equal(ToolOutcome.Answered, ToolOutcome.Refused, ToolOutcome.Error);
    }
}
