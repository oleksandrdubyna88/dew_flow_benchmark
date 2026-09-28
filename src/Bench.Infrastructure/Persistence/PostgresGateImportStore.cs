using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>The database half of E5's import. An imported cell is written SETTLED, with its facts, findings and artefact
/// refs, in ONE transaction — the campaign row too when it is the campaign's first cell (a FINISHED run, so no sweep and
/// no claim ever reaches it). The summary-only rows go to <c>gate_summaries</c>, which no report reads into a run figure.</summary>
public sealed class PostgresGateImportStore(BenchDbContext db, TimeProvider clock) : IGateImportStore
{
    public async Task<ImportedCellState> CellStateAsync(Guid cellId, CancellationToken cancellationToken)
    {
        if (!await db.GateCells.AsNoTracking().AnyAsync(c => c.Id == cellId, cancellationToken))
        {
            return ImportedCellState.Absent;
        }

        var sha = await db.GateArtifacts.AsNoTracking()
            .Where(a => a.CellId == cellId && a.RelativePath.EndsWith("/" + GateImportWriter.SourceFile))
            .Select(a => a.Sha256)
            .FirstOrDefaultAsync(cancellationToken);

        return new ImportedCellState(true, sha ?? string.Empty);
    }

    public async Task<Outcome<ImportedCell>> ImportCellAsync(ImportedCell cell, CancellationToken cancellationToken)
    {
        var refusal = await RefusalAsync(cell, cancellationToken);
        if (refusal.Length > 0)
        {
            return Outcome<ImportedCell>.Failure(refusal);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        if (!await db.GateRuns.AnyAsync(r => r.Id == cell.Campaign.Id, cancellationToken))
        {
            db.GateRuns.Add(GateRowMapping.ToRow(cell.Campaign));
        }

        var row = GateRowMapping.ToRow(cell.Cell);
        GateFactsMapping.Apply(row, cell.Settlement);
        db.GateCells.Add(row);
        db.GateFindings.AddRange(cell.Settlement.Findings.Select(f => GateRowMapping.ToRow(cell.Cell.Id, cell.Cell.Attempts, f)));
        var now = clock.GetUtcNow();
        db.GateArtifacts.AddRange(cell.Artifacts.Select(a => GateRowMapping.ToRow(a, now)));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Outcome<ImportedCell>.Success(cell);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Outcome<ImportedCell>.Failure($"the cell could not be written — {ex.InnerException?.Message.Split('\n')[0] ?? ex.Message}; nothing of it was stored");
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    public async Task<Outcome<int>> RecordSummaryAsync(SummaryCitation citation, GateKind gate, SummaryTable table, CancellationToken cancellationToken)
    {
        var stored = await db.GateSummaries.AsNoTracking()
            .Where(s => s.DocumentSha256 == citation.DocumentSha256 && s.Section == table.Section)
            .Select(s => new { s.RowOrdinal, s.Metric, s.Gate, s.Source })
            .ToListAsync(cancellationToken);

        if (stored.FirstOrDefault(s => s.Gate != gate || s.Source != citation.Source) is { } other)
        {
            return Outcome<int>.Failure(
                $"{citation.Document} § {table.Section} is stored as a {other.Gate} table from {other.Source} — imported again as a {gate} table from {citation.Source}, it is refused rather than kept under the first");
        }

        var held = stored.Select(s => (s.RowOrdinal, s.Metric)).ToHashSet();

        var now = clock.GetUtcNow();
        var fresh = table.Rows
            .SelectMany(row => row.Figures.Select(figure => (row, figure)))
            .Where(p => !held.Contains((p.row.Ordinal, p.figure.Metric)))
            .Select(p => new GateSummaryRow
            {
                Gate = gate,
                Source = citation.Source,
                Document = citation.Document,
                Section = table.Section,
                DocumentSha256 = citation.DocumentSha256,
                RowOrdinal = p.row.Ordinal,
                Label = p.row.Label,
                Metric = p.figure.Metric,
                Captured = p.figure.Captured,
                Value = p.figure.Value,
                ImportedAt = now,
            })
            .ToList();

        db.GateSummaries.AddRange(fresh);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Outcome<int>.Success(fresh.Count);
        }
        catch (DbUpdateException)
        {
            return Outcome<int>.Failure($"{citation.Document} § {table.Section} was stored concurrently — nothing of this import was written; run it again");
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private async Task<string> RefusalAsync(ImportedCell cell, CancellationToken cancellationToken)
    {
        var existing = await db.GateRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == cell.Campaign.Id, cancellationToken);

        return (await db.GateCells.AsNoTracking().AnyAsync(c => c.Id == cell.Cell.Id, cancellationToken), existing) switch
        {
            (true, _) => $"cell {cell.Cell.Id} is already stored",
            (_, { } run) when run.Gate != cell.Campaign.Gate || run.SuiteStamp != cell.Campaign.SuiteStamp || run.Source != cell.Campaign.Source.Label =>
                $"campaign {cell.Campaign.Id} is stored as a {run.Gate} run of {run.SuiteStamp} from {run.Source} — this cell says otherwise",
            _ => string.Empty,
        };
    }
}
