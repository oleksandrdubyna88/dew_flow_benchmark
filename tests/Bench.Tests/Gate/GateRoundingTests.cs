using Bench.Domain.Gate;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Gate.GateReportFixtures;

namespace Bench.Tests.Gate;

/// <summary>Every place the Python report calls <c>round</c>, this report rounds the same way — so an imported
/// row and a native one agree in the last digit, which is the digit a table is read by. Each vector below is
/// one where <c>Math.Round(x, n)</c> (scale by <c>10ⁿ</c> in floating point, then round) and Python's
/// <c>round(x, n)</c> (the exact binary value, half to even) DISAGREE; the expected values were printed by
/// Python 3.14.6 on 2026-09-27 with the report's own expressions: <c>pct</c> =
/// <c>round(100.0 * a / b, 1)</c>, <c>round(statistics.mean(xs), 2)</c>, <c>round(cost / n, 4)</c>,
/// <c>round(cost, 4)</c>, <c>round(cost / seeds_found_total, 4)</c>.</summary>
public sealed class GateRoundingTests
{
    [Theory]
    // round(100.0 * 7 / 2000, 1) → 0.3 (0.35 is 0.34999…; ×10 lands on the tie 3.5)
    [InlineData(7, 2000, 0.3)]
    // round(100.0 * 1 / 2000, 1) → 0.1 (0.05 is 0.05000000000000000277…, above the tie)
    [InlineData(1, 2000, 0.1)]
    public void A_percentage_rounds_as_the_python_pct_does(long part, long whole, double expected)
    {
        Figure.Percent(part, whole).Should().Be(Figure.Of(expected));
    }

    [Theory]
    // round(statistics.mean([0.2, 0.45]), 2) → 0.33
    [InlineData(new[] { 0.2, 0.45 }, 0.33)]
    // round(statistics.mean([0.025, 0.285]), 2) → 0.15
    [InlineData(new[] { 0.025, 0.285 }, 0.15)]
    public void A_mean_rounds_as_the_python_report_does(double[] readings, double expected)
    {
        Figure.Mean(readings).Should().Be(Figure.Of(expected));
    }

    [Fact]
    public void Cost_per_run_total_and_per_seed_round_as_the_python_report_does()
    {
        var a = Run("cs2", "grok", 1, findings: 1, cost: CapturedUsd.Amount(0.00005m));
        var b = Run("cs2", "grok", 2, findings: 1, cost: CapturedUsd.Amount(0.12345m));
        var verdicts = new[]
        {
            Verdict(a, 0, Strict(StrictReading.Supported, seed: "cs2-S1"), StrictHash),
            Verdict(b, 0, Strict(StrictReading.Supported, seed: "cs2-S2"), StrictHash),
        };

        var row = GateReport.PerModel(Scope(PinA), StrictRubric, Input([a, b], verdicts)).Rows.Single();

        row.CostPerRun.Should().Be(Figure.Of(0.0617), "python: round(0.1235 / 2, 4) → 0.0617");
        row.CostPerSeed.Should().Be(Figure.Of(0.0617), "python: round(0.1235 / 2, 4) → 0.0617 — two seeds found");
        row.CostTotal.Should().Be(Figure.Of(0.1235));

        var c = Run("cs2", "grok", 1, findings: 1, cost: CapturedUsd.Amount(0.00025m));
        var d = Run("cs2", "grok", 2, findings: 1, cost: CapturedUsd.Amount(0.2m));
        GateReport.PerModel(Scope(PinA), StrictRubric, Input([c, d])).Rows.Single().CostTotal
            .Should().Be(Figure.Of(0.2003), "python: round(0.00025 + 0.2, 4) → 0.2003");
    }
}
