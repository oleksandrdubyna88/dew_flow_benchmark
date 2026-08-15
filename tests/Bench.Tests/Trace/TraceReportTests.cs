using Bench.Application;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Trace;

/// <summary>What a reader of one leg actually sees.
/// <para>
/// The report is where every "not captured" either survives or quietly becomes a zero, so its exact
/// text is worth pinning. A gap in instrumentation that renders as a number is indistinguishable from
/// a measurement, and it is the reader — not the code — who then draws the wrong conclusion.
/// </para></summary>
public sealed class TraceReportTests
{
    [Fact]
    public void An_empty_trace_says_not_captured_on_every_line_that_has_nothing()
    {
        var lines = TraceReport.Render(LegTrace.Empty);

        lines.Single(l => l.StartsWith("prompt", StringComparison.Ordinal))
            .Should().Contain(TraceReport.NotCaptured).And.Contain("not run");
        lines.Single(l => l.StartsWith("funnel", StringComparison.Ordinal))
            .Should().Contain(TraceReport.NotCaptured);
    }

    [Fact]
    public void A_captured_value_reports_its_size_rather_than_its_body()
    {
        var trace = LegTrace.Empty with { Response = Captured.Text("in OrderService.Total") };

        // A leg's answer can be tens of kilobytes. The report is a scan line, not the evidence — the
        // text itself lives in the result store, which is where a judge reads it from.
        TraceReport.Render(trace).Single(l => l.StartsWith("response", StringComparison.Ordinal))
            .Should().Contain("21 char(s)").And.NotContain(TraceReport.NotCaptured);
    }

    [Fact]
    public void The_reason_a_field_is_missing_is_printed_beside_it()
    {
        var trace = LegTrace.Empty with
        {
            Response = Captured.Unavailable("this CLI stream reports a result's size, not its text"),
        };

        // "not captured" alone invites the reader to assume an oversight. The reason is what turns it
        // into a fact about the runtime.
        TraceReport.Render(trace).Single(l => l.StartsWith("response", StringComparison.Ordinal))
            .Should().Contain("size, not its text");
    }

    [Fact]
    public void Refused_calls_are_counted_separately_from_the_calls_that_ran()
    {
        var trace = LegTrace.Empty with
        {
            ToolCalls =
            [
                new ToolCall("read_file", "{}", false, string.Empty, TimeSpan.Zero),
                new ToolCall("read_file", "{}", true, "outside", TimeSpan.Zero),
                new ToolCall("read_file", "{}", true, "outside", TimeSpan.Zero),
            ],
        };

        // A leg that made thirty calls and had twenty refused did not do the same work as one that made
        // ten and had none — and the totals alone cannot tell them apart.
        TraceReport.Render(trace).Single(l => l.StartsWith("tools", StringComparison.Ordinal))
            .Should().Contain("3 call(s)").And.Contain("2 refused");
    }

    [Fact]
    public void The_three_time_buckets_are_printed_apart()
    {
        var trace = LegTrace.Empty with
        {
            Time = new TimeBuckets(TimeSpan.FromMilliseconds(30), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8)),
        };

        // Never one total. The third bucket exists because a busy accelerator otherwise reads as a slow
        // model, and a single number would put that conclusion back on the reader.
        TraceReport.Render(trace).Single(l => l.StartsWith("time", StringComparison.Ordinal))
            .Should().Contain("tools 30 ms").And.Contain("thinking 2000 ms").And.Contain("waiting 8000 ms");
    }

    [Fact]
    public void A_white_box_funnel_prints_its_version_its_stages_and_its_remainder()
    {
        var trace = LegTrace.Empty with
        {
            Funnel = new RetrievalFunnel(
                TraceContract.V0,
                [new FunnelStage("retrieve", 200, 145, 210), new FunnelStage("rerank", 50, 20, 3500)],
                8010,
                ["graph-enrich"]),
        };

        var lines = TraceReport.Render(trace);

        lines.Single(l => l.StartsWith("funnel", StringComparison.Ordinal))
            .Should().Contain(TraceContract.V0).And.Contain("retrieve 200/145");
        lines.Single(l => l.StartsWith("accounted", StringComparison.Ordinal))
            .Should().Contain("8010 ms total").And.Contain("4300 ms unattributed");
        lines.Single(l => l.StartsWith("absent", StringComparison.Ordinal))
            .Should().Contain("graph-enrich");
    }

    [Fact]
    public void An_engine_that_declares_no_absent_stages_says_so_rather_than_printing_nothing()
    {
        var trace = LegTrace.Empty with
        {
            Funnel = new RetrievalFunnel(TraceContract.V0, [new FunnelStage("fuse", 10, 8, 1)], 10, []),
        };

        // A blank cell is read as "none" by an optimist and "unknown" by a pessimist, and both are
        // guessing at what the engine declared.
        TraceReport.Render(trace).Single(l => l.StartsWith("absent", StringComparison.Ordinal))
            .Should().Contain("none declared");
    }

    [Fact]
    public void Cost_is_printed_with_enough_precision_to_be_summed()
    {
        var trace = LegTrace.Empty with { CostUsd = 0.0431m };

        // Four decimals because a single leg costs cents and a series of them costs real money — the
        // whole measured series came to $164.16, one rounding at a time.
        TraceReport.Render(trace).Single(l => l.StartsWith("cost", StringComparison.Ordinal))
            .Should().Contain("$0.0431");
    }
}
