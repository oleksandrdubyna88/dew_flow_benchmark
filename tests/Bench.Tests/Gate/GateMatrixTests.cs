using Bench.Domain.Gate;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate;

/// <summary>The gate run's order plan: repeats OUTERMOST, reviewers rotated on the global slot counter.
/// <para>
/// The rotation is the retrieval matrix's — <c>MatrixOrderTests</c> pins the 2:1 defect it removes, and stays
/// unchanged as the proof the extraction changed nothing. What is this matrix's own is the outer loop: the
/// three repeats of one task must be hours apart, because a vendor's cache is warmest and its rate limit
/// tightest right after the same prompt, and back-to-back repeats would measure that rather than the model.
/// </para></summary>
public sealed class GateMatrixTests
{
    [Fact]
    public void Repeats_are_outermost_so_the_repeats_of_one_task_are_never_adjacent()
    {
        var cells = GateMatrix.Plan(Tasks("cs2", "rs3", "js3"), Reviewers("grok", "qwen"), repeats: 3).Ok();

        var slots = cells.GroupBy(c => c.Slot).OrderBy(g => g.Key).Select(g => g.First()).ToList();

        slots.Should().HaveCount(9);
        for (var i = 1; i < slots.Count; i++)
        {
            slots[i].Task.Should().NotBe(slots[i - 1].Task,
                $"slot {i} must not repeat the task of slot {i - 1} — a repeat is only a repeat if something else ran in between");
        }

        foreach (var task in slots.Select(s => s.Task).Distinct())
        {
            slots.Where(s => s.Task == task).Select(s => s.Repeat).Should().Equal([1, 2, 3],
                "within one task the repeats still arrive in order, so a stopped campaign resumes where it was");
        }
    }

    [Fact]
    public void First_position_is_balanced_across_the_whole_matrix_at_an_odd_repeat_count()
    {
        var cells = GateMatrix.Plan(Tasks("cs2", "rs3", "js3"), Reviewers("grok", "qwen"), repeats: 3).Ok();

        var firsts = GateMatrix.FirstPositionCounts(cells);

        firsts.Should().HaveCount(2);
        (firsts.Values.Max() - firsts.Values.Min()).Should().BeLessThanOrEqualTo(
            1, "across the whole matrix each reviewer leads equally often, give or take the odd slot");
    }

    [Fact]
    public void Every_reviewer_runs_exactly_once_per_task_and_repeat()
    {
        var cells = GateMatrix.Plan(Tasks("cs2", "rs3"), Reviewers("grok", "qwen", "glm"), repeats: 2).Ok();

        cells.Should().HaveCount(2 * 2 * 3);
        cells.GroupBy(c => (c.Task, c.Repeat))
            .Should().OnlyContain(g => g.Select(c => c.Reviewer.Value).Distinct().Count() == 3);
        cells.Select(c => c.Repeat).Distinct().Should().BeEquivalentTo([1, 2], "repeats are numbered from one, as the caller session id names them");
    }

    [Fact]
    public void Positions_within_one_slot_are_dense_and_start_at_zero()
    {
        var cells = GateMatrix.Plan(Tasks("cs2"), Reviewers("grok", "qwen", "glm"), repeats: 1).Ok();

        cells.Select(c => c.Position).Should().BeEquivalentTo([0, 1, 2]);
    }

    [Fact]
    public void A_matrix_without_reviewers_is_refused_rather_than_defaulted()
    {
        GateMatrix.Plan(Tasks("cs2"), [], repeats: 3).Reason().Should().Contain("an id is not a vendor");
    }

    [Fact]
    public void A_matrix_without_tasks_or_repeats_is_refused()
    {
        GateMatrix.Plan([], Reviewers("grok"), repeats: 3).Reason().Should().Contain("at least one task");
        GateMatrix.Plan(Tasks("cs2"), Reviewers("grok"), repeats: 0).Reason().Should().Contain("repeats must be at least 1");
    }

    [Fact]
    public void A_task_or_reviewer_listed_twice_is_refused_by_name()
    {
        GateMatrix.Plan(Tasks("cs2", "cs2"), Reviewers("grok"), repeats: 1).Reason().Should().Contain("'cs2' is listed twice");
        GateMatrix.Plan(Tasks("cs2"), Reviewers("grok", "grok"), repeats: 1).Reason().Should().Contain("'grok' is listed twice");
    }

    private static IReadOnlyList<GateTaskId> Tasks(params string[] ids) => [.. ids.Select(id => GateTaskId.Parse(id).Ok())];

    private static IReadOnlyList<GateReviewerId> Reviewers(params string[] ids) => [.. ids.Select(id => GateReviewerId.Parse(id).Ok())];
}
