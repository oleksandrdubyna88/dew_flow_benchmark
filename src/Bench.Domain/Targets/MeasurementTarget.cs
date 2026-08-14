namespace Bench.Domain.Targets;

/// <summary>A git remote the benchmark may clone. Parsed rather than trusted: the value reaches a
/// process launcher, and an argument that can be read as an option is a different kind of bug.</summary>
public sealed record RepoUrl
{
    private RepoUrl(string value) => Value = value;

    public string Value { get; }

    public static Outcome<RepoUrl> Parse(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            return Outcome<RepoUrl>.Failure("a repository url is required");
        }

        return HasKnownScheme(trimmed)
            ? Outcome<RepoUrl>.Success(new RepoUrl(trimmed))
            : Outcome<RepoUrl>.Failure($"unsupported repository url '{trimmed}' — expected https://, ssh://, git@ or file://");
    }

    private static bool HasKnownScheme(string value) =>
        value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("git@", StringComparison.Ordinal);

    public override string ToString() => Value;
}

/// <summary>A full 40-character git object name. Abbreviated shas are refused: the whole point of the
/// target is that a result names exactly one tree, and an abbreviation is ambiguous by construction.</summary>
public sealed record CommitSha
{
    public const int Length = 40;

    private CommitSha(string value) => Value = value;

    public string Value { get; }

    public static Outcome<CommitSha> Parse(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();

        if (trimmed.Length != Length)
        {
            return Outcome<CommitSha>.Failure(
                $"a commit sha is {Length} hexadecimal characters, got {trimmed.Length} — abbreviations are ambiguous and are refused");
        }

        return trimmed.All(Uri.IsHexDigit)
            ? Outcome<CommitSha>.Success(new CommitSha(trimmed.ToLowerInvariant()))
            : Outcome<CommitSha>.Failure("a commit sha is hexadecimal");
    }

    public string Short => Value[..12];

    public override string ToString() => Value;
}

/// <summary>What is being measured: one repository, one commit, and the paths that must not be read.
/// <para>
/// <see cref="Exclusions"/> is not an optimisation. A benchmark whose target repository contains prior
/// results is measuring an engine that can read its own answers — the failure that produced defect 9
/// of the v6 series upstream. There the answer keys lived in the measured tree by accident; here they
/// live in this repository by design, but the TARGET may still carry published findings of its own.
/// </para></summary>
public sealed record MeasurementTarget(RepoUrl Repo, CommitSha Commit, IReadOnlyList<string> Exclusions)
{
    public static MeasurementTarget At(RepoUrl repo, CommitSha commit) => new(repo, commit, []);

    public MeasurementTarget Excluding(params string[] globs) =>
        this with { Exclusions = [.. Exclusions, .. globs.Where(g => !string.IsNullOrWhiteSpace(g))] };

    /// <summary>The canonical form that identifies this target inside a measurement tuple. Exclusions
    /// are part of it: the same commit read with different blind spots is not the same measurement.</summary>
    public string Canonical =>
        $"{Repo.Value}@{Commit.Value}[{string.Join('|', Exclusions.Order(StringComparer.Ordinal))}]";
}
