using Bench.Application.Probes;
using Bench.Domain.Probes;
using Bench.Domain.Runs;
using Bench.Domain.Trace;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Infrastructure.ProbeStoreFixtures;

namespace Bench.Tests.Probes;

/// <summary>Which lineages <c>bench probes rerun</c> appends to: one cell's, or a subject's slice — and never while that subject still
/// has cells to measure, because the campaign would measure those too.</summary>
public sealed class ProbeRerunTargetsTests
{
    private static readonly ProbeSubject A = Subject("subject-a", "claude");
    private static readonly ProbeSubject B = Subject("subject-b", "codex");

    [Fact]
    public void A_cell_names_its_lineage_and_the_seed_is_its_highest_generation()
    {
        var (_, _, cells) = PlannedMatrix([ProbeKind.ReadInside], A);
        var settled = cells.Select(Settle).ToList();
        var g2 = Settle(ProbeCellLifecycle.NextGeneration([settled[0]], Guid.CreateVersion7()).Ok());

        var seeds = ProbeRerunTargets.ForCell([.. settled, g2], settled[0].Id).Ok();

        seeds.Should().ContainSingle().Which.Id.Should().Be(g2.Id, "a re-run of an old generation's id numbers after the newest");
    }

    [Fact]
    public void A_slice_takes_every_lineage_of_the_subject_or_only_the_named_probes()
    {
        var (run, _, cells) = PlannedMatrix([ProbeKind.ReadInside, ProbeKind.ReadOutsideBare], A, B);
        var settled = cells.Select(Settle).ToList();

        ProbeRerunTargets.ForSlice(run, settled, A.Id, []).Ok().Should().HaveCount(4, "two probes × two repeats")
            .And.OnlyContain(c => c.Subject == A.Id);
        ProbeRerunTargets.ForSlice(run, settled, A.Id, [ProbeKind.ReadOutsideBare]).Ok().Should().HaveCount(2)
            .And.OnlyContain(c => c.Probe == ProbeKind.ReadOutsideBare);
    }

    [Fact]
    public void A_subject_with_cells_still_pending_is_refused_naming_the_resume_command()
    {
        var (run, _, cells) = PlannedMatrix([ProbeKind.ReadInside], A, B);
        var mixed = cells.Select(c => c.Subject == B.Id && c.Repeat == 2 ? c : Settle(c)).ToList();

        var refused = ProbeRerunTargets.ForCell(mixed, mixed.First(c => c.Subject == B.Id && c.Repeat == 1).Id);

        refused.Reason().Should().Contain("subject-b").And.Contain("1 Pending").And.Contain($"bench probes resume --run {run.Id}");
        ProbeRerunTargets.ForSlice(run, mixed, A.Id, []).Ok().Should().HaveCount(2, "another subject's pending cells do not block this one");
    }

    [Fact]
    public void An_unknown_cell_subject_or_probe_is_refused_by_name()
    {
        var (run, _, cells) = PlannedMatrix([ProbeKind.ReadInside], A);
        var settled = cells.Select(Settle).ToList();

        ProbeRerunTargets.ForCell(settled, Guid.NewGuid()).Reason().Should().Contain("not a cell of this run");
        ProbeRerunTargets.ForSlice(run, settled, B.Id, []).Reason().Should().Contain("subject-b");
        ProbeRerunTargets.ForSlice(run, settled, A.Id, [ProbeKind.WebSearch]).Reason().Should().Contain("web-search");
    }

    private static ProbeCell Settle(ProbeCell cell) =>
        ProbeCellLifecycle.Settle(
            ProbeCellLifecycle.Claim(cell, WorkerIdentity.Here("t"), Noon, Pin()).Ok(),
            new ProbeSettlement(ProbeFacts.Answered(CapturedCount.Number(0)), [])).Ok();
}
