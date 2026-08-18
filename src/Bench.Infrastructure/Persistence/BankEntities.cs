using Bench.Domain.Authoring;
using Bench.Domain.Bank;
using Bench.Domain.Runs;

namespace Bench.Infrastructure.Persistence;

/// <summary>A named group of the bank. A row rather than an enum member: a sixth group is an INSERT, and
/// every report that groups by this key keeps working.</summary>
public sealed class QuestionGroupRow
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public int Ordinal { get; set; }
}

/// <summary>Who marks questions. Also a row, for the same reason and one more: the questions page renders
/// a checkmark column per reviewer, and a fourth reviewer must not be a migration.</summary>
public sealed class ReviewerRow
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public int Ordinal { get; set; }

    /// <summary>The registry key of the model that answers for this slot, or empty for a person. Stored
    /// because the self-review rule needs "who is reviewer-2" to be a fact, not a command-line flag.</summary>
    public string ModelKey { get; set; } = string.Empty;
}

/// <summary>One question of the bank.
/// <para>
/// <see cref="QuestionId"/> is the suite-facing identity — what a cell, a result and the per-test snapshot
/// all carry — and it is unique across the whole bank rather than per group, because that is the key every
/// report joins on. <see cref="Id"/> is this row.
/// </para></summary>
public sealed class BankQuestionRow
{
    public Guid Id { get; set; }

    public Guid GroupId { get; set; }

    /// <summary>The number an operator quotes when selecting — "group 1, questions 1–10".</summary>
    public int Ordinal { get; set; }

    public string QuestionId { get; set; } = string.Empty;

    public TaskKind Kind { get; set; }

    /// <summary>Text, not jsonb, and deliberately: the code lane's shape is owned by another plan, and an
    /// empty value has to stay distinguishable from an empty OBJECT rather than being normalised into one.</summary>
    public string CodeTaskJson { get; set; } = string.Empty;

    public string Prompt { get; set; } = string.Empty;

    public string ReferenceAnswer { get; set; } = string.Empty;

    public string ExpectationsJson { get; set; } = "[]";

    public string TargetRepoUrl { get; set; } = string.Empty;

    public string AuthoredAtCommit { get; set; } = string.Empty;

    public AuthoringSource SourceKind { get; set; }

    public string AuthorModel { get; set; } = string.Empty;

    /// <summary>What the question was derived from, and WHEN that material entered the world. The date is
    /// the memorisation check's only input and is not the import date — a question seeded before a
    /// subject's training cutoff may be answerable from memory rather than from work.</summary>
    public string SeedKind { get; set; } = string.Empty;

    public string SeedReference { get; set; } = string.Empty;

    public DateTimeOffset SeedAt { get; set; }

    public CandidateState State { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public QuestionGroupRow? Group { get; set; }
}

/// <summary>One reviewer's mark on one question. At most one per pair — the unique index is what holds
/// when two sessions mark the same question at the same moment.</summary>
public sealed class QuestionReviewRow
{
    public Guid Id { get; set; }

    public Guid QuestionId { get; set; }

    public Guid ReviewerId { get; set; }

    public ReviewVerdict Verdict { get; set; }

    public string Note { get; set; } = string.Empty;

    public DateTimeOffset At { get; set; }
}

/// <summary>A question's move between groups, kept so a finished report can EXPLAIN why its snapshot
/// disagrees with the bank as it is today, instead of silently disagreeing.</summary>
public sealed class QuestionGroupMoveRow
{
    public Guid Id { get; set; }

    public Guid QuestionId { get; set; }

    public string FromGroup { get; set; } = string.Empty;

    public string ToGroup { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset At { get; set; }
}

/// <summary>The per-test snapshot of the selection: which questions a run measures, and which group each
/// was in WHEN THE TEST WAS CREATED.
/// <para>
/// Group membership is versioned in the bank, so a per-group report that read the bank live would move
/// last month's numbers into a different column the moment somebody re-filed a question. Reports read this
/// snapshot; a toggle regroups by the current bank; a badge marks the difference.
/// </para></summary>
public sealed class RunQuestionRow
{
    public Guid RunId { get; set; }

    public string QuestionId { get; set; } = string.Empty;

    public string GroupKey { get; set; } = string.Empty;

    public int Ordinal { get; set; }

    public RunRow? Run { get; set; }
}
