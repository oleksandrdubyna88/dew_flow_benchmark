namespace Bench.Delivered;

/// <summary>One file in a diff, with its raw added and removed line counts.</summary>
public sealed record ChangedFile(string Path, int Added, int Removed);

/// <summary>One file's added and removed CONTENT, with the leading marker stripped.</summary>
public sealed record FileHunks(string Path, IReadOnlyList<string> Added, IReadOnlyList<string> Removed);

/// <summary>Pure unified-diff parsing. Everything derived from a diff comes from here rather than from a
/// model, so it is context that can be trusted outright.
///
/// <para>Ported from <c>scoreMeter · Diffs/DiffParser.cs</c>, with the file splitter it shared with
/// <c>LocMetrics</c> extracted rather than written twice — the source had two copies of the same
/// <c>diff --git</c> walk and they could have drifted.</para>
/// </summary>
public static class DiffParser
{
    private const string Header = "diff --git ";

    private const string PathMarker = " b/";

    /// <summary>The changed-files table: one row per file with its RAW counts, before any cleaning.</summary>
    public static IReadOnlyList<ChangedFile> ChangedFiles(string diff, string pathPrefix = "") =>
        [.. Hunks(diff).Select(f => new ChangedFile(pathPrefix + f.Path, f.Added.Count, f.Removed.Count))];

    /// <summary>Each file's added and removed content lines. Both sides count: the source measured that
    /// deletions carry real signal, which is why uncleaned churn outperformed the additions-only figure.</summary>
    public static IEnumerable<FileHunks> Hunks(string diff)
    {
        string? path = null;
        var added = new List<string>();
        var removed = new List<string>();

        foreach (var line in diff.Split('\n'))
        {
            if (line.StartsWith(Header, StringComparison.Ordinal))
            {
                if (path is not null)
                {
                    yield return new FileHunks(path, added, removed);
                }

                path = PathOf(line);
                added = [];
                removed = [];
                continue;
            }

            if (path is not null)
            {
                Collect(line, added, removed);
            }
        }

        if (path is not null)
        {
            yield return new FileHunks(path, added, removed);
        }
    }

    /// <summary>The whole text of each file's section, for a cleaner that keeps or drops it wholesale.</summary>
    public static IEnumerable<(string Path, string Text)> Sections(string diff)
    {
        string? path = null;
        var buffer = new List<string>();

        foreach (var line in diff.Split('\n'))
        {
            if (line.StartsWith(Header, StringComparison.Ordinal))
            {
                if (path is not null)
                {
                    yield return (path, string.Join('\n', buffer));
                }

                path = PathOf(line);
                buffer = [line];
                continue;
            }

            if (path is not null)
            {
                buffer.Add(line);
            }
        }

        if (path is not null)
        {
            yield return (path, string.Join('\n', buffer));
        }
    }

    /// <summary>The path plus its suffixes after dropping leading segments. Anchors and diff paths are
    /// rooted differently (repo-relative against package-relative), so a SUFFIX match is what makes "the
    /// same file" detectable across the two.</summary>
    public static IReadOnlyList<string> PathVariants(string path)
    {
        var parts = path.Split('/');

        return
        [
            .. new[] { path, string.Join('/', parts.Skip(1)), string.Join('/', parts.Skip(2)) }
                .Where(v => v.Length > 0),
        ];
    }

    private static string PathOf(string header)
    {
        var marker = header.IndexOf(PathMarker, StringComparison.Ordinal);

        return marker >= 0 ? header[(marker + PathMarker.Length)..].TrimEnd('\r') : string.Empty;
    }

    /// <summary><c>+++</c> and <c>---</c> are the file markers, not content.</summary>
    private static void Collect(string line, List<string> added, List<string> removed)
    {
        if (line.StartsWith('+') && !line.StartsWith("+++", StringComparison.Ordinal))
        {
            added.Add(line[1..]);
        }
        else if (line.StartsWith('-') && !line.StartsWith("---", StringComparison.Ordinal))
        {
            removed.Add(line[1..]);
        }
    }
}
