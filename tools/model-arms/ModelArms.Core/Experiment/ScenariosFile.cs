using System.Text.Json.Nodes;
using Bench.Domain;

namespace ModelArms.Core.Experiment;

/// <summary>One objective check the target's suites are scanned for: an id, the kind of suite it lives in (<c>cs</c> or <c>node</c>) and its name.</summary>
public sealed record Scenario(string Id, string Kind, string Name);

/// <summary>The scenarios file, <c>scenarios.json</c> of the experiment root: a JSON array of <c>{"id","kind","name"}</c>, as the shell kept it. Read
/// strictly like the experiment file — an unknown or missing member is named, every problem comes back at once — and never empty, because an
/// experiment with no scenario has nothing to measure.</summary>
public static class ScenariosFile
{
    private const string FileName = "scenarios.json";

    private static readonly string[] Members = ["id", "kind", "name"];
    private static readonly (string Key, string Value)[] Kinds = [("cs", "cs"), ("node", "node")];

    public static Outcome<IReadOnlyList<Scenario>> Read(string json)
    {
        var reader = new StrictJson();
        var array = reader.ParseArray(json, FileName);
        IReadOnlyList<Scenario> scenarios = array is null ? [] : [.. array.Select((node, index) => One(reader, node, index))];
        if (array is { Count: 0 })
        {
            reader.Add($"{FileName} holds no scenario");
        }

        foreach (var repeated in scenarios.Where(s => s.Id.Length > 0).GroupBy(s => s.Id, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            reader.Add($"{FileName} lists the id '{repeated.Key}' {repeated.Count()} times — an id names one scenario");
        }

        return reader.Result(() => scenarios);
    }

    private static Scenario One(StrictJson reader, JsonNode? node, int index)
    {
        var where = $"scenarios[{index}]";
        var scenario = reader.Object(node, where, Members, Members);
        return new Scenario(reader.Text(scenario, where, "id"), reader.Choice(scenario, where, "kind", Kinds, string.Empty), reader.Text(scenario, where, "name"));
    }
}
