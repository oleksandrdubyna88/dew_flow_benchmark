using Bench.Domain;
using Bench.Domain.Gate;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>Reads a run's settled cells back as <see cref="GateRunRecord"/>s — the shape the report is computed
/// from. The run is the record's CAMPAIGN and the cell is its run id: only one attempt of a cell ever settles, so
/// the cell names the session. Two queries, never one per cell: a campaign is hundreds of cells.</summary>
internal sealed class GateRecordReader(BenchDbContext db)
{
    public async Task<IReadOnlyList<GateRunRecord>> ReadAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await db.GateRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);

        if (run is null)
        {
            return [];
        }

        var cells = await db.GateCells.AsNoTracking()
            .Where(c => c.RunId == runId && c.FactsRecorded)
            .OrderBy(c => c.Position).ThenBy(c => c.Slot)
            .ToListAsync(cancellationToken);

        var ids = cells.Select(c => c.Id).ToList();
        var findings = (await db.GateFindings.AsNoTracking().Where(f => ids.Contains(f.CellId)).ToListAsync(cancellationToken))
            .ToLookup(f => (f.CellId, f.Attempt));

        var campaign = GateRowMapping.ToDomain(run);

        return [.. cells.SelectMany(cell => Record(campaign, cell, findings[(cell.Id, cell.Attempts)]))];
    }

    /// <summary>One record, or none when a stored id no longer parses — skipped rather than failing the whole read,
    /// the <c>RecentAsync</c> precedent: one bad row must not hide a campaign.</summary>
    private static IEnumerable<GateRunRecord> Record(GateRun run, GateCellRow row, IEnumerable<GateFindingRow> findings)
    {
        if (GateRowMapping.ToDomain(row) is not Outcome<GateCell>.Ok cell)
        {
            yield break;
        }

        var pin = GateRowMapping.Pin(row);

        yield return new GateRunRecord(
            run.Id,
            row.Id,
            GateScope.Of(run.SuiteStamp, run.Gate, pin, row.SettingsHash),
            cell.Value.Task,
            cell.Value.Reviewer,
            row.Repeat,
            row.Attempts,
            GateFactsMapping.Facts(row),
            [.. findings.OrderBy(f => f.Ordinal).Select(GateRowMapping.ToDomain).OfType<Outcome<GateFinding>.Ok>().Select(ok => ok.Value)],
            run.Source);
    }
}
