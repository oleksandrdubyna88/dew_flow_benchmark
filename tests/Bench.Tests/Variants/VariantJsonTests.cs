using Bench.Application.Variants;
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
}
