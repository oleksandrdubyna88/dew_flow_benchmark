namespace Bench.Domain.Gate;

/// <summary>The product's severity scale, as names. <see cref="Unknown"/> is a word this build has not met.</summary>
public enum FindingSeverity
{
    Blocking,
    Major,
    Minor,
    Nit,
    Unknown,
}

/// <summary>The product's finding categories, as names. A word this build has not met is <see cref="Unknown"/>
/// — counted, never dropped, and never quietly filed under a neighbour.</summary>
public enum FindingCategory
{
    Correctness,
    Reliability,
    Security,
    Performance,
    Maintainability,
    Testing,
    Documentation,
    Contract,
    Unknown,
}

public static class FindingWords
{
    public static FindingSeverity Severity(string? word) =>
        Enum.TryParse<FindingSeverity>((word ?? string.Empty).Trim(), ignoreCase: true, out var severity) && severity != FindingSeverity.Unknown
            ? severity
            : FindingSeverity.Unknown;

    public static FindingCategory Category(string? word) =>
        Enum.TryParse<FindingCategory>((word ?? string.Empty).Trim(), ignoreCase: true, out var category) && category != FindingCategory.Unknown
            ? category
            : FindingCategory.Unknown;
}

/// <summary>One finding a reviewer produced — HASHES ONLY.
/// <para>
/// The guard is structural: this record has no field for the finding's title, its reasoning, its fix, or the
/// path it names, so the entity that stores it cannot store them either. The text lives in the artefact store
/// (<c>findings.jsonl</c> per run) outside git and outside the published database; what is here is enough
/// for every count on the page and no code. A test walks the type (nested types and collections included) and
/// asserts that the only text it can carry is the two hashes.
/// </para></summary>
public sealed record GateFinding
{
    private GateFinding(int ordinal, FindingSeverity severity, FindingCategory category, bool isGating, int line, string textHash, string fileHash)
    {
        Ordinal = ordinal;
        Severity = severity;
        Category = category;
        IsGating = isGating;
        Line = line;
        TextHash = textHash;
        FileHash = fileHash;
    }

    public int Ordinal { get; }

    public FindingSeverity Severity { get; }

    public FindingCategory Category { get; }

    public bool IsGating { get; }

    /// <summary>The line the finding points at; zero when it named none.</summary>
    public int Line { get; }

    /// <summary>SHA-256 of the finding's text (title, why, fix) — enough to join it to its artefact and to
    /// tell two findings apart, and nothing a reader could recover code from.</summary>
    public string TextHash { get; }

    /// <summary>HMAC-SHA256, under the artefact root's <see cref="FileHashKey"/>, of the path the finding names in
    /// its one normal form (<see cref="FindingPath.Normalise"/>) — so "same file" is computable without the path
    /// being stored, and a published hash cannot be confirmed by hashing a guessed path.</summary>
    public string FileHash { get; }

    /// <summary>The one constructor: takes the TEXT and keeps the HASH. There is no way to build one from a
    /// hash somebody else computed, so a stored row is always re-derivable from its artefact and the key.</summary>
    public static Outcome<GateFinding> Of(
        int ordinal, FindingSeverity severity, FindingCategory category, bool isGating, int line, string? text, string? file, FileHashKey fileKey)
    {
        var refusal = (ordinal, line, (text ?? string.Empty).Trim().Length) switch
        {
            ( < 0, _, _) => $"a finding's ordinal is its position in the reply, got {ordinal}",
            (_, < 0, _) => $"a line is zero (none named) or positive, got {line}",
            (_, _, 0) => $"finding {ordinal} has no text — a finding that says nothing cannot be assessed",
            _ => string.Empty,
        };

        return refusal.Length > 0
            ? Outcome<GateFinding>.Failure(refusal)
            : Outcome<GateFinding>.Success(new GateFinding(
                ordinal, severity, category, isGating, line, StableHash.Of(text!), fileKey.Hash(FindingPath.Normalise(file))));
    }
}
