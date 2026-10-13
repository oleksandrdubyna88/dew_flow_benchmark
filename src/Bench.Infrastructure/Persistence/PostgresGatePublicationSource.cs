using System.Collections;
using System.Globalization;
using Bench.Application.Gate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Bench.Infrastructure.Persistence;

/// <summary>Every row of every <c>gate_*</c>, <c>probe_*</c> and <c>scout_*</c> table, read through the EF MODEL rather than through
/// a list of columns — so a column added next month is read, guarded and exported without anyone remembering to add it
/// here. That is the property that makes this the guard's input rather than a second opinion about it. The probe
/// tables joined the walk with S1 of the question-consultant plan, the scout tables with S1 of the scout calibration plan:
/// they are published under the same rule (D11), so they go through the same guard and the same export rather than a second one.</summary>
public sealed class PostgresGatePublicationSource(BenchDbContext db) : IGatePublicationSource
{
    /// <summary>The gate's public-url column, the probes' and the scouts' (none) — one set, because one guard reads every table.</summary>
    public IReadOnlySet<string> PublicUrlColumns { get; } =
        new HashSet<string>([.. GateModel.PublicUrlColumns, .. ProbeModel.PublicUrlColumns, .. ScoutModel.PublicUrlColumns], StringComparer.Ordinal);

    /// <summary>The gate entity types, as the model maps them — also what the structural guard test walks.</summary>
    public static IReadOnlyList<IEntityType> GateEntities(BenchDbContext context) => WithPrefix(context, GateModel.TablePrefix);

    /// <summary>The probe entity types, as the model maps them — what <c>ProbeEntitiesGuardTests</c> walks.</summary>
    public static IReadOnlyList<IEntityType> ProbeEntities(BenchDbContext context) => WithPrefix(context, ProbeModel.TablePrefix);

    /// <summary>The scout entity types, as the model maps them — what <c>ScoutEntitiesGuardTests</c> walks.</summary>
    public static IReadOnlyList<IEntityType> ScoutEntities(BenchDbContext context) => WithPrefix(context, ScoutModel.TablePrefix);

    /// <summary>Every entity type the public export carries and the guard re-reads: the gate's, then the probes', then the scouts'.</summary>
    public static IReadOnlyList<IEntityType> PublishedEntities(BenchDbContext context) => [.. GateEntities(context), .. ProbeEntities(context), .. ScoutEntities(context)];

    private static IReadOnlyList<IEntityType> WithPrefix(BenchDbContext context, string prefix) =>
        [.. context.Model.GetEntityTypes()
            .Where(e => (e.GetTableName() ?? string.Empty).StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(e => e.GetTableName(), StringComparer.Ordinal)];

    public async Task<IReadOnlyList<PublishedTable>> ReadAsync(CancellationToken cancellationToken)
    {
        var tables = new List<PublishedTable>();
        var privateRows = await PrivateRowsAsync(cancellationToken);

        foreach (var entity in PublishedEntities(db))
        {
            tables.Add(new PublishedTable(entity.GetTableName()!, await RowsAsync(entity, privateRows, cancellationToken)));
        }

        return tables;
    }

    /// <summary>The scouts' PRIVATE tasks and their cells (S4, §3.13): read first, so every scout row the walk reaches can be projected
    /// (<see cref="ScoutRowPublication"/>) before the guard reads it — a private task's url, statement, G, KEY FILES and claim paths never
    /// become a published value, and the export needs no key: the commitments are stored.</summary>
    private async Task<ScoutRowPublication.PrivateRows> PrivateRowsAsync(CancellationToken cancellationToken) =>
        ScoutRowPublication.PrivateRows.Of(
            await db.ScoutTasks.AsNoTracking().Where(t => t.Privacy == Bench.Domain.Scouts.ScoutTaskPrivacy.Private).ToListAsync(cancellationToken),
            await db.ScoutCells.AsNoTracking().ToListAsync(cancellationToken));

    private async Task<IReadOnlyList<PublishedRow>> RowsAsync(IEntityType entity, ScoutRowPublication.PrivateRows privateRows, CancellationToken cancellationToken)
    {
        var rows = (await ListAsync(entity, cancellationToken)).Select(row => ScoutRowPublication.Published(row, privateRows)).ToList();
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
