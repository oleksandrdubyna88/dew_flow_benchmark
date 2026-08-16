using Bench.Application.Variants;
using Bench.Domain;
using Bench.Domain.Variants;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>The durable variant catalog.
/// <para>
/// Names are unique in the DATABASE. The check below is a courtesy that produces a readable refusal; the
/// unique index is what actually holds when two sessions add the same name at the same moment, which is
/// why the insert is also guarded by catching the conflict rather than trusting the check.
/// </para></summary>
public sealed class PostgresVariantCatalog(BenchDbContext db) : IVariantCatalog
{
    public async Task<Outcome<RetrievalVariant>> AddAsync(
        RetrievalVariant variant, CancellationToken cancellationToken)
    {
        if (await db.Variants.AnyAsync(v => v.Name == variant.Name.Value, cancellationToken))
        {
            return Taken(variant.Name.Value);
        }

        db.Variants.Add(ToRow(variant));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Taken(variant.Name.Value);
        }

        return Outcome<RetrievalVariant>.Success(variant);
    }

    public async Task<Outcome<IReadOnlyList<RetrievalVariant>>> ListAsync(
        bool includeRetired, CancellationToken cancellationToken)
    {
        var rows = await db.Variants.AsNoTracking()
            .Where(v => includeRetired || v.RetiredAt == default)
            .OrderBy(v => v.Name)
            .ToListAsync(cancellationToken);

        var variants = new List<RetrievalVariant>(rows.Count);
        foreach (var row in rows)
        {
            // A row that cannot be read fails the whole listing by name. Skipping it would render a
            // catalog that is quietly missing a configuration somebody is measuring against.
            var read = ToDomain(row);
            if (read is Outcome<RetrievalVariant>.Fail failure)
            {
                return Outcome<IReadOnlyList<RetrievalVariant>>.Failure(failure.Reason);
            }

            variants.Add(((Outcome<RetrievalVariant>.Ok)read).Value);
        }

        return Outcome<IReadOnlyList<RetrievalVariant>>.Success(variants);
    }

    public async Task<Outcome<RetrievalVariant>> FindAsync(string name, CancellationToken cancellationToken)
    {
        var row = await db.Variants.AsNoTracking().FirstOrDefaultAsync(v => v.Name == name, cancellationToken);

        return row is null
            ? Outcome<RetrievalVariant>.Failure($"no variant '{name}' in the catalog")
            : ToDomain(row);
    }

    public async Task<Outcome<RetrievalVariant>> RetireAsync(
        string name, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var found = await FindAsync(name, cancellationToken);

        if (found is not Outcome<RetrievalVariant>.Ok(var variant))
        {
            return found;
        }

        var retired = variant.Retire(now);
        if (retired is not Outcome<RetrievalVariant>.Ok(var value))
        {
            return retired;
        }

        await db.Variants
            .Where(v => v.Id == value.Id && v.RetiredAt == default)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.RetiredAt, now), cancellationToken);

        return Outcome<RetrievalVariant>.Success(value);
    }

    private static Outcome<RetrievalVariant> Taken(string name) =>
        Outcome<RetrievalVariant>.Failure(
            $"the name '{name}' is already in the catalog — a variant is added and retired, never redefined, so that "
            + "every result naming it still means what it said");

    private static VariantRow ToRow(RetrievalVariant variant) => new()
    {
        Id = variant.Id,
        Name = variant.Name.Value,
        DisplayName = variant.DisplayName,
        DefinitionJson = VariantJson.Write(variant.Definition),
        Hash = variant.Hash,
        CreatedAt = variant.CreatedAt,
        RetiredAt = variant.RetiredAt,
    };

    private static Outcome<RetrievalVariant> ToDomain(VariantRow row) =>
        VariantJson.Read(row.DefinitionJson).Match(
            definition => RetrievalVariant.Rehydrate(
                row.Id, row.Name, row.DisplayName, definition, row.CreatedAt, row.RetiredAt),
            reason => Outcome<RetrievalVariant>.Failure($"variant '{row.Name}' cannot be read — {reason}"));
}
