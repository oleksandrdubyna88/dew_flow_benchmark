using Bench.Domain.Runs;

namespace Bench.Domain.Lanes;

/// <summary>A lane's name: short, lower-case, quotable in a report and safe in a column heading.
/// <para>
/// The same <see cref="Slug"/> rule a variant name, a bank group and a reviewer key all share, and for the
/// reason that rule exists in one place: these identities appear beside each other in one report, so a
/// fourth nearly-identical spelling rule is how they drift.
/// </para></summary>
public sealed record LaneName
{
    private LaneName(string value) => Value = value;

    public string Value { get; }

    public static Outcome<LaneName> Parse(string? value)
    {
        var trimmed = Slug.Clean(value);

        return Slug.IsValid(trimmed)
            ? Outcome<LaneName>.Success(new LaneName(trimmed))
            : Outcome<LaneName>.Failure($"'{trimmed}' is not a usable lane name — {Slug.Rule}");
    }

    public override string ToString() => Value;
}

/// <summary>
/// One row of the lane catalog: a named, hashed tool surface.
///
/// <para><b>Why a catalog at all.</b> Lane has been an axis of the measurement tuple since the founding
/// plan — as a bare NAME, resolving to nothing. A name alone cannot say which tools were offered, in which
/// words, under which instruction; so every comparison over lanes has been a comparison of labels. This row
/// is what a label finally resolves to.</para>
///
/// <para><b>It stores no <c>LaneId</c> on a cell, deliberately.</b> The variant catalog needed a foreign key
/// because variant was a brand-new axis; <see cref="RunCell.LaneName"/> already carries a stable, never-
/// renamed identity, and a catalog changes what a name RESOLVES TO rather than how a cell stores it. So this
/// costs zero schema change to <c>cells</c> — one fewer migration racing a parallel session.</para>
/// </summary>
public sealed record ToolLane
{
    private ToolLane(
        Guid id,
        LaneName name,
        string displayName,
        LaneDefinition definition,
        DateTimeOffset createdAt,
        DateTimeOffset retiredAt)
    {
        Id = id;
        Name = name;
        DisplayName = displayName;
        Definition = definition;
        CreatedAt = createdAt;
        RetiredAt = retiredAt;
    }

    public Guid Id { get; }

    public LaneName Name { get; }

    /// <summary>What a matrix column is headed with — "bridge · 4 tools · doctrine-first". Falls back to the
    /// name, so a heading is never an empty string.</summary>
    public string DisplayName { get; }

    public LaneDefinition Definition { get; }

    public DateTimeOffset CreatedAt { get; }

    /// <summary>Unset while active — the "it has not happened" shape this repository uses rather than a
    /// nullable every reader unwraps.</summary>
    public DateTimeOffset RetiredAt { get; }

    public bool IsActive => RetiredAt == default;

    public string Hash => Definition.Hash;

    /// <summary>What a report quotes: the name, plus enough of the hash to prove which surface it was.</summary>
    public string Stamp => $"{Name.Value}#{Hash[..12]}";

    public static Outcome<ToolLane> Create(
        string? name, string? displayName, LaneDefinition definition, DateTimeOffset now) =>
        LaneName.Parse(name).Match(
            parsed => Outcome<ToolLane>.Success(new ToolLane(
                Guid.CreateVersion7(),
                parsed,
                Display(displayName, parsed),
                definition,
                now,
                retiredAt: default)),
            Outcome<ToolLane>.Failure);

    /// <summary>Rebuilds a stored row. The name is re-parsed rather than trusted: a hand-edited row is a
    /// real event, and the catalog should refuse it by name instead of serving it.</summary>
    public static Outcome<ToolLane> Rehydrate(
        Guid id,
        string? name,
        string? displayName,
        LaneDefinition definition,
        DateTimeOffset createdAt,
        DateTimeOffset retiredAt) =>
        LaneName.Parse(name).Match(
            parsed => Outcome<ToolLane>.Success(new ToolLane(
                id, parsed, Display(displayName, parsed), definition, createdAt, retiredAt)),
            Outcome<ToolLane>.Failure);

    /// <summary>Takes the lane out of the active catalog, keeping its identity so historical cells still
    /// resolve. Returns a new value; the one held elsewhere is untouched.</summary>
    public Outcome<ToolLane> Retire(DateTimeOffset now) =>
        IsActive
            ? Outcome<ToolLane>.Success(new ToolLane(Id, Name, DisplayName, Definition, CreatedAt, now))
            : Outcome<ToolLane>.Failure($"lane '{Name}' is already retired, since {RetiredAt:u}");

    /// <summary>
    /// What a planned leg carries.
    ///
    /// <para>The EXISTING <see cref="Lane"/> axis record, not a new selection type — and this is where the
    /// dead field comes alive. <c>Lane.Preamble</c> was declared in the founding tuple, documented, and read
    /// by nothing; it is the doctrine, and a catalog row is what finally gives it a value. Reviving it is
    /// cheaper than deprecating it and building a parallel concept, and it makes the axis legible in the one
    /// place a reader already looks.</para>
    /// </summary>
    public Lane Select() => new(Name.Value, Definition.Doctrine);

    private static string Display(string? displayName, LaneName name)
    {
        var trimmed = (displayName ?? string.Empty).Trim();
        return trimmed.Length > 0 ? trimmed : name.Value;
    }
}
