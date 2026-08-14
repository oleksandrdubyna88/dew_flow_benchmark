namespace Bench.Domain.Suites;

/// <summary>A frozen, hashed set of questions. Freezing is the whole mechanism: an editable suite is
/// not a version, and results that name a mutable version cannot be compared to each other later.
/// <para>
/// The failure this prevents was observed upstream and is worth restating, because it is quiet: a
/// measured question set existed only in the database while the file on disk that claimed to be it had
/// drifted several versions behind. Nothing failed; the numbers simply stopped meaning what their
/// label said.
/// </para></summary>
public sealed record Suite
{
    private Suite(string id, int version, IReadOnlyList<Question> questions, bool frozen, string hash)
    {
        Id = id;
        Version = version;
        Questions = questions;
        IsFrozen = frozen;
        Hash = hash;
    }

    public string Id { get; }

    public int Version { get; }

    public IReadOnlyList<Question> Questions { get; }

    public bool IsFrozen { get; }

    /// <summary>Empty until frozen — an unfrozen suite deliberately has no identity to quote.</summary>
    public string Hash { get; }

    public static Suite Draft(string id) => new(id, 0, [], frozen: false, hash: string.Empty);

    /// <summary>Adds a question to a DRAFT. Refused on a frozen suite: the way to change a frozen
    /// suite is <see cref="Amend"/>, which produces the next version rather than mutating this one.</summary>
    public Outcome<Suite> With(Question question) =>
        IsFrozen
            ? Outcome<Suite>.Failure($"suite {Id} v{Version} is frozen — call Amend() to start version {Version + 1}")
            : Outcome<Suite>.Success(this with { Questions = [.. Questions, question] });

    /// <summary>Freezes the draft as version <c>Version + 1</c> and stamps its content hash.</summary>
    public Outcome<Suite> Freeze()
    {
        if (IsFrozen)
        {
            return Outcome<Suite>.Failure($"suite {Id} v{Version} is already frozen");
        }

        if (Questions.Count == 0)
        {
            return Outcome<Suite>.Failure($"suite {Id} has no questions — an empty version is not worth a number");
        }

        var next = Version + 1;
        return Outcome<Suite>.Success(new Suite(Id, next, Questions, frozen: true, hash: HashOf(Id, next, Questions)));
    }

    /// <summary>Reopens a frozen suite as the next draft. The frozen version keeps existing — every
    /// result that names it still resolves.</summary>
    public Outcome<Suite> Amend() =>
        IsFrozen
            ? Outcome<Suite>.Success(new Suite(Id, Version, Questions, frozen: false, hash: string.Empty))
            : Outcome<Suite>.Failure($"suite {Id} v{Version} is already a draft");

    /// <summary>The version stamp a result quotes. Includes the hash, so a result can be checked
    /// against the suite it claims rather than trusted.</summary>
    public string Stamp => IsFrozen ? $"{Id}@v{Version}#{Hash[..12]}" : $"{Id}@draft";

    private static string HashOf(string id, int version, IReadOnlyList<Question> questions)
    {
        var canonical = string.Join(
            "\n",
            [$"suite:{id}", $"version:{version}", .. questions.OrderBy(q => q.Id, StringComparer.Ordinal).Select(q => q.Canonical)]);

        return StableHash.Of(canonical);
    }
}
