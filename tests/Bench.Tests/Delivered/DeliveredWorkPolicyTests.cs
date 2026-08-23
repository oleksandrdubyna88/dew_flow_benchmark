using Bench.Delivered;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Delivered;

/// <summary>The deterministic corrections that run AFTER the model.
/// <para>
/// Two of these reproduce measured cases from the source corpus by shape — run #13862's one-line diff that
/// scored 79 on 22 rescued points, and run #15105's unit that paid 5 for calling itself a mirror. They are
/// the reason the rules exist, and a port whose rules fired differently on them would be a port of the
/// vocabulary rather than of the correction.
/// </para>
/// <para>
/// The EXEMPTIONS matter as much as the rules. Co-location never caps and a cross-file mirror never caps;
/// without both, the near-duplicate rule stops being a cap on repetition and becomes a penalty on any large
/// file — which would punish exactly the fat-class refactors this benchmark most wants to price.
/// </para></summary>
public sealed class DeliveredWorkPolicyTests
{
    [Fact]
    public void An_UNCORRECTED_run_says_so_rather_than_reading_like_one_nobody_checked()
    {
        var result = DeliveredWorkPolicy.Apply(Input([Unit("a", 5), Unit("b", 3)]));

        result.Total.Should().Be(8);
        result.Corrected.Should().BeFalse();
        result.Describe.Should().Contain("uncorrected");
    }

    [Fact]
    public void A_unit_that_DECLARES_itself_a_repeat_of_a_same_file_sibling_is_capped()
    {
        // Run #15105's shape: the unit's own justification names it a mirror, and it kept full price.
        var result = DeliveredWorkPolicy.Apply(Input(
            [Unit("first", 5, "the original gate"), Unit("second", 5, "mirrors first — same established pattern")],
            anchors: new() { ["first"] = "src/Manager.cs", ["second"] = "src/Manager.cs" }));

        result.Total.Should().Be(5 + Inherited.NearDuplicateCap);
        result.Adjustments.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new Adjustment("second", 5, 2, DeliveredWorkPolicy.NearDuplicateRule));
    }

    [Fact]
    public void The_STRONGEST_unit_on_a_file_keeps_full_price_however_the_input_is_ordered()
    {
        var weakFirst = DeliveredWorkPolicy.Apply(Input(
            [Unit("weak", 3, "mirrors strong"), Unit("strong", 8, "mirrors weak")],
            anchors: new() { ["weak"] = "src/M.cs", ["strong"] = "src/M.cs" }));

        // Deterministic election — score descending, then key — is what lets a rescore reproduce a
        // published number exactly rather than approximately.
        weakFirst.Applied.Single(u => u.Key == "strong").Score.Should().Be(8);
        weakFirst.Applied.Single(u => u.Key == "weak").Score.Should().Be(2);
    }

    [Fact]
    public void CO_LOCATION_ALONE_never_caps_because_five_gates_in_one_class_are_five_units()
    {
        var result = DeliveredWorkPolicy.Apply(Input(
            [Unit("a", 5, "validates the owner"), Unit("b", 5, "validates the deadline")],
            anchors: new() { ["a"] = "src/FatManager.cs", ["b"] = "src/FatManager.cs" }));

        // Without this exemption the rule stops being a cap on repetition and becomes a penalty on large
        // files — punishing precisely the refactors worth pricing.
        result.Adjustments.Should().BeEmpty();
        result.Total.Should().Be(10);
    }

    [Fact]
    public void A_CROSS_FILE_mirror_is_following_a_pattern_rather_than_repeating_work()
    {
        var result = DeliveredWorkPolicy.Apply(Input(
            [Unit("a", 5, "the original"), Unit("b", 5, "mirrors a — same established pattern")],
            anchors: new() { ["a"] = "src/One.cs", ["b"] = "src/Two.cs" }));

        result.Adjustments.Should().BeEmpty();
    }

    [Fact]
    public void A_SYMBOL_SUFFIX_names_a_place_inside_a_file_not_another_file()
    {
        var result = DeliveredWorkPolicy.Apply(Input(
            [Unit("a", 5, "the original"), Unit("b", 5, "duplicate of a")],
            anchors: new() { ["a"] = "src/M.cs#Alpha", ["b"] = "src/M.cs#Beta" }));

        result.Adjustments.Should().ContainSingle().Which.Rule.Should().Be(DeliveredWorkPolicy.NearDuplicateRule);
    }

    [Fact]
    public void A_unit_with_NO_ANCHOR_groups_with_nothing()
    {
        var result = DeliveredWorkPolicy.Apply(Input(
            [Unit("a", 5, "the original"), Unit("b", 5, "mirrors a")]));

        // No anchor is not "the same file as every other anchorless unit". Treating it that way would cap
        // whole runs whose weigher simply failed to name files.
        result.Adjustments.Should().BeEmpty();
    }

    [Fact]
    public void A_unit_already_at_or_below_the_cap_is_left_alone()
    {
        var result = DeliveredWorkPolicy.Apply(Input(
            [Unit("a", 5, "the original"), Unit("b", 2, "mirrors a")],
            anchors: new() { ["a"] = "src/M.cs", ["b"] = "src/M.cs" }));

        result.Adjustments.Should().BeEmpty("a cap that RAISES a score would be a different rule entirely");
        result.Total.Should().Be(7);
    }

    [Fact]
    public void A_ONE_LINE_diff_cannot_score_seventy_nine_on_rescued_points()
    {
        // Run #13862 by shape: one unit of diff-side evidence, twenty-three points, twenty-two of them
        // rescued. The allowance admits 2 x 1 = 2 scored units in total; one is already matched, so exactly
        // one rescue survives — the strongest.
        var units = new List<UnitScore> { Unit("matched", 5, "real work") };
        units.AddRange(Enumerable.Range(1, 22).Select(i => Unit($"rescued-{i:00}", 3, "adjudicator rescue")));

        var result = DeliveredWorkPolicy.Apply(Input(
            units, rescued: [.. units.Skip(1).Select(u => u.Key)], diffUnits: 1));

        result.Applied.Should().HaveCount(2);
        result.Total.Should().Be(8, "the matched 5 plus the single admitted rescue");
        result.Adjustments.Should().HaveCount(21).And.OnlyContain(
            a => a.Rule == DeliveredWorkPolicy.RescueAllowanceRule && a.AppliedScore == 0);
    }

    [Fact]
    public void A_dropped_rescue_is_RECORDED_at_zero_rather_than_vanishing()
    {
        var result = DeliveredWorkPolicy.Apply(Input(
            [Unit("matched", 4, "real"), Unit("rescue", 9, "rescued")],
            rescued: ["rescue"], diffUnits: 0));

        // The difference between a correction a reader can audit and a number that simply came out lower.
        result.Adjustments.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new Adjustment("rescue", 9, 0, DeliveredWorkPolicy.RescueAllowanceRule));
        result.Total.Should().Be(4);
    }

    [Fact]
    public void MATCHED_units_are_never_touched_however_thin_the_evidence()
    {
        var result = DeliveredWorkPolicy.Apply(Input(
            [Unit("a", 5, "real"), Unit("b", 5, "real"), Unit("c", 5, "real")], diffUnits: 0));

        // Each matched unit is tied to work the matcher SAW in the diff. The allowance is a rule about
        // rescues, and applying it to matched work would be a different and unmeasured correction.
        result.Adjustments.Should().BeEmpty();
        result.Total.Should().Be(15);
    }

    [Fact]
    public void With_NO_evidence_reading_every_rescue_is_kept_rather_than_a_denominator_invented()
    {
        var result = DeliveredWorkPolicy.Apply(Input(
            [Unit("a", 5, "real"), Unit("b", 7, "rescued")], rescued: ["b"], diffUnits: null));

        result.Total.Should().Be(12);
        result.Adjustments.Should().BeEmpty();
    }

    [Fact]
    public void The_RAW_score_survives_in_the_trail_even_when_the_cap_already_lowered_it()
    {
        var result = DeliveredWorkPolicy.Apply(Input(
            [Unit("first", 9, "the original"), Unit("second", 9, "mirrors first")],
            anchors: new() { ["first"] = "src/M.cs", ["second"] = "src/M.cs" },
            rescued: ["second"], diffUnits: 0));

        // Both rules fired on `second`. The trail must show the MODEL's 9 in each, not the capped 2 — a
        // reader asking "what did the model say" cannot get that from an applied score.
        result.Adjustments.Should().HaveCount(2).And.OnlyContain(a => a.RawScore == 9);
        result.Adjustments.Select(a => a.Rule).Should().BeEquivalentTo(
            [DeliveredWorkPolicy.NearDuplicateRule, DeliveredWorkPolicy.RescueAllowanceRule]);
    }

    [Fact]
    public void The_result_is_IDENTICAL_when_the_same_input_is_applied_twice()
    {
        var input = Input(
            [Unit("a", 5, "the original"), Unit("b", 5, "mirrors a"), Unit("c", 4, "rescued")],
            anchors: new() { ["a"] = "src/M.cs", ["b"] = "src/M.cs" },
            rescued: ["c"], diffUnits: 1);

        // The recompute property in miniature: policy replayed over stored values must reproduce the
        // published number exactly, or a rescore is a new measurement wearing an old run's id.
        DeliveredWorkPolicy.Apply(input).Should().BeEquivalentTo(DeliveredWorkPolicy.Apply(input));
    }

    [Fact]
    public void Every_inherited_constant_carries_the_measurement_that_produced_it()
    {
        // §4.4: "which numbers are ours" must be answerable by reading one file.
        Inherited.NearDuplicateCap.Should().Be(2);
        Inherited.RescueAllowancePerEvidenceUnit.Should().Be(2);
        Inherited.Badge.Should().Be("inherited calibration");
    }

    // ---- scaffolding -------------------------------------------------------------------------------

    private static UnitScore Unit(string key, int score, string why = "did a thing") => new(key, score, why);

    private static PolicyInput Input(
        IReadOnlyList<UnitScore> scores,
        Dictionary<string, string>? anchors = null,
        string[]? rescued = null,
        int? diffUnits = null) =>
        new(scores, anchors ?? [], new HashSet<string>(rescued ?? [], StringComparer.Ordinal), diffUnits);
}
