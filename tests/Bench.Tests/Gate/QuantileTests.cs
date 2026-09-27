using Bench.Domain.Gate;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate;

/// <summary>The quantile the Python report computes (<c>report.py: q</c>), pinned on fixed vectors whose
/// expected values were produced by running that function — so an imported p50 and a native one agree to the
/// digit. A nearest-rank quantile would give 9.0 where the report says 6.9.</summary>
public sealed class QuantileTests
{
    private static readonly double[] Eight = [3.0, 1.0, 4.0, 1.0, 5.0, 9.0, 2.0, 6.0];

    [Theory]
    [InlineData(0.5, 3.5)]
    [InlineData(0.9, 6.9)]
    [InlineData(0.1, 1.0)]
    public void The_quantile_matches_the_python_report_on_a_fixed_vector(double p, double expected)
    {
        // python: q([3.0, 1.0, 4.0, 1.0, 5.0, 9.0, 2.0, 6.0], p) → 3.5 · 6.9 · 1.0 (run 2026-09-27)
        Quantile.Q(Eight, p).Should().Be(Figure.Of(expected));
    }

    [Fact]
    public void A_single_reading_rounds_half_to_even_as_python_does()
    {
        // python: q([7.25], 0.9) → 7.2 — banker's rounding, which .NET's Math.Round also applies
        Quantile.Q([7.25], 0.9).Should().Be(Figure.Of(7.2));
    }

    [Fact]
    public void Two_readings_interpolate_and_none_is_unknown_rather_than_zero()
    {
        // python: q([2.0, 3.0], 0.5) → 2.5; q([], 0.5) → None
        Quantile.Q([2.0, 3.0], 0.5).Should().Be(Figure.Of(2.5));
        Quantile.Q([], 0.5).Should().Be(Figure.Unknown, "a median of nothing is not zero seconds");
    }
}
