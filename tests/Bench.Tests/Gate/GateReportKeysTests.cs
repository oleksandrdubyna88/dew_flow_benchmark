using System.Security.Cryptography;
using System.Text;
using Bench.Domain;
using Bench.Domain.Gate;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Gate.GateReportFixtures;

namespace Bench.Tests.Gate;

/// <summary>The keys the report surfaces pass around (E6): the scope id the CLI's <c>--scope</c>, the API's <c>?scope=</c>
/// and the page's control all carry; the one reading of a gate word; the superseded mark on a run list; the canonical
/// form a recorded task set is compared by.</summary>
public sealed class GateReportKeysTests
{
    [Fact]
    public void The_scope_id_is_twelve_hex_of_the_length_prefixed_five_fields()
    {
        var scope = new GateScope("gate-seeded#abc", GateKind.Feature, "0.0.0+aaaaaaa", new string('a', 64), "s1");
        var canonical = "5:scope15:gate-seeded#abc7:feature13:0.0.0+aaaaaaa64:" + new string('a', 64) + "2:s1";
        var expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..12];

        scope.Id.Should().Be(expected, "the id is a documented formula — a refactor that reorders the fields breaks every bookmarked scope");
    }

    [Fact]
    public void Each_of_the_five_fields_moves_the_scope_id()
    {
        var scope = new GateScope("gate-seeded#abc", GateKind.Feature, "0.0.0+aaaaaaa", new string('a', 64), "s1");

        new[]
        {
            scope with { SuiteStamp = "gate-seeded#abd" },
            scope with { Gate = GateKind.Plan },
            scope with { ProductVersion = "0.0.0+aaaaaab" },
            scope with { ProductSha256 = new string('b', 64) },
            scope with { SettingsHash = "s2" },
        }.Select(s => s.Id).Should().OnlyHaveUniqueItems().And.NotContain(scope.Id);
    }

    [Fact]
    public void Two_fields_cannot_trade_text_and_keep_the_id()
    {
        var a = new GateScope("stamp", GateKind.Code, "1.0", string.Empty, "x1");
        var b = new GateScope("stamp", GateKind.Code, "1.0x", string.Empty, "1");

        a.Id.Should().NotBe(b.Id, "the fields are length-prefixed, so a boundary cannot move between them");
    }

    [Theory]
    [InlineData("plan", GateKind.Plan)]
    [InlineData("Code", GateKind.Code)]
    [InlineData("FEATURE", GateKind.Feature)]
    public void A_gate_word_reads_as_its_gate(string word, GateKind gate) =>
        GateWord.Parse(word).Ok().Should().Be(gate);

    [Theory]
    [InlineData("")]
    [InlineData("7")]
    [InlineData("0")]
    [InlineData("plans")]
    [InlineData("feature ")]
    public void Anything_but_the_three_gate_words_is_refused_naming_them(string word) =>
        GateWord.Parse(word).Should().BeOfType<Outcome<GateKind>.Fail>()
            .Which.Reason.Should().Contain("plan, code or feature");

    [Fact]
    public void A_gate_is_written_as_its_lower_case_word() =>
        GateWord.Of(GateKind.Feature).Should().Be("feature");

    [Fact]
    public void An_earlier_attempt_of_a_cell_is_superseded_and_its_latest_attempt_is_not()
    {
        var campaign = Guid.CreateVersion7();
        var first = Run("cs2", "grok", 3, valid: false, campaign: campaign, attempt: 1);
        var second = Run("cs2", "grok", 3, campaign: campaign, attempt: 2);
        var other = Run("cs2", "grok", 2, campaign: campaign);

        GateRunList.Superseded([first, second, other]).Should().BeEquivalentTo([first.RunId]);
    }

    [Fact]
    public void Repeat_one_of_two_campaigns_is_two_cells_and_neither_supersedes_the_other()
    {
        var a = Run("cs2", "grok", 1, campaign: Guid.CreateVersion7(), attempt: 1);
        var b = Run("cs2", "grok", 1, campaign: Guid.CreateVersion7(), attempt: 2);

        GateRunList.Superseded([a, b]).Should().BeEmpty("the campaign is part of the cell, as the population's latest-attempt rule reads it");
    }

    [Fact]
    public void A_task_summary_reads_alike_whatever_order_its_seeds_came_in_and_a_flipped_cross_epic_does_not()
    {
        var one = Task("cs2", [("cs2-S1", false), ("cs2-S2", true)]);
        var reordered = Task("cs2", [("cs2-S2", true), ("cs2-S1", false)]);
        var flipped = Task("cs2", [("cs2-S1", false), ("cs2-S2", false)]);

        one.Canonical.Should().Be(reordered.Canonical);
        one.Canonical.Should().NotBe(flipped.Canonical);
        one.Canonical.Should().NotBe(Task("cs2", [("cs2-S1", false), ("cs2-S2", true)], calibration: true).Canonical);
    }
}
