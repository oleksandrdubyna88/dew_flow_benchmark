namespace Bench.Domain.Scouts;

/// <summary>The D10 selection's LAZY read (operator, 2026-10-14). The draw (<see cref="ScoutSelectionDraw"/>) is fixed by hashes the candidate
/// list alone gives, so a candidate only has to be read off its mirror when it can still change the draw:
/// <list type="bullet">
/// <item>repositories are read in the draw's order (<see cref="ScoutSelectionDraw.ReadingOrder"/>), each one's candidates in theirs;</item>
/// <item>a repository is read until its <see cref="ScoutSelectionDraw.MinAdmissible"/>th ADMISSIBLE candidate — the draw takes its first two,
/// and eligibility only needs the count to reach the bar;</item>
/// <item>the read stops once <see cref="RepositoriesNeeded"/> repositories are eligible — every later one would rank after them.</item>
/// </list>
/// The facts it returns, admitted and drawn, give the SAME tasks in the SAME roles as reading every candidate
/// (<c>ScoutLazySelectionTests</c>); what it cannot give is the full exclusion statistics, which <c>--read-all</c> keeps.</summary>
public static class ScoutLazySelection
{
    public const int RepositoriesNeeded = ScoutSelectionDraw.MainRepositories + ScoutSelectionDraw.PilotRepositories;

    public static async Task<Outcome<IReadOnlyList<ScoutCandidateFacts>>> ReadAsync(
        IReadOnlyList<ScoutCandidate> candidates, string seed, DateOnly laterCutoff, Func<ScoutCandidate, Task<Outcome<ScoutCandidateFacts>>> read)
    {
        var facts = new List<ScoutCandidateFacts>();
        var eligible = 0;

        foreach (var repository in ScoutSelectionDraw.ReadingOrder(candidates, seed))
        {
            if (eligible >= RepositoriesNeeded)
            {
                break;
            }

            var group = await ReadRepositoryAsync(repository, laterCutoff, read);

            if (group is Outcome<IReadOnlyList<ScoutCandidateFacts>>.Fail fail)
            {
                return Outcome<IReadOnlyList<ScoutCandidateFacts>>.Failure(fail.Reason);
            }

            var members = ((Outcome<IReadOnlyList<ScoutCandidateFacts>>.Ok)group).Value;
            facts.AddRange(members);
            eligible += ScoutCandidates.Admit(members, laterCutoff).Admitted.Count >= ScoutSelectionDraw.MinAdmissible ? 1 : 0;
        }

        return Outcome<IReadOnlyList<ScoutCandidateFacts>>.Success(facts);
    }

    /// <summary>Every candidate, in the listed order — <c>--read-all</c>, for the full exclusion statistics. A read that fails stops everything.</summary>
    public static Task<Outcome<IReadOnlyList<ScoutCandidateFacts>>> ReadEveryAsync(
        IReadOnlyList<ScoutCandidate> candidates, Func<ScoutCandidate, Task<Outcome<ScoutCandidateFacts>>> read) =>
        ReadRepositoryAsync(candidates, DateOnly.MinValue, read, stopAtBar: false);

    /// <summary>One repository's candidates in the draw's order, until the fifth admissible one; a read that fails stops everything.</summary>
    private static async Task<Outcome<IReadOnlyList<ScoutCandidateFacts>>> ReadRepositoryAsync(
        IReadOnlyList<ScoutCandidate> repository, DateOnly laterCutoff, Func<ScoutCandidate, Task<Outcome<ScoutCandidateFacts>>> read, bool stopAtBar = true)
    {
        var facts = new List<ScoutCandidateFacts>();
        var admissible = 0;

        foreach (var candidate in repository.TakeWhile(_ => !stopAtBar || admissible < ScoutSelectionDraw.MinAdmissible))
        {
            var outcome = await read(candidate);

            if (outcome is Outcome<ScoutCandidateFacts>.Fail fail)
            {
                return Outcome<IReadOnlyList<ScoutCandidateFacts>>.Failure(fail.Reason);
            }

            var one = ((Outcome<ScoutCandidateFacts>.Ok)outcome).Value;
            facts.Add(one);
            admissible += ScoutCandidates.Admit([one], laterCutoff).Admitted.Count;
        }

        return Outcome<IReadOnlyList<ScoutCandidateFacts>>.Success(facts);
    }
}
