using System.Text.RegularExpressions;

namespace Bench.Delivered;

/// <param name="ExcludedPaths">What was removed, NAMED. A dropped path is reported rather than deleted
/// silently — a prompt that quietly lost a file is a prompt whose size nobody can account for.</param>
public sealed record CleanedDiff(string Diff, IReadOnlyList<string> ExcludedPaths, bool Truncated);

/// <summary>Removes no-value files from a diff before it reaches a prompt, and records what was removed.
///
/// <para>Paths excluded from the SIZE count are still shown to the model, deliberately: a point anchored in
/// generated code or proven by a test must remain findable. Only the <see cref="PathFate.Dropped"/> fate —
/// vendored trees, lockfiles, build output — is cut from the text.</para>
/// </summary>
public static class DiffCleaner
{
    /// <summary>The dry-run harness's limit, inherited from <c>build_diffs.py</c>'s <c>MAX_DIFF_CHARS</c>.</summary>
    public const int MaxDiffChars = 340_000;

    /// <summary>The exact suffix the reference appends. Byte-identical on purpose: it lands INSIDE the
    /// prompt, so a different marker changes the prompt hash and breaks parity with the measured run. Note
    /// the U+2026 ellipsis, not three dots.</summary>
    public const string TruncationMarker = "\n…[truncated at 340000 characters]";

    public static CleanedDiff Clean(string diff, IReadOnlyList<string>? extraExclusionGlobs = null)
    {
        var extra = (extraExclusionGlobs ?? [])
            .Select(g => new Regex(PathCategoryTable.GlobToPattern(g), RegexOptions.CultureInvariant))
            .ToList();

        var kept = new List<string>();
        var excluded = new List<string>();

        foreach (var (path, text) in DiffParser.Sections(diff))
        {
            var drop = PathCategoryTable.Fate(path) == PathFate.Dropped
                || extra.Any(r => r.IsMatch(path));

            (drop ? excluded : kept).Add(drop ? path : text);
        }

        return Truncate(string.Join('\n', kept), excluded);
    }

    /// <summary>Cuts the assembled diff at the character limit exactly as the reference does: the whole
    /// string is truncated MID-FILE if necessary, rather than dropping trailing files whole.
    /// <para>
    /// The nicer-looking alternative — keeping only complete files — was measured as a divergence: it
    /// produces different prompt text for any diff over the cap, and therefore a different prompt hash than
    /// the run the calibration came from. Parity with the measured baseline outranks tidiness here.
    /// </para></summary>
    private static CleanedDiff Truncate(string diff, IReadOnlyList<string> excluded) =>
        diff.Length <= MaxDiffChars
            ? new CleanedDiff(diff, excluded, Truncated: false)
            : new CleanedDiff(diff[..MaxDiffChars] + TruncationMarker, excluded, Truncated: true);
}
