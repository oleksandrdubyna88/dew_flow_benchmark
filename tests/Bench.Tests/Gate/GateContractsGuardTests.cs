using System.Text.Json;
using Bench.Contracts;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate;

/// <summary>The publication guard, structural first: finding text and prompt text never reach the wire,
/// because nothing a gate DTO can reach — itself, the records it nests, the elements of its lists — has a
/// text-bearing property that is not named here as <c>Type.Property</c>. A new field, a new nested record, a
/// list of strings, an <c>object</c> or a <c>JsonElement</c> is red until it is named, and the one free-text
/// field, the redacted failure cause, is named ON ITS TYPE as the exception rather than let through by name
/// wherever it appears.</summary>
public sealed class GateContractsGuardTests
{
    /// <summary>What text a gate DTO may carry, keyed by the type that carries it. Grouped by type so a reviewer
    /// can see that nothing here could hold a sentence somebody wrote about private code.</summary>
    private static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        "GateScopeDto.SuiteStamp", "GateScopeDto.Gate", "GateScopeDto.ProductVersion", "GateScopeDto.BinarySha256", "GateScopeDto.SettingsHash",
        "GateFigureDto.State",
        "GateModelRowDto.ReviewerId",
        "GateFailureCountDto.FailureKind",
        "GateVarianceDto.TaskId", "GateVarianceDto.ReviewerId", "GateVarianceDto.SeedsState", "GateVarianceDto.FindingsState",
        "GatePerTaskRowDto.TaskId", "GatePerTaskRowDto.Language", "GatePerTaskRowDto.ReviewerId",
        "GateModelTableDto.RubricKind", "GateModelTableDto.RubricId",
        "GateRunSummaryDto.Gate", "GateRunSummaryDto.TaskId", "GateRunSummaryDto.Language", "GateRunSummaryDto.ReviewerId",
        "GateRunSummaryDto.Source", "GateRunSummaryDto.Verdict", "GateRunSummaryDto.FailureKind", "GateRunSummaryDto.ProductVersion",
        "GateRunSummaryDto.BinarySha256",
        TheOneException,
        "GateFindingDto.Severity", "GateFindingDto.Category", "GateFindingDto.TextHash", "GateFindingDto.FileHash",
        "GateVerdictDto.RubricId", "GateVerdictDto.RubricKind", "GateVerdictDto.RubricHash", "GateVerdictDto.Reading", "GateVerdictDto.Value",
        "GateVerdictDto.SeverityFair", "GateVerdictDto.Grounded", "GateVerdictDto.ClusterHash", "GateVerdictDto.SeedHit",
        "GateVerdictDto.AssessorId", "GateVerdictDto.FailureCause",
        "GateRunDetailDto.SettingsHash", "GateRunDetailDto.PromptHash",
    };

    /// <summary>The one string that is prose — a failure cause that has passed the publication redaction — and
    /// only on the one type that carries it.</summary>
    private const string TheOneException = "GateRunSummaryDto.FailureText";

    [Fact]
    public void Every_text_bearing_property_a_gate_dto_can_reach_is_allowed_by_type_and_name()
    {
        TextSurface.Offenders(GateDtos(), Allowed).Should().BeEmpty(
            "a text-bearing property that is not an id, a hash, an enum name or a label could carry a finding's or a prompt's text "
            + "to a public page; name it in the allow-list, as Type.Property, only if it provably cannot");
    }

    [Fact]
    public void The_walk_reaches_every_contracts_type_the_gate_dtos_use_and_nothing_outside_the_contracts()
    {
        var reached = TextSurface.Reachable(GateDtos());

        reached.Should().Contain([typeof(GateFigureDto), typeof(GateFailureCountDto), typeof(GateTokensDto), typeof(GatePerTaskRowDto)],
            "a walk that stops at the roots is the check this replaced — the nested shapes are where text hides");
        reached.Should().OnlyContain(t => t.Assembly == typeof(GateScopeDto).Assembly,
            "every shape a gate DTO carries is a contract, and every contract it carries is walked");
        GateDtos().Should().HaveCountGreaterThanOrEqualTo(8, "a scan that finds nothing passes forever");
    }

    [Fact]
    public void Every_allow_list_entry_names_a_property_that_exists()
    {
        var carriers = TextSurface.Carriers(GateDtos()).Select(c => c.Key).ToHashSet(StringComparer.Ordinal);

        Allowed.Where(entry => !carriers.Contains(entry)).Should().BeEmpty(
            "an allow-list entry for a property that is gone is a door left open for the next one to take its name");
        carriers.Should().Contain(TheOneException, "the exception must exist for the rule to have been applied to it");
    }

    [Fact]
    public void A_gate_figure_is_never_a_bare_number()
    {
        GateFigureDto.Unassessed.Known.Should().BeFalse();
        GateFigureDto.Unassessed.State.Should().Be(GateFigureDto.UnassessedState, "rendered as a dash, never as a zero");
        GateFigureDto.Unknown.State.Should().Be(GateFigureDto.UnknownState, "a CLI reviewer's cost is unknown, never free");
        GateFigureDto.Withheld.State.Should().Be(GateFigureDto.WithheldState);
        GateFigureDto.NotApplicable.State.Should().Be(GateFigureDto.NotApplicableState);
        GateFigureDto.Of(12.5).Should().Be(new GateFigureDto(true, 12.5, GateFigureDto.KnownState));
    }

    [Fact]
    public void A_planted_free_text_field_is_refused_even_under_the_allowed_name()
    {
        TextSurface.Offenders([typeof(PlantedRunDto)], Allowed).Should().Equal(["PlantedRunDto.FailureText (string)"],
            "the exception is allowed on the summary DTO, not wherever the name 'FailureText' turns up");
    }

    [Fact]
    public void A_planted_nested_record_with_a_title_is_found_through_its_parent()
    {
        TextSurface.Offenders([typeof(PlantedFindingDto)], Allowed).Should().Equal(["FindingNote.Title (string)"]);
        TextSurface.Offenders([typeof(PlantedNotesDto)], Allowed).Should().Equal(["FindingNote.Title (string)"],
            "a list of a nested record is walked into as well");
    }

    [Fact]
    public void Planted_collections_of_strings_and_opaque_values_are_refused()
    {
        TextSurface.Offenders([typeof(PlantedCollectionsDto)], Allowed).Should().Equal(
        [
            "PlantedCollectionsDto.Any (opaque)",
            "PlantedCollectionsDto.Array (string)",
            "PlantedCollectionsDto.ByName (string)",
            "PlantedCollectionsDto.Json (opaque)",
            "PlantedCollectionsDto.List (string)",
            "PlantedCollectionsDto.Sequence (string)",
            "PlantedCollectionsDto.Values (string)",
        ]);
    }

    /// <summary>The approach this replaced, reproduced so its defect has a shape: top-level <c>string</c>
    /// properties of <c>Gate*Dto</c>-named types, allowed by property NAME. Every planted type above walks straight
    /// through it — which is the point.</summary>
    [Fact]
    public void The_name_keyed_top_level_string_check_lets_every_planted_shape_through_and_that_is_the_point()
    {
        var names = new HashSet<string>(StringComparer.Ordinal) { "FailureText" };

        static IEnumerable<string> Refuted(Type type, IReadOnlySet<string> allowed) =>
            type.Name.StartsWith("Gate", StringComparison.Ordinal) && type.Name.EndsWith("Dto", StringComparison.Ordinal)
                ? type.GetProperties().Where(p => p.PropertyType == typeof(string) && !allowed.Contains(p.Name)).Select(p => p.Name)
                : [];

        new[] { typeof(PlantedRunDto), typeof(PlantedFindingDto), typeof(PlantedNotesDto), typeof(PlantedCollectionsDto) }
            .SelectMany(t => Refuted(t, names)).Should().BeEmpty("the name filter never even looks at them, and the name list waves FailureText through");
        TextSurface.Offenders([typeof(PlantedRunDto), typeof(PlantedFindingDto), typeof(PlantedCollectionsDto)], Allowed).Should().HaveCount(9);
    }

    private static IReadOnlyList<Type> GateDtos() =>
        [.. typeof(GateScopeDto).Assembly.GetTypes()
            .Where(t => t.IsPublic && t.Name.StartsWith("Gate", StringComparison.Ordinal) && t.Name.EndsWith("Dto", StringComparison.Ordinal))];

    private sealed record PlantedRunDto(Guid RunId, string FailureText);

    private sealed record FindingNote(string Title);

    private sealed record PlantedFindingDto(int Ordinal, FindingNote Note);

    private sealed record PlantedNotesDto(IReadOnlyList<FindingNote> Notes);

    private sealed record PlantedCollectionsDto(
        List<string> List,
        string[] Array,
        IEnumerable<string> Sequence,
        Dictionary<string, int> ByName,
        IReadOnlyDictionary<int, string> Values,
        object Any,
        JsonElement Json,
        IReadOnlyList<int> Counts);
}
