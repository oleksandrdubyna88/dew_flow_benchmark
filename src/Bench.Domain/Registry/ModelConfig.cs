using System.Text.RegularExpressions;
using Bench.Domain.Runs;

namespace Bench.Domain.Registry;

/// <summary>How a registry row is actually reached at run time.</summary>
public enum ModelRuntimeKind
{
    /// <summary>An OpenAI-compatible completions endpoint — the only one with a runtime today.</summary>
    OpenAiEndpoint,

    CliClaude,

    CliCodex,

    CliGemini,

    /// <summary>A local model driven in-process through the bridge, in agent mode.</summary>
    BridgeLocal,
}

/// <summary>A registry row's configuration: <b>references, never values</b>.
/// <para>
/// This is the publication guarantee, enforced by a type rather than by a redaction pass nobody has
/// scheduled. The obvious shape — endpoint, CLI path, api key — puts this machine's absolute paths and,
/// sooner or later, somebody's API key into the one database this project promises to publish unedited.
/// So what is stored is the NAME of an environment variable, and the value is resolved at use.
/// </para>
/// <para>
/// Sampling and prices stay as VALUES, deliberately: they are neither secret nor machine-specific, and a
/// run must be able to say what sampling it asked for and what its tokens cost. What leaves is anything
/// whose value is a property of <i>this machine</i> or <i>this account</i>.
/// </para></summary>
/// <param name="ModelId">The id the runtime is asked for — <c>qwen3-coder:latest</c>. An id is data: it is
/// the same on every machine, and a result that could not name it would be unreadable.</param>
/// <param name="BaseUrlRef">The NAME of the variable holding the endpoint. Empty for a runtime that has
/// no endpoint.</param>
/// <param name="ApiKeyRef">The NAME of the variable holding the key. Empty when the endpoint needs none.</param>
/// <param name="ExecutableRef">The NAME of the variable holding a CLI's path. Empty for an endpoint.</param>
public sealed partial record ModelConfig(
    string ModelId,
    string BaseUrlRef,
    string ApiKeyRef,
    string ExecutableRef,
    Sampling Sampling,
    decimal InputCostPerMTok,
    decimal OutputCostPerMTok)
{
    /// <summary>A reference: an environment variable's name, or a configuration section path. Deliberately
    /// narrow — no slashes, no colons-with-slashes, no spaces — so that the shapes people actually paste
    /// (a url, an absolute path, a key) cannot pass as one.</summary>
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_:.]{0,127}$")]
    private static partial Regex Reference { get; }

    public static Outcome<ModelConfig> Parse(
        string? modelId,
        string? baseUrlRef,
        string? apiKeyRef,
        string? executableRef,
        Sampling sampling,
        decimal inputPerMTok = 0,
        decimal outputPerMTok = 0)
    {
        var id = (modelId ?? string.Empty).Trim();

        if (id.Length == 0)
        {
            return Outcome<ModelConfig>.Failure(
                "a registry row must name its model id — an unset id is a refusal, never a fallback to a default");
        }

        if (LooksLikeAValue(id))
        {
            return Outcome<ModelConfig>.Failure(
                $"'{id}' is a url or a path, not a model id — the endpoint belongs in a REFERENCE, so the database can be published unedited");
        }

        return Refs(baseUrlRef, apiKeyRef, executableRef).Match(
            refs => Priced(id, refs, sampling, inputPerMTok, outputPerMTok),
            Outcome<ModelConfig>.Failure);
    }

    /// <summary>Every stored reference of this row, for the publication check that re-reads the database.</summary>
    public IReadOnlyList<string> References =>
        [.. new[] { BaseUrlRef, ApiKeyRef, ExecutableRef }.Where(r => r.Length > 0)];

    public string Describe =>
        $"{ModelId} · " + (References.Count > 0 ? string.Join(", ", References) : "no references");

    private static Outcome<(string BaseUrl, string ApiKey, string Executable)> Refs(
        string? baseUrlRef, string? apiKeyRef, string? executableRef)
    {
        var candidates = new[]
        {
            ("--base-url-ref", (baseUrlRef ?? string.Empty).Trim()),
            ("--api-key-ref", (apiKeyRef ?? string.Empty).Trim()),
            ("--executable-ref", (executableRef ?? string.Empty).Trim()),
        };

        foreach (var (flag, value) in candidates.Where(c => c.Item2.Length > 0))
        {
            if (!Reference.IsMatch(value))
            {
                return Outcome<(string, string, string)>.Failure(Refusal(flag, value));
            }
        }

        return Outcome<(string, string, string)>.Success(
            (candidates[0].Item2, candidates[1].Item2, candidates[2].Item2));
    }

    /// <summary>Names the two shapes people actually paste, because "invalid reference" teaches nobody
    /// what the rule is for.</summary>
    private static string Refusal(string flag, string value) =>
        LooksLikeAValue(value)
            ? $"{flag} was given a VALUE ('{Short(value)}') — store the NAME of the environment variable that holds it. "
              + "This database is published unedited, and an endpoint or a path in it is a machine's identity leaving with the results"
            : $"'{Short(value)}' is not a usable reference for {flag} — an environment variable name or a configuration "
              + "section path (letters, digits, '_', ':' and '.')";

    private static bool LooksLikeAValue(string value) =>
        value.Contains("://", StringComparison.Ordinal)
        || value.StartsWith('/')
        || value.StartsWith("\\\\", StringComparison.Ordinal)
        || (value.Length > 2 && value[1] == ':' && (value[2] == '\\' || value[2] == '/'));

    private static Outcome<ModelConfig> Priced(
        string id,
        (string BaseUrl, string ApiKey, string Executable) refs,
        Sampling sampling,
        decimal inputPerMTok,
        decimal outputPerMTok) =>
        inputPerMTok < 0 || outputPerMTok < 0
            ? Outcome<ModelConfig>.Failure("a negative price is not a price")
            : Outcome<ModelConfig>.Success(new ModelConfig(
                id, refs.BaseUrl, refs.ApiKey, refs.Executable, sampling, inputPerMTok, outputPerMTok));

    private static string Short(string value) => value.Length <= 40 ? value : value[..40] + "…";
}
