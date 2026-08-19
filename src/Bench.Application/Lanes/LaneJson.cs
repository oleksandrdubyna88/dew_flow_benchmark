using System.Text.Json;
using System.Text.Json.Serialization;
using Bench.Domain;
using Bench.Domain.Lanes;

namespace Bench.Application.Lanes;

/// <summary>
/// A lane definition's wire shape — what a catalog row stores and a CLI accepts.
///
/// <para><b>A field this build does not know is REFUSED, never dropped</b>
/// (<see cref="JsonUnmappedMemberHandling.Disallow"/>). The failure it prevents is the one this whole
/// catalog exists against: a lane run under a surface nobody asked for, labelled with the name that asked
/// for the other one. Same discipline as <c>VariantJson</c>, the telemetry codec's unknown version and the
/// trace contract's unknown stage.</para>
///
/// <para>The doctrine travels as TEXT inside this JSON, so the row that names a measurement also carries
/// the paragraph that produced it — a published database explains its own numbers without a second
/// artefact.</para>
/// </summary>
public static class LaneJson
{
    /// <summary>camelCase because this string IS the published artefact — stored verbatim in the row and
    /// shipped with results. Read case-insensitively so a row written under an earlier naming still
    /// resolves: a definition that stops parsing is a lane every historical result names and nothing can
    /// explain.</summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static Outcome<LaneDefinition> Read(string json)
    {
        try
        {
            var wire = JsonSerializer.Deserialize<LaneWire>(json, Options);

            return wire is null
                ? Outcome<LaneDefinition>.Failure("the lane definition is empty")
                : FromWire(wire);
        }
        catch (JsonException ex)
        {
            return Outcome<LaneDefinition>.Failure($"the lane definition could not be read — {ex.Message}");
        }
    }

    public static string Write(LaneDefinition definition) =>
        JsonSerializer.Serialize(
            new LaneWire
            {
                Presentation = definition.Presentation.ToString(),
                Tools = definition.ToolNames,
                DescriptionSet = definition.DescriptionSet,
                Doctrine = definition.Doctrine,
                MaxTurns = definition.MaxTurns,
            },
            Options);

    /// <summary>An unknown presentation is refused by name with the legal values listed — never resolved to
    /// a default. A lane silently demoted to <c>None</c> would be the no-tools floor wearing a tool lane's
    /// name, which is the one substitution that makes every comparison against the floor meaningless.</summary>
    private static Outcome<LaneDefinition> FromWire(LaneWire wire)
    {
        var presentation = (wire.Presentation ?? string.Empty).Trim();

        return Enum.TryParse<ToolPresentation>(presentation, ignoreCase: true, out var parsed)
            ? LaneDefinition.Create(wire.Tools, wire.DescriptionSet, wire.Doctrine, parsed, wire.MaxTurns)
            : Outcome<LaneDefinition>.Failure(
                $"unknown tool presentation '{presentation}' — this build knows "
                + string.Join(", ", Enum.GetNames<ToolPresentation>()));
    }

    /// <summary>The stored shape. <c>MaxTurns</c> is a plain int with no default of its own: a wire that
    /// omits it lands on 0, which the domain refuses by name — better than a silent 1, which would record a
    /// single-turn micro-task under the name of an agentic lane.</summary>
    private sealed record LaneWire
    {
        public string? Presentation { get; init; }

        public IReadOnlyList<string>? Tools { get; init; }

        public string? DescriptionSet { get; init; }

        public string? Doctrine { get; init; }

        public int MaxTurns { get; init; }
    }
}
