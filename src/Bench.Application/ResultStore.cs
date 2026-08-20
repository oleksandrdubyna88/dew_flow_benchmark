using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Trace;

namespace Bench.Application;

/// <param name="Dimension">The value of whatever the results were grouped by — an engine, a lane, a subject.</param>
/// <param name="Average">The metric's mean across the legs in that group.</param>
/// <param name="Legs">How many legs contributed. Reported because a mean over two legs and a mean over
/// two hundred are different claims, and the report must be able to refuse to rank the first.</param>
public readonly record struct MetricByDimension(string Dimension, double Average, int Legs);

/// <summary>One scored leg, reduced to what an arm comparison can use.</summary>
/// <param name="RunId">Which run it came from. The arm is a property of the RUN, so this is the join key —
/// and it is why a per-run aggregate cannot answer the arm question at all.</param>
/// <param name="QuestionId">Which question. Carried because the selection/held-out half is derived from it by
/// a hash, above the database, so the split needs the id rather than the number alone.</param>
public readonly record struct MetricLeg(Guid RunId, string QuestionId, double Value);

/// <summary>Which axis of the measurement tuple an aggregate groups by.
/// <para>
/// A parameter rather than four near-identical methods on the port. There were two —
/// <c>AverageByEngineAsync</c> and <c>AverageByLaneAsync</c> — and the report needs subject and variant as
/// well; a fifth copy is where the family drifts, and the adapter's own group-by was already general
/// (a dimension selector) behind those two public faces.
/// </para>
/// <para>
/// Every member names a column the cell already carries, which is why this costs no join and no schema
/// change: <c>CellRow</c>'s own comment says these two axes are columns precisely so that "average by
/// subject" is a group-by rather than string parsing.
/// </para></summary>
public enum ReportDimension
{
    /// <summary>The run's engine kind. One value per run today; an axis once the backend echo lands.</summary>
    Engine,

    Lane,

    Subject,

    /// <summary>The variant's <c>Canonical</c> — its name, or <c>"-"</c> for a cell that named none.
    /// <para>
    /// Read through <c>VariantSelectionCodec.Decode(...).Canonical</c> rather than off the name column, so the
    /// control arm carries the same mark here as it does in a leg identity. A blank would put the control arm
    /// and a variant whose name failed to store into one row of the report.
    /// </para></summary>
    Variant,

    /// <summary>Which slice of a fix task the leg ran — <see cref="Bench.Domain.Runs.FixArm"/>'s canonical
    /// token. Investigate-only against implement-only against full is the comparison
    /// <c>todo/PLAN_investigate_vs_implement.md</c> exists to produce, and it is a column on the cell for
    /// the same reason subject and lane are: a report groups, it does not parse.</summary>
    FixArm,
}

/// <summary>Which questions an aggregate may read.
/// <para>
/// A closed pair rather than a nullable collection, and it exists for the SPLIT: comparing a configuration's
/// showing on the half that selected it against the half that did not is the whole guard against a false
/// winner, and <see cref="Bench.Domain.Splitting.SeedSplit"/> assigns that half by a hash no database can
/// compute. So the half arrives here as the question ids it contains — a suite has tens of questions where a
/// run has thousands of legs, which keeps the aggregate a group-by instead of a fold over hydrated rows.
/// </para>
/// <para>
/// <see cref="Only"/> with an empty list is legal and means what it says: this half has no questions, so
/// there is nothing to average. That is the <c>Unmeasured</c> state a report must be able to render, and it
/// is a different fact from <see cref="All"/>.
/// </para></summary>
public abstract record QuestionScope
{
    private QuestionScope() { }

    public sealed record Every : QuestionScope
    {
        internal Every() { }
    }

    public sealed record Some : QuestionScope
    {
        internal Some(IReadOnlyList<string> ids) => Ids = ids;

        public IReadOnlyList<string> Ids { get; }
    }

    public static QuestionScope All { get; } = new Every();

    public static QuestionScope Only(IReadOnlyList<string> ids) => new Some(ids);
}

/// <param name="QuestionId">The suite-facing id, which is what a report and a result row both quote.</param>
/// <param name="SubjectModelId">The model id, never the endpoint — folding an address into the identity
/// would make the same model at a different port a different subject.</param>
/// <param name="PassRate">The metric's mean for this pair. A boolean reads as 1 or 0, so a pass rate and a
/// score aggregate the same way.</param>
public readonly record struct QuestionPassRate(string QuestionId, string SubjectModelId, double PassRate);

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

    /// <summary>The aggregate the schema exists for: one metric, averaged along one axis of a run.
    /// <para>
    /// This is the query the adopted library's disk store cannot answer without reading every result and
    /// parsing dimensions back out of a directory name. Keeping it a group-by is the entire justification
    /// for owning the storage rather than inheriting theirs.
    /// </para>
    /// <para>
    /// <paramref name="scope"/> is what makes the SPLIT readable: the same metric along the same axis, asked
    /// twice — once over the half that selected a configuration and once over the half that did not — is the
    /// pair <c>SeedSplit.Proof</c> turns into a verdict. Asked with <see cref="QuestionScope.All"/> it is the
    /// whole suite.
    /// </para>
    /// <para>
    /// An absent metric is an EMPTY result, never a zero: a dimension nobody measured on this metric and a
    /// dimension that measured zero are different facts, and a report that merges them invents data.
    /// </para></summary>
    Task<IReadOnlyList<MetricByDimension>> AverageByAsync(
        Guid runId,
        ReportDimension dimension,
        string metricName,
        QuestionScope scope,
        CancellationToken cancellationToken);

    /// <summary>One metric per (question, subject) pair — the input <c>Discrimination</c> reads.
    /// <para>
    /// Separate from <see cref="AverageByAsync"/> rather than a fifth dimension, because it groups by TWO
    /// keys and what it feeds is not a ranking: <c>QuestionSpread</c> asks whether a question can separate
    /// these subjects at all, which is a statement about the question and never a verdict on it.
    /// </para>
    /// <para>
    /// A pair with no legs is ABSENT from this list rather than present with a zero. That is what lets
    /// <c>QuestionSpread.Unmeasured</c> name the models that never attempted a question instead of counting
    /// them as failures.
    /// </para></summary>
    /// <summary>What the machine was doing across each scored leg of a run.
    /// <para>
    /// Its own read rather than a field of <see cref="ForRunAsync"/>, and projected to ONE column: a report
    /// wanting the peaks must not pull every prompt and answer of a campaign to fold four numbers — the
    /// defect this port has now recorded three times, on <c>ScoreboardAsync</c>, on <c>TotalsAsync</c> and on
    /// the aggregate itself.
    /// </para></summary>
    Task<IReadOnlyList<LegLoad>> LoadsAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>Every scored leg of SEVERAL runs, as bare numbers — what an arm comparison folds.
    /// <para>
    /// Cross-run by necessity rather than by ambition: the compute backend is recorded on the run, so two
    /// arms are two runs and no per-run aggregate can put them beside each other. It returns legs rather
    /// than averages because the half a leg belongs to is derived from its question id by a hash, so the
    /// split cannot happen in the database.
    /// </para></summary>
    Task<IReadOnlyList<MetricLeg>> LegsAsync(
        IReadOnlyList<Guid> runIds, string metricName, CancellationToken cancellationToken);

    Task<IReadOnlyList<QuestionPassRate>> PassRateByQuestionAndSubjectAsync(
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
