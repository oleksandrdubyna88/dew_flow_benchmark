using System.Text.Json;
using Bench.Application;
using Bench.Domain;
using Bench.Domain.Engines;
using Bench.Domain.Runs;
using Bench.Domain.Trace;

namespace Bench.Infrastructure.Engines;

/// <summary>The QLN retrieval engine as a tool SURFACE a subject works.
/// <para>
/// <b>It offers file tools as well as search, by composing the baseline engine.</b> Not a convenience:
/// the measured retrieval arm made 63 file reads beside its 39 searches, and an engine that offered
/// search ALONE would be measuring a subject working with one hand tied — against a baseline that has
/// both. The comparison "retrieval against no retrieval" is only honest if the retrieval arm is a
/// superset.
/// </para>
/// <para>
/// The wire itself lives in <see cref="QlnRetriever"/>, which this class composes and which also serves the
/// single-shot lane directly. One round trip, one funnel path, whichever lane asked — and the single-shot
/// lane does not have to be handed a filesystem root it would never read.
/// </para></summary>
public sealed class QlnEngine(QlnRetriever retrieval, IEngine files) : IEngine
{
    public const string SearchCode = "rag_search_code";

    /// <summary>Hits a single search returns. Deliberately not the engine's own default: a subject that
    /// gets 150 members per call spends its context on the tool rather than on the question, and the
    /// result count is a measured axis rather than a taste.</summary>
    private const int DefaultLimit = 20;

    /// <summary>The shape every existing caller and test builds: an endpoint, a project, a branch, the file
    /// tools and a funnel sink. Kept so that splitting the wire out did not become a change to everything
    /// that constructs an engine.</summary>
    public QlnEngine(
        HttpClient http,
        Guid projectId,
        string branch,
        IEngine files,
        IFunnelSink funnels,
        string engineVersion = "",
        string indexFingerprint = "")
        : this(new QlnRetriever(http, projectId, branch, funnels, engineVersion, indexFingerprint), files)
    {
    }

    public EngineRef Describe => retrieval.Describe;

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
        var probe = await retrieval.SearchAsync(
            "does this index answer at all", AxesWire.Limited(1), cancellationToken);

        return probe.Match(
            response => Outcome<string>.Success($"{Describe.Canonical}|collection={response.Wire.Collection}"),
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

        var result = await retrieval.SearchAsync(
            query, AxesWire.Limited(Number(args, "limit", DefaultLimit)), cancellationToken);

        return result.Match(response => Render(response.Wire), ToolAnswer.Failure);
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
