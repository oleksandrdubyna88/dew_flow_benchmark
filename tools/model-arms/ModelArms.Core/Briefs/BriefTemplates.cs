using ModelArms.Core.Experiment;

namespace ModelArms.Core.Briefs;

/// <summary>Which shipped template file each step kind is briefed from. The names are the part-6 harness's, kept as they
/// were so the eight files stay byte for byte what that experiment ran; the console reads them from the experiment
/// root's <c>briefs</c> folder first and from the shipped folder second.</summary>
public static class BriefTemplates
{
    /// <summary>The template file name of a step kind.</summary>
    public static string NameOf(StepKind kind) =>
        kind switch
        {
            StepKind.Code => "1-code.template.md",
            StepKind.Review => "2-review.template.md",
            StepKind.Fix => "3-fix.template.md",
            StepKind.HReview => "hr-review-fix.template.md",
            StepKind.Gate => "gate.template.md",
            StepKind.Split => "x-split.template.md",
            StepKind.SplitFix => "x-split-fix.template.md",
            StepKind.TaskCode => "t2-coder.template.md",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "a step kind this build has no brief template for"),
        };

    /// <summary>Every template file name, one per step kind, derived from the kinds so a kind added without a template fails here.</summary>
    public static IReadOnlyList<string> All { get; } = [.. Enum.GetValues<StepKind>().Select(NameOf)];
}
