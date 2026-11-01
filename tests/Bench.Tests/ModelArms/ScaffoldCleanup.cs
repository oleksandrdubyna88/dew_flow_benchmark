namespace Bench.Tests.ModelArms;

/// <summary>Removes what a scaffold made, best effort: a folder whose mode was lowered (a locked root, a 000 folder) is opened first — the folder's own owner access BEFORE it is listed, each child on its own so one failure never stops its siblings — then deleted; a path that is absent, a link (even a broken one) and a file are each handled by what they are, never by whether their target exists. Every path it could not open or delete comes back, never thrown; the caller aggregates.
/// <para>
/// What this does NOT prove: removal of a path another user owns, or of one on a file system that refuses <c>chmod</c> (it comes back as left).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
internal static class ScaffoldCleanup
{
    private const UnixFileMode Owner = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private enum Kind
    {
        Absent,
        Link,
        Folder,
        File,
    }

    /// <summary>The paths under (and including) <paramref name="path"/> that could not be removed; empty when it is gone.</summary>
    public static IReadOnlyList<string> Remove(string path) =>
        KindOf(path) switch
        {
            Kind.Absent => [],
            Kind.Folder => RemoveFolder(path),
            _ => Delete(() => File.Delete(path), path),
        };

    private static IReadOnlyList<string> RemoveFolder(string folder)
    {
        List<string> left = [.. Open(folder)];
        left.AddRange(Delete(() => Directory.Delete(folder, recursive: true), folder));
        return left;
    }

    /// <summary>Grants the folder's own access, then lists it, then opens each child alone.</summary>
    private static IReadOnlyList<string> Open(string folder)
    {
        try
        {
            File.SetUnixFileMode(folder, File.GetUnixFileMode(folder) | Owner);
            List<string> children = [.. Directory.EnumerateFileSystemEntries(folder)];
            return [.. children.SelectMany(OpenEntry)];
        }
        catch (Exception problem) when (IsFileSystem(problem))
        {
            return [folder];
        }
    }

    private static IReadOnlyList<string> OpenEntry(string entry) =>
        KindOf(entry) switch
        {
            Kind.Folder => Open(entry),
            Kind.File => Allow(entry),
            _ => [],
        };

    private static IReadOnlyList<string> Allow(string file)
    {
        try
        {
            File.SetUnixFileMode(file, File.GetUnixFileMode(file) | UnixFileMode.UserRead | UnixFileMode.UserWrite);
            return [];
        }
        catch (Exception problem) when (IsFileSystem(problem))
        {
            return [file];
        }
    }

    private static IReadOnlyList<string> Delete(Action delete, string path)
    {
        try
        {
            delete();
            return [];
        }
        catch (Exception problem) when (IsFileSystem(problem))
        {
            return [path];
        }
    }

    /// <summary>What the entry IS, from its own attributes (the link's, never its target's), so an unreadable folder and a broken link are neither "absent" nor "a file".</summary>
    private static Kind KindOf(string path)
    {
        var attributes = new FileInfo(path).Attributes;
        return (int)attributes == -1 ? Kind.Absent
            : attributes.HasFlag(FileAttributes.ReparsePoint) ? Kind.Link
            : attributes.HasFlag(FileAttributes.Directory) ? Kind.Folder
            : Kind.File;
    }

    private static bool IsFileSystem(Exception problem) => problem is IOException or UnauthorizedAccessException;
}
