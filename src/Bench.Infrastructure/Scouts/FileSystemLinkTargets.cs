using Bench.Domain.Scouts;

namespace Bench.Infrastructure.Scouts;

/// <summary>The path audit's link port on a real disk (§3.2): a path resolved by the audit is walked segment by segment from its root,
/// and every segment that is a symbolic link or a junction is replaced by its FINAL target — so a junction inside the tree that points
/// out of it is judged by where it points. A segment that does not exist (the target was never created, or was cleaned up) ends the
/// walk: what is left of the path is kept as written.</summary>
public static class FileSystemLinkTargets
{
    public static LinkTargets Here { get; } = new(Resolve);

    public static string Resolve(string path)
    {
        var segments = path.Split('/');
        var current = segments[0].Length == 0 ? "/" : segments[0] + "/";

        foreach (var (segment, index) in segments.Skip(1).Select((s, i) => (s, i + 1)).Where(p => p.s.Length > 0))
        {
            var next = Path.Combine(current, segment);

            if (!Path.Exists(next))
            {
                return Joined(next, segments.Skip(index + 1));
            }

            current = Final(next);
        }

        return Normal(current);
    }

    /// <summary>The final target of a link, or the path itself when it is none.</summary>
    private static string Final(string path)
    {
        FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        var target = info.LinkTarget is null ? null : info.ResolveLinkTarget(returnFinalTarget: true);

        return target?.FullName ?? path;
    }

    private static string Joined(string head, IEnumerable<string> tail) => Normal(string.Join('/', [head, .. tail]));

    private static string Normal(string path) => path.Replace('\\', '/').TrimEnd('/') is { Length: > 0 } trimmed ? trimmed : "/";
}
