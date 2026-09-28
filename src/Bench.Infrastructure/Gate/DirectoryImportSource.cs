using Bench.Application.Gate;
using Bench.Domain;

namespace Bench.Infrastructure.Gate;

/// <summary>An import source on the local filesystem — READ-ONLY by construction: this type opens files for reading and
/// enumerates directories, and has no member that writes, moves or deletes. A relative path that climbs out of the
/// source's root is refused.</summary>
public sealed class DirectoryImportSource : IImportSource
{
    private readonly string _root;

    private DirectoryImportSource(string root) => _root = root;

    public string Label => Path.GetFileName(_root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    public static Outcome<DirectoryImportSource> Open(string? root) =>
        !string.IsNullOrWhiteSpace(root) && Directory.Exists(root)
            ? Outcome<DirectoryImportSource>.Success(new DirectoryImportSource(Path.GetFullPath(root)))
            : Outcome<DirectoryImportSource>.Failure($"the import source {Path.GetFileName(root ?? string.Empty)} is not a directory that exists");

    public bool Exists(string relative) => Full(relative) is { } full && File.Exists(full);

    public async Task<Outcome<byte[]>> ReadBytesAsync(string relative, CancellationToken cancellationToken)
    {
        if (Full(relative) is not { } full || !File.Exists(full))
        {
            return Outcome<byte[]>.Failure($"{relative} is not in {Label}");
        }

        try
        {
            await using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var bytes = new byte[stream.Length];
            await stream.ReadExactlyAsync(bytes, cancellationToken);
            return Outcome<byte[]>.Success(bytes);
        }
        catch (IOException ex)
        {
            return Outcome<byte[]>.Failure($"{relative} in {Label} could not be read — {ex.Message}");
        }
    }

    public Task<IReadOnlyList<string>> FilesUnderAsync(string relativeDirectory, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> files = Full(relativeDirectory) is { } full && Directory.Exists(full)
            ? [.. Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(full, f).Replace(Path.DirectorySeparatorChar, '/'))
                .Order(StringComparer.Ordinal)]
            : [];

        return Task.FromResult(files);
    }

    private string? Full(string relative)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relative));
        var inside = full.StartsWith(_root + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

        return inside ? full : null;
    }
}
