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

    /// <summary>What this recipe becomes on the wire, or why it cannot. The same decision
    /// <see cref="RetrieveAsync"/> makes, asked before any cell exists — so a run whose variant names an axis
    /// this engine has no field for ends at its start rather than as ten thousand identical leg failures.</summary>
    public Outcome<string> CanServe(VariantDefinition.RetrievalRecipe recipe) =>
        QlnRequest.From(recipe).Match(request => Outcome<string>.Success(request.Asked.Canonical), Outcome<string>.Failure);

    /// <summary>What is in the index a search for this recipe will actually reach.
    /// <para>
    /// <b>The window size is deliberately NOT sent.</b> This engine's index-state read accepts one
    /// (<c>?windowTokens=</c>) and will happily describe the collection a 512-token corpus WOULD live in —
    /// but its <c>/search</c> endpoint has no such parameter, so a search always goes to whatever the
    /// resolver's default window produces. Asking about a collection nobody can query would confirm a corpus
    /// that is not the one serving the run, which is the mislabelling this read exists to catch rather than a
    /// subtler version of it. So the read describes the SERVING corpus, and the recipe is compared against
    /// that.
    /// </para></summary>
    public async Task<Outcome<IndexState>> InspectAsync(
        VariantDefinition.RetrievalRecipe recipe, CancellationToken cancellationToken)
    {
        var wire = QlnRequest.From(recipe);

        if (wire is Outcome<QlnRequest>.Fail unexpressible)
        {
            return Outcome<IndexState>.Failure(unexpressible.Reason);
        }

        var shape = ((Outcome<QlnRequest>.Ok)wire).Value.TextShape ?? string.Empty;
        var query = $"api/rag/projects/{projectId:D}/index-state?textShape={Uri.EscapeDataString(shape)}"
            + (branch.Length > 0 ? $"&branch={Uri.EscapeDataString(branch)}" : string.Empty);

        try
        {
            var response = await http.GetAsync(query, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return response.IsSuccessStatusCode
                ? ReadState(body)
                : Outcome<IndexState>.Failure($"the index state could not be read: HTTP {(int)response.StatusCode}: {Trim(body)}");
        }
        catch (HttpRequestException ex)
        {
            return Outcome<IndexState>.Failure($"the index state could not be read: unreachable: {ex.Message}");
        }
    }

    private static Outcome<IndexState> ReadState(string body)
    {
        IndexStateWire? wire;

        try
        {
            wire = JsonSerializer.Deserialize<IndexStateWire>(body, Json);
        }
        catch (JsonException ex)
        {
            return Outcome<IndexState>.Failure($"the index state could not be read: {ex.Message}");
        }

        return wire is null
            ? Outcome<IndexState>.Failure("the engine described the index with an empty body")
            : Outcome<IndexState>.Success(new IndexState(
                wire.Collection,
                wire.Exists,
                wire.Points,
                new CorpusIdentity(
                    wire.TextShape, wire.WindowTokens, wire.OverlapTokens, wire.EmbedModel, wire.Tokenizer),
                wire.Fingerprint,
                IndexCommit.Read(wire.IndexedCommit),
                wire.WorkingTreeDirty,
                // No pass row at all is NOT a success: it means nothing this engine remembers wrote that
                // collection, so whatever is in it has no provenance.
                wire.PassStatus == SucceededStatus)
            {
                // Read, never inferred from the endpoint. A url says where a request went and nothing about
                // the host, provider or card that answered it — which is precisely the confound this axis
                // exists to remove, so guessing here would reintroduce it wearing a stored field's authority.
                Backend = BackendDeclaration.Read(wire.Backend),
            });
    }

    /// <summary>The engine's <c>IndexPassStatus.Succeeded</c>, as its JSON renders it — an ordinal, which is
    /// why the constant is named here rather than inlined at the comparison.</summary>
    private const int SucceededStatus = 2;

    /// <summary>One retrieval for the single-shot lane: the variant's recipe out, hits and funnel back.
    /// <para>
    /// The recipe becomes a request through <see cref="QlnRequest.From"/>, which REFUSES what this engine
    /// cannot serve rather than sending it and hoping. Since 2026-08-17 that refusal covers only a foreign
    /// engine's recipe and a corpus shape this one does not have: the fusion algorithm and its normalization —
    /// the two that used to be refused — became real axes when the sibling plan's step 1 landed.
    /// </para>
    /// <para>
    /// The engine's own axes contract now carries <c>JsonUnmappedMemberHandling.Disallow</c>, so a field it
    /// does not know is a 400 rather than a silent drop. That is the guarantee this side no longer has to
    /// catch — and it is why a stray member in the request record is now a broken retrieval rather than
    /// harmless noise.
    /// </para></summary>
    public async Task<Outcome<RetrievedContext>> RetrieveAsync(
        RetrievalRequest request, CancellationToken cancellationToken)
    {
        var wire = QlnRequest.From(request.Recipe);

        if (wire is Outcome<QlnRequest>.Fail unexpressible)
        {
            return Outcome<RetrievedContext>.Failure(unexpressible.Reason);
        }

        var sent = ((Outcome<QlnRequest>.Ok)wire).Value;
        var searched = await SearchAsync(request.Query, sent, cancellationToken);

        return searched.Match(
            response => Outcome<RetrievedContext>.Success(Context(response, sent)),
            reason => Outcome<RetrievedContext>.Failure($"the QLN engine did not retrieve: {reason}"));
    }

    /// <summary>One search, and the funnel it produced handed to the sink either way. Internal because
    /// <see cref="QlnEngine"/> serves its search TOOL through the same call — one round trip, one funnel
    /// path, whichever lane asked.</summary>
    internal async Task<Outcome<Searched>> SearchAsync(
        string query, QlnRequest request, CancellationToken cancellationToken)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        HttpResponseMessage response;

        try
        {
            response = await http.PostAsJsonAsync(
                $"api/rag/projects/{projectId:D}/search",
                new SearchInputWire
                {
                    Query = query,
                    Branch = branch,
                    Axes = request.Axes,
                    TextShape = request.TextShape,
                },
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
    private static RetrievedContext Context(Searched response, QlnRequest sent)
    {
        var funnel = ToFunnel(response.Wire.Funnel);

        return RetrievedContext.Of(
            response.Wire.Collection,
            [.. response.Wire.Hits.Select((hit, index) => ToHit(hit, index + 1))],
            funnel.Match(ok => ok, _ => RetrievalFunnel.None),
            funnel.Match(_ => string.Empty, reason => reason),
            sent.Asked,
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

    /// <summary>Which corpus variant answers. Index-time, so it selects a COLLECTION rather than a knob —
    /// which is why it rides beside the axes rather than inside them. Omitted keeps the engine's default.</summary>
    [JsonPropertyName("textShape")] public string? TextShape { get; init; }
}

/// <summary>One recipe as this engine's request: the query-time axes, and the corpus variant that answers.
/// <para>
/// Two levels of the same JSON, so one type carries both — and one type produces the stored
/// <see cref="Asked"/>, which therefore covers the whole recipe rather than only the half that happens to
/// live under <c>axes</c>.
/// </para></summary>
internal sealed record QlnRequest(AxesWire Axes, string? TextShape)
{
    /// <summary>The corpus variants this engine can select per request, by their own names.
    /// <para>
    /// Its vocabulary, not ours: a variant's <c>textShape</c> is a string the ADAPTER maps onto the engine
    /// it serves, because this benchmark measures engines it does not compile against. A shape this engine
    /// does not have is refused by name rather than sent — the request would otherwise be rejected at the
    /// boundary anyway, and a refusal that names the four legal values is worth more than a 400.
    /// </para></summary>
    public static IReadOnlyList<string> TextShapes => ["SourceOnly", "GraphHeader", "ClassContext", "LeadingDoc"];

    /// <summary>A result count and nothing else — the tool-surface path, where the subject asks for a search
    /// and every other knob, the corpus included, is the engine's own.</summary>
    public static QlnRequest Limited(int limit) => new(AxesWire.Limited(limit), TextShape: null);

    /// <summary>A catalog recipe as this engine's request, or a refusal naming what it cannot serve.
    /// <para>
    /// <b>What it CAN serve, as of 2026-08-17:</b> the channels, both weights, the rank constant, the fusion
    /// ALGORITHM and its normalization, the reranker and its pool, the result limit, and the corpus text
    /// shape. Fusion and normalization arrived that morning in
    /// <c>dew_flow_rag_qln · todo/PLAN_search_variant_axes.md</c> step 1; before it, a <c>wsum</c> recipe was
    /// refused here because the engine would have accepted the request, ignored the field, and served rank
    /// fusion under a record that said otherwise.
    /// </para>
    /// <para>
    /// <b>What it still cannot:</b> chunk size and embed model. Those are not request axes at all — the
    /// engine derives them from its own configured recipe — so they are neither sent nor verifiable here, and
    /// what a run records instead is the COLLECTION that answered. Making that collection provably the
    /// recipe's is the index-preparation step (`todo/PLAN_variant_matrix.md` §3.4 step 2), and until it lands
    /// a variant that differs only in chunk size is a variant this engine cannot distinguish.
    /// </para></summary>
    public static Outcome<QlnRequest> From(VariantDefinition.RetrievalRecipe recipe)
    {
        var refusal = Refuse(recipe);

        return refusal.Length > 0
            ? Outcome<QlnRequest>.Failure(refusal)
            : Outcome<QlnRequest>.Success(new QlnRequest(AxesWire.From(recipe), Shape(recipe.Corpus.TextShape)));
    }

    /// <summary>What this run asked for, as named values — the axes plus the corpus shape, since both are
    /// axes of the MEASUREMENT even though they ride at different levels of the request.</summary>
    public EngineAxes Asked =>
        new([.. Axes.Axes.Values, .. TextShape is { } shape ? new[] { new Axis("textShape", shape) } : []]);

    private static string Refuse(VariantDefinition.RetrievalRecipe recipe) =>
        (recipe.Engine == EngineKind.Qln, Shape(recipe.Corpus.TextShape)) switch
        {
            (false, _) =>
                $"this adapter serves {EngineKind.Qln}, and the recipe names {recipe.Engine} — an engine that "
                + "measured another engine's recipe would label the result with the recipe and the numbers with itself",
            (_, null) =>
                $"this engine has no corpus text shape called '{recipe.Corpus.TextShape}' — it serves "
                + $"{string.Join(", ", TextShapes)}. A shape it does not have would come back a 400, and a "
                + "refusal that names the legal ones is worth more than that",
            _ => string.Empty,
        };

    /// <summary>The engine's own spelling of a shape name, or nothing when it has no such shape. Matched
    /// case-insensitively: the catalog is written by hand and <c>graphheader</c> is the same corpus.</summary>
    private static string? Shape(string textShape) =>
        TextShapes.FirstOrDefault(s => string.Equals(s, textShape, StringComparison.OrdinalIgnoreCase));
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

    /// <summary>Which algorithm fuses the channels — this engine knows <c>rrf</c> and <c>wsum</c>.</summary>
    [JsonPropertyName("fusion")] public string? Fusion { get; init; }

    /// <summary>How a weighted sum makes the channels comparable. Sent only UNDER a weighted sum: this
    /// system's "none" means no normalization is applied, which under rank fusion is simply true and is
    /// expressed by the engine ignoring the field — not by sending it a value it does not have, which its
    /// axes contract would refuse.</summary>
    [JsonPropertyName("wsumNorm")] public string? WsumNorm { get; init; }

    /// <summary>A result count and nothing else — the tool-surface path, where the subject asks for a search
    /// and every other knob is the engine's own.</summary>
    public static AxesWire Limited(int limit) => new() { Limit = limit };

    /// <summary>A catalog recipe as this engine's query-time axes.
    /// <para>
    /// No refusal here any more: what this engine cannot serve is decided by <see cref="QlnRequest.From"/>,
    /// which owns the whole request. Every axis below has existed since
    /// <c>dew_flow_rag_qln · PLAN_search_variant_axes</c> step 1 landed on 2026-08-17 — the fusion algorithm
    /// and its normalization are the two that were refused before it.
    /// </para></summary>
    public static AxesWire From(VariantDefinition.RetrievalRecipe recipe) => new()
    {
        Limit = recipe.Limit,
        Dense = recipe.Channels is RetrievalChannels.Dense or RetrievalChannels.Hybrid,
        Sparse = recipe.Channels is RetrievalChannels.Sparse or RetrievalChannels.Hybrid,
        DenseWeight = recipe.Fusion.DenseWeight,
        SparseWeight = recipe.Fusion.SparseWeight,
        RrfK = recipe.Fusion.K,
        Rerank = recipe.Rerank.Enabled,
        RerankPool = recipe.Rerank.Enabled ? recipe.Rerank.Pool : null,
        Fusion = recipe.Fusion.Mode,
        WsumNorm = recipe.Fusion.Mode == FusionSpec.WeightedSum ? recipe.Fusion.Normalization : null,
    };

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
        .. Text("fusion", Fusion),
        .. Text("wsumNorm", WsumNorm),
    ]);

    /// <summary>A named axis whose value is already text, or nothing when it was not set.</summary>
    private static IEnumerable<Axis> Text(string name, string? value) =>
        value is { Length: > 0 } set ? [new Axis(name, set)] : [];

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

/// <summary>The index-state response, pinned to <c>Platform.Contracts.IndexStateVm</c> as it stood on
/// 2026-08-17. Every field is DERIVED there — the collection name from the same resolver the pass and the
/// search use, the counts from the vector store, the stamp from the newest succeeded pass — so this read
/// cannot describe a collection nobody would write to.</summary>
internal sealed record IndexStateWire
{
    [JsonPropertyName("collection")] public string Collection { get; init; } = string.Empty;

    [JsonPropertyName("exists")] public bool Exists { get; init; }

    [JsonPropertyName("points")] public long Points { get; init; }

    [JsonPropertyName("embedModel")] public string EmbedModel { get; init; } = string.Empty;

    [JsonPropertyName("textShape")] public string TextShape { get; init; } = string.Empty;

    [JsonPropertyName("windowTokens")] public int WindowTokens { get; init; }

    [JsonPropertyName("overlapTokens")] public int OverlapTokens { get; init; }

    [JsonPropertyName("tokenizer")] public string Tokenizer { get; init; } = string.Empty;

    [JsonPropertyName("fingerprint")] public string Fingerprint { get; init; } = string.Empty;

    /// <summary>Empty for an index built before the engine began stamping its commit. Not a different
    /// commit — a thing nobody knows, which is its own state.</summary>
    [JsonPropertyName("indexedCommit")] public string IndexedCommit { get; init; } = string.Empty;

    [JsonPropertyName("workingTreeDirty")] public bool WorkingTreeDirty { get; init; }

    /// <summary>Absent when no pass this engine remembers wrote the collection.</summary>
    [JsonPropertyName("passStatus")] public int? PassStatus { get; init; }

    /// <summary>What the engine computed on, as <c>host/provider/device</c>. Absent from every version of
    /// qln shipped so far, which reads as <c>not declared</c> — the honest state rather than a gap, since an
    /// engine that says nothing has not agreed with anything.</summary>
    [JsonPropertyName("backend")] public string Backend { get; init; } = string.Empty;
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
