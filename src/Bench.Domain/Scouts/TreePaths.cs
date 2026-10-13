namespace Bench.Domain.Scouts;

/// <summary>Path arithmetic for the path audit, written out rather than borrowed from <see cref="System.IO.Path"/>: a stream captured
/// on Windows is read on a Linux CI runner, and <c>Path.GetFullPath</c> answers differently on each. Both separators are accepted; a
/// path is ABSOLUTE when it starts with a drive (<c>C:/</c>, <c>C:\</c>), a UNC prefix (<c>\\</c>, <c>//</c>) or <c>/</c>; <c>.</c> and
/// <c>..</c> segments are folded, and a <c>..</c> that climbs above the root is refused. A drive-letter path compares ignoring case,
/// as Windows does; any other compares exactly.</summary>
public static class TreePaths
{
    private static readonly char[] Wildcards = ['*', '?', '[', '{'];

    public static bool IsAbsolute(string path) =>
        path.StartsWith('/') || path.StartsWith('\\') || path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] is '/' or '\\';

    /// <summary><paramref name="path"/> against <paramref name="baseDirectory"/> — an absolute path stands alone — folded to one form
    /// with <c>/</c> separators. Empty when the path climbs above its root: an unresolvable target is not a safe one.</summary>
    public static string Resolve(string baseDirectory, string path)
    {
        var joined = IsAbsolute(path) ? path : $"{baseDirectory}/{path}";

        return Fold(joined.Replace('\\', '/'));
    }

    /// <summary>The non-wildcard head of a glob pattern — <c>../*.txt</c> → <c>..</c>, <c>src/**/x.cs</c> → <c>src</c>,
    /// <c>**/*.cs</c> → empty. What a pattern can reach is under its head.</summary>
    public static string GlobHead(string pattern)
    {
        var segments = pattern.Replace('\\', '/').Split('/');
        var fixedCount = segments.TakeWhile(s => s.IndexOfAny(Wildcards) < 0).Count();
        var head = string.Join('/', segments.Take(Math.Min(fixedCount, segments.Length - 1)));

        return IsAbsolute(pattern) && !IsAbsolute(head) ? RootOf(pattern) : head;
    }

    /// <summary>The root an absolute pattern starts from — <c>C:/</c> or <c>/</c> — when its head is nothing more than that root.</summary>
    private static string RootOf(string pattern) => IsDrive(pattern) ? pattern[..3] : pattern[..1];

    /// <summary>Whether <paramref name="candidate"/> is the root or under it. Both must be resolved (<see cref="Resolve"/>) first.</summary>
    public static bool IsInside(string root, string candidate)
    {
        var comparison = IsDrive(root) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var trimmedRoot = root.TrimEnd('/');

        return candidate.Length > 0 && trimmedRoot.Length > 0
            && (string.Equals(candidate.TrimEnd('/'), trimmedRoot, comparison) || candidate.StartsWith(trimmedRoot + "/", comparison));
    }

    private static bool IsDrive(string path) => path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':';

    private static string Fold(string path)
    {
        var (prefix, remainder) = Split(path);
        var kept = new List<string>();

        foreach (var segment in remainder.Split('/').Where(s => s.Length > 0 && s != "."))
        {
            if (segment != "..")
            {
                kept.Add(segment);
                continue;
            }

            if (kept.Count == 0)
            {
                return string.Empty;
            }

            kept.RemoveAt(kept.Count - 1);
        }

        return prefix + string.Join('/', kept);
    }

    /// <summary>The root a path keeps whatever its <c>..</c> segments say: <c>C:/</c>, <c>//</c> (UNC) or <c>/</c>.</summary>
    private static (string Prefix, string Remainder) Split(string path) =>
        (IsDrive(path), path.StartsWith("//", StringComparison.Ordinal), path.StartsWith('/')) switch
        {
            (true, _, _) => ($"{char.ToUpperInvariant(path[0])}:/", path[2..]),
            (_, true, _) => ("//", path[2..]),
            (_, _, true) => ("/", path[1..]),
            _ => (string.Empty, path),
        };
}
