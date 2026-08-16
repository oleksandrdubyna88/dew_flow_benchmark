using System.Globalization;
using Bench.Domain.Runs;

namespace Bench.Domain.Variants;

/// <summary>Which channels admit candidates. Dense-only and sparse-only are not degenerate hybrids —
/// they are the comparison the matrix exists to run, so they are named rather than expressed as two
/// booleans a caller can leave both false.</summary>
public enum RetrievalChannels
{
    Dense,
    Sparse,
    Hybrid,
}

/// <summary>How ranked channels are combined into one list.</summary>
public sealed record FusionSpec
{
    /// <summary>Reciprocal rank fusion — reads ranks only, so incomparable score scales cannot hurt it.</summary>
    public const string ReciprocalRank = "rrf";

    /// <summary>Convex combination of the channels' own scores. Needs a normalization: see <see cref="Parse"/>.</summary>
    public const string WeightedSum = "wsum";

    public const string NoNormalization = "none";

    public const string MinMax = "minmax";

    public static IReadOnlyList<string> Modes => [ReciprocalRank, WeightedSum];

    public static IReadOnlyList<string> Normalizations => [NoNormalization, MinMax];

    private FusionSpec(string mode, int k, double denseWeight, double sparseWeight, string normalization)
    {
        Mode = mode;
        K = k;
        DenseWeight = denseWeight;
        SparseWeight = sparseWeight;
        Normalization = normalization;
    }

    public string Mode { get; }

    /// <summary>The rank constant. Meaningless under <see cref="WeightedSum"/> and carried anyway, because
    /// a variant that switches mode and back must produce the same identity it had before.</summary>
    public int K { get; }

    public double DenseWeight { get; }

    public double SparseWeight { get; }

    public string Normalization { get; }

    /// <summary>The shipped default: rank fusion at k = 60, both channels equal.</summary>
    public static Outcome<FusionSpec> Rrf(int k) =>
        Parse(ReciprocalRank, k, 1.0, 1.0, NoNormalization);

    /// <summary>
    /// Every refusal names the legal values rather than falling back to one. The
    /// <see cref="WeightedSum"/>-without-normalization case is the one worth stating: a dense channel
    /// answers bounded cosine and a sparse one an unbounded dot product, so summing them raw is
    /// arithmetic over two different units — the reason rank fusion was the first implementation.
    /// </summary>
    public static Outcome<FusionSpec> Parse(
        string? mode, int k, double denseWeight, double sparseWeight, string? normalization)
    {
        var refusal = Refuse(mode, k, denseWeight, sparseWeight, normalization);

        return refusal.Length > 0
            ? Outcome<FusionSpec>.Failure(refusal)
            : Outcome<FusionSpec>.Success(new FusionSpec(mode!, k, denseWeight, sparseWeight, normalization!));
    }

    public string Canonical =>
        $"fusion={Mode},k={K},wd={Weight(DenseWeight)},ws={Weight(SparseWeight)},norm={Normalization}";

    private static string Refuse(string? mode, int k, double denseWeight, double sparseWeight, string? normalization) =>
        (Modes.Contains(mode), Normalizations.Contains(normalization), k, denseWeight + sparseWeight) switch
        {
            (false, _, _, _) => $"unknown fusion mode '{mode}' — this build knows {string.Join(", ", Modes)}",
            (_, false, _, _) => $"unknown normalization '{normalization}' — this build knows {string.Join(", ", Normalizations)}",
            (_, _, < 1, _) => $"the rank constant k must be at least 1, got {k}",
            (_, _, _, <= 0) => "both channel weights are zero — that admits nothing; weight at least one channel",
            _ => IncomparableScales(mode, normalization),
        };

    private static string IncomparableScales(string? mode, string? normalization) =>
        mode == WeightedSum && normalization == NoNormalization
            ? "a weighted sum needs a normalization: the dense channel answers bounded cosine and the sparse one an "
              + $"unbounded dot product, so summing them raw adds two different units. Use {MinMax}, or {ReciprocalRank}"
            : string.Empty;

    private static string Weight(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}

/// <summary>Which corpus answers — the index-time half of a variant, selected per request because the
/// variants coexist as separate collections rather than replacing one another.</summary>
public sealed record CorpusSpec
{
    private CorpusSpec(string textShape, int chunkTokens, string embedModel)
    {
        TextShape = textShape;
        ChunkTokens = chunkTokens;
        EmbedModel = embedModel;
    }

    public string TextShape { get; }

    public int ChunkTokens { get; }

    public string EmbedModel { get; }

    /// <summary>An unset embed model is a refusal, never a default — the same rule
    /// <see cref="ModelRef"/> enforces, and for the same near-miss.</summary>
    public static Outcome<CorpusSpec> Parse(string? textShape, int chunkTokens, string? embedModel)
    {
        var shape = (textShape ?? string.Empty).Trim();
        var model = (embedModel ?? string.Empty).Trim();

        var refusal = (shape.Length, chunkTokens, model.Length) switch
        {
            (0, _, _) => "the corpus text shape is unset — name the shape the pass indexed",
            (_, < 1, _) => $"chunk tokens must be at least 1, got {chunkTokens}",
            (_, _, 0) => "the embed model is unset — that is a refusal, never a fallback to whichever model is loaded",
            _ => string.Empty,
        };

        return refusal.Length > 0
            ? Outcome<CorpusSpec>.Failure(refusal)
            : Outcome<CorpusSpec>.Success(new CorpusSpec(shape, chunkTokens, model));
    }

    public string Canonical => $"corpus={TextShape}/{ChunkTokens}/{EmbedModel}";
}

/// <summary>Whether a cross-encoder re-scores the fused pool, and how deep.</summary>
public sealed record RerankSpec
{
    private RerankSpec(bool enabled, int pool)
    {
        Enabled = enabled;
        Pool = pool;
    }

    public bool Enabled { get; }

    public int Pool { get; }

    public static RerankSpec Off { get; } = new(false, 0);

    public static Outcome<RerankSpec> Pooled(int pool) =>
        pool < 1
            ? Outcome<RerankSpec>.Failure($"a reranker that is on needs a pool of at least 1, got {pool}")
            : Outcome<RerankSpec>.Success(new RerankSpec(true, pool));

    public string Canonical => Enabled ? $"rerank={Pool}" : "rerank=off";
}

/// <summary>One named retrieval configuration, as a value.
/// <para>
/// Two shapes, closed: a retrieval recipe, and the baseline that performs no retrieval at all. The
/// baseline is a case rather than a recipe whose fields are ignored — a definition carrying a fusion
/// mode nothing reads is a definition that will eventually be read.
/// </para></summary>
public abstract record VariantDefinition
{
    private VariantDefinition() { }

    /// <summary>The retrieval recipe. Constructed through <see cref="Retrieval"/>, which validates.</summary>
    public sealed record RetrievalRecipe : VariantDefinition
    {
        internal RetrievalRecipe(
            EngineKind engine,
            RetrievalChannels channels,
            FusionSpec fusion,
            CorpusSpec corpus,
            RerankSpec rerank,
            int limit)
        {
            Engine = engine;
            Channels = channels;
            Fusion = fusion;
            Corpus = corpus;
            Rerank = rerank;
            Limit = limit;
        }

        public EngineKind Engine { get; }

        public RetrievalChannels Channels { get; }

        public FusionSpec Fusion { get; }

        public CorpusSpec Corpus { get; }

        public RerankSpec Rerank { get; }

        public int Limit { get; }

        public override string Canonical => string.Join(
            '|',
            [
                $"engine={Engine}",
                $"channels={Channels}",
                Fusion.Canonical,
                Corpus.Canonical,
                Rerank.Canonical,
                $"limit={Limit}",
            ]);
    }

    /// <summary>No retrieval at all — the arm every retrieval claim is measured against.</summary>
    public sealed record Baseline : VariantDefinition
    {
        internal Baseline() { }

        public override string Canonical => $"engine={EngineKind.NoRetrieval}";
    }

    public static VariantDefinition NoRetrieval { get; } = new Baseline();

    public static Outcome<VariantDefinition> Retrieval(
        EngineKind engine,
        RetrievalChannels channels,
        FusionSpec fusion,
        CorpusSpec corpus,
        RerankSpec rerank,
        int limit)
    {
        if (engine == EngineKind.NoRetrieval)
        {
            return Outcome<VariantDefinition>.Failure(
                $"{EngineKind.NoRetrieval} is not a retrieval recipe — use the baseline definition, which carries no "
                + "channels, fusion or corpus because it performs no retrieval");
        }

        return limit < 1
            ? Outcome<VariantDefinition>.Failure($"the result limit must be at least 1, got {limit}")
            : Outcome<VariantDefinition>.Success(
                new RetrievalRecipe(engine, channels, fusion, corpus, rerank, limit));
    }

    /// <summary>The whole recipe as one string, in a fixed order. This is what is hashed, so a change to
    /// its shape changes every identity — which is why the order is fixed rather than reflected.</summary>
    public abstract string Canonical { get; }

    /// <summary>The recipe's identity. Two rows with the same hash are the same configuration under two
    /// names, which is a duplicate a catalog can detect rather than a coincidence a report has to explain.</summary>
    public string Hash => StableHash.Of(Canonical);
}
