using Bench.Domain.Scouts;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>§3.8's sizing (E2 S8, D7: REPORTED, not a gate): the pilot's cells feed a simulation of exactly the pre-registered procedure —
/// tasks clustered by repository, runs per task fixed, the clustered bootstrap and <see cref="ScoutDecisions.Decide"/> — at a true Δ of 0 and at
/// the −0.03 boundary, for every task count from 8 to 30. The answer is the smallest count with ≥ 80 % passes at 0 and ≤ 5 % false passes at
/// the boundary, or none. Synthetic samples, as a table; the same seed gives the same table to the last bit. Written RED first against a stub
/// that sized nothing.</summary>
public sealed class ScoutSizingTests
{
    /// <summary>Small enough to run in a test, large enough that the rates mean something.</summary>
    private static ScoutSizingSettings Settings(int seed = 7) => ScoutSizingSettings.Of(seed, simulations: 120, resamples: 400).Ok();

    /// <summary>W1's two arms over pilot tasks, one task per repository: per task the A1 and B1 recalls of its cells, and one claim tally per cell.</summary>
    private static IReadOnlyList<ScoutSizingSample> Pilot(params (double[] A, double[] B)[] tasks) =>
        [.. tasks.SelectMany((t, i) =>
            t.A.Select(r => new ScoutSizingSample(ScoutArmId.A1, $"t{i}", $"repo{i}", r, new ClaimTally(1, 10)))
            .Concat(t.B.Select(r => new ScoutSizingSample(ScoutArmId.B1, $"t{i}", $"repo{i}", r, new ClaimTally(1, 10)))))];

    /// <summary>B − A is −0.01 on five pilot tasks and +0.05 on the sixth: every simulated task sits within five points of the true Δ, so the
    /// teams separate from the margin at once. (The shape matters at the boundary, not only the spread: measured while building this, a
    /// SYMMETRIC ±0.01 pilot passes 5–15 % of the boundary runs at every count from 8 to 30, because the percentile bootstrap over so few
    /// clusters is a little too narrow — see <c>research/module_scouts.md</c>, <i>The sizing</i>.)</summary>
    private static IReadOnlyList<ScoutSizingSample> Tight() =>
        Pilot(([0.70], [0.69]), ([0.70], [0.69]), ([0.70], [0.69]), ([0.70], [0.69]), ([0.70], [0.69]), ([0.70], [0.75]));

    /// <summary>B − A swings by ±0.40 between pilot tasks: no task count up to 30 resolves a 3-point margin.</summary>
    private static IReadOnlyList<ScoutSizingSample> Noisy() => Pilot(([0.30, 0.90], [0.70, 0.20]), ([0.80, 0.10], [0.40, 0.95]), ([0.50], [0.10]));

    [Fact]
    public void A_tight_pilot_passes_at_zero_almost_always_and_finds_a_task_count()
    {
        var result = ScoutSizing.Of(Tight(), Settings()).Ok();

        result.Found.Should().BeTrue(string.Join("\n", result.Rows.Select(Row)));
        var smallest = result.Rows.Single(r => r.Tasks == result.SmallestTasks);
        smallest.PassAtZero.Should().BeGreaterThanOrEqualTo(ScoutSizing.PowerTarget);
        smallest.FalsePassAtBoundary.Should().BeLessThanOrEqualTo(ScoutSizing.FalsePassLimit);
        result.Rows.Where(r => r.Tasks < result.SmallestTasks).Should().NotContain(r => r.Meets, "the smallest count is the first that meets both");
        result.Rows.Should().OnlyContain(r => r.PassAtZero >= 0.95, "at a true Δ of 0 a pilot this tight passes nearly every simulated run");
    }

    [Fact]
    public void A_noisy_pilot_returns_no_task_count_and_still_reports_every_row()
    {
        var result = ScoutSizing.Of(Noisy(), Settings()).Ok();

        result.Found.Should().BeFalse(string.Join("\n", result.Rows.Select(Row)));
        result.SmallestTasks.Should().Be(0);
        result.Rows.Select(r => r.Tasks).Should().Equal(Enumerable.Range(8, 23), "every count from 8 to 30 is reported, met or not");
        result.Rows.Should().OnlyContain(r => r.PassAtZero < ScoutSizing.PowerTarget);
    }

    [Fact]
    public void The_same_seed_gives_the_same_table_and_another_seed_draws_other_runs()
    {
        var first = ScoutSizing.Of(Noisy(), Settings(7)).Ok();
        var again = ScoutSizing.Of([.. Noisy().Reverse()], Settings(7)).Ok();
        var other = ScoutSizing.Of(Noisy(), Settings(8)).Ok();

        again.Rows.Should().BeEquivalentTo(first.Rows, o => o.WithStrictOrdering(), "the same seed over the same cells, in any order, sizes the same");
        other.Rows.Select(r => r.PassAtZero).Should().NotEqual(first.Rows.Select(r => r.PassAtZero), "the seed drives the simulated runs");
    }

    [Fact]
    public void The_pilot_summary_names_the_workloads_it_could_size_and_says_when_wrong_claims_were_not_assessed()
    {
        var unassessed = Tight().Select(s => s with { Claims = ClaimTally.Zero }).ToList();
        var result = ScoutSizing.Of(unassessed, Settings()).Ok();

        result.Pilot.Should().ContainSingle().Which.Should().Match<ScoutSizingPilot>(p =>
            p.Workload == ScoutWorkload.W1 && p.Tasks == 6 && p.Repositories == 6 && p.CellsA == 6 && p.CellsB == 6 && !p.WrongSimulated);
        result.Pilot[0].ObservedRecallDelta.Should().BeApproximately(0.0, 1e-12, "five tasks at −0.01 and one at +0.05 average to zero");
        ScoutSizing.Of(Tight(), Settings()).Ok().Pilot[0].WrongSimulated.Should().BeTrue("with assessed claims the wrong-claim half is simulated too");
        result.Rows.Should().OnlyContain(r => r.Workloads.Count == 1 && r.Workloads[0].Workload == ScoutWorkload.W1);
    }

    [Fact]
    public void A_pilot_with_no_paired_task_or_bad_settings_is_refused_by_name()
    {
        ScoutSizing.Of([new ScoutSizingSample(ScoutArmId.A1, "t0", "repo0", 0.5, ClaimTally.Zero)], Settings()).Reason()
            .Should().Contain("no workload");
        ScoutSizingSettings.Of(1, simulations: 5, resamples: 400).Reason().Should().Contain("simulations");
        ScoutSizingSettings.Of(1, simulations: 100, resamples: 10).Reason().Should().Contain("resamples");
        ScoutSizingSettings.Of(1, simulations: 100, resamples: 400, minTasks: 1, maxTasks: 30).Reason().Should().Contain("task");
    }

    [Fact]
    public void The_samples_are_the_scored_cells_with_their_recall_and_claims()
    {
        IReadOnlyList<ScoutCellEvidence> cells =
        [
            ScoutAnalysisFixtures.Cell("t01", ScoutArmId.A1, found: 1, total: 2, verdicts: ScoutAnalysisFixtures.Verdicts(1, 3)),
            ScoutAnalysisFixtures.Cell("t01", ScoutArmId.B1, repository: "org/repo-b", found: 2, total: 2, verdicts: ScoutAnalysisFixtures.Verdicts(0, 2, 1)),
            ScoutAnalysisFixtures.Cell("t02", ScoutArmId.B1, outcome: ScoutCellOutcome.Void),
        ];

        ScoutSizing.Samples(cells).Should().Equal(
            new ScoutSizingSample(ScoutArmId.A1, "t01", "org/repo-a", 0.5, new ClaimTally(1, 4)),
            new ScoutSizingSample(ScoutArmId.B1, "t01", "org/repo-b", 1.0, new ClaimTally(0, 2)));
    }

    [Fact]
    public void The_written_sizing_names_no_task_or_repository_and_is_the_same_bytes_twice()
    {
        const string privateRepo = "file:///srv/git/zqvtharnish-repo";
        var samples = Tight().Select(s => s with { Task = "p" + s.Task[1..].PadLeft(4, '0'), Repository = privateRepo + s.Repository }).ToList();
        var result = ScoutSizing.Of(samples, Settings()).Ok();
        var run = Guid.Parse("0199e0aa-0000-7000-8000-000000000001");

        var markdown = ScoutSizingReport.Markdown(run, ScoutRunKind.Pilot, result);

        markdown.Should().Be(ScoutSizingReport.Markdown(run, ScoutRunKind.Pilot, ScoutSizing.Of(samples, Settings()).Ok()));
        markdown.Should().Contain(ScoutSizingReport.ReportedNotAGate).And.Contain("| 8 |").And.Contain("| 30 |").And.Contain("| W2 | 0 |")
            .And.Contain(run.ToString()).And.Contain("pilot");
        (markdown + string.Join("\n", ScoutSizingReport.Lines(result))).Should().NotContain("zqvtharnish").And.NotContain("p0000").And.NotContain("file:");
        ScoutSizingReport.Lines(result)[^1].Should().StartWith("answer").And.Contain(result.Found ? $"{result.SmallestTasks} tasks" : "no task count");
    }

    private static string Row(ScoutSizingRow r) => $"{r.Tasks}: {r.PassAtZero:0.000} / {r.FalsePassAtBoundary:0.000}";
}
