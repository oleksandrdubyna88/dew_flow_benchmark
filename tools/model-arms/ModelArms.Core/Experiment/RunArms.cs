namespace ModelArms.Core.Experiment;

/// <summary>Which arms the runs of an experiment name (C15, E3.S3): every arm of every run, in file order, once. The one derivation: <c>make</c> asks which arms to make, <c>status</c> which arms to list and the brief rehearsal which arms to
/// stand in for — three callers, one answer, so a run that names an arm twice (a later part reuses an earlier part's arm as a closed one) lists it once, at its first place.</summary>
public static class RunArms
{
    public static IReadOnlyList<string> Of(IReadOnlyList<RunSpec> runs) =>
        [.. runs.SelectMany(run => run.Rounds.SelectMany(round => round)).Distinct(StringComparer.Ordinal)];
}
