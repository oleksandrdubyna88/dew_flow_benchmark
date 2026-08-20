using Bench.Contracts;

namespace Bench.Ui;

/// <summary>One compute arm and the runs measured on it.</summary>
/// <param name="Declared">False for the single group holding runs whose engine echoed no backend. Not a
/// styling hint: an absence is not a competitor, and the page must be able to place it apart from the arms
/// rather than beside them.</param>
public sealed record ArmGroup(string Arm, bool Declared, IReadOnlyList<RunSummaryDto> Runs);

/// <summary>The arms side by side, and why they are not ranked.</summary>
/// <param name="Refusal">Always populated. Which refusal it is depends on what there is to refuse — "nothing
/// to compare" and "these cannot be compared" are opposite pieces of news, the same distinction the API
/// draws between 404 and 400.</param>
/// <param name="Differences">What actually varies across the runs on screen. The refusal is generic; this is
/// the actionable half, and it is empty when the runs genuinely differ in nothing but their arm.</param>
public sealed record ArmComparison(
    IReadOnlyList<ArmGroup> Groups,
    string Refusal,
    IReadOnlyList<string> Differences)
{
    /// <summary>How many arms were actually echoed. Runs that declared nothing are not one of them.</summary>
    public int Declared => Groups.Count(group => group.Declared);
}

/// <summary>Folding runs onto the arm they were measured on — and declining to rank them.
/// <para>
/// <b>The refusal is the feature.</b> The operator's question is whether the WSL sidecar beats the Windows
/// one, and the honest answer today is that this harness cannot say: the arm is recorded on the RUN, so
/// comparing two arms means comparing two runs, and cross-run comparison was excluded from the report on
/// purpose (<c>research/PLAN_run_report.md</c> §3.9) because two runs are only comparable inside a
/// <c>ComparisonScope</c> that nothing yet establishes. A console that ranked them anyway would manufacture
/// exactly the false winner the whole harness exists to catch — and it would be indistinguishable from a
/// correct answer afterwards.
/// </para>
/// <para>
/// Pure, and separate from the markup, for the reason <c>SampleWindow</c> was extracted: these are the only
/// decisions on the page, and they become testable with the xUnit already here instead of needing a
/// component-testing dependency to assert a table.
/// </para></summary>
public static class ArmGrouping
{
    public const string NoRuns = "no run has been measured yet — there is nothing to show.";

    public const string OneArm =
        "only one arm has been measured, so there is nothing to put beside it. Measure the same suite on a "
        + "second backend and both will appear here.";

    public const string NotComparable =
        "these arms are shown side by side and deliberately NOT ranked: two runs are only comparable inside "
        + "one comparison scope, and nothing establishes one yet. Read the numbers as two separate "
        + "measurements — a winner declared from them would be a claim this harness cannot support.";

    public static ArmComparison Compare(IReadOnlyList<RunSummaryDto> runs)
    {
        var groups = Group(runs);

        return new ArmComparison(groups, Refuse(groups), Differences(runs));
    }

    /// <summary>Declared arms in name order, the undeclared group last.
    /// <para>
    /// Last rather than first or hidden. Hiding it would drop runs off a page that claims to list them;
    /// mixing it in would let un-echoed rows read as an arm's own result, which is the error the
    /// three-state discipline exists to prevent.
    /// </para></summary>
    private static IReadOnlyList<ArmGroup> Group(IReadOnlyList<RunSummaryDto> runs) =>
        [.. runs
            .GroupBy(run => run.ComputeArm, StringComparer.Ordinal)
            .Select(group => new ArmGroup(
                group.Key,
                group.Key != ComputeArmLabel.NotDeclared,
                [.. group.OrderByDescending(run => run.CreatedAt)]))
            .OrderBy(group => group.Declared ? 0 : 1)
            .ThenBy(group => group.Arm, StringComparer.Ordinal)];

    private static string Refuse(IReadOnlyList<ArmGroup> groups) =>
        groups.Count switch
        {
            0 => NoRuns,
            1 => OneArm,
            _ => NotComparable,
        };

    /// <summary>What varies across the runs besides the arm — the part a reader can act on.
    /// <para>
    /// Only the two axes that make a difference meaningless on their own: a different suite is a different
    /// set of questions, and a different target is a different repository at a different commit. Either one
    /// explains a gap without any sidecar being involved.
    /// </para></summary>
    private static IReadOnlyList<string> Differences(IReadOnlyList<RunSummaryDto> runs) =>
        [.. Vary(runs, run => run.SuiteStamp, "suite"), .. Vary(runs, run => run.TargetCanonical, "target")];

    private static IEnumerable<string> Vary(
        IReadOnlyList<RunSummaryDto> runs, Func<RunSummaryDto, string> axis, string named)
    {
        var distinct = runs.Select(axis).Distinct(StringComparer.Ordinal).Count();

        return distinct > 1
            ? [$"the runs do not share one {named} — {distinct} distinct values, which explains a gap on its own"]
            : [];
    }
}
