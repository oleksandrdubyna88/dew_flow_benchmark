using System.Text.RegularExpressions;

namespace Bench.Domain.Gate;

/// <summary>Which KIND of question a rubric asks. Two kinds never sum: <c>strict-v1</c> asks whether trigger,
/// mechanism and consequence are all correct at the code; <c>lenient-worth-v1</c> asks a one-turn judge
/// whether a finding was worth having. A mean across them is a number about nothing, and every report call
/// takes the kind as a required dimension for exactly that reason.</summary>
public enum RubricKind
{
    Strict,
    LenientWorth,
}

public sealed record RubricId
{
    private RubricId(string value) => Value = value;

    public string Value { get; }

    public static Outcome<RubricId> Parse(string? value)
    {
        var trimmed = Slug.Clean(value);

        return Slug.IsValid(trimmed)
            ? Outcome<RubricId>.Success(new RubricId(trimmed))
            : Outcome<RubricId>.Failure($"'{trimmed}' is not a usable rubric id — {Slug.Rule}");
    }

    public override string ToString() => Value;
}

/// <summary>One rubric: its id, its kind, and the hash of the prompt text that IS the rubric — so a verdict
/// names exactly the wording it was judged under, and an edited prompt is a new rubric rather than a silent
/// re-reading of old verdicts.</summary>
public sealed partial record Rubric
{
    private Rubric(RubricId id, RubricKind kind, string hash)
    {
        Id = id;
        Kind = kind;
        Hash = hash;
    }

    public RubricId Id { get; }

    public RubricKind Kind { get; }

    public string Hash { get; }

    public string Stamp => $"{Id.Value}#{HashText.Short(Hash)}";

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Sha256Hex { get; }

    public static Outcome<Rubric> Of(string? id, RubricKind kind, string? hash)
    {
        var digest = (hash ?? string.Empty).Trim().ToLowerInvariant();

        return RubricId.Parse(id).Match(
            parsed => Sha256Hex.IsMatch(digest)
                ? Outcome<Rubric>.Success(new Rubric(parsed, kind, digest))
                : Outcome<Rubric>.Failure($"'{digest}' is not a rubric hash — the SHA-256 of the prompt file, 64 hex characters"),
            Outcome<Rubric>.Failure);
    }
}

/// <summary>The rubrics this build holds. A verdict under a hash the catalog does not hold is refused: it
/// would be a verdict under a wording nobody can read back.</summary>
public sealed record RubricCatalog(IReadOnlyList<Rubric> Rubrics)
{
    public Outcome<Rubric> Resolve(string? hash)
    {
        var digest = (hash ?? string.Empty).Trim().ToLowerInvariant();

        var held = Rubrics.FirstOrDefault(r => string.Equals(r.Hash, digest, StringComparison.Ordinal));

        return held is null
            ? Outcome<Rubric>.Failure(
                $"no rubric in the catalog hashes to {HashText.Short(digest)} — it holds {string.Join(", ", Rubrics.Select(r => r.Stamp))}; "
                + "a verdict under a wording nobody can read back is a verdict about nothing")
            : Outcome<Rubric>.Success(held);
    }
}
