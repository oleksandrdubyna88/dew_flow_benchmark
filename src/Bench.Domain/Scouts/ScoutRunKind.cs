namespace Bench.Domain.Scouts;

/// <summary>What a run is allowed to conclude (plan D7) and what it may spend (E2 S6). A <see cref="Pilot"/> run corrects the instrument on
/// live streams under hard bounds (<see cref="ScoutRunLimits"/>: a cap of at most $3 per cell, a wall of at most 20 minutes, a ceiling of at
/// most $60) and takes its caps from flags; a <see cref="Preliminary"/> run — E3's 50 cells over 8 tasks, which cannot resolve a 3-point margin
/// — reports its numbers and their intervals and NO verdict; only a <see cref="Decision"/> run, sized by §3.8's simulation, may say the rule
/// changes. A preliminary or decision run takes its caps and walls from <c>schedule.json</c> only, never from a flag. The kind is frozen on
/// the run, so a report can never be read as a verdict the run was not planned to give.</summary>
public enum ScoutRunKind
{
    Preliminary,
    Decision,
    Pilot,
}

/// <summary>The run kind's words, spelled once.</summary>
public static class ScoutRunKinds
{
    public const string Forms = "pilot, preliminary or decision";

    public static string Word(ScoutRunKind kind) => kind switch
    {
        ScoutRunKind.Decision => "decision",
        ScoutRunKind.Pilot => "pilot",
        _ => "preliminary",
    };

    /// <summary>A kind by its word; an unknown word is refused naming the legal ones.</summary>
    public static Outcome<ScoutRunKind> Parse(string? word) =>
        (word ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "preliminary" => Outcome<ScoutRunKind>.Success(ScoutRunKind.Preliminary),
            "decision" => Outcome<ScoutRunKind>.Success(ScoutRunKind.Decision),
            "pilot" => Outcome<ScoutRunKind>.Success(ScoutRunKind.Pilot),
            var other => Outcome<ScoutRunKind>.Failure($"'{other}' is not a scout run kind — {Forms}"),
        };
}
