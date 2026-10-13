using Bench.Contracts;
using Bench.Tests.Gate;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>The scout report on the wire carries no free text: every text-bearing property a <c>Scout*Dto</c> can reach is an id, a closed word,
/// a model id, a hash, a repository slug or a CLI version — named here as <c>Type.Property</c>. A new string field is red until it is named, so
/// no scout report, claim or assessor note can reach the read API of E3 S8 through one.</summary>
public sealed class ScoutContractsGuardTests
{
    private static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        "ScoutRunReportDto.Kind", "ScoutRunReportDto.Verdict",
        "ScoutPinsDto.CliVersion", "ScoutPinsDto.PluginCacheHash", "ScoutPinsDto.ResolvedOpus", "ScoutPinsDto.ResolvedSonnet", "ScoutPinsDto.SettingsHash",
        "ScoutWorkloadDto.Workload", "ScoutWorkloadDto.ArmA", "ScoutWorkloadDto.ArmB", "ScoutWorkloadDto.Outcome", "ScoutWorkloadDto.Reasons",
        "ScoutFigureDto.State",
        "ScoutArmReportDto.Arm", "ScoutArmReportDto.Workload", "ScoutArmReportDto.Model", "ScoutArmReportDto.Agent", "ScoutArmReportDto.WrongGap",
        // S4: a private task's Repository is its LABEL (ScoutPublication); Privacy is a closed word; Language a word like PHP or C#.
        "ScoutTaskReportDto.Task", "ScoutTaskReportDto.Repository", "ScoutTaskReportDto.Privacy", "ScoutTaskReportDto.Language", "ScoutTaskReportDto.Kind",
        "ScoutTaskArmDto.Arm",
    };

    [Fact]
    public void Every_text_bearing_property_a_scout_dto_can_reach_is_allowed_by_type_and_name()
    {
        TextSurface.Offenders(ScoutDtos(), Allowed).Should().BeEmpty(
            "a string that is not an id, a word, a hash or a slug could carry a scout's report or a claim to a public page");
    }

    [Fact]
    public void The_walk_reaches_the_nested_scout_shapes_and_every_allow_list_entry_still_exists()
    {
        TextSurface.Reachable(ScoutDtos()).Should().Contain([typeof(ScoutArmReportDto), typeof(ScoutWorkloadDto), typeof(ScoutIntervalDto), typeof(ScoutFigureDto),
            typeof(ScoutTokensDto), typeof(ScoutCountsDto), typeof(ScoutTaskReportDto), typeof(ScoutTaskArmDto), typeof(ScoutLedgerDto), typeof(ScoutPinsDto)]);
        var carriers = TextSurface.Carriers(ScoutDtos()).Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
        Allowed.Where(entry => !carriers.Contains(entry)).Should().BeEmpty("an entry for a property that is gone is a door left open");
    }

    [Fact]
    public void A_planted_claim_text_field_is_refused()
    {
        TextSurface.Offenders([typeof(PlantedArmDto)], Allowed).Should().Equal(["PlantedArmDto.ClaimText (string)"]);
    }

    private static IReadOnlyList<Type> ScoutDtos() =>
        [.. typeof(ScoutRunReportDto).Assembly.GetTypes()
            .Where(t => t.IsPublic && t.Name.StartsWith("Scout", StringComparison.Ordinal) && t.Name.EndsWith("Dto", StringComparison.Ordinal))];

    private sealed record PlantedArmDto(Guid CellId, string ClaimText);
}
