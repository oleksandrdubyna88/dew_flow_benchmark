using Bench.Application;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI.Evaluation;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The result store, and the one query that justifies owning storage at all.
/// <para>
/// The adopted library's disk store keys results by a directory path, so "average this metric per engine"
/// means reading every result and parsing dimensions back out of a composite name. Here it is a group-by,
/// and <see cref="The_average_of_one_metric_per_engine_is_a_query_not_a_full_scan"/> is the proof.
/// </para></summary>
[Collection("postgres")]
public sealed class PostgresResultStoreTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_scored_leg_round_trips_with_its_metrics_and_their_metadata()
    {
        var (runId, cells) = await SeedAsync(EngineKind.Qln, questions: 1, lanes: 1);
        var store = new Bench.Infrastructure.Persistence.PostgresResultStore(postgres.NewContext());

        var metric = StoredMetric.Numeric("Anchor recall", 1, "surfaced", failed: false, "Exceptional")
            .With(new Dictionary<string, string> { ["anchor"] = "src/A.cs#A.Foo", ["hitCount"] = "3" });

        await store.SaveAsync(LegResult.Of(cells[0], "where is X?", "in A.Foo", [metric], Noon), Ct);

        var read = (await store.ForRunAsync(runId, Ct)).Single();
        read.Answer.Should().Be("in A.Foo", "the subject's answer is the expensive artefact — a second arbiter must not need a re-run");
        read.Metrics.Single().Metadata.Should().Contain("anchor", "src/A.cs#A.Foo");
        read.Metrics.Single().AsNumber().Ok().Should().Be(1);
        read.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task A_leg_is_scored_once_and_a_second_write_is_refused()
    {
        var (_, cells) = await SeedAsync(EngineKind.Qln, 1, 1);
        var store = new Bench.Infrastructure.Persistence.PostgresResultStore(postgres.NewContext());
        var metric = StoredMetric.Boolean("Answered", true, "", false, "Good");

        await store.SaveAsync(LegResult.Of(cells[0], "q", "a", [metric], Noon), Ct);
        var second = await store.SaveAsync(LegResult.Of(cells[0], "q", "different", [metric], Noon), Ct);

        second.Reason().Should().Contain("already has a result").And.Contain("rather than a revision");
    }

    [Fact]
    public async Task A_result_without_a_leg_is_refused_because_it_would_have_no_measurement_key()
    {
        var store = new Bench.Infrastructure.Persistence.PostgresResultStore(postgres.NewContext());

        var orphan = await store.SaveAsync(
            LegResult.Of(Guid.CreateVersion7(), "q", "a", [StoredMetric.Boolean("x", true, "", false, "Good")], Noon), Ct);

        orphan.Reason().Should().Contain("no measurement key");
    }

    [Fact]
    public async Task The_average_of_one_metric_per_engine_is_a_query_not_a_full_scan()
    {
        var (qlnRun, qlnCells) = await SeedAsync(EngineKind.Qln, questions: 2, lanes: 1);
        var store = new Bench.Infrastructure.Persistence.PostgresResultStore(postgres.NewContext());

        // Two legs on one engine: one found its anchor, one did not. Each also carries a SECOND metric
        // with a deliberately different value, so an implementation that forgot to filter by name would
        // average the two together and land somewhere other than 0.5.
        await store.SaveAsync(Result(qlnCells[0], recall: 1, latency: 900), Ct);
        await store.SaveAsync(Result(qlnCells[1], recall: 0, latency: 900), Ct);

        var byEngine = await store.AverageByEngineAsync(qlnRun, "Anchor recall", Ct);

        byEngine.Should().ContainSingle();
        byEngine[0].Dimension.Should().Be("Qln");
        byEngine[0].Average.Should().Be(0.5, "only the named metric may enter the aggregate");
        byEngine[0].Legs.Should().Be(2, "a mean over two legs and a mean over two hundred are different claims");

        (await store.AverageByEngineAsync(qlnRun, "Latency ms", Ct))[0].Average.Should().Be(900);
        (await store.AverageByEngineAsync(qlnRun, "No such metric", Ct))
            .Should().BeEmpty("an absent metric is an empty result, never a zero");
    }

    [Fact]
    public async Task Averaging_by_lane_separates_the_lanes_of_one_run()
    {
        var (runId, cells) = await SeedAsync(EngineKind.NoRetrieval, questions: 1, lanes: 2);
        var store = new Bench.Infrastructure.Persistence.PostgresResultStore(postgres.NewContext());

        await store.SaveAsync(Result(cells[0], recall: 1), Ct);
        await store.SaveAsync(Result(cells[1], recall: 0), Ct);

        var byLane = await store.AverageByLaneAsync(runId, "Anchor recall", Ct);

        byLane.Should().HaveCount(2);
        byLane.Select(x => x.Average).Should().BeEquivalentTo([1.0, 0.0]);
    }

    [Fact]
    public async Task A_boolean_metric_aggregates_as_a_pass_rate()
    {
        var (runId, cells) = await SeedAsync(EngineKind.Qln, questions: 2, lanes: 1);
        var store = new Bench.Infrastructure.Persistence.PostgresResultStore(postgres.NewContext());

        await store.SaveAsync(LegResult.Of(cells[0], "q", "a", [StoredMetric.Boolean("Hidden tests", true, "", false, "Good")], Noon), Ct);
        await store.SaveAsync(LegResult.Of(cells[1], "q", "a", [StoredMetric.Boolean("Hidden tests", false, "", true, "Unacceptable")], Noon), Ct);

        (await store.AverageByEngineAsync(runId, "Hidden tests", Ct))[0].Average
            .Should().Be(0.5, "a pass rate and a numeric score have to aggregate the same way");
    }

    [Fact]
    public async Task A_metric_the_library_produced_survives_the_round_trip_back_into_its_own_model()
    {
        var (runId, cells) = await SeedAsync(EngineKind.Qln, 1, 1);
        var store = new Bench.Infrastructure.Persistence.PostgresResultStore(postgres.NewContext());

        var theirs = new NumericMetric("Anchor recall", 1)
        {
            Reason = "surfaced among 3 hits",
            Interpretation = new EvaluationMetricInterpretation(EvaluationRating.Exceptional, failed: false, "found"),
        };
        theirs.AddOrUpdateMetadata("anchor", "src/A.cs#A.Foo");

        await store.SaveAsync(LegResult.Of(cells[0], "q", "a", [MetricCodec.Encode(theirs)], Noon), Ct);
        var stored = (await store.ForRunAsync(runId, Ct)).Single().Metrics.Single();
        var back = MetricCodec.Decode(stored).Ok();

        back.Should().BeOfType<NumericMetric>().Which.Value.Should().Be(1);
        back.Reason.Should().Be("surfaced among 3 hits");
        back.Interpretation!.Rating.Should().Be(EvaluationRating.Exceptional, "a rating stored as a name survives someone inserting an enum member");
        back.Metadata.Should().Contain("anchor", "src/A.cs#A.Foo");
    }

    [Fact]
    public async Task The_run_summary_does_not_hydrate_the_run_to_count_it()
    {
        var (runId, cells) = await SeedAsync(EngineKind.Qln, questions: 3, lanes: 1);
        var sql = new List<string>();

        await using var db = Recording(sql);
        var store = new Bench.Infrastructure.Persistence.PostgresResultStore(db);

        await store.SaveAsync(Result(cells[0], recall: 1), Ct);
        await store.SaveAsync(Result(cells[1], recall: 1), Ct);
        await store.SaveAsync(Result(cells[2], recall: 0), Ct);
        sql.Clear();

        var board = await store.ScoreboardAsync(runId, Ct);

        board.Scored.Should().Be(3);
        board.Passed.Should().Be(2, "a leg is passed when it failed no expectation");
        sql.Should().NotBeEmpty("the counting has to reach the database at all");
        sql.Should().AllSatisfy(statement => statement.Should().NotContain(
            "\"Prompt\"",
            "at tens of thousands of cells, hydrating every prompt and answer to print two integers IS the defect"));
        sql.Should().AllSatisfy(statement => statement.Should().NotContain("\"MetadataJson\""));
        string.Join(" ", sql).Should().Contain("count(", "two integers are a COUNT, not a scan the client folds");
    }

    [Fact]
    public async Task A_run_nobody_has_scored_yet_counts_zero_rather_than_failing()
    {
        var (runId, _) = await SeedAsync(EngineKind.Qln, questions: 1, lanes: 1);
        var store = new Bench.Infrastructure.Persistence.PostgresResultStore(postgres.NewContext());

        (await store.ScoreboardAsync(runId, Ct)).Should().Be(new RunScoreboard(0, 0));
    }

    /// <summary>A context that keeps every SQL statement it issued, so the QUERY SHAPE can be asserted.
    /// Timing would prove nothing here — the defect is invisible at three rows and fatal at fifty
    /// thousand, which is exactly the range a stopwatch cannot distinguish.</summary>
    private BenchDbContext Recording(List<string> sql) =>
        new(new DbContextOptionsBuilder<BenchDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .LogTo(sql.Add, [DbLoggerCategory.Database.Command.Name])
            .Options);

    private static LegResult Result(Guid cellId, double recall, double latency = 0) => LegResult.Of(
        cellId, "q", "a",
        [
            StoredMetric.Numeric("Anchor recall", recall, "", failed: recall == 0, recall > 0 ? "Exceptional" : "Unacceptable"),
            .. latency > 0 ? new[] { StoredMetric.Numeric("Latency ms", latency, "", false, "Average") } : [],
        ],
        Noon);

    private async Task<(Guid RunId, IReadOnlyList<Guid> Cells)> SeedAsync(EngineKind engine, int questions, int lanes)
    {
        var commit = CommitSha.Parse(new string('b', 40)).Ok();
        var target = MeasurementTarget.At(RepoUrl.Parse("https://example.invalid/x.git").Ok(), commit);
        var run = BenchRun.Planned("results", target, new EngineRef(engine, "", "", "fp"), "s@v1#abc", Noon);

        var questionList = Enumerable.Range(1, questions)
            .Select(i => new Question($"q{i}", $"prompt {i}", [Expectation.File(SourceAnchor.File($"src/F{i}.cs", commit))], string.Empty))
            .ToList();

        var cells = Matrix.Plan(
            questionList,
            repeats: 1,
            [new Subject(ModelRef.Parse("m", ModelHosting.Local).Ok(), Sampling.Deterministic(1))],
            [.. Enumerable.Range(1, lanes).Select(i => Lane.Named($"lane{i}"))]).Ok()
            .Select(c => RunCell.Pending(run.Id, c))
            .ToList();

        await postgres.NewStore(new TestClock(Noon)).CreateAsync(run, cells, Ct);

        return (run.Id, [.. cells.Select(c => c.Id)]);
    }
}
