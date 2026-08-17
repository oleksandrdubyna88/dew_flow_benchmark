using Bench.Domain;

namespace Bench.Infrastructure.Engines;

/// <summary>Where the retrieval engine is, and which index it should answer from.
/// <para>
/// A value rather than three loose strings in a container: a run either names an engine completely or names
/// none, and <see cref="None"/> is the second state said once instead of checked three times.
/// </para>
/// <para>
/// The url and the project id are configuration and stay OUT of the results database — a benchmark's store
/// is published unedited, and a machine-specific address in it is what a redaction pass would then have to
/// find by hand. What a run records is the engine's canonical <c>EngineRef</c>, which the endpoint's own
/// address forms part of; that is a deliberate, named exception, and it is why this type carries no
/// credential at all.
/// </para></summary>
public sealed record QlnEngineOptions(string BaseUrl, Guid ProjectId, string Branch)
{
    public const string DefaultBranch = "main";

    /// <summary>A run with no retrieval engine. Every cell is then the control arm, which is what every run
    /// planned before this lane existed already was.</summary>
    public static QlnEngineOptions None { get; } = new(string.Empty, Guid.Empty, string.Empty);

    public bool IsConfigured => BaseUrl.Length > 0 && ProjectId != Guid.Empty;

    /// <summary>Reads the pair, refusing a half-named engine.
    /// <para>
    /// Half-named is the case worth refusing loudly: a url with no project would be a run that looks
    /// configured for retrieval and measures the baseline, which is the worst of the three outcomes because
    /// nothing in the report would say so.
    /// </para></summary>
    public static Outcome<QlnEngineOptions> Parse(string? baseUrl, string? projectId, string? branch)
    {
        var url = (baseUrl ?? string.Empty).Trim();
        var project = (projectId ?? string.Empty).Trim();

        if (url.Length == 0 && project.Length == 0)
        {
            return Outcome<QlnEngineOptions>.Success(None);
        }

        if (url.Length == 0 || project.Length == 0)
        {
            return Outcome<QlnEngineOptions>.Failure(
                "--engine-url and --engine-project go together: an engine named halfway would produce a run that "
                + "looks configured for retrieval and measures the baseline, with nothing in the report saying so");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || parsed.Scheme is not ("http" or "https"))
        {
            return Outcome<QlnEngineOptions>.Failure($"--engine-url '{url}' is not an absolute http(s) url");
        }

        return Guid.TryParse(project, out var guid)
            ? Outcome<QlnEngineOptions>.Success(
                new QlnEngineOptions(url.TrimEnd('/'), guid, Named(branch)))
            : Outcome<QlnEngineOptions>.Failure($"--engine-project '{project}' is not a guid");
    }

    private static string Named(string? branch)
    {
        var trimmed = (branch ?? string.Empty).Trim();
        return trimmed.Length > 0 ? trimmed : DefaultBranch;
    }
}
