namespace Bench.Domain.Variants;

/// <summary>Which catalog variant a leg runs under.
/// <para>
/// Two states, closed. <see cref="NotApplicable"/> is the honest state of a leg planned before the
/// catalog existed — deliberately not "the default variant", which does not exist, and deliberately not
/// an empty id, which would read as a variant nobody can look up.
/// </para></summary>
public abstract record VariantSelection
{
    private VariantSelection() { }

    public sealed record Selected : VariantSelection
    {
        internal Selected(Guid id, string name)
        {
            Id = id;
            Name = name;
        }

        public Guid Id { get; }

        public string Name { get; }

        public override string Canonical => Name;
    }

    public sealed record NotApplicable : VariantSelection
    {
        internal NotApplicable() { }

        public override string Canonical => "-";
    }

    public static VariantSelection None { get; } = new NotApplicable();

    public static VariantSelection Of(Guid id, string name) => new Selected(id, name);

    public abstract string Canonical { get; }
}

/// <summary>Flattening a selection for storage and back.
/// <para>
/// Unlike <see cref="Runs.LegOutcomeCodec"/> this one decodes as well as encodes, and can: both halves
/// are structured values, not a sentence written for a human. The absent id is the whole point — a row
/// with no variant is a row planned without one, never a row whose variant was lost.
/// </para></summary>
public static class VariantSelectionCodec
{
    public static (Guid? Id, string Name) Encode(VariantSelection selection) =>
        selection switch
        {
            VariantSelection.Selected s => (s.Id, s.Name),
            _ => (null, string.Empty),
        };

    public static VariantSelection Decode(Guid? id, string? name) =>
        id is { } value ? VariantSelection.Of(value, name ?? string.Empty) : VariantSelection.None;
}
