using System.Reflection;
using Bench.Contracts;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate;

/// <summary>The publication guard, structural first: finding text and prompt text never reach the wire,
/// because no <c>Gate*Dto</c> has a string property that could carry them. Every string property of every
/// gate DTO is on an allow-list of ids, hashes, enum names, stamps and version texts — a new field is red
/// until it is named here — and the one free-text field, the redacted failure cause, is named as the
/// exception rather than let through by a looser rule.</summary>
public sealed class GateContractsGuardTests
{
    /// <summary>What a string on a gate DTO may be. Grouped by what the name means, so a reviewer can see
    /// that nothing here could hold a sentence somebody wrote about private code.</summary>
    private static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        // ids and stamps
        "TaskId", "ReviewerId", "AssessorId", "RubricId", "SuiteStamp", "BatchId",
        // hashes
        "SettingsHash", "PromptHash", "RubricHash", "TextHash", "FileHash", "ClusterHash", "BinarySha256",
        // enum names and states
        "Gate", "RubricKind", "State", "Source", "Verdict", "FailureKind", "Severity", "Category",
        "Reading", "Value", "SeverityFair", "Grounded", "SeedHit", "FailureCause",
        // labels that are the trial's own words, never a sentence
        "Language", "ProductVersion",
    };

    /// <summary>The one string that is prose: a failure cause that has passed the publication redaction.</summary>
    private const string TheOneException = "FailureText";

    [Fact]
    public void Every_string_property_of_every_gate_dto_is_on_the_allow_list_but_the_failure_text()
    {
        var offenders = GateDtos()
            .SelectMany(type => StringProperties(type).Select(p => (Type: type.Name, Property: p.Name)))
            .Where(p => !Allowed.Contains(p.Property) && p.Property != TheOneException)
            .Select(p => $"{p.Type}.{p.Property}")
            .Order(StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "a string property that is not an id, a hash, an enum name or a label could carry a finding's or a prompt's text "
            + "to a public page; name it in the allow-list only if it provably cannot");
    }

    [Fact]
    public void The_scan_finds_the_gate_dtos_and_the_one_named_exception()
    {
        var dtos = GateDtos().Select(t => t.Name).Order(StringComparer.Ordinal).ToList();

        dtos.Should().HaveCountGreaterThanOrEqualTo(8, "a scan that finds nothing passes forever");
        dtos.Should().Contain(["GateModelTableDto", "GateRunSummaryDto", "GateRunDetailDto", "GateScopeDto", "GateFindingDto", "GateVerdictDto"]);

        StringProperties(typeof(GateRunSummaryDto)).Select(p => p.Name).Should().Contain(TheOneException,
            "the exception must exist for the rule to have been applied to it");
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
    public void Every_list_of_strings_on_a_gate_dto_is_also_held_to_the_allow_list()
    {
        var lists = GateDtos()
            .SelectMany(type => type.GetProperties().Where(p => p.PropertyType == typeof(IReadOnlyList<string>)).Select(p => $"{type.Name}.{p.Name}"))
            .ToList();

        lists.Should().BeEmpty("a list of strings is a list of possible sentences — the gate contracts carry counts and figures instead");
    }

    private static IReadOnlyList<Type> GateDtos() =>
        [.. typeof(GateScopeDto).Assembly.GetTypes()
            .Where(t => t.IsPublic && t.Name.StartsWith("Gate", StringComparison.Ordinal) && t.Name.EndsWith("Dto", StringComparison.Ordinal))];

    private static IEnumerable<PropertyInfo> StringProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.PropertyType == typeof(string));
}
