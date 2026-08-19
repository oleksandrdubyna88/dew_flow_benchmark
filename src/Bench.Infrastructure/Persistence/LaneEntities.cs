namespace Bench.Infrastructure.Persistence;

/// <summary>
/// One row of the lane catalog.
///
/// <para><b>Four columns beside the JSON are projections of it</b> — <c>ToolsHash</c>, <c>DescriptionSet</c>,
/// <c>DoctrineHash</c> and <c>Presentation</c>. They exist so that "which wording wins, holding the tool set
/// and the presentation fixed" is a <c>GROUP BY</c> rather than JSON parsing in SQL, which is the shape of
/// every question this catalog was built to answer.</para>
///
/// <para>They cannot drift from the definition, and not because anything keeps them in step: the row is
/// immutable — a lane is added and retired, never edited — so they are written once, at insert, from the
/// value they project.</para>
/// </summary>
public sealed class LaneRow
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string DefinitionJson { get; set; } = "{}";

    /// <summary>The whole surface's identity — what a result's stamp quotes.</summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>The tool subset alone, so a leaderboard over wordings can hold it fixed.</summary>
    public string ToolsHash { get; set; } = string.Empty;

    /// <summary>The description set's NAME, not a hash: it is short, it is what an operator typed, and a
    /// report heading wants to print it rather than twelve hex characters.</summary>
    public string DescriptionSet { get; set; } = string.Empty;

    public string DoctrineHash { get; set; } = string.Empty;

    /// <summary>Stored as its name rather than an ordinal. An enum written as a number is a column that
    /// changes meaning the day a member is inserted, and this one is read by reports and by humans.</summary>
    public string Presentation { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Default while active — the same "has not happened yet" shape <see cref="VariantRow"/> and
    /// <see cref="CellRow"/> use, rather than a nullable every read unwraps.</summary>
    public DateTimeOffset RetiredAt { get; set; }
}
