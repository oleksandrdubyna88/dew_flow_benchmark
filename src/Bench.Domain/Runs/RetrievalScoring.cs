using Bench.Domain.Retrieval;
using Bench.Domain.Suites;

namespace Bench.Domain.Runs;

/// <summary>One retrieval expectation and where — if anywhere — the returned list reached it.</summary>
/// <param name="Rank">1-based rank of the first hit that satisfied it; 0 for "nothing did". Zero rather
/// than a nullable because every reader here asks the same question first, and
/// <see cref="Surfaced"/> answers it once.</param>
public readonly record struct AnchorSurfacing(Expectation Expectation, int Rank)
{
    public bool Surfaced => Rank > 0;
}

/// <summary>Turning a returned list of hits into retrieval metrics.
/// <para>
/// Mechanical and free at this point: the hits are already stored, the anchors were authored with the
/// question, and every number below is arithmetic over the two. Which is the whole argument for storing
/// hits as rows — recomputing a metric must never mean re-running a campaign.
/// </para>
/// <para>
/// <b>A false positive here is worse than a miss.</b> These numbers are what a retrieval claim is made of,
/// so the matching rule refuses to be generous: a member is reached when the anchor's own
/// <c>Type.Member</c> matches the hit's, or when the hit's LINES cover the anchor's. Never by member name
/// alone — two members of one name in one file are commonplace, and a recall figure inflated by that would
/// be indistinguishable from a real one.
/// </para></summary>
public static class RetrievalScoring
{
    public const string Mrr = "Retrieval MRR";

    public const string FirstHitRank = "Retrieval first-hit rank";

    /// <summary>The cut-offs recall is reported at.
    /// <para>
    /// Two, not one, and both smaller than a typical limit of 20. A recall figure at the full limit answers
    /// "did the engine return it anywhere", which is the question the funnel already answers better; what a
    /// subject actually reads is the top of the list, and the measured difference between rank 3 and rank 15
    /// is the difference between a hit that informed the answer and one that did not.
    /// </para></summary>
    public static IReadOnlyList<int> Cutoffs => [5, 10];

    /// <summary>Which anchors the returned list reached, and at what rank.</summary>
    public static IReadOnlyList<AnchorSurfacing> Match(Question question, IReadOnlyList<RetrievedHit> hits) =>
        [.. question.RetrievalExpectations.Select(e => new AnchorSurfacing(e, RankOf(e, hits)))];

    /// <summary>The reading <see cref="AnswerScoring"/> takes: which anchors were surfaced, by canonical
    /// name. Reusing that scorer's anchor-recall metric rather than computing a second one — one
    /// definition of recall, in one place, so a report cannot print two.</summary>
    public static RetrievalObservation Observe(Question question, RetrievedContext context) =>
        context.WasPerformed
            ? new RetrievalObservation(
                true,
                [.. Match(question, context.Hits).Where(m => m.Surfaced).Select(m => m.Expectation.Anchor.Canonical)])
            : RetrievalObservation.None;

    /// <summary>The rank-sensitive metrics, as rows.
    /// <para>
    /// Empty for a leg that performed no retrieval, deliberately: a baseline arm keeps exactly the metric
    /// set it had before this lane existed, and <see cref="AnswerScoring"/> already states anchor recall's
    /// non-applicability there. Emitting zeroes instead would put the control arm into every retrieval
    /// aggregate at the bottom.
    /// </para></summary>
    public static IReadOnlyList<StoredMetric> Score(Question question, RetrievedContext context)
    {
        if (!context.WasPerformed)
        {
            return [];
        }

        var matched = Match(question, context.Hits);

        return matched.Count == 0
            ? [Nothing(Mrr), Nothing(FirstHitRank), .. Cutoffs.Select(k => Nothing(RecallAt(k)))]
            : [MrrMetric(matched), FirstHitMetric(matched), .. Cutoffs.Select(k => RecallMetric(matched, k))];
    }

    public static string RecallAt(int cutoff) => $"Retrieval recall@{cutoff}";

    /// <summary>Mean reciprocal rank over the question's anchors — a missed anchor contributes 0.
    /// <para>
    /// Averaged over the anchors the question NAMES rather than over the ones that were found: an engine
    /// that surfaces one of three anchors at rank 1 has not scored a perfect 1.0, and averaging over hits
    /// only is the arithmetic that would say it had.
    /// </para></summary>
    private static StoredMetric MrrMetric(IReadOnlyList<AnchorSurfacing> matched)
    {
        var mrr = matched.Sum(m => m.Surfaced ? 1.0 / m.Rank : 0.0) / matched.Count;

        return StoredMetric.Numeric(
            Mrr,
            mrr,
            $"{matched.Count(m => m.Surfaced)} of {matched.Count} anchor(s) surfaced, at rank(s) {Ranks(matched)}",
            failed: mrr <= 0,
            Rating(mrr));
    }

    /// <summary>The best rank any anchor reached. Text when none did: a rank of 0 would average into
    /// "very good", since a smaller rank is a better one.</summary>
    private static StoredMetric FirstHitMetric(IReadOnlyList<AnchorSurfacing> matched)
    {
        var surfaced = matched.Where(m => m.Surfaced).Select(m => m.Rank).DefaultIfEmpty(0).Min();

        return surfaced == 0
            ? StoredMetric.Text(
                FirstHitRank, "not surfaced",
                $"none of {matched.Count} anchor(s) appeared in the returned list", failed: true, "Unacceptable")
            : StoredMetric.Numeric(
                FirstHitRank, surfaced, $"the best-placed anchor came back at rank {surfaced}",
                failed: false, surfaced <= 3 ? "Exceptional" : surfaced <= 10 ? "Average" : "Unacceptable");
    }

    private static StoredMetric RecallMetric(IReadOnlyList<AnchorSurfacing> matched, int cutoff)
    {
        var within = matched.Count(m => m.Surfaced && m.Rank <= cutoff);
        var recall = (double)within / matched.Count;

        return StoredMetric.Numeric(
            RecallAt(cutoff),
            recall,
            $"{within} of {matched.Count} anchor(s) within the first {cutoff} hit(s)",
            failed: within == 0,
            Rating(recall));
    }

    private static StoredMetric Nothing(string name) =>
        StoredMetric.Text(name, "nothing to find", "the question names no anchors", failed: false, "Unknown");

    private static string Rating(double value) =>
        value >= 1 ? "Exceptional" : value > 0 ? "Average" : "Unacceptable";

    private static string Ranks(IReadOnlyList<AnchorSurfacing> matched) =>
        matched.Any(m => m.Surfaced)
            ? string.Join(", ", matched.Where(m => m.Surfaced).Select(m => m.Rank))
            : "none";

    private static int RankOf(Expectation expectation, IReadOnlyList<RetrievedHit> hits) =>
        hits.FirstOrDefault(hit => Satisfies(expectation, hit))?.Rank ?? 0;

    /// <summary>Whether one hit reaches one anchor.
    /// <para>
    /// The path must match in every case, and it is compared case-insensitively: the same suite is authored
    /// on Windows and replayed on a Linux runner, and a case difference there is a fact about a filesystem
    /// rather than about retrieval.
    /// </para></summary>
    private static bool Satisfies(Expectation expectation, RetrievedHit hit) =>
        AnchorMatching.SamePath(expectation.Anchor.FilePath, hit.RelativePath)
        && (expectation.Kind == ExpectationKind.File || ReachesMember(expectation.Anchor, hit));

    /// <summary>Whether a hit in the right file reaches the right MEMBER — by identity or by covering its
    /// lines.
    /// <para>
    /// Identity is <see cref="SourceAnchor.MemberKey"/> against <see cref="RetrievedHit.Member"/>: both are
    /// the readable <c>Type.Member</c> form. The hit's own <see cref="RetrievedHit.MemberKey"/> is
    /// deliberately not consulted — that string is the engine's internal identity and an anchor authored
    /// here does not share its format.
    /// </para>
    /// <para>
    /// The span fallback is what makes this work against an engine that names members differently, and it
    /// is the stricter of the two rather than the looser: a hit whose lines cover the anchor's lines has
    /// literally returned that code.
    /// </para></summary>
    private static bool ReachesMember(SourceAnchor anchor, RetrievedHit hit) =>
        AnchorMatching.SameMember(anchor.MemberKey, hit.Member)
        || (!anchor.Lines.IsWhole && hit.Covers(anchor.Lines.Start, anchor.Lines.End));
}
