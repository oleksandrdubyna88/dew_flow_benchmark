using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Retrieval;
using Bench.Domain.Variants;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Retrieval;

/// <summary>The vector width as a corpus axis.
///
/// <para><b>Why it had to exist.</b> A corpus was identified by shape, chunk size and the embedder's NAME —
/// and a name is the one thing about an embedder that does not identify it. The two sides legitimately spell
/// it differently (<c>bge-m3</c> in a catalog row against <c>BAAI/bge-m3 (dense, FP32)</c> from the engine),
/// so the comparison normalises across that gap by design. Width does not normalise: two models whose names
/// match after normalisation and whose vectors are 1024 and 768 wide are two corpora, and nothing in the
/// benchmark could say so.</para>
///
/// <para><b>Three states, and the hash is the reason.</b> A catalog row is immutable — added and retired,
/// never edited — so its hash is its identity. A width folded into the canonical form unconditionally would
/// silently re-identify every variant already published against one, which is the regression
/// <c>todo/PLAN_corpus_axis_integrity.md</c> names in its own test plan.</para>
/// </summary>
public sealed class EmbedDimensionsTests
{
    [Fact]
    public void A_variant_that_declares_no_width_hashes_exactly_as_it_did()
    {
        var before = "corpus=GraphHeader/256/bge-m3";

        var spec = CorpusSpec.Parse("GraphHeader", 256, "bge-m3").Ok();

        // The whole reason the width is conditional. Every row in the live catalog was written before this
        // axis existed, and a moved hash would re-identify work already published against it.
        spec.Canonical.Should().Be(before);
        spec.Dimensions.Declared.Should().BeFalse();
    }

    [Fact]
    public void A_declared_width_becomes_part_of_the_identity()
    {
        var narrow = CorpusSpec.Parse("GraphHeader", 256, "bge-m3", dimensions: 768).Ok();
        var wide = CorpusSpec.Parse("GraphHeader", 256, "bge-m3", dimensions: 1024).Ok();

        // Two corpora, and before this they were one row: same shape, same chunk size, same model name.
        narrow.Canonical.Should().NotBe(wide.Canonical);
        wide.Canonical.Should().EndWith("/dim=1024");
    }

    [Fact]
    public void A_width_of_zero_is_NOT_DECLARED_rather_than_a_zero_wide_corpus()
    {
        // Zero is not a narrow corpus, it is an absent fact — and the same reading a negative gets, exactly
        // as BackendDeclaration.Read treats a value it cannot use.
        EmbedDimensions.Of(0).Declared.Should().BeFalse();
        EmbedDimensions.Of(-1).Declared.Should().BeFalse();
        EmbedDimensions.Of(1024).Declared.Should().BeTrue();
    }

    [Fact]
    public void Two_declared_widths_that_differ_block_the_run()
    {
        var refusal = Engine(dimensions: 768).Refuse(Recipe(dimensions: 1024));

        // The case the model-name check structurally cannot see: names that normalise to the same thing over
        // vectors that cannot be the same embedder's.
        refusal.Should().Contain("768").And.Contain("1024");
        refusal.Should().Contain("two embedders");
    }

    [Fact]
    public void An_engine_that_reports_no_width_has_not_DISAGREED()
    {
        // Every engine build that predates this reports nothing. Blocking there would refuse every correct
        // recipe against every engine not yet taught to answer — the three-state rule, doing its job.
        Engine(dimensions: 0).Refuse(Recipe(dimensions: 1024)).Should().BeEmpty();
        Engine(dimensions: 1024).Refuse(Recipe(dimensions: 0)).Should().BeEmpty();
    }

    [Fact]
    public void Matching_widths_pass_in_silence()
    {
        Engine(dimensions: 1024).Refuse(Recipe(dimensions: 1024)).Should().BeEmpty();
    }

    [Fact]
    public void An_unverified_width_is_a_WARNING_on_a_run_that_otherwise_measures()
    {
        var state = State(dimensions: 0);
        var recipe = Recipe(dimensions: 1024);

        var approved = IndexReadiness.Of(state, Wrap(recipe), Target, new ReadinessAllowances(true, true));

        // It measures — no allowance gates this axis, because an undeclared width blocks nothing and there is
        // therefore no flag to forget. What the reader gets is the name of the axis nobody checked.
        approved.Should().BeOfType<Outcome<IndexApproval>.Ok>()
            .Which.Value.Warning.Should().Contain("UNVERIFIED").And.Contain("model NAME does not settle it");
    }

    [Fact]
    public void A_mismatched_width_is_refused_after_the_shape_and_the_chunk_size()
    {
        var state = State(dimensions: 768);

        var refused = IndexReadiness.Of(state, Wrap(Recipe(dimensions: 1024)), Target, new ReadinessAllowances(true, true));

        // Order matters: an operator acts on the FIRST line they read, and the three differences are not
        // equally specific. The width is the narrowest of them, so it comes last.
        refused.Should().BeOfType<Outcome<IndexApproval>.Fail>()
            .Which.Reason.Should().Contain("two embedders");
    }

    // ---- scaffolding -------------------------------------------------------------------------------

    private static readonly Bench.Domain.Targets.CommitSha Target =
        Bench.Domain.Targets.CommitSha.Parse(new string('c', 40)).Ok();

    private static CorpusIdentity Engine(int dimensions) =>
        new("GraphHeader", 256, 64, "BAAI/bge-m3 (dense, FP32)", "bge", EmbedDimensions.Of(dimensions));

    private static CorpusSpec Recipe(int dimensions) =>
        CorpusSpec.Parse("GraphHeader", 256, "bge-m3", dimensions).Ok();

    private static IndexState State(int dimensions) => new(
        "code_ab12",
        Exists: true,
        Points: 100,
        Engine(dimensions),
        "fp",
        IndexCommit.Of(Target),
        WorkingTreeDirty: false,
        PassSucceeded: true);

    private static VariantDefinition.RetrievalRecipe Wrap(CorpusSpec corpus) =>
        (VariantDefinition.RetrievalRecipe)VariantDefinition.Retrieval(
            EngineKind.Qln,
            RetrievalChannels.Hybrid,
            FusionSpec.Rrf(60).Ok(),
            corpus,
            RerankSpec.Off,
            limit: 5).Ok();
}
