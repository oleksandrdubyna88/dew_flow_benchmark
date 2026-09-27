using System.Reflection;
using Bench.Domain;
using Bench.Domain.Gate;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate;

/// <summary>The finding is hashes only, the verdict carries its rubric, and an assessment failure is a
/// verdict case rather than a null. The first guard is STRUCTURAL: a finding with no text field cannot be
/// stored with one, whatever an entity or a page later does.</summary>
public sealed class GateFindingTests
{
    private static readonly string StrictHash = StableHash.Of("prompts/gate-assess/strict.md");
    private static readonly string LenientHash = StableHash.Of("prompts/gate-assess/lenient-worth-v1.md");

    [Fact]
    public void A_finding_has_no_string_property_other_than_its_hashes()
    {
        var strings = typeof(GateFinding).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => p.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        strings.Should().Equal(["FileHash", "TextHash"],
            "the finding's title, reasoning, fix and path quote private code — they live in the artefact store, never on this record");
    }

    [Fact]
    public void A_finding_hashes_its_text_and_file_and_keeps_neither()
    {
        var finding = GateFinding.Of(0, FindingSeverity.Major, FindingCategory.Correctness, isGating: true, line: 42,
            "Null order line dereferenced in TotalOf", "src/Orders/OrderService.cs").Ok();

        finding.TextHash.Should().Be(StableHash.Of("Null order line dereferenced in TotalOf"));
        finding.FileHash.Should().Be(StableHash.Of("src/Orders/OrderService.cs"));
        finding.Line.Should().Be(42);
        finding.IsGating.Should().BeTrue();
    }

    [Fact]
    public void A_finding_without_text_or_with_a_negative_ordinal_is_refused()
    {
        GateFinding.Of(0, FindingSeverity.Nit, FindingCategory.Unknown, false, 0, "  ", "f").Reason().Should().Contain("no text");
        GateFinding.Of(-1, FindingSeverity.Nit, FindingCategory.Unknown, false, 0, "t", "f").Reason().Should().Contain("ordinal");
        GateFinding.Of(0, FindingSeverity.Nit, FindingCategory.Unknown, false, -5, "t", "f").Reason().Should().Contain("line");
    }

    [Theory]
    [InlineData("blocking", FindingSeverity.Blocking)]
    [InlineData("Major", FindingSeverity.Major)]
    [InlineData("nit", FindingSeverity.Nit)]
    [InlineData("catastrophic", FindingSeverity.Unknown)]
    [InlineData("unknown", FindingSeverity.Unknown)]
    public void The_products_severity_words_parse_by_name_and_an_unknown_one_is_a_state(string word, FindingSeverity expected)
    {
        FindingWords.Severity(word).Should().Be(expected);
        FindingWords.Category("reliability").Should().Be(FindingCategory.Reliability);
        FindingWords.Category("vibes").Should().Be(FindingCategory.Unknown);
    }

    [Fact]
    public void A_verdict_under_a_rubric_hash_the_catalog_does_not_hold_is_refused()
    {
        var stranger = StableHash.Of("an edited prompt nobody catalogued");

        GateVerdict.Under(Catalog(), stranger, Guid.NewGuid(), 0, Strict(StrictReading.Supported), Assessor(), "b1", StrictHash, false).Reason()
            .Should().Contain("no rubric").And.Contain(stranger[..12]).And.Contain("strict-v1#",
                "a verdict under a wording nobody can read back is a verdict about nothing");
    }

    [Fact]
    public void A_strict_reading_under_the_lenient_rubric_is_refused_and_vice_versa()
    {
        GateVerdict.Under(Catalog(), LenientHash, Guid.NewGuid(), 0, Strict(StrictReading.Partial), Assessor(), "b1", LenientHash, false).Reason()
            .Should().Contain("Strict reading").And.Contain("LenientWorth").And.Contain("never share a row");
        GateVerdict.Under(Catalog(), StrictHash, Guid.NewGuid(), 0, new Verdict.Lenient(true), Assessor(), "b1", StrictHash, false).Reason()
            .Should().Contain("LenientWorth reading");

        var strict = GateVerdict.Under(Catalog(), StrictHash, Guid.NewGuid(), 3, Strict(StrictReading.Supported), Assessor(), "b1", StrictHash, false).Ok();
        strict.Rubric.Kind.Should().Be(RubricKind.Strict);
        strict.FindingOrdinal.Should().Be(3);
    }

    [Fact]
    public void An_assessment_failure_is_a_verdict_case_that_never_counts_in_a_rate()
    {
        var failure = GateVerdict.Under(Catalog(), StrictHash, Guid.NewGuid(), 0,
            new Verdict.AssessmentFailure(AssessmentFailureCause.UnknownIds), Assessor(), "b7", StrictHash, false).Ok();

        failure.Reading.CountsInRates.Should().BeFalse("shown as its own count — never read as unassessed, never as refuted");
        failure.Reading.IsSupported.Should().BeFalse();
        failure.Rubric.Hash.Should().Be(StrictHash, "the failure is issued under the same rubric, so a later pass can re-ask exactly these");
        ((Verdict.AssessmentFailure)failure.Reading).Cause.Should().Be(AssessmentFailureCause.UnknownIds);
    }

    [Fact]
    public void A_strict_verdict_carries_a_cluster_hash_and_a_seed_hit_and_no_note()
    {
        var reading = new Verdict.Strict(StrictReading.Supported, ValueLevel.High, SeverityFairness.Overstated, Grounding.Near,
            StableHash.Of("cs2:missing-null-check"), SeedHit.Parse("cs2-S1"));

        reading.IsSupported.Should().BeTrue();
        reading.IsHighValue.Should().BeTrue();
        reading.IsOverstated.Should().BeTrue();
        reading.SeedHit.Should().Be(new SeedHit.Of(SeedId.Parse("cs2-S1").Ok()));
        SeedHit.Parse("none").IsHit.Should().BeFalse();
        SeedHit.Parse("").IsHit.Should().BeFalse();
        typeof(Verdict.Strict).GetProperties().Where(p => p.PropertyType == typeof(string)).Select(p => p.Name)
            .Should().Equal(["ClusterHash"], "the note is text and lives in the artefact store");
    }

    [Fact]
    public void A_rubric_id_is_a_slug_and_its_hash_is_sha256()
    {
        Rubric.Of("strict-v1", RubricKind.Strict, StrictHash).Ok().Stamp.Should().Be($"strict-v1#{StrictHash[..12]}");
        Rubric.Of("Strict V1", RubricKind.Strict, StrictHash).Reason().Should().Contain("rubric id");
        Rubric.Of("strict-v1", RubricKind.Strict, "abc").Reason().Should().Contain("not a rubric hash");
    }

    private static RubricCatalog Catalog() => new(
    [
        Rubric.Of("strict-v1", RubricKind.Strict, StrictHash).Ok(),
        Rubric.Of("lenient-worth-v1", RubricKind.LenientWorth, LenientHash).Ok(),
    ]);

    private static Verdict Strict(StrictReading reading) =>
        new Verdict.Strict(reading, ValueLevel.Medium, SeverityFairness.Yes, Grounding.Yes, StableHash.Of("cs2:x"), new SeedHit.None());

    private static GateReviewerId Assessor() => GateReviewerId.Parse("codex-astra").Ok();
}
