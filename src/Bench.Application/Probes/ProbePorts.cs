using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Probes;
using Bench.Domain.Runs;

namespace Bench.Application.Probes;

/// <summary>Durable probe state — <c>IGateStore</c>'s guarantees over probe cells, with the plan's own rules on top.
/// <para>
/// <b>Persist before enqueue</b> (a run and every cell in one transaction); <b>claim and settle</b> (exactly one lane owns a
/// cell, decided in the WHERE clause of one UPDATE — a probe lane claims FOR ITS SUBJECT, in the matrix's order); <b>sweep</b>
/// (a cell whose owner is provably gone comes back, at most <see cref="Claimable.MaxAttempts"/> times). What is the probe's
/// own: a run has no status (D3) — the sweep's whole world is every claimed probe cell —, an unmeasured attempt is handed
/// back with its attempt COUNTED and never abandons (D8), and a re-run APPENDS a generation (D2).
/// </para></summary>
public interface IProbeStore
{
    /// <summary>Writes the run and all its cells in ONE transaction; refuses a run with no cells, a cell of another run, or an id taken.</summary>
    Task<Outcome<ProbeRun>> PlanAsync(ProbeRun run, IReadOnlyList<ProbeCell> cells, CancellationToken cancellationToken);

    Task<Outcome<ProbeRun>> LoadAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>Takes the next pending cell of <paramref name="subject"/> in <paramref name="runId"/>, atomically, under
    /// <paramref name="pin"/> — slot, then position, then generation. The attempt count moves in the same UPDATE.</summary>
    Task<Outcome<ProbeCell>> ClaimNextAsync(Guid runId, ProbeSubjectId subject, WorkerIdentity owner, ProductPin pin, CancellationToken cancellationToken);

    /// <summary>Records how a claimed cell ended — the state change, the facts and the artefact refs in one transaction. Refused
    /// for a cell this owner does not hold, and for an unmeasured kind (<see cref="ProbeCellLifecycle.Settle"/>).</summary>
    Task<Outcome<ProbeCell>> SettleAsync(Guid cellId, WorkerIdentity owner, ProbeSettlement settlement, CancellationToken cancellationToken);

    /// <summary>The attempt ran and measured nothing the bench may use (an empty account): ONE guarded UPDATE — still claimed, by
    /// this owner, at this attempt — back to Pending with the attempt KEPT, the kind <i>unmeasured</i> and the reason recorded.
    /// Never abandons.</summary>
    Task<Outcome<ProbeCell>> HandBackUnmeasuredAsync(Guid cellId, WorkerIdentity owner, int attempt, ProbeReason reason, CancellationToken cancellationToken);

    /// <summary>Hands back the stale claims whose owner is provably gone — each with ONE guarded UPDATE re-checking the state, the
    /// owner, the claim time and the attempt count it decided on, choosing requeue or abandonment inside the statement.</summary>
    Task<ProbeSweepReport> SweepAsync(TimeSpan staleAfter, CancellationToken cancellationToken);

    /// <summary>Appends the next generation of the lineage <paramref name="cellId"/> belongs to (D2): a fresh Pending cell numbered
    /// max + 1, the id the caller's. Refused while any generation of the lineage is still Pending or Claimed; a concurrent
    /// append of the same number is refused by the unique index.</summary>
    Task<Outcome<ProbeCell>> NextGenerationAsync(Guid cellId, Guid newId, CancellationToken cancellationToken);

    /// <summary>Flags the run's artefacts as deleted (<c>bench probes prune</c>), in ONE guarded UPDATE that refuses while any
    /// cell of the run is Pending or Claimed (finding 5).</summary>
    Task<Outcome<ProbeRun>> MarkArtifactsPrunedAsync(Guid runId, CancellationToken cancellationToken);

    Task<Outcome<ProbeCell>> CellAsync(Guid cellId, CancellationToken cancellationToken);

    /// <summary>Every cell of a run, every generation, in plan order — what <c>bench probes status</c> lists.</summary>
    Task<IReadOnlyList<ProbeCell>> CellsAsync(Guid runId, CancellationToken cancellationToken);
}

/// <summary>What one sweep did.</summary>
public sealed record ProbeSweepReport(int Requeued, int Abandoned)
{
    public int Total => Requeued + Abandoned;
}

/// <summary>The probes' READ side — what the report, the API and the page are computed from, and nothing that writes. A read
/// host registers this and no write port, so no route it maps can claim or settle.</summary>
public interface IProbeReads
{
    Task<IReadOnlyList<ProbeRun>> RecentRunsAsync(int limit, CancellationToken cancellationToken);

    Task<Outcome<ProbeRun>> RunAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>Every cell of a run, every generation, in plan order.</summary>
    Task<IReadOnlyList<ProbeCell>> CellsAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>One cell per lineage — the highest SETTLED generation (<see cref="ProbeGenerations.LatestSettled"/>).</summary>
    Task<IReadOnlyList<ProbeCell>> LatestSettledAsync(Guid runId, CancellationToken cancellationToken);
}

/// <summary>How one attempt ended, as a runner hands it back: a settlement, or an attempt that measured nothing the bench may
/// use and goes back to the queue with its reason.</summary>
public abstract record ProbeAttemptResult
{
    private ProbeAttemptResult()
    {
    }

    public sealed record Settled(ProbeSettlement Settlement) : ProbeAttemptResult;

    public sealed record Unmeasured(ProbeReason Reason) : ProbeAttemptResult;
}

/// <summary>One attempt of one claimed cell — the port the campaign (S2) drives a CLI or the product through. The runner
/// builds the fixture, launches, reads the transcript through <see cref="ProbeVerdicts"/>, commits the artefacts, and answers;
/// the store is the campaign's to call. A runner ALWAYS answers — a settlement or a hand-back — because a cell it left claimed
/// would stay claimed for as long as this process lives.</summary>
public interface IProbeRunner
{
    Task<ProbeAttemptResult> RunAsync(ProbeRun run, ProbeSubject subject, ProbeCell claimed, CancellationToken cancellationToken);
}

/// <summary>The phrase a claim refusal carries when a subject has nothing pending — read by the campaign as the end of its
/// lane, the way <c>ClaimRefusal.NoPendingCell</c> is read by the drain.</summary>
public static class ProbeClaimRefusal
{
    public const string NoPendingCell = "no pending probe cell to claim";
}

/// <summary>An attempt's fixture directories under the WORK root (§4, §6): a fresh root per attempt with fresh random tokens,
/// deleted after the attempt, and the cleanup keyed to the DIRECTORY (finding 3) that every verb runs on entry.</summary>
public interface IProbeFixtures
{
    /// <summary>Makes <c>probes/&lt;run&gt;/&lt;cell&gt;/g&lt;n&gt;/a&lt;k&gt;/</c> with the files <see cref="ProbePaths.Layout"/> says
    /// the probe wants, under two tokens no earlier attempt used. Refuses a directory that already exists — it is a leftover,
    /// and leftovers are <see cref="DeleteStranded"/>'s to remove, never continued.</summary>
    Outcome<ProbeFixture> Begin(ProbeKind probe, ProbeAttemptScope scope);

    /// <summary>Removes the attempt's root. Best effort and never throws: a tree a dying child still holds is the next entry's
    /// <see cref="DeleteStranded"/> to remove.</summary>
    void Delete(ProbeFixture fixture);

    /// <summary>Finding 3: every cell folder under the run's root whose cell is not in <paramref name="liveCells"/> — Pending
    /// cells included — is deleted whole; nothing outside <c>probes/&lt;run&gt;/</c> is touched and no link is followed. Returns
    /// how many were removed.</summary>
    int DeleteStranded(Guid runId, IReadOnlySet<Guid> liveCells);
}

/// <summary>The attempt's three files under the ARTEFACT root — answer, stdout, stderr — committed stage → flush → rename and
/// handed back as refs (path relative to the root, SHA-256, length) for the cell row (D11).</summary>
public interface IProbeArtifacts
{
    Task<Outcome<ProbeArtifact>> CommitAsync(ProbeAttemptScope scope, ProbeArtifactKind kind, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken);
}

/// <summary>The web oracle (D7): the registry's current version of <c>@openai/codex</c>, read ONCE by <c>bench probes run</c>
/// BEFORE anything is planned and frozen on the run — <c>resume</c> and <c>rerun</c> read it from the run and never call this. A
/// read that fails (offline, the registry down, rate-limited) is a refusal naming the cause, never an empty version: an oracle
/// that cannot be read stops the run rather than freezing nothing (gate round 1, finding 0).</summary>
public interface IProbeOracle
{
    Task<Outcome<ProbeOracle>> LatestAsync(CancellationToken cancellationToken);
}

/// <summary>Where the product's vault key comes from for the api probe (D6) — the machine's coai settings in production,
/// a sentinel in the rig. The bench never reads a vendor key; this is the one secret it passes on, by name.</summary>
public interface IProbeSecrets
{
    Outcome<SecretValue> CredsKey();
}
