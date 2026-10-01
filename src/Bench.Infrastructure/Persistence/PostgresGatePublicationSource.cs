using System.Collections;
using System.Globalization;
using Bench.Application.Gate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Bench.Infrastructure.Persistence;

/// <summary>Every row of every <c>gate_*</c> and <c>probe_*</c> table, read through the EF MODEL rather than through a list of
/// columns — so a column added next month is read, guarded and exported without anyone remembering to add it
/// here. That is the property that makes this the guard's input rather than a second opinion about it. The probe
/// tables joined the walk with S1 of the question-consultant plan: they are published under the same rule (D11), so
/// they go through the same guard and the same export rather than a second one.</summary>
public sealed class PostgresGatePublicationSource(BenchDbContext db) : IGatePublicationSource
{
    /// <summary>The gate's public-url column and the probes' — one set, because one guard reads both tables.</summary>
    public IReadOnlySet<string> PublicUrlColumns { get; } =
        new HashSet<string>([.. GateModel.PublicUrlColumns, .. ProbeModel.PublicUrlColumns], StringComparer.Ordinal);

    /// <summary>The gate entity types, as the model maps them — also what the structural guard test walks.</summary>
    public static IReadOnlyList<IEntityType> GateEntities(BenchDbContext context) => WithPrefix(context, GateModel.TablePrefix);

    /// <summary>The probe entity types, as the model maps them — what <c>ProbeEntitiesGuardTests</c> walks.</summary>
    public static IReadOnlyList<IEntityType> ProbeEntities(BenchDbContext context) => WithPrefix(context, ProbeModel.TablePrefix);

    /// <summary>Every entity type the public export carries and the guard re-reads: the gate's, then the probes'.</summary>
    public static IReadOnlyList<IEntityType> PublishedEntities(BenchDbContext context) => [.. GateEntities(context), .. ProbeEntities(context)];

    private static IReadOnlyList<IEntityType> WithPrefix(BenchDbContext context, string prefix) =>
        [.. context.Model.GetEntityTypes()
            .Where(e => (e.GetTableName() ?? string.Empty).StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(e => e.GetTableName(), StringComparer.Ordinal)];

    public async Task<IReadOnlyList<PublishedTable>> ReadAsync(CancellationToken cancellationToken)
    {
        var tables = new List<PublishedTable>();

        foreach (var entity in PublishedEntities(db))
        {
            tables.Add(new PublishedTable(entity.GetTableName()!, await RowsAsync(entity, cancellationToken)));
        }

        return tables;
    }

    private async Task<IReadOnlyList<PublishedRow>> RowsAsync(IEntityType entity, CancellationToken cancellationToken)
    {
        var rows = await ListAsync(entity, cancellationToken);
        // The claim owner (label, host, pid) never becomes a field of a public row: it names a machine and a process,
        // and it is sweep state, not a measurement. It stays in the database; the guard refuses it if it ever returns.
        var columns = entity.GetProperties()
            .Where(p => p.PropertyInfo is not null && !GatePublication.ClaimOwnerColumns.Contains(p.GetColumnName()))
            .ToList();
        var key = entity.FindPrimaryKey()!.Properties[0].PropertyInfo!;

        return [.. rows.Select(row => new PublishedRow(
            Text(key.GetValue(row)),
            [.. columns.Select(p => new PublishedField(p.GetColumnName(), Value(p.PropertyInfo!.GetValue(row))))]))];
    }

    /// <summary>The rows of one entity type, untracked. Reflection picks the type; the query itself is an ordinary
    /// typed <c>Set&lt;T&gt;()</c>, so EF translates exactly what it would for hand-written code.</summary>
    private Task<List<object>> ListAsync(IEntityType entity, CancellationToken cancellationToken) =>
        (Task<List<object>>)typeof(PostgresGatePublicationSource)
            .GetMethod(nameof(TypedListAsync), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .MakeGenericMethod(entity.ClrType)
            .Invoke(this, [cancellationToken])!;

    private async Task<List<object>> TypedListAsync<T>(CancellationToken cancellationToken)
        where T : class =>
        [.. await db.Set<T>().AsNoTracking().ToListAsync(cancellationToken)];

    /// <summary>A stored value as a published one. Text is what the guard reads; an enum is text too — its name is
    /// what the column holds — so nothing that reaches the database as a word escapes the guard.</summary>
    private static PublishedValue Value(object? value) => value switch
    {
        null => new PublishedValue.Text(string.Empty),
        string s => new PublishedValue.Text(s),
        Enum e => new PublishedValue.Text(e.ToString()),
        bool b => new PublishedValue.Flag(b),
        Guid g => new PublishedValue.Identity(g),
        DateTimeOffset d => new PublishedValue.Moment(d),
        double d => new PublishedValue.Real(d),
        IEnumerable sequence => Sequence(sequence),
        _ => new PublishedValue.Number(Convert.ToDecimal(value, CultureInfo.InvariantCulture)),
    };

    private static PublishedValue Sequence(IEnumerable sequence)
    {
        var items = sequence.Cast<object>().ToList();

        return items switch
        {
            _ when items.All(i => i is bool) => new PublishedValue.Flags([.. items.Cast<bool>()]),
            _ when items.All(i => i is double) => new PublishedValue.Reals([.. items.Cast<double>()]),
            _ when items.All(i => i is int or long or decimal) => new PublishedValue.Numbers([.. items.Select(i => Convert.ToDecimal(i, CultureInfo.InvariantCulture))]),
            _ => new PublishedValue.Texts([.. items.Select(Text)]),
        };
    }

    private static string Text(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
}
