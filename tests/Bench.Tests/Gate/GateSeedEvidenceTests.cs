using Bench.Application.Gate;
using Bench.Domain.Gate;
using Bench.Tests.Gate.Assessment;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate;

/// <summary>Where each seed sat, read off the task's first readable turn-1 prompt (E4) — pinned over a real store and a real
/// artefact root BEFORE the prompt lookup was extracted into <see cref="GateTurnOnePrompt"/> (E7), so the extraction is
/// proven to keep the one behaviour nothing else tested: a cell whose prompt is absent or no longer verifies is skipped,
/// never read as "the prompt was empty".</summary>
[Collection("postgres")]
public sealed class GateSeedEvidenceTests(PostgresFixture postgres)
{
    private const string WithoutTheSeed = "no seed here";

    private const string WithTheSeed = "the pack shows b";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static GateTask Cs2 => GateSuiteFile.Parse(AssessRig.SuiteJson).Ok().Tasks[0];

    [Fact]
    public async Task A_prompt_that_no_longer_verifies_is_skipped_and_the_next_cells_prompt_is_read()
    {
        using var rig = new PromptRig(postgres);
        var (campaign, cells) = await rig.CampaignAsync([PromptCell.Api(WithoutTheSeed), PromptCell.Api(WithTheSeed)], "sample#0123456789ab", Ct);
        await rig.TamperAsync(campaign, cells[0], Ct);

        var rows = await GateSeedEvidence.ReadAsync(rig.NewStore(), rig.Artifacts, [campaign], Cs2, Ct);

        rows.Select(r => r.Where).Should().AllBeEquivalentTo(EvidenceWhere.Pack, "the first cell's file changed under its ref, so the second cell's prompt is the evidence");
    }

    [Fact]
    public async Task A_cell_that_left_no_prompt_is_skipped_and_the_next_cells_prompt_is_read()
    {
        using var rig = new PromptRig(postgres);
        var (campaign, _) = await rig.CampaignAsync([PromptCell.Api(string.Empty), PromptCell.Api(WithTheSeed)], "sample#0123456789ab", Ct);

        var rows = await GateSeedEvidence.ReadAsync(rig.NewStore(), rig.Artifacts, [campaign], Cs2, Ct);

        rows.Select(r => r.Where).Should().AllBeEquivalentTo(EvidenceWhere.Pack);
    }

    [Fact]
    public async Task No_readable_prompt_anywhere_reads_unknown_never_missed()
    {
        using var rig = new PromptRig(postgres);
        var (campaign, cells) = await rig.CampaignAsync([PromptCell.Api(WithTheSeed)], "sample#0123456789ab", Ct);
        await rig.TamperAsync(campaign, cells[0], Ct);

        var rows = await GateSeedEvidence.ReadAsync(rig.NewStore(), rig.Artifacts, [campaign], Cs2, Ct);

        rows.Select(r => r.Where).Should().AllBeEquivalentTo(EvidenceWhere.Unknown);
    }
}
