using System.Text.RegularExpressions;

namespace Bench.Domain.Scouts;

/// <summary>Recall for one team: which files of the ground truth G its KEY FILES named.</summary>
public sealed record RecallResult(IReadOnlyList<string> Found, int Total)
{
    public decimal Recall => Total == 0 ? 0m : (decimal)Found.Count / Total;
}

/// <summary>The recall matcher (§3.6, hand-checked in §3.7). A KEY FILES entry is first NORMALISED — backticks and quotes stripped,
/// a <c>:42</c>, <c>:42-50</c> or <c>#L42</c> citation dropped, <c>\</c> read as <c>/</c>, a leading <c>./</c> and the tree's own absolute
/// root removed — then RESOLVED against the files that exist in the tree at P:
/// <list type="bullet">
/// <item>an exact path (ignoring case, as a Windows checkout does) is that file;</item>
/// <item>otherwise a path that is a whole-segment SUFFIX of exactly one file — a bare basename included — is that file;</item>
/// <item>a suffix two files share is AMBIGUOUS and matches nothing: <c>Program.cs</c> in a tree of six is not a finding.</item>
/// </list>
/// Without the tree's file list only exact paths can match: uniqueness cannot be proven, and an unproven match inflates recall.</summary>
public static partial class RecallMatcher
{
    public static RecallResult Match(IReadOnlyList<string> keyFiles, IReadOnlyList<string> groundTruth, IReadOnlyList<string> treeFiles, string treeRoot)
    {
        var tree = treeFiles.Select(Normalise).Where(f => f.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var named = keyFiles.Select(entry => Normalise(entry, treeRoot)).Where(e => e.Length > 0).SelectMany(entry => Resolve(entry, tree)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var truth = groundTruth.Select(Normalise).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        return new RecallResult([.. truth.Where(named.Contains)], truth.Count);
    }

    /// <summary>The file one normalised, non-empty entry names, or none — an exact path, else a unique whole-segment suffix.</summary>
    public static IReadOnlyList<string> Resolve(string entry, IReadOnlyList<string> tree)
    {
        var exact = tree.Where(f => string.Equals(f, entry, StringComparison.OrdinalIgnoreCase)).ToList();
        var bySuffix = tree.Where(f => f.EndsWith("/" + entry, StringComparison.OrdinalIgnoreCase)).ToList();

        return (exact.Count, bySuffix.Count) switch
        {
            ( > 0, _) => [exact[0]],
            (_, 1) => [bySuffix[0]],
            (_, > 1) => [],
            // Not in the tree's list (or no list): only the path as written can still match G, exactly.
            _ => [entry],
        };
    }

    /// <summary>An entry as a repository-relative path with <c>/</c> separators, no citation, no decoration.</summary>
    public static string Normalise(string entry) => Normalise(entry, string.Empty);

    public static string Normalise(string entry, string treeRoot)
    {
        var bare = Citation().Replace(entry.Trim().Trim('`', '"', '\'', '*', ' '), string.Empty).Replace('\\', '/');
        var root = treeRoot.Replace('\\', '/').TrimEnd('/');
        var relative = root.Length > 0 && bare.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase) ? bare[(root.Length + 1)..] : bare;

        return LeadingDot().Replace(relative, string.Empty).TrimEnd('/');
    }

    [GeneratedRegex(@"(?::\d+(?:-\d+)?|#L\d+(?:-L?\d+)?)$")]
    private static partial Regex Citation();

    [GeneratedRegex(@"^(?:\./)+")]
    private static partial Regex LeadingDot();
}
