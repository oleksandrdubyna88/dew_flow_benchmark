using Bench.Domain;
using Bench.Infrastructure.Gate;

namespace Bench.Infrastructure.Probes;

/// <summary>The probes' one way of deleting a directory tree — shared by the fixture cleanup (<see cref="ProbeFixtures"/>, under the
/// WORK root) and the artefact prune (<see cref="ProbeArtifacts.DeleteRun"/>, under the ARTEFACT root), extracted from the former
/// so the two deletions cannot come to disagree about what a link is. Every rule is a scope rule: the path is resolved on the real
/// filesystem (links included) and must lie under the root it was asked about; a folder that IS a link is removed as itself and
/// never followed; anything the filesystem refuses is left for the next pass rather than thrown.</summary>
internal static class ProbeTrees
{
    /// <summary>Whether <paramref name="path"/> REALLY lies under <paramref name="root"/> — resolved on the filesystem, links included.</summary>
    public static bool IsUnder(string path, string root) =>
        ArtifactContainment.Resolve(path).Match(full => ArtifactContainment.IsWithin(full, root), _ => false);

    /// <summary>A link is removed as ITSELF — never followed; a real directory is checked to resolve under <paramref name="root"/>
    /// before anything is deleted. Anything the filesystem refuses is left for the next entry.</summary>
    public static bool Remove(string folder, string root)
    {
        try
        {
            if (new DirectoryInfo(folder).LinkTarget is not null)
            {
                Directory.Delete(folder);
                return true;
            }

            return IsUnder(folder, root) && TryDeleteTree(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool TryDeleteTree(string path)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            // A recursive delete does not follow a link inside the tree — the link is removed, its target untouched.
            Directory.Delete(path, recursive: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Every folder directly under <paramref name="parent"/> removed by <see cref="Remove"/> (scoped to
    /// <paramref name="parent"/>), then <paramref name="parent"/> itself once it is empty: how many were removed, or a refusal naming
    /// how many the filesystem kept.</summary>
    public static Outcome<int> RemoveAll(string parent)
    {
        var folders = Directory.EnumerateDirectories(parent).ToList();
        var removed = folders.Count(folder => Remove(folder, parent));
        var left = Directory.EnumerateFileSystemEntries(parent).Count();

        if (left == 0)
        {
            TryDeleteTree(parent);
        }

        return left == 0
            ? Outcome<int>.Success(removed)
            : Outcome<int>.Failure($"{left} entr(y/ies) under the run's folder could not be removed — the filesystem still holds them; prune again");
    }
}
