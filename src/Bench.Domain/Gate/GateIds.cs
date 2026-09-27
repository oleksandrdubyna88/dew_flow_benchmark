using System.Text.RegularExpressions;

namespace Bench.Domain.Gate;

/// <summary>A seeded task's id — <c>cs2</c>, <c>php1</c>: the one word a per-task column is headed with.
/// <see cref="Slug"/>-shaped, like every other identity that ends up beside another in a report.</summary>
public sealed record GateTaskId
{
    private GateTaskId(string value) => Value = value;

    public string Value { get; }

    public static Outcome<GateTaskId> Parse(string? value)
    {
        var trimmed = Slug.Clean(value);

        return Slug.IsValid(trimmed)
            ? Outcome<GateTaskId>.Success(new GateTaskId(trimmed))
            : Outcome<GateTaskId>.Failure($"'{trimmed}' is not a usable task id — {Slug.Rule}");
    }

    public override string ToString() => Value;
}

/// <summary>A reviewer catalog row's id — <c>grok-4-7-xai-medium</c>. The row, not the model: the same
/// model at two transports is two reviewers, and the id is only what a row is called.</summary>
public sealed record GateReviewerId
{
    private GateReviewerId(string value) => Value = value;

    public string Value { get; }

    public static Outcome<GateReviewerId> Parse(string? value)
    {
        var trimmed = Slug.Clean(value);

        return Slug.IsValid(trimmed)
            ? Outcome<GateReviewerId>.Success(new GateReviewerId(trimmed))
            : Outcome<GateReviewerId>.Failure($"'{trimmed}' is not a usable reviewer id — {Slug.Rule}");
    }

    public override string ToString() => Value;
}

/// <summary>A planted defect's id — <c>cs2-S1</c>. The trial wrote these with a capital, so this is
/// deliberately NOT a <see cref="Slug"/>: refusing the ids the seeds already carry would make the first suite
/// unloadable. Case is preserved and compared ordinally, because the id is quoted back from the assessor's
/// <c>seedHit</c> and has to round-trip byte for byte.</summary>
public sealed partial record SeedId
{
    private SeedId(string value) => Value = value;

    public string Value { get; }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")]
    private static partial Regex Pattern { get; }

    public static Outcome<SeedId> Parse(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();

        return Pattern.IsMatch(trimmed)
            ? Outcome<SeedId>.Success(new SeedId(trimmed))
            : Outcome<SeedId>.Failure(
                $"'{trimmed}' is not a usable seed id — 1 to 64 characters of letters, digits, '.', '_' and '-', starting with a letter or digit");
    }

    public override string ToString() => Value;
}
