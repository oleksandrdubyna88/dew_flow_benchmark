using Bench.Domain.Runs;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Runs;

/// <summary>The claim/settle/sweep rules, tested without a database — because they are rules, and a rule
/// that can only be checked by standing up Postgres is a rule nobody checks.</summary>
public sealed class CellLifecycleTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Claiming_a_pending_cell_takes_ownership_and_counts_the_attempt()
    {
        var claimed = CellLifecycle.Claim(Cell(), Worker("worker-1"), Noon).Ok();

        claimed.State.Should().Be(CellState.Claimed);
        claimed.Owner.Label.Should().Be("worker-1");
        claimed.Owner.IsTraceable.Should().BeTrue("a claim the sweep cannot ask about is one it has to guess about");
        claimed.ClaimedAt.Should().Be(Noon);
        claimed.Attempts.Should().Be(1, "the attempt is counted on the way IN, so a host that dies mid-cell still spent one");
    }

    [Fact]
    public void A_claim_without_an_owner_is_refused()
    {
        CellLifecycle.Claim(Cell(), Worker("  "), Noon).Reason()
            .Should().Contain("needs an owner").And.Contain("swept");
    }

    [Fact]
    public void A_claim_that_records_no_host_or_pid_is_refused()
    {
        CellLifecycle.Claim(Cell(), new WorkerIdentity("worker-1", string.Empty, 0), Noon).Reason()
            .Should().Contain("host and a pid",
                "a pid without its machine is what let a sweep on host B judge a process on host A");
    }

    [Fact]
    public void A_cell_cannot_be_claimed_twice()
    {
        var claimed = CellLifecycle.Claim(Cell(), Worker("worker-1"), Noon).Ok();

        CellLifecycle.Claim(claimed, Worker("worker-2"), Noon).Reason().Should().Contain("is Claimed, not Pending");
    }

    [Fact]
    public void Only_a_claimed_cell_can_settle()
    {
        CellLifecycle.Settle(Cell(), new LegOutcome.Completed()).Reason()
            .Should().Contain("is Pending, not Claimed");
    }

    [Fact]
    public void Settling_records_the_kind_and_a_readable_detail()
    {
        var claimed = CellLifecycle.Claim(Cell(), Worker("worker-1"), Noon).Ok();

        var settled = CellLifecycle.Settle(
            claimed, new LegOutcome.CapExceeded(BudgetKind.CostUsd, BudgetScope.Question, 1.25m, 1.31m)).Ok();

        settled.State.Should().Be(CellState.Settled);
        settled.OutcomeKind.Should().Be(LegOutcomeKind.CapExceeded, "a ceiling is not a crash and not a wrong answer");
        settled.OutcomeDetail.Should().Contain("CostUsd/Question").And.Contain("1.31");
    }

    [Fact]
    public void A_stranded_cell_comes_back_with_its_attempt_count_intact()
    {
        var claimed = CellLifecycle.Claim(Cell(), Worker("dead-host"), Noon).Ok();

        var reclaimed = CellLifecycle.Reclaim(claimed);

        reclaimed.State.Should().Be(CellState.Pending);
        reclaimed.Owner.Should().Be(WorkerIdentity.Nobody);
        reclaimed.Attempts.Should().Be(1, "forgetting the attempt is how a poison cell loops forever");
    }

    [Fact]
    public void A_cell_that_has_used_up_its_attempts_is_abandoned_rather_than_requeued()
    {
        var cell = Cell();

        for (var i = 0; i < CellLifecycle.MaxAttempts; i++)
        {
            cell = CellLifecycle.Reclaim(CellLifecycle.Claim(cell, Worker($"host-{i}"), Noon).Ok());
        }

        cell.State.Should().Be(CellState.Abandoned);
        cell.OutcomeDetail.Should().Contain("will kill the next one");
        CellLifecycle.Reclaim(cell).Should().Be(cell, "abandoned is terminal — a sweep must not resurrect it");
    }

    [Fact]
    public void A_settled_cell_is_left_alone_by_the_sweep()
    {
        var settled = CellLifecycle.Settle(CellLifecycle.Claim(Cell(), Worker("w"), Noon).Ok(), new LegOutcome.Completed()).Ok();

        CellLifecycle.Reclaim(settled).Should().Be(settled);
    }

    [Theory]
    [InlineData(29, false)]
    [InlineData(30, true)]
    [InlineData(120, true)]
    public void Staleness_is_measured_from_when_the_claim_was_taken(int minutesLater, bool expected)
    {
        var claimed = CellLifecycle.Claim(Cell(), Worker("w"), Noon).Ok();

        CellLifecycle.IsStale(claimed, Noon.AddMinutes(minutesLater), TimeSpan.FromMinutes(30))
            .Should().Be(expected);
    }

    [Fact]
    public void A_pending_cell_is_never_stale_however_long_it_waits()
    {
        CellLifecycle.IsStale(Cell(), Noon.AddDays(7), TimeSpan.FromMinutes(1))
            .Should().BeFalse("nobody is holding it, so there is nothing to take back");
    }

    private static WorkerIdentity Worker(string label) => WorkerIdentity.Here(label);

    private static RunCell Cell() => RunCell.Pending(
        Guid.CreateVersion7(),
        new MatrixCell("q1", 0, Leg.Of(
            new Subject(ModelRef.Parse("m", ModelHosting.Local).Ok(), Sampling.Deterministic(1)),
            Lane.Named("native")), 0));
}
