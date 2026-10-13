using Bench.Domain;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Git;

namespace Bench.Infrastructure.Scouts;

/// <summary>The roots a scout verb writes under, checked together before anything runs — the scouts' <see cref="Probes.ProbeRoots"/>.</summary>
/// <param name="Artifacts">The artefact root (streams, launch records) — empty for a verb that writes none.</param>
/// <param name="Trees">The history-free trees.</param>
/// <param name="Mirrors">The checkout root the bare mirrors live under.</param>
public sealed record ScoutRootSet(string Artifacts, string Trees, string Mirrors);

/// <summary>Every rule a scout verb's roots are held to, refused in the order a person fixes things — each a CONFIGURATION fact (exit 4):
/// <list type="bullet">
/// <item><b>the artefact root</b> outside every git checkout — the gate's rule (<see cref="FileSystemGateArtifactStore.Open"/>): streams quote
/// code and must never sit where a <c>git add</c> can reach them;</item>
/// <item><b>the trees root</b> GIT-IGNORED when it lies inside a checkout (§3.11): the trees are whole repositories at P, each with its own
/// <c>.git</c>, and the benchmark's repository is public — a tree that could be tracked is a tree that will be;</item>
/// <item><b>the grant guard</b> (§3.2, <see cref="ScoutGrantGuard"/>): neither the trees root nor the mirror root may overlap a directory the
/// Claude user settings grant — a granted directory is readable from any tree.</item>
/// </list></summary>
public static class ScoutRoots
{
    public static string DefaultTreesRoot(string checkoutRoot) => Path.Combine(checkoutRoot, "scout-trees");

    public static async Task<Outcome<ScoutRootSet>> CheckAsync(
        string artifactRoot, string treesRoot, string mirrorRoot, IReadOnlyList<string> userSettings, CancellationToken cancellationToken)
    {
        var artifacts = artifactRoot.Length == 0
            ? Outcome<string>.Success(string.Empty)
            : FileSystemGateArtifactStore.Open(artifactRoot, TimeProvider.System, ArtifactProbe.None).Match(a => Outcome<string>.Success(a.Root), Outcome<string>.Failure);

        if (artifacts is Outcome<string>.Fail badArtifacts)
        {
            return Outcome<ScoutRootSet>.Failure(badArtifacts.Reason);
        }

        var trees = Path.GetFullPath(treesRoot);
        var ignored = await IgnoredAsync(trees, cancellationToken);
        var granted = ScoutGrantGuard.Refusal(trees, Path.GetFullPath(mirrorRoot), userSettings);

        return (ignored.Length > 0, granted.Length > 0) switch
        {
            (true, _) => Outcome<ScoutRootSet>.Failure(ignored),
            (_, true) => Outcome<ScoutRootSet>.Failure(granted),
            _ => Outcome<ScoutRootSet>.Success(new ScoutRootSet(((Outcome<string>.Ok)artifacts).Value, trees, Path.GetFullPath(mirrorRoot))),
        };
    }

    /// <summary>Why the trees root is not safe inside the checkout it lies in, or empty: outside every checkout, or git-ignored there.</summary>
    public static async Task<string> IgnoredAsync(string treesRoot, CancellationToken cancellationToken)
    {
        var above = Nearest(treesRoot);
        var checkout = above.Length == 0 ? string.Empty : FileSystemGateArtifactStore.GitCheckoutAbove(above);

        if (checkout.Length == 0)
        {
            return string.Empty;
        }

        var probe = Path.GetRelativePath(checkout, Path.Combine(treesRoot, "probe-file")).Replace('\\', '/');
        var checkedIgnore = await GitCommand.ReadAsync(checkout, TimeSpan.FromSeconds(30), cancellationToken, "check-ignore", "-q", "--", probe);
        var tracked = await TrackedAsync(checkout, treesRoot, cancellationToken);

        return (checkedIgnore is Outcome<string>.Ok, tracked.Count) switch
        {
            (false, _) => $"the trees root {treesRoot} is inside the git checkout at {checkout} and is NOT git-ignored there — every tree is a whole repository at P, "
                          + "and one that can be tracked will be; add it to that checkout's .gitignore or choose a root outside every repository",
            (_, > 0) => $"{tracked.Count} file(s) under the trees root {treesRoot} are TRACKED by the checkout at {checkout} (first: {tracked[0]}) — the repository is public; untrack them first",
            _ => string.Empty,
        };
    }

    /// <summary>The files under <paramref name="folder"/> the checkout at <paramref name="checkout"/> TRACKS — the hygiene check §3.13 holds the
    /// trees' directory to; empty is clean.</summary>
    public static async Task<IReadOnlyList<string>> TrackedAsync(string checkout, string folder, CancellationToken cancellationToken)
    {
        var relative = Path.GetRelativePath(checkout, folder).Replace('\\', '/');
        var listed = await GitCommand.ReadAsync(checkout, TimeSpan.FromSeconds(30), cancellationToken, "ls-files", "--", relative);

        return listed.Match<IReadOnlyList<string>>(text => [.. text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0)], reason => [$"(git ls-files failed: {reason})"]);
    }

    /// <summary>The nearest directory on the way up that exists — the trees root may not have been created yet.</summary>
    private static string Nearest(string path)
    {
        for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
        {
            if (current.Exists)
            {
                return current.FullName;
            }
        }

        return string.Empty;
    }
}
