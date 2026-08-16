namespace Bench.Domain.Variants;

/// <summary>A variant's name: short, lower-case, quotable in a report and safe in a column heading.
/// <para>
/// Typed rather than a string because it is an identity every result carries. A name that differs only in
/// case would pass a unique index and read as two configurations in one report — the exact class of
/// defect the catalog exists to prevent. The shape itself is <see cref="Slug"/>, shared with the bank's
/// group and reviewer keys: these identities appear side by side in a report, so they cannot be allowed
/// to drift into three nearly-identical rules.
/// </para></summary>
public sealed record VariantName
{
    private VariantName(string value) => Value = value;

    public string Value { get; }

    public static Outcome<VariantName> Parse(string? value)
    {
        var trimmed = Slug.Clean(value);

        return Slug.IsValid(trimmed)
            ? Outcome<VariantName>.Success(new VariantName(trimmed))
            : Outcome<VariantName>.Failure($"'{trimmed}' is not a usable variant name — {Slug.Rule}");
    }

    public override string ToString() => Value;
}

/// <summary>One row of the variant catalog: a named, hashed retrieval configuration.
/// <para>
/// <b>A definition is never edited.</b> Results name the variant they ran under, so changing a recipe in
/// place would relabel every number already measured against it — the same failure
/// <see cref="Suites.Suite"/> freezes questions to prevent. Changing a configuration means adding a row
/// and retiring the old one, and both then resolve forever.
/// </para></summary>
public sealed record RetrievalVariant
{
    private RetrievalVariant(
        Guid id,
        VariantName name,
        string displayName,
        VariantDefinition definition,
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

    public VariantName Name { get; }

    /// <summary>What a matrix cell is labelled with — "sparse · bge-m3 · 256". Falls back to the name, so
    /// a column is never headed by an empty string.</summary>
    public string DisplayName { get; }

    public VariantDefinition Definition { get; }

    public DateTimeOffset CreatedAt { get; }

    /// <summary>Unset while the variant is active — the same "default means it has not happened" shape
    /// <see cref="Runs.RunCell.ClaimedAt"/> uses, rather than a nullable that every caller must unwrap.</summary>
    public DateTimeOffset RetiredAt { get; }

    public bool IsActive => RetiredAt == default;

    public string Hash => Definition.Hash;

    /// <summary>What a report quotes: the name plus enough of the recipe hash to prove which recipe it was.</summary>
    public string Stamp => $"{Name.Value}#{Hash[..12]}";

    public static Outcome<RetrievalVariant> Create(
        string? name, string? displayName, VariantDefinition definition, DateTimeOffset now) =>
        VariantName.Parse(name).Match(
            parsed => Outcome<RetrievalVariant>.Success(new RetrievalVariant(
                Guid.CreateVersion7(),
                parsed,
                Display(displayName, parsed),
                definition,
                now,
                retiredAt: default)),
            Outcome<RetrievalVariant>.Failure);

    /// <summary>Rebuilds a stored row. Its name is re-parsed rather than trusted: a row edited by hand is
    /// a real event, and the catalog should refuse it by name instead of serving it.</summary>
    public static Outcome<RetrievalVariant> Rehydrate(
        Guid id,
        string? name,
        string? displayName,
        VariantDefinition definition,
        DateTimeOffset createdAt,
        DateTimeOffset retiredAt) =>
        VariantName.Parse(name).Match(
            parsed => Outcome<RetrievalVariant>.Success(new RetrievalVariant(
                id, parsed, Display(displayName, parsed), definition, createdAt, retiredAt)),
            Outcome<RetrievalVariant>.Failure);

    /// <summary>Takes the variant out of the active catalog, keeping its identity so historical cells
    /// still resolve. Returns a new value; the one already held elsewhere is untouched.</summary>
    public Outcome<RetrievalVariant> Retire(DateTimeOffset now) =>
        IsActive
            ? Outcome<RetrievalVariant>.Success(new RetrievalVariant(Id, Name, DisplayName, Definition, CreatedAt, now))
            : Outcome<RetrievalVariant>.Failure($"variant '{Name}' is already retired, since {RetiredAt:u}");

    /// <summary>What a planned leg carries: enough to identify the variant without a second lookup.</summary>
    public VariantSelection Select() => VariantSelection.Of(Id, Name.Value);

    private static string Display(string? displayName, VariantName name)
    {
        var trimmed = (displayName ?? string.Empty).Trim();
        return trimmed.Length > 0 ? trimmed : name.Value;
    }
}
