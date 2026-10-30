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

/// <summary>The fields one step's brief is filled with: the same thirteen for every kind, one notes file per kind, and
/// the task or the problems for the kinds that carry them — the shell's three field sets (<c>run-arm.sh</c> and
/// <c>t-lib.sh</c>), in one place. A template that uses none of a field is not an error: the shell passed
/// <c>HREVIEW_NOTES</c> and <c>BEFORE</c> to templates that never named them, and so does this.</summary>
public static class BriefFields
{
    public static IReadOnlyDictionary<string, string> For(StepKind kind, ArmPaths paths, BriefFacts facts) =>
        Common(kind, paths, facts).Concat(Extra(kind, facts)).ToDictionary(field => field.Key, field => field.Value);

    private static IEnumerable<KeyValuePair<string, string>> Common(StepKind kind, ArmPaths paths, BriefFacts facts) =>
    [
        Field("PLAN", facts.Plan),
        Field("BASE", facts.Base),
        Field("BRANCH", facts.Branch),
        Field("SCEN_COUNT", facts.ScenarioCount.ToString(CultureInfo.InvariantCulture)),
        Field("DEADLINE", facts.Deadline),
        Field("KNOWN", facts.Known),
        Field("REVIEW", paths.ReviewJson),
        Field("CODE_NOTES", paths.CodeNotes),
        Field("HREVIEW_NOTES", paths.HReviewNotes),
        Field("FIX_NOTES", paths.FixNotes),
        Field("WORK", paths.Work),
        Field("GATE_JSON", paths.GateJson),
        Field("NOTES", NotesOf(kind, paths, facts.Task)),
    ];

    private static IEnumerable<KeyValuePair<string, string>> Extra(StepKind kind, BriefFacts facts) =>
        kind switch
        {
            StepKind.TaskCode =>
            [
                Field("TASK", facts.Task.Id),
                Field("TASK_BRIEF", facts.Task.Brief),
                Field("TASK_FILES", facts.Task.Files),
                Field("BEFORE", facts.Task.Before),
            ],
            StepKind.SplitFix => [Field("PROBLEMS", facts.Problems)],
            _ => [],
        };

    /// <summary>The file a step writes its notes to. The review step writes none: the shell passed a dash.</summary>
    private static string NotesOf(StepKind kind, ArmPaths paths, TaskFacts task) =>
        kind switch
        {
            StepKind.Code => paths.CodeNotes,
            StepKind.Review => "-",
            StepKind.Fix => paths.FixNotes,
            StepKind.HReview => paths.HReviewNotes,
            StepKind.Gate => paths.GateNotes,
            StepKind.Split or StepKind.SplitFix => paths.SplitNotes,
            StepKind.TaskCode => paths.TaskNotes(task.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "a step kind this build has no notes file for"),
        };

    private static KeyValuePair<string, string> Field(string name, string value) => new(name, value);
}
