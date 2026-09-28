using Bench.Application.Gate;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate;

/// <summary>The ONE rule for which file is a cell attempt's turn-1 prompt (E7): class <c>Prompt</c>, first by ordinal
/// path — and three answers, not two, because the A/A must tell a cell that wrote no prompt from one whose prompt no
/// longer verifies.</summary>
[Collection("postgres")]
public sealed class GateTurnOnePromptTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_first_prompt_file_by_path_is_the_turn_one_prompt_and_carries_its_committed_hash()
    {
        using var rig = new PromptRig(postgres);
        var (campaign, cells) = await rig.CampaignAsync([PromptCell.Api("turn one") with { LaterPrompt = "turn two" }], "sample#0123456789ab", Ct);

        var read = await ReadAsync(rig, campaign, cells[0]);

        read.Should().Be(new TurnOnePrompt.Present("turn one", PromptRig.Sha("turn one")));
    }

    [Fact]
    public async Task A_cell_that_wrote_no_prompt_reads_absent()
    {
        using var rig = new PromptRig(postgres);
        var (campaign, cells) = await rig.CampaignAsync([PromptCell.Api(string.Empty)], "sample#0123456789ab", Ct);

        (await ReadAsync(rig, campaign, cells[0])).Should().BeOfType<TurnOnePrompt.Absent>();
    }

    [Fact]
    public async Task A_prompt_changed_under_its_ref_reads_unreadable_with_the_stores_reason()
    {
        using var rig = new PromptRig(postgres);
        var (campaign, cells) = await rig.CampaignAsync([PromptCell.Api("turn one")], "sample#0123456789ab", Ct);
        await rig.TamperAsync(campaign, cells[0], Ct);

        var read = await ReadAsync(rig, campaign, cells[0]);

        read.Should().BeOfType<TurnOnePrompt.Unreadable>().Which.Reason.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Another_attempt_of_the_same_cell_is_not_its_prompt()
    {
        using var rig = new PromptRig(postgres);
        var (campaign, cells) = await rig.CampaignAsync([PromptCell.Api("turn one")], "sample#0123456789ab", Ct);
        var artifacts = await rig.NewStore().ArtifactsAsync(campaign, Ct);

        (await GateTurnOnePrompt.ReadAsync(rig.Artifacts, artifacts, cells[0], attempt: 2, Ct)).Should().BeOfType<TurnOnePrompt.Absent>();
    }

    private static async Task<TurnOnePrompt> ReadAsync(PromptRig rig, Guid campaign, Guid cell) =>
        await GateTurnOnePrompt.ReadAsync(rig.Artifacts, await rig.NewStore().ArtifactsAsync(campaign, Ct), cell, attempt: 1, Ct);
}
