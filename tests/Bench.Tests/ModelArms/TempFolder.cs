using Bench.Tests.Leftovers;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>A scratch folder a test owns: the owned folder <c>testsToDelete-&lt;owner&gt;-bench-modelarms-&lt;id&gt;</c>, claimed beside it and made by the copied helper (<see cref="OwnedDir"/>; plan D5), so a crashed run's leftover is found by the one sweep (common.test-leftovers).
/// <see cref="Dispose"/> opens every folder a test closed (a file inside may have had its mode lowered; the owned removal does not restore modes), then removes folder and claim by the exact path and never throws, not even when the opening walk fails:
/// that is one warning naming the path, the removal is still tried, and what stays keeps its claim for the end-of-run audit to name.</summary>
internal sealed class TempFolder : IDisposable
{
    private readonly Action<DirectoryInfo> _open;
    private readonly Action<string> _warn;
    private readonly OwnedDir _owned;

    public TempFolder()
        : this(null, null)
    {
    }

    /// <summary>The two seams are for the test of a failing walk only: the step that opens the closed folders, and where a warning goes.</summary>
    internal TempFolder(Action<DirectoryInfo>? open, Action<string>? warn)
    {
        _open = open ?? OpenForDeleting;
        _warn = warn ?? Owned.DefaultWarn;
        _owned = OwnedDir.Create("modelarms", warn: _warn);
    }

    public string Path => _owned.Path;

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

    /// <summary>Removes the folder and its claim, by the exact path. The removal is in <c>finally</c>: a run removes what it created whatever happened on the way (common.test-leftovers). A directory link is unlinked by the removal without being entered, so a loop cannot reach it.
    /// Never throws (<see cref="OwnedDir.Dispose"/> never does).</summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                _open(new DirectoryInfo(Path));
            }
        }
        catch (Exception ex)
        {
            _warn($"modelarms: could not reopen {Path} before its removal ({ex.GetType().Name}: {ex.Message}) — the removal is still tried; what stays keeps its claim for the end-of-run audit");
        }
        finally
        {
            _owned.Dispose();
        }
    }

    /// <summary>Gives the owner back full access to every folder a test closed, folder first so its children can be listed. A directory link is a reparse point and is skipped: following one is what made a link to the folder's own parent throw halfway through the walk.
    /// An unreadable folder is not ignored: its owner opened it a line earlier, so one that still cannot be read is a real failure (here a warning, then the removal is tried anyway).</summary>
    private static void OpenForDeleting(DirectoryInfo folder)
    {
        if (!OperatingSystem.IsWindows())
        {
            Open(folder);
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
