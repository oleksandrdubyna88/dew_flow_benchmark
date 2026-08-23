using Bench.Domain;
using Bench.Domain.Retrieval;
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

    [Fact]
    public void A_recipe_that_names_no_backend_hashes_EXACTLY_as_it_did_before_that_axis_existed()
    {
        // Pinned literally, not against a sibling call: a definition is never edited because results name
        // the variant they ran under, so adding an optional axis must not relabel a single number already
        // measured. Emitting an empty `backend=` segment unconditionally would change every catalog row's
        // hash on the day this shipped, and nothing would have said so.
        Retrieval().Ok().Canonical.Should().Be(
            "engine=Qln|channels=Hybrid|fusion=rrf,k=60,wd=1,ws=1,norm=none|corpus=src/256/bge-m3|rerank=50|limit=20");
        Retrieval().Ok().Canonical.Should().NotContain("backend=");
    }

    [Fact]
    public void A_recipe_that_names_an_arm_is_a_DIFFERENT_configuration()
    {
        var plain = (VariantDefinition.RetrievalRecipe)Retrieval().Ok();
        var onWsl = plain.On(ComputeBackend.Parse("wsl/migraphx/R9700").Ok());

        onWsl.Canonical.Should().Contain("backend=wsl/migraphx/R9700");
        onWsl.Hash.Should().NotBe(plain.Hash,
            "measuring the same recipe on two sidecars is two configurations, which is the entire point of "
            + "the axis — a catalog that hashed them alike could not tell the arms apart");

        var onWindows = plain.On(ComputeBackend.Parse("windows/dml/R9700").Ok());
        onWindows.Hash.Should().NotBe(onWsl.Hash);
    }

    [Fact]
    public void Two_corpora_counted_by_DIFFERENT_TOKENIZERS_are_two_configurations()
    {
        var bge = CorpusSpec.Parse("src", 256, "bge-m3", tokenizer: "bge-m3").Ok();
        var qwen = CorpusSpec.Parse("src", 256, "bge-m3", tokenizer: "Qwen/Qwen3-Embedding-0.6B").Ok();

        // Both say 256 and the numbers are equal. Folding them into one canonical would make two different
        // amounts of text hash as one comparable configuration, which is the whole defect.
        bge.Canonical.Should().NotBe(qwen.Canonical);
    }

    [Fact]
    public void A_variant_that_names_no_tokenizer_hashes_EXACTLY_as_it_did_before_the_axis_existed()
    {
        var before = CorpusSpec.Parse("src", 256, "bge-m3").Ok();

        // The regression this axis is most likely to cause, and the one that cannot be undone: a catalog row
        // is immutable — added and retired, never edited — so a canonical that grew a segment would silently
        // re-identify every variant already measured against. The literal is pinned deliberately.
        before.Canonical.Should().Be("corpus=src/256/bge-m3");
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
