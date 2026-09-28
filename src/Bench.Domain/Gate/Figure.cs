namespace Bench.Domain.Gate;

/// <summary>Why a figure is not a number: <see cref="Unassessed"/> is rendered <c>—</c> and never <c>0</c>;
/// <see cref="Unknown"/> is a value that could not be captured — a CLI reviewer's cost, never free;
/// <see cref="Withheld"/> is too few repeats to state; <see cref="NotApplicable"/> is a column the row has no
/// business in — a task with no seeds has no recall.</summary>
public enum FigureState
{
    Known,
    Unassessed,
    Unknown,
    Withheld,
    NotApplicable,

    /// <summary>A strict percentage nobody has hand-checked yet: the verdicts exist, but no person has read twenty of
    /// them against the code, so the number is not shown (measurement rule 2).</summary>
    NotHandChecked,
}

/// <summary>A number that may not be one — the domain twin of the wire's figure, so a refusal is a state the
/// page renders in words rather than a zero it averages.</summary>
public sealed record Figure(bool Known, double Value, FigureState State)
{
    public static Figure Of(double value) => new(true, value, FigureState.Known);

    public static Figure Unassessed { get; } = new(false, 0, FigureState.Unassessed);

    public static Figure Unknown { get; } = new(false, 0, FigureState.Unknown);

    public static Figure Withheld { get; } = new(false, 0, FigureState.Withheld);

    public static Figure NotApplicable { get; } = new(false, 0, FigureState.NotApplicable);

    public static Figure NotHandChecked { get; } = new(false, 0, FigureState.NotHandChecked);

    /// <summary>A percentage of <paramref name="part"/> in <paramref name="whole"/>, or <see cref="Unassessed"/>
    /// when the whole is empty — the Python report's <c>pct</c>, which answers <c>None</c> for a zero denominator.</summary>
    public static Figure Percent(long part, long whole) =>
        whole > 0 ? Of(PythonRound.Of(100.0 * part / whole, 1)) : Unassessed;

    /// <summary>The mean of some readings, or <see cref="Unknown"/> when there are none — rounded by
    /// <see cref="PythonRound"/>, as the Python report's <c>round(statistics.mean(xs), n)</c> is, so an imported
    /// and a native mean agree in the last digit (<see cref="Percent"/> likewise).</summary>
    public static Figure Mean(IReadOnlyList<double> readings, int decimals = 2) =>
        readings.Count > 0 ? Of(PythonRound.Of(readings.Sum() / readings.Count, decimals)) : Unknown;
}

/// <summary>The linear-interpolated quantile the Python report uses (<c>report.py: q</c>), so imported and
/// native p50 / p90 are computed by ONE function and agree to the digit. Sorted, <c>k = (n - 1) · p</c>,
/// interpolated between the two neighbours, rounded to one decimal by <see cref="PythonRound"/> — Python's
/// <c>round</c>, which rounds the EXACT binary value half to even. <c>Math.Round(x, 1)</c> does not: it scales
/// by ten first, and <c>0.35 × 10</c> lands on the tie <c>3.5</c> that the true value (0.3499…) never was.</summary>
public static class Quantile
{
    public static Figure Q(IReadOnlyList<double> readings, double p)
    {
        var xs = readings.Order().ToList();
        if (xs.Count == 0)
        {
            return Figure.Unknown;
        }

        var k = (xs.Count - 1) * p;
        var lo = (int)k;
        var hi = Math.Min(lo + 1, xs.Count - 1);
        return Figure.Of(PythonRound.Of(xs[lo] + (xs[hi] - xs[lo]) * (k - lo), 1));
    }
}
