using Bench.Domain.Gate;
using Bench.Domain.Scouts;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Scouts.ScoutAnalysisFixtures;

namespace Bench.Tests.Scouts;

/// <summary>§3.6's arithmetic over hand-built cells: wrong claims POOLED per task over its runs, recall averaged per task first, the
/// not-measured rule (more than 10 % silent cells, or claims left unassessed), unresolved verdicts out of the denominator, only scored cells
/// measured, and the workload pairs built over the tasks both arms have — clustered by repository.</summary>
public sealed class ScoutAnalysisTests
{
    private static readonly ScoutArmId[] W1 = [ScoutArmId.A1, ScoutArmId.B1];

    [Fact]
    public void A_tasks_wrong_claims_are_pooled_over_its_runs_so_a_run_with_one_claim_weighs_less_than_a_run_with_ten()
    {
        // Hand table — task t01, arm A1, two runs:
        //   run 1: 1 wrong, 1 right, 1 unresolved → decided 2
        //   run 2: 0 wrong, 10 right               → decided 10
        //   pooled = (1 + 0) / (2 + 10) × 10 = 0.8333…      (the mean of per-run rates would be (5 + 0) / 2 = 2.5)
        var cells = new[]
        {
            Cell("t01", ScoutArmId.A1, repeat: 1, verdicts: Verdicts(wrong: 1, right: 1, unresolved: 1)),
            Cell("t01", ScoutArmId.A1, repeat: 2, verdicts: Verdicts(wrong: 0, right: 10)),
        };

        var rate = ScoutAnalysis.TaskArms(cells, W1).Single().Wrong;

        rate.Measured.Should().BeTrue();
        rate.PerTen.Should().BeApproximately(10m / 12m, 0.000001m);
    }

    [Fact]
    public void Recall_is_averaged_per_task_first_and_then_over_tasks()
    {
        // Hand table — arm A1:
        //   t01: runs 1/2 and 2/2 → task mean 0.75
        //   t02: one run 1/4      → 0.25
        //   arm recall = (0.75 + 0.25) / 2 = 0.5   (a mean over the three cells would be (0.5 + 1 + 0.25) / 3 = 0.5833)
        var cells = new[]
        {
            Cell("t01", ScoutArmId.A1, repeat: 1, found: 1, total: 2),
            Cell("t01", ScoutArmId.A1, repeat: 2, found: 2, total: 2),
            Cell("t02", ScoutArmId.A1, found: 1, total: 4),
        };

        var analysis = ScoutAnalysis.Of(cells, [ScoutArmId.A1], BootstrapSettings.Default);

        analysis.TaskArms.Select(t => t.Recall.Value).Should().Equal(0.75, 0.25);
        analysis.Arms.Single().Recall.Value.Should().Be(0.5);
    }

    [Fact]
    public void More_than_ten_percent_silent_cells_make_the_arms_wrong_claims_not_measured_and_ten_percent_exactly_does_not()
    {
        IReadOnlyList<ScoutCellEvidence> Arm(int silent) =>
            [.. Enumerable.Range(1, 10).Select(i => Cell($"t{i:00}", ScoutArmId.A1, silent: i <= silent))];

        var twoOfTen = ScoutAnalysis.Of(Arm(2), [ScoutArmId.A1], BootstrapSettings.Default).Arms.Single();
        var oneOfTen = ScoutAnalysis.Of(Arm(1), [ScoutArmId.A1], BootstrapSettings.Default).Arms.Single();

        twoOfTen.WrongGap.Should().Be(ScoutWrongGap.SilentCells);
        twoOfTen.WrongPerTen.State.Should().Be(FigureState.Withheld, "not measured is never a number");
        twoOfTen.Counts.SilentCells.Should().Be(2);
        oneOfTen.WrongGap.Should().Be(ScoutWrongGap.None, "10 % is not MORE than 10 %");
        oneOfTen.WrongPerTen.Value.Should().Be(5.0, "the nine cells with a rate each have 1 wrong of 2");
    }

    [Fact]
    public void A_cell_whose_claims_are_all_unresolved_has_no_rate_and_counts_as_silent()
    {
        var cells = Enumerable.Range(1, 5).Select(i => Cell($"t{i:00}", ScoutArmId.A1, verdicts: i == 1 ? Verdicts(0, 0, unresolved: 3) : null)).ToList();

        var arm = ScoutAnalysis.Of(cells, [ScoutArmId.A1], BootstrapSettings.Default).Arms.Single();

        arm.Counts.Should().Match<ScoutArmCounts>(c => c.SilentCells == 1 && c.Unresolved == 3 && c.Decided == 8 && c.Wrong == 4);
        arm.WrongGap.Should().Be(ScoutWrongGap.SilentCells, "one of five is 20 %");
    }

    [Fact]
    public void Claims_still_waiting_for_a_verdict_leave_the_wrong_claim_result_not_measured()
    {
        var cells = new[] { Cell("t01", ScoutArmId.A1, unassessed: 3), Cell("t02", ScoutArmId.A1) };

        var arm = ScoutAnalysis.Of(cells, [ScoutArmId.A1], BootstrapSettings.Default).Arms.Single();

        arm.WrongGap.Should().Be(ScoutWrongGap.ClaimsUnassessed);
        arm.Counts.Unassessed.Should().Be(3);
    }

    [Fact]
    public void Only_scored_cells_carry_numbers_and_every_other_outcome_is_counted()
    {
        var cells = new[]
        {
            Cell("t01", ScoutArmId.A1, found: 2, total: 2),
            Cell("t02", ScoutArmId.A1, outcome: ScoutCellOutcome.Void),
            Cell("t03", ScoutArmId.A1, outcome: ScoutCellOutcome.Stopped),
            Cell("t04", ScoutArmId.A1, outcome: ScoutCellOutcome.Aborted),
        };

        var arm = ScoutAnalysis.Of(cells, [ScoutArmId.A1], BootstrapSettings.Default).Arms.Single();

        arm.Counts.Should().Match<ScoutArmCounts>(c => c.Cells == 4 && c.Scored == 1 && c.Void == 1 && c.Stopped == 1 && c.Aborted == 1);
        arm.Recall.Value.Should().Be(1.0, "the void, stopped and aborted cells have no recall to average");
        arm.CallerLoad.Value.Should().Be(2, "src/A.cs and src/a.cs are one file to a caller on a case-insensitive checkout");
    }

    [Fact]
    public void A_workload_pairs_only_the_tasks_both_arms_measured_and_clusters_them_by_repository()
    {
        // Hand table — Δrecall = B − A per task:
        //   t01 (repo-a): A 1/2, B 2/2 → +0.5
        //   t02 (repo-b): A 1/2, B 1/2 →  0
        //   t03 (repo-b): A 1/2, B void → not paired
        var cells = new[]
        {
            Cell("t01", ScoutArmId.A1, repository: "org/repo-a"), Cell("t01", ScoutArmId.B1, repository: "org/repo-a", found: 2),
            Cell("t02", ScoutArmId.A1, repository: "org/repo-b"), Cell("t02", ScoutArmId.B1, repository: "org/repo-b"),
            Cell("t03", ScoutArmId.A1, repository: "org/repo-b"), Cell("t03", ScoutArmId.B1, repository: "org/repo-b", outcome: ScoutCellOutcome.Void),
        };

        var w1 = ScoutAnalysis.Of(cells, W1, BootstrapSettings.Default).Workloads.Single();

        w1.Workload.Should().Be(ScoutWorkload.W1);
        w1.Recall.Should().Match<ScoutInterval>(i => i.Tasks == 2 && i.Clusters == 2 && Math.Abs(i.Point - 0.25) < 1e-12);
        w1.WrongMeasured.Should().BeTrue();
    }

    [Fact]
    public void The_secondary_arm_is_reported_and_never_decided()
    {
        var cells = new[] { Cell("t01", ScoutArmId.A3), Cell("t01", ScoutArmId.C3) };

        var analysis = ScoutAnalysis.Of(cells, [ScoutArmId.A3, ScoutArmId.C3], BootstrapSettings.Default);

        analysis.Arms.Select(a => a.Arm.Id).Should().Equal(ScoutArmId.A3, ScoutArmId.C3);
        analysis.Workloads.Should().BeEmpty("W3 is decided by A3 against B3 — C3 separates the model effect from the count effect, nothing more");
    }

    [Fact]
    public void Team_time_shorter_than_the_slowest_scout_is_flagged_by_the_cross_check()
    {
        var cells = new[] { Cell("t01", ScoutArmId.A1, teamMs: 900, scoutMs: 1_000), Cell("t02", ScoutArmId.A1, teamMs: 5_000, scoutMs: 1_000) };

        var arm = ScoutAnalysis.Of(cells, [ScoutArmId.A1], BootstrapSettings.Default).Arms.Single();

        arm.Counts.TeamTimeBelowSlowestScout.Should().Be(1);
        arm.SlowestScoutMs.Value.Should().Be(1_000);
    }
}
