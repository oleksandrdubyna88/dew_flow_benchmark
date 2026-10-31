using System.Text.Json.Nodes;
using Bench.Domain;

namespace ModelArms.Core.Experiment;

/// <summary>The <c>tasks</c> block: the limits of the SRS pipeline, the ids of its three steps and the outputs it hands to the steps after it. An absent block is
/// <see cref="TaskLimits.Shell"/>; the ids are checked against the steps whenever the block is present or a scheme has a <c>tasks</c> entry.</summary>
internal static class TaskSection
{
    private static readonly string[] Required = ["min", "max", "maxParallel", "split", "splitFix", "taskCode"];
    private static readonly string[] Members = [.. Required, "outputs"];

    public static TaskLimits Read(StrictJson json, JsonObject top)
    {
        if (!top.ContainsKey("tasks"))
        {
            return TaskLimits.Shell;
        }

        var tasks = json.ObjectMember(top, string.Empty, "tasks", Members, Required);
        var limits = new TaskLimits(
            json.Positive(tasks, "tasks", "min"),
            json.Positive(tasks, "tasks", "max"),
            json.Positive(tasks, "tasks", "maxParallel"),
            new StepId(json.Text(tasks, "tasks", "split")),
            new StepId(json.Text(tasks, "tasks", "splitFix")),
            new StepId(json.Text(tasks, "tasks", "taskCode")),
            StepSection.Bindings(json, tasks, "tasks"));
        if (limits.Min > limits.Max && limits.Max > 0)
        {
            json.Add($"tasks.min ({limits.Min}) is greater than tasks.max ({limits.Max}) — the split plans at least min and at most max tasks");
        }

        return limits;
    }

    /// <summary>The three ids must name steps of the right kinds. An id the block did not give is already reported as missing.</summary>
    public static IEnumerable<string> Problems(TaskLimits tasks, IReadOnlyDictionary<StepId, StepSpec> steps) =>
    [
        .. Problem("split", tasks.Split, StepKind.Session, "the split is a session step", steps),
        .. Problem("splitFix", tasks.SplitFix, StepKind.Session, "the split fix is a session step", steps),
        .. SameStep(tasks),
        .. Problem("taskCode", tasks.TaskCode, StepKind.TaskCode, "the task coder is a taskCode step", steps),
    ];

    /// <summary>The split fix appends to the split's notes; it is not the split. One step in both places would hold the split's outputs twice in what a brief may see.</summary>
    private static IEnumerable<string> SameStep(TaskLimits tasks) =>
        tasks.Split.Value.Length > 0 && tasks.Split == tasks.SplitFix
            ? [$"tasks.splitFix is '{tasks.SplitFix}' — the split fix is a step of its own, not the split"]
            : [];

    private static IEnumerable<string> Problem(string member, StepId id, StepKind kind, string role, IReadOnlyDictionary<StepId, StepSpec> steps) =>
        id.Value.Length == 0
            ? []
            : !steps.TryGetValue(id, out var spec)
                ? [$"tasks.{member} is '{id}' — not a step id defined under steps ({string.Join(", ", steps.Keys)})"]
                : spec.Kind != kind
                    ? [$"tasks.{member} is '{id}' — its kind is {StepKinds.Key(spec.Kind)}; {role}"]
                    : [];
}
