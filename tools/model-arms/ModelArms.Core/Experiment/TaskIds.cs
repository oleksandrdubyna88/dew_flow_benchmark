using System.Text.RegularExpressions;

namespace ModelArms.Core.Experiment;

/// <summary>The shape of a task id, defined once: <c>t</c> and a number (<c>t1</c>, <c>t12</c>). The task pipeline's plan checker validates ids by it and names a task's files by it
/// (<c>work/t1.md</c>, <c>notes/t1-code.md</c>); the reader refuses a binding of those files by it, and E4's <c>tasks.json</c> checker reads the same constant.</summary>
public static class TaskIds
{
    /// <summary>A whole id and nothing else: <c>\z</c>, not <c>$</c>, so a trailing newline is not part of one.</summary>
    public static Regex Pattern { get; } = new(@"^t[0-9]+\z", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public static bool IsTaskId(string id) => Pattern.IsMatch(id);
}
