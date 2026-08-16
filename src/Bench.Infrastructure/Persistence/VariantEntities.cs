namespace Bench.Infrastructure.Persistence;

/// <summary>A catalog row. The definition is stored as its JSON rather than as columns per axis,
/// deliberately: a new axis is then a definition that reads differently, not a migration — while an axis
/// this build does not know is still refused when the row is read, never dropped.
/// <para>
/// <see cref="Hash"/> is derived from the definition and stored anyway, so "is this the same recipe under
/// another name" is an indexed lookup instead of a scan that re-parses every row.
/// </para></summary>
public sealed class VariantRow
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string DefinitionJson { get; set; } = "{}";

    public string Hash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Default while active — the same shape <see cref="CellRow.ClaimedAt"/> uses for "has not
    /// happened yet", rather than a nullable every read has to unwrap.</summary>
    public DateTimeOffset RetiredAt { get; set; }
}
