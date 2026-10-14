using Bench.Domain.Scouts;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>The operator, 2026-10-14: "write input and output tokens, and their prices, separately". A team's mean tokens split by kind —
/// input, output, cache read, cache write 5 m, cache write 1 h — each with its count and its dollars at the published price, and the parts
/// summing to the team's cost. Computed BY HAND at the published Sonnet 5.5 prices ($2 / $10 / $0.10 / $2.50 / $4 per million).</summary>
public sealed class ScoutCostSplitTests
{
    private static readonly ModelPrice Sonnet = ModelPrice.Of("claude-sonnet-5-5", 2m, 10m, 0.10m, 2.50m, 4m).Ok();

    [Fact]
    public void A_team_is_split_into_five_kinds_each_with_tokens_and_dollars_that_sum_to_the_whole()
    {
        var parts = ScoutCostSplit.Of(input: 31, output: 20_772.5, cacheRead: 1_552_211, cacheWrite5m: 179_475, cacheWrite1h: 0, Sonnet);

        parts.Select(p => p.Kind).Should().Equal("input", "output", "cache read", "cache write 5m", "cache write 1h");
        parts.Select(p => p.Usd).Should().Equal(0.000062m, 0.207725m, 0.155221m, 0.448688m, 0m);
        parts.Sum(p => p.Usd).Should().Be(0.811696m);
        ScoutCostSplit.Line(parts).Should().Be(
            "input 31 tok $0.000062 · output 20772.5 tok $0.207725 · cache read 1552211 tok $0.155221 · cache write 5m 179475 tok $0.448688 · cache write 1h 0 tok $0");
    }
}
