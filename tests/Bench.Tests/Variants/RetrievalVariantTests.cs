using Bench.Domain;
using Bench.Domain.Variants;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Variants;

/// <summary>The catalog row's own rules: a name that can be quoted, and a definition that can never be
/// edited under a name results already carry.
/// <para>
/// This mirrors <see cref="Bench.Domain.Suites.Suite"/>'s freeze, and for the same reason: results name
/// the variant they ran under, so a definition that could be edited in place would silently relabel every
/// number ever measured against it.
/// </para></summary>
public sealed class RetrievalVariantTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData("Hybrid-RRF")]
    [InlineData("hybrid rrf")]
    [InlineData("hybrid_rrf")]
    public void A_name_that_is_not_a_short_slug_is_refused(string name)
    {
        Variant(name).Reason().Should().Contain("name");
    }

    [Theory]
    [InlineData("hybrid-rrf-bge-256")]
    [InlineData("dense-only")]
    [InlineData("baseline2")]
    public void A_short_slug_is_accepted(string name)
    {
        Variant(name).Ok().Name.Value.Should().Be(name);
    }

    [Fact]
    public void A_blank_display_name_falls_back_to_the_name_rather_than_rendering_as_nothing()
    {
        Variant(display: "").Ok().DisplayName.Should().Be("hybrid-rrf");
    }

    [Fact]
    public void A_new_variant_is_active_and_carries_its_definition_hash()
    {
        var variant = Variant().Ok();

        variant.IsActive.Should().BeTrue();
        variant.Hash.Should().Be(variant.Definition.Hash);
        variant.Stamp.Should().StartWith("hybrid-rrf#");
    }

    [Fact]
    public void Retiring_returns_a_new_value_and_leaves_the_original_untouched()
    {
        var active = Variant().Ok();

        var retired = active.Retire(Now).Ok();

        retired.IsActive.Should().BeFalse();
        retired.RetiredAt.Should().Be(Now);
        active.IsActive.Should().BeTrue("a variant is a value; retiring one may never mutate the one already held elsewhere");
        retired.Id.Should().Be(active.Id, "retiring is not a new variant — the row keeps its identity so historical cells still resolve");
    }

    [Fact]
    public void Retiring_twice_is_refused_rather_than_silently_accepted()
    {
        var retired = Variant().Ok().Retire(Now).Ok();

        retired.Retire(Now.AddDays(1)).Reason().Should().Contain("already retired");
    }

    [Fact]
    public void A_selection_carries_the_id_and_the_name_so_a_cell_needs_no_second_lookup()
    {
        var variant = Variant().Ok();

        var selection = variant.Select();

        selection.Should().BeOfType<VariantSelection.Selected>()
            .Which.Should().Match<VariantSelection.Selected>(s => s.Id == variant.Id && s.Name == "hybrid-rrf");
    }

    [Fact]
    public void The_not_applicable_selection_is_a_state_of_its_own_never_an_empty_id()
    {
        VariantSelection.None.Should().BeOfType<VariantSelection.NotApplicable>();
        VariantSelection.None.Canonical.Should().NotBeEmpty();
    }

    private static Outcome<RetrievalVariant> Variant(string name = "hybrid-rrf", string display = "hybrid · rrf") =>
        RetrievalVariant.Create(name, display, VariantDefinitionTests.Retrieval().Ok(), Now);
}
