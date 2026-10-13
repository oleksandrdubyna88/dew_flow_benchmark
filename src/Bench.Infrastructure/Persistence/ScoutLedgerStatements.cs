using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>The spend ledger's statements (§3.10) — every money move is ONE guarded UPDATE, in one transaction with the attempt row it
/// belongs to, re-checking the state its decision was taken on. The atomic operations it rests on:
/// <list type="bullet">
/// <item><b>reserve</b> — <c>UPDATE scout_runs SET Reserved += x WHERE Reserved + Settled + Unknown + x &lt;= Ceiling</c>: Postgres
/// re-evaluates the WHERE on the row a concurrent writer just committed, so two reservations cannot both fit into one headroom;
/// the attempt row's unique (cell, number) refuses a second reservation for the same claim attempt;</item>
/// <item><b>launching / settle / charge / release</b> — <c>UPDATE scout_attempts … WHERE State = &lt;the state it was decided on&gt;</c>:
/// exactly one transition per attempt, so an attempt settles once, never twice, and a launching one is never released.</item>
/// </list>
/// The arithmetic is <see cref="SpendLedger"/>'s; these statements are its durable form.</summary>
internal sealed class ScoutLedgerStatements(BenchDbContext db, TimeProvider clock)
{
    public async Task<ScoutReservation> ReserveAsync(Guid cellId, WorkerIdentity owner, Guid attemptId, decimal amountUsd, CancellationToken cancellationToken)
    {
        if (amountUsd <= 0m)
        {
            return new ScoutReservation.Refused($"a reservation of ${amountUsd} reserves nothing");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var cell = await db.ScoutCells.AsNoTracking()
            .Where(c => c.Id == cellId && c.State == CellState.Claimed && c.Owner == owner.Label && c.OwnerHost == owner.Host && c.OwnerPid == owner.Pid)
            .Select(c => new { c.RunId, c.Attempts })
            .FirstOrDefaultAsync(cancellationToken);

        if (cell is null)
        {
            return new ScoutReservation.Refused($"scout cell {cellId} is not claimed by {owner.Canonical} — only the claim's holder reserves for it");
        }

        var fitted = await db.ScoutRuns
            .Where(r => r.Id == cell.RunId && r.ReservedUsd + r.SettledUsd + r.UnknownUsd + amountUsd <= r.CeilingUsd)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.ReservedUsd, r => r.ReservedUsd + amountUsd), cancellationToken);

        if (fitted != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return await DoesNotFitAsync(cell.RunId, amountUsd, cancellationToken);
        }

        return await InsertAsync(new ScoutAttempt(attemptId, cell.RunId, cellId, cell.Attempts, amountUsd, ScoutAttemptState.Reserved, 0m, clock.GetUtcNow()), transaction, cancellationToken);
    }

    private async Task<ScoutReservation> InsertAsync(ScoutAttempt attempt, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        db.ScoutAttempts.Add(ScoutRowMapping.ToRow(attempt));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ScoutReservation.Reserved(attempt);
        }
        catch (DbUpdateException)
        {
            // The unique (cell, number) index fired: this claim attempt already holds a reservation. The ledger update rolls back with it.
            await transaction.RollbackAsync(cancellationToken);
            return new ScoutReservation.Refused($"attempt {attempt.Number} of scout cell {attempt.CellId} already holds a reservation — nothing was reserved twice");
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private async Task<ScoutReservation> DoesNotFitAsync(Guid runId, decimal amountUsd, CancellationToken cancellationToken)
    {
        var run = await db.ScoutRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);

        return run is null
            ? new ScoutReservation.Refused($"no scout run {runId}")
            : new ScoutReservation.DoesNotFit(new SpendLedger(run.CeilingUsd, run.ReservedUsd, run.SettledUsd, run.UnknownUsd).FreeUsd, amountUsd);
    }

    public async Task<Outcome<ScoutAttempt>> MarkLaunchingAsync(Guid attemptId, CancellationToken cancellationToken)
    {
        var marked = await db.ScoutAttempts
            .Where(a => a.Id == attemptId && a.State == ScoutAttemptState.Reserved)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.State, ScoutAttemptState.Launching), cancellationToken);

        return marked == 1 ? await AttemptAsync(attemptId, cancellationToken) : await RefusedAsync(attemptId, "only a reserved attempt is marked launching, once", cancellationToken);
    }

    public Task<Outcome<ScoutAttempt>> SettleAsync(Guid attemptId, decimal actualUsd, CancellationToken cancellationToken) =>
        actualUsd < 0m
            ? Task.FromResult(Outcome<ScoutAttempt>.Failure($"scout attempt {attemptId}: a cost of ${actualUsd} is not a cost"))
            : MoveAsync(attemptId, ScoutAttemptState.Launching, ScoutAttemptState.Settled, actualUsd, "only a launching attempt settles, once", cancellationToken);

    public Task<Outcome<ScoutAttempt>> ChargeUnknownAsync(Guid attemptId, CancellationToken cancellationToken) =>
        MoveAsync(attemptId, ScoutAttemptState.Launching, ScoutAttemptState.ChargedUnknown, 0m, "only a launching attempt is charged as unknown", cancellationToken);

    public Task<Outcome<ScoutAttempt>> ReleaseAsync(Guid attemptId, CancellationToken cancellationToken) =>
        MoveAsync(attemptId, ScoutAttemptState.Reserved, ScoutAttemptState.Released, 0m,
            "only an attempt that was never marked launching is released — a launching mark means it may have spent", cancellationToken);

    public async Task<IReadOnlyList<ScoutAttempt>> AttemptsAsync(Guid runId, CancellationToken cancellationToken)
    {
        var rows = await db.ScoutAttempts.AsNoTracking().Where(a => a.RunId == runId).OrderBy(a => a.ReservedAt).ThenBy(a => a.Number).ToListAsync(cancellationToken);

        return [.. rows.Select(ScoutRowMapping.ToDomain)];
    }

    /// <summary>One terminal transition: the attempt's guarded UPDATE from <paramref name="from"/>, then the run's ledger moved by the same
    /// arithmetic as <see cref="SpendLedger"/> — the reservation leaves, and the settled cost or the whole reservation as unknown is booked.</summary>
    private async Task<Outcome<ScoutAttempt>> MoveAsync(Guid attemptId, ScoutAttemptState from, ScoutAttemptState to, decimal actualUsd, string rule, CancellationToken cancellationToken)
    {
        var seen = await db.ScoutAttempts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attemptId, cancellationToken);

        if (seen is null)
        {
            return Outcome<ScoutAttempt>.Failure($"no scout attempt {attemptId}");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var moved = await db.ScoutAttempts
            .Where(a => a.Id == attemptId && a.State == from)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.State, to).SetProperty(a => a.SettledUsd, actualUsd), cancellationToken);

        if (moved != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return await RefusedAsync(attemptId, rule, cancellationToken);
        }

        var (settled, unknown) = Booked(to, actualUsd, seen.ReservedUsd);

        await db.ScoutRuns.Where(r => r.Id == seen.RunId).ExecuteUpdateAsync(
            s => s.SetProperty(r => r.ReservedUsd, r => r.ReservedUsd - seen.ReservedUsd)
                  .SetProperty(r => r.SettledUsd, r => r.SettledUsd + settled)
                  .SetProperty(r => r.UnknownUsd, r => r.UnknownUsd + unknown),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await AttemptAsync(attemptId, cancellationToken);
    }

    /// <summary>What a terminal transition books: a settlement its actual cost, an unknown charge the WHOLE reservation, a release nothing
    /// — <see cref="SpendLedger.Settle"/>, <see cref="SpendLedger.ChargeUnknown"/> and <see cref="SpendLedger.Release"/>.</summary>
    private static (decimal SettledUsd, decimal UnknownUsd) Booked(ScoutAttemptState to, decimal actualUsd, decimal reservedUsd) => to switch
    {
        ScoutAttemptState.Settled => (actualUsd, 0m),
        ScoutAttemptState.ChargedUnknown => (0m, reservedUsd),
        _ => (0m, 0m),
    };

    private async Task<Outcome<ScoutAttempt>> AttemptAsync(Guid attemptId, CancellationToken cancellationToken)
    {
        var row = await db.ScoutAttempts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attemptId, cancellationToken);

        return row is null ? Outcome<ScoutAttempt>.Failure($"no scout attempt {attemptId}") : Outcome<ScoutAttempt>.Success(ScoutRowMapping.ToDomain(row));
    }

    private async Task<Outcome<ScoutAttempt>> RefusedAsync(Guid attemptId, string rule, CancellationToken cancellationToken) =>
        (await AttemptAsync(attemptId, cancellationToken)).Match(
            attempt => Outcome<ScoutAttempt>.Failure($"{attempt.Label} is {attempt.State} — {rule}"),
            Outcome<ScoutAttempt>.Failure);
}
