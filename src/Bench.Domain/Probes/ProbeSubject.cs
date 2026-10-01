using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bench.Domain.Gate;
using Bench.Domain.Registry;

namespace Bench.Domain.Probes;

/// <summary>The runtime a probe subject answers through — a WORD per product, as <c>ReviewerRuntime</c> is for the gate.
/// <c>Api</c> is the one subject that is not a CLI: it is measured through the product's own transport (D6).</summary>
public enum ProbeRuntime
{
    Claude,
    Codex,
    Antigravity,
    Api,
}

/// <summary>The one reading of a runtime word — <c>claude</c>, <c>codex</c>, <c>antigravity</c>, <c>api</c>.</summary>
public static class ProbeRuntimeWord
{
    private static readonly IReadOnlyDictionary<ProbeRuntime, string> Words = new Dictionary<ProbeRuntime, string>
    {
        [ProbeRuntime.Claude] = "claude",
        [ProbeRuntime.Codex] = "codex",
        [ProbeRuntime.Antigravity] = "antigravity",
        [ProbeRuntime.Api] = "api",
    };

    public static string Of(ProbeRuntime runtime) => Words[runtime];

    public static Outcome<ProbeRuntime> Parse(string? word)
    {
        var trimmed = (word ?? string.Empty).Trim();
        var match = Words.Where(w => string.Equals(w.Value, trimmed, StringComparison.OrdinalIgnoreCase)).Select(w => (ProbeRuntime?)w.Key).FirstOrDefault();

        return match is { } runtime
            ? Outcome<ProbeRuntime>.Success(runtime)
            : Outcome<ProbeRuntime>.Failure($"'{trimmed}' is not a probe runtime — one of {string.Join(", ", Words.Values)}");
    }
}

/// <summary>A subject's id — <c>claude-sonnet</c>, <c>codex-astra</c>: <see cref="Slug"/>-shaped, the word a column is headed with.</summary>
public sealed record ProbeSubjectId
{
    private ProbeSubjectId(string value) => Value = value;

    public string Value { get; }

    public static Outcome<ProbeSubjectId> Parse(string? value)
    {
        var trimmed = Slug.Clean(value);

        return Slug.IsValid(trimmed)
            ? Outcome<ProbeSubjectId>.Success(new ProbeSubjectId(trimmed))
            : Outcome<ProbeSubjectId>.Failure($"'{trimmed}' is not a usable subject id — {Slug.Rule}");
    }

    public override string ToString() => Value;
}

/// <summary>One subject of a probe run: a runtime word, a model id, and the executable as the NAME of an environment
/// variable — never a path, never anything key-shaped (D4). Frozen on the run, so <c>resume</c> and <c>rerun</c> never
/// depend on a file that may have changed, and the database stays publishable (<c>ModelConfig</c>'s publication rule).</summary>
public sealed partial record ProbeSubject
{
    private ProbeSubject(ProbeSubjectId id, ProbeRuntime runtime, string modelId, string executableRef, ApiTransport api)
    {
        Id = id;
        Runtime = runtime;
        ModelId = modelId;
        ExecutableRef = executableRef;
        Vendor = api.Vendor;
        Endpoint = api.Endpoint;
        Dialect = api.Dialect;
    }

    public ProbeSubjectId Id { get; }

    public ProbeRuntime Runtime { get; }

    public string ModelId { get; }

    /// <summary>The NAME of the environment variable holding the executable's path — <c>BENCH_CLAUDE</c>.</summary>
    public string ExecutableRef { get; }

    /// <summary>The api subject's coai vendor id (<c>coai-mcp --probe-api --vendor</c>), which is also the vault key's name when
    /// no settings row names one (<c>grok</c>); empty for a CLI subject. Frozen on the run (S2, the S1 open question).</summary>
    public string Vendor { get; }

    /// <summary>The api subject's PUBLIC vendor base url (<c>--endpoint</c>, <c>https://api.x.ai/v1</c>) — a value, as the gate's
    /// reviewer endpoint is: a public vendor url names no machine (the publication guard's endpoint rule); empty for a CLI.</summary>
    public string Endpoint { get; }

    /// <summary>The api subject's wire dialect (<c>--dialect</c>, <c>xai</c>); empty for a CLI.</summary>
    public string Dialect { get; }

    /// <summary>What the product's api path needs beside the model: the vendor, the endpoint, the dialect — empty for a CLI subject.</summary>
    private readonly record struct ApiTransport(string Vendor, string Endpoint, string Dialect)
    {
        public static ApiTransport None => new(string.Empty, string.Empty, string.Empty);

        public bool IsEmpty => Vendor.Length == 0 && Endpoint.Length == 0 && Dialect.Length == 0;
    }

    /// <summary>An environment variable NAME as every shell spells one: upper-case letters, digits and underscores. Narrower
    /// than <c>ModelConfig</c>'s reference on purpose — a probe subject names a variable, never a configuration section — and
    /// narrow enough that the two shapes people paste by mistake (a path, a key) cannot pass as one.</summary>
    [GeneratedRegex("^[A-Z][A-Z0-9_]{0,127}$")]
    private static partial Regex VariableName { get; }

    /// <summary>The shapes an API key takes: a vendor prefix (<c>sk-</c>, <c>xai-</c>, <c>AIza</c>, <c>ghp_</c>, <c>gsk_</c>,
    /// <c>AKIA</c>) or a long mixed-case alphanumeric run with no underscore in it.</summary>
    [GeneratedRegex(@"^(?:sk-|xai-|AIza|ghp_|gsk_|AKIA|pk-)|^(?=.*[a-z])(?=.*[A-Z0-9])[A-Za-z0-9+/=-]{24,}$")]
    private static partial Regex KeyShaped { get; }

    /// <summary>A CLI subject — no vendor, endpoint or dialect.</summary>
    public static Outcome<ProbeSubject> Parse(string? id, string? runtimeWord, string? modelId, string? executableRef) =>
        Parse(id, runtimeWord, modelId, executableRef, string.Empty, string.Empty, string.Empty);

    /// <summary>Any subject. An <c>api</c> subject needs all three of vendor, endpoint and dialect (D6 spells them on the launch,
    /// and <c>resume</c> must not depend on a file); a CLI subject is refused when given any of them — a transport nothing reads
    /// is a typo the run would otherwise freeze.</summary>
    public static Outcome<ProbeSubject> Parse(string? id, string? runtimeWord, string? modelId, string? executableRef, string? vendor, string? endpoint, string? dialect) =>
        ProbeSubjectId.Parse(id).Match(
            subjectId => ProbeRuntimeWord.Parse(runtimeWord).Match(
                runtime => Checked(subjectId, runtime, (modelId ?? string.Empty).Trim(), (executableRef ?? string.Empty).Trim(),
                    new ApiTransport((vendor ?? string.Empty).Trim(), (endpoint ?? string.Empty).Trim(), (dialect ?? string.Empty).Trim())),
                Outcome<ProbeSubject>.Failure),
            Outcome<ProbeSubject>.Failure);

    private static Outcome<ProbeSubject> Checked(ProbeSubjectId id, ProbeRuntime runtime, string model, string executableRef, ApiTransport api)
    {
        var refusal = Refusal(id, model, executableRef);
        var transport = refusal.Length > 0 ? refusal : TransportRefusal(id, runtime, api);

        return transport.Length > 0
            ? Outcome<ProbeSubject>.Failure(transport)
            : Outcome<ProbeSubject>.Success(new ProbeSubject(id, runtime, model, executableRef, api));
    }

    private static string Refusal(ProbeSubjectId id, string model, string executableRef) =>
        (IsModelId(model), ExecutableRefusal(executableRef)) switch
        {
            (false, _) => $"'{Short(model)}' is not a model id for subject '{id}' — a model id is the string the runtime is asked for, never a url or a path, and never empty",
            (_, { Length: > 0 } why) => $"subject '{id}': {why}",
            _ => string.Empty,
        };

    private static string TransportRefusal(ProbeSubjectId id, ProbeRuntime runtime, ApiTransport api) =>
        (runtime == ProbeRuntime.Api, api.IsEmpty) switch
        {
            (false, false) => $"subject '{id}' runs on a CLI and names a vendor, endpoint or dialect — those belong to an api subject only; nothing on a CLI reads them",
            (true, _) => ApiRefusal(id, api),
            _ => string.Empty,
        };

    /// <summary>The api transport's three fields, each refused by name: the vendor and the dialect are words (the vault key's
    /// name is the vendor id), the endpoint is a public vendor url — the gate's own endpoint rule, so the publication guard
    /// passes it in the one column that may hold a url.</summary>
    private static string ApiRefusal(ProbeSubjectId id, ApiTransport api) =>
        (Slug.IsValid(api.Vendor), Slug.IsValid(api.Dialect), ReviewerEndpoint.Parse(api.Endpoint)) switch
        {
            (false, _, _) => $"subject '{id}': '{Short(api.Vendor)}' is not a vendor id — an api subject names the coai vendor row (--vendor), {Slug.Rule}",
            (_, false, _) => $"subject '{id}': '{Short(api.Dialect)}' is not a dialect — an api subject names the wire dialect (--dialect: xai, openai, anthropic), {Slug.Rule}",
            (_, _, Outcome<ReviewerEndpoint>.Ok { Value: ReviewerEndpoint.Value }) => string.Empty,
            (_, _, Outcome<ReviewerEndpoint>.Fail f) => $"subject '{id}': the endpoint is refused — {f.Reason}",
            _ => $"subject '{id}': the endpoint '{Short(api.Endpoint)}' is not a public vendor url — an api subject names the vendor's base url (https://api.x.ai/v1), never a reference or nothing",
        };

    private static bool IsModelId(string model) => model.Length > 0 && !ModelConfig.LooksLikeAValue(model);

    /// <summary>Why an executable reference is refused — naming the SHAPE, because "invalid reference" teaches nobody the rule.</summary>
    private static string ExecutableRefusal(string value) =>
        (VariableName.IsMatch(value), IsPath(value), KeyShaped.IsMatch(value)) switch
        {
            (true, _, _) => string.Empty,
            (_, true, _) => $"the executable reference '{Short(value)}' is a PATH — store the NAME of the environment variable that holds it; "
                            + "this database is published unedited, and a path in it is a machine's identity leaving with the results",
            (_, _, true) => $"the executable reference '{Short(value)}' looks like a KEY — a reference is the NAME of an environment variable, "
                            + "and a secret in this database would be published with it",
            _ => $"'{Short(value)}' is not an environment variable NAME — upper-case letters, digits and underscores, starting with a letter (BENCH_CLAUDE)",
        };

    private static bool IsPath(string value) =>
        ModelConfig.LooksLikeAValue(value) || value.Contains('/') || value.Contains('\\') || value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

    private static string Short(string value) => value.Length <= 40 ? value : value[..40] + "…";

    public string Describe =>
        $"{Id} · {ProbeRuntimeWord.Of(Runtime)} · {ModelId} · {ExecutableRef}" + (Runtime == ProbeRuntime.Api ? $" · {Vendor} @ {Endpoint} ({Dialect})" : string.Empty);
}

/// <summary>The subjects file — <c>samples/question-consultant-probe-subjects.json</c> — read into frozen subjects. A field
/// the reader does not know, a runtime word it does not know, or an id listed twice is refused by name: a file that is
/// accepted with a typo in it is a run that measures something nobody configured.</summary>
public static class ProbeSubjectsFile
{
    private static readonly IReadOnlySet<string> KnownFields =
        new HashSet<string>(StringComparer.Ordinal) { "id", "runtime", "model", "executableRef", "vendor", "endpoint", "dialect" };

    public static Outcome<IReadOnlyList<ProbeSubject>> Read(string json)
    {
        try
        {
            return JsonNode.Parse(json) is JsonObject root && root["subjects"] is JsonArray subjects
                ? Entries(subjects)
                : Outcome<IReadOnlyList<ProbeSubject>>.Failure("the subjects file has no 'subjects' array — one entry per subject: id, runtime, model, executableRef");
        }
        catch (JsonException ex)
        {
            return Outcome<IReadOnlyList<ProbeSubject>>.Failure($"the subjects file is not JSON — {ex.Message}");
        }
    }

    private static Outcome<IReadOnlyList<ProbeSubject>> Entries(JsonArray subjects)
    {
        var read = new List<ProbeSubject>();

        foreach (var (entry, index) in subjects.Select((node, index) => (node, index)))
        {
            var outcome = Entry(entry, index);

            if (outcome is Outcome<ProbeSubject>.Fail fail)
            {
                return Outcome<IReadOnlyList<ProbeSubject>>.Failure(fail.Reason);
            }

            read.Add(((Outcome<ProbeSubject>.Ok)outcome).Value);
        }

        var twice = read.GroupBy(s => s.Id.Value, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);

        return twice is null
            ? Outcome<IReadOnlyList<ProbeSubject>>.Success(read)
            : Outcome<IReadOnlyList<ProbeSubject>>.Failure($"subject '{twice.Key}' is listed twice in the subjects file");
    }

    private static Outcome<ProbeSubject> Entry(JsonNode? node, int index)
    {
        if (node is not JsonObject entry)
        {
            return Outcome<ProbeSubject>.Failure($"subjects[{index}] is not an object");
        }

        var unknown = entry.Select(p => p.Key).FirstOrDefault(key => !KnownFields.Contains(key));

        return unknown is null
            ? ProbeSubject.Parse(
                Text(entry, "id"), Text(entry, "runtime"), Text(entry, "model"), Text(entry, "executableRef"),
                Text(entry, "vendor"), Text(entry, "endpoint"), Text(entry, "dialect"))
            : Outcome<ProbeSubject>.Failure(
                $"subjects[{index}] ('{Text(entry, "id")}') carries a field this reader does not know: '{unknown}' — the fields are {string.Join(", ", KnownFields)}");
    }

    private static string Text(JsonObject o, string name) => o[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : string.Empty;
}
