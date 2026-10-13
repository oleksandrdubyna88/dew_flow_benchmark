namespace Bench.Domain.Scouts;

/// <summary>What a run is allowed to conclude (plan D7). A <see cref="Preliminary"/> run — E3's 50 cells over 8 tasks, which cannot resolve a
/// 3-point margin — reports its numbers and their intervals and NO verdict; only a <see cref="Decision"/> run, sized by §3.8's simulation,
/// may say the rule changes. The kind is frozen on the run, so a report can never be read as a verdict the run was not planned to give.</summary>
public enum ScoutRunKind
{
    Preliminary,
    Decision,
}

/// <summary>The run kind's words, spelled once.</summary>
public static class ScoutRunKinds
{
    public static string Word(ScoutRunKind kind) => kind == ScoutRunKind.Decision ? "decision" : "preliminary";

    /// <summary>A kind by its word; an unknown word is refused naming the legal ones.</summary>
    public static Outcome<ScoutRunKind> Parse(string? word) =>
        (word ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "preliminary" => Outcome<ScoutRunKind>.Success(ScoutRunKind.Preliminary),
            "decision" => Outcome<ScoutRunKind>.Success(ScoutRunKind.Decision),
            var other => Outcome<ScoutRunKind>.Failure($"'{other}' is not a scout run kind — preliminary or decision"),
        };
}
