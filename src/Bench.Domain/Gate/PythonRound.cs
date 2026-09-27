using System.Globalization;
using System.Numerics;

namespace Bench.Domain.Gate;

/// <summary>Python's <c>round(x, n)</c>, exactly: the EXACT binary value of <paramref name="value"/> is rounded
/// to <c>n</c> decimals half to even, and the decimal result is read back as the nearest double.
/// <para>
/// This is not <c>Math.Round(x, n)</c>. .NET scales by <c>10ⁿ</c> in floating point and rounds the product, and
/// the product can be a tie the true value never was: <c>0.35</c> is <c>0.34999999999999997779…</c>, Python says
/// <c>0.3</c>, and <c>Math.Round(0.35, 1)</c> says <c>0.4</c> because <c>0.35 × 10</c> rounds to exactly 3.5. An
/// imported p50 and a native one computed by two roundings disagree in the last digit, which is the digit a
/// table is read by. The arithmetic is done in integers: a finite double is <c>m · 2ᵉ</c>, so
/// <c>value · 10ⁿ</c> is the exact fraction <c>m · 10ⁿ / 2⁻ᵉ</c>.
/// </para></summary>
public static class PythonRound
{
    public static double Of(double value, int digits)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(digits);

        if (!double.IsFinite(value) || value == 0)
        {
            return value;
        }

        var (numerator, denominator) = Exact(Math.Abs(value));
        var quotient = BigInteger.DivRem(numerator * BigInteger.Pow(10, digits), denominator, out var remainder);
        var magnitude = double.Parse(Decimal(quotient + HalfEven(quotient, remainder, denominator), digits), CultureInfo.InvariantCulture);

        return value < 0 ? -magnitude : magnitude;
    }

    /// <summary>A positive finite double as the exact fraction <c>numerator / denominator</c>, the denominator a
    /// power of two.</summary>
    private static (BigInteger Numerator, BigInteger Denominator) Exact(double value)
    {
        var bits = BitConverter.DoubleToInt64Bits(value);
        var biased = (int)((bits >> 52) & 0x7FF);
        var mantissa = new BigInteger(Mantissa(bits, biased));
        var exponent = Math.Max(biased, 1) - 1075;

        return exponent >= 0 ? (mantissa << exponent, BigInteger.One) : (mantissa, BigInteger.One << -exponent);
    }

    /// <summary>The 53-bit significand; a subnormal has no implicit leading one.</summary>
    private static long Mantissa(long bits, int biased) =>
        (bits & 0xFFFFFFFFFFFFFL) | (biased == 0 ? 0L : 1L << 52);

    /// <summary>One when the dropped remainder rounds the quotient up: above half, or exactly half with an odd
    /// quotient.</summary>
    private static BigInteger HalfEven(BigInteger quotient, BigInteger remainder, BigInteger denominator) =>
        (remainder * 2).CompareTo(denominator) switch
        {
            > 0 => BigInteger.One,
            0 when !quotient.IsEven => BigInteger.One,
            _ => BigInteger.Zero,
        };

    /// <summary>An integer count of <c>10⁻ⁿ</c> units written as a decimal literal.</summary>
    private static string Decimal(BigInteger units, int digits)
    {
        var text = units.ToString(CultureInfo.InvariantCulture).PadLeft(digits + 1, '0');

        return digits == 0 ? text : $"{text[..^digits]}.{text[^digits..]}";
    }
}
