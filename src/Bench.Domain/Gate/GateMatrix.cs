using Bench.Domain.Runs;

namespace Bench.Domain.Gate;

/// <summary>One planned execution: a task, a reviewer, a repeat, the slot it belongs to and the position the
/// reviewer runs in within that slot. <see cref="Slot"/> is carried so the planned ORDER is a value a test can
/// read rather than the order of a list somebody may sort.</summary>
public sealed record GateMatrixCell(GateTaskId Task, GateReviewerId Reviewer, int Repeat, int Slot, int Position);

/// <summary>Materialising a gate run as task × reviewer × repeat, with <b>repeats outermost</b> and the
/// reviewers rotated on the global slot counter.
/// <para>
/// Outermost, so the three repeats of one task are hours apart rather than back to back: a vendor's cache is
/// warmest and its rate limit tightest immediately after the same prompt, and three adjacent repeats would
/// measure that rather than the model. The rotation is <see cref="SlotRotation"/>, the same function the
/// retrieval matrix uses, so no reviewer always goes first and the 2:1 defect that function exists for cannot
/// be re-introduced here.
/// </para>
/// <para>
/// Repeats are numbered from ONE. A gate run names its repeat in the caller session id it hands the product
/// (<c>…-r1</c>), and the other harness's records are numbered the same way, so an ordinal that started at
/// zero would be off by one against every imported row.
/// </para></summary>
public static class GateMatrix
{
    public static Outcome<IReadOnlyList<GateMatrixCell>> Plan(
        IReadOnlyList<GateTaskId> tasks,
        IReadOnlyList<GateReviewerId> reviewers,
        int repeats)
    {
        var refusal = Validate(tasks, reviewers, repeats);
        if (refusal.Length > 0)
        {
            return Outcome<IReadOnlyList<GateMatrixCell>>.Failure(refusal);
        }

        var cells = Slots(tasks, repeats)
            .SelectMany((slot, slotIndex) => SlotRotation.Rotated(reviewers, slotIndex)
                .Select((reviewer, position) => new GateMatrixCell(slot.Task, reviewer, slot.Repeat, slotIndex, position)));

        return Outcome<IReadOnlyList<GateMatrixCell>>.Success([.. cells]);
    }

    /// <summary>How often each reviewer ran first. Across the whole matrix these counts differ by at most one.</summary>
    public static IReadOnlyDictionary<string, int> FirstPositionCounts(IReadOnlyList<GateMatrixCell> cells) =>
        cells.Where(c => c.Position == 0)
            .GroupBy(c => c.Reviewer.Value)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

    /// <summary>Repeats OUTERMOST: every task once, then every task again. The inner order is the tasks' own,
    /// so a stopped campaign resumes where it was and repeat <c>n</c> of a task always follows repeat <c>n-1</c>.</summary>
    private static IEnumerable<(GateTaskId Task, int Repeat)> Slots(IReadOnlyList<GateTaskId> tasks, int repeats) =>
        Enumerable.Range(1, repeats).SelectMany(repeat => tasks.Select(task => (task, repeat)));

    private static string Validate(IReadOnlyList<GateTaskId> tasks, IReadOnlyList<GateReviewerId> reviewers, int repeats) =>
        (tasks.Count, reviewers.Count, repeats) switch
        {
            (0, _, _) => "a gate matrix needs at least one task",
            (_, 0, _) => "a gate matrix needs at least one reviewer — an id is not a vendor, and inventing one is how a "
                + "bench ends up measuring a machine nobody has",
            (_, _, < 1) => $"repeats must be at least 1, got {repeats}",
            _ => Distinct(tasks, reviewers),
        };

    private static string Distinct(IReadOnlyList<GateTaskId> tasks, IReadOnlyList<GateReviewerId> reviewers)
    {
        var task = tasks.GroupBy(t => t.Value, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        var reviewer = reviewers.GroupBy(r => r.Value, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);

        return (task, reviewer) switch
        {
            (not null, _) => $"task '{task.Key}' is listed twice — a matrix over one task twice is two columns about one thing",
            (_, not null) => $"reviewer '{reviewer.Key}' is listed twice — the same row twice is one opinion sampled twice",
            _ => string.Empty,
        };
    }
}
