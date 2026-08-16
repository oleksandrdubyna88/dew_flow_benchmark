using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Variants;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Variants;

/// <summary>What a retrieval variant is allowed to be.
/// <para>
/// Every refusal below exists because the alternative — a silent default — produces a measurement that
/// looks configured and is not. The matrix multiplies these definitions by every question, subject and
/// repeat, so a definition that quietly means something other than it says is wrong thousands of times.
/// </para></summary>
public sealed class VariantDefinitionTests
{
    [Fact]
    public void An_unknown_fusion_mode_is_refused_naming_the_legal_ones()
    {
        var refusal = FusionSpec.Parse("borda", 60, 1, 1, FusionSpec.NoNormalization).Reason();

        refusal.Should().Contain("borda").And.Contain(FusionSpec.ReciprocalRank).And.Contain(FusionSpec.WeightedSum);
    }

    [Fact]
    public void An_unknown_normalization_is_refused_naming_the_legal_ones()
    {
        FusionSpec.Parse(FusionSpec.WeightedSum, 60, 1, 1, "zscore")
            .Reason().Should().Contain("zscore").And.Contain(FusionSpec.MinMax);
    }

    [Fact]
    public void Weighted_sum_without_normalization_is_refused_because_the_channel_scales_are_incomparable()
    {
        FusionSpec.Parse(FusionSpec.WeightedSum, 60, 1, 1, FusionSpec.NoNormalization)
            .Reason().Should().Contain("normal");
    }

    [Fact]
    public void Reciprocal_rank_needs_a_positive_k()
    {
        FusionSpec.Rrf(0).Reason().Should().Contain("k");
    }

    [Fact]
    public void Two_channels_at_zero_weight_would_admit_nothing_and_are_refused()
    {
        FusionSpec.Parse(FusionSpec.ReciprocalRank, 60, 0, 0, FusionSpec.NoNormalization)
            .Reason().Should().Contain("weight");
    }

    [Fact]
    public void An_unset_embed_model_is_refused_rather_than_defaulted()
    {
        CorpusSpec.Parse("src", 256, "  ")
            .Reason().Should().Contain("embed model");
    }

    [Fact]
    public void A_chunk_size_of_zero_is_refused()
    {
        CorpusSpec.Parse("src", 0, "bge-m3").Reason().Should().Contain("chunk");
    }

    [Fact]
    public void A_reranker_that_is_on_needs_a_pool()
    {
        RerankSpec.Pooled(0).Reason().Should().Contain("pool");
    }

    [Fact]
    public void The_no_retrieval_engine_is_not_a_retrieval_definition()
    {
        Retrieval(EngineKind.NoRetrieval)
            .Reason().Should().Contain("baseline");
    }

    [Fact]
    public void A_definition_hash_is_the_recipe_so_the_same_recipe_under_two_names_is_detectable()
    {
        Retrieval().Ok().Hash.Should().Be(Retrieval().Ok().Hash);
    }

    [Fact]
    public void Two_definitions_differing_only_in_chunk_size_hash_differently()
    {
        var small = Retrieval(chunkTokens: 256).Ok();
        var large = Retrieval(chunkTokens: 512).Ok();

        large.Hash.Should().NotBe(small.Hash, "chunk size is an axis, and an axis that does not change the identity is an axis nothing can compare");
    }

    [Fact]
    public void The_baseline_and_a_retrieval_definition_never_share_a_hash()
    {
        VariantDefinition.NoRetrieval.Hash.Should().NotBe(Retrieval().Ok().Hash);
    }

    [Fact]
    public void A_limit_of_zero_would_return_nothing_and_is_refused()
    {
        Retrieval(limit: 0).Reason().Should().Contain("limit");
    }

    internal static Outcome<VariantDefinition> Retrieval(
        EngineKind engine = EngineKind.Qln,
        RetrievalChannels channels = RetrievalChannels.Hybrid,
        int chunkTokens = 256,
        int limit = 20) =>
        VariantDefinition.Retrieval(
            engine,
            channels,
            FusionSpec.Rrf(60).Ok(),
            CorpusSpec.Parse("src", chunkTokens, "bge-m3").Ok(),
            RerankSpec.Pooled(50).Ok(),
            limit);
}
