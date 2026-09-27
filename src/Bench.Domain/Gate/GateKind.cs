namespace Bench.Domain.Gate;

/// <summary>Which of the product's three review gates a run drove. Stored as a NAME wherever it is stored,
/// because an ordinal changes meaning the day somebody inserts a member and these rows are published.</summary>
public enum GateKind
{
    /// <summary><c>open</c> → <c>review_plan</c> over the plan text at the variant head.</summary>
    Plan,

    /// <summary>After a plan round passed: <c>review_code</c> over <c>base..variant</c> — the planted-defect diff.</summary>
    Code,

    /// <summary><c>review_feature</c> from the checkout at the variant, with the suite's epics and lessons.</summary>
    Feature,
}
