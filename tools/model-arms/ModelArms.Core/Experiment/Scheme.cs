using System.Text.RegularExpressions;
using Bench.Domain;

namespace ModelArms.Core.Experiment;

/// <summary>One entry of a scheme: a named step of a kind, or the SRS pipeline's <c>tasks</c> block (the split, one fix round, the batches).</summary>
public abstract record SchemeEntry
{
    private SchemeEntry() { }

    /// <summary>A step: its file name stem (<c>1-code</c>) and its kind.</summary>
    public sealed record Step(string Name, StepKind Kind) : SchemeEntry;

    /// <summary>The SRS pipeline's tasks block.</summary>
    public sealed record Tasks : SchemeEntry;
}

/// <summary>A scheme: its letters and its entries in order. An arm is a scheme name and a number (<c>SRS2</c>).</summary>
public sealed record Scheme(string Name, IReadOnlyList<SchemeEntry> Entries);

/// <summary>The one reading of an arm's name.</summary>
public static partial class ArmNames
{
    /// <summary>The scheme an arm belongs to: <c>SRS2</c> is <c>SRS</c>. An arm is letters followed by digits and nothing else.</summary>
    public static Outcome<string> SchemeOf(string arm)
    {
        var match = ArmPattern().Match(arm);
        return match.Success
            ? Outcome<string>.Success(match.Groups["scheme"].Value)
            : Outcome<string>.Failure($"'{arm}' is not an arm name — letters then a number, such as C1 or SRS2");
    }

    [GeneratedRegex("^(?<scheme>[A-Za-z]+)[0-9]+$")]
    private static partial Regex ArmPattern();
}
