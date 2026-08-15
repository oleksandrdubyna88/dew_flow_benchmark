using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bench.Application;
using Bench.Domain;
using Bench.Domain.Engines;
using Bench.Domain.Runs;
using Bench.Domain.Trace;

namespace Bench.Infrastructure.Engines;

/// <summary>The QLN retrieval engine, over HTTP.
/// <para>
/// <b>No project reference to that repository, deliberately.</b> This benchmark measures engines it does
/// not compile against — a third-party service, a competitor's index — so the coupling that matters is
/// the WIRE, and an adapter that referenced QLN's assemblies would be an adapter only QLN could ever
/// have. It also means this file is the one place a change in their JSON shows up, rather than a
/// compile error scattered across a solution.
/// </para>
/// <para>
/// <b>It offers file tools as well as search, by composing the baseline engine.</b> Not a convenience:
/// the measured retrieval arm made 63 file reads beside its 39 searches, and an engine that offered
/// search ALONE would be measuring a subject working with one hand tied — against a baseline that has
/// both. The comparison "retrieval against no retrieval" is only honest if the retrieval arm is a
/// superset.
/// </para>
/// <para>
/// <b>The funnel goes to a sink, and a rejected one goes there too.</b> An engine that claims
/// <c>trace/v0</c> and then sends stages this build does not define renders exactly like a black-box
/// engine once the reason is dropped — see <see cref="IFunnelSink"/>.
/// </para></summary>
public sealed class QlnEngine(
    HttpClient http,
    Guid projectId,
    string branch,
    IEngine files,
    IFunnelSink funnels,
    string engineVersion = "",
    string indexFingerprint = "") : IEngine
{
    public const string SearchCode = "rag_search_code";

    /// <summary>Hits a single search returns. Deliberately not the engine's own default: a subject that
    /// gets 150 members per call spends its context on the tool rather than on the question, and the
    /// result count is a measured axis rather than a taste.</summary>
    private const int DefaultLimit = 20;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public EngineRef Describe => new(EngineKind.Qln, http.BaseAddress?.ToString() ?? string.Empty, engineVersion, indexFingerprint);

    /// <summary>Declared, never assumed. This engine is expected to emit <c>trace/v0</c>; whether the
    /// payload actually validates is decided per response, and a mismatch degrades that response to
    /// black-box rather than failing the leg.</summary>
    public string TraceContractVersion => TraceContract.V0;

    public IReadOnlyList<EngineTool> Tools =>
    [
        new EngineTool(
            SearchCode,
            "Search the indexed code by MEANING rather than by name: ask what something does, in a full "
            + "sentence, and get back the members that do it. Returns file, line span, signature and the "
            + "reason each hit matched. Use it when you do not know what a thing is called; use the file "
            + "tools when you do.",
            """{"query":"string","limit":"int?"}"""),
        .. files.Tools,
    ];

    /// <summary>Confirms the index is reachable and answers before a run spends anything.
    /// <para>
    /// A search rather than a health check: an engine whose process is up and whose collection was never
    /// built returns zero hits, which is indistinguishable from a hard question. Upstream, a stale pinned
    /// port left a cross-encoder dead for four measured arms while the settings page still reported one —
    /// what an engine SAYS about itself is not what served the run.
    /// </para></summary>
    public async Task<Outcome<string>> WarmAsync(string checkoutPath, CancellationToken cancellationToken)
    {
        var probe = await PostAsync("does this index answer at all", 1, cancellationToken);

        return probe.Match(
            response => Outcome<string>.Success(
                $"{Describe.Canonical}|collection={response.Collection}"),
            reason => Outcome<string>.Failure($"the QLN index did not answer: {reason}"));
    }

    public async Task<ToolAnswer> InvokeAsync(string tool, string argumentsJson, CancellationToken cancellationToken)
    {
        if (tool != SearchCode)
        {
            return await files.InvokeAsync(tool, argumentsJson, cancellationToken);
        }

        var arguments = Parse(argumentsJson);
        if (arguments is Outcome<JsonElement>.Fail bad)
        {
            return ToolAnswer.Refusal(bad.Reason);
        }

        var args = ((Outcome<JsonElement>.Ok)arguments).Value;
        var query = Text(args, "query");
        if (query.Length == 0)
        {
            return ToolAnswer.Refusal("argument 'query' is required — ask what the code does, in a sentence");
        }

        var result = await PostAsync(query, Number(args, "limit", DefaultLimit), cancellationToken);

        return result.Match(Render, ToolAnswer.Failure);
    }

    /// <summary>One search, and the funnel it produced handed to the sink either way.</summary>
    private async Task<Outcome<SearchWire>> PostAsync(string query, int limit, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync(
                $"api/rag/projects/{projectId:D}/search",
                new SearchInputWire { Query = query, Branch = branch, Axes = new AxesWire { Limit = limit } },
                Json,
                cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            // An engine that is down is an environment fact about the run, not an exception that ends
            // ten thousand legs — the leg records it and the report says which engine was unreachable.
            return Outcome<SearchWire>.Failure($"unreachable: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return Outcome<SearchWire>.Failure($"HTTP {(int)response.StatusCode}: {Trim(body)}");
        }

        var wire = await response.Content.ReadFromJsonAsync<SearchWire>(Json, cancellationToken);
        if (wire is null)
        {
            return Outcome<SearchWire>.Failure("the engine answered with an empty body");
        }

        funnels.Retrieved(ToFunnel(wire.Funnel));
        return Outcome<SearchWire>.Success(wire);
    }

    /// <summary>Their funnel as ours, or the reason it could not be read.
    /// <para>
    /// The version is taken from the PAYLOAD rather than from what the engine declared, and the two are
    /// not the same claim: an engine can be configured to say <c>trace/v0</c> while a newer build behind
    /// the same endpoint answers with something else. What served the request is what counts.
    /// </para></summary>
    private static Outcome<RetrievalFunnel> ToFunnel(FunnelWire? funnel)
    {
        if (funnel is null)
        {
            return Outcome<RetrievalFunnel>.Failure(
                "the engine declared a trace contract but sent no funnel with its answer");
        }

        var version = funnel.ContractVersion.Length == 0 ? "(none stamped)" : funnel.ContractVersion;

        return TraceContract.Validate(new RetrievalFunnel(
            version,
            [.. funnel.Stages.Select(s => new FunnelStage(s.Name, s.In, s.Out, s.Ms))],
            funnel.TotalMs,
            funnel.Absent));
    }

    /// <summary>The hits as a subject reads them.
    /// <para>
    /// Line spans are included because the next thing a subject does with a hit is read around it, and a
    /// result without a span costs it a whole file. The matching REASON — which channel found it, and
    /// whether the order came from the reranker or the fusion — is included for the same purpose the
    /// engine carries it: a score with no stated origin cannot be compared to anything.
    /// </para></summary>
    private static ToolAnswer Render(SearchWire result)
    {
        if (result.Hits.Count == 0)
        {
            // Distinct from a failure, and said in words: an index that answered with nothing is a fact
            // about the query, while a silent empty string is a fact about nothing.
            return ToolAnswer.Success($"no hits in {result.Collection}");
        }

        var lines = result.Hits.Select(h =>
            $"{h.RelativePath}:{h.StartLine}-{h.EndLine}  {h.TypeName}.{h.MemberName}  "
            + $"[{h.Ordering} {h.Score:F3}; found by {string.Join('+', h.Channels)}]\n    {h.Signature}");

        return ToolAnswer.Success(string.Join('\n', lines));
    }

    private static string Trim(string body) => body.Length <= 400 ? body : body[..400] + "…";

    private static Outcome<JsonElement> Parse(string argumentsJson)
    {
        try
        {
            var text = string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson;
            return Outcome<JsonElement>.Success(JsonDocument.Parse(text).RootElement.Clone());
        }
        catch (JsonException ex)
        {
            return Outcome<JsonElement>.Failure($"arguments are not valid JSON: {ex.Message}");
        }
    }

    private static string Text(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object
        && args.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int Number(JsonElement args, string name, int fallback) =>
        args.ValueKind == JsonValueKind.Object
        && args.TryGetProperty(name, out var value)
        && value.TryGetInt32(out var number)
            ? number
            : fallback;
}

/// <summary>The request shape, as their endpoint reads it.</summary>
internal sealed record SearchInputWire
{
    [JsonPropertyName("query")] public string Query { get; init; } = string.Empty;

    [JsonPropertyName("branch")] public string Branch { get; init; } = string.Empty;

    [JsonPropertyName("axes")] public AxesWire? Axes { get; init; }
}

/// <summary>Only the axis this adapter sets. Every other knob keeps the engine's shipped default, and
/// the response echoes back what actually applied — so a run records what served it rather than what it
/// asked for.</summary>
internal sealed record AxesWire
{
    [JsonPropertyName("limit")] public int Limit { get; init; }
}

/// <summary>The response shape, pinned to what `dew_flow_rag_qln` emitted on 2026-08-15
/// (<c>Platform.Contracts.SearchResponse</c>). Anything this record does not name is ignored, so their
/// adding a field cannot break a run — only a RENAME can, and that is what the contract version is
/// for.</summary>
internal sealed record SearchWire
{
    [JsonPropertyName("hits")] public IReadOnlyList<HitWire> Hits { get; init; } = [];

    [JsonPropertyName("collection")] public string Collection { get; init; } = string.Empty;

    [JsonPropertyName("funnel")] public FunnelWire? Funnel { get; init; }
}

internal sealed record HitWire
{
    [JsonPropertyName("relativePath")] public string RelativePath { get; init; } = string.Empty;

    [JsonPropertyName("startLine")] public int StartLine { get; init; }

    [JsonPropertyName("endLine")] public int EndLine { get; init; }

    [JsonPropertyName("typeName")] public string TypeName { get; init; } = string.Empty;

    [JsonPropertyName("memberName")] public string MemberName { get; init; } = string.Empty;

    [JsonPropertyName("signature")] public string Signature { get; init; } = string.Empty;

    [JsonPropertyName("score")] public double Score { get; init; }

    [JsonPropertyName("ordering")] public string Ordering { get; init; } = string.Empty;

    [JsonPropertyName("channels")] public IReadOnlyList<string> Channels { get; init; } = [];
}

internal sealed record FunnelWire
{
    [JsonPropertyName("contractVersion")] public string ContractVersion { get; init; } = string.Empty;

    [JsonPropertyName("stages")] public IReadOnlyList<StageWire> Stages { get; init; } = [];

    [JsonPropertyName("totalMs")] public long TotalMs { get; init; }

    [JsonPropertyName("absent")] public IReadOnlyList<string> Absent { get; init; } = [];
}

internal sealed record StageWire
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;

    [JsonPropertyName("in")] public int In { get; init; }

    [JsonPropertyName("out")] public int Out { get; init; }

    [JsonPropertyName("ms")] public long Ms { get; init; }
}
