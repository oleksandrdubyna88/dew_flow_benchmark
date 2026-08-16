using Bench.Domain.Runs;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Runs;

/// <summary>One deadline for a whole leg — the property the agentic loop is blocked on.
/// <para>
/// Tested here rather than through a loop that does not exist yet, and deliberately so: retrofitting a
/// leg-wide ceiling after the first long agentic campaign means discovering it from a three-day gap in a
/// log. The arithmetic below is that campaign's arithmetic — a 25-turn cap against a 10-minute wall is
/// 4 h 10 m per leg, and a breaker that fires at twenty consecutive failures needs ~3.5 days to say what
/// the first hang already said.
/// </para></summary>
public sealed class LegDeadlineTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_looping_leg_stops_at_its_wall_budget_rather_than_at_the_budget_times_its_turns()
    {
        var deadline = LegDeadline.For([Budget.Of(BudgetKind.Wall, BudgetScope.Question, 60)], Noon);
        var now = Noon;
        var handedOut = new List<decimal>();

        // The shape the tool lane will have: a turn ceiling of 25, each turn slow.
        while (handedOut.Count < 25 && !deadline.Exhausted(now))
        {
            handedOut.Add(deadline.ForCall(now).Single(b => b.Kind == BudgetKind.Wall).Limit);
            now = now.AddSeconds(20);
        }

        handedOut.Should().HaveCount(3, "60s of wall clock at 20s a turn is three turns — not 25 turns of 60s each");
        handedOut.Should().BeInDescendingOrder("every call is handed what the LEG has left, never a fresh ceiling");
        handedOut.Should().Equal(60m, 40m, 20m);
        deadline.Exhausted(now).Should().BeTrue();
    }

    [Fact]
    public void A_leg_that_names_no_wall_ceiling_is_unbounded_rather_than_instantly_capped()
    {
        var deadline = LegDeadline.For([Budget.Of(BudgetKind.Context, BudgetScope.Question, 8192)], Noon);

        deadline.IsBounded.Should().BeFalse();
        deadline.Exhausted(Noon.AddDays(3)).Should().BeFalse("a zero here would cap every leg the moment it started");
        deadline.ForCall(Noon).Should().ContainSingle().Which.Kind.Should().Be(
            BudgetKind.Context, "the other ceilings of the leg travel untouched");
    }

    [Fact]
    public void A_ceiling_of_zero_is_read_as_no_ceiling_rather_than_as_no_time()
    {
        LegDeadline.For([Budget.Of(BudgetKind.Wall, BudgetScope.Question, 0)], Noon)
            .IsBounded.Should().BeFalse("a budget of zero is how an unset flag arrives, not an instruction to cap instantly");
    }

    [Fact]
    public void The_remainder_never_goes_negative_however_far_the_leg_overran()
    {
        var deadline = LegDeadline.For([Budget.Of(BudgetKind.Wall, BudgetScope.Question, 60)], Noon);

        deadline.Remaining(Noon.AddSeconds(300)).Should().Be(
            TimeSpan.Zero, "a negative ceiling handed to a runtime is an argument exception wearing a budget");
        deadline.ForCall(Noon.AddSeconds(300)).Single().Limit.Should().Be(0m);
    }

    [Fact]
    public void A_cap_names_the_ceiling_that_stopped_the_leg_and_what_it_reached()
    {
        var deadline = LegDeadline.For([Budget.Of(BudgetKind.Wall, BudgetScope.Question, 60)], Noon);

        var cap = deadline.Cap(Noon.AddSeconds(93.4));

        cap.Should().BeOfType<LegOutcome.CapExceeded>();
        ((LegOutcome.CapExceeded)cap).Kind.Should().Be(BudgetKind.Wall);
        ((LegOutcome.CapExceeded)cap).Limit.Should().Be(60m);
        ((LegOutcome.CapExceeded)cap).Reached.Should().Be(93.4m);
        cap.CountsInPairedDelta.Should().BeFalse("pairing a capped leg against a completed one measures the ceiling");
    }
}
