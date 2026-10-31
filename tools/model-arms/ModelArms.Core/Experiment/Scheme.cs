using System.Text.RegularExpressions;
using Bench.Domain;

namespace ModelArms.Core.Experiment;

/// <summary>One entry of a scheme: a named step of a defined identity, or the SRS pipeline's <c>tasks</c> block (the split, one fix round, the batches).</summary>
public abstract record SchemeEntry
{
    private SchemeEntry() { }

    /// <summary>A step: its file name stem (<c>1-code</c>) and the id of the step it runs (<c>code</c>).</summary>
    public sealed record Step(string Name, StepId Id) : SchemeEntry;

    /// <summary>The SRS pipeline's tasks block; the ids of its three steps are <see cref="TaskLimits"/>'.</summary>
    public sealed record Tasks : SchemeEntry;
}

/// <summary>A scheme: its letters and its entries in order. An arm is a scheme name and a number (<c>SRS2</c>).</summary>
public sealed record Scheme(string Name, IReadOnlyList<SchemeEntry> Entries);

/// <summary>The one reading of an arm's name. Strict: <c>\z</c>, not <c>$</c>, so a trailing newline is not part of a name.</summary>
public static partial class ArmNames
{
    /// <summary>Whether <paramref name="arm"/> is an arm name: letters followed by digits and nothing else.</summary>
    public static bool IsArmName(string arm) => ArmPattern().IsMatch(arm);

    /// <summary>The scheme an arm belongs to: <c>SRS2</c> is <c>SRS</c>. An arm is letters followed by digits and nothing else.</summary>
    public static Outcome<string> SchemeOf(string arm)
    {
        var match = ArmPattern().Match(arm);
        return match.Success
            ? Outcome<string>.Success(match.Groups["scheme"].Value)
            : Outcome<string>.Failure($"'{arm}' is not an arm name — letters then a number, such as C1 or SRS2");
    }

    [GeneratedRegex(@"^(?<scheme>[A-Za-z]+)[0-9]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex ArmPattern();
}
