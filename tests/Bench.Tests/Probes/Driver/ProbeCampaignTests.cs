using System.Text.Json.Nodes;
using Bench.Application.Gate;
using Bench.Domain.Probes;
using Bench.Domain.Runs;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Probes.Driver;

/// <summary>S2 acceptance 6 (§9 <i>Driver</i>) through the rig: a scripted run settles, a hanging cell times out and the lane goes
/// on, a quota marker stops the campaign <c>AccountOut</c> with exactly the right cells Pending and a second campaign finishes
/// them in fresh attempts; the pin per claim is the fake's real <c>--version</c> (D9); <c>PrepareAsync</c> sweeps a dead owner
/// and deletes every stranded fixture directory but a live owner's (finding 3).</summary>
[Collection("postgres")]
public sealed class ProbeCampaignTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly IReadOnlyList<ProbeKind> ThreeProbes = [ProbeKind.ReadInside, ProbeKind.ReadOutsideBare, ProbeKind.WebSearch];

    [Fact]
    public async Task A_scripted_run_settles_every_cell_of_every_subject_each_under_the_build_that_answered()
    {
        await using var rig = await ProbeDriverRig.StartAsync(postgres);
        rig.AddSubject("claude-fake", "claude", new JsonObject { ["readInside"] = true, ["webSearch"] = true });
        rig.AddSubject("codex-fake", "codex", new JsonObject { ["readInside"] = true, ["readOutside"] = true });
        var (run, cells) = await rig.PlanAsync(ThreeProbes, repeats: 2);
        var lines = new List<string>();

        var report = await rig.CampaignAsync(run, lines.Add);

        report.Stop.Should().Be(CampaignStop.Drained, report.Reason);
        report.Settled.Should().Be(cells.Count);
        var stored = await rig.NewStore().CellsAsync(run.Id, Ct);
        stored.Should().OnlyContain(c => c.State == CellState.Settled && c.Attempts == 1 && c.Facts.Kind == ProbeAttemptKind.Answered);
        stored.Should().OnlyContain(c => c.Pin.VersionText == "fake-cli 1.0.0-fake", "every claim carries the build that answered --version (D9), read by the real pin reader");
        stored.Where(c => c.Probe == ProbeKind.ReadInside).Should().OnlyContain(c => c.Facts.CanaryRead == ProbeFact.Yes);
        stored.Where(c => c.Probe == ProbeKind.ReadOutsideBare && c.Subject.Value == "claude-fake").Should().OnlyContain(c => c.Facts.CanaryRead == ProbeFact.NotCaptured,
            "this fake declines without a single call while Read is offered — a missing canary with no stop in the transcript is not confinement (S2c)");
        stored.Where(c => c.Probe == ProbeKind.ReadOutsideBare && c.Subject.Value == "codex-fake").Should().OnlyContain(c => c.Facts.CanaryRead == ProbeFact.Yes, "this fake reads anywhere");
        stored.Where(c => c.Probe == ProbeKind.WebSearch && c.Subject.Value == "claude-fake").Should().OnlyContain(c => c.Facts.AnswerCurrent == ProbeFact.Yes && c.Facts.ToolEvidence == ProbeFact.Yes);
        stored.Where(c => c.Probe == ProbeKind.WebSearch && c.Subject.Value == "codex-fake").Should().OnlyContain(c => c.Facts.ToolEvidence == ProbeFact.No, "no web_search item in its transcript");
        stored.Should().OnlyContain(c => c.Artifacts.Count == 6, "stdout, stderr, argv and prompt (the raw evidence), then answer and tools per cell (S2b)");
        lines.Should().HaveCount(cells.Count).And.OnlyContain(l => l.StartsWith("settled", StringComparison.Ordinal));
        var runRoot = Path.Combine(rig.WorkRoot, "probes", run.Id.ToString("D"));
        (Directory.Exists(runRoot) ? Directory.EnumerateDirectories(runRoot) : []).Should().BeEmpty("every fixture is deleted after its attempt");
    }

    [Fact]
    public async Task A_cell_whose_cli_hangs_past_the_wall_settles_timed_out_and_the_lane_goes_on()
    {
        await using var rig = await ProbeDriverRig.StartAsync(postgres);
        rig.Wall = TimeSpan.FromSeconds(2);
        rig.AddSubject("claude-fake", "claude", new JsonObject { ["hangOnCall"] = 1 });
        var (run, cells) = await rig.PlanAsync([ProbeKind.ReadInside, ProbeKind.ReadOutsideBare], repeats: 1);

        var report = await rig.CampaignAsync(run);

        report.Stop.Should().Be(CampaignStop.Drained, report.Reason);
        var stored = await rig.NewStore().CellsAsync(run.Id, Ct);
        stored.Should().HaveCount(cells.Count).And.OnlyContain(c => c.State == CellState.Settled);
        stored.Select(c => c.Facts.Kind).Should().BeEquivalentTo([ProbeAttemptKind.TimedOut, ProbeAttemptKind.Answered], "the first call hung and was killed at the wall; the second answered");
        stored.Single(c => c.Facts.Kind == ProbeAttemptKind.TimedOut).Facts.ExitCode.WasCaptured.Should().BeFalse();
    }

    [Fact]
    public async Task A_quota_marker_stops_the_campaign_AccountOut_with_exactly_the_right_cells_pending_and_a_second_campaign_finishes_them()
    {
        await using var rig = await ProbeDriverRig.StartAsync(postgres);
        var outSubject = rig.AddSubject("codex-fake", "codex", new JsonObject { ["quotaFrom"] = 2 });
        rig.AddSubject("claude-fake", "claude", new JsonObject());
        var (run, cells) = await rig.PlanAsync([ProbeKind.ReadInside, ProbeKind.ReadOutsideBare], repeats: 2);

        var report = await rig.CampaignAsync(run);

        report.Stop.Should().Be(CampaignStop.AccountOut, report.Reason);
        report.Reason.Should().Contain("codex-fake").And.Contain("AccountOut").And.Contain("resume");
        report.Benched.Keys.Select(k => k.Value).Should().Equal(["codex-fake"]);
        var stored = await rig.NewStore().CellsAsync(run.Id, Ct);
        stored.Where(c => c.Subject.Value == "claude-fake").Should().OnlyContain(c => c.State == CellState.Settled, "the other lane went on to the end");
        var outCells = stored.Where(c => c.Subject.Value == "codex-fake").OrderBy(c => c.Slot).ToList();
        outCells[0].State.Should().Be(CellState.Settled, "the first call answered");
        outCells.Skip(1).Should().OnlyContain(c => c.State == CellState.Pending, "an empty account is not a measurement — its cells wait for a resume");
        outCells[1].Attempts.Should().Be(1, "the cell that met the quota keeps its attempt counted (D8)");
        outCells[1].Facts.Kind.Should().Be(ProbeAttemptKind.Unmeasured);
        outCells[1].Reason.Should().Be(ProbeReason.AccountOut);
        outCells.Skip(2).Should().OnlyContain(c => c.Attempts == 0, "benched FIRST — no later cell of the subject was claimed");
        rig.Fake(outSubject).Calls().Should().HaveCount(2, "one answer, one quota; nothing after the bench");

        rig.Fake(outSubject).Rewrite(new JsonObject()); // the account is back
        var resumed = await rig.CampaignAsync(run);

        resumed.Stop.Should().Be(CampaignStop.Drained, resumed.Reason);
        resumed.Settled.Should().Be(3, "exactly the cells that were pending");
        var after = await rig.NewStore().CellsAsync(run.Id, Ct);
        after.Should().HaveCount(cells.Count).And.OnlyContain(c => c.State == CellState.Settled);
        after.Where(c => c.Subject.Value == "codex-fake").OrderBy(c => c.Slot).Select(c => c.Attempts).Should().Equal([1, 2, 1, 1], "the cell that met the quota runs again in attempt 2 — a fresh directory, never its first one");
    }

    [Fact]
    public async Task Prepare_sweeps_a_dead_owner_and_deletes_every_stranded_fixture_but_a_live_owners()
    {
        await using var rig = await ProbeDriverRig.StartAsync(postgres);
        var subject = rig.AddSubject("claude-fake", "claude");
        var (run, cells) = await rig.PlanAsync([ProbeKind.ReadInside, ProbeKind.ReadOutsideBare, ProbeKind.WebSearch], repeats: 1);
        var store = rig.NewStore();
        var live = (await store.ClaimNextAsync(run.Id, subject.Id, WorkerIdentity.Here("alive-lane"), ProbeStoreFixtures.Pin(), Ct)).Ok();
        var dead = (await store.ClaimNextAsync(run.Id, subject.Id, TestWorkers.Dead("dead-lane"), ProbeStoreFixtures.Pin(), Ct)).Ok();
        var pending = cells.Single(c => c.Id != live.Id && c.Id != dead.Id);
        var liveRoot = rig.Fixtures.Begin(ProbeKind.ReadInside, new ProbeAttemptScope(run.Id, live.Id, 1, 1)).Ok().Root;
        var deadRoot = rig.Fixtures.Begin(ProbeKind.ReadInside, new ProbeAttemptScope(run.Id, dead.Id, 1, 1)).Ok().Root;
        var pendingRoot = rig.Fixtures.Begin(ProbeKind.ReadInside, new ProbeAttemptScope(run.Id, pending.Id, 1, 1)).Ok().Root;
        var otherRun = rig.Fixtures.Begin(ProbeKind.ReadInside, new ProbeAttemptScope(Guid.CreateVersion7(), Guid.CreateVersion7(), 1, 1)).Ok().Root;

        // Staleness is a margin, not a death certificate (WorkerIdentity): with no margin every claim is a candidate, and the
        // ownership check alone decides — the dead pid is handed back, the live one is kept.
        var prepared = await rig.PrepareAsync(run, staleAfter: TimeSpan.Zero);

        prepared.Sweep.Requeued.Should().Be(1, "the dead owner's claim is handed back");
        prepared.StrandedFixturesDeleted.Should().Be(2, "the Pending cell's leftover and the dead owner's");
        Directory.Exists(pendingRoot).Should().BeFalse("Pending included (finding 3)");
        Directory.Exists(deadRoot).Should().BeFalse();
        Directory.Exists(liveRoot).Should().BeTrue("a live owner on this host is still measuring it");
        Directory.Exists(otherRun).Should().BeTrue("another run's root is never touched");
        (await store.CellAsync(dead.Id, Ct)).Ok().State.Should().Be(CellState.Pending);
        (await store.CellAsync(live.Id, Ct)).Ok().State.Should().Be(CellState.Claimed);
    }
}
