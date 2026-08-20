using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Domain.Variants;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Runs;

/// <summary>The arm axis in the matrix (todo/PLAN_investigate_vs_implement.md §3.1): the same fix task
/// runs as investigate-only, implement-only or full, and the axis is ADDITIVE — a leg planned before
/// it existed keeps the identity it always stored, exactly as the variant axis promised one axis over
/// (<see cref="MatrixVariantAxisTests"/>).</summary>
public sealed class MatrixArmAxisTests
{
    private static readonly CommitSha Commit = CommitSha.Parse(new string('c', 40)).Ok();

    private static readonly IReadOnlyList<FixArm> AllArms =
        [FixArm.Full, FixArm.InvestigateOnly, FixArm.ImplementOnly];

    [Fact]
    public void Every_arm_runs_once_per_question_repeat_subject_lane_and_variant()
    {
        var cells = Matrix.Plan(
            Questions(2), repeats: 2, Subjects("m1"), Lanes("a"), Variants("v1"), AllArms).Ok();

        cells.Should().HaveCount(2 * 2 * 1 * 1 * 1 * 3);
        cells.GroupBy(c => (c.QuestionId, c.Repeat))
            .Should().OnlyContain(g => g.Select(c => c.Leg.Canonical).Distinct().Count() == 3);
    }

    [Fact]
    public void First_position_stays_balanced_across_the_whole_matrix_with_an_arm_axis()
    {
        var cells = Matrix.Plan(
            Questions(5), repeats: 3, Subjects("m1", "m2"), Lanes("a"), Variants("v1"),
            [FixArm.Full, FixArm.InvestigateOnly]).Ok();

        var firsts = Matrix.FirstPositionCounts(cells);

        firsts.Should().HaveCount(4, "two subjects by two arms is four legs");
        (firsts.Values.Max() - firsts.Values.Min()).Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public void A_matrix_without_arms_is_refused_rather_than_silently_running_the_whole_task()
    {
        Matrix.Plan(Questions(1), repeats: 1, Subjects("m1"), Lanes("a"), Variants("v1"), [])
            .Reason().Should().Contain("arm");
    }

    [Fact]
    public void A_leg_names_its_arm_so_two_slices_never_share_an_identity()
    {
        var cells = Matrix.Plan(
            Questions(1), repeats: 1, Subjects("m1"), Lanes("a"), Variants("v1"),
            [FixArm.InvestigateOnly, FixArm.ImplementOnly]).Ok();

        cells.Select(c => c.Leg.Canonical).Distinct().Should().HaveCount(2);
        cells.Select(c => c.Leg.Canonical)
            .Should().AllSatisfy(c => c.Should().ContainAny("!investigate-only", "!implement-only"));
    }

    [Fact]
    public void A_full_leg_keeps_the_canonical_form_it_had_before_the_axis_existed()
    {
        Matrix.Plan(Questions(1), repeats: 1, Subjects("m1"), Lanes("a")).Ok()
            .Single().Leg.Canonical.Should().Be(
                "m1|t=0,s=1@a",
                "the axis is additive — a run planned before it existed stores the identity it always stored");

        Matrix.Plan(Questions(1), repeats: 1, Subjects("m1"), Lanes("a"), Variants("v1"), [FixArm.Full]).Ok()
            .Single().Leg.Canonical.Should().Be("m1|t=0,s=1@a#v1", "the whole task appends no arm token");
    }

    [Fact]
    public void A_cell_carries_its_arm_beside_the_canonical_leg_rather_than_inside_it()
    {
        var cells = Matrix.Plan(
            Questions(1), repeats: 1, Subjects("m1"), Lanes("a"), Variants("v1"),
            [FixArm.InvestigateOnly]).Ok();

        var cell = RunCell.Pending(Guid.CreateVersion7(), cells.Single());

        cell.Arm.Should().Be(FixArm.InvestigateOnly);
    }

    [Fact]
    public void A_cell_planned_without_the_axis_reads_as_the_whole_task()
    {
        var cells = Matrix.Plan(Questions(1), repeats: 1, Subjects("m1"), Lanes("a")).Ok();

        RunCell.Pending(Guid.CreateVersion7(), cells.Single()).Arm.Should().Be(
            FixArm.Full, "every cell that existed before this axis ran the whole task, which is what it was");
    }

    private static IReadOnlyList<Question> Questions(int count) =>
        [.. Enumerable.Range(1, count).Select(i =>
            new Question($"q{i}", $"prompt {i}", [Expectation.File(SourceAnchor.File($"src/F{i}.cs", Commit))], string.Empty))];

    private static IReadOnlyList<Subject> Subjects(params string[] ids) =>
        [.. ids.Select(id => new Subject(ModelRef.Parse(id, ModelHosting.Local).Ok(), Sampling.Deterministic(1)))];

    private static IReadOnlyList<Lane> Lanes(params string[] names) => [.. names.Select(Lane.Named)];

    private static IReadOnlyList<VariantSelection> Variants(params string[] names) =>
        [.. names.Select(n => VariantSelection.Of(Guid.CreateVersion7(), n))];
}
