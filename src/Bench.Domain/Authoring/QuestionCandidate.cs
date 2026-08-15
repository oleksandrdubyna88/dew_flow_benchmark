using Bench.Domain.Suites;

namespace Bench.Domain.Authoring;

/// <summary>Where a candidate question came from. Combinable per batch rather than exclusive — a batch may
/// draw on several at once, which is why deduplication is a first-class concern rather than an afterthought.</summary>
public enum AuthoringSource
{
    /// <summary>A merged pull request: its description is the gold answer, its changed files the anchors.
    /// The backbone, because the answer key is written by the project's own maintainers and cannot be
    /// tuned to whatever our retrieval happens to be good at.</summary>
    RepositoryHistory,

    /// <summary>An issue plus the commit that fixed it. For a fix task this IS the ground truth.</summary>
    BugsAndTests,

    /// <summary>Generated from a member's own description and checked to return that member. Unlimited and
    /// free, but it measures the index against itself and cannot say whether a HUMAN's question would find
    /// it — a metric, never a set on its own.</summary>
    Synthetic,

    /// <summary>A person wrote it. Highest quality, rarest case, same shape as the rest.</summary>
    Human,
}

/// <summary>What a question was derived from, and when that thing entered the world.
/// <para>
/// The date is the load-bearing part. A question seeded by material older than a subject's training cutoff
/// may be answerable from memory rather than from work, and memory is not what this benchmark measures.
/// </para></summary>
public sealed record QuestionSeed(string Kind, string Reference, DateTimeOffset At)
{
    public static QuestionSeed PullRequest(string reference, DateTimeOffset mergedAt) =>
        new("pull-request", reference, mergedAt);

    public static QuestionSeed Issue(string reference, DateTimeOffset openedAt) => new("issue", reference, openedAt);

    public static QuestionSeed Member(string targetKey, DateTimeOffset indexedAt) => new("member", targetKey, indexedAt);

    public static QuestionSeed Person(string who, DateTimeOffset authoredAt) => new("human", who, authoredAt);

    public string Describe => $"{Kind} {Reference} ({At:yyyy-MM-dd})";
}

public enum CandidateState
{
    /// <summary>A source produced it. Nothing has vouched for it yet.</summary>
    Proposed,

    /// <summary>A reviewer took responsibility for it. Only these may enter a suite.</summary>
    Accepted,

    /// <summary>Refused, with the reason kept — a rejected candidate is evidence about the source that
    /// produced it, and deleting it loses the only record of what that source tends to get wrong.</summary>
    Rejected,
}

/// <summary>One proposed question on its way into a suite.
/// <para>
/// A source produces CANDIDATES, not suite members. The review state exists because a suite must contain
/// nothing nobody vouched for, and because authoring at volume means most of what arrives is a draft.
/// </para></summary>
public sealed record QuestionCandidate(
    string Id,
    AuthoringSource Source,
    string AuthorModel,
    QuestionSeed Seed,
    Question Question,
    CandidateState State,
    string ReviewNote)
{
    /// <summary>Proposes a candidate. <paramref name="authorModel"/> is empty only for
    /// <see cref="AuthoringSource.Human"/> — for every other source the model that wrote the question is
    /// recorded, because a set's ceiling becomes its author's ceiling and that has to be readable later.</summary>
    public static Outcome<QuestionCandidate> Propose(
        AuthoringSource source, string authorModel, QuestionSeed seed, Question question)
    {
        if (source != AuthoringSource.Human && string.IsNullOrWhiteSpace(authorModel))
        {
            return Outcome<QuestionCandidate>.Failure(
                $"a {source} candidate must record the model that authored it — a set's ceiling becomes its author's ceiling");
        }

        if (question.RetrievalExpectations.Count == 0)
        {
            return Outcome<QuestionCandidate>.Failure(
                $"candidate '{question.Id}' has no retrieval expectation — there would be nothing to score against the code");
        }

        return Outcome<QuestionCandidate>.Success(new QuestionCandidate(
            question.Id, source, authorModel.Trim(), seed, question, CandidateState.Proposed, string.Empty));
    }

    public Outcome<QuestionCandidate> Accept(string note) =>
        State == CandidateState.Proposed
            ? Outcome<QuestionCandidate>.Success(this with { State = CandidateState.Accepted, ReviewNote = note })
            : Outcome<QuestionCandidate>.Failure($"candidate '{Id}' is {State}, not Proposed");

    public Outcome<QuestionCandidate> Reject(string why) =>
        string.IsNullOrWhiteSpace(why)
            ? Outcome<QuestionCandidate>.Failure("a rejection needs a reason — it is the only record of what a source gets wrong")
            : Outcome<QuestionCandidate>.Success(this with { State = CandidateState.Rejected, ReviewNote = why });
}
