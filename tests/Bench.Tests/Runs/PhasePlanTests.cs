using Bench.Domain.Runs;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Runs;

/// <summary>The phase model — the part the adopted evaluation library does not reach, and therefore the
/// part that has to be right here.</summary>
public sealed class PhasePlanTests
{
    private static readonly Guid Cell = Guid.CreateVersion7();

    [Fact]
    public void A_reading_task_answers_once_and_is_judged()
    {
        PhasePlan.For(TaskKind.Reading).Should().Equal(PhaseKind.Answer, PhaseKind.Judge);
    }

    [Fact]
    public void A_fix_task_investigates_fixes_verifies_and_is_judged()
    {
        PhasePlan.For(TaskKind.Fix).Should().Equal(
            PhaseKind.Investigate, PhaseKind.Fix, PhaseKind.Verify, PhaseKind.Judge);
    }

    [Fact]
    public void Materialising_numbers_the_phases_in_order_and_starts_them_all_pending()
    {
        var phases = PhasePlan.Materialise(Cell, TaskKind.Fix);

        phases.Select(p => p.Ordinal).Should().Equal(0, 1, 2, 3);
        phases.Should().OnlyContain(p => p.State == PhaseState.Pending && p.CellId == Cell);
        phases.Select(p => p.Id).Distinct().Should().HaveCount(4);
    }

    [Fact]
    public void A_fix_cannot_start_before_the_investigation_has_finished()
    {
        var phases = PhasePlan.Materialise(Cell, TaskKind.Fix);

        var refused = PhasePlan.Start(phases[1], phases);

        refused.Reason().Should().Contain("cannot start while").And.Contain("Investigate");
    }

    [Fact]
    public void The_first_phase_starts_immediately()
    {
        var phases = PhasePlan.Materialise(Cell, TaskKind.Fix);

        PhasePlan.Start(phases[0], phases).Ok().State.Should().Be(PhaseState.Running);
    }

    [Fact]
    public void A_phase_cannot_start_twice()
    {
        var phases = PhasePlan.Materialise(Cell, TaskKind.Fix);
        var running = PhasePlan.Start(phases[0], phases).Ok();

        PhasePlan.Start(running, [running, .. phases.Skip(1)]).Reason().Should().Contain("is Running, not Pending");
    }

    [Fact]
    public void Phases_run_in_order_when_each_one_completes()
    {
        var phases = Run(TaskKind.Fix, new LegOutcome.Completed(), through: 4);

        phases.Should().OnlyContain(p => p.State == PhaseState.Done);
        PhasePlan.IsFinished(phases).Should().BeTrue();
    }

    [Fact]
    public void A_ceiling_in_one_phase_stops_the_whole_leg()
    {
        var phases = Run(
            TaskKind.Fix,
            new LegOutcome.CapExceeded(BudgetKind.CostUsd, BudgetScope.Phase, 1.25m, 1.40m),
            through: 2);

        phases.Single(p => p.Kind == PhaseKind.Investigate).State.Should().Be(PhaseState.Done);
        phases.Single(p => p.Kind == PhaseKind.Fix).State.Should().Be(PhaseState.Done);
        phases.Where(p => p.Kind is PhaseKind.Verify or PhaseKind.Judge)
            .Should().OnlyContain(p => p.State == PhaseState.Stopped,
                "continuing past a budget would measure a leg that got more than the arm allowed it");
        phases.Single(p => p.Kind == PhaseKind.Verify).Detail.Should().Contain("not run: Fix ended with CapExceeded");
        PhasePlan.IsFinished(phases).Should().BeTrue("stopped is an ending too");
    }

    [Fact]
    public void A_crash_stops_the_leg_the_same_way_a_ceiling_does()
    {
        var phases = Run(TaskKind.Fix, new LegOutcome.Crashed("runtime died"), through: 1);

        phases.Single(p => p.Kind == PhaseKind.Fix).State.Should().Be(PhaseState.Stopped);
        phases.Single(p => p.Kind == PhaseKind.Judge).State.Should().Be(PhaseState.Stopped);
    }

    [Fact]
    public void An_unfinished_leg_is_not_reported_as_finished()
    {
        var phases = Run(TaskKind.Fix, new LegOutcome.Completed(), through: 2);

        PhasePlan.IsFinished(phases).Should().BeFalse();
    }

    /// <summary>Runs the first <paramref name="through"/> phases, ending the last of them with
    /// <paramref name="outcome"/> and every earlier one as completed.</summary>
    private static IReadOnlyList<LegPhase> Run(TaskKind kind, LegOutcome outcome, int through)
    {
        var phases = PhasePlan.Materialise(Cell, kind);

        for (var i = 0; i < through; i++)
        {
            var current = phases.Single(p => p.Ordinal == i);
            var started = PhasePlan.Start(current, phases).Ok();
            var ending = i == through - 1 ? outcome : new LegOutcome.Completed();

            var (ended, others) = PhasePlan.End(started, [started, .. phases.Where(p => p.Id != started.Id)], ending);
            phases = [.. others.Append(ended).OrderBy(p => p.Ordinal)];
        }

        return phases;
    }
}
