using Bench.Domain;

namespace Bench.Infrastructure.Gate;

/// <summary>Where a path REALLY is — canonical, with every symbolic link and junction on the way resolved — and
/// whether that is inside a root.
/// <para>
/// A lexical check (<c>GetFullPath</c> + a prefix) is fooled by one link: <c>attempt-1/tap</c> made a junction to
/// <c>C:\elsewhere</c> spells like a path under the root and writes outside it. So every EXISTING component of the
/// path is asked whether it is a link, and a link is replaced by its final target before the next component is
/// appended; what does not exist yet is appended as written, because it cannot be a link until someone creates it.
/// The comparison is separator-aware — <c>C:\root-other</c> is not under <c>C:\root</c> — and case-insensitive on
/// Windows, whose filesystem is.
/// </para>
/// <para>
/// What this cannot close is the window between the check and the write: a link created in that instant is not
/// seen. The artefact root is a directory of the operator's own, never a shared one, so the residual race needs
/// a hostile process already running as the operator — which could read the files directly.
/// </para></summary>
public static class ArtifactContainment
{
    /// <summary>A chain of links longer than this is a loop, not a layout.</summary>
    private const int MaxLinkHops = 40;

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>The canonical form of <paramref name="path"/>: full, links resolved, no trailing separator.</summary>
    public static Outcome<string> Resolve(string path) => ResolveFrom(path, hops: 0);

    /// <summary>Whether <paramref name="candidate"/> is <paramref name="root"/> or lies under it — both already
    /// canonical. Separator-aware: a sibling that merely shares the prefix is outside.</summary>
    public static bool IsWithin(string candidate, string root)
    {
        var trimmedRoot = Path.TrimEndingDirectorySeparator(root);

        return string.Equals(candidate, trimmedRoot, PathComparison)
            || candidate.StartsWith(trimmedRoot + Path.DirectorySeparatorChar, PathComparison);
    }

    /// <summary>Whether two canonical paths name one place, by the platform's own case rule.</summary>
    public static bool Same(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(a), Path.TrimEndingDirectorySeparator(b), PathComparison);

    private static Outcome<string> ResolveFrom(string path, int hops)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? string.Empty;
        var segments = full[root.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

        return segments.Aggregate(
            Outcome<(string Path, int Hops)>.Success((root, hops)),
            (walked, segment) => walked is Outcome<(string Path, int Hops)>.Ok ok ? Step(ok.Value.Path, segment, ok.Value.Hops) : walked)
            .Match(
                done => Outcome<string>.Success(Path.TrimEndingDirectorySeparator(done.Path)),
                Outcome<string>.Failure);
    }

    /// <summary>One component: appended as written, or — when it is a link — replaced by its resolved target.</summary>
    private static Outcome<(string Path, int Hops)> Step(string current, string segment, int hops)
    {
        var next = Path.Combine(current, segment);
        var link = LinkTarget(next);

        return (link.Length, hops >= MaxLinkHops) switch
        {
            (0, _) => Outcome<(string, int)>.Success((next, hops)),
            (_, true) => Outcome<(string, int)>.Failure($"more than {MaxLinkHops} links on the way to {next} — a loop, not a layout"),
            _ => ResolveFrom(Path.IsPathRooted(link) ? link : Path.Combine(current, link), hops + 1)
                .Match(resolved => Outcome<(string, int)>.Success((resolved, hops + 1)), Outcome<(string, int)>.Failure),
        };
    }

    /// <summary>The target a link points at, as written; empty when <paramref name="path"/> is not a link or does not
    /// exist. Junctions answer here as well as symbolic links — .NET reads both reparse tags — and a DANGLING link
    /// answers too, because a write through it would create its target wherever it points.</summary>
    private static string LinkTarget(string path)
    {
        try
        {
            FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
            return info.LinkTarget ?? string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}
