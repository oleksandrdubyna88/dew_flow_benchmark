namespace Bench.Contracts;

/// <summary>The same inputs the CLI takes, as a request body. One shape for both surfaces: the API is
/// not a second implementation, it is a second door onto the same use case.</summary>
public sealed record PlanRequestDto
{
    public string Repo { get; init; } = string.Empty;

    public string Commit { get; init; } = string.Empty;

    public string SuiteJson { get; init; } = string.Empty;

    public int Repeats { get; init; } = 1;

    public int Seed { get; init; } = 1;

    /// <summary>Entries of the form <c>id@local</c> / <c>id@cloud</c>. An entry with no id is refused.</summary>
    public IReadOnlyList<string> Subjects { get; init; } = [];

    public IReadOnlyList<string> Lanes { get; init; } = [];

    public IReadOnlyList<string> Exclusions { get; init; } = [];

    public string Engine { get; init; } = "NoRetrieval";

    public string EngineEndpoint { get; init; } = string.Empty;

    public string EngineVersion { get; init; } = string.Empty;

    public string IndexFingerprint { get; init; } = string.Empty;
}
