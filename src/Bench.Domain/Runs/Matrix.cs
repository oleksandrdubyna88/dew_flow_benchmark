using Bench.Domain.Suites;
using Bench.Domain.Variants;

namespace Bench.Domain.Runs;

/// <summary>One (model, tool surface, retrieval variant) triple — the unit whose execution order has to
/// be balanced.</summary>
public sealed record Leg(Subject Subject, Lane Lane, VariantSelection Variant)
{
    /// <summary>A leg with no variant — a run planned without the catalog.</summary>
    public static Leg Of(Subject subject, Lane lane) => new(subject, lane, VariantSelection.None);

    /// <summary>Which slice of a fix task this leg runs. An <c>init</c> member defaulting to
    /// <see cref="FixArm.Full"/> rather than a fourth positional, for the same reason the variant is a
    /// distinct not-applicable state: the axis is additive, every caller planning without it keeps
    /// compiling, and every leg it plans keeps meaning what it meant.</summary>
    public FixArm Arm { get; init; } = FixArm.Full;

    /// <summary>The variant is appended only when there is one, and the ARM only when it is not the
    /// whole task, so a run planned before either axis existed stores the identity it always stored.
    /// The axes are additive: they must not silently rewrite what earlier results are keyed by.</summary>
    public string Canonical =>
        Identity + (Arm == FixArm.Full ? string.Empty : $"!{Arm.Canonical()}");

    private string Identity => Variant switch
    {
        VariantSelection.Selected selected => $"{Subject.Canonical}@{Lane.Canonical}#{selected.Name}",
        _ => $"{Subject.Canonical}@{Lane.Canonical}",
    };
}

/// <summary>One planned execution: a question, a repeat, a leg, and the position that leg runs in.</summary>
public sealed record MatrixCell(string QuestionId, int Repeat, Leg Leg, int Position);

/// <summary>Materialising a run as the cross product of question × repeat × subject × lane, with the
/// execution order balanced ACROSS THE WHOLE MATRIX.
/// <para>
/// The "whole matrix" part is the entire subtlety, and it was learned the expensive way. Rotating by
/// the repeat index alone — <c>repeatIndex % legCount</c> — looks balanced and is not: at an odd
/// repeat count it deals 2:1, identically for every question, so the bias never averages out and the
/// leg that runs first systematically enjoys a warmer cache and a fresher context. Counting slots
/// globally instead costs one integer and removes the bias entirely.
/// </para></summary>
public static class Matrix
{
    /// <summary>A matrix over one implicit configuration — every leg carries
    /// <see cref="VariantSelection.None"/>. Kept so a run planned before the catalog existed plans
    /// identically today.</summary>
    public static Outcome<IReadOnlyList<MatrixCell>> Plan(
        IReadOnlyList<Question> questions,
        int repeats,
        IReadOnlyList<Subject> subjects,
        IReadOnlyList<Lane> lanes) =>
        Plan(questions, repeats, subjects, lanes, [VariantSelection.None]);

    /// <summary>A matrix over whole tasks only — every leg runs <see cref="FixArm.Full"/>. Kept so a run
    /// planned before the arm axis existed plans identically today.</summary>
    public static Outcome<IReadOnlyList<MatrixCell>> Plan(
        IReadOnlyList<Question> questions,
        int repeats,
        IReadOnlyList<Subject> subjects,
        IReadOnlyList<Lane> lanes,
        IReadOnlyList<VariantSelection> variants) =>
        Plan(questions, repeats, subjects, lanes, variants, [FixArm.Full]);

    /// <summary>The full matrix: question × repeat × subject × lane × variant × <b>arm</b>. Each is an
    /// axis rather than one value fixed for a whole run, which is what lets a finished test grow when a
    /// catalog does — and what lets one fix task be measured whole and as its two slices in one run.</summary>
    public static Outcome<IReadOnlyList<MatrixCell>> Plan(
        IReadOnlyList<Question> questions,
        int repeats,
        IReadOnlyList<Subject> subjects,
        IReadOnlyList<Lane> lanes,
        IReadOnlyList<VariantSelection> variants,
        IReadOnlyList<FixArm> arms)
    {
        var refusal = Validate(questions, repeats, subjects, lanes, variants, arms);
        if (refusal.Length > 0)
        {
            return Outcome<IReadOnlyList<MatrixCell>>.Failure(refusal);
        }

        var legs = Legs(subjects, lanes, variants, arms);
        var cells = Slots(questions, repeats)
            .SelectMany((slot, slotIndex) => Rotated(legs, slotIndex)
                .Select((leg, position) => new MatrixCell(slot.QuestionId, slot.Repeat, leg, position)));

        return Outcome<IReadOnlyList<MatrixCell>>.Success([.. cells]);
    }

    /// <summary>How often each leg ran first. The balance guarantee, in a form a test can assert and a
    /// report can print: across the whole matrix these counts differ by at most one.</summary>
    public static IReadOnlyDictionary<string, int> FirstPositionCounts(IReadOnlyList<MatrixCell> cells) =>
        cells.Where(c => c.Position == 0)
            .GroupBy(c => c.Leg.Canonical)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

    private static IReadOnlyList<Leg> Legs(
        IReadOnlyList<Subject> subjects,
        IReadOnlyList<Lane> lanes,
        IReadOnlyList<VariantSelection> variants,
        IReadOnlyList<FixArm> arms) =>
        [.. subjects.SelectMany(s => lanes.SelectMany(l => variants.SelectMany(v =>
            arms.Select(a => new Leg(s, l, v) { Arm = a }))))];

    private static IEnumerable<(string QuestionId, int Repeat)> Slots(IReadOnlyList<Question> questions, int repeats) =>
        questions.SelectMany(q => Enumerable.Range(0, repeats).Select(r => (q.Id, r)));

    private static IReadOnlyList<Leg> Rotated(IReadOnlyList<Leg> legs, int slotIndex)
    {
        var offset = slotIndex % legs.Count;
        return [.. legs.Skip(offset), .. legs.Take(offset)];
    }

    private static string Validate(
        IReadOnlyList<Question> questions,
        int repeats,
        IReadOnlyList<Subject> subjects,
        IReadOnlyList<Lane> lanes,
        IReadOnlyList<VariantSelection> variants,
        IReadOnlyList<FixArm> arms) =>
        (questions.Count, repeats, subjects.Count, lanes.Count, variants.Count, arms.Count) switch
        {
            (0, _, _, _, _, _) => "a matrix needs at least one question",
            (_, < 1, _, _, _, _) => $"repeats must be at least 1, got {repeats}",
            (_, _, 0, _, _, _) => "a matrix needs at least one subject — and an unset model id is a refusal, not a default",
            (_, _, _, 0, _, _) => "a matrix needs at least one lane",
            (_, _, _, _, 0, _) => "a matrix needs at least one variant — pass the not-applicable selection for a run "
                + "planned without the catalog, so that 'no variant' is stated rather than assumed",
            (_, _, _, _, _, 0) => "a matrix needs at least one arm — pass Full for a run planned before the arm "
                + "axis existed, so that 'the whole task' is stated rather than assumed",
            _ => string.Empty,
        };
}
