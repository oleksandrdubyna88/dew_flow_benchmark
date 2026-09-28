using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

/// <summary>The private half of the gate's storage: a directory tree OUTSIDE git holding every request, reply,
/// stderr, ledger, prompt, answer and tap body, laid out by <see cref="CellPaths"/>.
/// <para>
/// Three guarantees. <b>Containment</b> — a write lands under its own attempt's root or its own data directory,
/// resolved against the real filesystem with links followed, or it is refused. <b>Commit</b> — a file is
/// staged, flushed to disk, hashed, and only then renamed into place, so a crash leaves a staging file nobody
/// reads rather than half an artefact under its real name. <b>Fresh attempts</b> — a later attempt of a cell
/// gets a new directory, and every earlier one is kept and marked interrupted, never reused or continued.
/// </para></summary>
public interface IGateArtifactStore
{
    /// <summary>Creates the attempt's directory (and its data directory), refusing one that already exists, and
    /// marks every EARLIER attempt directory of the cell interrupted. An attempt is only begun under a claim, and
    /// a cell whose earlier attempt had settled is terminal — so every earlier attempt is, by construction, one
    /// that did not finish.</summary>
    Task<Outcome<ArtifactPath>> BeginAttemptAsync(ArtifactScope scope, CancellationToken cancellationToken);

    /// <summary>Stages, flushes, hashes and renames <paramref name="bytes"/> into <paramref name="path"/>, which
    /// must lie inside <paramref name="scope"/>'s writable roots. Refuses a file that already exists.</summary>
    Task<Outcome<ArtifactRef>> WriteAsync(
        ArtifactScope scope, ArtifactClass kind, ArtifactPath path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken);

    /// <summary>A file that was written LIVE inside the attempt's writable roots — the product's data directory, the stderr
    /// the harness streamed, a tap body written before forwarding — flushed to disk, hashed and returned as a ref. The
    /// same containment as <see cref="WriteAsync"/>; nothing is copied, so a large tap body is on disk once.</summary>
    Task<Outcome<ArtifactRef>> AdoptAsync(ArtifactScope scope, ArtifactClass kind, ArtifactPath path, CancellationToken cancellationToken);

    /// <summary>Re-reads a committed artefact and refuses it unless its bytes still hash to the ref.</summary>
    Task<Outcome<ReadOnlyMemory<byte>>> ReadAsync(ArtifactRef artifact, CancellationToken cancellationToken);

    /// <summary>The state of every attempt directory a cell has on disk, in attempt order.</summary>
    Task<IReadOnlyList<AttemptOnDisk>> AttemptsAsync(Guid runId, Guid cellId, CancellationToken cancellationToken);

    /// <summary>What a run's artefacts hold on disk, or <i>unknown</i> when it could not be measured.</summary>
    Task<ArtifactFootprint> FootprintAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>The run ids that have a directory under the root.</summary>
    Task<IReadOnlyList<Guid>> RunsAsync(CancellationToken cancellationToken);

    /// <summary>The key a finding's file hash is computed under — <see cref="FileHashKeyRead.Missing"/> when this
    /// root has none yet. Never logged, never returned as text.</summary>
    Task<FileHashKeyRead> ReadFileHashKeyAsync(CancellationToken cancellationToken);

    /// <summary>Creates the key file with fresh random bytes, readable by this user only. Refuses when one exists.</summary>
    Task<Outcome<FileHashKey>> CreateFileHashKeyAsync(CancellationToken cancellationToken);

    /// <summary>Releases tap request/response BODIES older than <paramref name="cutoff"/> from attempts that
    /// finished, after making each call's facts file durable. <paramref name="dryRun"/> lists and releases nothing.</summary>
    Task<TapPruneReport> PruneTapAsync(DateTimeOffset cutoff, bool dryRun, CancellationToken cancellationToken);
}

/// <summary>An attempt directory as the disk shows it.</summary>
public enum AttemptState
{
    /// <summary>Begun; no run record committed yet and nothing later has replaced it.</summary>
    Open,

    /// <summary>Its run record was committed.</summary>
    Recorded,

    /// <summary>A later attempt of the same cell replaced it; kept, never continued.</summary>
    Interrupted,
}

public sealed record AttemptOnDisk(int Attempt, AttemptState State, ArtifactPath Root);

/// <summary>What reading the file-hash key found. A closed hierarchy, because "no key yet" and "a key file that
/// cannot be used" call for opposite actions — create one, or refuse and say which file is wrong.</summary>
public abstract record FileHashKeyRead
{
    private FileHashKeyRead()
    {
    }

    public sealed record Present(FileHashKey Key) : FileHashKeyRead;

    public sealed record Missing : FileHashKeyRead;

    public sealed record Unusable(string Reason) : FileHashKeyRead;
}

/// <summary>What one prune decided about one attempt directory.</summary>
public enum TapPruneState
{
    /// <summary>Bodies past the window were released; the facts files stay.</summary>
    Released,

    /// <summary>A dry run: these bodies WOULD be released.</summary>
    WouldRelease,

    /// <summary>No run record — the attempt never finished. Listed, never touched.</summary>
    Unfinished,

    /// <summary>Replaced by a later attempt. Kept whole as the evidence of what went wrong.</summary>
    Interrupted,

    /// <summary>Nothing past the window.</summary>
    Recent,

    /// <summary>A call has bodies but no facts file — its bodies are kept, because deleting them would leave neither.</summary>
    FactsMissing,

    /// <summary>A link on the way to the attempt's tap folder. A prune deletes, so it never follows one; nothing touched.</summary>
    Linked,
}

public sealed record TapPruneEntry(Guid RunId, Guid CellId, int Attempt, TapPruneState State, int Bodies, long Bytes);

public sealed record TapPruneReport(IReadOnlyList<TapPruneEntry> Entries)
{
    public int BodiesReleased => Entries.Where(e => e.State == TapPruneState.Released).Sum(e => e.Bodies);

    public long BytesReleased => Entries.Where(e => e.State == TapPruneState.Released).Sum(e => e.Bytes);
}
