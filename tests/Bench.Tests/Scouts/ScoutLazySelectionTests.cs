using Bench.Domain;
using Bench.Domain.Scouts;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>The LAZY read of the D10 selection (operator, 2026-10-14: "why read them all if the first ones that qualify will do?"). The draw
/// is fixed by hashes the candidate LIST alone gives — repositories by <c>sha256(seed|repository)</c>, candidates by <c>sha256(seed|key)</c> —
/// so the mirror only has to be read in that order, a repository only until its fifth admissible candidate, and only until ten repositories
/// are eligible. Whatever is not read cannot change the draw. These tests hold the lazy read to the full one: the SAME tasks in the SAME roles
/// on every seed, over a pool where some repositories fall short and inadmissible candidates sit between admissible ones.</summary>
public sealed class ScoutLazySelectionTests
{
    private const string P = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static readonly DateOnly Cutoff = new(2026, 6, 30);

    private static readonly DateTimeOffset After = new(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);

    public static TheoryData<string> Seeds() => ["scouts-preliminary-2026-10-12", "another-campaign", "s"];

    [Theory]
    [MemberData(nameof(Seeds))]
    public async Task The_lazy_read_draws_exactly_what_reading_every_candidate_draws_and_reads_fewer(string seed)
    {
        var pool = Pool(repositories: 24);
        var read = new List<string>();

        var lazy = (await ScoutLazySelection.ReadAsync([.. pool.Select(f => f.Candidate)], seed, Cutoff, c => Read(pool, c, read))).Ok();

        Drawn(lazy, seed).Should().Equal(Drawn(pool, seed), "the lazy read must draw the same tasks in the same roles");
        Drawn(pool, seed).Should().HaveCount(18, "the pool holds ten eligible repositories, so the draw is the full D10 set");
        read.Should().HaveCountLessThan(pool.Count, "the point of reading lazily").And.OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task With_fewer_eligible_repositories_than_the_draw_needs_every_candidate_is_read_and_the_draw_is_the_same()
    {
        var pool = Pool(repositories: 6);
        var read = new List<string>();

        var lazy = (await ScoutLazySelection.ReadAsync([.. pool.Select(f => f.Candidate)], "s", Cutoff, c => Read(pool, c, read))).Ok();

        Drawn(lazy, "s").Should().Equal(Drawn(pool, "s"));
        read.Should().HaveCount(pool.Count - SkippedAfterFive(pool), "only a repository already at five admissible stops early");
    }

    [Fact]
    public async Task A_candidate_that_cannot_be_read_stops_the_lazy_read_with_its_reason()
    {
        var pool = Pool(repositories: 24);

        var outcome = await ScoutLazySelection.ReadAsync([.. pool.Select(f => f.Candidate)], "s", Cutoff,
            _ => Task.FromResult(Outcome<ScoutCandidateFacts>.Failure("the mirror could not be read")));

        outcome.Reason().Should().Be("the mirror could not be read");
    }

    private static IReadOnlyList<(string, ScoutTaskRole)> Drawn(IReadOnlyList<ScoutCandidateFacts> facts, string seed) =>
        [.. ScoutSelectionDraw.Of(ScoutCandidates.Admit(facts, Cutoff).Admitted, seed).Tasks.Select(t => (t.Facts.Candidate.Id.Value, t.Role))];

    private static Task<Outcome<ScoutCandidateFacts>> Read(IReadOnlyList<ScoutCandidateFacts> pool, ScoutCandidate candidate, List<string> read)
    {
        read.Add(candidate.Id.Value);
        return Task.FromResult(Outcome<ScoutCandidateFacts>.Success(pool.Single(f => f.Candidate.Id == candidate.Id)));
    }

    /// <summary>How many candidates a repository leaves unread once it has five admissible ones — in its hash order, under seed "s".</summary>
    private static int SkippedAfterFive(IReadOnlyList<ScoutCandidateFacts> pool) =>
        ScoutSelectionDraw.ReadingOrder([.. pool.Select(f => f.Candidate)], "s").Sum(group =>
        {
            var admissible = 0;
            return group.Count(c => admissible >= ScoutSelectionDraw.MinAdmissible || (Admissible(pool, c) && ++admissible > ScoutSelectionDraw.MinAdmissible));
        });

    private static bool Admissible(IReadOnlyList<ScoutCandidateFacts> pool, ScoutCandidate candidate) =>
        ScoutCandidates.Admit([pool.Single(f => f.Candidate.Id == candidate.Id)], Cutoff).Admitted.Count == 1;

    /// <summary>Repository <c>i</c> holds <c>5 + (i % 4) * 3</c> candidates; one in three is inadmissible (two files of G) and one in seven
    /// was committed before the cut-off — so some repositories fall short of five and the admissible ones are interleaved with the rest.</summary>
    private static IReadOnlyList<ScoutCandidateFacts> Pool(int repositories) =>
        [.. Enumerable.Range(0, repositories).SelectMany(i => Enumerable.Range(0, 5 + (i % 4) * 3).Select(j => Facts(i, j)))];

    private static ScoutCandidateFacts Facts(int repository, int index)
    {
        var serial = (repository * 100) + index;
        var groundTruth = serial % 3 == 0 ? new[] { "src/A.cs", "src/B.cs" } : ["src/A.cs", "src/B.cs", "src/C.cs"];
        var parentDate = serial % 7 == 0 ? new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero) : After;
        var candidate = new ScoutCandidate(
            ScoutTaskId.Parse($"c{repository:00}-{index:00}").Ok(), $"org/repo{repository:00}", CommitSha.Parse(P).Ok(),
            CommitSha.Parse(serial.ToString("x40", System.Globalization.CultureInfo.InvariantCulture)).Ok(), ScoutQuestionKind.HowEndToEnd, "How does it work?", string.Empty)
        {
            MergeStyle = ScoutMergeStyles.Parse("local-squash").Ok(),
        };

        return new ScoutCandidateFacts(candidate, parentDate, groundTruth, new ScoutParentFacts([CommitSha.Parse(P).Ok()], false));
    }
}
