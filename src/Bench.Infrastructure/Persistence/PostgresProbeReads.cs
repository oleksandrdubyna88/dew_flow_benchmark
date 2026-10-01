using Bench.Application.Probes;
using Bench.Domain;
using Bench.Domain.Probes;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>The probes' read side over the store. It writes nothing: the cell reads are the store's own (composed, as
/// <see cref="PostgresGateReads"/> composes the verdict store), and the one reading a report needs — the highest SETTLED
/// generation per lineage — is <see cref="ProbeGenerations.LatestSettled"/>, the domain's, so the page and the CLI agree.</summary>
public sealed class PostgresProbeReads(BenchDbContext db) : IProbeReads
{
    private PostgresProbeStore Store => new(db, TimeProvider.System);

    public async Task<IReadOnlyList<ProbeRun>> RecentRunsAsync(int limit, CancellationToken cancellationToken)
    {
        var rows = await db.ProbeRuns.AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);

        return [.. rows.Select(ProbeRowMapping.ToDomain).OfType<Outcome<ProbeRun>.Ok>().Select(ok => ok.Value)];
    }

    public Task<Outcome<ProbeRun>> RunAsync(Guid runId, CancellationToken cancellationToken) => Store.LoadAsync(runId, cancellationToken);

    public Task<IReadOnlyList<ProbeCell>> CellsAsync(Guid runId, CancellationToken cancellationToken) => Store.CellsAsync(runId, cancellationToken);

    public async Task<IReadOnlyList<ProbeCell>> LatestSettledAsync(Guid runId, CancellationToken cancellationToken) =>
        ProbeGenerations.LatestSettled(await Store.CellsAsync(runId, cancellationToken));
}
