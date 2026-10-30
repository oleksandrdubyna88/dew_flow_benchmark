using System.Text.Json.Nodes;

namespace ModelArms.Core.Experiment;

/// <summary><c>runs</c> (D27): a map of run name → its rounds of arms, the arms of earlier runs it closes and the paths it closes besides.</summary>
internal static class RunSection
{
    private static readonly string[] Members = ["rounds", "closedArms", "closed"];
    private static readonly string[] Required = ["rounds"];

    public static IReadOnlyList<RunSpec> Runs(StrictJson json, JsonObject top, IReadOnlyList<Scheme> schemes, ExperimentLocation location) =>
        [.. json.MapMember(top, string.Empty, "runs").Select(member => Run(json, member.Key, member.Value, schemes, location))];

    private static RunSpec Run(StrictJson json, string name, JsonNode? node, IReadOnlyList<Scheme> schemes, ExperimentLocation location)
    {
        var where = $"runs.{name}";
        var run = json.Object(node, where, Members, Required);
        return new RunSpec(
            name,
            Rounds(json, run, where, schemes),
            json.Texts(run, where, "closedArms"),
            ExperimentFile.Expanded(json, run, where, "closed", location));
    }

    private static IReadOnlyList<IReadOnlyList<string>> Rounds(StrictJson json, JsonObject run, string where, IReadOnlyList<Scheme> schemes)
    {
        if (run["rounds"] is not JsonArray rounds)
        {
            return [];
        }

        return [.. rounds.Select((round, index) => Round(json, round, $"{where}.rounds[{index}]", schemes))];
    }

    private static IReadOnlyList<string> Round(StrictJson json, JsonNode? node, string where, IReadOnlyList<Scheme> schemes)
    {
        if (node is not JsonArray arms)
        {
            return [];
        }

        return [.. arms.Select((arm, index) => json.NonEmptyText(arm, $"{where}[{index}]"))];
    }
}
