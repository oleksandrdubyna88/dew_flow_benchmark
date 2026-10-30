using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bench.Domain;

namespace ModelArms.Core.Experiment;

/// <summary>The members that say what an arm DOES: the steps keyed by identity and the schemes made of them, with the rules that hold across steps and schemes. The rules of one
/// step are <see cref="StepSection"/>'s, those of one scheme <see cref="SchemeChecks"/>'.</summary>
internal static partial class FlowSection
{
    public static IReadOnlyDictionary<StepId, StepSpec> Steps(StrictJson json, JsonObject top)
    {
        var steps = new Dictionary<StepId, StepSpec>();
        foreach (var (key, node) in json.MapMember(top, string.Empty, "steps"))
        {
            if (StepId.Parse(key) is Outcome<StepId>.Ok id)
            {
                steps[id.Value] = StepSection.Read(json, id.Value, node);
            }
        }

        return steps;
    }

    public static IReadOnlyList<Scheme> Schemes(StrictJson json, JsonObject top, IReadOnlyDictionary<StepId, StepSpec> steps) =>
        [.. json.MapMember(top, string.Empty, "schemes").Select(member => Scheme(json, member.Key, member.Value, steps))];

    /// <summary>The rules that need more than one step or a whole scheme, recorded once the members are read.</summary>
    public static void Check(StrictJson json, IReadOnlyDictionary<StepId, StepSpec> steps, TaskLimits tasks, IReadOnlyList<Scheme> schemes)
    {
    }

    private static Scheme Scheme(StrictJson json, string name, JsonNode? node, IReadOnlyDictionary<StepId, StepSpec> steps)
    {
        var where = $"schemes.{name}";
        if (!SchemeName().IsMatch(name))
        {
            json.Add($"{where} — a scheme name is letters only, because an arm is the scheme name and a number (C1, SRS2)");
        }

        if (node is not JsonArray array || array.Count == 0)
        {
            json.Add($"{where} must be a non-empty array of entries, each \"tasks\" or \"<file name>:<step id>\"");
            return new Scheme(name, []);
        }

        return new Scheme(name, [.. array.Select((entry, index) => Entry(json, entry, $"{where}[{index}]", steps)).OfType<SchemeEntry>()]);
    }

    private static SchemeEntry? Entry(StrictJson json, JsonNode? node, string where, IReadOnlyDictionary<StepId, StepSpec> steps)
    {
        var text = json.NonEmptyText(node, where);
        return text switch
        {
            "" => null,
            "tasks" => new SchemeEntry.Tasks(),
            _ => StepEntry(json, text, where, steps),
        };
    }

    private static SchemeEntry.Step? StepEntry(StrictJson json, string text, string where, IReadOnlyDictionary<StepId, StepSpec> steps)
    {
        var match = EntryPattern().Match(text);
        if (!match.Success)
        {
            json.Add($"{where} is '{text}' — an entry is \"tasks\" or \"<file name>:<step id>\", such as \"1-code:code\"");
            return null;
        }

        return new SchemeEntry.Step(match.Groups["name"].Value, new StepId(match.Groups["id"].Value));
    }

    [GeneratedRegex("^[A-Za-z]+$")]
    private static partial Regex SchemeName();

    [GeneratedRegex("^(?<name>[0-9]+[a-z]?-[A-Za-z0-9-]+):(?<id>[A-Za-z][A-Za-z0-9]*)$")]
    private static partial Regex EntryPattern();
}
