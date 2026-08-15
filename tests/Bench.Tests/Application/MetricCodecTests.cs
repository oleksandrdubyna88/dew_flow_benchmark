using Bench.Application;
using Bench.Domain.Runs;
using FluentAssertions;
using Microsoft.Extensions.AI.Evaluation;
using Xunit;

namespace Bench.Tests.Application;

/// <summary>The boundary between the adopted library's metric model and our stored shape. Both directions,
/// because encoding is how a run gets stored and decoding is how a stored run becomes a report without
/// re-running anything.</summary>
public sealed class MetricCodecTests
{
    [Fact]
    public void A_numeric_metric_survives_both_directions()
    {
        var back = RoundTrip(new NumericMetric("Anchor recall", 0.75) { Reason = "two of three" });

        back.Should().BeOfType<NumericMetric>().Which.Value.Should().Be(0.75);
        back.Reason.Should().Be("two of three");
    }

    [Fact]
    public void A_boolean_metric_survives_both_directions()
    {
        RoundTrip(new BooleanMetric("Hidden tests", true)).Should().BeOfType<BooleanMetric>()
            .Which.Value.Should().Be(true);
    }

    [Fact]
    public void A_string_metric_survives_both_directions()
    {
        RoundTrip(new StringMetric("Diagnosis", "patched the symptom")).Should().BeOfType<StringMetric>()
            .Which.Value.Should().Be("patched the symptom");
    }

    [Fact]
    public void A_boolean_aggregates_as_one_or_zero_so_a_pass_rate_and_a_score_add_up_the_same_way()
    {
        MetricCodec.Encode(new BooleanMetric("x", true)).AsNumber().Ok().Should().Be(1);
        MetricCodec.Encode(new BooleanMetric("x", false)).AsNumber().Ok().Should().Be(0);
    }

    [Fact]
    public void A_text_metric_has_no_numeric_reading_and_says_so_rather_than_returning_zero()
    {
        MetricCodec.Encode(new StringMetric("Diagnosis", "wrong file")).AsNumber()
            .Reason().Should().Contain("no numeric reading");
    }

    [Fact]
    public void A_rating_is_stored_as_a_name_so_it_survives_someone_inserting_an_enum_member()
    {
        var stored = MetricCodec.Encode(new NumericMetric("x", 1)
        {
            Interpretation = new EvaluationMetricInterpretation(EvaluationRating.Exceptional, failed: false, "found"),
        });

        stored.Rating.Should().Be("Exceptional");
        stored.Failed.Should().BeFalse();
    }

    [Fact]
    public void A_metric_with_no_interpretation_is_not_silently_marked_failed()
    {
        var stored = MetricCodec.Encode(new NumericMetric("x", 1));

        stored.Failed.Should().BeFalse("an absent judgement is not a negative one");
        stored.Rating.Should().Be("Unknown");
    }

    [Fact]
    public void Metadata_travels_in_both_directions()
    {
        var metric = new NumericMetric("Anchor recall", 1);
        metric.AddOrUpdateMetadata("anchor", "src/A.cs#A.Foo");
        metric.AddOrUpdateMetadata("hitCount", "3");

        var back = RoundTrip(metric);

        back.Metadata.Should().Contain("anchor", "src/A.cs#A.Foo").And.Contain("hitCount", "3");
    }

    [Fact]
    public void An_unreadable_rating_decodes_as_unknown_rather_than_the_first_enum_member()
    {
        var stored = StoredMetric.Numeric("x", 1, "", failed: false, rating: "SomethingFromAFutureBuild");

        MetricCodec.Decode(stored).Ok().Interpretation!.Rating.Should().Be(EvaluationRating.Unknown);
    }

    [Fact]
    public void A_whole_result_encodes_every_metric_it_carries()
    {
        var result = new EvaluationResult(
            new NumericMetric("Anchor recall", 1),
            new BooleanMetric("Answered", true));

        MetricCodec.Encode(result).Select(m => m.Name).Should().BeEquivalentTo(["Anchor recall", "Answered"]);
    }

    [Fact]
    public void A_numeric_metric_that_somehow_holds_text_refuses_a_number_instead_of_inventing_one()
    {
        var corrupt = new StoredMetric("x", MetricKind.Numeric, "not-a-number", "", false, "Unknown", new Dictionary<string, string>());

        corrupt.AsNumber().Reason().Should().Contain("claims to be numeric");
        MetricCodec.Decode(corrupt).Failed().Should().BeTrue();
    }

    private static EvaluationMetric RoundTrip(EvaluationMetric metric) =>
        MetricCodec.Decode(MetricCodec.Encode(metric)).Ok();
}
