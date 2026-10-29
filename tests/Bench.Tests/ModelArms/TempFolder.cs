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

    public void Dispose()
    {
        if (!OperatingSystem.IsWindows())
        {
            foreach (var entry in _folder.EnumerateFileSystemInfos("*", SearchOption.AllDirectories).OfType<DirectoryInfo>())
            {
                entry.UnixFileMode |= UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            }
        }

        _folder.Delete(recursive: true);
    }
}
