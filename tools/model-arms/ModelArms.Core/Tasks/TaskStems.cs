using ModelArms.Core.Experiment;

namespace ModelArms.Core.Tasks;

/// <summary>The step names the task pipeline owns (D5; <c>t-lib.sh:80</c>, <c>:84</c>, <c>:53</c>): the stems are not configuration, so a scheme entry named like one is refused (<c>SchemeChecks</c>).</summary>
public static class TaskStems
{
    public const string Split = "1-split";

    public const string SplitFix = "1b-split-fix";

    private const string CoderPrefix = "2-";

    private const string CoderSuffix = "-code";

    /// <summary>A task's coder: <c>2-t3-code</c>. An id <see cref="TaskIds"/> refuses is a caller's bug — the stem names files.</summary>
    public static string Coder(string id) =>
        TaskIds.IsTaskId(id) ? CoderPrefix + id + CoderSuffix : throw new ArgumentException($"'{id}' is not a task id", nameof(id));

    /// <summary>One of the three shapes (<see cref="Split"/>, <see cref="SplitFix"/>, <see cref="Coder"/>), without regard to case: a host whose file system ignores it would write the pipeline's file under the scheme's name.</summary>
    public static bool IsPipelineStem(string name) =>
        name.Equals(Split, StringComparison.OrdinalIgnoreCase) || name.Equals(SplitFix, StringComparison.OrdinalIgnoreCase) || IsCoder(name);

    private static bool IsCoder(string name) =>
        name.Length > CoderPrefix.Length + CoderSuffix.Length
        && name.StartsWith(CoderPrefix, StringComparison.Ordinal)
        && name.EndsWith(CoderSuffix, StringComparison.OrdinalIgnoreCase)
        && TaskIds.IsTaskId(name[CoderPrefix.Length..^CoderSuffix.Length].ToLowerInvariant());
}
