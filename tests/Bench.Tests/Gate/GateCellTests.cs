using Bench.Domain.Gate;
using Bench.Domain.Runs;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate;

/// <summary>The gate cell's claim/settle/sweep rules — the SAME rules as the retrieval cell's, because both
/// compose one <see cref="Claimable"/>. The proof that the extraction changed nothing is
/// <c>CellLifecycleTests</c> passing unchanged; these tests are what the gate cell adds on top: the product
/// pin taken at claim time, and an outcome vocabulary of its own.</summary>
public sealed class GateCellTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_gate_cell_follows_the_same_three_attempt_abandon_rule_as_a_run_cell()
    {
        var cell = Cell();

        for (var i = 0; i < CellLifecycle.MaxAttempts; i++)
        {
            cell = GateCellLifecycle.Reclaim(GateCellLifecycle.Claim(cell, Worker($"host-{i}"), Noon, Pin("a")).Ok());
        }

        cell.State.Should().Be(CellState.Abandoned, "the rule is Claimable's, and a gate cell composes it rather than re-reading it");
        cell.OutcomeKind.Should().Be(GateCellOutcomeKind.Failed);
        cell.OutcomeDetail.Should().Contain("will kill the next one");
        GateCellLifecycle.Reclaim(cell).Should().Be(cell, "abandoned is terminal — a sweep must not resurrect it");
    }

    [Fact]
    public void Claiming_takes_ownership_counts_the_attempt_and_records_the_pin()
    {
        var pin = Pin("a");

        var claimed = GateCellLifecycle.Claim(Cell(), Worker("lane-1"), Noon, pin).Ok();

        claimed.State.Should().Be(CellState.Claimed);
        claimed.Owner.Label.Should().Be("lane-1");
        claimed.Attempts.Should().Be(1);
        claimed.ClaimedAt.Should().Be(Noon);
        claimed.Pin.Should().Be(pin, "the pin is stored per cell AT CLAIM TIME, never read once per run");
    }

    [Fact]
    public void A_claim_without_a_pin_is_refused()
    {
        GateCellLifecycle.Claim(Cell(), Worker("lane-1"), Noon, ProductPin.None).Reason()
            .Should().Contain("product pin").And.Contain("claim time");
    }

    [Fact]
    public void A_cell_reclaimed_and_claimed_again_carries_the_pin_it_was_claimed_under_the_second_time()
    {
        var first = GateCellLifecycle.Claim(Cell(), Worker("lane-1"), Noon, Pin("a")).Ok();

        var second = GateCellLifecycle.Claim(GateCellLifecycle.Reclaim(first), Worker("lane-2"), Noon.AddHours(1), Pin("b")).Ok();

        second.Pin.Should().Be(Pin("b"), "a pending cell claims under the pin the campaign holds THEN");
        second.Attempts.Should().Be(2, "forgetting the attempt is how a poison cell loops forever");
    }

    [Fact]
    public void A_claim_without_an_owner_is_refused_by_the_shared_rule()
    {
        GateCellLifecycle.Claim(Cell(), new WorkerIdentity("lane-1", string.Empty, 0), Noon, Pin("a")).Reason()
            .Should().Contain("host and a pid");
    }

    [Fact]
    public void Only_a_claimed_cell_can_settle_and_a_settle_needs_an_outcome()
    {
        GateCellLifecycle.Settle(Cell(), GateCellOutcomeKind.Completed, string.Empty).Reason()
            .Should().Contain("is Pending, not Claimed");

        var claimed = GateCellLifecycle.Claim(Cell(), Worker("lane-1"), Noon, Pin("a")).Ok();

        GateCellLifecycle.Settle(claimed, GateCellOutcomeKind.None, string.Empty).Reason().Should().Contain("no outcome");

        var settled = GateCellLifecycle.Settle(claimed, GateCellOutcomeKind.Failed, "the coai process died on turn 2").Ok();
        settled.State.Should().Be(CellState.Settled);
        settled.OutcomeKind.Should().Be(GateCellOutcomeKind.Failed);
        settled.OutcomeDetail.Should().Contain("died on turn 2");
        settled.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void A_stranded_cell_comes_back_pending_with_its_attempt_count_intact()
    {
        var claimed = GateCellLifecycle.Claim(Cell(), Worker("dead-host"), Noon, Pin("a")).Ok();

        var reclaimed = GateCellLifecycle.Reclaim(claimed);

        reclaimed.State.Should().Be(CellState.Pending);
        reclaimed.Owner.Should().Be(WorkerIdentity.Nobody);
        reclaimed.Attempts.Should().Be(1);
        GateCellLifecycle.IsStale(claimed, Noon.AddMinutes(31), TimeSpan.FromMinutes(30)).Should().BeTrue();
        GateCellLifecycle.IsStale(reclaimed, Noon.AddDays(7), TimeSpan.FromMinutes(1)).Should().BeFalse("nobody holds it");
    }

    [Fact]
    public void A_pending_cell_carries_the_id_its_caller_minted()
    {
        var id = new Guid("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");
        var runId = new Guid("0199a1b2-c3d4-7e5f-8a9b-000000000001");

        var cell = GateCell.Pending(id, runId, Matrix());

        cell.Id.Should().Be(id, "the domain factory mints nothing — the caller owns identity, so a plan can be replayed and a test can name its cell");
        cell.RunId.Should().Be(runId);
        GateCell.Pending(id, runId, Matrix()).Should().Be(cell, "the same inputs make the same cell — a factory that reads the clock does not");
    }

    private static WorkerIdentity Worker(string label) => WorkerIdentity.Here(label);

    private static ProductPin Pin(string letter) => ProductPin.Hashed(
        new string(letter[0], 64), "0.0.0+abc1234", "abc1234", CapturedCount.Number(0), "src_mcp").Ok();

    private static GateMatrixCell Matrix() =>
        new(GateTaskId.Parse("cs2").Ok(), GateReviewerId.Parse("grok-medium").Ok(), Repeat: 1, Slot: 0, Position: 0);

    private static GateCell Cell() => GateCell.Pending(Guid.CreateVersion7(), Guid.CreateVersion7(), Matrix());
}
