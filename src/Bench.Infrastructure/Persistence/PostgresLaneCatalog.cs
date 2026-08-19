using Bench.Application.Lanes;
using Bench.Domain;
using Bench.Domain.Lanes;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>The durable lane catalog.
/// <para>
/// Names are unique in the DATABASE. The check below is a courtesy that produces a readable refusal; the
/// unique index is what actually holds when two sessions add the same name in the same moment, which is why
/// the insert also catches the conflict rather than trusting the check. The same shape as
/// <see cref="PostgresVariantCatalog"/>, deliberately — two catalogs of immutable named rows should not be
/// two different mechanisms.
/// </para></summary>
public sealed class PostgresLaneCatalog(BenchDbContext db) : ILaneCatalog
{
    public async Task<Outcome<ToolLane>> AddAsync(ToolLane lane, CancellationToken cancellationToken)
    {
        if (await db.Lanes.AnyAsync(l => l.Name == lane.Name.Value, cancellationToken))
        {
            return Taken(lane.Name.Value);
        }

        db.Lanes.Add(ToRow(lane));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Taken(lane.Name.Value);
        }

        return Outcome<ToolLane>.Success(lane);
    }

    public async Task<Outcome<IReadOnlyList<ToolLane>>> ListAsync(
        bool includeRetired, CancellationToken cancellationToken)
    {
        var rows = await db.Lanes.AsNoTracking()
            .Where(l => includeRetired || l.RetiredAt == default)
            .OrderBy(l => l.Name)
            .ToListAsync(cancellationToken);

        var lanes = new List<ToolLane>(rows.Count);
        foreach (var row in rows)
        {
            // A row that cannot be read fails the whole listing by name. Skipping it would render a catalog
            // quietly missing a surface somebody is measuring against.
            var read = ToDomain(row);
            if (read is Outcome<ToolLane>.Fail failure)
            {
                return Outcome<IReadOnlyList<ToolLane>>.Failure(failure.Reason);
            }

            lanes.Add(((Outcome<ToolLane>.Ok)read).Value);
        }

        return Outcome<IReadOnlyList<ToolLane>>.Success(lanes);
    }

    public async Task<Outcome<ToolLane>> FindAsync(string name, CancellationToken cancellationToken)
    {
        var row = await db.Lanes.AsNoTracking().FirstOrDefaultAsync(l => l.Name == name, cancellationToken);

        return row is null
            ? Outcome<ToolLane>.Failure($"no lane '{name}' in the catalog")
            : ToDomain(row);
    }

    public async Task<Outcome<ToolLane>> RetireAsync(
        string name, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var found = await FindAsync(name, cancellationToken);

        if (found is not Outcome<ToolLane>.Ok(var lane))
        {
            return found;
        }

        var retired = lane.Retire(now);
        if (retired is not Outcome<ToolLane>.Ok(var value))
        {
            return retired;
        }

        // Guarded on still-active, so two sessions retiring at once produce one retirement date rather than
        // the later one overwriting the earlier — the date a report quotes must be when it actually stopped.
        await db.Lanes
            .Where(l => l.Id == value.Id && l.RetiredAt == default)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.RetiredAt, now), cancellationToken);

        return Outcome<ToolLane>.Success(value);
    }

    private static Outcome<ToolLane> Taken(string name) =>
        Outcome<ToolLane>.Failure(
            $"the name '{name}' is already in the catalog — a lane is added and retired, never redefined, so that "
            + "every result naming it still means what it said");

    private static LaneRow ToRow(ToolLane lane) => new()
    {
        Id = lane.Id,
        Name = lane.Name.Value,
        DisplayName = lane.DisplayName,
        DefinitionJson = LaneJson.Write(lane.Definition),
        Hash = lane.Hash,
        ToolsHash = lane.Definition.ToolsHash,
        DescriptionSet = lane.Definition.DescriptionSet,
        DoctrineHash = lane.Definition.DoctrineHash,
        Presentation = lane.Definition.Presentation.ToString(),
        CreatedAt = lane.CreatedAt,
        RetiredAt = lane.RetiredAt,
    };

    /// <summary>Rebuilt from the JSON alone — the projected columns are never read back into the domain.
    /// <para>They are an index, not a second source of truth. Reading them would make a hand-edited column
    /// able to contradict the definition it projects, and the row would then answer two different questions
    /// two different ways.</para></summary>
    private static Outcome<ToolLane> ToDomain(LaneRow row) =>
        LaneJson.Read(row.DefinitionJson).Match(
            definition => ToolLane.Rehydrate(
                row.Id, row.Name, row.DisplayName, definition, row.CreatedAt, row.RetiredAt),
            reason => Outcome<ToolLane>.Failure($"lane '{row.Name}' cannot be read — {reason}"));
}
