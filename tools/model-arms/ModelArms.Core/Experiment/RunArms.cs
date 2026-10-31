namespace ModelArms.Core.Experiment;

/// <summary>Which arms the runs of an experiment name (C15, E3.S3): every arm of every run, in file order, once. The one derivation: <c>make</c> asks which arms to make, <c>status</c> which arms to list and the brief rehearsal which arms to
/// stand in for — three callers, one answer, so an arm that two rounds or two runs name is listed once, at its first place. Only <c>rounds</c> name arms here: a run's <c>closedArms</c> are earlier runs' arms it closes, not arms it makes.</summary>
public static class RunArms
{
    public static IReadOnlyList<string> Of(IReadOnlyList<RunSpec> runs) =>
        [.. runs.SelectMany(run => run.Rounds.SelectMany(round => round)).Distinct(StringComparer.Ordinal)];
}
