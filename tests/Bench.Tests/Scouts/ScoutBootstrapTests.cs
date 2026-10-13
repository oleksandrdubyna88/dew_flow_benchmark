using Bench.Domain;
using Bench.Domain.Scouts;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>§3.8's paired, clustered bootstrap: fixed by its seed to the last bit, blind to the order its tasks arrive in, and resampling
/// whole REPOSITORIES — the case below is built so that resampling tasks one by one gives a visibly narrower interval than the run licenses.</summary>
public sealed class ScoutBootstrapTests
{
    private static readonly BootstrapSettings Settings = BootstrapSettings.Default;

    /// <summary>Two repositories of five tasks each; every task of repo-a is +0.10, every task of repo-b −0.10. The tasks of one repository move
    /// together, so the honest uncertainty is "which repositories did we happen to draw" — with two of them the mean is +0.1, 0 or −0.1.</summary>
    private static IReadOnlyList<TaskDelta> TwoCorrelatedRepositories() =>
    [
        .. Enumerable.Range(1, 5).Select(i => new TaskDelta($"a{i}", "org/repo-a", 0.10)),
        .. Enumerable.Range(1, 5).Select(i => new TaskDelta($"b{i}", "org/repo-b", -0.10)),
    ];

    [Fact]
    public void The_same_deltas_and_seed_give_the_same_interval_to_the_last_bit_and_another_seed_draws_differently()
    {
        var deltas = Mixed();

        var first = ScoutBootstrap.Of(deltas, Settings);
        var again = ScoutBootstrap.Of(deltas, Settings);
        var other = ScoutBootstrap.Of(deltas, new BootstrapSettings(Settings.Seed + 1, Settings.Resamples));

        again.Should().Be(first);
        BitConverter.DoubleToInt64Bits(again.Lower).Should().Be(BitConverter.DoubleToInt64Bits(first.Lower));
        (other.Lower, other.Upper).Should().NotBe((first.Lower, first.Upper), "the seed is what fixes the draws");
        other.Point.Should().Be(first.Point, "the point is the mean over tasks, whatever the seed");
    }

    [Fact]
    public void The_order_the_tasks_arrive_in_does_not_move_the_interval()
    {
        var deltas = Mixed();

        ScoutBootstrap.Of([.. deltas.Reverse()], Settings).Should().Be(ScoutBootstrap.Of(deltas, Settings));
    }

    [Fact]
    public void Whole_repositories_are_resampled_so_two_correlated_repositories_leave_the_honest_width()
    {
        var interval = ScoutBootstrap.Of(TwoCorrelatedRepositories(), Settings);

        interval.Should().Match<ScoutInterval>(i => i.Known && i.Estimable && i.Tasks == 10 && i.Clusters == 2);
        interval.Point.Should().BeApproximately(0.0, 1e-12);
        interval.Lower.Should().BeApproximately(-0.10, 1e-12, "a quarter of the resamples draw repo-b twice, so the 5th percentile is its −0.10");
        interval.Upper.Should().BeApproximately(0.10, 1e-12);
    }

    [Fact]
    public void Resampling_tasks_one_by_one_would_claim_a_precision_two_repositories_do_not_have()
    {
        // The refuted approach, given a shape: the same ten tasks, each its own cluster — what a per-task bootstrap does.
        var perTask = TwoCorrelatedRepositories().Select(d => d with { Cluster = d.Task }).ToList();

        var naive = ScoutBootstrap.Of(perTask, Settings);
        var clustered = ScoutBootstrap.Of(TwoCorrelatedRepositories(), Settings);

        naive.Lower.Should().BeGreaterThan(-0.08, "ten independent draws of ±0.10 average out: the 5th percentile sits near −0.05");
        clustered.Lower.Should().BeLessThan(naive.Lower - 0.03, "clustering by repository is what keeps the interval as wide as two repositories allow");
    }

    [Fact]
    public void One_repository_collapses_the_interval_onto_the_point_and_says_so()
    {
        var interval = ScoutBootstrap.Of([new TaskDelta("t1", "org/only", 0.2), new TaskDelta("t2", "org/only", -0.1)], Settings);

        interval.Should().Match<ScoutInterval>(i => i.Known && !i.Estimable && i.Clusters == 1);
        (interval.Lower, interval.Upper).Should().Be((interval.Point, interval.Point), "every resample draws the one repository whole");
    }

    [Fact]
    public void No_paired_task_is_no_interval_rather_than_a_zero()
    {
        ScoutBootstrap.Of([], Settings).Should().Be(ScoutInterval.None);
        ScoutInterval.None.Known.Should().BeFalse();
    }

    [Fact]
    public void The_draws_are_SplitMix64_so_the_sequence_is_a_documented_algorithm_and_not_a_runtime_detail()
    {
        // SplitMix64's published first outputs for seed 0 (Vigna's reference implementation).
        var draws = new SeededDraws(0UL);

        draws.Next().Should().Be(0xE220A8397B1DCDAFUL);
        draws.Next().Should().Be(0x6E789E6AA1B965F4UL);
    }

    [Fact]
    public void The_settings_refuse_a_resample_count_that_is_not_a_bootstrap()
    {
        BootstrapSettings.Of(1, 10).Failed().Should().BeTrue();
        BootstrapSettings.Of(1, 10_000).Ok().Should().Be(BootstrapSettings.Default with { Seed = 1 });
    }

    /// <summary>Twelve repositories with irregular deltas — enough distinct resample means that two seeds land on different percentiles.</summary>
    private static IReadOnlyList<TaskDelta> Mixed() =>
        [.. Enumerable.Range(1, 18).Select(i => new TaskDelta($"t{i:00}", $"org/r{(i % 12) + 1}", Math.Sin(i * 1.7) / 10))];
}
