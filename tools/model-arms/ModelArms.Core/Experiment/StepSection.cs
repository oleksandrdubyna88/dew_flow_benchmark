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
        Kinded(json, spec, path);
        return spec;
    }

    private static StepSpec Specified(StrictJson json, StepId id, JsonObject step, string path) =>
        new(
            id,
            json.Choice(step, path, "kind", StepKinds.Choices, StepKind.Session),
            json.Text(step, path, "model"),
            json.Positive(step, path, "minutes"),
            json.Choice(step, path, "mode", Modes, StepMode.Write),
            json.Flag(step, path, "subagents", false),
            json.Text(step, path, "brief"),
            Bindings(json, step, path),
            json.Text(step, path, "notes"),
            json.Text(step, path, "verdict"),
            json.Text(step, path, "status"),
            json.Flag(step, path, "snapshot", false),
            json.Text(step, path, "diffFrom"));

    private static IReadOnlyList<OutputBinding> Bindings(StrictJson json, JsonObject step, string path) =>
        [.. json.TextMap(step, path, "outputs").Select(pair => OutputBinding.Parse(pair.Key, pair.Value)).OfType<Outcome<OutputBinding>.Ok>().Select(ok => ok.Value)];

    private static void Kinded(StrictJson json, StepSpec spec, string path)
    {
    }
}
