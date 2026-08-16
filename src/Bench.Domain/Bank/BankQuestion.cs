using Bench.Domain.Authoring;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;

namespace Bench.Domain.Bank;

/// <summary>One question in the bank: everything the authoring domain already models, plus where it lives
/// and what it was authored against.
/// <para>
/// It carries a <see cref="Suites.Question"/> rather than copying its prompt, answer and expectations,
/// because that type is what a suite is made of and what a leg is scored against — a second shape here
/// would have to be kept in step with it forever, and the first divergence would be a question that scores
/// differently depending on which door it came through.
/// </para>
/// <para>
/// <b>Two identities, deliberately.</b> <see cref="Id"/> is the bank row; <c>Question.Id</c> is the
/// suite-facing string every cell and every result carries. Reports join on the second, which is why it is
/// unique across the bank rather than per group.
/// </para></summary>
/// <param name="Ordinal">The number an operator quotes — "group 1, questions 1–10". Stored rather than
/// derived from a sort, because a selection is described by it.</param>
/// <param name="CodeTaskJson">The code lane's task shape; empty for a reading question. Held as JSON here
/// on purpose: `PLAN_code_lane.md` owns that shape, and inventing columns for it now would be this
/// repository guessing at another plan's design.</param>
/// <param name="Seed">What the question was derived from, and WHEN that thing entered the world. The date
/// is the memorisation check's only input, and it is not the import date — a question seeded by material
/// older than a subject's training cutoff may be answerable from memory rather than from work.</param>
public sealed record BankQuestion(
    Guid Id,
    Guid GroupId,
    int Ordinal,
    TaskKind Kind,
    Question Question,
    string CodeTaskJson,
    AuthoringSource Source,
    string AuthorModel,
    QuestionSeed Seed,
    CandidateState State,
    RepoUrl TargetRepo,
    CommitSha AuthoredAt,
    DateTimeOffset CreatedAt)
{
    /// <summary>Admits a question to the bank under the AUTHORING domain's own rule.
    /// <para>
    /// The validation is <see cref="QuestionCandidate.Propose"/> rather than a second rule written here:
    /// a non-human source must name the model that authored it, and a question with no retrieval
    /// expectation has nothing to score against the code. Two admission rules would drift, and the one
    /// that drifted would be the one nobody was reading.
    /// </para></summary>
    public static Outcome<BankQuestion> Create(
        Guid groupId,
        int ordinal,
        TaskKind kind,
        Question question,
        string codeTaskJson,
        AuthoringSource source,
        string authorModel,
        QuestionSeed seed,
        RepoUrl targetRepo,
        CommitSha authoredAt,
        DateTimeOffset now) =>
        QuestionCandidate.Propose(source, authorModel, seed, question).Match(
            candidate => Outcome<BankQuestion>.Success(new BankQuestion(
                Guid.CreateVersion7(),
                groupId,
                ordinal,
                kind,
                question,
                (codeTaskJson ?? string.Empty).Trim(),
                source,
                candidate.AuthorModel,
                seed,
                CandidateState.Proposed,
                targetRepo,
                authoredAt,
                now)),
            Outcome<BankQuestion>.Failure);

    public bool IsSelectable => State == CandidateState.Accepted;

    /// <summary>Moves the question's own state. Reviews are a separate fact: three reviewers may each have
    /// marked it while the bank still holds it as Proposed, and collapsing the two would make "two of three
    /// approved" unrepresentable.</summary>
    public BankQuestion As(CandidateState state) => this with { State = state };

    /// <summary>The authoring view of this row, so a selection freezes through
    /// <see cref="AuthoringBatch.Promote"/> — the collision and empty-batch refusals already written and
    /// tested there, rather than a second promotion path that would have to grow its own.</summary>
    public QuestionCandidate AsCandidate() =>
        new(Question.Id, Source, AuthorModel, Seed, Question, State, string.Empty);
}

/// <summary>A bank question with the group it currently lives in — what a listing and a selection read.</summary>
public sealed record BankEntry(BankQuestion Question, QuestionGroup Group);

/// <summary>One question's move between groups, kept as history.
/// <para>
/// The current home is a column and the moves are rows, because a report over a finished test must not
/// change retroactively when somebody re-files a question: the per-test snapshot names the group as it was,
/// this history explains the difference, and a badge marks it.
/// </para></summary>
public sealed record GroupMove(Guid QuestionId, GroupKey From, GroupKey To, string Reason, DateTimeOffset At);
