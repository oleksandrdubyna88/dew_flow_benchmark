using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Domain.Variants;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Runs;

/// <summary>The variant axis: the retrieval configuration becomes a dimension of the matrix rather than
/// one value fixed for a whole run.
/// <para>
/// The balance guarantee <see cref="MatrixOrderTests"/> states must survive the third axis — with three
/// axes the leg count is no longer a small number, and the rotation has to keep dealing first position
/// evenly across all of them or a variant systematically measures a warmer cache than its neighbour.
/// </para></summary>
public sealed class MatrixVariantAxisTests
{
    private static readonly CommitSha Commit = CommitSha.Parse(new string('c', 40)).Ok();

    [Fact]
    public void Every_variant_runs_once_per_question_repeat_subject_and_lane()
    {
        var cells = Matrix.Plan(Questions(2), repeats: 2, Subjects("m1"), Lanes("a"), Variants("v1", "v2", "v3")).Ok();

        cells.Should().HaveCount(2 * 2 * 1 * 1 * 3);
        cells.GroupBy(c => (c.QuestionId, c.Repeat))
            .Should().OnlyContain(g => g.Select(c => c.Leg.Canonical).Distinct().Count() == 3);
    }

    [Fact]
    public void First_position_is_balanced_across_the_whole_matrix_with_a_variant_axis()
    {
        var cells = Matrix.Plan(Questions(5), repeats: 3, Subjects("m1", "m2"), Lanes("a"), Variants("v1", "v2", "v3")).Ok();

        var firsts = Matrix.FirstPositionCounts(cells);

        firsts.Should().HaveCount(6, "two subjects by three variants is six legs");
        (firsts.Values.Max() - firsts.Values.Min()).Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public void A_matrix_without_variants_is_refused_rather_than_silently_running_one_configuration()
    {
        Matrix.Plan(Questions(1), repeats: 1, Subjects("m1"), Lanes("a"), [])
            .Reason().Should().Contain("variant");
    }

    [Fact]
    public void A_leg_names_its_variant_so_two_configurations_never_share_an_identity()
    {
        var cells = Matrix.Plan(Questions(1), repeats: 1, Subjects("m1"), Lanes("a"), Variants("v1", "v2")).Ok();

        cells.Select(c => c.Leg.Canonical).Distinct().Should().HaveCount(2);
        cells.Select(c => c.Leg.Canonical).Should().AllSatisfy(c => c.Should().ContainAny("#v1", "#v2"));
    }

    [Fact]
    public void A_leg_without_a_variant_keeps_the_canonical_form_it_had_before_the_axis_existed()
    {
        var cells = Matrix.Plan(Questions(1), repeats: 1, Subjects("m1"), Lanes("a")).Ok();

        cells.Single().Leg.Canonical.Should().Be(
            "m1|t=0,s=1@a",
            "a run planned without the catalog stores the identity it always stored — the axis is additive, not a rewrite");
    }

    [Fact]
    public void A_cell_carries_its_variant_beside_the_canonical_leg_rather_than_inside_it()
    {
        var variant = Variants("v1")[0];
        var cells = Matrix.Plan(Questions(1), repeats: 1, Subjects("m1"), Lanes("a"), [variant]).Ok();

        var cell = RunCell.Pending(Guid.CreateVersion7(), cells.Single());

        cell.Variant.Should().Be(variant);
    }

    private static IReadOnlyList<Question> Questions(int count) =>
        [.. Enumerable.Range(1, count).Select(i =>
            new Question($"q{i}", $"prompt {i}", [Expectation.File(SourceAnchor.File($"src/F{i}.cs", Commit))], string.Empty))];

    private static IReadOnlyList<Subject> Subjects(params string[] ids) =>
        [.. ids.Select(id => new Subject(ModelRef.Parse(id, ModelHosting.Local).Ok(), Sampling.Deterministic(1)))];

    private static IReadOnlyList<Lane> Lanes(params string[] names) => [.. names.Select(Lane.Named)];

    private static IReadOnlyList<VariantSelection> Variants(params string[] names) =>
        [.. names.Select(n => VariantSelection.Of(Guid.CreateVersion7(), n))];
}
