using Bench.Domain.Gate;

namespace Bench.Domain.Scouts;

/// <summary>One task's paired difference B − A for one workload, and the cluster it is resampled with — its REPOSITORY (§3.8): tasks drawn
/// from one repository share its code, its conventions and its size, so they do not vary independently, and resampling them one by one
/// would claim a precision the run does not have.</summary>
public sealed record TaskDelta(string Task, string Cluster, double Delta);

/// <summary>A paired difference with its one-sided 95 % bounds: <see cref="Lower"/> is the bootstrap's 5th percentile (the bound recall is
/// held to), <see cref="Upper"/> its 95th (the bound wrong claims are held to). Not <see cref="Known"/> when no task had both arms.
/// <see cref="Estimable"/> needs at least two clusters: with one repository every resample is the same sample, the interval collapses onto
/// the point, and a bound read off it would be a point estimate in disguise.</summary>
public sealed record ScoutInterval(bool Known, double Point, double Lower, double Upper, int Tasks, int Clusters)
{
    public static ScoutInterval None { get; } = new(false, 0, 0, 0, 0, 0);

    public bool Estimable => Known && Clusters >= 2;
}

/// <summary>The bootstrap's pre-registered settings: a FIXED seed and the number of resamples (10 000 by default, §3.8).</summary>
public sealed record BootstrapSettings(int Seed, int Resamples)
{
    public const int DefaultSeed = 20261012;

    public const int DefaultResamples = 10_000;

    public static BootstrapSettings Default { get; } = new(DefaultSeed, DefaultResamples);

    public static Outcome<BootstrapSettings> Of(int seed, int resamples) =>
        resamples is >= 100 and <= 1_000_000
            ? Outcome<BootstrapSettings>.Success(new BootstrapSettings(seed, resamples))
            : Outcome<BootstrapSettings>.Failure($"{resamples} resamples is not a bootstrap — 100 to 1 000 000");
}

/// <summary>The paired, CLUSTERED bootstrap of §3.8, pure and reproducible from its seed. Pairing happens before resampling — each task
/// carries its own B − A — so a resample keeps every task's two arms together; then whole repositories are drawn with replacement (as many
/// draws as there are repositories), every task of a drawn repository joins the resample, and the resample's statistic is the mean over the
/// tasks it holds. The bounds are the linearly interpolated 5th and 95th percentiles of the statistics (<see cref="Quantile.Interpolated"/>).
/// <para>
/// The draws are <see cref="SeededDraws"/>, never <see cref="Random"/>, and the clusters and their tasks are taken in ordinal order of their
/// names, so the same deltas give the same interval to the last bit on any machine — what makes <c>report --json</c> byte-deterministic.
/// </para></summary>
public static class ScoutBootstrap
{
    public const double LowerPercentile = 0.05;

    public const double UpperPercentile = 0.95;

    public static ScoutInterval Of(IReadOnlyList<TaskDelta> deltas, BootstrapSettings settings)
    {
        if (deltas.Count == 0)
        {
            return ScoutInterval.None;
        }

        var clusters = deltas
            .GroupBy(d => d.Cluster, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.OrderBy(d => d.Task, StringComparer.Ordinal).Select(d => d.Delta).ToArray())
            .ToArray();
        var statistics = Resample(clusters, settings);

        return new ScoutInterval(
            true,
            clusters.SelectMany(c => c).Sum() / deltas.Count,
            Quantile.Interpolated(statistics, LowerPercentile),
            Quantile.Interpolated(statistics, UpperPercentile),
            deltas.Count,
            clusters.Length);
    }

    /// <summary>The resampled means, sorted ascending.</summary>
    private static double[] Resample(double[][] clusters, BootstrapSettings settings)
    {
        var draws = new SeededDraws(settings.Seed);
        var statistics = new double[settings.Resamples];

        for (var r = 0; r < statistics.Length; r++)
        {
            statistics[r] = OneResample(clusters, draws);
        }

        Array.Sort(statistics);
        return statistics;
    }

    private static double OneResample(double[][] clusters, SeededDraws draws)
    {
        var (sum, count) = (0.0, 0);

        for (var k = 0; k < clusters.Length; k++)
        {
            var drawn = clusters[draws.Below(clusters.Length)];
            sum += drawn.Sum();
            count += drawn.Length;
        }

        return sum / count;
    }
}
