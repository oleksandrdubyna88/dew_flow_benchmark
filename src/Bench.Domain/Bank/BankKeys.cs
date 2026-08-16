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
public sealed record Reviewer(Guid Id, ReviewerKey Key, string DisplayName, int Ordinal)
{
    public static Outcome<Reviewer> Create(string? key, string? displayName, int ordinal) =>
        ReviewerKey.Parse(key).Match(
            parsed => Outcome<Reviewer>.Success(
                new Reviewer(Guid.CreateVersion7(), parsed, Name(displayName, parsed), ordinal)),
            Outcome<Reviewer>.Failure);

    public static Outcome<Reviewer> Rehydrate(Guid id, string? key, string? displayName, int ordinal) =>
        ReviewerKey.Parse(key).Match(
            parsed => Outcome<Reviewer>.Success(new Reviewer(id, parsed, Name(displayName, parsed), ordinal)),
            Outcome<Reviewer>.Failure);

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
public sealed record QuestionReview(Guid QuestionId, Guid ReviewerId, ReviewVerdict Verdict, string Note, DateTimeOffset At);
