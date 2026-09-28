using System.Diagnostics;
using System.Text.Json.Nodes;
using Bench.Application.Gate;
using Bench.Domain.Gate;
using Bench.Domain.Runs;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Infrastructure.GateStoreFixtures;

namespace Bench.Tests.Gate.Driver;

/// <summary>S3.8 — the campaign's lanes against the fake product and a real store: the limits enforced and REACHED, no
/// lane blocked at the head of the line, a swept cell restarted fresh, and a product that moves either stopping the run
/// or — when allowed — measured as a new pin while a claimed cell settles under its own.</summary>
[Collection("postgres")]
public sealed class GateCampaignTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_lanes_never_exceed_parallel_or_per_endpoint_and_reach_both_under_enough_work()
    {
        await using var rig = await GateDriverRig.StartAsync(postgres, new JsonObject { ["reviewMs"] = 700 });
        GateReviewer[] reviewers =
        [
            GateDriverRig.Reviewer("rev-a1", "api.vendor-a.example.com"), GateDriverRig.Reviewer("rev-a2", "api.vendor-a.example.com"),
            GateDriverRig.Reviewer("rev-b1", "api.vendor-b.example.com"), GateDriverRig.Reviewer("rev-b2", "api.vendor-b.example.com"),
        ];
        var (run, cells) = await rig.PlanAsync(GateKind.Plan, reviewers, repeats: 3);

        var report = await rig.CampaignAsync(run, reviewers, parallel: 3, perEndpoint: 2);

        report.Settled.Should().Be(cells.Count, report.Reason);
        var sessions = rig.Fake.MaxConcurrent("open", "close", byKey: false)["*"];
        var perEndpoint = rig.Fake.MaxConcurrent("review-start", "review-end", byKey: true).Where(e => e.Key.StartsWith("https://", StringComparison.Ordinal)).ToList();
        sessions.Should().Be(3, "never above --parallel, and a cap that is never reached is indistinguishable from serial execution");
        perEndpoint.Should().HaveCount(2).And.OnlyContain(e => e.Value == 2, "never above --per-endpoint on either endpoint, and reached on both");
    }

    /// <summary>Every reviewer behind ONE endpoint and more lanes than the endpoint takes: the cap, not the lane count, is
    /// what bounds the reviews in flight — and a lane that could not claim waited rather than opening a process.</summary>
    [Fact]
    public async Task The_per_endpoint_cap_holds_when_there_are_more_lanes_than_the_endpoint_takes()
    {
        await using var rig = await GateDriverRig.StartAsync(postgres, new JsonObject { ["reviewMs"] = 700 });
        GateReviewer[] reviewers =
        [
            GateDriverRig.Reviewer("rev-a1", "api.vendor-a.example.com"), GateDriverRig.Reviewer("rev-a2", "api.vendor-a.example.com"),
            GateDriverRig.Reviewer("rev-a3", "api.vendor-a.example.com"), GateDriverRig.Reviewer("rev-a4", "api.vendor-a.example.com"),
        ];
        var (run, cells) = await rig.PlanAsync(GateKind.Plan, reviewers, repeats: 2);

        var report = await rig.CampaignAsync(run, reviewers, parallel: 4, perEndpoint: 2);

        report.Settled.Should().Be(cells.Count, report.Reason);
        rig.Fake.MaxConcurrent("review-start", "review-end", byKey: true)["https://api.vendor-a.example.com/v1"].Should().Be(2,
            "four lanes, one endpoint, a cap of two: never above it, and reached");
        rig.Fake.MaxConcurrent("open", "close", byKey: false)["*"].Should().Be(2, "a lane opens a process only for a cell its endpoint has room for");
    }

    [Fact]
    public async Task A_lane_never_waits_at_the_head_of_the_line_while_another_endpoint_is_idle()
    {
        await using var rig = await GateDriverRig.StartAsync(postgres, new JsonObject { ["reviewMs"] = 1500 });
        GateReviewer[] reviewers =
        [
            GateDriverRig.Reviewer("rev-a1", "api.vendor-a.example.com"), GateDriverRig.Reviewer("rev-a2", "api.vendor-a.example.com"),
            GateDriverRig.Reviewer("rev-b1", "api.vendor-b.example.com"),
        ];
        var (run, _) = await rig.PlanAsync(GateKind.Plan, reviewers, repeats: 1);

        await rig.CampaignAsync(run, reviewers, parallel: 2, perEndpoint: 1);

        var starts = rig.Fake.Events().Where(e => e.Text.StartsWith("review-start ", StringComparison.Ordinal)).Select(e => e.Text).ToList();
        starts.Should().HaveCount(3);
        starts.FindIndex(s => s.Contains("vendor-b", StringComparison.Ordinal)).Should().BeLessThan(
            starts.FindLastIndex(s => s.Contains("vendor-a", StringComparison.Ordinal)),
            "with endpoint A full, the second lane takes B's cell instead of holding A's second cell until the first finishes");
    }

    [Fact]
    public async Task A_swept_cells_next_attempt_runs_in_a_fresh_process_data_directory_and_caller_session_and_the_first_is_kept_and_marked()
    {
        await using var rig = await GateDriverRig.StartAsync(postgres);
        var reviewer = GateDriverRig.Reviewer("rev-a", "api.vendor-a.example.com");
        var (run, cells) = await rig.PlanAsync(GateKind.Plan, [reviewer], repeats: 1);
        var dead = WorkerIdentity.Stored("gate-lane-dead", Environment.MachineName, DeadPid());
        var store = rig.NewStore();
        var claimed = (await store.ClaimNextAsync(run.Id, dead, Pin('a'), Ct)).Ok();
        var first = new ArtifactScope(run, claimed.Id, 1);
        (await rig.Artifacts.BeginAttemptAsync(first, Ct)).Ok();
        var firstData = Path.Combine([rig.Artifacts.Root, .. CellPaths.DataDirFor(first).Segments]);
        await File.WriteAllTextAsync(Path.Combine(firstData, "usage.jsonl"), "{\"outcome\":\"ok\"}\n", Ct);

        (await store.SweepAsync(TimeSpan.Zero, Ct)).Requeued.Should().Be(1);
        var report = await rig.CampaignAsync(run, [reviewer], parallel: 1, perEndpoint: 1);

        report.Settled.Should().Be(1, report.Reason);
        (await rig.NewStore().CellAsync(cells[0].Id, Ct)).Ok().Attempts.Should().Be(2);
        var env = rig.Fake.Events().Should().ContainSingle(e => e.Text.StartsWith("env ", StringComparison.Ordinal), "one process, for the fresh attempt").Subject.Text;
        env.Should().Contain("-a2 ").And.Contain(Path.Combine("attempt-2", "data"));
        File.Exists(Path.Combine(firstData, "usage.jsonl")).Should().BeTrue("the interrupted attempt's artefacts are kept");
        File.Exists(Path.Combine(Path.GetDirectoryName(firstData)!, CellPaths.InterruptedFile)).Should().BeTrue("and marked, never continued");
        (await File.ReadAllTextAsync(Path.Combine(firstData, "usage.jsonl"), Ct)).Should().Be("{\"outcome\":\"ok\"}\n");
    }

    [Fact]
    public async Task A_product_that_moves_mid_run_stops_the_campaign_naming_both_shas()
    {
        await using var rig = await GateDriverRig.StartAsync(postgres);
        var reviewer = GateDriverRig.Reviewer("rev-a", "api.vendor-a.example.com");
        var (run, _) = await rig.PlanAsync(GateKind.Plan, [reviewer], repeats: 2);
        rig.Pins.MoveTo(Pin('b'));

        var report = await rig.CampaignAsync(run, [reviewer], parallel: 1, perEndpoint: 1);

        report.Stop.Should().Be(CampaignStop.ProductMoved);
        report.Reason.Should().Contain(HashText.Short(new string('a', 64))).And.Contain(HashText.Short(new string('b', 64)));
        report.Settled.Should().Be(0);
        (await rig.NewStore().CellsAsync(run.Id, Ct)).Should().OnlyContain(c => c.State == CellState.Pending, "nothing is measured under bytes the campaign was not pinned to");
    }

    [Fact]
    public async Task With_a_product_change_allowed_a_claimed_cell_settles_under_its_own_pin_and_the_next_claims_under_the_new_one()
    {
        await using var rig = await GateDriverRig.StartAsync(postgres, new JsonObject { ["reviewMs"] = 1500 });
        var reviewer = GateDriverRig.Reviewer("rev-a", "api.vendor-a.example.com");
        var (run, cells) = await rig.PlanAsync(GateKind.Plan, [reviewer], repeats: 2, allowProductChange: true);

        var campaign = rig.CampaignAsync(run, [reviewer], parallel: 1, perEndpoint: 1);
        await Eventually(() => rig.Fake.Events().Any(e => e.Text.StartsWith("review-start", StringComparison.Ordinal)));
        rig.Pins.MoveTo(Pin('b'));
        var report = await campaign;

        report.Settled.Should().Be(2, report.Reason);
        report.PinsSeen.Select(p => p.BinarySha256[0]).Should().Equal(['a', 'b']);
        var settled = await rig.NewStore().CellsAsync(run.Id, Ct);
        settled.Single(c => c.Id == cells[0].Id).Pin.BinarySha256.Should().StartWith("a", "a claimed cell finishes under the pin it started with");
        settled.Single(c => c.Id == cells[1].Id).Pin.BinarySha256.Should().StartWith("b", "the next pending cell claims under the new one — a new scope");
    }

    private static async Task Eventually(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition() && clock.Elapsed < TimeSpan.FromSeconds(30))
        {
            await Task.Delay(25, Ct);
        }

        condition().Should().BeTrue();
    }

    /// <summary>A pid that belonged to a process on this machine and no longer does.</summary>
    private static int DeadPid()
    {
        using var process = Process.Start(new ProcessStartInfo(FakeCoai.Executable, "--version") { RedirectStandardOutput = true, UseShellExecute = false })!;
        process.WaitForExit();
        return process.Id;
    }
}
