using Bench.Domain.Authoring;
using Bench.Domain.Suites;

namespace Bench.Domain.Bank;

/// <summary>One question of a test, as the test froze it: which group it was in, and at what ordinal.
/// <para>
/// This is the <c>run_questions</c> snapshot. It exists because group membership is versioned and a
/// finished report must not change retroactively — a question re-filed from <c>code-lookup</c> into
/// <c>adversarial</c> next month must not silently move last month's numbers into a different column.
/// </para></summary>
public sealed record SelectedQuestion(string QuestionId, GroupKey Group, int Ordinal);

/// <summary>A frozen selection: the suite a test measures, and the snapshot that says where each of its
/// questions came from.</summary>
public sealed record BankSelection(Suite Suite, IReadOnlyList<SelectedQuestion> Questions)
{
    public string Stamp => Suite.Stamp;
}

/// <summary>Turning a chosen set of bank questions into a frozen suite.
/// <para>
/// The suite machinery is untouched: a selection is promoted through
/// <see cref="AuthoringBatch.Promote"/> and frozen by <see cref="Suite.Freeze"/>, so a test created from
/// the bank names the same kind of hashed stamp as one created from a file, and a result cannot tell which
/// door its questions came through. That is the point — the stamp is the identity, and there must be
/// exactly one way to mint it.
/// </para></summary>
public static class BankFreeze
{
    public static Outcome<BankSelection> Freeze(string suiteId, IReadOnlyList<BankEntry> entries)
    {
        var duplicate = FirstDuplicateId(entries);

        if (duplicate.Length > 0)
        {
            // Before promotion, because Suite.With would accept both and the suite would then score one
            // question twice under one id — invisible in every number it produced afterwards.
            return Outcome<BankSelection>.Failure(
                $"question id '{duplicate}' appears twice in the selection — a suite cannot hold one id twice");
        }

        return AuthoringBatch.Promote(suiteId, [.. entries.Select(e => e.Question.AsCandidate())]).Match(
            suite => Outcome<BankSelection>.Success(new BankSelection(suite, Snapshot(entries))),
            Outcome<BankSelection>.Failure);
    }

    /// <summary>The snapshot rows, ordered as the operator selected them — group first, then the ordinal
    /// they quote.</summary>
    private static IReadOnlyList<SelectedQuestion> Snapshot(IReadOnlyList<BankEntry> entries) =>
        [.. entries
            .Where(e => e.Question.IsSelectable)
            .Select(e => new SelectedQuestion(e.Question.Question.Id, e.Group.Key, e.Question.Ordinal))
            .OrderBy(q => q.Group.Value, StringComparer.Ordinal)
            .ThenBy(q => q.Ordinal)];

    private static string FirstDuplicateId(IReadOnlyList<BankEntry> entries) =>
        entries
            .Where(e => e.Question.IsSelectable)
            .GroupBy(e => e.Question.Question.Id, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .FirstOrDefault() ?? string.Empty;
}
