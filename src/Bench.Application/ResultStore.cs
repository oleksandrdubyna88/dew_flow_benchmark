using Bench.Domain;
using Bench.Domain.Runs;

namespace Bench.Application;

/// <param name="Dimension">The value of whatever the results were grouped by — an engine, a lane, a subject.</param>
/// <param name="Average">The metric's mean across the legs in that group.</param>
/// <param name="Legs">How many legs contributed. Reported because a mean over two legs and a mean over
/// two hundred are different claims, and the report must be able to refuse to rank the first.</param>
public readonly record struct MetricByDimension(string Dimension, double Average, int Legs);

/// <param name="Scored">How many legs of the run carry a result.</param>
/// <param name="Passed">How many of those failed no expectation. Same rule as
/// <see cref="LegResult.Passed"/> — a leg with no metrics at all has not passed anything.</param>
public readonly record struct RunScoreboard(int Scored, int Passed);

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

/// <summary>What one retention pass dropped.</summary>
/// <param name="Hits">Rows whose snippet text was released. The rows themselves survive — every retrieval
/// metric recomputes from their ranks, scores, paths and spans.</param>
/// <param name="BytesFreed">What that text weighed. Reported because a retention pass that cannot say what
/// it reclaimed is a retention pass nobody can size a disk from.</param>
public readonly record struct SnippetPruning(int Hits, long BytesFreed)
{
    public string Describe =>
        Hits == 0
            ? "no hit snippets were old enough to release"
            : $"released the text of {Hits} hit(s), {BytesFreed / 1024} KiB — ranks, scores and spans kept";
}

public interface IResultStore
{
    /// <summary>Stores a leg's result. One result per cell: a second write is a bug, not a revision.
    /// <para>
    /// The result, its metrics, its funnel and its hits go in ONE write. Not an optimisation: a leg whose
    /// answer is durable and whose evidence is not would be a scored number nobody can re-check, and the
    /// re-entrancy check that follows a crash reads the result's presence as "this leg is finished".
    /// </para></summary>
    Task<Outcome<LegResult>> SaveAsync(LegResult result, CancellationToken cancellationToken);

    /// <summary>Releases the source TEXT of hits older than a window, keeping every row intact.
    /// <para>
    /// The owner of the one surface in this schema that grows without bound
    /// (<c>todo/PLAN_variant_matrix.md</c> §3.5): at a limit of 20 hits, snippets are most of a cell's bytes,
    /// and they are the one part that is reproducible — the corpus at the pinned commit contains them. So
    /// they are kept raw for a configured window and dropped after, which leaves ranks, scores, spans and
    /// channels — everything a retrieval metric is computed from — untouched and recomputable forever.
    /// </para>
    /// <para>
    /// Decided BEFORE the first write, per the founding plan: a budget that lives in the schema is a budget;
    /// one that lives in a clean-up job somebody writes after the disk fills is a hope. The reference shape
    /// is <c>dew_flow_rag_qln · SizeHistoryStore</c> — raw for days, rolled up beyond, one place, tested.
    /// </para></summary>
    Task<SnippetPruning> PruneHitSnippetsAsync(DateTimeOffset olderThan, CancellationToken cancellationToken);

    /// <summary>Whether this leg has already been scored. The runner asks before it settles a cell: a
    /// crash between storing a result and settling leaves the cell claimed, the sweep hands it back, and the
    /// retry must be able to finish the job rather than deadlock against its own earlier write.</summary>
    Task<bool> HasResultAsync(Guid cellId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LegResult>> ForRunAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>The two integers a run summary prints, COUNTED where the rows are.
    /// <para>
    /// Separate from <see cref="ForRunAsync"/> because the summary used to call that one: every prompt,
    /// every answer and every metric with its metadata crossed the wire and was deserialized so a
    /// finished campaign could print "N of M passed". At the tens of thousands of cells this schema
    /// targets, that is the whole run pulled into memory to render one line. The store has learned this
    /// lesson before — <c>TotalsAsync</c> carries the same diagnosis in its own comment.
    /// </para></summary>
    Task<RunScoreboard> ScoreboardAsync(Guid runId, CancellationToken cancellationToken);

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
