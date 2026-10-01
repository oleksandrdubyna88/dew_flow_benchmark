using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
    private ProbeSubject(ProbeSubjectId id, ProbeRuntime runtime, string modelId, string executableRef)
    {
        Id = id;
        Runtime = runtime;
        ModelId = modelId;
        ExecutableRef = executableRef;
    }

    public ProbeSubjectId Id { get; }

    public ProbeRuntime Runtime { get; }

    public string ModelId { get; }

    /// <summary>The NAME of the environment variable holding the executable's path — <c>BENCH_CLAUDE</c>.</summary>
    public string ExecutableRef { get; }

    /// <summary>An environment variable NAME as every shell spells one: upper-case letters, digits and underscores. Narrower
    /// than <c>ModelConfig</c>'s reference on purpose — a probe subject names a variable, never a configuration section — and
    /// narrow enough that the two shapes people paste by mistake (a path, a key) cannot pass as one.</summary>
    [GeneratedRegex("^[A-Z][A-Z0-9_]{0,127}$")]
    private static partial Regex VariableName { get; }

    /// <summary>The shapes an API key takes: a vendor prefix (<c>sk-</c>, <c>xai-</c>, <c>AIza</c>, <c>ghp_</c>, <c>gsk_</c>,
    /// <c>AKIA</c>) or a long mixed-case alphanumeric run with no underscore in it.</summary>
    [GeneratedRegex(@"^(?:sk-|xai-|AIza|ghp_|gsk_|AKIA|pk-)|^(?=.*[a-z])(?=.*[A-Z0-9])[A-Za-z0-9+/=-]{24,}$")]
    private static partial Regex KeyShaped { get; }

    public static Outcome<ProbeSubject> Parse(string? id, string? runtimeWord, string? modelId, string? executableRef) =>
        ProbeSubjectId.Parse(id).Match(
            subjectId => ProbeRuntimeWord.Parse(runtimeWord).Match(
                runtime => Checked(subjectId, runtime, (modelId ?? string.Empty).Trim(), (executableRef ?? string.Empty).Trim()),
                Outcome<ProbeSubject>.Failure),
            Outcome<ProbeSubject>.Failure);

    private static Outcome<ProbeSubject> Checked(ProbeSubjectId id, ProbeRuntime runtime, string model, string executableRef)
    {
        var refusal = Refusal(id, model, executableRef);

        return refusal.Length > 0
            ? Outcome<ProbeSubject>.Failure(refusal)
            : Outcome<ProbeSubject>.Success(new ProbeSubject(id, runtime, model, executableRef));
    }

    private static string Refusal(ProbeSubjectId id, string model, string executableRef) =>
        (IsModelId(model), ExecutableRefusal(executableRef)) switch
        {
            (false, _) => $"'{Short(model)}' is not a model id for subject '{id}' — a model id is the string the runtime is asked for, never a url or a path, and never empty",
            (_, { Length: > 0 } why) => $"subject '{id}': {why}",
            _ => string.Empty,
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

    public string Describe => $"{Id} · {ProbeRuntimeWord.Of(Runtime)} · {ModelId} · {ExecutableRef}";
}

/// <summary>The subjects file — <c>samples/question-consultant-probe-subjects.json</c> — read into frozen subjects. A field
/// the reader does not know, a runtime word it does not know, or an id listed twice is refused by name: a file that is
/// accepted with a typo in it is a run that measures something nobody configured.</summary>
public static class ProbeSubjectsFile
{
    private static readonly IReadOnlySet<string> KnownFields = new HashSet<string>(StringComparer.Ordinal) { "id", "runtime", "model", "executableRef" };

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
            ? ProbeSubject.Parse(Text(entry, "id"), Text(entry, "runtime"), Text(entry, "model"), Text(entry, "executableRef"))
            : Outcome<ProbeSubject>.Failure(
                $"subjects[{index}] ('{Text(entry, "id")}') carries a field this reader does not know: '{unknown}' — the fields are {string.Join(", ", KnownFields)}");
    }

    private static string Text(JsonObject o, string name) => o[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : string.Empty;
}
