using System.Text.Json.Nodes;
using Bench.Application.Gate;
using Bench.Domain.Gate;
using Bench.Domain.Runs;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate.Driver;

/// <summary>T5 of <c>todo/PLAN_gate_measurement_tail.md</c> — the bench half of coai #622. On 2026-09-29 Fable's monthly
/// spend limit ran out mid-campaign and every remaining cell of it settled as a MEASUREMENT (verdict Unknown, "the plan
/// loop never passed"), 44 of them, because a settled cell resets the lane breaker. A reviewer whose account is out is the
/// environment, not a result: its cell is not settled, the reviewer is not claimed again, the others keep running, and the
/// run stays resumable for when the account is back.</summary>
[Collection("postgres")]
public sealed class AReviewerWhoseAccountRanOutTests(PostgresFixture postgres)
{
    private const string SpendLimit = "exit 1: You've hit your monthly spend limit. Switch to another model to continue. (HTTP 429)";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Is_benched_its_cells_stay_pending_and_the_reviewer_still_answering_is_measured()
    {
        await using var rig = await GateDriverRig.StartAsync(postgres, AccountOut("rev-out"));
        GateReviewer[] reviewers = [GateDriverRig.Reviewer("rev-ok", "api.vendor-a.example.com"), GateDriverRig.Reviewer("rev-out", "api.vendor-b.example.com")];
        var (run, _) = await rig.PlanAsync(GateKind.Plan, reviewers, repeats: 3);

        var report = await rig.CampaignAsync(run, reviewers, parallel: 2, perEndpoint: 1);

        var cells = await rig.NewStore().CellsAsync(run.Id, Ct);
        cells.Where(c => c.Reviewer.Value == "rev-ok").Should().OnlyContain(c => c.State == CellState.Settled, "the reviewer still answering is measured to the end");
        var outCells = cells.Where(c => c.Reviewer.Value == "rev-out").ToList();
        outCells.Should().OnlyContain(c => c.State == CellState.Pending, "an empty account is not a measurement — its cells wait for a resume");
        outCells.Count(c => c.Attempts == 1).Should().Be(1, "one cell met the empty account; the reviewer was benched before another was claimed");
        report.Stop.Should().Be(CampaignStop.AccountOut, report.Reason);
        report.Reason.Should().Contain("rev-out").And.Contain("monthly spend limit");
    }

    /// <summary>The window the bench-before-requeue order closes. Lane 1 measures the reviewer still answering while
    /// lane 2 meets the empty account; the out reviewer's endpoint has room for two, so the moment lane 2's cell is
    /// Pending again, lane 1 — just freed, claiming in plan order — would take that same cell for a second attempt,
    /// unless the reviewer was benched first. Proved 2026-09-30: with the requeue moved ahead of the bench (and the window held open for 3 s) this goes red, a rev-out cell at attempt 2.</summary>
    [Fact]
    public async Task No_cell_of_it_is_attempted_twice_in_one_campaign()
    {
        await using var rig = await GateDriverRig.StartAsync(postgres, AccountOut("rev-out"));
        GateReviewer[] reviewers = [GateDriverRig.Reviewer("rev-ok", "api.vendor-a.example.com"), GateDriverRig.Reviewer("rev-out", "api.vendor-b.example.com")];
        var (run, _) = await rig.PlanAsync(GateKind.Plan, reviewers, repeats: 3);

        var report = await rig.CampaignAsync(run, reviewers, parallel: 2, perEndpoint: 2);

        var cells = await rig.NewStore().CellsAsync(run.Id, Ct);
        cells.Where(c => c.Reviewer.Value == "rev-out").Should().OnlyContain(c => c.State == CellState.Pending && c.Attempts <= 1, report.Reason);
        cells.Where(c => c.Reviewer.Value == "rev-ok").Should().OnlyContain(c => c.State == CellState.Settled);
        report.Stop.Should().Be(CampaignStop.AccountOut, report.Reason);
    }

    [Fact]
    public async Task A_resume_once_the_account_is_back_measures_the_requeued_cells_in_fresh_attempts()
    {
        await using var rig = await GateDriverRig.StartAsync(postgres, AccountOut("rev-out"));
        GateReviewer[] reviewers = [GateDriverRig.Reviewer("rev-out", "api.vendor-b.example.com")];
        var (run, _) = await rig.PlanAsync(GateKind.Plan, reviewers, repeats: 2);
        await rig.CampaignAsync(run, reviewers, parallel: 1, perEndpoint: 1);

        await File.WriteAllTextAsync(rig.Fake.ScriptPath, "{}", Ct); // the account is back
        var resumed = await rig.CampaignAsync(run, reviewers, parallel: 1, perEndpoint: 1);

        resumed.Settled.Should().Be(2, resumed.Reason);
        var cells = await rig.NewStore().CellsAsync(run.Id, Ct);
        cells.Should().OnlyContain(c => c.State == CellState.Settled);
        cells.Select(c => c.Attempts).Should().BeEquivalentTo([2, 1], "the cell that met the empty account runs again in attempt 2 — a fresh directory, never its first one");
    }

    private static JsonObject AccountOut(string reviewer) => new() { ["accountOut"] = new JsonObject { [reviewer] = SpendLimit } };
}
