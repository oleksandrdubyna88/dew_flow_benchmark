using Bench.Application.Scouts;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;
using Bench.Infrastructure.Process;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>The durable scout store — <see cref="PostgresProbeStore"/>'s load-bearing shapes over scout cells: the claim is one UPDATE
/// guarded on <c>State == Pending</c>, the sweep is ownership-checked (<see cref="WorkerIdentity.IsProvablyGoneOn"/>) with ONE guarded
/// hand-back re-checking everything it decided on, and a re-run appends a generation with the unique index as the race guard. What is
/// the scouts' own: cells are claimed in the run's SCHEDULE order (one at a time, §3.4), a settle writes the per-scout records with
/// the cell, and every money move is the ledger's (<see cref="ScoutLedgerStatements"/>).</summary>
public sealed class PostgresScoutStore(BenchDbContext db, TimeProvider clock) : IScoutStore
{
    private const int ClaimAttempts = 8;

    /// <summary>The phrase a claim refusal carries when nothing is pending — the end of the campaign's queue.</summary>
    public const string NoPendingCell = "no pending scout cell to claim";

    private ScoutLedgerStatements Ledger => new(db, clock);

    public async Task<Outcome<ScoutRun>> PlanAsync(ScoutRun run, IReadOnlyList<ScoutTask> tasks, IReadOnlyList<ScoutCell> cells, CancellationToken cancellationToken)
    {
        var refusal = PlanRefusal(run, tasks, cells);

        if (refusal.Length == 0 && await db.ScoutRuns.AnyAsync(r => r.Id == run.Id, cancellationToken))
        {
            refusal = $"scout run {run.Id} already exists";
        }

        if (refusal.Length > 0)
        {
            return Outcome<ScoutRun>.Failure(refusal);
        }

        db.ScoutRuns.Add(ScoutRowMapping.ToRow(run));
        db.ScoutTasks.AddRange(tasks.Select(t => ScoutRowMapping.ToRow(run.Id, t)));
        db.ScoutCells.AddRange(cells.Select(ScoutRowMapping.ToRow));

        // One SaveChanges is one transaction: the run, its tasks and every cell land together or not at all.
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        return Outcome<ScoutRun>.Success(run);
    }

    private static string PlanRefusal(ScoutRun run, IReadOnlyList<ScoutTask> tasks, IReadOnlyList<ScoutCell> cells) =>
        (cells.Count, cells.All(c => c.RunId == run.Id)) switch
        {
            (0, _) => "a scout run with no cells would look started and could never finish",
            (_, false) => $"every cell of run {run.Id} names that run — a cell planned under another run id would be orphaned",
            _ => TaskRefusal(run, tasks, cells),
        };

    private static string TaskRefusal(ScoutRun run, IReadOnlyList<ScoutTask> tasks, IReadOnlyList<ScoutCell> cells)
    {
        var frozen = run.Tasks.Select(t => t.Value).ToHashSet(StringComparer.Ordinal);
        var given = tasks.Select(t => t.Id.Value).ToHashSet(StringComparer.Ordinal);

        return (frozen.SetEquals(given) && given.Count == tasks.Count, cells.All(c => frozen.Contains(c.Task.Value))) switch
        {
            (false, _) => "the tasks given are not exactly the run's frozen task list",
            (_, false) => "a cell names a task the run does not hold",
            _ => string.Empty,
        };
    }

    public async Task<Outcome<ScoutRun>> LoadAsync(Guid runId, CancellationToken cancellationToken)
    {
        var row = await db.ScoutRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);

        return row is null ? Outcome<ScoutRun>.Failure($"no scout run {runId}") : ScoutRowMapping.ToDomain(row);
    }

    public async Task<IReadOnlyList<ScoutTask>> TasksAsync(Guid runId, CancellationToken cancellationToken)
    {
        var rows = await db.ScoutTasks.AsNoTracking().Where(t => t.RunId == runId).OrderBy(t => t.TaskId).ToListAsync(cancellationToken);

        return [.. rows.Select(ScoutRowMapping.ToDomain).OfType<Outcome<ScoutTask>.Ok>().Select(ok => ok.Value)];
    }

    public async Task<Outcome<ScoutCell>> ClaimNextAsync(Guid runId, WorkerIdentity owner, ProductPin pin, CancellationToken cancellationToken)
    {
        var refusal = (owner.CanClaim, pin.IsPinned) switch
        {
            (false, _) => "a claim needs an owner with a host and a pid — an unowned claim can never be swept correctly",
            (_, false) => "a scout cell is claimed under a product pin, and none was given — the pin is stored per cell at claim time",
            _ => string.Empty,
        };

        return refusal.Length > 0 ? Outcome<ScoutCell>.Failure(refusal) : await ClaimLoopAsync(runId, owner, pin, cancellationToken);
    }

    public async Task<Outcome<ScoutCell>> SettleAsync(
        Guid cellId, WorkerIdentity owner, ScoutSettlement settlement, IReadOnlyList<ScoutRecord> scouts, CancellationToken cancellationToken)
    {
        var decided = (await CellAsync(cellId, cancellationToken)).Match(cell => ScoutCellLifecycle.Settle(cell, settlement), Outcome<ScoutCell>.Failure);

        if (decided is Outcome<ScoutCell>.Fail refused)
        {
            return refused;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // The whole identity is in the WHERE: a settle from a worker whose claim was swept away must not overwrite the retry.
        var settled = await db.ScoutCells
            .Where(c => c.Id == cellId && c.State == CellState.Claimed
                     && c.Owner == owner.Label && c.OwnerHost == owner.Host && c.OwnerPid == owner.Pid)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.State, CellState.Settled), cancellationToken);

        if (settled != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return await ExplainSettleRefusalAsync(cellId, owner, cancellationToken);
        }

        var row = await db.ScoutCells.FirstAsync(c => c.Id == cellId, cancellationToken);
        ScoutRowMapping.Apply(row, settlement);
        db.ScoutRecords.AddRange(scouts.Select(s => ScoutRowMapping.ToRow(cellId, s)));

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();

        return await CellAsync(cellId, cancellationToken);
    }

    public async Task<ScoutSweepReport> SweepAsync(TimeSpan staleAfter, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var cutoff = now - staleAfter;
        var stale = await db.ScoutCells.AsNoTracking()
            .Where(c => c.State == CellState.Claimed && (c.ClaimedAt <= cutoff || c.ClaimedAt > now))
            .ToListAsync(cancellationToken);

        var (requeued, abandoned) = (0, 0);

        foreach (var cell in stale.Where(IsOrphan))
        {
            if (await HandBackAsync(cell, cancellationToken) == 1)
            {
                (requeued, abandoned) = Abandons(cell) ? (requeued, abandoned + 1) : (requeued + 1, abandoned);
            }
        }

        return new ScoutSweepReport(requeued, abandoned);
    }

    public async Task<Outcome<ScoutCell>> NextGenerationAsync(Guid cellId, Guid newId, CancellationToken cancellationToken)
    {
        var seed = await db.ScoutCells.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cellId, cancellationToken);

        if (seed is null)
        {
            return Outcome<ScoutCell>.Failure($"no scout cell {cellId}");
        }

        var lineage = await db.ScoutCells.AsNoTracking()
            .Where(c => c.RunId == seed.RunId && c.TaskId == seed.TaskId && c.Arm == seed.Arm && c.Repeat == seed.Repeat)
            .ToListAsync(cancellationToken);

        var next = ScoutCellLifecycle.NextGeneration([.. lineage.Select(ScoutRowMapping.ToDomain).OfType<Outcome<ScoutCell>.Ok>().Select(ok => ok.Value)], newId);

        return next is Outcome<ScoutCell>.Ok ok ? await AppendAsync(ok.Value, cancellationToken) : next;
    }

    public async Task<Outcome<ScoutCell>> CellAsync(Guid cellId, CancellationToken cancellationToken)
    {
        var row = await db.ScoutCells.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cellId, cancellationToken);

        return row is null ? Outcome<ScoutCell>.Failure($"no scout cell {cellId}") : ScoutRowMapping.ToDomain(row);
    }

    public async Task<IReadOnlyList<ScoutCell>> CellsAsync(Guid runId, CancellationToken cancellationToken)
    {
        var rows = await db.ScoutCells.AsNoTracking()
            .Where(c => c.RunId == runId)
            .OrderBy(c => c.Slot).ThenBy(c => c.Position).ThenBy(c => c.Generation)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(ScoutRowMapping.ToDomain).OfType<Outcome<ScoutCell>.Ok>().Select(ok => ok.Value)];
    }

    public Task<ScoutReservation> ReserveAsync(Guid cellId, WorkerIdentity owner, Guid attemptId, decimal reservedUsd, CancellationToken cancellationToken) =>
        Ledger.ReserveAsync(cellId, owner, attemptId, reservedUsd, cancellationToken);

    public Task<Outcome<ScoutAttempt>> MarkLaunchingAsync(Guid attemptId, CancellationToken cancellationToken) =>
        Ledger.MarkLaunchingAsync(attemptId, cancellationToken);

    public Task<Outcome<ScoutAttempt>> SettleAttemptAsync(Guid attemptId, decimal actualUsd, CancellationToken cancellationToken) =>
        Ledger.SettleAsync(attemptId, actualUsd, cancellationToken);

    public Task<Outcome<ScoutAttempt>> ChargeUnknownAsync(Guid attemptId, CancellationToken cancellationToken) =>
        Ledger.ChargeUnknownAsync(attemptId, cancellationToken);

    public Task<Outcome<ScoutAttempt>> ReleaseAsync(Guid attemptId, CancellationToken cancellationToken) =>
        Ledger.ReleaseAsync(attemptId, cancellationToken);

    public Task<IReadOnlyList<ScoutAttempt>> AttemptsAsync(Guid runId, CancellationToken cancellationToken) =>
        Ledger.AttemptsAsync(runId, cancellationToken);

    private async Task<Outcome<ScoutCell>> AppendAsync(ScoutCell next, CancellationToken cancellationToken)
    {
        db.ScoutCells.Add(ScoutRowMapping.ToRow(next));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return await CellAsync(next.Id, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique index fired: another re-run appended this generation between our read and our write. Nothing landed.
            return Outcome<ScoutCell>.Failure($"generation {next.Generation} of {next.Label}'s lineage was appended concurrently — nothing was written; read the lineage again");
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private async Task<Outcome<ScoutCell>> ClaimLoopAsync(Guid runId, WorkerIdentity owner, ProductPin pin, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < ClaimAttempts; attempt++)
        {
            var candidate = await db.ScoutCells.AsNoTracking()
                .Where(c => c.RunId == runId && c.State == CellState.Pending)
                .OrderBy(c => c.Slot).ThenBy(c => c.Position).ThenBy(c => c.Generation)
                .Select(c => c.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (candidate == Guid.Empty)
            {
                return Outcome<ScoutCell>.Failure($"scout run {runId} has {NoPendingCell}");
            }

            if (await TryClaimAsync(candidate, owner, pin, cancellationToken))
            {
                return await CellAsync(candidate, cancellationToken);
            }
        }

        return Outcome<ScoutCell>.Failure($"lost {ClaimAttempts} claim races in a row — the queue is contended, retry");
    }

    /// <summary>The atomic step: <c>State == Pending</c> in the WHERE, the attempt count moved and the pin written in the same statement.</summary>
    private async Task<bool> TryClaimAsync(Guid cellId, WorkerIdentity owner, ProductPin pin, CancellationToken cancellationToken) =>
        await db.ScoutCells
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

    private static bool Abandons(ScoutCellRow seen) => seen.Attempts >= Claimable.MaxAttempts;

    /// <summary>THE hand-back: one statement guarded on every fact the decision read — state, owner triple, claim time, attempts.</summary>
    private Task<int> HandBackAsync(ScoutCellRow seen, CancellationToken cancellationToken)
    {
        var abandon = Abandons(seen);

        return db.ScoutCells
            .Where(c => c.Id == seen.Id && c.State == CellState.Claimed
                     && c.Owner == seen.Owner && c.OwnerHost == seen.OwnerHost && c.OwnerPid == seen.OwnerPid
                     && c.ClaimedAt == seen.ClaimedAt && c.Attempts == seen.Attempts)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.State, abandon ? CellState.Abandoned : CellState.Pending)
                      .SetProperty(c => c.Owner, string.Empty)
                      .SetProperty(c => c.OwnerHost, string.Empty)
                      .SetProperty(c => c.OwnerPid, 0)
                      .SetProperty(c => c.Reason, abandon ? ScoutReason.Abandoned : ScoutReason.None),
                cancellationToken);
    }

    private static bool IsOrphan(ScoutCellRow row) =>
        WorkerIdentity.Stored(row.Owner, row.OwnerHost, row.OwnerPid).IsProvablyGoneOn(WorkerLiveness.ThisHost, WorkerLiveness.ProcessIsAlive);

    private async Task<Outcome<ScoutCell>> ExplainSettleRefusalAsync(Guid cellId, WorkerIdentity owner, CancellationToken cancellationToken)
    {
        var row = await db.ScoutCells.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cellId, cancellationToken);

        return row switch
        {
            null => Outcome<ScoutCell>.Failure($"no scout cell {cellId}"),
            { State: not CellState.Claimed } => Outcome<ScoutCell>.Failure($"scout cell {cellId} is {row.State}, not Claimed — only a claimed cell can settle"),
            _ => Outcome<ScoutCell>.Failure(
                $"scout cell {cellId} is held by {WorkerIdentity.Stored(row.Owner, row.OwnerHost, row.OwnerPid).Canonical}, not {owner.Canonical} "
                + "— a swept-away claim must not overwrite the retry that replaced it"),
        };
    }
}
