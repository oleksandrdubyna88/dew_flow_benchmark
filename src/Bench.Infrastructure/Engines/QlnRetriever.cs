using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bench.Application;
using Bench.Domain;
using Bench.Domain.Retrieval;
using Bench.Domain.Runs;
using Bench.Domain.Trace;
using Bench.Domain.Variants;

namespace Bench.Infrastructure.Engines;

/// <summary>The wire to the QLN search endpoint — the one place this repository knows that engine's JSON.
/// <para>
/// <b>No project reference to that repository, deliberately.</b> This benchmark measures engines it does
/// not compile against — a third-party service, a competitor's index — so the coupling that matters is the
/// WIRE, and an adapter that referenced QLN's assemblies would be an adapter only QLN could ever have.
/// </para>
/// <para>
/// <b>Separate from <see cref="QlnEngine"/>, which composes it.</b> Retrieval-as-a-CALL (this class,
/// serving the single-shot lane) and retrieval-as-a-SURFACE (that class, serving a subject's tool loop) are
/// two measurements over one endpoint. Splitting them gives the wire code one owner and lets the
/// single-shot lane be wired without a filesystem root it would never read — a dependency that is inert
/// only until somebody calls it.
/// </para>
/// <para>
/// <b>The funnel goes to a sink, and a rejected one goes there too.</b> An engine that claims
/// <c>trace/v0</c> and then sends stages this build does not define renders exactly like a black-box engine
/// once the reason is dropped — see <see cref="IFunnelSink"/>.
/// </para></summary>
public sealed class QlnRetriever(
    HttpClient http,
    Guid projectId,
    string branch,
    IFunnelSink funnels,
    string engineVersion = "",
    string indexFingerprint = "") : IRetriever
{
    /// <summary>Web naming, and <b>omitted means "keep the engine's shipped default"</b>.
    /// <para>
    /// The ignore condition is load-bearing rather than tidiness. Every axis on the request record is
    /// nullable, and a null is left OUT of the JSON: an adapter that serialised its own C# defaults would
    /// send <c>rerank: false, rerankPool: 0, rrfK: 0</c> for a call that meant to set only a limit, and the
    /// engine would dutifully clamp those into a configuration nobody asked for — measuring a recipe that
    /// exists in no catalog row.
    /// </para></summary>
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public EngineRef Describe =>
        new(EngineKind.Qln, http.BaseAddress?.ToString() ?? string.Empty, engineVersion, indexFingerprint);

    /// <summary>One retrieval for the single-shot lane: the variant's recipe out, hits and funnel back.
    /// <para>
    /// The recipe is mapped to this engine's axes by <see cref="AxesWire.From"/>, which REFUSES a recipe
    /// this engine cannot express rather than sending it and hoping. That refusal is the point: the axes
    /// contract has no <c>JsonUnmappedMemberHandling.Disallow</c>, so an engine handed a field it does not
    /// know ignores it and echoes back a well-formed set that merely lacks it — which would record
    /// <c>wsum</c> in a variant's definition and rank fusion in its numbers, with nothing anywhere
    /// disagreeing. That is the reranker scar exactly.
    /// </para></summary>
    public async Task<Outcome<RetrievedContext>> RetrieveAsync(
        RetrievalRequest request, CancellationToken cancellationToken)
    {
        var axes = AxesWire.From(request.Recipe);

        if (axes is Outcome<AxesWire>.Fail unexpressible)
        {
            return Outcome<RetrievedContext>.Failure(unexpressible.Reason);
        }

        var sent = ((Outcome<AxesWire>.Ok)axes).Value;
        var searched = await SearchAsync(request.Query, sent, cancellationToken);

        return searched.Match(
            response => Outcome<RetrievedContext>.Success(Context(response, sent)),
            reason => Outcome<RetrievedContext>.Failure($"the QLN engine did not retrieve: {reason}"));
    }

    /// <summary>One search, and the funnel it produced handed to the sink either way. Internal because
    /// <see cref="QlnEngine"/> serves its search TOOL through the same call — one round trip, one funnel
    /// path, whichever lane asked.</summary>
    internal async Task<Outcome<Searched>> SearchAsync(
        string query, AxesWire axes, CancellationToken cancellationToken)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        HttpResponseMessage response;

        try
        {
            response = await http.PostAsJsonAsync(
                $"api/rag/projects/{projectId:D}/search",
                new SearchInputWire { Query = query, Branch = branch, Axes = axes },
                Json,
                cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            // An engine that is down is an environment fact about the run, not an exception that ends
            // ten thousand legs — the leg records it and the report says which engine was unreachable.
            return Outcome<Searched>.Failure($"unreachable: {ex.Message}");
        }

        // Read as text rather than straight into the type: the response's SIZE is part of what a cell
        // records, and a deserializer that consumed the stream leaves nothing to measure.
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        return response.IsSuccessStatusCode
            ? Read(body, clock.ElapsedMilliseconds)
            : Outcome<Searched>.Failure($"HTTP {(int)response.StatusCode}: {Trim(body)}");
    }

    private Outcome<Searched> Read(string body, long elapsedMs)
    {
        SearchWire? wire;

        try
        {
            wire = JsonSerializer.Deserialize<SearchWire>(body, Json);
        }
        catch (JsonException ex)
        {
            return Outcome<Searched>.Failure($"the engine's answer could not be read: {ex.Message}");
        }

        if (wire is null)
        {
            return Outcome<Searched>.Failure("the engine answered with an empty body");
        }

        funnels.Retrieved(ToFunnel(wire.Funnel));

        return Outcome<Searched>.Success(
            new Searched(wire, System.Text.Encoding.UTF8.GetByteCount(body), elapsedMs));
    }

    /// <summary>The response as this system stores it.
    /// <para>
    /// Two clocks, deliberately: <see cref="RetrievedContext.ElapsedMs"/> is what THIS process waited —
    /// network and deserialization included — while the funnel's total is what the engine measured inside
    /// itself. Reporting one as the other is how a slow network becomes a slow reranker.
    /// </para></summary>
    private static RetrievedContext Context(Searched response, AxesWire sent)
    {
        var funnel = ToFunnel(response.Wire.Funnel);

        return RetrievedContext.Of(
            response.Wire.Collection,
            [.. response.Wire.Hits.Select((hit, index) => ToHit(hit, index + 1))],
            funnel.Match(ok => ok, _ => RetrievalFunnel.None),
            funnel.Match(_ => string.Empty, reason => reason),
            sent.Axes,
            Echoed(response.Wire.Axes),
            response.PayloadBytes,
            response.ElapsedMs);
    }

    /// <summary>Their funnel as ours, or the reason it could not be read.
    /// <para>
    /// The version is taken from the PAYLOAD rather than from what the engine declared, and the two are not
    /// the same claim: an engine can be configured to say <c>trace/v0</c> while a newer build behind the
    /// same endpoint answers with something else. What served the request is what counts.
    /// </para></summary>
    internal static Outcome<RetrievalFunnel> ToFunnel(FunnelWire? funnel)
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

    private static RetrievedHit ToHit(HitWire hit, int rank) => new(
        rank,
        hit.RelativePath,
        hit.StartLine,
        hit.EndLine,
        Member(hit),
        hit.MemberKey,
        hit.Signature,
        hit.Score,
        hit.Ordering,
        hit.Channels,
        hit.Ranks,
        hit.Text.Length > 0
            ? HitSnippet.Text(hit.Text)
            : HitSnippet.NotReported("the engine's hit carried an empty text field"));

    /// <summary>The readable <c>Type.Member</c> identity a suite's anchors are written in. A file-scoped hit
    /// has no type, and a hit with neither has no member identity at all — which is a state, not an empty
    /// string standing in for one.</summary>
    private static string Member(HitWire hit) =>
        (hit.TypeName.Length, hit.MemberName.Length) switch
        {
            (> 0, > 0) => $"{hit.TypeName}.{hit.MemberName}",
            (0, > 0) => hit.MemberName,
            _ => string.Empty,
        };

    /// <summary>The axes the engine says it applied, read as NAMED VALUES rather than into a typed mirror.
    /// <para>
    /// Every field the engine echoes is stored, including ones this build never sets and ones a future build
    /// of that engine adds. A typed mirror would silently drop exactly the axis whose appearance matters
    /// most — the one we did not know about.
    /// </para></summary>
    private static EngineAxes Echoed(JsonElement axes) =>
        axes.ValueKind != JsonValueKind.Object
            ? EngineAxes.None
            : new EngineAxes([.. axes.EnumerateObject().Select(p => new Axis(p.Name, p.Value.ToString()))]);

    internal static string Trim(string body) => body.Length <= 400 ? body : body[..400] + "…";
}

/// <summary>The request shape, as their endpoint reads it.</summary>
internal sealed record SearchInputWire
{
    [JsonPropertyName("query")] public string Query { get; init; } = string.Empty;

    [JsonPropertyName("branch")] public string Branch { get; init; } = string.Empty;

    [JsonPropertyName("axes")] public AxesWire? Axes { get; init; }
}

/// <summary>The query-time axes, as this engine's contract spells them.
/// <para>
/// Every field is nullable and an omitted one keeps the engine's shipped default — see the ignore condition
/// on <see cref="QlnRetriever.Json"/>, which is what makes "omitted" real rather than "sent as a C# zero".
/// </para>
/// <para>
/// This record is the ONE place a recipe becomes a request, and it is also where the stored
/// <see cref="EngineAxes"/> comes from (<see cref="Axes"/>). One source, so what a run records as "asked
/// for" is literally what went out — not a second mapping that agrees with the first until somebody edits
/// one of them.
/// </para></summary>
internal sealed record AxesWire
{
    [JsonPropertyName("limit")] public int? Limit { get; init; }

    [JsonPropertyName("dense")] public bool? Dense { get; init; }

    [JsonPropertyName("sparse")] public bool? Sparse { get; init; }

    [JsonPropertyName("denseWeight")] public double? DenseWeight { get; init; }

    [JsonPropertyName("sparseWeight")] public double? SparseWeight { get; init; }

    [JsonPropertyName("rrfK")] public int? RrfK { get; init; }

    [JsonPropertyName("rerank")] public bool? Rerank { get; init; }

    [JsonPropertyName("rerankPool")] public int? RerankPool { get; init; }

    /// <summary>A result count and nothing else — the tool-surface path, where the subject asks for a search
    /// and every other knob is the engine's own.</summary>
    public static AxesWire Limited(int limit) => new() { Limit = limit };

    /// <summary>A catalog recipe as this engine's axes, or a refusal naming what it cannot express.
    /// <para>
    /// The refusals matter more than the mapping. This engine fuses by reciprocal rank only: it has no
    /// fusion-MODE axis and no normalization axis, so a <c>wsum</c> recipe cannot be served here — and
    /// because its axes contract ignores fields it does not know, sending one would produce a run that
    /// records a weighted sum and measures rank fusion. Both halves of that guarantee are named in
    /// <c>todo/PLAN_variant_matrix.md</c> §4 items 1 and 5; until the engine's half lands, a variant that
    /// needs it is refused HERE, loudly, rather than measured wrongly.
    /// </para>
    /// <para>
    /// The corpus half of a recipe — text shape, chunk size, embed model — is not an axis at all: it selects
    /// a COLLECTION, chosen by the engine's own recipe machinery. What comes back is the collection that
    /// answered, and it is stored; verifying that the collection IS this recipe's needs the index
    /// preparations of §3.4 step 2 and is build-order step 5.
    /// </para></summary>
    public static Outcome<AxesWire> From(VariantDefinition.RetrievalRecipe recipe)
    {
        var refusal = Refuse(recipe);

        return refusal.Length > 0
            ? Outcome<AxesWire>.Failure(refusal)
            : Outcome<AxesWire>.Success(new AxesWire
            {
                Limit = recipe.Limit,
                Dense = recipe.Channels is RetrievalChannels.Dense or RetrievalChannels.Hybrid,
                Sparse = recipe.Channels is RetrievalChannels.Sparse or RetrievalChannels.Hybrid,
                DenseWeight = recipe.Fusion.DenseWeight,
                SparseWeight = recipe.Fusion.SparseWeight,
                RrfK = recipe.Fusion.K,
                Rerank = recipe.Rerank.Enabled,
                RerankPool = recipe.Rerank.Enabled ? recipe.Rerank.Pool : null,
            });
    }

    /// <summary>What this run asked for, as named values, derived from the fields actually set above.
    /// <para>
    /// <b><c>JsonIgnore</c>, and not as a formality.</b> This is a computed property on a record that IS the
    /// request body, so without it the serializer wrote our own bookkeeping into the outgoing JSON as a
    /// nested <c>axes.axes</c> object. Today the engine's axes contract ignores members it does not know, so
    /// that noise was invisible — and the sibling plan's §4 item 5 is to make that contract REFUSE unknown
    /// members, which would have turned every retrieval in this benchmark into a 400 on the day it landed.
    /// </para></summary>
    [JsonIgnore]
    public EngineAxes Axes => new(
    [
        .. Named("limit", Limit),
        .. Named("dense", Dense),
        .. Named("sparse", Sparse),
        .. Named("denseWeight", DenseWeight),
        .. Named("sparseWeight", SparseWeight),
        .. Named("rrfK", RrfK),
        .. Named("rerank", Rerank),
        .. Named("rerankPool", RerankPool),
    ]);

    private static string Refuse(VariantDefinition.RetrievalRecipe recipe) =>
        (recipe.Engine, recipe.Fusion.Mode, recipe.Fusion.Normalization) switch
        {
            (not EngineKind.Qln, _, _) =>
                $"this adapter serves {EngineKind.Qln}, and the recipe names {recipe.Engine} — an engine that "
                + "measured another engine's recipe would label the result with the recipe and the numbers with itself",
            (_, not FusionSpec.ReciprocalRank, _) =>
                $"this engine fuses by {FusionSpec.ReciprocalRank} only and its axes contract has no fusion-mode "
                + $"field, so a '{recipe.Fusion.Mode}' recipe cannot be served: the request would be accepted, the "
                + "field ignored, and the run would record a weighted sum while measuring rank fusion. The engine's "
                + "half of this is PLAN_search_variant_axes in dew_flow_rag_qln",
            (_, _, not FusionSpec.NoNormalization) =>
                $"this engine has no score-normalization axis, so '{recipe.Fusion.Normalization}' cannot be served "
                + "— and a normalization that is recorded but not applied is the same defect as an ignored fusion mode",
            _ => string.Empty,
        };

    /// <summary>One axis, or nothing at all when it was not set. A spread over an empty sequence is how
    /// "this axis was omitted" stays absent from the record instead of appearing as a default.</summary>
    private static IEnumerable<Axis> Named<T>(string name, T? value)
        where T : struct =>
        value is { } set
            ? [new Axis(name, Canonical(set))]
            : [];

    private static string Canonical<T>(T value)
        where T : struct =>
        value switch
        {
            bool flag => flag ? "true" : "false",
            double number => number.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
        };
}

/// <summary>One answered search: the payload, its size, and what this process waited for it.</summary>
internal sealed record Searched(SearchWire Wire, long PayloadBytes, long ElapsedMs);

/// <summary>The response shape, pinned to what `dew_flow_rag_qln` emitted on 2026-08-15
/// (<c>Platform.Contracts.SearchResponse</c>). Anything this record does not name is ignored, so their
/// adding a field cannot break a run — only a RENAME can, and that is what the contract version is for.</summary>
internal sealed record SearchWire
{
    [JsonPropertyName("hits")] public IReadOnlyList<HitWire> Hits { get; init; } = [];

    [JsonPropertyName("collection")] public string Collection { get; init; } = string.Empty;

    [JsonPropertyName("funnel")] public FunnelWire? Funnel { get; init; }

    /// <summary>The axes AS APPLIED, kept as raw JSON on purpose: every field the engine echoes is stored,
    /// including the ones this build never sets and the ones a future build of that engine adds.</summary>
    [JsonPropertyName("axes")] public JsonElement Axes { get; init; }
}

internal sealed record HitWire
{
    [JsonPropertyName("relativePath")] public string RelativePath { get; init; } = string.Empty;

    [JsonPropertyName("startLine")] public int StartLine { get; init; }

    [JsonPropertyName("endLine")] public int EndLine { get; init; }

    [JsonPropertyName("typeName")] public string TypeName { get; init; } = string.Empty;

    [JsonPropertyName("memberName")] public string MemberName { get; init; } = string.Empty;

    /// <summary>The engine's own member identity. Stored verbatim, never matched on — its format is that
    /// engine's, and an anchor authored here does not share it.</summary>
    [JsonPropertyName("memberKey")] public string MemberKey { get; init; } = string.Empty;

    [JsonPropertyName("signature")] public string Signature { get; init; } = string.Empty;

    /// <summary>The hit's own source text. This is what makes single-shot RAG possible at all — and it is
    /// the bulk of what a retrieval run stores, which is why its retention has an owner.</summary>
    [JsonPropertyName("text")] public string Text { get; init; } = string.Empty;

    [JsonPropertyName("score")] public double Score { get; init; }

    [JsonPropertyName("ordering")] public string Ordering { get; init; } = string.Empty;

    [JsonPropertyName("channels")] public IReadOnlyList<string> Channels { get; init; } = [];

    /// <summary>Its rank within each channel that found it. What separates "the sparse head found it and
    /// fusion buried it" from "nothing found it".</summary>
    [JsonPropertyName("ranks")] public IReadOnlyList<int> Ranks { get; init; } = [];
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
