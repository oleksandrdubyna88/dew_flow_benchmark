using FluentAssertions;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>Model arms E2.S8 (E2 §8 item 59) — the one number a gate waits for the lock for: a TOTAL time, sized so a queue of healthy holders never passes it. Each arm of the round that can queue ahead holds the lock for the gate step's wall plus the grace
/// it is killed after plus the usage-limit waits the runner may spend under the lock; the ceiling is at least that for every other arm of the experiment, never shorter than one such span, never longer than a timer can carry.
/// <para>
/// What this does NOT prove: that the lock is held for exactly the wall (a gate that is resumed after a blocked round takes the lock again); the heartbeat the plan names as the other answer — not built.
/// </para></summary>
public sealed class GateLockCeilingTests
{
    private static readonly TimeSpan Maximum = TimeSpan.FromDays(49);

    private static RunSpec Run(string name, params string[][] rounds) => new(name, rounds, [], []);

    [Theory]
    [InlineData(4, 150, 600, 0, 0, 28800)]
    [InlineData(2, 150, 600, 0, 0, 9600)]
    [InlineData(1, 150, 600, 0, 0, 9600)]
    [InlineData(0, 150, 600, 0, 0, 9600)]
    [InlineData(3, 1, 600, 0, 0, 1320)]
    [InlineData(3, 1, 600, 2, 30, 1440)]
    [InlineData(3, 150, 600, 40, 900, 91200)]
    public void The_ceiling_is_one_holders_wall_grace_and_limit_waits_for_every_other_arm_of_the_queue_and_at_least_one(
        int queue, int gateMinutes, int killGraceSeconds, int limitTries, int limitWaitSeconds, int expectedSeconds)
    {
        GateLockCeiling.For(queue, gateMinutes, killGraceSeconds, limitTries, limitWaitSeconds, Maximum).Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void A_ceiling_is_never_zero_and_never_past_what_a_timer_can_carry()
    {
        GateLockCeiling.For(5, 0, 0, 0, 0, Maximum).Should().Be(TimeSpan.FromMinutes(1), "a gate with no wall and no grace still waits a minute, not for ever and not for nothing");
        GateLockCeiling.For(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, Maximum).Should().Be(Maximum, "a huge experiment is clamped, in long arithmetic, to the ceiling's maximum");
    }

    [Fact]
    public void The_queue_is_the_largest_round_of_the_run_that_names_the_arm_and_not_every_arm_ever_made()
    {
        var runs = new[] { Run("part6", ["C1", "CR1", "SRS1"], ["C2", "CR2"]), Run("part7", ["CHH1", "CHH2"]) };

        GateLockCeiling.QueueOf(runs, "C1", 12).Should().Be(new GateQueue(3, FromRound: true), "C1 is in part6, whose largest round holds three arms — the twelve of the map are not queued behind it");
        GateLockCeiling.QueueOf(runs, "CHH2", 12).Should().Be(new GateQueue(2, FromRound: true));
    }

    [Fact]
    public void An_arm_in_no_run_falls_back_to_the_arms_of_the_map_and_says_so()
    {
        GateLockCeiling.QueueOf([Run("part6", ["C1", "CR1"])], "C4", 5).Should().Be(new GateQueue(5, FromRound: false));
        GateLockCeiling.QueueOf([], "C1", 2).Should().Be(new GateQueue(2, FromRound: false));
    }
}
