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
/// <param name="QuestionId">Which suite question this leg answered — the judge needs the reference
/// answer, and that lives in the suite rather than in the result.</param>
/// <param name="SubjectModelId">Recorded on the verdict so self-judging is a filter after the fact,
/// not something only the person watching the run could have noticed.</param>
public readonly record struct JudgeableLeg(
    Guid ResultId, string QuestionId, string SubjectModelId, string Prompt, string Answer);

public interface IResultStore
{
    /// <summary>Stores a leg's result. One result per cell: a second write is a bug, not a revision.</summary>
    Task<Outcome<LegResult>> SaveAsync(LegResult result, CancellationToken cancellationToken);

    /// <summary>Whether this leg has already been scored. The runner asks before it settles a cell: a
    /// crash between storing a result and settling leaves the cell claimed, the sweep hands it back, and the
    /// retry must be able to finish the job rather than deadlock against its own earlier write.</summary>
    Task<bool> HasResultAsync(Guid cellId, CancellationToken cancellationToken);

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

    /// <summary>Stored legs of a run that do NOT yet carry <paramref name="metricName"/>.
    /// <para>
    /// The filter is what makes re-judging cheap and interruptible in one stroke: a second arbiter sees
    /// every leg because its metric name differs, the SAME arbiter re-run after a crash sees only what it
    /// never finished, and neither can produce a duplicate. The same shape as the telemetry ingest, for the
    /// same reason — resumability that depends on nobody killing the process is not resumability.
    /// </para></summary>
    Task<IReadOnlyList<JudgeableLeg>> WithoutMetricAsync(
        Guid runId, string metricName, CancellationToken cancellationToken);

    /// <summary>Appends metrics to a stored leg.
    /// <para>
    /// Appending is not editing, and the distinction is the reason this sits beside an otherwise
    /// write-once port: the subject's answer and its mechanical score are never touched. A judgement is a
    /// LATER, separately-attributed reading of the same evidence, and it must be able to arrive without
    /// the run that produced the evidence being re-run — which is the entire justification for storing the
    /// answer in the first place.
    /// </para></summary>
    Task<Outcome<int>> AppendMetricsAsync(
        Guid resultId, IReadOnlyList<StoredMetric> metrics, CancellationToken cancellationToken);
}
