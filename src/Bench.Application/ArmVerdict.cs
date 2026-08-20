using Bench.Domain;
using Bench.Domain.Splitting;
using Bench.Domain.Suites;

namespace Bench.Application;

/// <summary>The suite's two halves, as the question ids each holds.</summary>
public readonly record struct SplitHalves(IReadOnlyList<string> Selection, IReadOnlyList<string> HeldOut);

/// <summary>Whether an arm may be announced as a winner — the rule, in ONE place.
/// <para>
/// Extracted from <see cref="RunReport"/> when the arm comparison was built, rather than copied into it.
/// Both surfaces answer the same question about different populations — one run's variants, several runs'
/// compute backends — and a second copy of a rule about false winners is the last place this repository can
/// afford drift: two verdicts derived by two slightly different implementations are indistinguishable from
/// one correct verdict afterwards.
/// </para></summary>
public static class ArmVerdict
{
    /// <summary>The suite's halves, split on the suite ID rather than its stamp.
    /// <para>
    /// <see cref="Suite.IdOf"/>, because the assignment must survive a new frozen version — a comparison
    /// spanning versions would otherwise silently be a comparison of two different splits. A question the
    /// hash refuses lands in NEITHER half and shows up as the counts not adding to the question total, which
    /// is honest: inventing a half for it would put an unassignable question on whichever side happened to
    /// be checked first.
    /// </para></summary>
    public static SplitHalves Halves(string suiteStamp, IReadOnlyList<string> questions)
    {
        var suiteId = Suite.IdOf(suiteStamp);
        var assigned = questions
            .Select(question => (Question: question, Half: SeedSplit.Assign(suiteId, question)))
            .Where(x => x.Half is Outcome<SplitHalf>.Ok)
            .Select(x => (x.Question, Half: ((Outcome<SplitHalf>.Ok)x.Half).Value))
            .ToList();

        return new SplitHalves(
            [.. assigned.Where(x => x.Half == SplitHalf.Selection).Select(x => x.Question)],
            [.. assigned.Where(x => x.Half == SplitHalf.HeldOut).Select(x => x.Question)]);
    }

    /// <summary>Which half a question belongs to, for the callers that classify legs one at a time.</summary>
    public static Outcome<SplitHalf> HalfOf(string suiteStamp, string questionId) =>
        SeedSplit.Assign(Suite.IdOf(suiteStamp), questionId);

    /// <summary>Whether this arm may be announced, and by how much it beat the baseline where it did.
    /// <para>
    /// The baseline is not compared with itself, and an arm with no baseline to beat is
    /// <see cref="ProofState.NotAWinner"/> rather than a winner by default — a report never nominates a
    /// baseline by score. A half NEITHER side measured yields no win on that half and <b>never a loss</b>:
    /// an absent measurement and a defeat are different facts, and folding one into the other is how an
    /// unrun arm acquires a verdict.
    /// </para></summary>
    public static (ProofState State, double Margin) Of(
        string arm,
        string baseline,
        HalfReading selection,
        HalfReading heldOut,
        HalfReading baselineSelection,
        HalfReading baselineHeldOut)
    {
        if (baseline.Length == 0 || arm == baseline)
        {
            return (ProofState.NotAWinner, 0);
        }

        return (
            SeedSplit.Proof(Beat(selection, baselineSelection), Beat(heldOut, baselineHeldOut)),
            Margin(heldOut, baselineHeldOut));
    }

    public static bool Beat(HalfReading arm, HalfReading baseline) =>
        arm.Measured && baseline.Measured && arm.Average > baseline.Average;

    public static double Margin(HalfReading arm, HalfReading baseline) =>
        arm.Measured && baseline.Measured ? arm.Average - baseline.Average : 0;
}
