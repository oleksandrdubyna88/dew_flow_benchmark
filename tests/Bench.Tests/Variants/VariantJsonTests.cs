using Bench.Application.Variants;
using Bench.Domain.Retrieval;
using Bench.Domain.Runs;
using Bench.Domain.Variants;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Variants;

/// <summary>The definition's wire shape — the form a catalog row is stored in and a CLI accepts.
/// <para>
/// The load-bearing rule is the last test: an axis this build does not know is REFUSED, never dropped.
/// Silently ignoring it would run the cell under a configuration nobody asked for and label the result
/// with the name that asked for the other one — the same failure the telemetry contract refuses a line
/// for, applied to a configuration instead of a record.
/// </para></summary>
public sealed class VariantJsonTests
{
    [Fact]
    public void A_retrieval_definition_round_trips()
    {
        var definition = VariantDefinitionTests.Retrieval(channels: RetrievalChannels.Sparse).Ok();

        var restored = VariantJson.Read(VariantJson.Write(definition)).Ok();

        restored.Should().Be(definition);
        restored.Hash.Should().Be(definition.Hash, "a round trip that changes the hash changes the identity of every result naming it");
    }

    [Fact]
    public void The_baseline_round_trips_without_carrying_retrieval_fields_it_does_not_have()
    {
        var json = VariantJson.Write(VariantDefinition.NoRetrieval);

        json.Should().NotContain("fusion");
        VariantJson.Read(json).Ok().Should().Be(VariantDefinition.NoRetrieval);
    }

    [Fact]
    public void The_written_shape_is_the_documented_one_because_this_row_is_the_published_artefact()
    {
        var json = VariantJson.Write(VariantDefinitionTests.Retrieval().Ok());

        json.Should().Contain("\"textShape\"").And.Contain("\"chunkTokens\"").And.Contain("\"limit\"");
        json.Should().NotContain("\"TextShape\"", "the definition is stored verbatim and published; it has to read as its contract");
    }

    [Fact]
    public void A_definition_written_before_the_naming_was_fixed_still_reads()
    {
        var pascal = """
            {"Engine":"qln","Channels":"hybrid",
             "Fusion":{"Mode":"rrf","K":60,"DenseWeight":1,"SparseWeight":1,"Norm":"none"},
             "Corpus":{"TextShape":"src","ChunkTokens":256,"EmbedModel":"bge-m3"},
             "Rerank":{"Enabled":true,"Pool":50},"Limit":20}
            """;

        VariantJson.Read(pascal).Ok().Should().Be(VariantDefinitionTests.Retrieval().Ok());
    }

    [Fact]
    public void An_unknown_axis_is_refused_rather_than_ignored()
    {
        var json = """
            {"engine":"qln","channels":"hybrid",
             "fusion":{"mode":"rrf","k":60,"denseWeight":1,"sparseWeight":1,"norm":"none"},
             "corpus":{"textShape":"src","chunkTokens":256,"embedModel":"bge-m3"},
             "rerank":{"enabled":true,"pool":50},"limit":20,
             "graphExpansion":true}
            """;

        VariantJson.Read(json).Reason().Should().Contain("graphExpansion");
    }

    [Fact]
    public void An_unknown_engine_is_refused_naming_what_is_known()
    {
        VariantJson.Read("""{"engine":"vespa"}""").Reason().Should().Contain("vespa");
    }

    [Fact]
    public void A_retrieval_definition_missing_its_corpus_is_refused_rather_than_defaulted()
    {
        var json = """
            {"engine":"qln","channels":"hybrid",
             "fusion":{"mode":"rrf","k":60,"denseWeight":1,"sparseWeight":1,"norm":"none"},
             "rerank":{"enabled":true,"pool":50},"limit":20}
            """;

        VariantJson.Read(json).Reason().Should().Contain("corpus");
    }

    [Fact]
    public void Malformed_json_is_a_refusal_not_an_exception()
    {
        VariantJson.Read("{not json").Reason().Should().NotBeEmpty();
    }

    [Fact]
    public void A_variant_that_names_no_arm_stores_no_backend_field_at_all()
    {
        var written = VariantJson.Write(VariantDefinitionTests.Retrieval().Ok());

        // Absent, not empty. This row is published with the results, and a row written before the axis
        // existed must stay byte-identical — an added `"backend":""` would rewrite the whole catalog's
        // stored shape on the day this shipped.
        written.Should().NotContain("backend");
    }

    [Fact]
    public void The_arm_a_variant_measures_survives_the_catalog()
    {
        var recipe = (VariantDefinition.RetrievalRecipe)VariantDefinitionTests.Retrieval().Ok();
        var onWsl = recipe.On(ComputeBackend.Parse("wsl/migraphx/R9700").Ok());

        var written = VariantJson.Write(onWsl);
        var read = (VariantDefinition.RetrievalRecipe)VariantJson.Read(written).Ok();

        written.Should().Contain("\"backend\":\"wsl/migraphx/R9700\"");
        read.Backend.Describe.Should().Be("wsl/migraphx/R9700",
            "an axis that cannot be stored is not an axis — the catalog is where a variant lives");
        read.Hash.Should().Be(onWsl.Hash, "and the recipe it comes back as must be the one that was hashed");
    }

    [Fact]
    public void A_stored_arm_this_build_cannot_read_is_REFUSED_rather_than_dropped()
    {
        var json = """
            {"engine":"qln","channels":"hybrid",
             "fusion":{"mode":"rrf","k":60,"denseWeight":1,"sparseWeight":1,"norm":"none"},
             "corpus":{"textShape":"src","chunkTokens":256,"embedModel":"bge-m3"},
             "rerank":{"enabled":true,"pool":50},"limit":20,"backend":"wsl-migraphx"}
            """;

        // The opposite of how an ENGINE'S echo is read. This string is a configuration somebody wrote down,
        // and the catalog's standing rule is that an axis this build cannot honour fails by name rather than
        // running as something else; an unreadable echo is a fact about the engine and reads as not declared.
        VariantJson.Read(json).Reason().Should().Contain("host/provider/device");
    }
}
