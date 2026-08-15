using Bench.Domain;
using Bench.Domain.Runs;

namespace Bench.Application;

/// <param name="Dimension">The value of whatever the results were grouped by — an engine, a lane, a subject.</param>
/// <param name="Average">The metric's mean across the legs in that group.</param>
/// <param name="Legs">How many legs contributed. Reported because a mean over two legs and a mean over
/// two hundred are different claims, and the report must be able to refuse to rank the first.</param>
public readonly record struct MetricByDimension(string Dimension, double Average, int Legs);

/// <summary>Where scored legs live.
/// <para>
/// Separate from <see cref="IRunStore"/> on purpose: that port is about a queue of work, this one is
/// about evidence. Results are immutable once written — a leg is scored, not edited — so nothing here
/// updates anything.
/// </para></summary>
public interface IResultStore
{
    /// <summary>Stores a leg's result. One result per cell: a second write is a bug, not a revision.</summary>
    Task<Outcome<LegResult>> SaveAsync(LegResult result, CancellationToken cancellationToken);

    Task<IReadOnlyList<LegResult>> ForRunAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>The aggregate the schema exists for: one metric, averaged per engine across a run.
    /// <para>
    /// This is the query the adopted library's disk store cannot answer without reading every result and
    /// parsing dimensions back out of a directory name. Keeping it a group-by is the entire justification
    /// for owning the storage rather than inheriting theirs.
    /// </para></summary>
    Task<IReadOnlyList<MetricByDimension>> AverageByEngineAsync(
        Guid runId, string metricName, CancellationToken cancellationToken);

    Task<IReadOnlyList<MetricByDimension>> AverageByLaneAsync(
        Guid runId, string metricName, CancellationToken cancellationToken);
}
