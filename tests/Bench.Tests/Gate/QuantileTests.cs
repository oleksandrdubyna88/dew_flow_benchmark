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

    /// <summary>Python's <c>round</c> rounds the EXACT binary value, half to even; <c>Math.Round(x, 1)</c> scales
    /// by ten first, and the scaled product can land on a tie the true value never was. Every expected value below
    /// was printed by running the report's own <c>q</c> under Python 3.14.6 (2026-09-27), with the exact decimal
    /// expansion of the interpolated value alongside, so the reason for each digit is on the page.</summary>
    [Theory]
    // q([12.3, 12.4], .5): raw 12.35000000000000142108547152020037174224853515625 → 12.4
    [InlineData(new[] { 12.3, 12.4 }, 0.5, 12.4)]
    // q([0.1, 0.2], .5): raw 0.15000000000000002220446049250313080847263336181640625 → 0.2
    [InlineData(new[] { 0.1, 0.2 }, 0.5, 0.2)]
    // q([1.0, 1.5], .5): raw 1.25 exactly — a TRUE tie, half to even → 1.2
    [InlineData(new[] { 1.0, 1.5 }, 0.5, 1.2)]
    // q([0.35], .5): 0.34999999999999997779553950749686919152736663818359375 → 0.3 (×10 lands on 3.5 and would give 0.4)
    [InlineData(new[] { 0.35 }, 0.5, 0.3)]
    // q([1.45], .5): 1.4499999999999999555910790149937383830547332763671875 → 1.4
    [InlineData(new[] { 1.45 }, 0.5, 1.4)]
    // q([2.675], .5): 2.67499999999999982236431605997495353221893310546875 → 2.7 (one decimal: 2.67… rounds up)
    [InlineData(new[] { 2.675 }, 0.5, 2.7)]
    public void The_quantile_rounds_the_exact_binary_value_half_to_even_as_python_does(double[] readings, double p, double expected)
    {
        Quantile.Q(readings, p).Should().Be(Figure.Of(expected));
    }

    [Theory]
    [InlineData(0.25, 1, 0.2)]
    [InlineData(0.35, 1, 0.3)]
    [InlineData(-0.35, 1, -0.3)]
    [InlineData(2.5, 0, 2.0)]
    [InlineData(3.5, 0, 4.0)]
    [InlineData(1e22, 1, 1e22)]
    [InlineData(0.0, 1, 0.0)]
    public void Python_rounding_is_half_to_even_on_the_exact_value(double value, int digits, double expected)
    {
        // python: round(0.25, 1) → 0.2 · round(0.35, 1) → 0.3 · round(-0.35, 1) → -0.3 · round(2.5) → 2 · round(3.5) → 4
        PythonRound.Of(value, digits).Should().Be(expected);
    }

    [Fact]
    public void Two_readings_interpolate_and_none_is_unknown_rather_than_zero()
    {
        // python: q([2.0, 3.0], 0.5) → 2.5; q([], 0.5) → None
        Quantile.Q([2.0, 3.0], 0.5).Should().Be(Figure.Of(2.5));
        Quantile.Q([], 0.5).Should().Be(Figure.Unknown, "a median of nothing is not zero seconds");
    }
}
