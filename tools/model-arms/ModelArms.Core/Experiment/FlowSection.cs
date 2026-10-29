using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bench.Domain;

namespace ModelArms.Core.Experiment;

/// <summary>The members that say what an arm DOES: the step kinds and their budgets, the schemes made of steps, and the rounds of arms.</summary>
internal static partial class FlowSection
{
    private static readonly string[] StepMembers = ["model", "minutes", "mode"];

    private static readonly (string Key, StepMode Value)[] Modes =
        [("write", StepMode.Write), ("read", StepMode.Read), ("gate", StepMode.Gate)];

    public static IReadOnlyDictionary<StepKind, StepSpec> Steps(StrictJson json, JsonObject top)
    {
        var steps = new Dictionary<StepKind, StepSpec>();
        foreach (var (key, node) in json.MapMember(top, string.Empty, "steps"))
        {
            switch (StepKinds.Parse(key))
            {
                case Outcome<StepKind>.Ok kind:
                    steps[kind.Value] = Step(json, kind.Value, node, $"steps.{key}");
                    break;
                case Outcome<StepKind>.Fail fail:
                    json.Add($"steps.{fail.Reason}");
                    break;
            }
        }

        return steps;
    }

    public static IReadOnlyList<Scheme> Schemes(StrictJson json, JsonObject top, IReadOnlyDictionary<StepKind, StepSpec> steps) =>
        [.. json.MapMember(top, string.Empty, "schemes").Select(member => Scheme(json, member.Key, member.Value, steps))];

    public static IReadOnlyList<IReadOnlyList<string>> Rounds(StrictJson json, JsonObject top, IReadOnlyList<Scheme> schemes)
    {
        if (top["rounds"] is not JsonArray rounds)
        {
            if (top.ContainsKey("rounds"))
            {
                json.Add("rounds must be an array of arrays of arm names");
            }

            return [];
        }

        if (rounds.Count == 0)
        {
            json.Add("rounds must name at least one round");
        }

        return [.. rounds.Select((round, index) => Round(json, round, $"rounds[{index}]", schemes))];
    }

    private static StepSpec Step(StrictJson json, StepKind kind, JsonNode? node, string path)
    {
        var step = json.Object(node, path, StepMembers, StepMembers);
        return new StepSpec(
            kind,
            json.Text(step, path, "model"),
            json.Positive(step, path, "minutes"),
            json.Choice(step, path, "mode", Modes, StepMode.Write));
    }

    private static Scheme Scheme(StrictJson json, string name, JsonNode? node, IReadOnlyDictionary<StepKind, StepSpec> steps)
    {
        var where = $"schemes.{name}";
        if (!SchemeName().IsMatch(name))
        {
            json.Add($"{where} — a scheme name is letters only, because an arm is the scheme name and a number (C1, SRS2)");
        }

        if (node is not JsonArray array || array.Count == 0)
        {
            json.Add($"{where} must be a non-empty array of entries, each \"tasks\" or \"<file name>:<step kind>\"");
            return new Scheme(name, []);
        }

        return new Scheme(name, [.. array.Select((entry, index) => Entry(json, entry, $"{where}[{index}]", steps)).OfType<SchemeEntry>()]);
    }

    private static SchemeEntry? Entry(StrictJson json, JsonNode? node, string where, IReadOnlyDictionary<StepKind, StepSpec> steps)
    {
        var text = json.NonEmptyText(node, where);
        return text switch
        {
            "" => null,
            "tasks" => new SchemeEntry.Tasks(),
            _ => StepEntry(json, text, where, steps),
        };
    }

    private static SchemeEntry.Step? StepEntry(StrictJson json, string text, string where, IReadOnlyDictionary<StepKind, StepSpec> steps)
    {
        var match = EntryPattern().Match(text);
        if (!match.Success)
        {
            json.Add($"{where} is '{text}' — an entry is \"tasks\" or \"<file name>:<step kind>\", such as \"1-code:code\"");
            return null;
        }

        var kind = KindOf(json, match.Groups["kind"].Value, $"{where} is '{text}'", steps);
        return kind is null ? null : new SchemeEntry.Step(match.Groups["name"].Value, kind.Value);
    }

    private static StepKind? KindOf(StrictJson json, string key, string where, IReadOnlyDictionary<StepKind, StepSpec> steps) =>
        StepKinds.Parse(key).Match(
            kind => steps.ContainsKey(kind)
                ? kind
                : Refuse(json, $"{where} — step kind '{key}' is not defined under steps (defined: {string.Join(", ", steps.Keys.Select(StepKinds.Key))})"),
            reason => Refuse(json, $"{where} — {reason}"));

    private static StepKind? Refuse(StrictJson json, string problem)
    {
        json.Add(problem);
        return null;
    }

    private static IReadOnlyList<string> Round(StrictJson json, JsonNode? node, string where, IReadOnlyList<Scheme> schemes)
    {
        if (node is not JsonArray arms || arms.Count == 0)
        {
            json.Add($"{where} must be a non-empty array of arm names");
            return [];
        }

        return [.. arms.Select((arm, index) => Arm(json, arm, $"{where}[{index}]", schemes))];
    }

    private static string Arm(StrictJson json, JsonNode? node, string where, IReadOnlyList<Scheme> schemes)
    {
        var arm = json.NonEmptyText(node, where);
        var problem = arm.Length == 0 ? string.Empty : ArmNames.SchemeOf(arm).Match(
            scheme => schemes.Any(s => s.Name == scheme)
                ? string.Empty
                : $"{where} names '{arm}', whose scheme '{scheme}' is not among the schemes ({string.Join(", ", schemes.Select(s => s.Name))})",
            reason => $"{where}: {reason}");
        if (problem.Length > 0)
        {
            json.Add(problem);
        }

        return arm;
    }

    [GeneratedRegex("^[A-Za-z]+$")]
    private static partial Regex SchemeName();

    [GeneratedRegex("^(?<name>[0-9]+[a-z]?-[A-Za-z0-9-]+):(?<kind>[A-Za-z]+)$")]
    private static partial Regex EntryPattern();
}
