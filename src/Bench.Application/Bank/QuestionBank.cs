using Bench.Domain;
using Bench.Domain.Authoring;
using Bench.Domain.Bank;

namespace Bench.Application.Bank;

/// <summary>Which questions a listing or a selection is asking for.
/// <para>
/// Empty and zero mean "unbounded" rather than being nullable: this travels from a CLI where an unset flag
/// is the normal case, and a query object whose every field must be unwrapped before it can be read is a
/// query object that will be misread.
/// </para></summary>
/// <param name="GroupKey">One group, or every group when empty.</param>
/// <param name="FromOrdinal">The first ordinal to include; 0 means "from the start". The operator quotes
/// ranges — "group 1, questions 1–10" — so the query takes them in exactly that form.</param>
/// <param name="ToOrdinal">The last ordinal to include; 0 means "to the end".</param>
/// <param name="OnlyAccepted">A SELECTION always sets this. Only questions somebody vouched for may enter
/// a test, and the filter lives here so no caller can forget it.</param>
public sealed record BankQuery(string GroupKey = "", int FromOrdinal = 0, int ToOrdinal = 0, bool OnlyAccepted = false)
{
    public static BankQuery Selection(string groupKey, int from, int to) => new(groupKey, from, to, OnlyAccepted: true);

    public string Describe =>
        (GroupKey.Length > 0 ? GroupKey : "every group")
        + (FromOrdinal > 0 || ToOrdinal > 0 ? $" {FromOrdinal}–{(ToOrdinal > 0 ? ToOrdinal : "end")}" : string.Empty)
        + (OnlyAccepted ? ", accepted only" : string.Empty);
}

/// <summary>The durable question bank — the persistence the authoring domain was written for and never
/// got.
/// <para>
/// Groups and reviewers are ROWS here, not enum members: a sixth group and a fourth reviewer must each
/// cost one insert, because the questions page renders a column per reviewer and a report groups by the
/// group key. Uniqueness of a key, and one mark per reviewer per question, are the database's job rather
/// than a read-then-write two sessions can both pass.
/// </para></summary>
public interface IQuestionBank
{
    Task<Outcome<QuestionGroup>> AddGroupAsync(QuestionGroup group, CancellationToken cancellationToken);

    Task<Outcome<Reviewer>> AddReviewerAsync(Reviewer reviewer, CancellationToken cancellationToken);

    /// <summary>Stores a question in the state it arrives in. An import may carry an already-reviewed
    /// question; nothing here decides on its behalf.</summary>
    Task<Outcome<BankQuestion>> AddAsync(BankQuestion question, CancellationToken cancellationToken);

    Task<Outcome<IReadOnlyList<QuestionGroup>>> GroupsAsync(CancellationToken cancellationToken);

    Task<Outcome<IReadOnlyList<Reviewer>>> ReviewersAsync(CancellationToken cancellationToken);

    /// <summary>The questions a query names, each with the group it lives in NOW. A test freezes what this
    /// returns; the snapshot is what reports read afterwards.</summary>
    Task<Outcome<IReadOnlyList<BankEntry>>> QuestionsAsync(BankQuery query, CancellationToken cancellationToken);

    /// <summary>One reviewer's mark on one question, replacing that reviewer's previous mark. Another
    /// reviewer's mark is never touched — "two of three approved" has to stay representable.</summary>
    Task<Outcome<QuestionReview>> ReviewAsync(
        string questionId,
        string reviewerKey,
        ReviewVerdict verdict,
        string note,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    Task<Outcome<IReadOnlyList<QuestionReview>>> ReviewsAsync(
        IReadOnlyList<Guid> questionIds, CancellationToken cancellationToken);

    /// <summary>Moves the question's own state — the bank's verdict, distinct from any reviewer's mark.</summary>
    Task<Outcome<BankQuestion>> SetStateAsync(
        string questionId, CandidateState state, CancellationToken cancellationToken);

    /// <summary>Re-files a question, and records that it moved. The history is what lets a report explain
    /// why a finished test's snapshot disagrees with the bank as it is today.</summary>
    Task<Outcome<GroupMove>> MoveAsync(
        string questionId, string toGroupKey, string reason, DateTimeOffset at, CancellationToken cancellationToken);

    Task<Outcome<IReadOnlyList<GroupMove>>> MovesAsync(string questionId, CancellationToken cancellationToken);
}

/// <summary>The per-test snapshot of what was selected — written when a test is created, read by every
/// per-group report afterwards.</summary>
public interface IRunQuestionStore
{
    Task<Outcome<int>> SaveAsync(
        Guid runId, IReadOnlyList<SelectedQuestion> questions, CancellationToken cancellationToken);

    Task<Outcome<IReadOnlyList<SelectedQuestion>>> ForRunAsync(Guid runId, CancellationToken cancellationToken);
}
