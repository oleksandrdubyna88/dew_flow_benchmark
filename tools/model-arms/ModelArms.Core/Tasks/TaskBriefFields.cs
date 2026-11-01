namespace ModelArms.Core.Tasks;

/// <summary>The fields of a task's brief that the pipeline fills from the plan (<c>t-lib.sh:42-44</c>).</summary>
public static class TaskBriefFields
{
    /// <summary>The task's files, each in backquotes, joined by <c>, </c>: <c>`a`, `b`</c>.</summary>
    public static string Files(PlannedTask task) => string.Join(", ", task.Files.Select(file => $"`{file}`"));
}
