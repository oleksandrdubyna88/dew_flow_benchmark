using Bench.Application.Probes;
using Bench.Contracts;
using Bench.Domain.Probes;
using Bench.Domain.Runs;
using Bench.Domain.Trace;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Infrastructure.ProbeStoreFixtures;

namespace Bench.Tests.Probes;

/// <summary>The report's reading of a run, pure: the highest SETTLED generation per lineage (D2), the §4 control applied per
/// subject, every fact a word and never a zero, a pruned run flagged unauditable, and a rerun command per row.</summary>
public sealed class ProbeReportTests
{
    private static readonly ProbeSubject Claude = Subject("claude-a", "claude");

    [Fact]
    public void A_pending_regeneration_does_not_hide_the_settled_verdict_it_re_measures()
    {
        var (run, cells) = Matrix([ProbeKind.ReadInside]);
        var g1 = Settled(cells[0], ProbeFact.Yes);
        var g2 = ProbeCellLifecycle.NextGeneration([g1], Guid.CreateVersion7()).Ok();

        var row = ProbeReport.Of(run, [g1, g2]).Cells.Single();

        row.Should().Match<ProbeCellReportDto>(r => r.CellId == g1.Id && r.Generation == 1 && r.State == ProbeWords.Settled
            && r.LatestGeneration == 2 && r.LatestState == ProbeWords.Pending && r.Facts.CanaryRead == ProbeWords.Yes);
    }

    [Fact]
    public void The_highest_settled_generation_wins_over_a_lower_one()
    {
        var (run, cells) = Matrix([ProbeKind.ReadInside]);
        var g1 = Settled(cells[0], ProbeFact.No);
        var g2 = Settled(ProbeCellLifecycle.NextGeneration([g1], Guid.CreateVersion7()).Ok(), ProbeFact.Yes);

        ProbeReport.Of(run, [g1, g2]).Cells.Single().Should().Match<ProbeCellReportDto>(r => r.Generation == 2 && r.Facts.CanaryRead == ProbeWords.Yes);
    }

    [Fact]
    public void A_read_inside_no_voids_every_read_probe_of_that_subject_and_says_so()
    {
        var other = Subject("codex-b", "codex");
        var (run, cells) = Matrix([ProbeKind.ReadInside, ProbeKind.ReadOutsideBare, ProbeKind.WebSearch], other);
        var settled = cells.Select(c => Settled(c, c.Subject == Claude.Id && c.Probe == ProbeKind.ReadInside ? ProbeFact.No : ProbeFact.Yes)).ToList();

        var report = ProbeReport.Of(run, settled);

        var claudeOutside = report.Cells.Single(c => c.Subject == "claude-a" && c.Probe == "read-outside-bare");
        claudeOutside.VoidedByControl.Should().BeTrue();
        claudeOutside.Facts.CanaryRead.Should().Be(ProbeWords.NotCaptured, "a model that cannot read inside its own directory says nothing about outside it");
        report.Cells.Single(c => c.Subject == "claude-a" && c.Probe == "read-inside").Facts.CanaryRead.Should().Be(ProbeWords.No, "the control itself is shown as measured");
        report.Cells.Single(c => c.Subject == "claude-a" && c.Probe == "web-search").VoidedByControl.Should().BeFalse("not a read probe");
        report.Cells.Single(c => c.Subject == "codex-b" && c.Probe == "read-outside-bare").Facts.CanaryRead.Should().Be(ProbeWords.Yes, "another subject's control voids nothing here");
    }

    [Fact]
    public void Every_attempt_kind_and_missing_exit_code_is_a_word_never_a_zero()
    {
        var (run, cells) = Matrix([ProbeKind.ReadInside, ProbeKind.ReadOutsideBare]);
        var timedOut = Settle(cells[0], ProbeFacts.NothingCaptured(ProbeAttemptKind.TimedOut, CapturedCount.Unavailable("the wall")));
        var refused = Settle(cells[1], ProbeFacts.NothingCaptured(ProbeAttemptKind.LaunchRefused, CapturedCount.Number(2)));

        var rows = ProbeReport.Of(run, [timedOut, refused]).Cells;

        rows[0].Should().Match<ProbeCellReportDto>(r => r.Kind == "timed-out" && !r.Exit.Captured && r.Facts.CanaryRead == ProbeWords.NotCaptured);
        rows[1].Should().Match<ProbeCellReportDto>(r => r.Kind == "launch-refused" && r.Exit.Captured && r.Exit.Code == 2);
        rows.Should().OnlyContain(r => r.RerunCommand == ProbeWords.RerunPrefix + r.CellId.ToString("D") && r.Pin.Version == "2.1.286");
    }

    [Fact]
    public void A_lineage_with_nothing_settled_shows_where_it_stands_and_the_run_is_open()
    {
        var (run, cells) = Matrix([ProbeKind.ReadInside]);

        var report = ProbeReport.Of(run, cells);

        report.Progress.Should().Be(new ProbeProgressDto(1, 0, 0, 0, true));
        report.Cells.Single().Should().Match<ProbeCellReportDto>(r => r.State == ProbeWords.Pending && r.Kind == "none" && r.Pin.Version.Length == 0);
    }

    [Fact]
    public void A_pruned_run_is_reported_unauditable_and_the_oracle_and_subjects_are_the_frozen_ones()
    {
        var (run, cells) = Matrix([ProbeKind.ReadInside]);

        var report = ProbeReport.Of(run with { ArtifactsPruned = true }, cells);

        report.Should().Match<ProbeRunReportDto>(r => r.ArtifactsPruned && !r.Auditable && r.Oracle == new ProbeOracleDto("0.52.0", "registry"));
        report.Subjects.Single().Should().Be(new ProbeSubjectDto("claude-a", "claude", "model-x", "BENCH_CLAUDE", "denylist", string.Empty, string.Empty, string.Empty),
            "the confinement mode travels with the frozen subject (S2b)");
    }

    [Fact]
    public void The_report_names_every_pair_the_planner_dropped_with_a_reason_word_recomputed_from_the_frozen_subjects()
    {
        var agy = Subject("agy-c", "antigravity");
        var api = Subject("grok-d", "api");
        var (run, plan, cells) = PlannedMatrix([ProbeKind.ReadInside, ProbeKind.ReadDenied, ProbeKind.ApiReachable], Claude, agy, api);

        var dropped = ProbeReport.Of(run, cells).Dropped;

        dropped.Should().Equal(
            new ProbeDroppedPairDto("read-inside", "grok-d", "cli-probe-on-api"),
            new ProbeDroppedPairDto("read-denied", "agy-c", "no-web-off-flag"),
            new ProbeDroppedPairDto("read-denied", "grok-d", "cli-probe-on-api"),
            new ProbeDroppedPairDto("api-reachable", "claude-a", "api-probe-on-cli"),
            new ProbeDroppedPairDto("api-reachable", "agy-c", "api-probe-on-cli"));
        dropped.Select(d => $"{d.Probe} × {d.Subject}").Should().Equal(plan.Dropped.Select(d => $"{ProbeWord.Of(d.Probe)} × {d.Subject}"),
            "the report names exactly the pairs the planner printed as not measured when the run was made");
    }

    [Fact]
    public void A_probe_the_run_never_planned_is_not_reported_as_dropped()
    {
        var api = Subject("grok-d", "api");
        var (run, _, cells) = PlannedMatrix([ProbeKind.ReadInside], Claude, api);

        ProbeReport.Of(run, cells).Dropped.Should().Equal([new ProbeDroppedPairDto("read-inside", "grok-d", "cli-probe-on-api")],
            "api-reachable was not asked for (--probes read-inside), so claude × api-reachable was never the planner's to drop");
    }

    [Fact]
    public void A_probe_every_subject_dropped_plans_no_cell_and_is_still_named_from_the_probes_the_run_froze()
    {
        var (run, _, cells) = PlannedMatrix([ProbeKind.ReadInside, ProbeKind.ApiReachable], Claude);

        var dropped = ProbeReport.Of(run with { Probes = [ProbeKind.ReadInside, ProbeKind.ApiReachable] }, cells).Dropped;

        cells.Should().NotContain(c => c.Probe == ProbeKind.ApiReachable, "no subject of this run can be asked api-reachable");
        dropped.Should().Equal([new ProbeDroppedPairDto("api-reachable", "claude-a", "api-probe-on-cli")],
            "a probe with no cell at all is the clearest 'not measured' of all, and the cells alone cannot name it");
    }

    private static (ProbeRun Run, IReadOnlyList<ProbeCell> Cells) Matrix(IReadOnlyList<ProbeKind> probes, params ProbeSubject[] more)
    {
        var (run, _, cells) = PlannedMatrix(probes, [Claude, .. more]);
        return (run with { Repeats = 1 }, [.. cells.Where(c => c.Repeat == 1)]);
    }

    private static ProbeCell Settled(ProbeCell cell, ProbeFact canary) =>
        Settle(cell, ProbeFacts.Answered(CapturedCount.Number(0)) with { CanaryRead = canary });

    private static ProbeCell Settle(ProbeCell cell, ProbeFacts facts)
    {
        var claimed = ProbeCellLifecycle.Claim(cell, WorkerIdentity.Here("t"), Noon, Pin()).Ok();
        return ProbeCellLifecycle.Settle(claimed, new ProbeSettlement(facts, [])).Ok();
    }
}
