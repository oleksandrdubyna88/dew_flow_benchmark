using Bench.Domain.Trace;

namespace Bench.Domain.Scouts;

/// <summary>What the assessor decided about one <c>CLAIMS</c> line (§3.7). <see cref="Unresolved"/> is a claim too vague to check against the
/// code: it is counted and reported, and it is NOT in the wrong-claim denominator — a rate over decided claims only, so an assessor that
/// cannot decide can never dilute a team's wrong claims.</summary>
public enum ScoutClaimVerdict
{
    Wrong,
    Right,
    Unresolved,
}

/// <summary>Which claim a verdict is about: the cell, the scout (its ordinal = its focus area), and the claim's place among the scout's
/// parseable claims (1-based). Stable across re-reads of the same stream, so an assessed claim is never assessed twice.</summary>
public sealed record ScoutClaimKey(Guid CellId, int Ordinal, int Index);

/// <summary>One assessed claim as the store keeps it — never the statement's text, only its SHA-256, the repository-relative path it names
/// (empty when it named none the tree could hold), the cited line, the verdict, and who assessed it under which rubric.</summary>
public sealed record ScoutAssessedClaim(
    ScoutClaimKey Key,
    Guid RunId,
    string Path,
    CapturedCount Line,
    string StatementSha256,
    ScoutClaimVerdict Verdict,
    string AssessorModel,
    string RubricSha256,
    DateTimeOffset AssessedAt);

/// <summary>The verdict words, spelled once — the assessor's schema and the store both read them.</summary>
public static class ScoutClaimVerdicts
{
    public static string Word(ScoutClaimVerdict verdict) => verdict switch
    {
        ScoutClaimVerdict.Wrong => "wrong",
        ScoutClaimVerdict.Right => "right",
        _ => "unresolved",
    };

    public static IReadOnlyList<string> Words { get; } = [.. Enum.GetValues<ScoutClaimVerdict>().Select(Word)];

    public static Outcome<ScoutClaimVerdict> Parse(string? word) =>
        Enum.GetValues<ScoutClaimVerdict>().Where(v => Word(v) == (word ?? string.Empty).Trim().ToLowerInvariant()).ToList() is [var verdict]
            ? Outcome<ScoutClaimVerdict>.Success(verdict)
            : Outcome<ScoutClaimVerdict>.Failure($"'{word}' is not a claim verdict — {string.Join(", ", Words)}");

    /// <summary>A scout's or a team's tally of its verdicts: wrong, and decided (wrong + right) as the denominator.</summary>
    public static ClaimTally Tally(IEnumerable<ScoutClaimVerdict> verdicts)
    {
        var list = verdicts.ToList();

        return new ClaimTally(list.Count(v => v == ScoutClaimVerdict.Wrong), list.Count(v => v != ScoutClaimVerdict.Unresolved));
    }
}
