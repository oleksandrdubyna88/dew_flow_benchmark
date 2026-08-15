using Bench.Domain.Telemetry;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Telemetry;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The query correlation exists for: one leg's server-side cost, split across its phases.
/// <para>
/// A fix task budgets investigate, fix and verify separately. Without this the server's own time and
/// tokens can only be attributed to the whole leg, which makes a phase that blew its ceiling
/// indistinguishable from one that was cheap.
/// </para></summary>
[Collection("postgres")]
public sealed class TelemetryByPhaseTests(PostgresFixture postgres)
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_legs_server_cost_splits_across_its_phases()
    {
        var leg = $"cell-{Guid.CreateVersion7()}";
        var store = new PostgresTelemetryStore(postgres.NewContext());

        await store.AppendAsync(
        [
            Call(leg, "Investigate", serverMs: 100, tokens: 10),
            Call(leg, "Investigate", serverMs: 200, tokens: 20, tool: "rag_search_docs"),
            Call(leg, "Fix", serverMs: 50, tokens: 5),
            Call(leg, "Verify", serverMs: 400, tokens: -1),
        ], Ct);

        var phases = await store.ByPhaseAsync(leg, Ct);

        phases.Select(p => p.Phase).Should().Equal("Fix", "Investigate", "Verify");
        phases.Single(p => p.Phase == "Investigate").ServerMs.Should().Be(300);
        phases.Single(p => p.Phase == "Investigate").Calls.Should().Be(2);
        phases.Single(p => p.Phase == "Verify").ServerMs.Should().Be(400);
    }

    [Fact]
    public async Task Tokens_are_summed_only_over_the_calls_that_reported_them()
    {
        var leg = $"cell-{Guid.CreateVersion7()}";
        var store = new PostgresTelemetryStore(postgres.NewContext());

        await store.AppendAsync(
        [
            Call(leg, "Fix", serverMs: 10, tokens: 40),
            Call(leg, "Fix", serverMs: 20, tokens: -1, tool: "rt_read_local_file"),
        ], Ct);

        var fix = (await store.ByPhaseAsync(leg, Ct)).Single();

        fix.Calls.Should().Be(2);
        fix.Tokens.Should().Be(40);
        fix.CallsWithTokens.Should().Be(
            1, "a total over an unknown denominator is not a measurement — a file read has no tokens, and its absence is not a zero");
    }

    [Fact]
    public async Task Traffic_that_belongs_to_no_leg_is_not_folded_into_one()
    {
        var leg = $"cell-{Guid.CreateVersion7()}";
        var store = new PostgresTelemetryStore(postgres.NewContext());

        await store.AppendAsync(
        [
            Call(leg, "Fix", serverMs: 10, tokens: 1),
            TelemetryCorrelationTests.Call(TelemetryCorrelation.None, tool: "rt_glob", serverMs: 9_999),
        ], Ct);

        var phases = await store.ByPhaseAsync(leg, Ct);

        phases.Should().ContainSingle();
        phases[0].ServerMs.Should().Be(10, "a real session's traffic is the majority, and folding it in would invent the attribution");
    }

    [Fact]
    public async Task Asking_about_a_leg_nobody_declared_is_empty_rather_than_everything()
    {
        var store = new PostgresTelemetryStore(postgres.NewContext());

        (await store.ByPhaseAsync("", Ct)).Should().BeEmpty();
        (await store.ByPhaseAsync($"cell-{Guid.CreateVersion7()}", Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task Outcomes_stay_separated_within_a_phase()
    {
        var leg = $"cell-{Guid.CreateVersion7()}";
        var store = new PostgresTelemetryStore(postgres.NewContext());

        await store.AppendAsync(
        [
            Call(leg, "Fix", serverMs: 1, tokens: -1, outcome: ToolOutcome.Answered),
            Call(leg, "Fix", serverMs: 2, tokens: -1, outcome: ToolOutcome.Refused, tool: "rt_apply_edit_plan"),
            Call(leg, "Fix", serverMs: 3, tokens: -1, outcome: ToolOutcome.Error, tool: "rt_grep"),
        ], Ct);

        var fix = (await store.ByPhaseAsync(leg, Ct)).Single();

        fix.Answered.Should().Be(1);
        fix.Refused.Should().Be(1, "a refused call and an executed one are different facts, and a boolean merges them forever");
        fix.Errored.Should().Be(1);
    }

    private static ToolTelemetry Call(
        string leg, string phase, double serverMs, long tokens,
        string tool = "rag_search", ToolOutcome outcome = ToolOutcome.Answered) =>
        TelemetryCorrelationTests.Call(TelemetryCorrelation.Of(leg, phase), tool, outcome, tokens, serverMs);
}
