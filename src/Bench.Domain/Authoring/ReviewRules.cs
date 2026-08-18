using Bench.Domain.Bank;

namespace Bench.Domain.Authoring;

/// <summary>Whether a reviewer may mark a question its own model wrote.
/// <para>
/// Refused by default, and the reason is not squeamishness: self-review is the cheapest way to manufacture
/// agreement, and it leaves no trace afterwards. A bank holding three approvals per question looks reviewed
/// whether or not one model wrote and blessed every one of them, so the refusal has to happen at the moment
/// the mark would be written — nothing downstream can reconstruct it.
/// </para>
/// <para>
/// The comparison is on the resolved MODEL, never on the slot key or the registry key. Two reviewer slots
/// bound to two registry rows that both resolve to <c>claude-sonnet-4-6</c> are one opinion; comparing the
/// keys would call that pairing clean.
/// </para></summary>
public static class SelfReview
{
    /// <summary>The invariant fact about one model in several reviewer slots, true whether or not a pass is
    /// running — so a listing of the slots can say it too.
    /// <para>
    /// Written down once, here, because the operator's decision of 2026-08-18 was to run three Claude slots
    /// against a Claude author — a deliberate compromise while there is one verified CLI author, and one that
    /// stops being visible the moment the sentence lives only in a chat log.
    /// </para></summary>
    public const string OneOpinion =
        "three instances of one model are one opinion sampled three times, not three reviewers, and every "
        + "batch marked this way has to be reported that way";

    /// <summary>What allowing it costs, in the words the RUN that allowed it reports.</summary>
    public const string Cost = $"self-review is allowed for this pass: {OneOpinion}";

    /// <summary>Nothing to say, the cost, or a refusal naming the pairing.</summary>
    /// <param name="reviewerModel">The reviewer slot's resolved model id. Empty for a human slot, which can
    /// never be a self-review.</param>
    public static Outcome<string> Check(string reviewerKey, string reviewerModel, string authorModel, bool allowed)
    {
        if (!Same(reviewerModel, authorModel))
        {
            return Outcome<string>.Success(string.Empty);
        }

        return allowed
            ? Outcome<string>.Success(Cost)
            : Outcome<string>.Failure(
                $"'{reviewerKey}' is {reviewerModel} and would be reviewing its own authorship — a model must not "
                + "vouch for what it wrote. Pass --allow-self-review to take the mark anyway, at the cost the "
                + "flag prints");
    }

    private static bool Same(string reviewerModel, string authorModel) =>
        reviewerModel.Trim().Length > 0
        && string.Equals(reviewerModel.Trim(), authorModel.Trim(), StringComparison.OrdinalIgnoreCase);
}

public enum PromotionKind
{
    /// <summary>Every configured reviewer approved. The only state that makes a question selectable.</summary>
    Accept,

    /// <summary>At least one reviewer refused it. A rejection outranks any number of approvals — it is a
    /// specific defect somebody named, and no count of approvals answers it.</summary>
    Reject,

    /// <summary>Neither yet: some configured reviewer has not marked it. Not a promotion and not a defect.</summary>
    Wait,
}

public sealed record PromotionDecision(PromotionKind Kind, string Reason);

/// <summary>What a set of marks means for a question's own state.
/// <para>
/// <b>The strict rule, and no threshold knob.</b> Every configured reviewer must have approved. A majority
/// rule would be a quality claim nobody here has measured — "two of three is enough" is exactly the kind of
/// sentence this project requires a measurement for — so the one rule that needs no evidence is the one
/// implemented, and a different threshold stays an operator decision recorded per batch.
/// </para></summary>
public static class Promotion
{
    public static PromotionDecision Decide(
        IReadOnlyList<Reviewer> configured, IReadOnlyList<QuestionReview> marks)
    {
        if (configured.Count == 0)
        {
            // Vacuous truth is the trap worth naming: "every configured reviewer approved" is technically true
            // of no reviewers at all, and would promote a whole machine-written bank on an empty table.
            return new PromotionDecision(
                PromotionKind.Wait,
                "no reviewer is configured, so nothing can be promoted — a reviewer is a row, and an empty "
                + "table approves everything if you let it");
        }

        var refused = Named(configured, marks.Where(m => m.Verdict == ReviewVerdict.Rejected));

        if (refused.Count > 0)
        {
            return new PromotionDecision(PromotionKind.Reject, $"rejected by {string.Join(", ", refused)}");
        }

        var marked = marks.Select(m => m.ReviewerId).ToHashSet();
        var missing = configured.Where(r => !marked.Contains(r.Id)).Select(r => r.Key.Value).Order(StringComparer.Ordinal).ToList();

        return missing.Count > 0
            ? new PromotionDecision(
                PromotionKind.Wait,
                $"no mark yet from {string.Join(", ", missing)} — the strict rule promotes only when every "
                + "configured reviewer has approved")
            : new PromotionDecision(
                PromotionKind.Accept, $"approved by all {configured.Count} configured reviewer(s)");
    }

    private static List<string> Named(IReadOnlyList<Reviewer> configured, IEnumerable<QuestionReview> marks)
    {
        var byId = configured.ToDictionary(r => r.Id, r => r.Key.Value);

        return [.. marks
            .Select(m => byId.TryGetValue(m.ReviewerId, out var key) ? key : m.ReviewerId.ToString())
            .Order(StringComparer.Ordinal)];
    }
}
