using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>The verdicts and hand-checks of the gate benchmark (E4) — the ingestion contract a native assessment and E5's
/// import both write through. A verdict names a FINDING that is stored (the cell's settled attempt produced it) or the
/// batch is refused whole; a verdict already stored is not stored again, so a replay changes nothing; every row reads
/// back under the rubric catalog or is skipped.</summary>
public sealed class PostgresGateVerdictStore(BenchDbContext db, TimeProvider clock) : IGateVerdictStore
{
    public async Task<Outcome<int>> RecordAsync(IReadOnlyList<GateVerdict> verdicts, CancellationToken cancellationToken)
    {
        if (verdicts.Count == 0)
        {
            return Outcome<int>.Success(0);
        }

        var cells = verdicts.Select(v => v.RunId).Distinct().ToList();
        var findings = (await db.GateFindings.AsNoTracking()
                .Where(f => cells.Contains(f.CellId))
                .Join(db.GateCells.AsNoTracking(), f => new { f.CellId, f.Attempt }, c => new { CellId = c.Id, Attempt = c.Attempts }, (f, _) => new { f.CellId, f.Ordinal })
                .ToListAsync(cancellationToken))
            .Select(f => (f.CellId, f.Ordinal))
            .ToHashSet();

        var stranger = verdicts.FirstOrDefault(v => !findings.Contains((v.RunId, v.FindingOrdinal)));
        if (stranger is not null)
        {
            return Outcome<int>.Failure(
                $"a verdict names finding {stranger.FindingOrdinal} of cell {stranger.RunId}, and no settled attempt of that cell stored it — the batch is refused whole");
        }

        var stored = (await db.GateVerdicts.AsNoTracking()
                .Where(v => cells.Contains(v.CellId))
                .Select(v => new { v.CellId, v.FindingOrdinal, v.RubricHash, v.AssessorId, v.BatchId })
                .ToListAsync(cancellationToken))
            .Select(v => (v.CellId, v.FindingOrdinal, v.RubricHash, v.AssessorId, v.BatchId))
            .ToHashSet();

        var now = clock.GetUtcNow();
        var fresh = verdicts
            .Where(v => stored.Add((v.RunId, v.FindingOrdinal, v.Rubric.Hash, v.Assessor.Value, v.BatchId)))
            .Select(v => ToRow(v, now))
            .ToList();

        db.GateVerdicts.AddRange(fresh);

        // One SaveChanges is one transaction: a batch's verdicts land together or not at all.
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        return Outcome<int>.Success(fresh.Count);
    }

    public async Task<IReadOnlyList<GateVerdict>> VerdictsAsync(IReadOnlyCollection<Guid> runIds, RubricCatalog catalog, CancellationToken cancellationToken)
    {
        var ids = runIds.ToList();
        var rows = await db.GateVerdicts.AsNoTracking()
            .Where(v => ids.Contains(v.CellId))
            .OrderBy(v => v.Id)
            .ToListAsync(cancellationToken);

        return [.. rows.SelectMany(row => ToDomain(row, catalog))];
    }

    public async Task<Outcome<HandCheck>> RecordHandCheckAsync(HandCheck check, CancellationToken cancellationToken)
    {
        db.GateHandChecks.Add(new GateHandCheckRow
        {
            Campaigns = [.. check.Campaigns],
            RubricId = check.Rubric.Id.Value,
            RubricKind = check.Rubric.Kind,
            RubricHash = check.Rubric.Hash,
            AssessorId = check.Assessor.Value,
            Read = check.Read,
            Agreed = check.Agreed,
            NoteHash = check.NoteHash,
            RecordedAt = check.RecordedAt,
        });

        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        return Outcome<HandCheck>.Success(check);
    }

    public async Task<IReadOnlyList<HandCheck>> HandChecksAsync(RubricCatalog catalog, CancellationToken cancellationToken)
    {
        var rows = await db.GateHandChecks.AsNoTracking().OrderBy(c => c.Id).ToListAsync(cancellationToken);

        return [.. rows.SelectMany(row => HandCheckOf(row, catalog))];
    }

    private static IEnumerable<HandCheck> HandCheckOf(GateHandCheckRow row, RubricCatalog catalog) =>
        (catalog.Resolve(row.RubricHash), GateReviewerId.Parse(row.AssessorId)) switch
        {
            (Outcome<Rubric>.Ok rubric, Outcome<GateReviewerId>.Ok assessor) =>
                HandCheck.Of(row.Campaigns, rubric.Value, assessor.Value, row.Read, row.Agreed, row.NoteHash, row.RecordedAt) is Outcome<HandCheck>.Ok ok ? [ok.Value] : [],
            _ => [],
        };

    private static GateVerdictRow ToRow(GateVerdict v, DateTimeOffset now)
    {
        var row = new GateVerdictRow
        {
            CellId = v.RunId,
            FindingOrdinal = v.FindingOrdinal,
            RubricId = v.Rubric.Id.Value,
            RubricKind = v.Rubric.Kind,
            RubricHash = v.Rubric.Hash,
            Kind = v.Reading.GetType().Name,
            AssessorId = v.Assessor.Value,
            BatchId = v.BatchId,
            PromptHash = v.PromptHash,
            AssessorFamilyMatches = v.AssessorFamilyMatches,
            RecordedAt = now,
        };

        return v.Reading switch
        {
            Verdict.Strict s => Strict(row, s),
            Verdict.Lenient l => Lenient(row, l),
            Verdict.AssessmentFailure f => Failure(row, f),
            _ => row,
        };
    }

    private static GateVerdictRow Strict(GateVerdictRow row, Verdict.Strict s)
    {
        row.Reading = s.Reading;
        row.Value = s.Value;
        row.SeverityFair = s.SeverityFair;
        row.Grounded = s.Grounded;
        row.ClusterHash = s.ClusterHash;
        row.SeedHit = s.SeedHit is SeedHit.Of hit ? hit.Seed.Value : string.Empty;
        return row;
    }

    private static GateVerdictRow Lenient(GateVerdictRow row, Verdict.Lenient l)
    {
        row.WorthHaving = l.WorthHaving;
        return row;
    }

    private static GateVerdictRow Failure(GateVerdictRow row, Verdict.AssessmentFailure f)
    {
        row.FailureCause = f.Cause;
        return row;
    }

    private static IEnumerable<GateVerdict> ToDomain(GateVerdictRow row, RubricCatalog catalog)
    {
        Verdict? reading = row.Kind switch
        {
            nameof(Verdict.Strict) => new Verdict.Strict(row.Reading, row.Value, row.SeverityFair, row.Grounded, row.ClusterHash, SeedHit.Parse(row.SeedHit)),
            nameof(Verdict.Lenient) => new Verdict.Lenient(row.WorthHaving),
            nameof(Verdict.AssessmentFailure) => new Verdict.AssessmentFailure(row.FailureCause),
            _ => null,
        };

        return (reading, GateReviewerId.Parse(row.AssessorId)) is ({ } r, Outcome<GateReviewerId>.Ok assessor)
            && GateVerdict.Under(catalog, row.RubricHash, row.CellId, row.FindingOrdinal, r, assessor.Value, row.BatchId, row.PromptHash, row.AssessorFamilyMatches) is Outcome<GateVerdict>.Ok ok
                ? [ok.Value]
                : [];
    }
}
