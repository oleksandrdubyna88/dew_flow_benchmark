using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Runs;

namespace Bench.Application.Gate;

/// <summary>Durable gate state — the <see cref="IRunStore"/> guarantees, over gate cells.
/// <para>
/// <b>Persist before enqueue</b> (a run and every cell in one transaction), <b>claim and settle</b> (exactly one
/// worker owns a cell, decided in the WHERE clause of one UPDATE), <b>sweep</b> (a cell whose owner is provably
/// gone comes back, at most <see cref="Claimable.MaxAttempts"/> times, and never a cell of a run that already
/// ended). What is the gate's own: a claim is taken UNDER a product pin, and a settle carries the session's
/// facts and its findings — hashes only — in the same transaction as the state change.
/// </para></summary>
public interface IGateStore
{
    /// <summary>Writes the run and all its cells in ONE transaction; refuses a run with no cells or an id taken.</summary>
    Task<Outcome<GateRun>> PlanAsync(GateRun run, IReadOnlyList<GateCell> cells, CancellationToken cancellationToken);

    Task<Outcome<GateRun>> LoadAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>The newest runs, capped in the database.</summary>
    Task<IReadOnlyList<GateRun>> RecentAsync(int limit, CancellationToken cancellationToken);

    /// <summary>Takes one pending cell of a run that has not ended, atomically, under <paramref name="pin"/>. The
    /// claim increments the attempt count — once, in the same UPDATE — so the attempt number a cell is claimed at
    /// is the directory its artefacts go to.</summary>
    Task<Outcome<GateCell>> ClaimNextAsync(Guid runId, WorkerIdentity owner, ProductPin pin, CancellationToken cancellationToken);

    /// <summary><see cref="ClaimNextAsync"/> limited to cells of <paramref name="among"/> — the reviewers whose endpoint
    /// has a free slot. A lane claims only work it can start, so it never holds a claimed cell at the head of the line
    /// while another endpoint sits idle. An empty set claims nothing.</summary>
    Task<Outcome<GateCell>> ClaimNextAmongAsync(
        Guid runId, WorkerIdentity owner, ProductPin pin, IReadOnlyCollection<GateReviewerId> among, CancellationToken cancellationToken);

    /// <summary>Records how a claimed cell ended: the state change, the facts and the findings in one transaction.
    /// Refused for a cell this owner does not hold.</summary>
    Task<Outcome<GateCell>> SettleAsync(Guid cellId, WorkerIdentity owner, GateSettlement settlement, CancellationToken cancellationToken);

    /// <summary>Hands back the stale claims whose owner is provably gone — each with ONE guarded UPDATE that
    /// re-checks the state, the owner, the claim time and the attempt count it decided on — and never a cell of a
    /// run that is <see cref="GateRunStatus.Finished"/> or <see cref="GateRunStatus.Failed"/>.</summary>
    Task<GateSweepReport> SweepAsync(TimeSpan staleAfter, CancellationToken cancellationToken);

    /// <summary>Forward-only: Planned → Running → Finished | Failed. A terminal run does not start again.</summary>
    Task<Outcome<GateRunStatus>> AdvanceAsync(Guid runId, GateRunStatus to, CancellationToken cancellationToken);

    Task<Outcome<GateCell>> CellAsync(Guid cellId, CancellationToken cancellationToken);

    /// <summary>Every cell of a run, in plan order — what <c>bench gate status</c> lists.</summary>
    Task<IReadOnlyList<GateCell>> CellsAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>The run records of a run's SETTLED cells — the facts and findings a report is computed from.</summary>
    Task<IReadOnlyList<GateRunRecord>> FactsAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>Persists committed artefact refs, all or none. A path recorded twice is refused: an artefact is
    /// written once, under its own attempt.</summary>
    Task<Outcome<int>> RecordArtifactsAsync(IReadOnlyList<ArtifactRef> refs, CancellationToken cancellationToken);

    Task<IReadOnlyList<ArtifactRef>> ArtifactsAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>Whether any finding row exists — the question a missing file-hash key must be asked against.</summary>
    Task<bool> HasFindingsAsync(CancellationToken cancellationToken);
}

/// <summary>What one sweep did.</summary>
public sealed record GateSweepReport(int Requeued, int Abandoned)
{
    public int Total => Requeued + Abandoned;
}
