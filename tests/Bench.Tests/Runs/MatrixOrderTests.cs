using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Runs;

/// <summary>The order plan, and the defect it exists to prevent.
/// <para>
/// Balancing execution order by the repeat index alone — <c>repeatIndex % legCount</c> — looks
/// balanced and is not. At an odd repeat count it deals 2:1, and it deals the SAME 2:1 for every
/// question, so the bias never averages out: the leg that runs first systematically gets a warmer
/// cache and a fresher context. The first test below states the guarantee; the second reproduces the
/// naive scheme so the defect is visible in the suite rather than only in a commit message.
/// </para></summary>
public sealed class MatrixOrderTests
{
    private static readonly CommitSha Commit = CommitSha.Parse(new string('b', 40)).Ok();

    [Fact]
    public void First_position_is_balanced_across_the_whole_matrix_at_an_odd_repeat_count()
    {
        var cells = Matrix.Plan(Questions(3), repeats: 3, Subjects("m1"), Lanes("a", "b")).Ok();

        var firsts = Matrix.FirstPositionCounts(cells);

        firsts.Should().HaveCount(2);
        (firsts.Values.Max() - firsts.Values.Min()).Should().BeLessThanOrEqualTo(
            1, "across the whole matrix each leg leads equally often, give or take the odd cell");
    }

    [Fact]
    public void The_naive_per_repeat_rotation_would_deal_two_to_one_and_that_is_the_point()
    {
        // Not production code — the refuted scheme, reproduced so the regression has a shape.
        var legs = new[] { "a", "b" };
        var naiveFirsts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var _ in Enumerable.Range(0, 3))
        {
            for (var repeat = 0; repeat < 3; repeat++)
            {
                var first = legs[repeat % legs.Length];
                naiveFirsts[first] = naiveFirsts.GetValueOrDefault(first) + 1;
            }
        }

        (naiveFirsts.Values.Max() - naiveFirsts.Values.Min()).Should().Be(
            3, "6 against 3 over nine slots — the bias the global counter removes");
    }

    [Fact]
    public void Every_leg_runs_exactly_once_per_question_and_repeat()
    {
        var cells = Matrix.Plan(Questions(2), repeats: 2, Subjects("m1", "m2"), Lanes("a", "b")).Ok();

        cells.Should().HaveCount(2 * 2 * 4);
        cells.GroupBy(c => (c.QuestionId, c.Repeat))
            .Should().OnlyContain(g => g.Select(c => c.Leg.Canonical).Distinct().Count() == 4);
    }

    [Fact]
    public void Positions_within_one_slot_are_dense_and_start_at_zero()
    {
        var cells = Matrix.Plan(Questions(1), repeats: 1, Subjects("m1"), Lanes("a", "b", "c")).Ok();

        cells.Select(c => c.Position).Should().BeEquivalentTo([0, 1, 2]);
    }

    [Fact]
    public void A_matrix_without_subjects_is_refused_rather_than_defaulted()
    {
        Matrix.Plan(Questions(1), repeats: 1, [], Lanes("a"))
            .Reason().Should().Contain("unset model id is a refusal");
    }

    [Theory]
    [InlineData(0, "at least one question")]
    [InlineData(-1, "at least one question")]
    public void A_matrix_without_questions_is_refused(int questionCount, string expected)
    {
        Matrix.Plan(Questions(Math.Max(0, questionCount)), repeats: 1, Subjects("m1"), Lanes("a"))
            .Reason().Should().Contain(expected);
    }

    private static IReadOnlyList<Question> Questions(int count) =>
        [.. Enumerable.Range(1, count).Select(i =>
            new Question($"q{i}", $"prompt {i}", [Expectation.File(SourceAnchor.File($"src/F{i}.cs", Commit))], string.Empty))];

    private static IReadOnlyList<Subject> Subjects(params string[] ids) =>
        [.. ids.Select(id => new Subject(ModelRef.Parse(id, ModelHosting.Local).Ok(), Sampling.Deterministic(1)))];

    private static IReadOnlyList<Lane> Lanes(params string[] names) => [.. names.Select(Lane.Named)];
}
