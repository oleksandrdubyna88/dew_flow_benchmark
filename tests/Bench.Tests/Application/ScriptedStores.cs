using Bench.Application;
using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Targets;

namespace Bench.Tests.Application;

/// <summary>One scored leg, as a value. Enough to exercise every axis a report groups by.</summary>
internal sealed record ScriptedLeg(string Question, string Subject, string Variant, double Value);

/// <summary>A run whose only real content is its suite stamp and its planned questions — the two things
/// the split is derived from.
/// <para>
/// Shared by the use-case tests and the CLI's, deliberately: a second pair of doubles would let the two
/// surfaces be tested against two different ideas of what the store answers, and the whole point of
/// <c>RunReport</c> is that both render ONE object.
/// </para>
/// <para>
/// The default stamp is a FROZEN one (<c>s@v3#…</c>) rather than a bare id, because the report must split
/// on the suite id INSIDE it — a stamp that was merely the id would let a wrong implementation pass.
/// </para></summary>
internal sealed class ScriptedRun(IReadOnlyList<string> questions, string suiteStamp) : IRunStore
{
    public const string SuiteId = "s";

    public static readonly DateTimeOffset Noon = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

    public ScriptedRun(IReadOnlyList<string> questions)
        : this(questions, $"{SuiteId}@v3#abcdef012345")
    {
    }

    public BenchRun Run { get; } = BenchRun.Planned(
        "report",
        MeasurementTarget.At(RepoUrl.Parse("https://example.invalid/x.git").Ok(), CommitSha.Parse(new string('c', 40)).Ok()),
        new EngineRef(EngineKind.Qln, "http://localhost:5080", "1.0", "fp"),
        suiteStamp,
        Noon);

    public Task<Outcome<BenchRun>> LoadAsync(Guid runId, CancellationToken cancellationToken) =>
        Task.FromResult(runId == Run.Id
            ? Outcome<BenchRun>.Success(Run)
            : Outcome<BenchRun>.Failure($"no run {runId}"));

    public Task<IReadOnlyList<string>> QuestionIdsAsync(Guid runId, CancellationToken cancellationToken) =>
        Task.FromResult(questions);

    public Task<IReadOnlyList<BenchRun>> RecentAsync(int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BenchRun>>([Run]);

    // A report reads the machine; it never records one. Answering NotRecorded is the honest double: these
    // runs were never probed.
    public Task RecordMachineAsync(Guid runId, Bench.Domain.Trace.MachineFacts facts, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a report does not record machines");

    public Task<Bench.Domain.Trace.MachineFacts> MachineAsync(Guid runId, CancellationToken cancellationToken) =>
        Task.FromResult(Bench.Domain.Trace.MachineFacts.NotRecorded);

    // The queue half of the port. A report never claims, settles or sweeps, and a double that quietly
    // answered these would hide a report that had started doing so.
    public Task<Outcome<BenchRun>> CreateAsync(BenchRun run, IReadOnlyList<RunCell> cells, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a report does not create runs");

    public Task<Outcome<RunCell>> ClaimNextAsync(Guid runId, WorkerIdentity owner, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a report does not claim cells");

    public Task<Outcome<RunCell>> SettleAsync(Guid cellId, WorkerIdentity owner, LegOutcome outcome, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a report does not settle cells");

    public Task<SweepReport> SweepAsync(TimeSpan staleAfter, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a report does not sweep");

    public Task<RunProgress> ProgressAsync(Guid runId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a report reads results, not progress");
}

/// <summary>Scored legs held as values, aggregated the way the port promises: grouped, averaged, and with
/// an absent (question, subject) pair ABSENT rather than zero.</summary>
internal sealed class ScriptedResults(IReadOnlyList<ScriptedLeg> legs, string metricName) : IResultStore
{
    public Task<IReadOnlyList<MetricByDimension>> AverageByAsync(
        Guid runId, ReportDimension dimension, string metric, QuestionScope scope, CancellationToken cancellationToken)
    {
        IReadOnlyList<MetricByDimension> grouped = metric != metricName
            ? []
            : [.. In(scope)
                .GroupBy(leg => Key(leg, dimension), StringComparer.Ordinal)
                .Select(g => new MetricByDimension(g.Key, g.Average(l => l.Value), g.Count()))
                .OrderBy(x => x.Dimension, StringComparer.Ordinal)];

        return Task.FromResult(grouped);
    }

    public Task<IReadOnlyList<QuestionPassRate>> PassRateByQuestionAndSubjectAsync(
        Guid runId, string metric, CancellationToken cancellationToken)
    {
        IReadOnlyList<QuestionPassRate> rates =
            [.. legs
                .GroupBy(l => (l.Question, l.Subject))
                .Select(g => new QuestionPassRate(g.Key.Question, g.Key.Subject, g.Average(l => l.Value)))];

        return Task.FromResult(rates);
    }

    public Task<RunScoreboard> ScoreboardAsync(Guid runId, CancellationToken cancellationToken) =>
        Task.FromResult(new RunScoreboard(legs.Count, legs.Count(l => l.Value > 0)));

    private IEnumerable<ScriptedLeg> In(QuestionScope scope) =>
        scope is QuestionScope.Some some ? legs.Where(l => some.Ids.Contains(l.Question)) : legs;

    private static string Key(ScriptedLeg leg, ReportDimension dimension) =>
        dimension switch
        {
            ReportDimension.Variant => leg.Variant,
            ReportDimension.Subject => leg.Subject,
            ReportDimension.Lane => "native",
            ReportDimension.FixArm => "full",
            _ => "Qln",
        };

    // The write half. A report appends nothing and prunes nothing.
    public Task<Outcome<LegResult>> SaveAsync(LegResult result, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a report writes no results");

    public Task<SnippetPruning> PruneHitSnippetsAsync(DateTimeOffset olderThan, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a report prunes nothing");

    public Task<bool> HasResultAsync(Guid cellId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a report does not re-enter a leg");

    public Task<IReadOnlyList<LegResult>> ForRunAsync(Guid runId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a report aggregates rather than hydrating the run — that is the point");

    public Task<IReadOnlyList<JudgeableLeg>> WithoutMetricAsync(Guid runId, string metric, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a report is not the judge lane");

    public Task<Outcome<int>> AppendMetricsAsync(Guid resultId, IReadOnlyList<StoredMetric> metrics, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a report appends no metrics");
}
