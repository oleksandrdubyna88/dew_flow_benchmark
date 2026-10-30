using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bench.Domain;

namespace ModelArms.Core.Experiment;

/// <summary>The members that say what an arm DOES: the steps keyed by identity and the schemes made of them, with the rules that hold across steps and schemes. The rules of one
/// step are <see cref="StepSection"/>'s, those of one scheme <see cref="SchemeChecks"/>'.</summary>
internal static partial class FlowSection
{
    /// <summary>The steps, keyed by identity, each read by <see cref="StepSection"/>.</summary>
    public static IReadOnlyDictionary<StepId, StepSpec> Steps(StrictJson json, JsonObject top)
    {
        var steps = new Dictionary<StepId, StepSpec>();
        foreach (var (key, node) in json.MapMember(top, string.Empty, "steps"))
        {
            switch (StepId.Parse(key))
            {
                case Outcome<StepId>.Ok id:
                    steps[id.Value] = StepSection.Read(json, id.Value, node);
                    break;
                case Outcome<StepId>.Fail fail:
                    json.Add($"steps.{ExecutableRule.Printable(key)}: {fail.Reason}");
                    break;
            }
        }

        return steps;
    }

    public static IReadOnlyList<Scheme> Schemes(StrictJson json, JsonObject top, IReadOnlyDictionary<StepId, StepSpec> steps) =>
        [.. json.MapMember(top, string.Empty, "schemes").Select(member => Scheme(json, member.Key, member.Value, steps))];

    /// <summary>The rules that need more than one step or a whole scheme, recorded once the members are read: a field bound by two steps, the ids of the tasks block (checked when the block
    /// is declared or a scheme has a tasks entry), a <c>notes</c> or a <c>diffFrom</c> that names nothing, and each scheme's own rules.</summary>
    public static void Check(StrictJson json, IReadOnlyDictionary<StepId, StepSpec> steps, TaskLimits tasks, bool tasksDeclared, IReadOnlyList<Scheme> schemes)
    {
        var usesBlock = tasksDeclared || schemes.Any(scheme => scheme.Entries.OfType<SchemeEntry.Tasks>().Any());
        var noteable = SchemeChecks.Noteable(steps, tasks, json.RefusedFields);
        IEnumerable<string> problems =
        [
            .. FieldsBoundTwice(steps),
            .. usesBlock ? TaskSection.Problems(tasks, steps) : [],
            .. steps.Values.SelectMany(spec => Unbound(spec, SchemeChecks.AllFields(steps, tasks), steps, json.RefusedFields)),
            .. schemes.SelectMany(scheme => SchemeChecks.Problems(scheme, steps, tasks, noteable)),
        ];
        foreach (var problem in problems)
        {
            json.Add(problem);
        }
    }

    private static IEnumerable<string> FieldsBoundTwice(IReadOnlyDictionary<StepId, StepSpec> steps) =>
        steps.Values.SelectMany(spec => spec.Outputs.Select(binding => (spec.Id, binding.Field)))
            .GroupBy(bound => bound.Field, StringComparer.Ordinal)
            .SelectMany(group => group.Skip(1).Select(later => $"steps.{later.Id}.outputs — {group.Key} is already bound by step {group.First().Id}"));

    /// <summary>A <c>notes</c> no step and no tasks block binds, or one that names a verdict, and a <c>diffFrom</c> that is no step id. Whether either holds at the position a scheme places
    /// the step is the scheme's. A <c>notes</c> that names a binding already refused says nothing more.</summary>
    private static IEnumerable<string> Unbound(StepSpec spec, IReadOnlyList<string> fields, IReadOnlyDictionary<StepId, StepSpec> steps, IReadOnlySet<string> refused) =>
    [
        .. UnusableNotes(spec, fields, steps, refused),
        .. UndefinedDiffFrom(spec, steps),
    ];

    private static IEnumerable<string> UnusableNotes(StepSpec spec, IReadOnlyList<string> fields, IReadOnlyDictionary<StepId, StepSpec> steps, IReadOnlySet<string> refused) =>
        spec.Notes.Length == 0 || refused.Contains(spec.Notes) ? [] : NotesProblem(spec, fields, steps);

    /// <summary>The verdict is the runner's output: a notes file is one the MODEL writes, so a notes naming a verdict would have the model write over what the runner derives.</summary>
    private static IEnumerable<string> NotesProblem(StepSpec spec, IReadOnlyList<string> fields, IReadOnlyDictionary<StepId, StepSpec> steps) =>
        steps.Values.FirstOrDefault(other => other.Verdict == spec.Notes) is { } owner
            ? [$"steps.{spec.Id}.notes is '{spec.Notes}' — the verdict of step {owner.Id}, which the runner writes; notes are a file the model writes"]
            : fields.Contains(spec.Notes) ? [] : [$"steps.{spec.Id}.notes is '{spec.Notes}' — no step binds it"];

    private static IEnumerable<string> UndefinedDiffFrom(StepSpec spec, IReadOnlyDictionary<StepId, StepSpec> steps) =>
        spec.DiffFrom.Length > 0 && !steps.ContainsKey(new StepId(spec.DiffFrom))
            ? [$"steps.{spec.Id}.diffFrom is '{spec.DiffFrom}' — not a step id defined under steps ({string.Join(", ", steps.Keys)})"]
            : [];

    private static Scheme Scheme(StrictJson json, string name, JsonNode? node, IReadOnlyDictionary<StepId, StepSpec> steps)
    {
        var where = $"schemes.{ExecutableRule.Printable(name)}";
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
            json.Add($"{where} is '{ExecutableRule.Printable(text)}' — an entry is \"tasks\" or \"<file name>:<step id>\", such as \"1-code:code\"");
            return null;
        }

        var id = new StepId(match.Groups["id"].Value);
        if (!steps.ContainsKey(id))
        {
            json.Add($"{where} is '{ExecutableRule.Printable(text)}' — step id '{id}' is not defined under steps (defined: {string.Join(", ", steps.Keys)})");
            return null;
        }

        return new SchemeEntry.Step(match.Groups["name"].Value, id);
    }

    [GeneratedRegex(@"^[A-Za-z]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex SchemeName();

    [GeneratedRegex(@"^(?<name>[0-9]+[a-z]?-[A-Za-z0-9-]+):(?<id>[A-Za-z][A-Za-z0-9]*)\z", RegexOptions.CultureInvariant)]
    private static partial Regex EntryPattern();
}
