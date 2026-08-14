using Bench.Domain.Splitting;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Splitting;

/// <summary>The guard against a right measurement of noise. Its two properties are stability — the same
/// question lands in the same half on every machine, forever — and the refusal to crown a winner that
/// only won where it was chosen.</summary>
public sealed class SeedSplitTests
{
    private static readonly CommitSha Commit = CommitSha.Parse(new string('d', 40)).Ok();

    [Fact]
    public void The_same_question_lands_in_the_same_half_every_time()
    {
        var first = SeedSplit.Assign("suite-a", "q-17").Ok();
        var second = SeedSplit.Assign("suite-a", "q-17").Ok();

        second.Should().Be(first, "a split that re-assigns on re-run defeats itself, and does it invisibly");
    }

    [Fact]
    public void The_split_does_not_move_when_a_new_version_is_frozen()
    {
        var v1 = FrozenAtVersion(1);
        var v3 = FrozenAtVersion(3);

        v1.Version.Should().Be(1);
        v3.Version.Should().Be(3);
        SeedSplit.Assign(v3).Ok()["q1"].Should().Be(
            SeedSplit.Assign(v1).Ok()["q1"],
            "freezing a new version must not reshuffle halves under a comparison that spans versions");
    }

    [Fact]
    public void Both_halves_are_used_over_a_realistic_number_of_questions()
    {
        var halves = SeedSplit.Assign(LargeSuite(80)).Ok();

        halves.Values.Count(h => h == SplitHalf.Selection).Should().BeInRange(25, 55);
        halves.Values.Count(h => h == SplitHalf.HeldOut).Should().BeInRange(25, 55);
    }

    [Theory]
    [InlineData(true, true, ProofState.Confirmed)]
    [InlineData(true, false, ProofState.Unproven)]
    [InlineData(false, true, ProofState.Suspicious)]
    [InlineData(false, false, ProofState.NotAWinner)]
    public void A_winner_is_only_confirmed_when_it_also_wins_where_it_was_not_chosen(
        bool selection, bool heldOut, ProofState expected)
    {
        SeedSplit.Proof(selection, heldOut).Should().Be(expected);
    }

    /// <summary>A suite carried through <paramref name="version"/> freeze/amend cycles, so two suites
    /// with the same id and the same question can differ only by version number.</summary>
    private static Suite FrozenAtVersion(int version)
    {
        var draft = Suite.Draft("suite-a")
            .With(Question.Ask("q1", "one", Expectation.File(SourceAnchor.File("src/A.cs", Commit)))).Ok();

        var frozen = draft.Freeze().Ok();

        for (var i = 1; i < version; i++)
        {
            frozen = frozen.Amend().Ok().Freeze().Ok();
        }

        return frozen;
    }

    private static Suite LargeSuite(int questions) =>
        Enumerable.Range(1, questions).Aggregate(
            Suite.Draft("big"),
            (suite, i) => suite.With(
                Question.Ask($"q{i}", $"prompt {i}", Expectation.File(SourceAnchor.File($"src/F{i}.cs", Commit)))).Ok())
            .Freeze().Ok();
}
