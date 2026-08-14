using Bench.Domain.Suites;

namespace Bench.Domain.Runs;

/// <summary>One (model, tool surface) pair — the unit whose execution order has to be balanced.</summary>
public sealed record Leg(Subject Subject, Lane Lane)
{
    public string Canonical => $"{Subject.Canonical}@{Lane.Canonical}";
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
    public static Outcome<IReadOnlyList<MatrixCell>> Plan(
        IReadOnlyList<Question> questions,
        int repeats,
        IReadOnlyList<Subject> subjects,
        IReadOnlyList<Lane> lanes)
    {
        var refusal = Validate(questions, repeats, subjects, lanes);
        if (refusal.Length > 0)
        {
            return Outcome<IReadOnlyList<MatrixCell>>.Failure(refusal);
        }

        var legs = Legs(subjects, lanes);
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

    private static IReadOnlyList<Leg> Legs(IReadOnlyList<Subject> subjects, IReadOnlyList<Lane> lanes) =>
        [.. subjects.SelectMany(s => lanes.Select(l => new Leg(s, l)))];

    private static IEnumerable<(string QuestionId, int Repeat)> Slots(IReadOnlyList<Question> questions, int repeats) =>
        questions.SelectMany(q => Enumerable.Range(0, repeats).Select(r => (q.Id, r)));

    private static IReadOnlyList<Leg> Rotated(IReadOnlyList<Leg> legs, int slotIndex)
    {
        var offset = slotIndex % legs.Count;
        return [.. legs.Skip(offset), .. legs.Take(offset)];
    }

    private static string Validate(
        IReadOnlyList<Question> questions, int repeats, IReadOnlyList<Subject> subjects, IReadOnlyList<Lane> lanes) =>
        (questions.Count, repeats, subjects.Count, lanes.Count) switch
        {
            (0, _, _, _) => "a matrix needs at least one question",
            (_, < 1, _, _) => $"repeats must be at least 1, got {repeats}",
            (_, _, 0, _) => "a matrix needs at least one subject — and an unset model id is a refusal, not a default",
            (_, _, _, 0) => "a matrix needs at least one lane",
            _ => string.Empty,
        };
}
