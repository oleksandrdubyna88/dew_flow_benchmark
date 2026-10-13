using Bench.Domain.Probes;
using Bench.Domain.Scouts;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Scouts.ScoutStreams;

namespace Bench.Tests.Scouts;

/// <summary>The cost calculator and the 2 % check of §3.6, against a cell computed BY HAND: an Opus main session, two Sonnet scouts and
/// the CLI's internal Haiku call, at made-up prices chosen so every product is an integer number of micro-dollars.</summary>
public sealed class ScoutCostTests
{
    private const string Haiku = "claude-haiku-4-5-20251001";

    private static readonly PriceTable Prices = PriceTable.Of("https://vendor.example/pricing", new DateOnly(2026, 10, 12),
    [
        ModelPrice.Of(Opus, 5m, 25m, 0.5m, 6.25m, 10m).Ok(),
        ModelPrice.Of(Sonnet, 2m, 10m, 0.2m, 2.5m, 4m).Ok(),
        ModelPrice.Of(Haiku, 1m, 5m, 0.1m, 1.25m, 2m).Ok(),
    ]).Ok();

    /// <summary>main = 4×5 + 400×25 + 40 000×0.5 + 20 000×10 (1-hour writes) = 230 020 µ$; Sonnet team = 4×2 + 900×10 + 11 000×0.2 +
    /// 4 000×2.5 = 21 208 µ$ (its output 900 from modelUsage — the stream said 38); Haiku = 1 000×1 + 10×5 = 1 050 µ$. Total $0.252278.</summary>
    [Fact]
    public void A_hand_computed_cell_is_priced_to_the_micro_dollar()
    {
        var cost = ScoutCost.Of(ScoutTokens.Attribute(TwoSonnetScouts()).Ok(), Prices).Ok();

        cost.MainUsd.Should().Be(0.230020m);
        cost.Teams.Should().Equal(new TeamCostLine(Sonnet, 0.021208m));
        cost.InternalUsd.Should().Be(0.001050m);
        cost.TotalUsd.Should().Be(0.252278m);
        cost.Scouts.Should().OnlyContain(s => !s.Usd.WasCaptured, "two scouts share Sonnet's output total — their own costs are not separable, and not guessed");
    }

    [Fact]
    public void A_lone_scout_on_the_main_sessions_model_gets_its_output_by_subtracting_the_main_sessions_own()
    {
        var oneOpus = Transcript(
            main: new TokenUsage(4, 400, 40_000, 0, 20_000),
            children: [(Opus, new TokenUsage(10, 35, 79_310, 28_277, 0))],
            modelUsage: [new ModelUsageEntry(Opus, 14, 1_842, 119_310, 48_277, 0m)]);

        var usage = ScoutTokens.Attribute(oneOpus).Ok();

        usage.Scouts.Single().Should().Be(new ScoutUsage("call-0", Opus, new TokenUsage(10, 1_442, 79_310, 28_277, 0), OutputSeparable: true));
        ScoutCost.Of(usage, Prices).Ok().Scouts.Single().Usd.Should().Be(CapturedUsd.Amount((10 * 5m + 1_442 * 25m + 79_310 * 0.5m + 28_277 * 6.25m) / 1_000_000m));
    }

    [Fact]
    public void The_computed_cost_agrees_within_two_percent_and_a_planted_mismatch_stops_the_run()
    {
        ScoutCost.Check(0.252278m, CapturedUsd.Amount(0.2523m)).Should().BeOfType<CostCheck.Agrees>();
        ScoutCost.Check(0.252278m, CapturedUsd.Amount(0.252278m * 1.019m)).Should().BeOfType<CostCheck.Agrees>();
        ScoutCost.Check(0.252278m, CapturedUsd.Amount(0.252278m * 1.03m)).Should().BeOfType<CostCheck.Disagrees>();

        var planted = ScoutCost.Check(0.252278m, CapturedUsd.Amount(0.30m));

        planted.Should().BeOfType<CostCheck.Disagrees>().Which.Gap.Should().BeApproximately(0.159m, 0.001m);
        ScoutCost.Tolerance.Should().Be(0.02m);
    }

    [Fact]
    public void Without_a_reported_cost_there_is_nothing_to_check_and_that_is_not_an_agreement()
    {
        ScoutCost.Check(0.25m, CapturedUsd.Unavailable("no total_cost_usd")).Should().BeOfType<CostCheck.Unchecked>();
    }

    [Fact]
    public void A_model_the_price_table_does_not_price_is_refused_by_name()
    {
        var table = PriceTable.Of("https://vendor.example/pricing", new DateOnly(2026, 10, 12), [ModelPrice.Of(Opus, 5m, 25m, 0.5m, 6.25m, 10m).Ok()]).Ok();

        ScoutCost.Of(ScoutTokens.Attribute(TwoSonnetScouts()).Ok(), table).Reason().Should().Contain($"no price for '{Sonnet}'");
        PriceTable.Of("http://vendor.example", new DateOnly(2026, 10, 12), []).Reason().Should().Contain("https");
        ModelPrice.Of(Opus, -1m, 0m, 0m, 0m, 0m).Reason().Should().Contain("negative");
    }

    [Fact]
    public void An_envelope_whose_model_usage_lacks_a_scouts_model_cannot_be_attributed()
    {
        var missing = Transcript(new TokenUsage(4, 400, 40_000, 0, 20_000), [(Sonnet, TokenUsage.Zero)], [new ModelUsageEntry(Opus, 4, 400, 40_000, 20_000, 0m)]);

        ScoutTokens.Attribute(missing).Reason().Should().Contain($"no line for '{Sonnet}'");
    }

    private static SubagentTranscript TwoSonnetScouts() => Transcript(
        main: new TokenUsage(4, 400, 40_000, 0, 20_000),
        children: [(Sonnet, new TokenUsage(2, 16, 5_000, 3_000, 0)), (Sonnet, new TokenUsage(2, 22, 6_000, 1_000, 0))],
        modelUsage:
        [
            new ModelUsageEntry(Opus, 4, 400, 40_000, 20_000, 0m),
            new ModelUsageEntry(Sonnet, 4, 900, 11_000, 4_000, 0m),
            new ModelUsageEntry(Haiku, 1_000, 10, 0, 0, 0m),
        ]);

    private static SubagentTranscript Transcript(TokenUsage main, IReadOnlyList<(string Model, TokenUsage Stream)> children, IReadOnlyList<ModelUsageEntry> modelUsage) =>
        new(
            Complete: true,
            MainModel: Opus,
            CliVersion: "2.1.289",
            WorkingDirectory: Tree,
            MainCalls: [.. children.Select((_, i) => new MainToolCall($"call-{i}", "Agent"))],
            MainStreamUsage: main with { Output = 5 },
            Agents: [.. children.Select((c, i) => new SubagentCall(
                $"call-{i}", ScoutArms.CodeExplorerType, "sonnet", $"p{i}", Hash($"p{i}"), StreamMoment.None,
                AgentResult.NotCaptured with { Captured = true, ResolvedModel = c.Model },
                ChildTrace.Nothing with { MessageModels = [c.Model], StreamUsage = c.Stream }))],
            Envelope: new ResultEnvelope(true, false, "success", CapturedCount.Number(1), CapturedUsd.Amount(0.25m), main, modelUsage, new SubagentStats(true, children.Count, children.Count, 0, 0)));
}
