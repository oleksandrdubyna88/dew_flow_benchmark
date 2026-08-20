namespace Bench.Domain.Runs;

/// <summary>Which slice of a fix task a leg runs (todo/PLAN_investigate_vs_implement.md §3.1).
/// <para>
/// The slices exist so investigation quality and implementation quality are separately measurable: a
/// composite leg cannot say whether a model investigated brilliantly and fumbled the diff, or guessed
/// the diagnosis and got lucky editing. An AXIS rather than three task kinds, because the arms run the
/// SAME question — same statement, same base commit, same hidden tests — and three kinds would be three
/// bank entries that must agree byte for byte and will not.
/// </para></summary>
public enum FixArm
{
    /// <summary>The whole task: Investigate → Fix → Verify → Judge. The default, and what every leg
    /// planned before this axis existed ran.</summary>
    Full,

    /// <summary>Understand the defect and stop: Investigate → Judge. The diagnosis is the deliverable.
    /// This arm builds nothing and runs no tests, which is what frees it from the code lane's
    /// isolation gate (PLAN_code_lane.md §4.2) — investigation can be measured unattended while the
    /// sandbox work is still open.</summary>
    InvestigateOnly,

    /// <summary>Produce the fix with the reference diagnosis handed in the prompt: Fix → Verify →
    /// Judge. The investigation is held fixed, so what is measured is the hands alone.</summary>
    ImplementOnly,
}

/// <summary>The arm's canonical token — what a leg identity appends and a report prints.</summary>
public static class FixArms
{
    public static string Canonical(this FixArm arm) =>
        arm switch
        {
            FixArm.Full => "full",
            FixArm.InvestigateOnly => "investigate-only",
            FixArm.ImplementOnly => "implement-only",
            _ => throw new ArgumentOutOfRangeException(nameof(arm), arm, "unknown fix arm"),
        };
}
