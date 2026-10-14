using Bench.Domain.Probes;
using Bench.Domain.Scouts;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>§3.6's cost check, settled by the pilot (2026-10-14) as the plan asked: the PUBLISHED price of a Sonnet 5.5 cache hit is $0.10 per
/// million (0.05× its input price — the pricing page's footnote 2, read again that night), but claude 2.1.289's <c>total_cost_usd</c> bills it at
/// $0.20 (0.1×): the pilot's B2 cell reported $1.10243 for Sonnet, which is exactly its tokens at $0.20 and 2.5 % off at $0.10 — while the Opus
/// main session matched the published table to the cent. So the table keeps the published prices, which every reported dollar ("which
/// team is cheaper") is computed at, and carries the CLI's OBSERVED rate beside it (<c>cliRates</c>) for the one purpose of the check: to
/// prove the tokens were attributed right, it compares like with like.</summary>
public sealed class ScoutCliCostBasisTests
{
    private const string Opus = "claude-opus-5-5";
    private const string Sonnet = "claude-sonnet-5-5";

    private const string Published = """
        { "source": "https://platform.claude.com/docs/en/about-claude/pricing", "readOn": "2026-10-12",
          "models": [
            { "modelId": "claude-opus-5-5", "input": 4, "output": 20, "cacheRead": 0.20, "cacheWrite5m": 5, "cacheWrite1h": 8 },
            { "modelId": "claude-sonnet-5-5", "input": 2, "output": 10, "cacheRead": 0.10, "cacheWrite5m": 2.50, "cacheWrite1h": 4 }
          ]
        """;

    private static readonly string WithCliRate = Published + """
        , "cliRates": [ { "modelId": "claude-sonnet-5-5", "cacheRead": 0.20, "observed": "the pilot's B2 cell, 2026-10-14" } ] }
        """;

    [Fact]
    public void The_cli_rate_is_read_beside_the_published_price_and_replaces_only_that_models_cache_read()
    {
        var prices = ScoutFiles.Prices(WithCliRate).Ok();
        var billed = prices.AsTheCliBills();

        prices.For(Sonnet).Ok().CacheReadPerMtok.Should().Be(0.10m, "the published price stays the price");
        billed.For(Sonnet).Ok().Should().Be(prices.For(Sonnet).Ok() with { CacheReadPerMtok = 0.20m });
        billed.For(Opus).Ok().Should().Be(prices.For(Opus).Ok(), "a model with no observed CLI rate is billed as published");
        ScoutFiles.Prices(Published + "}").Ok().AsTheCliBills().Should().BeEquivalentTo(ScoutFiles.Prices(Published + "}").Ok(), "no cliRates, nothing replaced");
    }

    [Theory]
    [InlineData("""{ "modelId": "claude-haiku-9", "cacheRead": 0.2, "observed": "x" }""", "claude-haiku-9")]
    [InlineData("""{ "modelId": "claude-sonnet-5-5", "cacheRead": 0.2, "observed": "" }""", "observed")]
    public void A_cli_rate_for_an_unpriced_model_or_without_its_observation_is_refused(string rate, string named)
    {
        ScoutFiles.Prices(Published + $", \"cliRates\": [ {rate} ] }}").Reason().Should().Contain(named);
    }

    /// <summary>A Sonnet team with the pilot cell's 1 749 236 cache-read tokens, its envelope reporting what the CLI bills: against the
    /// published table alone the check disagrees by more than 2 %; with the CLI's observed rate it agrees — and the gap is exactly those
    /// tokens at the $0.10 difference.</summary>
    [Fact]
    public void A_cell_the_cli_billed_at_its_own_cache_read_rate_agrees_only_when_the_check_uses_that_rate()
    {
        var published = ScoutFiles.Prices(Published + "}").Ok();
        var withCli = ScoutFiles.Prices(WithCliRate).Ok();
        var usage = ScoutTokens.Attribute(Cell(0m)).Ok();
        var atTable = ScoutCost.Of(usage, published).Ok().TotalUsd;
        var atCli = ScoutCost.Of(usage, withCli.AsTheCliBills()).Ok().TotalUsd;
        var cell = Cell(atCli);

        (atCli - atTable).Should().Be(1_749_236 * 0.10m / 1_000_000m);
        ScoutCellReading.CostOf(cell, published).Should().BeOfType<CostCheck.Disagrees>("without the CLI's rate the check compares unlike prices");
        ScoutCellReading.CostOf(cell, withCli).Should().BeOfType<CostCheck.Agrees>();
    }

    private static SubagentTranscript Cell(decimal reportedUsd)
    {
        var main = new TokenUsage(4, 1_605, 12_616, 0, 16_296);
        var child = new TokenUsage(104, 300, 1_749_236, 176_496, 0);

        return new SubagentTranscript(
            Complete: true,
            MainModel: Opus,
            CliVersion: "2.1.289",
            WorkingDirectory: "C:/work/tree",
            MainCalls: [new MainToolCall("call-0", "Agent")],
            MainStreamUsage: main with { Output = 5 },
            Agents: [new SubagentCall("call-0", ScoutArms.CodeExplorerType, "sonnet", "p0", ScoutStreams.Hash("p0"), StreamMoment.None,
                AgentResult.NotCaptured with { Captured = true, ResolvedModel = Sonnet },
                ChildTrace.Nothing with { MessageModels = [Sonnet], StreamUsage = child })],
            Envelope: new ResultEnvelope(true, false, "success", CapturedCount.Number(1), CapturedUsd.Amount(reportedUsd), main,
                [new ModelUsageEntry(Opus, 4, 1_605, 12_616, 16_296, 0m), new ModelUsageEntry(Sonnet, 104, 31_113, 1_749_236, 176_496, 0m)],
                new SubagentStats(true, 1, 1, 0, 0)));
    }
}
