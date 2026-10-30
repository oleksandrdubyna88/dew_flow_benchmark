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
    private const string Plan = "PLAN";
    private const string Base = "BASE";
    private const string Branch = "BRANCH";
    private const string ScenCount = "SCEN_COUNT";
    private const string Deadline = "DEADLINE";
    private const string Known = "KNOWN";
    private const string Work = "WORK";
    private const string Notes = "NOTES";
    private const string Task = "TASK";
    private const string TaskBrief = "TASK_BRIEF";
    private const string TaskFiles = "TASK_FILES";
    private const string Before = "BEFORE";
    private const string Problems = "PROBLEMS";

    /// <summary>The names every brief is filled with whatever the bindings say: the one list the builder below spells its fields from and the reader refuses a binding by (steps and
    /// the <c>tasks</c> block alike), so a binding never silently loses to a fact.</summary>
    public static IReadOnlyList<string> FixedNames { get; } = [Plan, Base, Branch, ScenCount, Deadline, Known, Work, Notes, Task, TaskBrief, TaskFiles, Before, Problems];

    /// <summary>The fields of the brief of <paramref name="spec"/>: one per visible binding, then the facts and <c>NOTES</c> and the fields of a task or of a split fix. The reader refuses a
    /// binding named like any of them (<see cref="FixedNames"/>); should one get here anyway, the facts come last and win.</summary>
    public static IReadOnlyDictionary<string, string> For(StepSpec spec, IReadOnlyList<OutputBinding> visible, TaskLimits tasks, ArmPaths paths, BriefFacts facts)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in Bound(visible, paths).Concat(Facts(paths, facts)).Append(Field(Notes, NotesOf(spec, visible, paths, facts.Task))).Concat(Extra(spec, tasks, facts)))
        {
            fields[field.Key] = field.Value;
        }

        return fields;
    }

    private static IEnumerable<KeyValuePair<string, string>> Bound(IReadOnlyList<OutputBinding> visible, ArmPaths paths) =>
        visible.Select(binding => Field(binding.Field, paths.NotesFile(binding)));

    private static IEnumerable<KeyValuePair<string, string>> Facts(ArmPaths paths, BriefFacts facts) =>
    [
        Field(Plan, facts.Plan),
        Field(Base, facts.Base),
        Field(Branch, facts.Branch),
        Field(ScenCount, facts.ScenarioCount.ToString(CultureInfo.InvariantCulture)),
        Field(Deadline, facts.Deadline),
        Field(Known, facts.Known),
        Field(Work, paths.Work),
    ];

    private static IEnumerable<KeyValuePair<string, string>> Extra(StepSpec spec, TaskLimits tasks, BriefFacts facts) =>
        spec.Kind == StepKind.TaskCode
            ?
            [
                Field(Task, facts.Task.Id),
                Field(TaskBrief, facts.Task.Brief),
                Field(TaskFiles, facts.Task.Files),
                Field(Before, facts.Task.Before),
            ]
            : spec.Id == tasks.SplitFix ? [Field(Problems, facts.Problems)] : [];

    /// <summary>The file a step's notes go to: a task coder's per-task notes, the dash the shell passed a step that writes none, else the visible binding <c>notes</c> names — which the
    /// reader guaranteed is there exactly once: a field is bound once per scheme, the split fix is not the split (or <c>Visible</c> would hold the split's outputs twice) and a
    /// <c>notes</c> never names a verdict, which the runner writes.</summary>
    private static string NotesOf(StepSpec spec, IReadOnlyList<OutputBinding> visible, ArmPaths paths, TaskFacts task) =>
        spec.Kind == StepKind.TaskCode
            ? paths.TaskNotes(task.Id)
            : spec.Notes.Length == 0 ? "-" : paths.NotesFile(visible.Single(binding => binding.Field == spec.Notes));

    private static KeyValuePair<string, string> Field(string name, string value) => new(name, value);
}
