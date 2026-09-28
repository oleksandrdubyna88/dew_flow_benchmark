using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Runs;
using Bench.Infrastructure.Process;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>The durable gate store — <see cref="PostgresRunStore"/>'s guarantees over gate cells, reusing its two
/// load-bearing shapes: the claim is one UPDATE guarded on <c>State == Pending</c>, and the sweep is
/// ownership-checked (<see cref="WorkerIdentity.IsProvablyGoneOn"/>), never time alone.
/// <para>
/// What is stricter here: the sweep's hand-back is ONE guarded UPDATE per stranded cell that re-checks
/// everything the decision was taken on — still claimed, the same owner (label, host, pid), the same claim time,
/// the same attempt count — and chooses requeue or abandon inside the statement. Two sweepers racing over one
/// dead owner's cell therefore hand it back once: the second one's WHERE no longer matches. And it never touches
/// a cell of a run that already ended; a finished or failed campaign is a record, not a queue.
/// </para></summary>
public sealed class PostgresGateStore(BenchDbContext db, TimeProvider clock) : IGateStore
{
    /// <summary>How many lost claim races in a row mean "the queue is contended", as in <see cref="PostgresRunStore"/>.</summary>
    private const int ClaimAttempts = 8;

    public const string NoPendingCell = "no pending gate cell to claim";

    public async Task<Outcome<GateRun>> PlanAsync(GateRun run, IReadOnlyList<GateCell> cells, CancellationToken cancellationToken)
    {
        var refusal = (cells.Count, cells.All(c => c.RunId == run.Id), await db.GateRuns.AnyAsync(r => r.Id == run.Id, cancellationToken)) switch
        {
            (0, _, _) => "a gate run with no cells would look started and could never finish",
            (_, false, _) => $"every cell of run {run.Id} names that run — a cell planned under another run id would be orphaned",
            (_, _, true) => $"gate run {run.Id} already exists",
            _ => string.Empty,
        };

        if (refusal.Length > 0)
        {
            return Outcome<GateRun>.Failure(refusal);
        }

        db.GateRuns.Add(GateRowMapping.ToRow(run));
        db.GateCells.AddRange(cells.Select(GateRowMapping.ToRow));

        // One SaveChanges is one transaction: the run and every cell land together or not at all.
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        return Outcome<GateRun>.Success(run);
    }

    public async Task<Outcome<GateRun>> LoadAsync(Guid runId, CancellationToken cancellationToken)
    {
        var row = await db.GateRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);

        return row is null ? Outcome<GateRun>.Failure($"no gate run {runId}") : Outcome<GateRun>.Success(GateRowMapping.ToDomain(row));
    }

    public async Task<IReadOnlyList<GateRun>> RecentAsync(int limit, CancellationToken cancellationToken)
    {
        var rows = await db.GateRuns.AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);

        return [.. rows.Select(GateRowMapping.ToDomain)];
    }

    public async Task<IReadOnlyList<Guid>> RunsOfSuiteAsync(string suiteStamp, CancellationToken cancellationToken) =>
        await db.GateRuns.AsNoTracking()
            .Where(r => r.SuiteStamp == suiteStamp)
            .OrderBy(r => r.CreatedAt)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

    public async Task<Outcome<GateCell>> ClaimNextAsync(
        Guid runId, WorkerIdentity owner, ProductPin pin, CancellationToken cancellationToken)
    {
        var refusal = (owner.CanClaim, pin.IsPinned) switch
        {
            (false, _) => "a claim needs an owner with a host and a pid — an unowned claim can never be swept correctly",
            (_, false) => "a gate cell is claimed under a product pin, and none was given — the pin is stored per cell at claim time",
            _ => string.Empty,
        };

        return refusal.Length > 0 ? Outcome<GateCell>.Failure(refusal) : await ClaimLoopAsync(runId, owner, pin, [], cancellationToken);
    }

    public async Task<Outcome<GateCell>> ClaimNextAmongAsync(
        Guid runId, WorkerIdentity owner, ProductPin pin, IReadOnlyCollection<GateReviewerId> among, CancellationToken cancellationToken)
    {
        if (among.Count == 0)
        {
            return Outcome<GateCell>.Failure($"gate run {runId} has {NoPendingCell} among no reviewers — every endpoint is full");
        }

        var refusal = (owner.CanClaim, pin.IsPinned) switch
        {
            (false, _) => "a claim needs an owner with a host and a pid — an unowned claim can never be swept correctly",
            (_, false) => "a gate cell is claimed under a product pin, and none was given — the pin is stored per cell at claim time",
            _ => string.Empty,
        };

        return refusal.Length > 0
            ? Outcome<GateCell>.Failure(refusal)
            : await ClaimLoopAsync(runId, owner, pin, [.. among.Select(r => r.Value)], cancellationToken);
    }

    public async Task<Outcome<GateCell>> SettleAsync(
        Guid cellId, WorkerIdentity owner, GateSettlement settlement, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // The whole identity is in the WHERE, as in PostgresRunStore: a settle from a worker whose claim was swept
        // away must not overwrite the retry that replaced it.
        var settled = await db.GateCells
            .Where(c => c.Id == cellId && c.State == CellState.Claimed
                     && c.Owner == owner.Label && c.OwnerHost == owner.Host && c.OwnerPid == owner.Pid)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.State, CellState.Settled), cancellationToken);

        if (settled != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return await ExplainSettleRefusalAsync(cellId, owner, cancellationToken);
        }

        var row = await db.GateCells.FirstAsync(c => c.Id == cellId, cancellationToken);
        GateFactsMapping.Apply(row, settlement);
        db.GateFindings.AddRange(Findings(settlement).Select(f => GateRowMapping.ToRow(cellId, row.Attempts, f)));

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();

        return await CellAsync(cellId, cancellationToken);
    }

    public async Task<GateSweepReport> SweepAsync(TimeSpan staleAfter, CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow() - staleAfter;
        var stranded = await StrandedAsync(cutoff, cancellationToken);

        var requeued = 0;
        var abandoned = 0;

        foreach (var cell in stranded)
        {
            if (await HandBackAsync(cell, cancellationToken) == 1)
            {
                (requeued, abandoned) = cell.Attempts >= Claimable.MaxAttempts ? (requeued, abandoned + 1) : (requeued + 1, abandoned);
            }
        }

        return new GateSweepReport(requeued, abandoned);
    }

    public async Task<Outcome<GateRunStatus>> AdvanceAsync(Guid runId, GateRunStatus to, CancellationToken cancellationToken)
    {
        // Forward-only, guarded in the WHERE: Running only from Planned; a terminal state only from a live one.
        _ = to switch
        {
            GateRunStatus.Running => await db.GateRuns
                .Where(r => r.Id == runId && r.Status == GateRunStatus.Planned)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, to), cancellationToken),
            GateRunStatus.Finished or GateRunStatus.Failed => await db.GateRuns
                .Where(r => r.Id == runId && (r.Status == GateRunStatus.Planned || r.Status == GateRunStatus.Running))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, to), cancellationToken),
            _ => 0,
        };

        return await CurrentStatusAsync(runId, to, cancellationToken);
    }

    public async Task<Outcome<GateCell>> CellAsync(Guid cellId, CancellationToken cancellationToken)
    {
        var row = await db.GateCells.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cellId, cancellationToken);

        return row is null ? Outcome<GateCell>.Failure($"no gate cell {cellId}") : GateRowMapping.ToDomain(row);
    }

    public async Task<IReadOnlyList<GateCell>> CellsAsync(Guid runId, CancellationToken cancellationToken)
    {
        var rows = await db.GateCells.AsNoTracking()
            .Where(c => c.RunId == runId)
            .OrderBy(c => c.Position).ThenBy(c => c.Slot)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(GateRowMapping.ToDomain).OfType<Outcome<GateCell>.Ok>().Select(ok => ok.Value)];
    }

    public Task<IReadOnlyList<GateRunRecord>> FactsAsync(Guid runId, CancellationToken cancellationToken) =>
        new GateRecordReader(db).ReadAsync(runId, cancellationToken);

    public async Task<Outcome<int>> RecordArtifactsAsync(IReadOnlyList<ArtifactRef> refs, CancellationToken cancellationToken)
    {
        var paths = refs.Select(r => r.Path.Value).ToList();
        var taken = await db.GateArtifacts.AsNoTracking().Where(a => paths.Contains(a.RelativePath)).Select(a => a.RelativePath).ToListAsync(cancellationToken);

        if (taken.Count > 0 || paths.Distinct(StringComparer.Ordinal).Count() != paths.Count)
        {
            return Outcome<int>.Failure(
                $"artefact path(s) already recorded: {string.Join(", ", taken.DefaultIfEmpty("a path twice in one commit"))} — an artefact is written once, under its own attempt");
        }

        var now = clock.GetUtcNow();
        db.GateArtifacts.AddRange(refs.Select(r => GateRowMapping.ToRow(r, now)));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Outcome<int>.Success(refs.Count);
        }
        catch (DbUpdateException)
        {
            // The unique path index fired: another writer recorded one of these paths between our read and our
            // write. Nothing of this batch landed — SaveChanges is one transaction.
            return Outcome<int>.Failure("an artefact path was recorded concurrently — nothing of this batch was written");
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    public async Task<IReadOnlyList<ArtifactRef>> ArtifactsAsync(Guid runId, CancellationToken cancellationToken)
    {
        var rows = await db.GateArtifacts.AsNoTracking().Where(a => a.RunId == runId).OrderBy(a => a.Id).ToListAsync(cancellationToken);

        return [.. rows.Select(GateRowMapping.ToDomain).OfType<Outcome<ArtifactRef>.Ok>().Select(ok => ok.Value)];
    }

    public Task<bool> HasFindingsAsync(CancellationToken cancellationToken) => db.GateFindings.AnyAsync(cancellationToken);

    public async Task<Outcome<GateCell>> HandBackUnmeasuredAsync(Guid cellId, WorkerIdentity owner, int attempt, string cause, CancellationToken cancellationToken)
    {
        var handed = await Live(db.GateCells)
            .Where(c => c.Id == cellId && c.State == CellState.Claimed && c.Attempts == attempt
                        && c.Owner == owner.Label && c.OwnerHost == owner.Host && c.OwnerPid == owner.Pid)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.State, CellState.Pending)
                      .SetProperty(c => c.Attempts, c => c.Attempts - 1)
                      .SetProperty(c => c.Owner, string.Empty)
                      .SetProperty(c => c.OwnerHost, string.Empty)
                      .SetProperty(c => c.OwnerPid, 0)
                      .SetProperty(c => c.FailureText, cause),
                cancellationToken);

        return handed == 1
            ? await CellAsync(cellId, cancellationToken)
            : Outcome<GateCell>.Failure($"gate cell {cellId} is not claimed by {owner.Canonical} at attempt {attempt} — nothing was handed back");
    }

    public async Task<IReadOnlyList<(GateReviewerId Reviewer, string ReferencesHash)>> ReferenceHashesAsync(Guid runId, CancellationToken cancellationToken)
    {
        var rows = await db.GateCells.AsNoTracking()
            .Where(c => c.RunId == runId && c.FactsRecorded && c.ReferencesHash != string.Empty)
            .Select(c => new { c.ReviewerId, c.ReferencesHash })
            .Distinct()
            .ToListAsync(cancellationToken);

        return [.. rows.SelectMany(r => GateReviewerId.Parse(r.ReviewerId) is Outcome<GateReviewerId>.Ok ok ? [(ok.Value, r.ReferencesHash)] : Array.Empty<(GateReviewerId, string)>())];
    }

    private async Task<Outcome<GateCell>> ClaimLoopAsync(
        Guid runId, WorkerIdentity owner, ProductPin pin, IReadOnlyList<string> among, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < ClaimAttempts; attempt++)
        {
            var candidate = await NextPendingIdAsync(runId, among, cancellationToken);

            if (candidate == Guid.Empty)
            {
                return Outcome<GateCell>.Failure($"gate run {runId} has {NoPendingCell}");
            }

            if (await TryClaimAsync(candidate, owner, pin, cancellationToken))
            {
                return await CellAsync(candidate, cancellationToken);
            }
        }

        return Outcome<GateCell>.Failure($"lost {ClaimAttempts} claim races in a row — the queue is contended, retry");
    }

    /// <summary>The next pending cell of a run that has NOT ended — a terminal run is never claimed from — in the
    /// MATRIX's order: slot, then the reviewer's position inside the slot. (It was position first, which reversed the
    /// nesting and ran a reviewer's second repeat before its first.) <paramref name="among"/>, when not empty, limits
    /// the candidates to those reviewers — the lanes' capacity-aware claim.</summary>
    private async Task<Guid> NextPendingIdAsync(Guid runId, IReadOnlyList<string> among, CancellationToken cancellationToken) =>
        await db.GateCells.AsNoTracking()
            .Where(c => c.RunId == runId && c.State == CellState.Pending
                     && db.GateRuns.Any(r => r.Id == runId && r.Status != GateRunStatus.Finished && r.Status != GateRunStatus.Failed))
            .Where(c => among.Count == 0 || among.Contains(c.ReviewerId))
            .OrderBy(c => c.Slot).ThenBy(c => c.Position)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>The atomic step: <c>State == Pending</c> in the WHERE, the attempt count incremented in the same
    /// statement, the pin written with the claim.</summary>
    private async Task<bool> TryClaimAsync(Guid cellId, WorkerIdentity owner, ProductPin pin, CancellationToken cancellationToken) =>
        await db.GateCells
            .Where(c => c.Id == cellId && c.State == CellState.Pending)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.State, CellState.Claimed)
                      .SetProperty(c => c.Owner, owner.Label)
                      .SetProperty(c => c.OwnerHost, owner.Host)
                      .SetProperty(c => c.OwnerPid, owner.Pid)
                      .SetProperty(c => c.ClaimedAt, clock.GetUtcNow())
                      .SetProperty(c => c.Attempts, c => c.Attempts + 1)
                      .SetProperty(c => c.PinBinarySha256, pin.BinarySha256)
                      .SetProperty(c => c.PinVersionText, pin.VersionText)
                      .SetProperty(c => c.PinGitSha, pin.GitSha)
                      .SetProperty(c => c.PinDirtyCaptured, pin.DirtyFiles.WasCaptured)
                      .SetProperty(c => c.PinDirtyFiles, pin.DirtyFiles.Value)
                      .SetProperty(c => c.PinCheckedTree, pin.CheckedTree),
                cancellationToken) == 1;

    /// <summary>Stale claims whose owner is provably gone, of runs that have not ended.</summary>
    private async Task<List<GateCellRow>> StrandedAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var stale = await Live(db.GateCells.AsNoTracking())
            .Where(c => c.State == CellState.Claimed && c.ClaimedAt <= cutoff)
            .ToListAsync(cancellationToken);

        return [.. stale.Where(IsOrphan)];
    }

    /// <summary>THE hand-back: one statement, guarded on every fact the decision read — state, owner triple, claim
    /// time, attempt count, and a run still live — choosing requeue or abandonment inside it. Zero rows means
    /// something else moved the cell first, which is the ordinary outcome of a concurrent sweep.</summary>
    private Task<int> HandBackAsync(GateCellRow seen, CancellationToken cancellationToken)
    {
        var abandon = seen.Attempts >= Claimable.MaxAttempts;
        var detail = $"abandoned after {seen.Attempts} attempts — a cell that kills its host will kill the next one";

        return Live(db.GateCells)
            .Where(c => c.Id == seen.Id && c.State == CellState.Claimed
                     && c.Owner == seen.Owner && c.OwnerHost == seen.OwnerHost && c.OwnerPid == seen.OwnerPid
                     && c.ClaimedAt == seen.ClaimedAt && c.Attempts == seen.Attempts)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.State, abandon ? CellState.Abandoned : CellState.Pending)
                      .SetProperty(c => c.Owner, string.Empty)
                      .SetProperty(c => c.OwnerHost, string.Empty)
                      .SetProperty(c => c.OwnerPid, 0)
                      .SetProperty(c => c.OutcomeKind, abandon ? GateCellOutcomeKind.Failed : GateCellOutcomeKind.None)
                      .SetProperty(c => c.FailureKind, abandon ? FailureKind.Interrupted : FailureKind.None)
                      .SetProperty(c => c.FailureText, abandon ? detail : string.Empty),
                cancellationToken);
    }

    /// <summary>Cells of runs that have not ended — the sweep's whole world.</summary>
    private IQueryable<GateCellRow> Live(IQueryable<GateCellRow> cells) =>
        cells.Where(c => db.GateRuns.Any(r => r.Id == c.RunId && r.Status != GateRunStatus.Finished && r.Status != GateRunStatus.Failed));

    private static bool IsOrphan(GateCellRow row) =>
        WorkerIdentity.Stored(row.Owner, row.OwnerHost, row.OwnerPid)
            .IsProvablyGoneOn(WorkerLiveness.ThisHost, WorkerLiveness.ProcessIsAlive);

    private static IReadOnlyList<GateFinding> Findings(GateSettlement settlement) =>
        settlement is GateSettlement.Completed completed ? completed.Findings : [];

    private async Task<Outcome<GateRunStatus>> CurrentStatusAsync(Guid runId, GateRunStatus to, CancellationToken cancellationToken)
    {
        var current = await db.GateRuns.AsNoTracking().Where(r => r.Id == runId).Select(r => (GateRunStatus?)r.Status).FirstOrDefaultAsync(cancellationToken);

        return current switch
        {
            null => Outcome<GateRunStatus>.Failure($"no gate run {runId}"),
            var held when held != to && held is GateRunStatus.Finished or GateRunStatus.Failed => Outcome<GateRunStatus>.Failure(
                $"gate run {runId} is {held} — a run that ended does not move again; a new campaign is a new run"),
            var held => Outcome<GateRunStatus>.Success(held.Value),
        };
    }

    private async Task<Outcome<GateCell>> ExplainSettleRefusalAsync(Guid cellId, WorkerIdentity owner, CancellationToken cancellationToken)
    {
        var row = await db.GateCells.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cellId, cancellationToken);

        return row switch
        {
            null => Outcome<GateCell>.Failure($"no gate cell {cellId}"),
            { State: not CellState.Claimed } => Outcome<GateCell>.Failure(
                $"gate cell {cellId} is {row.State}, not Claimed — only a claimed cell can settle"),
            _ => Outcome<GateCell>.Failure(
                $"gate cell {cellId} is held by {WorkerIdentity.Stored(row.Owner, row.OwnerHost, row.OwnerPid).Canonical}, not {owner.Canonical} "
                + "— a swept-away claim must not overwrite the retry that replaced it"),
        };
    }
}
