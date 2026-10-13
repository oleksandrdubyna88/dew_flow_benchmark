namespace Bench.Domain.Scouts;

/// <summary>What a drawn task is for (D10): one of the eight MAIN tasks, the reserve its repository holds behind it, or one of the two pilot
/// tasks (E2 S8, repositories 9 and 10 of the order).</summary>
public enum ScoutTaskRole
{
    Main,
    Reserve,
    Pilot,
}

public static class ScoutTaskRoles
{
    public static string Word(ScoutTaskRole role) => role switch
    {
        ScoutTaskRole.Reserve => "reserve",
        ScoutTaskRole.Pilot => "pilot",
        _ => "main",
    };

    public static Outcome<ScoutTaskRole> Parse(string? word) => (word ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "main" or "" => Outcome<ScoutTaskRole>.Success(ScoutTaskRole.Main),
        "reserve" => Outcome<ScoutTaskRole>.Success(ScoutTaskRole.Reserve),
        "pilot" => Outcome<ScoutTaskRole>.Success(ScoutTaskRole.Pilot),
        var other => Outcome<ScoutTaskRole>.Failure($"'{other}' is not a task role — main, reserve or pilot"),
    };
}

public sealed record ScoutDrawnTask(ScoutCandidateFacts Facts, ScoutTaskRole Role);

/// <summary>One repository as the draw ranked it: its full name, its short name (what the hash was taken over), what a console line may call
/// it (<see cref="ScoutPublication.Repository(ScoutCandidate)"/> — a private repository by a label), how many admissible candidates it holds,
/// and whether that reached the bar.</summary>
public sealed record ScoutRepositoryRank(string Repository, string ShortName, string Shown, int Admissible, bool Eligible);

/// <summary>The D10 draw: every repository in its drawn order (the ineligible ones after, by name), and the tasks with their roles.</summary>
public sealed record ScoutDraw(IReadOnlyList<ScoutRepositoryRank> Repositories, IReadOnlyList<ScoutDrawnTask> Tasks)
{
    public IReadOnlyList<ScoutDrawnTask> Main => [.. Tasks.Where(t => t.Role == ScoutTaskRole.Main)];

    public IReadOnlyList<ScoutDrawnTask> Reserves => [.. Tasks.Where(t => t.Role == ScoutTaskRole.Reserve)];

    public IReadOnlyList<ScoutDrawnTask> Pilot => [.. Tasks.Where(t => t.Role == ScoutTaskRole.Pilot)];
}

/// <summary>The D10 selection order (plan §1 D10, §4 E2 S7), reproducing the 2026-10-12 screen rather than redoing it. ONE seed value: the
/// campaign string (<c>scouts-preliminary-2026-10-12</c>) — the same string every private commitment is bound to.
/// <list type="number">
/// <item>repositories with at least <see cref="MinAdmissible"/> ADMISSIBLE candidates, ordered by SHA-256 over UTF-8 <c>"{seed}|{repository}"</c>
/// (hex ascending) — <c>repository</c> being the SHORT name (<see cref="ScoutRepository.ShortName"/>, the name the screen listed);</item>
/// <item>inside a repository, candidates by SHA-256 of <c>"{seed}|{key}"</c> (<see cref="ScoutCandidate.Key"/>: <c>repo#number</c>, <c>repo@fixsha</c>);</item>
/// <item>the first <see cref="MainRepositories"/> repositories give ONE main task each (their first candidate) and a RESERVE (their next);
/// repositories 9 and 10 give the PILOT (their first candidate). Fewer eligible repositories give fewer tasks, never a refusal — the verb
/// says the set is short.</item>
/// </list>
/// <see cref="StableHash"/> is exactly that SHA-256, so the order is the same on any machine; input order plays no part.</summary>
public static class ScoutSelectionDraw
{
    public const int MinAdmissible = 5;

    public const int MainRepositories = 8;

    public const int PilotRepositories = 2;

    private sealed record Group(string Repository, string ShortName, IReadOnlyList<ScoutCandidateFacts> Candidates)
    {
        public bool Eligible => Candidates.Count >= MinAdmissible;

        public ScoutRepositoryRank Rank => new(Repository, ShortName, ScoutPublication.Repository(Candidates[0].Candidate), Candidates.Count, Eligible);
    }

    public static ScoutDraw Of(IReadOnlyList<ScoutCandidateFacts> admitted, string seed)
    {
        var groups = admitted.GroupBy(c => c.Candidate.Repository, StringComparer.Ordinal)
            .Select(g => new Group(g.Key, ScoutRepository.ShortName(g.Key), [.. g.OrderBy(c => Hash(seed, c.Candidate.Key), StringComparer.Ordinal).ThenBy(c => c.Candidate.Key, StringComparer.Ordinal)]))
            .OrderBy(g => Hash(seed, g.ShortName), StringComparer.Ordinal).ThenBy(g => g.Repository, StringComparer.Ordinal)
            .ToList();
        var eligible = groups.Where(g => g.Eligible).ToList();
        var main = eligible.Take(MainRepositories).ToList();
        var pilot = eligible.Skip(MainRepositories).Take(PilotRepositories).ToList();

        return new ScoutDraw(
            [.. eligible.Select(g => g.Rank), .. groups.Where(g => !g.Eligible).OrderBy(g => g.ShortName, StringComparer.Ordinal).Select(g => g.Rank)],
            [
                .. main.Select(g => new ScoutDrawnTask(g.Candidates[0], ScoutTaskRole.Main)),
                .. main.Select(g => new ScoutDrawnTask(g.Candidates[1], ScoutTaskRole.Reserve)),
                .. pilot.Select(g => new ScoutDrawnTask(g.Candidates[0], ScoutTaskRole.Pilot)),
            ]);
    }

    /// <summary>SHA-256 over UTF-8 <c>"{seed}|{name}"</c>, lower-case hex — the order of the 2026-10-12 screen.</summary>
    public static string Hash(string seed, string name) => StableHash.Of($"{seed}|{name}");
}
