using System.Text.Json;
using Bench.Application.Probes;
using Bench.Domain;
using Bench.Domain.Probes;

namespace Bench.Infrastructure.Probes;

/// <summary>The web oracle's adapter (D7): ONE HTTPS read of the npm registry's <c>latest</c> dist-tag for <c>@openai/codex</c>,
/// its <c>version</c> field parsed as a semver. Every way the read can fail — no network, the registry down, a rate limit, a body
/// that is not the shape — is a refusal NAMING the cause; nothing here retries, guesses, or answers an empty version.</summary>
public sealed class NpmRegistryOracle(HttpClient http) : IProbeOracle
{
    public const string LatestUrl = "https://registry.npmjs.org/@openai/codex/latest";

    /// <summary>The ceiling the CLI gives the one read — a registry that has not answered in this long is not answering.</summary>
    public static TimeSpan Timeout => TimeSpan.FromSeconds(30);

    public async Task<Outcome<ProbeOracle>> LatestAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(LatestUrl, cancellationToken);

            return response.IsSuccessStatusCode
                ? Version(await response.Content.ReadAsStringAsync(cancellationToken))
                : Outcome<ProbeOracle>.Failure($"the npm registry answered HTTP {(int)response.StatusCode} for {LatestUrl}");
        }
        catch (HttpRequestException ex)
        {
            return Outcome<ProbeOracle>.Failure($"the npm registry is unreachable ({LatestUrl}) — {ex.Message.Split('\n')[0]}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Outcome<ProbeOracle>.Failure($"the npm registry did not answer {LatestUrl} in time");
        }
    }

    /// <summary>The registry's answer read as the oracle: a JSON object whose <c>version</c> is a semver, or a refusal saying which
    /// part was not there.</summary>
    public static Outcome<ProbeOracle> Version(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var version = VersionText(document.RootElement);

            return version.Length > 0
                ? ProbeOracle.Parse(version, OracleSource.Registry).Match(
                    Outcome<ProbeOracle>.Success,
                    reason => Outcome<ProbeOracle>.Failure($"the npm registry's version for @openai/codex was refused — {reason}"))
                : Outcome<ProbeOracle>.Failure("the npm registry's answer for @openai/codex carries no 'version' string");
        }
        catch (JsonException)
        {
            return Outcome<ProbeOracle>.Failure("the npm registry's answer for @openai/codex is not JSON");
        }
    }

    private static string VersionText(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.String
            ? version.GetString() ?? string.Empty
            : string.Empty;
}
