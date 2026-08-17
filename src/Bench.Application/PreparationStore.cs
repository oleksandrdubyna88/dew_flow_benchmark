using Bench.Domain;
using Bench.Domain.Retrieval;
using Bench.Domain.Runs;

namespace Bench.Application;

/// <param name="Stranded">Rows a dead worker left <c>Building</c>, moved to a state an operator can retry
/// from. Reported because "swept nothing" and "swept forty" mean different things about a deployment.</param>
public readonly record struct PreparationSweep(int Stranded)
{
    public string Describe =>
        Stranded == 0 ? "no preparation was stranded" : $"{Stranded} stranded preparation(s) moved to Failed";
}

/// <summary>Where the state of each corpus lives.
/// <para>
/// Separate from <see cref="IResultStore"/> and <see cref="IRunStore"/> because it is neither evidence nor a
/// queue of measurements: it is the readiness of the THINGS measured against, shared by every run that names
/// the same corpus. Two campaigns at one commit and one recipe wait on one row.
/// </para>
/// <para>
/// Every transition here is owner-guarded in the DATABASE, the same rule the cell queue follows. A
/// preparation with an unguarded update is one two workers can each believe they own — and each would poll a
/// different pass id while the other's collection is being written.
/// </para></summary>
public interface IPreparationStore
{
    /// <summary>Records that this corpus is wanted, or returns the row that already says so.
    /// <para>
    /// Idempotent by a unique index rather than by a read-then-write: two runs starting at the same moment
    /// would both find nothing and both insert, and the matrix would then hold two readiness answers for one
    /// index with no way to say which is current.
    /// </para></summary>
    Task<Outcome<IndexPreparation>> RequestAsync(CorpusKey key, CancellationToken cancellationToken);

    Task<Outcome<IndexPreparation>> FindAsync(CorpusKey key, CancellationToken cancellationToken);

    /// <summary>Takes a waiting request and records the pass now building it. Refused when somebody else got
    /// there first, which is a VALUE: several workers preparing a matrix is the expected shape.</summary>
    Task<Outcome<IndexPreparation>> StartAsync(
        CorpusKey key, WorkerIdentity owner, string passId, CancellationToken cancellationToken);

    /// <summary>Says the watcher is still there. A twenty-four-minute pass is normal, and this stamp is the
    /// only thing separating it from one whose host died four minutes in.</summary>
    Task<Outcome<IndexPreparation>> BeatAsync(
        CorpusKey key, WorkerIdentity owner, CancellationToken cancellationToken);

    /// <summary>Ends a preparation as Ready or Failed. Owner-guarded, so a worker cannot close a pass another
    /// one is watching.</summary>
    Task<Outcome<IndexPreparation>> EndAsync(
        CorpusKey key, WorkerIdentity owner, PreparationState state, string reason, CancellationToken cancellationToken);

    /// <summary>Moves rows a dead worker left <c>Building</c> to Failed, and only those.
    /// <para>
    /// Staleness selects the candidates and OWNERSHIP decides, exactly as the cell sweep does — a live worker
    /// past the window is legitimately watching a long pass, and a worker on another machine cannot be
    /// interrogated from here at all. Without this, one engine restart stalls every cell of a corpus for the
    /// life of the deployment, and the stall is indistinguishable from a slow index.
    /// </para></summary>
    Task<PreparationSweep> SweepAsync(TimeSpan window, CancellationToken cancellationToken);

    Task<IReadOnlyList<IndexPreparation>> ListAsync(CancellationToken cancellationToken);
}
