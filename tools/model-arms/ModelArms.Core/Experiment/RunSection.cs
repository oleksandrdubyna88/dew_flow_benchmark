using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ModelArms.Core.Experiment;

/// <summary><c>runs</c> (D27): a map of run name → its rounds of arms, the arms of earlier runs it closes and the paths it closes besides. A run's name is the folder its stage
/// markers live in, so it is letters, digits and dashes.</summary>
internal static partial class RunSection
{
    private static readonly string[] Members = ["rounds", "closedArms", "closed"];
    private static readonly string[] Required = ["rounds"];

    public static IReadOnlyList<RunSpec> Runs(StrictJson json, JsonObject top, IReadOnlyList<Scheme> schemes, ExperimentLocation location)
    {
        IReadOnlyList<RunSpec> runs = [.. json.MapMember(top, string.Empty, "runs").Select(member => Run(json, member.Key, member.Value, schemes, location))];
        foreach (var problem in Across(runs))
        {
            json.Add(problem);
        }

        return runs;
    }

    private static RunSpec Run(StrictJson json, string name, JsonNode? node, IReadOnlyList<Scheme> schemes, ExperimentLocation location)
    {
        var where = $"runs.{ExecutableRule.Printable(name)}";
        if (!RunName().IsMatch(name))
        {
            json.Add($"{where} — a run name is letters, digits and dashes: it names the folder chain/{ExecutableRule.Printable(name)}/");
        }

        var run = json.Object(node, where, Members, Required);
        return new RunSpec(name, Rounds(json, run, where, schemes), json.Texts(run, where, "closedArms"), ExperimentFile.Expanded(json, run, where, "closed", location));
    }

    private static IReadOnlyList<IReadOnlyList<string>> Rounds(StrictJson json, JsonObject run, string where, IReadOnlyList<Scheme> schemes)
    {
        if (run["rounds"] is JsonArray { Count: > 0 } rounds)
        {
            return [.. rounds.Select((round, index) => Round(json, round, $"{where}.rounds[{index}]", schemes))];
        }

        if (run.ContainsKey("rounds"))
        {
            json.Add($"{where}.rounds must be a non-empty array of arrays of arm names");
        }

        return [];
    }

    private static IReadOnlyList<string> Round(StrictJson json, JsonNode? node, string where, IReadOnlyList<Scheme> schemes)
    {
        if (node is not JsonArray { Count: > 0 } arms)
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

    /// <summary>The rules across runs: an arm is in one round of one run, and a closed arm is an arm some run names.</summary>
    private static IEnumerable<string> Across(IReadOnlyList<RunSpec> runs)
    {
        return
        [
            .. runs.SelectMany(run => Arms(run).GroupBy(arm => arm, StringComparer.Ordinal).Where(group => group.Count() > 1)
                .Select(group => $"runs.{run.Name}.rounds — arm {group.Key} is named twice")),
            .. runs.SelectMany((run, index) => Arms(run).Distinct(StringComparer.Ordinal)
                .Select(arm => (Arm: arm, Earlier: runs.Take(index).Where(earlier => Arms(earlier).Contains(arm, StringComparer.Ordinal)).Take(1).ToList()))
                .Where(found => found.Earlier.Count > 0)
                .Select(found => $"runs.{run.Name} — arm {found.Arm} is already in run {found.Earlier[0].Name}")),
            .. runs.SelectMany((run, index) => run.ClosedArms.Select((arm, at) => ClosedProblem(runs, run, index, arm, at)).Where(problem => problem.Length > 0)),
        ];
    }

    /// <summary>A run closes the arms of EARLIER runs: the folders its own arms' work could otherwise reach. An arm of this run or of a later one is no such folder yet; an arm no run names is none.</summary>
    private static string ClosedProblem(IReadOnlyList<RunSpec> runs, RunSpec run, int index, string arm, int at)
    {
        var where = $"runs.{run.Name}.closedArms[{at}] names '{arm}'";
        return runs.Take(index).Any(earlier => Arms(earlier).Contains(arm, StringComparer.Ordinal))
            ? string.Empty
            : Arms(run).Contains(arm, StringComparer.Ordinal)
                ? $"{where}, an arm of this run"
                : runs.Skip(index + 1).FirstOrDefault(later => Arms(later).Contains(arm, StringComparer.Ordinal)) is { } owner
                    ? $"{where}, an arm of a later run {owner.Name}"
                    : $"{where}, which no run names";
    }

    private static IEnumerable<string> Arms(RunSpec run) => run.Rounds.SelectMany(round => round);

    [GeneratedRegex(@"^[A-Za-z0-9-]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex RunName();
}
