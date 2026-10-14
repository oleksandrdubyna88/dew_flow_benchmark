using System.Text.RegularExpressions;
using Bench.Domain.Scouts;
using Bench.Infrastructure.Git;

namespace Bench.Infrastructure.Scouts;

/// <summary>Where a submodule of a LOCAL source is archived from (D10, found live 2026-10-14). A private repository is read from the operator's
/// working clone through a <c>file://</c> url, but its <c>.gitmodules</c> names a private GitHub url this machine cannot reach. In order:
/// <list type="number">
/// <item>the objects the clone keeps in its own git directory — <c>&lt;clone&gt;/.git/modules/&lt;path&gt;</c>, and for a nested one
/// <c>&lt;module&gt;/modules/&lt;path&gt;</c> — when that directory is a repository;</item>
/// <item>a clone BESIDE the parent clone, named as the submodule's repository — only when its own <c>origin</c> is that same repository
/// (host and path; https, ssh and <c>git@</c> alike), so a folder that merely shares the name is never taken for it. Found the same day: a
/// clone that never initialised its submodule, beside the operator's clone of that very repository;</item>
/// <item>otherwise, and for every network source, the url from <c>.gitmodules</c>, as it always was.</item>
/// </list>
/// Every local clone is only READ.</summary>
public static partial class ScoutLocalModules
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

        return File.Exists(Path.Combine(module, "HEAD")) ? Joined(parentSource, relative)
            : inClone && SiblingIs(parent, gitmodulesUrl) ? Sibling(parentSource, ScoutRepository.ShortName(gitmodulesUrl))
            : gitmodulesUrl;
    }

    /// <summary>Whether the clone beside <paramref name="parent"/>, named as <paramref name="url"/>'s repository, has that repository as its
    /// <c>origin</c> — read from its <c>.git/config</c>, nothing run.</summary>
    private static bool SiblingIs(string parent, string url)
    {
        var name = ScoutRepository.ShortName(url);
        var config = Path.Combine(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(parent)) ?? string.Empty, name, ".git", "config");

        return name.Length > 0 && File.Exists(config) && OriginUrl().Match(File.ReadAllText(config)) is { Success: true } origin
            && SameRepository(origin.Groups["url"].Value, url);
    }

    /// <summary>Two urls name the same repository when their host and path agree, case aside — <c>https://host/o/n.git</c>,
    /// <c>ssh://git@host/o/n</c> and <c>git@host:o/n.git</c> are one repository.</summary>
    public static bool SameRepository(string a, string b) =>
        Identity(a) is { Length: > 0 } left && string.Equals(left, Identity(b), StringComparison.OrdinalIgnoreCase);

    private static string Identity(string url) =>
        RemoteUrl().Match(url.Trim()) is { Success: true } m ? $"{m.Groups["host"].Value}/{m.Groups["path"].Value}" : string.Empty;

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

    /// <summary>The clone named <paramref name="name"/> beside the parent, in the parent's own form.</summary>
    private static string Sibling(string parentSource, string name) =>
        parentSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            ? parentSource.TrimEnd('/')[..(parentSource.TrimEnd('/').LastIndexOf('/') + 1)] + Uri.EscapeDataString(name)
            : Path.Combine(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(parentSource)) ?? string.Empty, name);

    [GeneratedRegex("""\[remote "origin"\][^\[]*?^\s*url\s*=\s*(?<url>\S+)""", RegexOptions.Multiline)]
    private static partial Regex OriginUrl();

    [GeneratedRegex(@"^(?:[a-z][a-z0-9+.-]*://)?(?:[^@/]+@)?(?<host>[^/:]+)(?::\d+)?[:/](?<path>.+?)(?:\.git)?/*$", RegexOptions.IgnoreCase)]
    private static partial Regex RemoteUrl();
}
