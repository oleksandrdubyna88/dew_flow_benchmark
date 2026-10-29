using System.Collections.Immutable;
using Bench.Domain;

namespace ModelArms.Core.Experiment;

/// <summary>Which tool set a step runs under.</summary>
public enum StepMode { Write, Read, Gate }

/// <summary>The kinds of step a scheme is made of. The file's keys are <c>code review fix hreview gate split splitFix taskCode</c>.</summary>
public enum StepKind { Code, Review, Fix, HReview, Gate, Split, SplitFix, TaskCode }

/// <summary>One step kind as the experiment configures it: the model alias, the budget in minutes and the tool mode.</summary>
public sealed record StepSpec(StepKind Kind, string Model, int Minutes, StepMode Mode);

/// <summary>The one spelling of a step kind's key in the experiment file, both ways.</summary>
public static class StepKinds
{
    private static readonly ImmutableArray<(StepKind Kind, string Key)> Table =
    [
        (StepKind.Code, "code"),
        (StepKind.Review, "review"),
        (StepKind.Fix, "fix"),
        (StepKind.HReview, "hreview"),
        (StepKind.Gate, "gate"),
        (StepKind.Split, "split"),
        (StepKind.SplitFix, "splitFix"),
        (StepKind.TaskCode, "taskCode"),
    ];

    /// <summary>The key a step kind is written as.</summary>
    public static string Key(StepKind kind) => Table.Single(row => row.Kind == kind).Key;

    /// <summary>The step kind a key names; the match is exact, so <c>Code</c> is not <c>code</c>. A failure names every legal key.</summary>
    public static Outcome<StepKind> Parse(string key)
    {
        var row = Table.Where(r => string.Equals(r.Key, key, StringComparison.Ordinal)).ToList();
        return row.Count == 1
            ? Outcome<StepKind>.Success(row[0].Kind)
            : Outcome<StepKind>.Failure($"{key} is not a step kind ({string.Join(", ", Table.Select(r => r.Key))})");
    }
}
