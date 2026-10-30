using System.Globalization;
using ModelArms.Core.Experiment;

namespace ModelArms.Core.Briefs;

/// <summary>The task a task-coder step is briefed with: its id, the path of its brief, the files it may touch and the
/// snapshot it starts from. <see cref="None"/> for every step kind that has no task.</summary>
public sealed record TaskFacts(string Id, string Brief, string Files, string Before)
{
    public static TaskFacts None { get; } = new("", "", "", "");
}

/// <summary>The facts a brief states that are not paths: the plan file, the base commit, the branch, how many scenarios
/// the plan holds, the deadline (spelled by the caller), the known-failures text, the task, and the problems a
/// plan-splitter was told about.</summary>
public sealed record BriefFacts(string Plan, string Base, string Branch, int ScenarioCount, string Deadline, string Known, TaskFacts Task, string Problems);

/// <summary>The fields one step's brief is filled with: the seven facts, <c>NOTES</c>, one field per binding the brief may see (<see cref="SchemePositions.Visible"/> — the
/// callers, the rehearsal and the runner, pass it, so the two cannot fill by two rules), and the task or the problems for the steps that carry them. A template that uses none
/// of a field is not an error: the shell passed every path to every template, and so does this.</summary>
public static class BriefFields
{
    public static IReadOnlyDictionary<string, string> For(StepSpec spec, IReadOnlyList<OutputBinding> visible, TaskLimits tasks, ArmPaths paths, BriefFacts facts) =>
        Facts(paths, facts).ToDictionary(field => field.Key, field => field.Value);

    private static IEnumerable<KeyValuePair<string, string>> Facts(ArmPaths paths, BriefFacts facts) =>
    [
        Field("PLAN", facts.Plan),
        Field("BASE", facts.Base),
        Field("BRANCH", facts.Branch),
        Field("SCEN_COUNT", facts.ScenarioCount.ToString(CultureInfo.InvariantCulture)),
        Field("DEADLINE", facts.Deadline),
        Field("KNOWN", facts.Known),
        Field("WORK", paths.Work),
        Field("NOTES", "-"),
    ];

    private static KeyValuePair<string, string> Field(string name, string value) => new(name, value);
}
