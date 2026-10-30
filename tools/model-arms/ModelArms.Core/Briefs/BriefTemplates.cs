using System.Text.RegularExpressions;
using Bench.Domain;
using ModelArms.Core.Experiment;

namespace ModelArms.Core.Briefs;

/// <summary>Which template file a step's brief stem names. A stem is what a step's <c>brief</c> member says (<c>hA-trace</c> names <c>hA-trace.template.md</c>); the console
/// reads the file from the experiment root's <c>briefs</c> folder first and from the shipped folder second, so a stem is a name and never a path — no separator and no
/// <c>..</c> can reach outside the two folders.</summary>
public static partial class BriefTemplates
{
    /// <summary>The stems of the eleven templates the module ships: part 6's eight and part 7's three, the shell harness's bytes.</summary>
    public static IReadOnlyList<string> Shipped { get; } =
        ["1-code", "2-review", "3-fix", "gate", "hr-review-fix", "t2-coder", "x-split", "x-split-fix", "hA-trace", "hB-tests", "hfix"];

    /// <summary>The template file name of a stem, or why the text is no stem.</summary>
    public static Outcome<string> NameOf(string stem) =>
        StemPattern().IsMatch(stem)
            ? Outcome<string>.Success(stem + ".template.md")
            : Outcome<string>.Failure($"'{ExecutableRule.Printable(stem)}' is not a template stem — letters, digits and dashes, no separator and no '..' (hA-trace names hA-trace.template.md)");

    [GeneratedRegex(@"^[A-Za-z0-9-]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex StemPattern();
}
