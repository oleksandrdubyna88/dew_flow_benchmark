using System.Text.RegularExpressions;

namespace Bench.Domain;

/// <summary>The shape every short, quotable identity in this system shares — a variant's name, a question
/// group's key, a reviewer's key.
/// <para>
/// One rule in one place, because these identities end up beside each other in a report and a column
/// heading. Lower case is the load-bearing part: a name differing only in case passes a unique index and
/// then reads as two things in one table, which is the defect each of these types exists to prevent.
/// </para></summary>
public static partial class Slug
{
    /// <summary>The rule, in the words a refusal uses. Written once so three refusals cannot describe
    /// three subtly different rules while enforcing one.</summary>
    public const string Rule =
        "3 to 64 characters of lower-case letters, digits and hyphens, starting with a letter or digit";

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{2,63}$")]
    private static partial Regex Pattern { get; }

    public static bool IsValid(string? value) => Pattern.IsMatch((value ?? string.Empty).Trim());

    public static string Clean(string? value) => (value ?? string.Empty).Trim();
}
