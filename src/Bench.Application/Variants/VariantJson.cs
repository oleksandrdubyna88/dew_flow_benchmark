using System.Text.Json;
using System.Text.Json.Serialization;
using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Variants;

namespace Bench.Application.Variants;

/// <summary>A definition's wire shape — what a catalog row stores and a CLI accepts.
/// <para>
/// <b>An axis this build does not know is REFUSED, never dropped.</b> Silently ignoring it would run the
/// cell under a configuration nobody asked for and then label the result with the name that asked for the
/// other one. It is the same discipline the telemetry contract applies to an unknown schema version, and
/// it costs one serializer option: <see cref="JsonUnmappedMemberHandling.Disallow"/>.
/// </para></summary>
public static class VariantJson
{
    /// <summary>
    /// Written in camelCase because this string IS the published artefact: it is stored verbatim in the
    /// catalog row and goes out with the results, so it has to read as the contract that documents it.
    /// Read case-insensitively so a row written under an earlier naming still resolves — a definition
    /// that stops parsing is a variant every historical result names and nothing can explain.
    /// </summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private const string NoRetrievalEngine = "noretrieval";

    public static Outcome<VariantDefinition> Read(string json)
    {
        try
        {
            var wire = JsonSerializer.Deserialize<DefinitionWire>(json, Options);

            return wire is null
                ? Outcome<VariantDefinition>.Failure("the definition is empty")
                : FromWire(wire);
        }
        catch (JsonException ex)
        {
            return Outcome<VariantDefinition>.Failure($"the definition could not be read — {ex.Message}");
        }
    }

    public static string Write(VariantDefinition definition) =>
        JsonSerializer.Serialize(ToWire(definition), Options);

    private static Outcome<VariantDefinition> FromWire(DefinitionWire wire)
    {
        var engine = (wire.Engine ?? string.Empty).Trim();

        if (string.Equals(engine, NoRetrievalEngine, StringComparison.OrdinalIgnoreCase))
        {
            return Baseline(wire);
        }

        return Enum.TryParse<EngineKind>(engine, ignoreCase: true, out var kind)
            ? Recipe(wire, kind)
            : Outcome<VariantDefinition>.Failure(
                $"unknown engine '{engine}' — this build knows {string.Join(", ", Enum.GetNames<EngineKind>()).ToLowerInvariant()}");
    }

    /// <summary>A baseline carrying retrieval fields is refused rather than served with them ignored — an
    /// ignored field is a configuration somebody wrote down and nothing honoured.</summary>
    private static Outcome<VariantDefinition> Baseline(DefinitionWire wire) =>
        wire is { Fusion: null, Corpus: null, Rerank: null, Channels: null, Limit: null }
            ? Outcome<VariantDefinition>.Success(VariantDefinition.NoRetrieval)
            : Outcome<VariantDefinition>.Failure(
                "the baseline performs no retrieval, so it may not carry channels, fusion, a corpus, a reranker or a limit");

    private static Outcome<VariantDefinition> Recipe(DefinitionWire wire, EngineKind engine)
    {
        if (wire.Fusion is not { } fusion || wire.Corpus is not { } corpus || wire.Rerank is not { } rerank)
        {
            return Outcome<VariantDefinition>.Failure(
                "a retrieval definition needs fusion, corpus and rerank — a missing one is a refusal, never a default");
        }

        return Channels(wire.Channels).Match(
            channels => FusionSpec.Parse(fusion.Mode, fusion.K, fusion.DenseWeight, fusion.SparseWeight, fusion.Norm).Match(
                fused => CorpusSpec.Parse(corpus.TextShape, corpus.ChunkTokens, corpus.EmbedModel).Match(
                    corpora => Rerank(rerank).Match(
                        reranked => VariantDefinition.Retrieval(
                            engine, channels, fused, corpora, reranked, wire.Limit ?? 0),
                        Outcome<VariantDefinition>.Failure),
                    Outcome<VariantDefinition>.Failure),
                Outcome<VariantDefinition>.Failure),
            Outcome<VariantDefinition>.Failure);
    }

    private static Outcome<RetrievalChannels> Channels(string? value) =>
        Enum.TryParse<RetrievalChannels>((value ?? string.Empty).Trim(), ignoreCase: true, out var parsed)
            ? Outcome<RetrievalChannels>.Success(parsed)
            : Outcome<RetrievalChannels>.Failure(
                $"unknown channels '{value}' — this build knows {string.Join(", ", Enum.GetNames<RetrievalChannels>()).ToLowerInvariant()}");

    private static Outcome<RerankSpec> Rerank(RerankWire wire) =>
        wire.Enabled ? RerankSpec.Pooled(wire.Pool) : Outcome<RerankSpec>.Success(RerankSpec.Off);

    private static DefinitionWire ToWire(VariantDefinition definition) =>
        definition switch
        {
            VariantDefinition.RetrievalRecipe recipe => new DefinitionWire
            {
                Engine = recipe.Engine.ToString().ToLowerInvariant(),
                Channels = recipe.Channels.ToString().ToLowerInvariant(),
                Fusion = new FusionWire(recipe.Fusion.Mode, recipe.Fusion.K, recipe.Fusion.DenseWeight, recipe.Fusion.SparseWeight, recipe.Fusion.Normalization),
                Corpus = new CorpusWire(recipe.Corpus.TextShape, recipe.Corpus.ChunkTokens, recipe.Corpus.EmbedModel),
                Rerank = new RerankWire(recipe.Rerank.Enabled, recipe.Rerank.Pool),
                Limit = recipe.Limit,
            },
            _ => new DefinitionWire { Engine = NoRetrievalEngine },
        };

    private sealed record DefinitionWire
    {
        public string? Engine { get; init; }

        public string? Channels { get; init; }

        public FusionWire? Fusion { get; init; }

        public CorpusWire? Corpus { get; init; }

        public RerankWire? Rerank { get; init; }

        public int? Limit { get; init; }
    }

    private sealed record FusionWire(string Mode, int K, double DenseWeight, double SparseWeight, string Norm);

    private sealed record CorpusWire(string TextShape, int ChunkTokens, string EmbedModel);

    private sealed record RerankWire(bool Enabled, int Pool);
}
