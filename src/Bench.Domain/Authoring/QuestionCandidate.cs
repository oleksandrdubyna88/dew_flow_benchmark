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
    /// <summary>Always UTC, normalised HERE rather than at each call site.
    /// <para>
    /// Found 2026-08-17 by the first authoring pass: an author writes <c>"at": "2026-05-14"</c>, a date with no
    /// zone, which deserialises to the READING MACHINE's offset — and Postgres accepts only UTC in a
    /// <c>timestamptz</c> parameter, so the insert failed. The bank import had the same hazard on the same
    /// line, unfixed, because nothing had yet imported a file whose seed date omitted a zone.
    /// </para>
    /// <para>
    /// In the domain type because every path that builds a seed is then correct at once, and no future one can
    /// forget. A seed's date is also compared against a model's training cutoff, and comparing two instants in
    /// different offsets is the other half of the same bug.
    /// </para></summary>
    public DateTimeOffset At { get; init; } = At.ToUniversalTime();

    /// <summary>A seed exactly as an author WROTE it — the calendar date kept as a calendar date.
    /// <para>
    /// <b>The one-day shift was ours, and this is where it happened.</b> The contract asks for a bare
    /// <c>"at": "2026-05-14"</c>; <c>System.Text.Json</c> reads a bare date as midnight in the READING machine's
    /// offset, and <see cref="At"/> then normalises that instant to UTC — so on a UTC+2 machine a correctly
    /// copied <c>2026-08-17</c> is stored as <c>2026-08-16T22:00Z</c> and reads back as the day before. Two
    /// authors were recorded on 2026-08-18 as "systematically dating a day early", and a reviewer rejected three
    /// questions for it; the arithmetic that produced that finding is this line. Measured 2026-08-19: a report
    /// saying <i>"the author dated it 2026-08-16"</i> is what a seed written <c>2026-08-17</c> produces.
    /// </para>
    /// <para>
    /// So the WALL date is stamped UTC rather than converted to it. A seed date is a day, never an instant: what
    /// a memorisation check compares against a training cutoff is which day the material entered the world, and
    /// no timezone of ours has an opinion about that. An author that wrote a time or an explicit <c>Z</c> keeps
    /// what it wrote, since for those the two readings agree.
    /// </para></summary>
    public static QuestionSeed Written(string kind, string reference, DateTimeOffset at) =>
        new(kind, reference, at == default ? default : new DateTimeOffset(at.DateTime, TimeSpan.Zero));

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
