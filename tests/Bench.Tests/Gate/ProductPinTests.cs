using Bench.Domain.Gate;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate;

/// <summary>Which bytes a run measured, and the one decision a pin makes: may this binary continue that
/// campaign. The calibration this replaces crossed the line once — a rebase mid-measurement left phase-1 and
/// phase-2 runs on different shas — which is exactly what a report must be able to see afterwards.</summary>
public sealed class ProductPinTests
{
    private static readonly string ShaA = new('a', 64);
    private static readonly string ShaB = new('b', 64);

    [Fact]
    public void A_campaign_continued_against_a_different_binary_is_refused_naming_both_shas()
    {
        var campaign = Pin(ShaA, "0.0.0+1111111");
        var moved = Pin(ShaB, "0.0.0+2222222");

        var refusal = ProductPin.Continue(campaign, moved).Reason();

        refusal.Should().Contain(ShaA[..12]).And.Contain(ShaB[..12],
            "the operator has to see WHAT changed, not only that something did");
        refusal.Should().Contain("--allow-product-change").And.Contain("new scope");
    }

    [Fact]
    public void The_same_binary_continues_the_campaign_whatever_its_checkout_says()
    {
        var campaign = Pin(ShaA, "0.0.0+1111111");
        var rebuilt = ProductPin.Hashed(ShaA, "0.0.0+1111111", "1111111", CapturedCount.Number(3), "src_mcp").Ok();

        ProductPin.Continue(campaign, rebuilt).Ok().Should().Be(rebuilt, "identical bytes are the same product, dirty tree or not");
        campaign.Matches(rebuilt).Should().BeTrue();
    }

    [Fact]
    public void An_imported_pin_never_matches_a_hashed_one_because_nothing_was_hashed()
    {
        var imported = ProductPin.Imported("98acfa74", CapturedCount.Number(0), "calib-py phase 2").Ok();
        var hashed = Pin(ShaA, "0.0.0+98acfa74");

        imported.BinaryHashed.Should().BeFalse();
        imported.VersionText.Should().Contain("imported").And.Contain("not hashed");
        imported.Matches(hashed).Should().BeFalse("a sha somebody wrote down is not evidence the bytes were the same");
        imported.Matches(imported).Should().BeFalse("two unhashed pins are not evidence of anything either");
        ProductPin.Continue(imported, hashed).Reason().Should().Contain("unhashed");
    }

    [Fact]
    public void Imported_pins_of_one_population_share_a_scope_whatever_commit_each_recorded()
    {
        var first = ProductPin.Imported("5d73ead7", CapturedCount.Number(1), "calib-py phase 2").Ok();
        var later = ProductPin.Imported("98acfa74", CapturedCount.Number(7), "calib-py phase 2").Ok();
        var phase1 = ProductPin.Imported("0d39a233", CapturedCount.Number(33), "calib-py phase 1").Ok();

        GateScope.Of("s#1", GateKind.Feature, first, "h").Should().Be(GateScope.Of("s#1", GateKind.Feature, later, "h"),
            "the other harness compared these runs as one population; a scope per commit would split its table into pieces nobody can hold against it");
        GateScope.Of("s#1", GateKind.Feature, phase1, "h").Should().NotBe(GateScope.Of("s#1", GateKind.Feature, later, "h"),
            "a different population is a different scope");
        later.GitSha.Should().Be("98acfa74", "the commit stays on the pin, per cell, for a partition by commit");
        later.IsPinned.Should().BeTrue();
        later.IsImported.Should().BeTrue();
        later.VersionText.Should().NotContain("98acfa74");
    }

    [Fact]
    public void An_imported_pin_reads_back_from_its_stored_text_as_the_pin_that_was_written()
    {
        var written = ProductPin.Imported(string.Empty, CapturedCount.Unavailable("not recorded"), "coai-bench epic1-v0.17.1").Ok();

        var read = ProductPin.ImportedStored(written.VersionText, written.GitSha, written.DirtyFiles).Ok();

        read.VersionText.Should().Be(written.VersionText);
        read.IsPinned.Should().BeTrue("a harness that recorded no sha still names the population it measured");
        ProductPin.Imported("98acfa74", CapturedCount.Number(0), " ").Reason().Should().Contain("population");
        ProductPin.ImportedStored("0.39.0", "98acfa74", CapturedCount.Number(0)).Reason().Should().Contain("not an imported pin");
    }

    [Fact]
    public void A_binary_outside_any_checkout_has_an_empty_git_sha_and_says_so()
    {
        var pin = ProductPin.Hashed(ShaA, "1.4.0", string.Empty, CapturedCount.Unavailable("no git checkout above the binary"), string.Empty).Ok();

        pin.UnderCheckout.Should().BeFalse();
        pin.DirtyFiles.WasCaptured.Should().BeFalse("no checkout means no dirty count — never a zero, which reads as clean");
        pin.Describe.Should().Contain("no checkout above the binary");
    }

    [Fact]
    public void The_pin_says_which_tree_its_dirty_check_covered()
    {
        var scoped = ProductPin.Hashed(ShaA, "0.0.0+1111111", "1111111", CapturedCount.Number(2), "src_mcp").Ok();
        var whole = ProductPin.Hashed(ShaA, "0.0.0+1111111", "1111111", CapturedCount.Number(2), string.Empty).Ok();

        scoped.Describe.Should().Contain("2 dirty file(s) in src_mcp");
        whole.Describe.Should().Contain("in the whole checkout", "a count without its scope cannot be re-checked");
    }

    [Theory]
    [InlineData("", "0.0.0+1111111", "1111111", "not a SHA-256")]
    [InlineData("abc", "0.0.0+1111111", "1111111", "not a SHA-256")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "", "1111111", "--version")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "1.0", "main", "not a git sha")]
    public void A_malformed_pin_is_refused_by_name(string sha, string version, string git, string expected)
    {
        ProductPin.Hashed(sha, version, git, CapturedCount.Number(0), string.Empty).Reason().Should().Contain(expected);
    }

    [Fact]
    public void The_unpinned_value_is_a_state_not_a_null()
    {
        ProductPin.None.IsPinned.Should().BeFalse();
        ProductPin.None.DirtyFiles.WasCaptured.Should().BeFalse();
    }

    private static ProductPin Pin(string sha, string version) =>
        ProductPin.Hashed(sha, version, string.Empty, CapturedCount.Unavailable("no checkout"), string.Empty).Ok();
}
