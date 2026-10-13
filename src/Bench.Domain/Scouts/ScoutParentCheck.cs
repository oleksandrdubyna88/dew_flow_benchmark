namespace Bench.Domain.Scouts;

/// <summary>P verified per merge style over the mirror's facts (plan §3.1, E2 S7): the candidates file SAYS which commit P is; the mirror says
/// how the fix actually landed, and the two must agree before the candidate is admitted. A mismatch excludes the candidate BY ID with one of
/// the reason CODES below — never a sha, a url or a path, so a private candidate's exclusion line is publishable as it is.
/// <list type="bullet">
/// <item><c>merge-commit</c> — the fix has TWO parents and P is the first;</item>
/// <item><c>pr-squash</c>, <c>local-squash</c> — the fix has ONE parent and P is it;</item>
/// <item><c>pr-rebase</c> — P is the RECORDED <c>baseRefOid</c>, and that base is an ancestor of the fix. Never <c>fix^1</c>: for a rebase merge
/// the first parent is a commit inside the PR (6 of the first 13 family picks were rebase-merged), and a tree at it would already hold part of
/// the answer.</item>
/// </list></summary>
public static class ScoutParentCheck
{
    public const string Prefix = "parent-check:";

    public const string MergeCommitNeedsTwoParents = Prefix + "merge-commit-needs-two-parents";

    public const string SquashNeedsOneParent = Prefix + "squash-needs-one-parent";

    public const string NotFirstParent = Prefix + "p-is-not-the-first-parent-of-fix";

    public const string RebaseNeedsBaseRef = Prefix + "pr-rebase-needs-a-base-ref-oid";

    public const string NotBaseRef = Prefix + "p-is-not-the-recorded-base-ref-oid";

    public const string BaseRefNotAncestor = Prefix + "base-ref-is-not-an-ancestor-of-fix";

    /// <summary>The reason code, or empty when P is what the merge style says it must be.</summary>
    public static string Refusal(ScoutCandidate candidate, ScoutParentFacts facts) => candidate.MergeStyle switch
    {
        ScoutMergeStyle.MergeCommit => Linear(candidate, facts, parents: 2, MergeCommitNeedsTwoParents),
        ScoutMergeStyle.PrRebase => Rebased(candidate, facts),
        _ => Linear(candidate, facts, parents: 1, SquashNeedsOneParent),
    };

    /// <summary>A fix with exactly <paramref name="parents"/> parents whose FIRST parent is P.</summary>
    private static string Linear(ScoutCandidate candidate, ScoutParentFacts facts, int parents, string countCode) =>
        (facts.FixParents.Count == parents, facts.FixParents.Count > 0 && Same(facts.FixParents[0].Value, candidate.Parent.Value)) switch
        {
            (false, _) => countCode,
            (_, false) => NotFirstParent,
            _ => string.Empty,
        };

    private static string Rebased(ScoutCandidate candidate, ScoutParentFacts facts) =>
        (candidate.BaseRef.IsRecorded, Same(candidate.BaseRef.Oid, candidate.Parent.Value), facts.BaseRefIsAncestorOfFix) switch
        {
            (false, _, _) => RebaseNeedsBaseRef,
            (_, false, _) => NotBaseRef,
            (_, _, false) => BaseRefNotAncestor,
            _ => string.Empty,
        };

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
