using Bench.Contracts;
using Bench.Tests.Gate;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Probes;

/// <summary>D11 on the wire: every text-bearing property a probe DTO can reach is an id, a closed word, a model id, a variable NAME, a
/// public vendor url, a hash, a relative path, a CLI's <c>--version</c> line or the rerun command — named here as
/// <c>Type.Property</c>. A new string field is red until it is named; no answer or transcript can reach a page through one.</summary>
public sealed class ProbeContractsGuardTests
{
    private static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        "ProbeOracleDto.Version", "ProbeOracleDto.Source",
        "ProbeSubjectDto.Id", "ProbeSubjectDto.Runtime", "ProbeSubjectDto.Model", "ProbeSubjectDto.ExecutableRef",
        "ProbeSubjectDto.Vendor", "ProbeSubjectDto.Endpoint", "ProbeSubjectDto.Dialect",
        "ProbeCellReportDto.Probe", "ProbeCellReportDto.Subject", "ProbeCellReportDto.State", "ProbeCellReportDto.Kind",
        "ProbeCellReportDto.Reason", "ProbeCellReportDto.LatestState", "ProbeCellReportDto.RerunCommand",
        "ProbeFactsDto.CanaryRead", "ProbeFactsDto.ReadAttempted", "ProbeFactsDto.AnswerCurrent", "ProbeFactsDto.ToolEvidence",
        "ProbeFactsDto.Reachable", "ProbeFactsDto.AccountOut",
        "ProbePinDto.Version", "ProbePinDto.BinarySha256",
        "ProbeArtifactDto.Kind", "ProbeArtifactDto.Path", "ProbeArtifactDto.Sha256",
    };

    [Fact]
    public void Every_text_bearing_property_a_probe_dto_can_reach_is_allowed_by_type_and_name()
    {
        TextSurface.Offenders(ProbeDtos(), Allowed).Should().BeEmpty(
            "a string that is not an id, a word, a hash or a path could carry an answer's text to a public page (D11)");
    }

    [Fact]
    public void The_walk_reaches_the_nested_probe_shapes_and_every_allow_list_entry_still_exists()
    {
        TextSurface.Reachable(ProbeDtos()).Should().Contain([typeof(ProbeCellReportDto), typeof(ProbeFactsDto), typeof(ProbePinDto), typeof(ProbeArtifactDto)]);
        var carriers = TextSurface.Carriers(ProbeDtos()).Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
        Allowed.Where(entry => !carriers.Contains(entry)).Should().BeEmpty("an entry for a property that is gone is a door left open");
    }

    [Fact]
    public void A_planted_answer_field_is_refused()
    {
        TextSurface.Offenders([typeof(PlantedCellDto)], Allowed).Should().Equal(["PlantedCellDto.AnswerText (string)"]);
    }

    private static IReadOnlyList<Type> ProbeDtos() =>
        [.. typeof(ProbeRunReportDto).Assembly.GetTypes()
            .Where(t => t.IsPublic && t.Name.StartsWith("Probe", StringComparison.Ordinal) && t.Name.EndsWith("Dto", StringComparison.Ordinal))];

    private sealed record PlantedCellDto(Guid CellId, string AnswerText);
}
