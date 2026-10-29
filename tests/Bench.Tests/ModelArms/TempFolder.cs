using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>A scratch folder a test owns: named <c>testsToDelete-…</c> on its own last path component so a crashed run's leftover is
/// found by the one sweep (common.test-leftovers), and removed with everything in it by <see cref="Dispose"/>. A file inside may have
/// had its mode lowered, so deleting first restores the owner's access to every folder.</summary>
internal sealed class TempFolder : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("testsToDelete-modelarms-");

    public string Path => _folder.FullName;

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    /// <summary>A folder inside this one, created.</summary>
    public string Folder(params string[] parts) => Directory.CreateDirectory(Combine(parts)).FullName;

    /// <summary>A file inside this one with the given text, its folder created.</summary>
    public string File(string text, params string[] parts)
    {
        var path = Combine(parts);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, text);
        return path;
    }

    /// <summary>Removes the folder with everything in it. The delete is in <c>finally</c>: a run removes what it created whatever happened on the way
    /// (common.test-leftovers). A directory link is unlinked by the delete without being entered, so a loop cannot reach it.</summary>
    /// <summary>An executable file inside this one (mode 700), its folder created. Returns its path.</summary>
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public string Executable(params string[] parts)
    {
        var path = File("#!/bin/sh\n", parts);
        System.IO.File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }

    /// <summary>Skips the test, saying why, where a symbolic link cannot be made. The probe link points at a FILE inside a folder of its own, never at
    /// its own parent: a link to its own parent made the folder's walk throw halfway and left the folder behind.</summary>
    public void RequireLinks()
    {
        try
        {
            var target = File("probe", "probe", "probe-file");
            System.IO.File.CreateSymbolicLink(Combine("probe", "probe-link"), target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Assert.Skip($"symbolic links cannot be made here: {ex.Message}");
        }
    }

    public void Dispose()
    {
        try
        {
            OpenForDeleting();
        }
        finally
        {
            _folder.Delete(recursive: true);
        }
    }

    /// <summary>Gives the owner back full access to every folder a test closed, folder first so its children can be listed. A directory link is a
    /// reparse point and is skipped: following one is what made a link to the folder's own parent throw halfway through the walk. An unreadable folder
    /// is not ignored: its owner opened it a line earlier, so one that still cannot be read is a real failure.</summary>
    private void OpenForDeleting()
    {
        if (!OperatingSystem.IsWindows())
        {
            Open(_folder);
        }
    }

    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    private static void Open(DirectoryInfo folder)
    {
        folder.UnixFileMode |= UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        foreach (var child in folder.EnumerateDirectories("*", new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false }))
        {
            Open(child);
        }
    }
}
