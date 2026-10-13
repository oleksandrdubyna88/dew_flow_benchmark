using Bench.Domain.Gate;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>The arms, the seeded schedule, the run, and the cell's claim/settle/generation lifecycle.</summary>
public sealed class ScoutScheduleTests
{
    private static readonly ScoutTaskId[] Tasks = [Id("t01"), Id("t02"), Id("t03"), Id("t04")];

    private static readonly ScoutArmId[] Main = [.. ScoutArms.Main.Select(a => a.Id)];

    [Fact]
    public void The_arms_are_the_plans_seven_and_the_explore_replication_with_the_sonnet_team_one_and_a_half_times_the_opus_team()
    {
        ScoutArms.Main.Select(a => (a.Word, a.ModelAlias, a.Count)).Should().Equal(
            ("A1", "opus", 1), ("B1", "sonnet", 2), ("A2", "opus", 2), ("B2", "sonnet", 3), ("A3", "opus", 3), ("B3", "sonnet", 5), ("C3", "sonnet", 3));
        ScoutArms.Replication.Select(a => (a.Word, a.AgentType, a.Count)).Should().Equal(("A3-explore", "Explore", 3), ("B3-explore", "Explore", 5));
        ScoutArms.All.Where(a => a.Id.ToString().StartsWith('B')).Should().OnlyContain(b =>
            b.Count == (int)Math.Ceiling(1.5 * ScoutArms.All.Single(a => a.Workload == b.Workload && a.Model == ScoutModel.Opus && a.Agent == b.Agent).Count));
        ScoutArms.Of(ScoutArmId.B3).Focuses.Should().Equal(Enum.GetValues<ScoutFocus>());
        ScoutArms.Parse("c3").Ok().Id.Should().Be(ScoutArmId.C3);
        ScoutArms.Parse("D1").Reason().Should().Contain("A1, B1, A2, B2, A3, B3, C3, A3-explore, B3-explore");
    }

    [Fact]
    public void Every_task_runs_every_main_arm_once_per_repeat_and_the_arms_are_interleaved_inside_each_task()
    {
        var cells = ScoutSchedule.Plan(Tasks, Main, [], repeats: 3, seed: 7).Ok();

        cells.Should().HaveCount(4 * 7 * 3);
        cells.GroupBy(c => (c.Task.Value, c.Arm)).Should().OnlyContain(g => g.Select(c => c.Repeat).Order().SequenceEqual(new[] { 1, 2, 3 }));
        cells.GroupBy(c => c.Slot).Should().OnlyContain(block => block.Select(c => c.Task.Value).Distinct().Count() == 1 && block.Count() == 7,
            "a slot is one task in one repeat, holding all seven arms back to back");
        cells.Select(c => c.Repeat).Should().BeInAscendingOrder("repeats are outermost");
        cells.GroupBy(c => c.Slot).Select(b => string.Join(",", b.OrderBy(c => c.Position).Select(c => c.Arm))).Distinct().Count()
            .Should().BeGreaterThan(1, "each block draws its own permutation — a fixed arm order would put one arm first in every task");
    }

    [Fact]
    public void The_schedule_is_reproducible_from_its_seed_and_another_seed_gives_another_order()
    {
        var once = ScoutSchedule.Plan(Tasks, Main, [], 2, seed: 7).Ok();

        ScoutSchedule.Plan(Tasks, Main, [], 2, seed: 7).Ok().Should().Equal(once);
        ScoutSchedule.Plan(Tasks, Main, [], 2, seed: 8).Ok().Should().NotEqual(once);
    }

    [Fact]
    public void The_explore_replication_runs_only_on_the_replication_tasks()
    {
        var cells = ScoutSchedule.Plan(Tasks, [.. Main, ScoutArmId.A3Explore, ScoutArmId.B3Explore], [Tasks[0]], 1, seed: 1).Ok();

        cells.Where(c => ScoutArms.Of(c.Arm).IsReplication).Select(c => c.Task.Value).Distinct().Should().Equal("t01");
        cells.Count(c => c.Task.Value == "t01").Should().Be(9);
        cells.Count(c => c.Task.Value == "t02").Should().Be(7);
    }

    [Fact]
    public void A_schedule_is_refused_naming_what_is_wrong()
    {
        ScoutSchedule.Plan([], Main, [], 1, 1).Reason().Should().Contain("at least one task");
        ScoutSchedule.Plan([Tasks[0], Tasks[0]], Main, [], 1, 1).Reason().Should().Contain("each named once");
        ScoutSchedule.Plan(Tasks, [ScoutArmId.A1, ScoutArmId.A1], [], 1, 1).Reason().Should().Contain("arm");
        ScoutSchedule.Plan(Tasks, Main, [], 0, 1).Reason().Should().Contain("repeats");
        ScoutSchedule.Plan(Tasks, [.. Main, ScoutArmId.A3Explore], [], 1, 1).Reason().Should().Contain("no replication task");
        ScoutSchedule.Plan(Tasks, Main, [Id("zz")], 1, 1).Reason().Should().Contain("not one of the run's tasks");
    }

    [Fact]
    public void A_pin_that_moved_mid_run_is_refused_naming_it_and_both_values()
    {
        var frozen = Pins();

        ScoutPins.Continue(frozen, Pins()).Ok().Should().Be(frozen);
        ScoutPins.Continue(frozen, Pins(sonnet: "claude-sonnet-5-6")).Reason().Should().Contain("resolved sonnet id claude-sonnet-5-5 → claude-sonnet-5-6");
        ScoutPins.Of("2.1.289", " ", "a", "b", "c").Reason().Should().Contain("plugin cache hash");
    }

    [Fact]
    public void A_run_needs_a_positive_ceiling_and_starts_with_an_empty_ledger()
    {
        var run = ScoutRun.Planned(Guid.CreateVersion7(), DateTimeOffset.UnixEpoch, Pins(), 7, 3, Main, Tasks, 250m).Ok();

        run.Ledger.Should().Be(SpendLedger.Empty(250m));
        ScoutRun.Planned(Guid.CreateVersion7(), DateTimeOffset.UnixEpoch, Pins(), 7, 3, Main, Tasks, 0m).Reason().Should().Contain("ceiling");
        ScoutRun.Planned(Guid.CreateVersion7(), DateTimeOffset.UnixEpoch, Pins(), 7, 3, Main, [Tasks[0], Tasks[0]], 1m).Reason().Should().Contain("task");
    }

    [Fact]
    public void A_cell_is_claimed_only_under_a_pin_and_its_outcome_and_reason_must_agree_when_it_settles()
    {
        var cell = ScoutCell.Pending(Guid.CreateVersion7(), Guid.CreateVersion7(), new ScheduledCell(Tasks[0], ScoutArmId.B1, 1, 0, 0));
        var owner = WorkerIdentity.Here("scouts");

        ScoutCellLifecycle.Claim(cell, owner, DateTimeOffset.UnixEpoch, ProductPin.None).Reason().Should().Contain("product pin");
        var claimed = ScoutCellLifecycle.Claim(cell, owner, DateTimeOffset.UnixEpoch, Pin()).Ok();

        ScoutCellLifecycle.Settle(claimed, Settlement(ScoutCellOutcome.Void, ScoutReason.None)).Reason().Should().Contain("cannot carry the reason None");
        ScoutCellLifecycle.Settle(claimed, Settlement(ScoutCellOutcome.Scored, ScoutReason.ChildModelMismatch)).Failed().Should().BeTrue();
        ScoutCellLifecycle.Settle(claimed, Settlement(ScoutCellOutcome.Stopped, ScoutReason.ChildModelMismatch)).Failed().Should().BeTrue("a void reason is not a stop");
        ScoutCellLifecycle.Settle(claimed, Settlement(ScoutCellOutcome.Aborted, ScoutReason.SpendLimit)).Ok().State.Should().Be(CellState.Settled);
        ScoutCellLifecycle.Settle(claimed, Settlement(ScoutCellOutcome.Void, ScoutReason.WrongCallCount)).Ok().Reason.Should().Be(ScoutReason.WrongCallCount);
    }

    [Fact]
    public void A_void_cell_is_rerun_as_the_next_generation_and_a_lineage_still_open_refuses_one()
    {
        var cell = ScoutCell.Pending(Guid.CreateVersion7(), Guid.CreateVersion7(), new ScheduledCell(Tasks[0], ScoutArmId.B1, 1, 4, 2));
        var owner = WorkerIdentity.Here("scouts");

        ScoutCellLifecycle.NextGeneration([cell], Guid.CreateVersion7()).Reason().Should().Contain("still being measured");

        var voided = ScoutCellLifecycle.Settle(ScoutCellLifecycle.Claim(cell, owner, DateTimeOffset.UnixEpoch, Pin()).Ok(), Settlement(ScoutCellOutcome.Void, ScoutReason.WrongCallCount)).Ok();
        var next = ScoutCellLifecycle.NextGeneration([voided], Guid.CreateVersion7()).Ok();

        (next.Generation, next.State, next.Slot, next.Position, next.Outcome, next.Reason).Should().Be((2, CellState.Pending, 4, 2, ScoutCellOutcome.None, ScoutReason.None));
        ScoutGenerations.LatestSettled([voided, next]).Should().Equal(voided);
    }

    internal static ScoutPins Pins(string sonnet = "claude-sonnet-5-5") => ScoutPins.Of("2.1.289", "f359d43b4060", "claude-opus-5-5", sonnet, new string('9', 64)).Ok();

    internal static ProductPin Pin() => ProductPin.Hashed(new string('a', 64), "2.1.289 (Claude Code)", string.Empty, CapturedCount.Unavailable("no checkout"), string.Empty).Ok();

    internal static ScoutSettlement Settlement(ScoutCellOutcome outcome, ScoutReason reason) => new(outcome, reason, ScoutCellFacts.None);

    private static ScoutTaskId Id(string value) => ScoutTaskId.Parse(value).Ok();
}
