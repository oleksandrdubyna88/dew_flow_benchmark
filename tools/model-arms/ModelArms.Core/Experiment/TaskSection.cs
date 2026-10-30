using System.Text.Json.Nodes;
using Bench.Domain;

namespace ModelArms.Core.Experiment;

/// <summary>The <c>tasks</c> block: the limits of the SRS pipeline, the ids of its three steps and the outputs it hands to the steps after it. An absent block is
/// <see cref="TaskLimits.Shell"/>.</summary>
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
            [.. json.TextMap(tasks, "tasks", "outputs").Select(pair => OutputBinding.Parse(pair.Key, pair.Value)).OfType<Outcome<OutputBinding>.Ok>().Select(ok => ok.Value)]);
        if (limits.Min > limits.Max && limits.Max > 0)
        {
            json.Add($"tasks.min ({limits.Min}) is greater than tasks.max ({limits.Max}) — the split plans at least min and at most max tasks");
        }

        return limits;
    }
}
