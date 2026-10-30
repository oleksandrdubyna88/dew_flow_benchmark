using System.Text.Json.Nodes;
using Bench.Domain;
using ModelArms.Core.Briefs;

namespace ModelArms.Core.Experiment;

/// <summary>One entry of <c>steps</c>, read strictly: its members, its bindings and the rules that hold for the step on its own — the pairing of kind, mode, verdict and
/// status, and what a <c>taskCode</c> step may not carry. The rules that need the other steps or a scheme (a field bound twice, a note nothing binds, a diff from a step the
/// scheme places later) are <see cref="FlowSection"/>'s and <see cref="SchemeChecks"/>'.</summary>
internal static class StepSection
{
    private static readonly string[] Members = ["kind", "model", "minutes", "mode", "subagents", "brief", "outputs", "notes", "verdict", "status", "snapshot", "diffFrom"];
    private static readonly string[] Required = ["kind", "model", "minutes", "mode", "brief"];

    private static readonly (string Key, StepMode Value)[] Modes = [("write", StepMode.Write), ("read", StepMode.Read), ("gate", StepMode.Gate)];

    public static StepSpec Read(StrictJson json, StepId id, JsonNode? node)
    {
        var path = $"steps.{id}";
        var step = json.Object(node, path, Members, Required);
        var spec = Specified(json, id, step, path);
        foreach (var refusal in Refusals(spec, path, json.RefusedFields))
        {
            json.Add(refusal);
        }

        return spec;
    }

    /// <summary>The outputs a member <c>outputs</c> of <paramref name="parent"/> declares. A name every brief is filled with is refused (<see cref="BriefFields.FixedNames"/>), the rest go
    /// through <see cref="OutputBinding.Parse"/>; a binding refused is recorded at <c>path.outputs.FIELD</c>, its name kept in <see cref="StrictJson.RefusedFields"/> and the binding left
    /// out, and two fields naming one file are recorded at <c>path.outputs</c>. The <c>tasks</c> block reads its outputs the same way.</summary>
    public static IReadOnlyList<OutputBinding> Bindings(StrictJson json, JsonObject parent, string path)
    {
        var parsed = json.TextMap(parent, path, "outputs").Select(pair => (pair.Key, Outcome: Binding(pair.Key, pair.Value))).ToList();
        foreach (var (field, outcome) in parsed)
        {
            Refuse(json, path, field, outcome);
        }

        IReadOnlyList<OutputBinding> outputs = [.. parsed.Select(pair => pair.Outcome).OfType<Outcome<OutputBinding>.Ok>().Select(ok => ok.Value)];
        foreach (var clash in Clashes(path, outputs))
        {
            json.Add(clash);
        }

        return outputs;
    }

    private static Outcome<OutputBinding> Binding(string field, string text) =>
        BriefFields.FixedNames.Contains(field, StringComparer.Ordinal)
            ? Outcome<OutputBinding>.Failure($"{field} is a field every brief is filled with — a binding cannot replace it")
            : OutputBinding.Parse(field, text);

    private static void Refuse(StrictJson json, string path, string field, Outcome<OutputBinding> outcome)
    {
        if (outcome is Outcome<OutputBinding>.Fail fail)
        {
            json.Add($"{ExecutableRule.Printable(StrictJson.Join(path + ".outputs", field))}: {fail.Reason}");
            json.RefuseField(field);
        }
    }

    private static StepSpec Specified(StrictJson json, StepId id, JsonObject step, string path) =>
        new(
            id,
            json.Choice(step, path, "kind", StepKinds.Choices, StepKind.Session),
            json.Text(step, path, "model"),
            json.Positive(step, path, "minutes"),
            json.Choice(step, path, "mode", Modes, StepMode.Write),
            json.Flag(step, path, "subagents", false),
            Brief(json, step, path),
            Bindings(json, step, path),
            json.Text(step, path, "notes"),
            json.Text(step, path, "verdict"),
            json.Text(step, path, "status"),
            json.Flag(step, path, "snapshot", false),
            json.Text(step, path, "diffFrom"));

    private static string Brief(StrictJson json, JsonObject step, string path)
    {
        var stem = json.Text(step, path, "brief");
        if (stem.Length > 0 && BriefTemplates.NameOf(stem) is Outcome<string>.Fail fail)
        {
            json.Add($"{path}.brief: {fail.Reason}");
        }

        return stem;
    }

    private static IEnumerable<string> Clashes(string path, IReadOnlyList<OutputBinding> outputs) =>
        outputs.GroupBy(binding => binding.RelativePath, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group.Skip(1).Select(later => $"{path}.outputs — {group.First().Field} and {later.Field} both name {later.RelativePath}"));

    private static IEnumerable<string> Refusals(StepSpec spec, string path, IReadOnlySet<string> refused) =>
    [
        .. ModePairing(spec, path),
        .. Naming(spec, path, "verdict", StepKind.Review, spec.Verdict, "a review step names the output its last result's JSON block is written to", refused),
        .. Naming(spec, path, "status", StepKind.Gate, spec.Status, "a gate step names the output it reads status from", refused),
        .. TaskCode(spec, path),
    ];

    /// <summary>The mode decides the MCP config and the tool set, the kind decides the lock and the status read: a gate step in write mode would run without the coai server and a
    /// session in gate mode would hold the lock for nothing.</summary>
    private static IEnumerable<string> ModePairing(StepSpec spec, string path) =>
        (spec.Kind, spec.Mode) switch
        {
            (StepKind.Gate, not StepMode.Gate) => [$"{path}.mode is '{Modes.First(mode => mode.Value == spec.Mode).Key}' — a gate step's mode is gate"],
            (not StepKind.Gate, StepMode.Gate) => [$"{path}.mode is 'gate' — only a gate step runs in gate mode (this step's kind is {StepKinds.Key(spec.Kind)})"],
            _ => [],
        };

    /// <summary>A <c>verdict</c> (review) or <c>status</c> (gate): required for its kind and naming one of the step's own outputs, refused on any other kind. A <c>taskCode</c> step
    /// is refused its bindings once, by <see cref="TaskCode"/>.</summary>
    private static IEnumerable<string> Naming(StepSpec spec, string path, string member, StepKind kind, string value, string why, IReadOnlySet<string> refused) =>
        spec.Kind == kind
            ? Named(spec, path, member, value, why, refused)
            : value.Length > 0 && spec.Kind != StepKind.TaskCode
                ? [$"{path}.{member} — only a {StepKinds.Key(kind)} step has a {member} (this step's kind is {StepKinds.Key(spec.Kind)})"]
                : [];

    private static IEnumerable<string> Named(StepSpec spec, string path, string member, string value, string why, IReadOnlySet<string> refused) =>
        value.Length == 0
            ? [$"{path}.{member} is missing — {why}"]
            : refused.Contains(value) || spec.Outputs.Any(binding => string.Equals(binding.Field, value, StringComparison.Ordinal))
                ? []
                : [$"{path}.{member} is '{value}' — not one of the step's outputs ({spec.FieldList})"];

    /// <summary>The task pipeline names a task coder's notes, snapshot and diff per task — paths a static binding cannot spell — so the step carries none.</summary>
    private static IEnumerable<string> TaskCode(StepSpec spec, string path) =>
        spec.Kind != StepKind.TaskCode
            ? []
            : [.. Bound(spec).Where(binding => binding.Present).Select(binding => $"{path}.{binding.Member} — a taskCode step has no bindings: the task pipeline names its notes, snapshot and diff per task (E4)")];

    private static IEnumerable<(string Member, bool Present)> Bound(StepSpec spec) =>
    [
        ("outputs", spec.Outputs.Count > 0),
        ("notes", spec.Notes.Length > 0),
        ("verdict", spec.Verdict.Length > 0),
        ("status", spec.Status.Length > 0),
        ("snapshot", spec.Snapshot),
        ("diffFrom", spec.DiffFrom.Length > 0),
    ];
}
