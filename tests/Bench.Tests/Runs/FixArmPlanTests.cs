using Bench.Domain.Runs;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Runs;

/// <summary>The arm axis over the phase plan (todo/PLAN_investigate_vs_implement.md §3.1): a fix task
/// runs whole, or as one of its two slices — and the slices exist so investigation quality and
/// implementation quality are separately measurable instead of one composite.</summary>
public sealed class FixArmPlanTests
{
    private static readonly Guid Cell = Guid.CreateVersion7();

    [Fact]
    public void The_full_arm_is_the_plan_the_task_kind_always_had()
    {
        PhasePlan.For(TaskKind.Fix, FixArm.Full).Ok().Should().Equal(
            PhaseKind.Investigate, PhaseKind.Fix, PhaseKind.Verify, PhaseKind.Judge);
        PhasePlan.For(TaskKind.Reading, FixArm.Full).Ok().Should().Equal(PhaseKind.Answer, PhaseKind.Judge);
    }

    [Fact]
    public void The_investigate_only_arm_ends_at_the_judged_diagnosis()
    {
        PhasePlan.For(TaskKind.Fix, FixArm.InvestigateOnly).Ok()
            .Should().Equal(PhaseKind.Investigate, PhaseKind.Judge);
    }

    [Fact]
    public void The_implement_only_arm_starts_from_a_handed_diagnosis()
    {
        PhasePlan.For(TaskKind.Fix, FixArm.ImplementOnly).Ok()
            .Should().Equal(PhaseKind.Fix, PhaseKind.Verify, PhaseKind.Judge);
    }

    [Fact]
    public void A_reading_task_refuses_a_fix_arm_by_name()
    {
        PhasePlan.For(TaskKind.Reading, FixArm.InvestigateOnly).Reason()
            .Should().Contain("Reading").And.Contain("investigate-only");
        PhasePlan.For(TaskKind.Reading, FixArm.ImplementOnly).Reason()
            .Should().Contain("Reading").And.Contain("implement-only");
    }

    [Fact]
    public void Materialising_an_arm_numbers_its_phases_from_zero()
    {
        var phases = PhasePlan.Materialise(Cell, TaskKind.Fix, FixArm.InvestigateOnly).Ok();

        phases.Select(p => p.Kind).Should().Equal(PhaseKind.Investigate, PhaseKind.Judge);
        phases.Select(p => p.Ordinal).Should().Equal(0, 1);
        phases.Should().OnlyContain(p => p.State == PhaseState.Pending && p.CellId == Cell);
    }

    [Fact]
    public void The_no_start_before_done_discipline_holds_inside_an_arm()
    {
        var phases = PhasePlan.Materialise(Cell, TaskKind.Fix, FixArm.ImplementOnly).Ok();

        PhasePlan.Start(phases[1], phases).Reason()
            .Should().Contain("cannot start while").And.Contain("Fix");
    }
}
