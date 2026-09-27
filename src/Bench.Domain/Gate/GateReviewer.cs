namespace Bench.Domain.Gate;

/// <summary>One row of the reviewer catalog: a named, hashed reviewer — a model PLUS its calibrated transport.
/// <para>
/// Mirrors <see cref="Variants.RetrievalVariant"/> row for row, and for the same reason: runs name the reviewer
/// they measured, so a definition edited in place would relabel every number already measured against it. A
/// row is added and retired, never edited, and both states resolve forever. Two rows with one hash are the
/// same configuration under two names, which the catalog can detect rather than a report has to explain.
/// </para></summary>
public sealed record GateReviewer
{
    private GateReviewer(GateReviewerId id, ReviewerDefinition definition, DateTimeOffset addedAt, DateTimeOffset retiredAt)
    {
        Id = id;
        Definition = definition;
        AddedAt = addedAt;
        RetiredAt = retiredAt;
    }

    public GateReviewerId Id { get; }

    public ReviewerDefinition Definition { get; }

    public DateTimeOffset AddedAt { get; }

    /// <summary>Unset while the reviewer is active — the "default means it has not happened" shape the
    /// variant catalog uses, rather than a nullable every caller must unwrap.</summary>
    public DateTimeOffset RetiredAt { get; }

    public bool IsActive => RetiredAt == default;

    public string Hash => Definition.Hash;

    /// <summary>What a report quotes: the id plus enough of the hash to prove which definition it was.</summary>
    public string Stamp => $"{Id.Value}#{HashText.Short(Hash)}";

    public static GateReviewer Create(GateReviewerId id, ReviewerDefinition definition, DateTimeOffset now) =>
        new(id, definition, now, retiredAt: default);

    /// <summary>Rebuilds a stored row. Its id is re-parsed rather than trusted: a row edited by hand is a real
    /// event, and the catalog should refuse it by name instead of serving it.</summary>
    public static Outcome<GateReviewer> Rehydrate(
        string? id, ReviewerDefinition definition, DateTimeOffset addedAt, DateTimeOffset retiredAt) =>
        GateReviewerId.Parse(id).Match(
            parsed => Outcome<GateReviewer>.Success(new GateReviewer(parsed, definition, addedAt, retiredAt)),
            Outcome<GateReviewer>.Failure);

    /// <summary>Takes the reviewer out of the active catalog, keeping its identity so historical runs still
    /// resolve. Returns a new value; the one already held elsewhere is untouched.</summary>
    public Outcome<GateReviewer> Retire(DateTimeOffset now) =>
        IsActive
            ? Outcome<GateReviewer>.Success(new GateReviewer(Id, Definition, AddedAt, now))
            : Outcome<GateReviewer>.Failure($"reviewer '{Id}' is already retired, since {RetiredAt:u}");
}

/// <summary>Two or more rows that hash to one definition — one configuration under several names. Reported,
/// never refused: a duplicate is legitimate while a name is being migrated, but every number measured under
/// either name is a number about one thing, and a report must say so.</summary>
public sealed record SharedConfiguration(string Hash, IReadOnlyList<GateReviewerId> Ids)
{
    public string Describe =>
        $"{string.Join(", ", Ids.Select(i => i.Value))} are one configuration ({HashText.Short(Hash)}) under {Ids.Count} names";
}

public static class GateReviewerCatalog
{
    /// <summary>The hash-collisions of a catalog: every group of rows sharing one definition hash.</summary>
    public static IReadOnlyList<SharedConfiguration> SameConfiguration(IReadOnlyList<GateReviewer> rows) =>
        [.. rows.GroupBy(r => r.Hash, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => new SharedConfiguration(g.Key, [.. g.Select(r => r.Id).OrderBy(i => i.Value, StringComparer.Ordinal)]))
            .OrderBy(s => s.Hash, StringComparer.Ordinal)];
}
