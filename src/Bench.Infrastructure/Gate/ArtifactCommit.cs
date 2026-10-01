using System.Globalization;
using System.Security.Cryptography;
using Bench.Domain;

namespace Bench.Infrastructure.Gate;

/// <summary>What a commit left on disk: the SHA-256 of the bytes under their real name, and how many there were.</summary>
public sealed record CommittedBytes(string Sha256, long Length);

/// <summary>Stage → flush → hash → rename: the one way bytes become an artefact on disk.
/// <para>
/// Extracted from <see cref="FileSystemGateArtifactStore"/> for the probes' artefacts (S2 of the question-consultant plan),
/// which commit under their own layout (<c>Bench.Domain.Probes.ProbePaths</c>) but with the SAME protocol — a crash at any
/// step leaves a staging file whose name says it is not an artefact, and a file that appeared meanwhile is never overwritten.
/// One implementation, two callers; the gate store keeps its containment and its refs, this keeps the protocol.
/// </para></summary>
public static class ArtifactCommit
{
    public const string StagingMarker = ".staging-";

    /// <summary>Commits <paramref name="bytes"/> at the absolute path <paramref name="full"/>, which the CALLER has already
    /// contained. A refusal names what happened without the path — the caller knows its own spelling of it.</summary>
    public static async Task<Outcome<CommittedBytes>> CommitAsync(string full, ReadOnlyMemory<byte> bytes, ArtifactProbe probe, CancellationToken cancellationToken)
    {
        if (File.Exists(full) || Directory.Exists(full))
        {
            return Outcome<CommittedBytes>.Failure("already exists — an artefact is committed once and never overwritten");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var staging = await StageAsync(full, bytes, probe, cancellationToken);

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes.Span));
        probe.Reached(ArtifactStep.Hashed);

        try
        {
            File.Move(staging, full, overwrite: false);
        }
        catch (IOException) when (File.Exists(full))
        {
            File.Delete(staging);
            return Outcome<CommittedBytes>.Failure("appeared while it was being committed — nothing was overwritten");
        }

        probe.Reached(ArtifactStep.Renamed);

        return Outcome<CommittedBytes>.Success(new CommittedBytes(sha256, bytes.Length));
    }

    /// <summary>Writes the bytes to a staging name beside the target and flushes them to the disk. A crash leaves a
    /// file whose name says it is not an artefact; nothing ever reads a staging file as one.</summary>
    private static async Task<string> StageAsync(string full, ReadOnlyMemory<byte> bytes, ArtifactProbe probe, CancellationToken cancellationToken)
    {
        var staging = full + StagingMarker + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

        await using (var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            await stream.WriteAsync(bytes, cancellationToken);
            probe.Reached(ArtifactStep.Staged);
            stream.Flush(flushToDisk: true);
            probe.Reached(ArtifactStep.Flushed);
        }

        return staging;
    }

    /// <summary>The synchronous twin for the small markers a store writes itself.</summary>
    public static void Stage(string full, byte[] bytes)
    {
        var staging = full + StagingMarker + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

        using (var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }

        File.Move(staging, full, overwrite: false);
    }
}
