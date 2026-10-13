using Bench.Application.Scouts;
using Bench.Domain;
using Bench.Domain.Scouts;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>The scouts' read side over the store. It writes nothing: the cell, task and attempt reads are the store's own (composed, as
/// <see cref="PostgresProbeReads"/> does), and the report's one reading — the highest SETTLED generation — is the domain's.</summary>
public sealed class PostgresScoutReads(BenchDbContext db) : IScoutReads
{
    private PostgresScoutStore Store => new(db, TimeProvider.System);

    public async Task<IReadOnlyList<ScoutRun>> RecentRunsAsync(int limit, CancellationToken cancellationToken)
    {
        var rows = await db.ScoutRuns.AsNoTracking().OrderByDescending(r => r.CreatedAt).Take(Math.Max(1, limit)).ToListAsync(cancellationToken);

        return [.. rows.Select(ScoutRowMapping.ToDomain).OfType<Outcome<ScoutRun>.Ok>().Select(ok => ok.Value)];
    }

    public Task<Outcome<ScoutRun>> RunAsync(Guid runId, CancellationToken cancellationToken) => Store.LoadAsync(runId, cancellationToken);

    public Task<IReadOnlyList<ScoutTask>> TasksAsync(Guid runId, CancellationToken cancellationToken) => Store.TasksAsync(runId, cancellationToken);

    public Task<IReadOnlyList<ScoutCell>> CellsAsync(Guid runId, CancellationToken cancellationToken) => Store.CellsAsync(runId, cancellationToken);

    public async Task<IReadOnlyList<ScoutCell>> LatestSettledAsync(Guid runId, CancellationToken cancellationToken) =>
        ScoutGenerations.LatestSettled(await Store.CellsAsync(runId, cancellationToken));

    public async Task<IReadOnlyList<ScoutRecord>> ScoutsAsync(Guid cellId, CancellationToken cancellationToken)
    {
        var rows = await db.ScoutRecords.AsNoTracking().Where(s => s.CellId == cellId).OrderBy(s => s.Ordinal).ToListAsync(cancellationToken);

        return [.. rows.Select(ScoutRowMapping.ToDomain)];
    }

    public async Task<IReadOnlyList<ScoutCellRecord>> ScoutRecordsAsync(Guid runId, CancellationToken cancellationToken)
    {
        var rows = await db.ScoutRecords.AsNoTracking()
            .Where(s => db.ScoutCells.Any(c => c.Id == s.CellId && c.RunId == runId))
            .OrderBy(s => s.CellId).ThenBy(s => s.Ordinal)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new ScoutCellRecord(r.CellId, ScoutRowMapping.ToDomain(r)))];
    }

    public async Task<IReadOnlyList<ScoutAssessedClaim>> VerdictsAsync(Guid runId, CancellationToken cancellationToken)
    {
        var rows = await db.ScoutClaims.AsNoTracking()
            .Where(c => c.RunId == runId)
            .OrderBy(c => c.CellId).ThenBy(c => c.Ordinal).ThenBy(c => c.Index)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(ScoutRowMapping.ToDomain)];
    }

    public Task<IReadOnlyList<ScoutAttempt>> AttemptsAsync(Guid runId, CancellationToken cancellationToken) => Store.AttemptsAsync(runId, cancellationToken);
}
