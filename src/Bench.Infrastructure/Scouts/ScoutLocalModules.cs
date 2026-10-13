using Bench.Infrastructure.Git;

namespace Bench.Infrastructure.Scouts;

/// <summary>Where a submodule of a LOCAL source is archived from (D10, found live 2026-10-14). A private repository is read from the operator's
/// working clone through a <c>file://</c> url, but its <c>.gitmodules</c> names a private GitHub url this machine cannot reach. The clone
/// already holds the submodule's objects in its own git directory — <c>&lt;clone&gt;/.git/modules/&lt;path&gt;</c>, and for a nested one
/// <c>&lt;module&gt;/modules/&lt;path&gt;</c> — so a local source's submodule is archived from there when that directory is a repository.
/// Otherwise, and for every network source, the url from <c>.gitmodules</c> is used as it always was. The clone is only READ.</summary>
public static class ScoutLocalModules
{
    public static string Source(string parentSource, string path, string gitmodulesUrl)
    {
        if (!GitCheckoutProvider.IsLocalSource(parentSource) || LocalPath(parentSource) is not { Length: > 0 } parent)
        {
            return gitmodulesUrl;
        }

        var inClone = Directory.Exists(Path.Combine(parent, ".git"));
        var relative = (inClone ? ".git/modules/" : "modules/") + path.Trim('/');
        var module = Path.Combine(parent, relative.Replace('/', Path.DirectorySeparatorChar));

        return File.Exists(Path.Combine(module, "HEAD")) ? Joined(parentSource, relative) : gitmodulesUrl;
    }

    /// <summary>The directory a local source names: a <c>file://</c> url's local path (a UNC path for <c>file:////host/share/…</c>), or the
    /// rooted path itself; empty when the url cannot be read as one.</summary>
    private static string LocalPath(string source) =>
        !source.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ? source
        : Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.IsFile ? uri.LocalPath
        : string.Empty;

    /// <summary>The submodule's source in the parent's own form — a url joined with <c>/</c>, a path combined — so the mirror cache keys it
    /// the way it keys every other local source.</summary>
    private static string Joined(string parentSource, string relative) =>
        parentSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            ? parentSource.TrimEnd('/') + "/" + string.Join('/', relative.Split('/').Select(Uri.EscapeDataString))
            : Path.Combine(parentSource, relative.Replace('/', Path.DirectorySeparatorChar));
}
