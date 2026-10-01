using Bench.Domain.Gate;
using Bench.Domain.Probes;
using Bench.Domain.Runs;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Probes;

/// <summary>The probe cell's claim/settle/sweep rules — the SAME rules as the gate's and the retrieval cell's, because all
/// three compose one <see cref="Claimable"/>. What these tests pin is what the probe cell adds: the pin at claim time, the
/// unmeasured hand-back that never abandons, and the GENERATION a re-run appends (D2).</summary>
public sealed class ProbeCellTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_probe_cell_follows_the_same_three_attempt_abandon_rule_as_the_other_cells()
    {
        var cell = Cell();

        for (var i = 0; i < Claimable.MaxAttempts; i++)
        {
            cell = ProbeCellLifecycle.Reclaim(ProbeCellLifecycle.Claim(cell, Worker($"host-{i}"), Noon, Pin('a')).Ok());
        }

        cell.State.Should().Be(CellState.Abandoned, "the rule is Claimable's, composed rather than re-read");
        cell.Reason.Should().Be(ProbeReason.Abandoned);
        ProbeCellLifecycle.Reclaim(cell).Should().Be(cell, "abandoned is terminal");
    }

    [Fact]
    public void Claiming_records_the_pin_and_is_refused_without_one()
    {
        var claimed = ProbeCellLifecycle.Claim(Cell(), Worker("lane-1"), Noon, Pin('a')).Ok();

        claimed.State.Should().Be(CellState.Claimed);
        claimed.Attempts.Should().Be(1);
        claimed.Pin.Should().Be(Pin('a'), "the answers are facts about a CLI BUILD — the pin is stored per cell at claim time (D9)");

        ProbeCellLifecycle.Claim(Cell(), Worker("lane-1"), Noon, ProductPin.None).Reason().Should().Contain("product pin").And.Contain("claim time");
    }

    [Fact]
    public void Only_a_claimed_cell_settles_and_a_settle_records_facts_and_artefacts()
    {
        var settlement = new ProbeSettlement(Facts(ProbeFact.Yes), [Artifact()]);

        ProbeCellLifecycle.Settle(Cell(), settlement).Reason().Should().Contain("is Pending, not Claimed");

        var settled = ProbeCellLifecycle.Settle(ProbeCellLifecycle.Claim(Cell(), Worker("lane-1"), Noon, Pin('a')).Ok(), settlement).Ok();

        settled.State.Should().Be(CellState.Settled);
        settled.IsTerminal.Should().BeTrue();
        settled.Facts.Should().Be(settlement.Facts);
        settled.Artifacts.Should().Equal(settlement.Artifacts);
        settled.Reason.Should().Be(ProbeReason.None);
    }

    [Fact]
    public void An_unmeasured_attempt_is_never_settled()
    {
        var claimed = ProbeCellLifecycle.Claim(Cell(), Worker("lane-1"), Noon, Pin('a')).Ok();

        ProbeCellLifecycle.Settle(claimed, new ProbeSettlement(ProbeFacts.NothingCaptured(ProbeAttemptKind.Unmeasured, CapturedCount.Number(0)), [])).Reason()
            .Should().Contain("handed back, never settled", "a quota stop is not a measurement (D8)");
        ProbeCellLifecycle.Settle(claimed, new ProbeSettlement(ProbeFacts.None, [])).Reason().Should().Contain("no attempt kind");
    }

    /// <summary>S2c, review finding 4: the sweep decides on MEASURED attempts — a cell handed back unmeasured three times is still on
    /// its first measured attempt when a crash strands it, and is requeued, not abandoned.</summary>
    [Fact]
    public void Unmeasured_hand_backs_never_count_toward_the_abandonment()
    {
        var cell = Cell();

        for (var i = 0; i < Claimable.MaxAttempts; i++)
        {
            cell = ProbeCellLifecycle.HandBackUnmeasured(ProbeCellLifecycle.Claim(cell, Worker("lane-1"), Noon, Pin('a')).Ok(), ProbeReason.AccountOut).Ok();
        }

        var crashed = ProbeCellLifecycle.Reclaim(ProbeCellLifecycle.Claim(cell, Worker("crashed"), Noon, Pin('a')).Ok());

        (crashed.Attempts, crashed.UnmeasuredAttempts, crashed.MeasuredAttempts).Should().Be((4, 3, 1));
        crashed.State.Should().Be(CellState.Pending, "the first MEASURED attempt died; the rule counts measured attempts, so the cell gets its second");
        crashed.Reason.Should().Be(ProbeReason.None);
    }

    [Fact]
    public void A_hand_back_keeps_the_attempt_counted_marks_it_unmeasured_and_never_abandons()
    {
        var cell = Cell();

        for (var attempt = 1; attempt <= Claimable.MaxAttempts + 1; attempt++)
        {
            cell = ProbeCellLifecycle.HandBackUnmeasured(ProbeCellLifecycle.Claim(cell, Worker("lane-1"), Noon, Pin('a')).Ok(), ProbeReason.AccountOut).Ok();
            cell.State.Should().Be(CellState.Pending, $"attempt {attempt}: an empty account is not the cell's fault");
            cell.Attempts.Should().Be(attempt, "the attempt ran and has a directory; it stays counted so the next claim takes a fresh one");
        }

        cell.Owner.Should().Be(WorkerIdentity.Nobody);
        cell.Facts.Kind.Should().Be(ProbeAttemptKind.Unmeasured);
        cell.Reason.Should().Be(ProbeReason.AccountOut);
        ProbeCellLifecycle.HandBackUnmeasured(Cell(), ProbeReason.AccountOut).Reason().Should().Contain("is Pending, not Claimed");
        ProbeCellLifecycle.HandBackUnmeasured(ProbeCellLifecycle.Claim(Cell(), Worker("lane-1"), Noon, Pin('a')).Ok(), ProbeReason.None).Reason()
            .Should().Contain("says why");
    }

    [Fact]
    public void NextGeneration_refuses_a_lineage_whose_latest_generation_is_still_pending_or_claimed()
    {
        var pending = Cell();
        var claimed = ProbeCellLifecycle.Claim(Cell(), Worker("lane-1"), Noon, Pin('a')).Ok();
        var settled = Settled(Cell());

        ProbeCellLifecycle.NextGeneration([pending], Guid.CreateVersion7()).Reason().Should().Contain("generation 1").And.Contain("is Pending");
        ProbeCellLifecycle.NextGeneration([claimed], Guid.CreateVersion7()).Reason().Should().Contain("is Claimed");
        ProbeCellLifecycle.NextGeneration([settled, settled with { Id = Guid.CreateVersion7(), Generation = 2, Claim = Claimable.Fresh }], Guid.CreateVersion7()).Reason()
            .Should().Contain("generation 2").And.Contain("is Pending", "a re-run already waits; a third generation on top of it would measure one cell twice");
    }

    [Fact]
    public void NextGeneration_appends_a_fresh_pending_cell_numbered_after_the_highest_and_keeps_the_lineage()
    {
        var first = Settled(Cell());
        var second = first with { Id = Guid.CreateVersion7(), Generation = 2, Claim = Claimable.Stored(CellState.Abandoned, 3, WorkerIdentity.Nobody, default), Reason = ProbeReason.Abandoned };
        var id = Guid.CreateVersion7();

        var third = ProbeCellLifecycle.NextGeneration([first, second], id).Ok();

        third.Id.Should().Be(id, "the caller mints the id");
        third.Generation.Should().Be(3, "max + 1, over every generation the lineage has — settled or abandoned");
        third.Lineage.Should().Be(first.Lineage);
        (third.Slot, third.Position).Should().Be((first.Slot, first.Position));
        third.Claim.Should().Be(Claimable.Fresh, "a new generation is a new measurement: no attempts carried over");
        third.Pin.Should().Be(ProductPin.None);
        third.Facts.Should().Be(ProbeFacts.None);
        third.Artifacts.Should().BeEmpty();
        third.Reason.Should().Be(ProbeReason.None);
    }

    [Fact]
    public void NextGeneration_refuses_an_empty_or_a_mixed_lineage()
    {
        var a = Settled(Cell());
        var other = Settled(Cell() with { Probe = ProbeKind.WebSearch });

        ProbeCellLifecycle.NextGeneration([], Guid.CreateVersion7()).Reason().Should().Contain("no generation");
        ProbeCellLifecycle.NextGeneration([a, other], Guid.CreateVersion7()).Reason().Should().Contain("two lineages");
    }

    [Fact]
    public void A_pending_cell_carries_the_callers_id_and_starts_at_generation_one()
    {
        var id = new Guid("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");
        var runId = new Guid("0199a1b2-c3d4-7e5f-8a9b-000000000001");

        var cell = ProbeCell.Pending(id, runId, Matrix());

        cell.Id.Should().Be(id);
        cell.RunId.Should().Be(runId);
        cell.Generation.Should().Be(ProbeCell.FirstGeneration);
        cell.State.Should().Be(CellState.Pending);
        cell.Facts.Should().Be(ProbeFacts.None, "nothing attempted, nothing captured — never a zero that reads as a measurement");
        ProbeCell.Pending(id, runId, Matrix()).Should().Be(cell, "the same inputs make the same cell");
    }

    [Fact]
    public void LatestSettled_answers_the_highest_settled_generation_per_lineage_and_skips_a_lineage_with_none()
    {
        var g1 = Settled(Cell());
        var g2Pending = g1 with { Id = Guid.CreateVersion7(), Generation = 2, Claim = Claimable.Fresh, Facts = ProbeFacts.None };
        var g2Settled = Settled(g2Pending);
        var g3Abandoned = g1 with { Id = Guid.CreateVersion7(), Generation = 3, Claim = Claimable.Stored(CellState.Abandoned, 3, WorkerIdentity.Nobody, default) };
        var otherPending = Cell() with { Probe = ProbeKind.WebSearch };

        ProbeGenerations.LatestSettled([g1, g2Pending, otherPending]).Should().Equal([g1], "a pending re-run does not hide the verdict it re-measures");
        ProbeGenerations.LatestSettled([g1, g2Settled, g3Abandoned]).Should().Equal([g2Settled], "the highest SETTLED generation, not the highest generation");
        ProbeGenerations.Highest([g1, g2Settled, g3Abandoned]).Should().Be(3);
    }

    private static ProbeCell Settled(ProbeCell cell) =>
        ProbeCellLifecycle.Settle(ProbeCellLifecycle.Claim(cell, Worker("lane-1"), Noon, Pin('a')).Ok(), new ProbeSettlement(Facts(ProbeFact.Yes), [Artifact()])).Ok();

    private static ProbeFacts Facts(ProbeFact canary) => ProbeFacts.Answered(CapturedCount.Number(0)) with { CanaryRead = canary };

    private static ProbeArtifact Artifact() =>
        ProbeArtifact.Of(ProbeArtifactKind.Answer, ArtifactPath.Parse("probes/run/cell/g1/a1/answer.txt").Ok(), new string('a', 64), 12).Ok();

    private static WorkerIdentity Worker(string label) => WorkerIdentity.Here(label);

    private static ProductPin Pin(char letter) => ProductPin.Hashed(new string(letter, 64), "2.1.286", "abc1234", CapturedCount.Number(0), "cli").Ok();

    private static ProbeMatrixCell Matrix() => new(ProbeKind.ReadOutsideBare, ProbeSubjectId.Parse("claude-sonnet").Ok(), Repeat: 1, Slot: 0, Position: 0);

    private static ProbeCell Cell() => ProbeCell.Pending(Guid.CreateVersion7(), Guid.CreateVersion7(), Matrix());
}
