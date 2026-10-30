using FluentAssertions;
using ModelArms.Core.Steps;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>Model arms E2.S8 (E2 §8 item 59) — the one number a gate waits for the lock for: a TOTAL time, sized so a healthy queue never passes it. Each arm that can queue ahead holds the lock for the gate step's wall plus the grace
/// it is killed after; the ceiling is at least that for every other arm of the experiment, never shorter than one such span, never longer than a timer can carry.
/// <para>
/// What this does NOT prove: that the lock is held for exactly the wall (a gate that is resumed after a blocked round takes the lock again); the heartbeat the plan names as the other answer — not built.
/// </para></summary>
public sealed class GateLockCeilingTests
{
    private static readonly TimeSpan Maximum = TimeSpan.FromDays(49);

    [Theory]
    [InlineData(4, 150, 600, 28800)]
    [InlineData(2, 150, 600, 9600)]
    [InlineData(1, 150, 600, 9600)]
    [InlineData(0, 150, 600, 9600)]
    [InlineData(3, 1, 600, 1320)]
    public void The_ceiling_is_one_gate_span_for_every_other_arm_and_at_least_one(int arms, int gateMinutes, int killGraceSeconds, int expectedSeconds)
    {
        GateLockCeiling.For(arms, gateMinutes, killGraceSeconds, Maximum).Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void A_ceiling_is_never_zero_and_never_past_what_a_timer_can_carry()
    {
        GateLockCeiling.For(5, 0, 0, Maximum).Should().Be(TimeSpan.FromMinutes(1), "a gate with no wall and no grace still waits a minute, not for ever and not for nothing");
        GateLockCeiling.For(int.MaxValue, int.MaxValue, int.MaxValue, Maximum).Should().Be(Maximum, "a huge experiment is clamped, in long arithmetic, to the ceiling's maximum");
    }
}
