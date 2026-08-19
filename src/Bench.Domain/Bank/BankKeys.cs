namespace Bench.Domain.Bank;

/// <summary>A question group's key — <c>code-lookup</c>, <c>semantic-intent</c>, <c>adversarial</c>.
/// <para>
/// Groups are DATA, not an enum, and that is the decision the plan makes rather than an accident: a sixth
/// group arrives as a row, and every report that groups by this key keeps working. The key is what a
/// per-test snapshot stores, so it must stay quotable years after the group itself was renamed or split.
/// </para></summary>
public sealed record GroupKey
{
    private GroupKey(string value) => Value = value;

    public string Value { get; }

    public static Outcome<GroupKey> Parse(string? value)
    {
        var trimmed = Slug.Clean(value);

        return Slug.IsValid(trimmed)
            ? Outcome<GroupKey>.Success(new GroupKey(trimmed))
            : Outcome<GroupKey>.Failure($"'{trimmed}' is not a usable group key — {Slug.Rule}");
    }

    public override string ToString() => Value;
}

/// <summary>A reviewer's key — <c>claude</c>, <c>codex</c>, <c>gemini</c>, and whoever comes fourth.
/// <para>
/// A row rather than an enum member for one measured reason: the questions page renders one checkmark
/// column per reviewer, and a fourth reviewer must cost one INSERT rather than a migration, a redeploy and
/// a schema every stored review has to be replayed against.
/// </para></summary>
public sealed record ReviewerKey
{
    private ReviewerKey(string value) => Value = value;

    public string Value { get; }

    public static Outcome<ReviewerKey> Parse(string? value)
    {
        var trimmed = Slug.Clean(value);

        return Slug.IsValid(trimmed)
            ? Outcome<ReviewerKey>.Success(new ReviewerKey(trimmed))
            : Outcome<ReviewerKey>.Failure($"'{trimmed}' is not a usable reviewer key — {Slug.Rule}");
    }

    public override string ToString() => Value;
}

/// <summary>One named group of the bank, in the order an operator reads them.</summary>
/// <param name="Ordinal">Where the group sits in the listing — "group 1, questions 1–10" is how the
/// operator actually refers to this material, so the number is stored rather than derived from a sort.</param>
public sealed record QuestionGroup(Guid Id, GroupKey Key, string Title, int Ordinal)
{
    public static Outcome<QuestionGroup> Create(string? key, string? title, int ordinal) =>
        GroupKey.Parse(key).Match(
            parsed => Outcome<QuestionGroup>.Success(
                new QuestionGroup(Guid.CreateVersion7(), parsed, Named(title, parsed), ordinal)),
            Outcome<QuestionGroup>.Failure);

    public static Outcome<QuestionGroup> Rehydrate(Guid id, string? key, string? title, int ordinal) =>
        GroupKey.Parse(key).Match(
            parsed => Outcome<QuestionGroup>.Success(new QuestionGroup(id, parsed, Named(title, parsed), ordinal)),
            Outcome<QuestionGroup>.Failure);

    private static string Named(string? title, GroupKey key)
    {
        var trimmed = (title ?? string.Empty).Trim();
        return trimmed.Length > 0 ? trimmed : key.Value;
    }
}

/// <summary>Who marks questions. Ordered, because the checkmark columns must appear in the same order on
/// every page and "whatever the database returned" is not an order.</summary>
/// <param name="ModelKey">The registry row that ANSWERS for this slot, or empty for a person.
/// <para>
/// A slot without this is a slot nothing can automate: the self-review rule compares a reviewer's model with
/// the question's author model, so "who is reviewer-2" has to be a stored fact rather than a flag typed on one
/// command line. Found 2026-08-18, with three seeded slots that named nobody.
/// </para></param>
/// <param name="Enabled">Whether this slot still takes part. A retired one is skipped by the vetting pass and
/// is not counted among the reviewers a question waits for.
/// <para>
/// <b>Added 2026-08-19, because a slot turned out to be irreversible.</b> Three slots were configured, one of
/// them bound to a model that failed on every launch, and the strict promotion rule requires every eligible
/// reviewer to approve — so every question in the bank waited forever on a mark that could never arrive, and
/// nothing could undo it. Unbinding the model does not help: a slot with no model is a PERSON, still eligible
/// and still silent. The registry had already solved exactly this for models — disabled, never deleted, so the
/// history stays readable — and this is that same move. Marks a retired slot already made are kept: a judgement
/// was made, and deleting it would rewrite what the bank was told.
/// </para></param>
public sealed record Reviewer(
    Guid Id, ReviewerKey Key, string DisplayName, int Ordinal, string ModelKey = "", bool Enabled = true)
{
    /// <summary>Trimmed here so no call site has to. An empty binding means a person marks this slot, which is
    /// the default a fresh row gets — binding a model is a deliberate act with a cost attached.</summary>
    public string ModelKey { get; init; } = (ModelKey ?? string.Empty).Trim();

    /// <summary>No model answers for this slot, so no pass can complete it.</summary>
    public bool IsHuman => ModelKey.Length == 0;

    /// <summary>A fresh slot is enabled: retiring one is a deliberate act, exactly as disabling a model is.</summary>
    public static Outcome<Reviewer> Create(string? key, string? displayName, int ordinal, string? modelKey = "") =>
        ReviewerKey.Parse(key).Match(
            parsed => Outcome<Reviewer>.Success(
                new Reviewer(Guid.CreateVersion7(), parsed, Name(displayName, parsed), ordinal, modelKey ?? "")),
            Outcome<Reviewer>.Failure);

    public static Outcome<Reviewer> Rehydrate(
        Guid id, string? key, string? displayName, int ordinal, string? modelKey = "", bool enabled = true) =>
        ReviewerKey.Parse(key).Match(
            parsed => Outcome<Reviewer>.Success(
                new Reviewer(id, parsed, Name(displayName, parsed), ordinal, modelKey ?? "", enabled)),
            Outcome<Reviewer>.Failure);

    public Reviewer Enable(bool enabled) => this with { Enabled = enabled };

    private static string Name(string? displayName, ReviewerKey key)
    {
        var trimmed = (displayName ?? string.Empty).Trim();
        return trimmed.Length > 0 ? trimmed : key.Value;
    }
}

public enum ReviewVerdict
{
    Approved,

    /// <summary>Refused, with the note kept — a rejection is evidence about the source that produced the
    /// question, and the note is the only record of what that source tends to get wrong.</summary>
    Rejected,
}

/// <summary>One reviewer's mark on one question. At most one per pair, enforced by a unique index rather
/// than by a read-then-write two sessions can both pass.</summary>
/// <param name="ModelId">The model that actually made this mark, or empty when a person did — or when the mark
/// predates this column.
/// <para>
/// Added 2026-08-18 because the slot turned out not to be provenance. <c>reviewer-2</c> held
/// <c>claude-sonnet-4-6</c> for thirty-three marks and <c>gpt-5.6-terra</c> for the next seven, so "reviewer-2
/// rejected 7" needed a timestamp filter to be honest — a footnote nobody would remember. A slot says WHICH
/// COLUMN a mark appears in; only this says who made it.
/// </para>
/// <para>
/// Marks written before this column stay EMPTY rather than being backfilled. Which model held a slot in the
/// past is knowable from a session's memory, not from the database, and writing a recollection into a column
/// would make an unverifiable claim look like a record.
/// </para></param>
public sealed record QuestionReview(
    Guid QuestionId, Guid ReviewerId, ReviewVerdict Verdict, string Note, DateTimeOffset At, string ModelId = "")
{
    /// <summary>Nothing recorded which model made this mark — a person, or a mark older than the column.</summary>
    public bool Unattributed => ModelId.Length == 0;
}
