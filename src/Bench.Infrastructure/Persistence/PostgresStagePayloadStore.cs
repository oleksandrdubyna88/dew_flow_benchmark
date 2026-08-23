using Bench.Application;
using Bench.Domain;
using Bench.Domain.Trace;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>The delivered-work pipeline's raw exchanges, stored so a score can be recomputed with no model
/// call.
///
/// <para><b>There is no update and no delete here, and their absence is the design.</b> A payload that
/// could be rewritten would make an old score unreproducible while still looking reproducible, and one
/// that could be aged out would end the recompute property outright. The size that buys is a budget line
/// the footprint read prints, not a leak to sweep.</para>
/// </summary>
public sealed class PostgresStagePayloadStore(BenchDbContext db) : IStagePayloadStore
{
    public async Task<Outcome<StagePayload>> AppendAsync(
        StagePayload payload, CancellationToken cancellationToken)
    {
        var duplicate = await db.StagePayloads.AsNoTracking().AnyAsync(
            row => row.ResultId == payload.ResultId
                && row.Stage == payload.Stage
                && row.Ordinal == payload.Ordinal,
            cancellationToken);

        // Refused by name rather than overwritten. Two payloads for one attempt would make "was this
        // re-asked" unanswerable, and that question is read straight off the ordinal.
        if (duplicate)
        {
            return Outcome<StagePayload>.Failure(
                $"result {payload.ResultId} already has a {payload.Stage} payload at ordinal "
                + $"{payload.Ordinal} — a stage payload is appended once and never rewritten");
        }

        db.StagePayloads.Add(new StagePayloadRow
        {
            Id = payload.Id,
            ResultId = payload.ResultId,
            Stage = payload.Stage,
            Ordinal = payload.Ordinal,
            PayloadJson = payload.PayloadJson,
            PromptHash = payload.PromptHash,
            Protocol = payload.Protocol,
            CreatedAt = payload.CreatedAt,
        });

        await db.SaveChangesAsync(cancellationToken);

        return Outcome<StagePayload>.Success(payload);
    }

    /// <summary>In PIPELINE order — decompose, then weigh, then the gate — and by ordinal within a stage,
    /// so the caller never has to know that a re-ask sorts after its first attempt.
    /// <para>
    /// <b>Ordered in memory, and that is not laziness.</b> The stage column stores the enum's NAME, so a
    /// database sort is alphabetical: <c>Coverage</c> before <c>Decompose</c> before <c>Weigh</c> — the gate
    /// replayed before the decomposition it gates. The rows for one result number at most a handful, so
    /// sorting them here costs nothing and says what the order actually is.
    /// </para></summary>
    public async Task<IReadOnlyList<StagePayload>> ForResultAsync(
        Guid resultId, CancellationToken cancellationToken)
    {
        var rows = await db.StagePayloads.AsNoTracking()
            .Where(row => row.ResultId == resultId)
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.OrderBy(row => row.Stage).ThenBy(row => row.Ordinal).Select(Read),
        ];
    }

    /// <summary>Counted in SQL rather than hydrated. The whole point of the read is that this table is the
    /// one expected to grow to tens of gigabytes, and pulling it into memory to measure it would be the
    /// defect the reliability rules already named once.</summary>
    public async Task<StagePayloadFootprint> FootprintAsync(CancellationToken cancellationToken)
    {
        var totals = await db.StagePayloads.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Rows = g.LongCount(),
                Results = g.Select(row => row.ResultId).Distinct().LongCount(),
                Bytes = (long?)g.Sum(row => (long)row.PayloadJson.Length),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return totals is null
            ? new StagePayloadFootprint(0, 0, 0)
            : new StagePayloadFootprint(totals.Rows, totals.Results, totals.Bytes ?? 0);
    }

    private static StagePayload Read(StagePayloadRow row) => new(
        row.Id,
        row.ResultId,
        row.Stage,
        row.Ordinal,
        row.PayloadJson,
        row.PromptHash,
        row.Protocol,
        row.CreatedAt);
}
