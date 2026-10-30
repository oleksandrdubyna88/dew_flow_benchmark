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
    /// <summary>The fields of the brief of <paramref name="spec"/>: one per visible binding, then the facts and <c>NOTES</c> and the fields of a task or of a split fix. The facts
    /// come last and win: a binding named like a fact would otherwise silently replace it.</summary>
    public static IReadOnlyDictionary<string, string> For(StepSpec spec, IReadOnlyList<OutputBinding> visible, TaskLimits tasks, ArmPaths paths, BriefFacts facts)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in Bound(visible, paths).Concat(Facts(paths, facts)).Append(Field("NOTES", NotesOf(spec, visible, paths, facts.Task))).Concat(Extra(spec, tasks, facts)))
        {
            fields[field.Key] = field.Value;
        }

        return fields;
    }

    private static IEnumerable<KeyValuePair<string, string>> Bound(IReadOnlyList<OutputBinding> visible, ArmPaths paths) =>
        visible.Select(binding => Field(binding.Field, paths.NotesFile(binding)));

    private static IEnumerable<KeyValuePair<string, string>> Facts(ArmPaths paths, BriefFacts facts) =>
    [
        Field("PLAN", facts.Plan),
        Field("BASE", facts.Base),
        Field("BRANCH", facts.Branch),
        Field("SCEN_COUNT", facts.ScenarioCount.ToString(CultureInfo.InvariantCulture)),
        Field("DEADLINE", facts.Deadline),
        Field("KNOWN", facts.Known),
        Field("WORK", paths.Work),
    ];

    private static IEnumerable<KeyValuePair<string, string>> Extra(StepSpec spec, TaskLimits tasks, BriefFacts facts) =>
        spec.Kind == StepKind.TaskCode
            ?
            [
                Field("TASK", facts.Task.Id),
                Field("TASK_BRIEF", facts.Task.Brief),
                Field("TASK_FILES", facts.Task.Files),
                Field("BEFORE", facts.Task.Before),
            ]
            : spec.Id == tasks.SplitFix ? [Field("PROBLEMS", facts.Problems)] : [];

    /// <summary>The file a step's notes go to: a task coder's per-task notes, the dash the shell passed a step that writes none, else the visible binding <c>notes</c> names — which the
    /// reader guaranteed is one, and exactly one.</summary>
    private static string NotesOf(StepSpec spec, IReadOnlyList<OutputBinding> visible, ArmPaths paths, TaskFacts task) =>
        spec.Kind == StepKind.TaskCode
            ? paths.TaskNotes(task.Id)
            : spec.Notes.Length == 0 ? "-" : paths.NotesFile(visible.Single(binding => binding.Field == spec.Notes));

    private static KeyValuePair<string, string> Field(string name, string value) => new(name, value);
}
