using Bench.Application;
using Bench.Domain;
using Bench.Domain.Retrieval;
using Bench.Domain.Runs;
using Bench.Domain.Targets;
using Bench.Domain.Trace;
using Bench.Domain.Variants;
using Bench.Infrastructure.Process;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>The durable run store.
/// <para>
/// Every state change is a <b>guarded</b> update — the condition that must hold is in the WHERE clause,
/// not in an <c>if</c> above it. That is the whole difference between "we checked it was Pending" and
/// "only one worker can move it out of Pending": the first is a check-then-act with a window in the
/// middle, and under two workers the window is not theoretical, it is the normal case.
/// </para></summary>
public sealed class PostgresRunStore(BenchDbContext db, TimeProvider clock) : IRunStore
{
    /// <summary>How many times a claim will step over a cell another worker won before giving up. Losing
    /// a race is ordinary; losing this many in a row means the queue is drained, not that we are unlucky.</summary>
    private const int ClaimAttempts = 8;

    public async Task<Outcome<BenchRun>> CreateAsync(
        BenchRun run, IReadOnlyList<RunCell> cells, CancellationToken cancellationToken)
    {
        if (cells.Count == 0)
        {
            return Outcome<BenchRun>.Failure("a run with no cells would look started and could never finish");
        }

        if (await db.Runs.AnyAsync(r => r.Id == run.Id, cancellationToken))
        {
            return Outcome<BenchRun>.Failure($"run {run.Id} already exists");
        }

        db.Runs.Add(ToRow(run));
        db.Cells.AddRange(cells.Select(ToRow));

        // One SaveChanges is one transaction: the run and every cell land together or not at all. This is
        // the "persist before enqueue" guarantee, and it is why nothing here returns before the write.
        await db.SaveChangesAsync(cancellationToken);

        return Outcome<BenchRun>.Success(run);
    }

    public async Task<Outcome<BenchRun>> LoadAsync(Guid runId, CancellationToken cancellationToken)
    {
        var row = await db.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);

        return row is null
            ? Outcome<BenchRun>.Failure($"no run {runId}")
            : ToDomain(row);
    }

    public async Task<Outcome<RunCell>> ClaimNextAsync(
        Guid runId, WorkerIdentity owner, CancellationToken cancellationToken)
    {
        if (!owner.CanClaim)
        {
            return Outcome<RunCell>.Failure(
                "a claim needs an owner with a host and a pid — an unowned claim can never be swept correctly");
        }

        for (var attempt = 0; attempt < ClaimAttempts; attempt++)
        {
            var candidate = await NextPendingIdAsync(runId, cancellationToken);

            if (candidate == Guid.Empty)
            {
                return Outcome<RunCell>.Failure($"run {runId} has {ClaimRefusal.NoPendingCell}");
            }

            if (await TryClaimAsync(candidate, owner, cancellationToken))
            {
                return await ReadAsync(candidate, cancellationToken);
            }
        }

        return Outcome<RunCell>.Failure($"lost {ClaimAttempts} claim races in a row — the queue is contended, retry");
    }

    public async Task<Outcome<RunCell>> SettleAsync(
        Guid cellId, WorkerIdentity owner, LegOutcome outcome, CancellationToken cancellationToken)
    {
        var (kind, detail) = LegOutcomeCodec.Encode(outcome);

        // The whole identity is in the WHERE, not just the label: two hosts may both call themselves
        // "cli", and a settle that matched on the label alone would let one of them close the other's leg.
        var settled = await db.Cells
            .Where(c => c.Id == cellId
                     && c.State == CellState.Claimed
                     && c.Owner == owner.Label
                     && c.OwnerHost == owner.Host
                     && c.OwnerPid == owner.Pid)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.State, CellState.Settled)
                      .SetProperty(c => c.OutcomeKind, kind)
                      .SetProperty(c => c.OutcomeDetail, detail),
                cancellationToken);

        return settled == 1
            ? await ReadAsync(cellId, cancellationToken)
            : await ExplainSettleRefusalAsync(cellId, owner, cancellationToken);
    }

    /// <summary>Hands back the cells whose OWNER is gone, and only those.
    /// <para>
    /// Staleness selects the candidates; ownership decides. Until 2026-08-16 elapsed time decided alone,
    /// which was harmless only while nothing called this method — the moment it ran at every
    /// <c>bench run</c> startup, worker B beginning a campaign could requeue a cell worker A was
    /// legitimately still measuring, and A's settle would then be refused for an owner mismatch it did
    /// nothing to cause. The 30-minute window against a 10-minute leg wall is a MARGIN, not a guarantee,
    /// and this system is meant to run unattended for weeks.
    /// </para>
    /// <para>
    /// The candidates are loaded and filtered in memory because a set-based <c>ExecuteUpdate</c> cannot
    /// ask the operating system whether a pid is alive. That is affordable precisely because the staleness
    /// predicate stays in SQL: claimed-and-stale rows are ~0 in a healthy system, so the list is empty on
    /// almost every sweep. The updates themselves stay GUARDED — <c>State == Claimed</c> and the same
    /// cutoff are still in the WHERE, so a cell that settled or was re-claimed between the read and the
    /// write is not clobbered by a decision taken about its previous life.
    /// </para></summary>
    public async Task<SweepReport> SweepAsync(TimeSpan staleAfter, CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow() - staleAfter;
        var stranded = await StrandedAsync(cutoff, cancellationToken);

        // Abandon first, then requeue. The two sets are disjoint on Attempts, so the order does not change
        // the result — but abandoning first means a cell can never be requeued and abandoned in one sweep,
        // which would report it twice.
        var abandoned = await AbandonAsync(
            Ids(stranded, c => c.Attempts >= CellLifecycle.MaxAttempts), cutoff, cancellationToken);

        var requeued = await RequeueAsync(
            Ids(stranded, c => c.Attempts < CellLifecycle.MaxAttempts), cutoff, cancellationToken);

        return new SweepReport(requeued, abandoned);
    }

    public async Task<RunProgress> ProgressAsync(Guid runId, CancellationToken cancellationToken)
    {
        var counts = await db.Cells.AsNoTracking()
            .Where(c => c.RunId == runId)
            .GroupBy(c => c.State)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.State, x => x.Count, cancellationToken);

        return new RunProgress(
            counts.GetValueOrDefault(CellState.Pending),
            counts.GetValueOrDefault(CellState.Claimed),
            counts.GetValueOrDefault(CellState.Settled),
            counts.GetValueOrDefault(CellState.Abandoned));
    }

    /// <summary>Records the machine, once. A second call for the same run REPLACES nothing and refuses
    /// nothing — it is simply a no-op, because the first read is the one that describes the machine the run
    /// started on and a later one would quietly re-label a measurement already under way.</summary>
    public async Task RecordMachineAsync(Guid runId, MachineFacts facts, CancellationToken cancellationToken)
    {
        if (await db.RunMachines.AnyAsync(m => m.RunId == runId, cancellationToken))
        {
            return;
        }

        db.RunMachines.Add(new RunMachineRow
        {
            RunId = runId,
            Fingerprint = facts.Fingerprint,
            FactsJson = MachineFactsJson.Write(facts),
            RecordedAt = clock.GetUtcNow(),
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<MachineFacts> MachineAsync(Guid runId, CancellationToken cancellationToken)
    {
        var row = await db.RunMachines.AsNoTracking().FirstOrDefaultAsync(m => m.RunId == runId, cancellationToken);

        // No row is NOT RECORDED, which is what every run stored before this table existed is — and a
        // different fact from a machine that was read and answered nothing.
        return row is null ? MachineFacts.NotRecorded : MachineFactsJson.Read(row.FactsJson);
    }

    /// <summary>The newest runs, capped in the database rather than in memory.
    /// <para>
    /// A row this cannot parse back — an unreadable url or commit, which is only possible if something
    /// wrote around this store — is SKIPPED rather than failing the listing: an operator hunting an id
    /// should not be blocked from seeing every other run by one bad row, and the row is still there for a
    /// direct <c>LoadAsync</c> to refuse by name.
    /// </para></summary>
    public async Task<IReadOnlyList<BenchRun>> RecentAsync(int limit, CancellationToken cancellationToken)
    {
        var rows = await db.Runs.AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);

        return [.. rows.Select(ToDomain).OfType<Outcome<BenchRun>.Ok>().Select(ok => ok.Value)];
    }

    /// <summary>The run's planned questions, distinct and ordered, computed in the database.
    /// <para>
    /// A <c>DISTINCT</c> over one indexed column rather than a read of the cells: a run has one row per
    /// question × repeat × subject × lane × variant, so hydrating cells to collect tens of ids would scale
    /// with the matrix instead of with the suite.
    /// </para></summary>
    public async Task<IReadOnlyList<string>> QuestionIdsAsync(Guid runId, CancellationToken cancellationToken)
    {
        var ids = await db.Cells.AsNoTracking()
            .Where(c => c.RunId == runId)
            .Select(c => c.QuestionId)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);

        return ids;
    }

    /// <summary>The stale claims whose owner is provably gone. Everything else a stale row can be — a
    /// colleague on this host still working, or any row belonging to ANOTHER machine — is left alone.</summary>
    private async Task<List<CellRow>> StrandedAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var stale = await db.Cells.AsNoTracking()
            .Where(c => c.State == CellState.Claimed && c.ClaimedAt <= cutoff)
            .ToListAsync(cancellationToken);

        return [.. stale.Where(IsOrphan)];
    }

    private static bool IsOrphan(CellRow row) =>
        WorkerIdentity.Stored(row.Owner, row.OwnerHost, row.OwnerPid)
            .IsProvablyGoneOn(WorkerLiveness.ThisHost, WorkerLiveness.ProcessIsAlive);

    private static List<Guid> Ids(IEnumerable<CellRow> rows, Func<CellRow, bool> matching) =>
        [.. rows.Where(matching).Select(r => r.Id)];

    private async Task<int> AbandonAsync(
        List<Guid> ids, DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        ids.Count == 0 ? 0 : await Claimed(ids, cutoff).ExecuteUpdateAsync(
            s => s.SetProperty(c => c.State, CellState.Abandoned)
                  .SetProperty(c => c.Owner, string.Empty)
                  .SetProperty(c => c.OwnerHost, string.Empty)
                  .SetProperty(c => c.OwnerPid, 0)
                  .SetProperty(c => c.OutcomeKind, LegOutcomeKind.Crashed)
                  .SetProperty(c => c.OutcomeDetail, $"abandoned after {CellLifecycle.MaxAttempts} attempts"),
            cancellationToken);

    private async Task<int> RequeueAsync(
        List<Guid> ids, DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        ids.Count == 0 ? 0 : await Claimed(ids, cutoff).ExecuteUpdateAsync(
            s => s.SetProperty(c => c.State, CellState.Pending)
                  .SetProperty(c => c.Owner, string.Empty)
                  .SetProperty(c => c.OwnerHost, string.Empty)
                  .SetProperty(c => c.OwnerPid, 0),
            cancellationToken);

    /// <summary>The guard the sweep's two updates share: still claimed, and still claimed as of the same
    /// cutoff the decision was taken against.</summary>
    private IQueryable<CellRow> Claimed(List<Guid> ids, DateTimeOffset cutoff) =>
        db.Cells.Where(c => ids.Contains(c.Id) && c.State == CellState.Claimed && c.ClaimedAt <= cutoff);

    private async Task<Guid> NextPendingIdAsync(Guid runId, CancellationToken cancellationToken) =>
        await db.Cells.AsNoTracking()
            .Where(c => c.RunId == runId && c.State == CellState.Pending)
            .OrderBy(c => c.Position).ThenBy(c => c.QuestionId).ThenBy(c => c.Repeat)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>The atomic step. <c>State == Pending</c> lives in the WHERE, so exactly one of any number
    /// of concurrent callers can see a row change and the rest see zero.</summary>
    private async Task<bool> TryClaimAsync(
        Guid cellId, WorkerIdentity owner, CancellationToken cancellationToken) =>
        await db.Cells
            .Where(c => c.Id == cellId && c.State == CellState.Pending)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.State, CellState.Claimed)
                      .SetProperty(c => c.Owner, owner.Label)
                      .SetProperty(c => c.OwnerHost, owner.Host)
                      .SetProperty(c => c.OwnerPid, owner.Pid)
                      .SetProperty(c => c.ClaimedAt, clock.GetUtcNow())
                      .SetProperty(c => c.Attempts, c => c.Attempts + 1),
                cancellationToken) == 1;

    public Task<Outcome<RunCell>> CellAsync(Guid cellId, CancellationToken cancellationToken) =>
        ReadAsync(cellId, cancellationToken);

    public async Task<IReadOnlyList<LegPhase>> EnsurePhasesAsync(
        Guid cellId, IReadOnlyList<LegPhase> phases, CancellationToken cancellationToken)
    {
        var existing = await PhasesAsync(cellId, cancellationToken);

        if (existing.Count > 0)
        {
            return existing;
        }

        db.LegPhases.AddRange(phases.Select(ToRow));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return phases;
        }
        catch (DbUpdateException)
        {
            // The unique (CellId, Ordinal) index fired: another worker materialised this cell's record
            // between our read and our write. The claim makes that theoretical; losing it politely is
            // reading what the winner wrote.
            db.ChangeTracker.Clear();
            return await PhasesAsync(cellId, cancellationToken);
        }
    }

    public async Task<Outcome<int>> SavePhasesAsync(
        IReadOnlyList<LegPhase> phases, CancellationToken cancellationToken)
    {
        var written = 0;

        foreach (var phase in phases)
        {
            written += await db.LegPhases
                .Where(p => p.Id == phase.Id)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(p => p.State, phase.State)
                          .SetProperty(p => p.Tools, phase.Tools)
                          .SetProperty(p => p.Thinking, phase.Thinking)
                          .SetProperty(p => p.InfrastructureWait, phase.InfrastructureWait)
                          .SetProperty(p => p.CostUsd, phase.CostUsd)
                          .SetProperty(p => p.OutcomeKind, phase.Outcome)
                          .SetProperty(p => p.Detail, phase.Detail),
                    cancellationToken);
        }

        return written == phases.Count
            ? Outcome<int>.Success(written)
            : Outcome<int>.Failure(
                $"updated {written} of {phases.Count} phase row(s) — a transition on a phase that was never materialised is a defect, not an upsert");
    }

    public async Task<IReadOnlyList<LegPhase>> PhasesAsync(Guid cellId, CancellationToken cancellationToken)
    {
        var rows = await db.LegPhases.AsNoTracking()
            .Where(p => p.CellId == cellId)
            .OrderBy(p => p.Ordinal)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(ToDomain)];
    }

    private static LegPhaseRow ToRow(LegPhase phase) => new()
    {
        Id = phase.Id,
        CellId = phase.CellId,
        Kind = phase.Kind,
        Ordinal = phase.Ordinal,
        State = phase.State,
        Tools = phase.Tools,
        Thinking = phase.Thinking,
        InfrastructureWait = phase.InfrastructureWait,
        CostUsd = phase.CostUsd,
        OutcomeKind = phase.Outcome,
        Detail = phase.Detail,
    };

    private static LegPhase ToDomain(LegPhaseRow row) => new(
        row.Id, row.CellId, row.Kind, row.Ordinal, row.State,
        row.Tools, row.Thinking, row.InfrastructureWait, row.CostUsd, row.OutcomeKind, row.Detail);

    private async Task<Outcome<RunCell>> ReadAsync(Guid cellId, CancellationToken cancellationToken)
    {
        var row = await db.Cells.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cellId, cancellationToken);

        return row is null ? Outcome<RunCell>.Failure($"no cell {cellId}") : Outcome<RunCell>.Success(ToDomain(row));
    }

    private async Task<Outcome<RunCell>> ExplainSettleRefusalAsync(
        Guid cellId, WorkerIdentity owner, CancellationToken cancellationToken)
    {
        var row = await db.Cells.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cellId, cancellationToken);

        return row switch
        {
            null => Outcome<RunCell>.Failure($"no cell {cellId}"),
            { State: not CellState.Claimed } => Outcome<RunCell>.Failure(
                $"cell {cellId} is {row.State}, not Claimed — only a claimed cell can settle"),
            _ => Outcome<RunCell>.Failure(
                $"cell {cellId} is held by '{row.Owner}' ({Owner(row).Canonical}), not '{owner.Label}' ({owner.Canonical}) "
                + "— a swept-away claim must not overwrite the retry that replaced it"),
        };
    }

    private static RunRow ToRow(BenchRun run) => new()
    {
        Id = run.Id,
        Label = run.Label,
        RepoUrl = run.Target.Repo.Value,
        CommitSha = run.Target.Commit.Value,
        Exclusions = [.. run.Target.Exclusions],
        EngineKind = run.Engine.Kind,
        EngineEndpoint = run.Engine.Endpoint,
        EngineVersion = run.Engine.Version,
        IndexFingerprint = run.Engine.IndexFingerprint,
        EngineBackend = run.Engine.Backend.Canonical,
        SuiteStamp = run.SuiteStamp,
        Status = run.Status,
        CreatedAt = run.CreatedAt,
    };

    private static CellRow ToRow(RunCell cell)
    {
        var (variantId, variantName) = VariantSelectionCodec.Encode(cell.Variant);

        return new CellRow
        {
            Id = cell.Id,
            RunId = cell.RunId,
            QuestionId = cell.QuestionId,
            Repeat = cell.Repeat,
            Leg = cell.Leg,
            SubjectModelId = cell.SubjectModelId,
            LaneName = cell.LaneName,
            VariantId = variantId,
            VariantName = variantName,
            Arm = cell.Arm,
            Position = cell.Position,
            State = cell.State,
            Attempts = cell.Attempts,
            Owner = cell.Owner.Label,
            OwnerHost = cell.Owner.Host,
            OwnerPid = cell.Owner.Pid,
            ClaimedAt = cell.ClaimedAt,
            OutcomeKind = cell.OutcomeKind,
            OutcomeDetail = cell.OutcomeDetail,
        };
    }

    private static RunCell ToDomain(CellRow row) => new(
        row.Id, row.RunId, row.QuestionId, row.Repeat, row.Leg, row.SubjectModelId, row.LaneName,
        VariantSelectionCodec.Decode(row.VariantId, row.VariantName),
        row.Position, row.State, row.Attempts, Owner(row), row.ClaimedAt, row.OutcomeKind, row.OutcomeDetail)
    {
        Arm = row.Arm,
    };

    private static WorkerIdentity Owner(CellRow row) =>
        WorkerIdentity.Stored(row.Owner, row.OwnerHost, row.OwnerPid);

    private static Outcome<BenchRun> ToDomain(RunRow row) =>
        RepoUrl.Parse(row.RepoUrl).Match(
            repo => CommitSha.Parse(row.CommitSha).Match(
                commit => Outcome<BenchRun>.Success(new BenchRun(
                    row.Id,
                    row.Label,
                    new MeasurementTarget(repo, commit, row.Exclusions),
                    new EngineRef(row.EngineKind, row.EngineEndpoint, row.EngineVersion, row.IndexFingerprint)
                    {
                        Backend = BackendDeclaration.Read(row.EngineBackend),
                    },
                    row.SuiteStamp,
                    row.Status,
                    row.CreatedAt)),
                Outcome<BenchRun>.Failure),
            Outcome<BenchRun>.Failure);
}
