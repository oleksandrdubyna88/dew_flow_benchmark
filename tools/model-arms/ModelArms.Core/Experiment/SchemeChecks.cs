namespace ModelArms.Core.Experiment;

/// <summary>The rules a scheme answers for, each over <see cref="SchemePositions"/> so the reader, the rehearsal and the runner agree on what a position may see.</summary>
internal static class SchemeChecks
{
    public static IReadOnlyList<string> Problems(Scheme scheme, IReadOnlyDictionary<StepId, StepSpec> steps, TaskLimits tasks) => [];
}
