using Bench.Application.Scouts;
using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bench.Infrastructure.Persistence;

/// <summary>The analysis's writes over the scout tables (S3): claim verdicts, each claim at most once — the unique index on (cell, ordinal,
/// index) is the guard, and a verdict for a claim that already has one is SKIPPED, never an overwrite — and a scored cell's recomputed recall.
/// Nothing here moves a cell's state or a dollar; those stay <see cref="PostgresScoutStore"/>'s.</summary>
public sealed class PostgresScoutVerdicts(BenchDbContext db) : IScoutVerdicts
{
    private const string UniqueViolation = "23505";

    public async Task<int> StoreVerdictsAsync(IReadOnlyList<ScoutAssessedClaim> verdicts, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var fresh = await FreshAsync(verdicts, cancellationToken);

            if (fresh.Count == 0)
            {
                return 0;
            }

            db.ScoutClaims.AddRange(fresh.Select(ScoutRowMapping.ToRow));

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return fresh.Count;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolation })
            {
                // Another pass stored one of these between our read and our write; nothing of ours landed — read again, keep the rest.
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        }

        return 0;
    }

    /// <summary>The verdicts whose claim has none in the store yet — and each claim once, should a batch carry it twice.</summary>
    private async Task<IReadOnlyList<ScoutAssessedClaim>> FreshAsync(IReadOnlyList<ScoutAssessedClaim> verdicts, CancellationToken cancellationToken)
    {
        var cells = verdicts.Select(v => v.Key.CellId).Distinct().ToList();
        var held = (await db.ScoutClaims.AsNoTracking().Where(c => cells.Contains(c.CellId)).Select(c => new { c.CellId, c.Ordinal, c.Index }).ToListAsync(cancellationToken))
            .Select(c => new ScoutClaimKey(c.CellId, c.Ordinal, c.Index))
            .ToHashSet();

        return [.. verdicts.Where(v => held.Add(v.Key))];
    }

    public async Task<IReadOnlyList<ScoutClaimKey>> AssessedKeysAsync(Guid runId, CancellationToken cancellationToken) =>
        [.. (await db.ScoutClaims.AsNoTracking().Where(c => c.RunId == runId).Select(c => new { c.CellId, c.Ordinal, c.Index }).ToListAsync(cancellationToken))
            .Select(c => new ScoutClaimKey(c.CellId, c.Ordinal, c.Index))];

    public async Task<Outcome<ScoutCell>> RescoreAsync(Guid cellId, ScoutRescore rescore, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var moved = await db.ScoutCells
            .Where(c => c.Id == cellId && c.State == CellState.Settled && c.Outcome == ScoutCellOutcome.Scored)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.RecallFoundCaptured, true).SetProperty(c => c.RecallFound, rescore.Found)
                      .SetProperty(c => c.RecallTotalCaptured, true).SetProperty(c => c.RecallTotal, rescore.Total),
                cancellationToken);

        if (moved != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Outcome<ScoutCell>.Failure($"scout cell {cellId} is not a settled, scored cell — only a scored cell carries recall");
        }

        foreach (var scout in rescore.Scouts)
        {
            List<string> files = [.. scout.KeyFiles];
            await db.ScoutRecords.Where(r => r.CellId == cellId && r.Ordinal == scout.Ordinal)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.KeyFiles, files), cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var row = await db.ScoutCells.AsNoTracking().FirstAsync(c => c.Id == cellId, cancellationToken);
        return ScoutRowMapping.ToDomain(row);
    }
}
