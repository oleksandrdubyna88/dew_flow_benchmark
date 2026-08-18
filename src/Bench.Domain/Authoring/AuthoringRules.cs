using Bench.Domain.Suites;

namespace Bench.Domain.Authoring;

/// <param name="Key">What the colliding candidates have in common.</param>
public sealed record CandidateCollision(string Key, IReadOnlyList<string> CandidateIds)
{
    public string Describe => $"{CandidateIds.Count} candidates share {Key}: {string.Join(", ", CandidateIds)}";
}

/// <summary>Finding candidates that are probably the same question.
/// <para>
/// It <b>flags and never drops</b>, and that is a decision rather than caution. Two sources routinely
/// produce a question about the same member — and two genuinely different questions about one member are
/// also normal. No key can tell those apart, so silently keeping the first would quietly discard real
/// coverage. A collision is a thing a reviewer resolves.
/// </para>
/// <para>
/// The key is built from the ANCHORS, not from the prompt: rewording defeats a text key immediately, while
/// two questions that point at the same lines are the case actually worth looking at.
/// </para></summary>
public static class Dedup
{
    public static string CollisionKey(QuestionCandidate candidate) =>
        string.Join(
            "+",
            candidate.Question.RetrievalExpectations
                .Select(e => e.Anchor.Canonical)
                .Order(StringComparer.Ordinal));

    /// <summary>The same idea WITHOUT the line span — file and member only.
    /// <para>
    /// Measured 2026-08-18, and it is why this exists: one author over two calls produced
    /// <c>StoreNaming.KindOf@33-49</c> and <c>StoreNaming.KindOf@45-49</c> in the same group, plus
    /// <c>RrfFusion.Fuse@17-28</c> and <c>@27-28</c>. Both pairs are two questions about one member, and
    /// <see cref="CollisionKey"/> cannot see either, because a span is part of it. Three authors writing a third
    /// of a group each will produce this constantly — the most obvious member in a repository is obvious to all
    /// three.
    /// </para></summary>
    public static string MemberKey(QuestionCandidate candidate) =>
        string.Join(
            "+",
            candidate.Question.RetrievalExpectations
                .Select(e => $"{e.Anchor.FilePath}#{e.Anchor.MemberKey}")
                .Order(StringComparer.Ordinal));

    /// <summary>The member-level key of a question already stored, so a new candidate can be compared against
    /// the bank rather than only against its own batch.</summary>
    public static string MemberKey(Suites.Question question) =>
        string.Join(
            "+",
            question.RetrievalExpectations
                .Select(e => $"{e.Anchor.FilePath}#{e.Anchor.MemberKey}")
                .Order(StringComparer.Ordinal));

    public static IReadOnlyList<CandidateCollision> Find(IReadOnlyList<QuestionCandidate> candidates) =>
        [.. candidates
            .Where(c => c.State != CandidateState.Rejected)
            .GroupBy(CollisionKey, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => new CandidateCollision(g.Key, [.. g.Select(c => c.Id).Order(StringComparer.Ordinal)]))
            .OrderBy(c => c.Key, StringComparer.Ordinal)];
}

public enum MemorisationRisk
{
    /// <summary>The seed postdates this subject's cutoff. The subject cannot have read it.</summary>
    Clear,

    /// <summary>The seed predates the cutoff: this subject may answer from memory rather than from work.</summary>
    MayRecall,

    /// <summary>No cutoff was declared for the subject, so nothing can be said. Not "clear".</summary>
    Unknown,
}

/// <summary>Whether a question risks measuring memory instead of work.
/// <para>
/// This is a property of a <b>(question, subject)</b> pair, not of a question — exactly like
/// discrimination. The same seed is safe against a model trained last year and unsafe against one trained
/// last month, so gating at authoring time would either block good questions or bless bad ones depending
/// on which subject somebody had in mind.
/// </para>
/// <para>
/// So authoring records the seed's date and the REPORT raises the flag. And there is a mechanical check
/// that beats any declaration: run the question in the no-tools lane. A subject that answers correctly
/// with no access to the tree answered from its weights.
/// </para></summary>
public static class Memorisation
{
    public static MemorisationRisk For(QuestionSeed seed, IReadOnlyDictionary<string, DateTimeOffset> cutoffByModel, string model) =>
        cutoffByModel.TryGetValue(model, out var cutoff)
            ? seed.At > cutoff ? MemorisationRisk.Clear : MemorisationRisk.MayRecall
            : MemorisationRisk.Unknown;

    /// <summary>Every subject in a comparison that may recall this question, so the report can name them
    /// rather than quietly averaging them in.</summary>
    public static IReadOnlyList<string> AtRisk(
        QuestionSeed seed, IReadOnlyDictionary<string, DateTimeOffset> cutoffByModel, IReadOnlyCollection<string> models) =>
        [.. models
            .Where(m => For(seed, cutoffByModel, m) != MemorisationRisk.Clear)
            .Order(StringComparer.Ordinal)];
}

/// <summary>Turning a reviewed batch into a frozen suite version.</summary>
public static class AuthoringBatch
{
    /// <summary>Promotes the accepted candidates into a frozen suite.
    /// <para>
    /// Refused while any collision is unresolved. A suite that quietly contains two questions about the
    /// same lines double-counts that member in every score it ever produces, and nothing downstream can
    /// detect it — which makes this the last place the problem is cheap.
    /// </para></summary>
    public static Outcome<Suite> Promote(string suiteId, IReadOnlyList<QuestionCandidate> candidates)
    {
        var accepted = candidates.Where(c => c.State == CandidateState.Accepted).ToList();

        if (accepted.Count == 0)
        {
            return Outcome<Suite>.Failure(
                $"no accepted candidates for suite '{suiteId}' — a suite must contain nothing nobody vouched for");
        }

        var collisions = Dedup.Find(accepted);

        if (collisions.Count > 0)
        {
            return Outcome<Suite>.Failure(
                $"{collisions.Count} unresolved collision(s): {string.Join(" | ", collisions.Select(c => c.Describe))}");
        }

        return accepted
            .Aggregate(
                Outcome<Suite>.Success(Suite.Draft(suiteId)),
                (draft, candidate) => draft.Match(s => s.With(candidate.Question), Outcome<Suite>.Failure))
            .Match(s => s.Freeze(), Outcome<Suite>.Failure);
    }

    /// <summary>What a batch is made of, for the record that travels with the version: which sources
    /// contributed and which model authored what.</summary>
    public static IReadOnlyList<string> Provenance(IReadOnlyList<QuestionCandidate> candidates) =>
        [.. candidates
            .Where(c => c.State == CandidateState.Accepted)
            .GroupBy(c => (c.Source, c.AuthorModel))
            .Select(g => $"{g.Key.Source}{(g.Key.AuthorModel.Length > 0 ? $" via {g.Key.AuthorModel}" : "")}: {g.Count()}")
            .Order(StringComparer.Ordinal)];
}
