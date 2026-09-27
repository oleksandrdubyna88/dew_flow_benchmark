namespace Bench.Domain.Runs;

/// <summary>The global slot rotation that balances execution order ACROSS A WHOLE MATRIX.
/// <para>
/// Rotating by the repeat index alone — <c>repeatIndex % legCount</c> — looks balanced and is not: at an
/// odd repeat count it deals 2:1, identically for every question, so the bias never averages out and the leg
/// that runs first systematically enjoys a warmer cache and a fresher context. Counting slots globally
/// instead costs one integer and removes the bias entirely; <c>MatrixOrderTests</c> pins the refuted scheme.
/// </para>
/// <para>
/// Extracted from <see cref="Matrix"/> when the gate benchmark needed the same rotation over reviewers
/// instead of legs: one function both matrices call, so the 2:1 defect cannot be re-introduced in a copy.
/// </para></summary>
public static class SlotRotation
{
    /// <summary>The legs of slot <paramref name="slotIndex"/>, starting from the one whose turn it is to
    /// lead. An empty list rotates to an empty list rather than dividing by zero — the matrices refuse an
    /// empty axis before they get here, and this stays a pure function of its arguments either way.</summary>
    public static IReadOnlyList<T> Rotated<T>(IReadOnlyList<T> legs, int slotIndex)
    {
        if (legs.Count == 0)
        {
            return [];
        }

        var offset = slotIndex % legs.Count;
        return [.. legs.Skip(offset), .. legs.Take(offset)];
    }
}
