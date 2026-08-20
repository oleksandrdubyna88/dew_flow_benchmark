using Bench.Application;
using Bench.Domain.Runs;
using Bench.Domain.Splitting;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Domain.Variants;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Variants;
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
        var store = postgres.NewResults();

        var metric = StoredMetric.Numeric("Anchor recall", 1, "surfaced", failed: false, "Exceptional")
            .With(new Dictionary<string, string> { ["anchor"] = "src/A.cs#A.Foo", ["hitCount"] = "3" });

        await store.SaveAsync(LegResult.Of(cells[0].Id, "where is X?", "in A.Foo", [metric], Noon), Ct);

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
        var store = postgres.NewResults();
        var metric = StoredMetric.Boolean("Answered", true, "", false, "Good");

        await store.SaveAsync(LegResult.Of(cells[0].Id, "q", "a", [metric], Noon), Ct);
        var second = await store.SaveAsync(LegResult.Of(cells[0].Id, "q", "different", [metric], Noon), Ct);

        second.Reason().Should().Contain("already has a result").And.Contain("rather than a revision");
    }

    [Fact]
    public async Task A_result_without_a_leg_is_refused_because_it_would_have_no_measurement_key()
    {
        var store = postgres.NewResults();

        var orphan = await store.SaveAsync(
            LegResult.Of(Guid.CreateVersion7(), "q", "a", [StoredMetric.Boolean("x", true, "", false, "Good")], Noon), Ct);

        orphan.Reason().Should().Contain("no measurement key");
    }

    [Fact]
    public async Task The_average_of_one_metric_per_engine_is_a_query_not_a_full_scan()
    {
        var (qlnRun, qlnCells) = await SeedAsync(EngineKind.Qln, questions: 2, lanes: 1);
        var store = postgres.NewResults();

        // Two legs on one engine: one found its anchor, one did not. Each also carries a SECOND metric
        // with a deliberately different value, so an implementation that forgot to filter by name would
        // average the two together and land somewhere other than 0.5.
        await store.SaveAsync(Result(qlnCells[0].Id, recall: 1, latency: 900), Ct);
        await store.SaveAsync(Result(qlnCells[1].Id, recall: 0, latency: 900), Ct);

        var byEngine = await store.AverageByAsync(qlnRun, ReportDimension.Engine, "Anchor recall", QuestionScope.All, Ct);

        byEngine.Should().ContainSingle();
        // The engine's IDENTITY, not its kind. A row labelled "Qln" collapses every engine this project can
        // tell apart — endpoint, version, index fingerprint, and the COMPUTE BACKEND — into one, which is the
        // defect the backend axis exists to end. Two sidecars are two arms; they must not be one row here.
        byEngine[0].Dimension.Should().Be("Qln|||fp|");
        byEngine[0].Average.Should().Be(0.5, "only the named metric may enter the aggregate");
        byEngine[0].Legs.Should().Be(2, "a mean over two legs and a mean over two hundred are different claims");

        (await store.AverageByAsync(qlnRun, ReportDimension.Engine, "Latency ms", QuestionScope.All, Ct))[0].Average.Should().Be(900);
        (await store.AverageByAsync(qlnRun, ReportDimension.Engine, "No such metric", QuestionScope.All, Ct))
            .Should().BeEmpty("an absent metric is an empty result, never a zero");
    }

    [Fact]
    public async Task Averaging_by_lane_separates_the_lanes_of_one_run()
    {
        var (runId, cells) = await SeedAsync(EngineKind.NoRetrieval, questions: 1, lanes: 2);
        var store = postgres.NewResults();

        await store.SaveAsync(Result(cells[0].Id, recall: 1), Ct);
        await store.SaveAsync(Result(cells[1].Id, recall: 0), Ct);

        var byLane = await store.AverageByAsync(runId, ReportDimension.Lane, "Anchor recall", QuestionScope.All, Ct);

        byLane.Should().HaveCount(2);
        byLane.Select(x => x.Average).Should().BeEquivalentTo([1.0, 0.0]);
    }

    [Fact]
    public async Task A_boolean_metric_aggregates_as_a_pass_rate()
    {
        var (runId, cells) = await SeedAsync(EngineKind.Qln, questions: 2, lanes: 1);
        var store = postgres.NewResults();

        await store.SaveAsync(LegResult.Of(cells[0].Id, "q", "a", [StoredMetric.Boolean("Hidden tests", true, "", false, "Good")], Noon), Ct);
        await store.SaveAsync(LegResult.Of(cells[1].Id, "q", "a", [StoredMetric.Boolean("Hidden tests", false, "", true, "Unacceptable")], Noon), Ct);

        (await store.AverageByAsync(runId, ReportDimension.Engine, "Hidden tests", QuestionScope.All, Ct))[0].Average
            .Should().Be(0.5, "a pass rate and a numeric score have to aggregate the same way");
    }

    [Fact]
    public async Task A_metric_the_library_produced_survives_the_round_trip_back_into_its_own_model()
    {
        var (runId, cells) = await SeedAsync(EngineKind.Qln, 1, 1);
        var store = postgres.NewResults();

        var theirs = new NumericMetric("Anchor recall", 1)
        {
            Reason = "surfaced among 3 hits",
            Interpretation = new EvaluationMetricInterpretation(EvaluationRating.Exceptional, failed: false, "found"),
        };
        theirs.AddOrUpdateMetadata("anchor", "src/A.cs#A.Foo");

        await store.SaveAsync(LegResult.Of(cells[0].Id, "q", "a", [MetricCodec.Encode(theirs)], Noon), Ct);
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
        var store = new Bench.Infrastructure.Persistence.PostgresResultStore(db, TimeProvider.System);

        await store.SaveAsync(Result(cells[0].Id, recall: 1), Ct);
        await store.SaveAsync(Result(cells[1].Id, recall: 1), Ct);
        await store.SaveAsync(Result(cells[2].Id, recall: 0), Ct);
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
    public async Task Averaging_by_subject_separates_the_models_of_one_run()
    {
        var (runId, cells) = await SeedAsync(EngineKind.Qln, questions: 1, lanes: 1, subjects: ["fast", "slow"]);
        var store = postgres.NewResults();

        foreach (var cell in cells)
        {
            await store.SaveAsync(Result(cell.Id, recall: cell.SubjectModelId == "fast" ? 1 : 0), Ct);
        }

        var bySubject = await store.AverageByAsync(runId, ReportDimension.Subject, "Anchor recall", QuestionScope.All, Ct);

        bySubject.Should().HaveCount(2, "a run plans several subjects, and folding them together measures neither");
        bySubject.Single(x => x.Dimension == "fast").Average.Should().Be(1);
        bySubject.Single(x => x.Dimension == "slow").Average.Should().Be(0);
    }

    [Fact]
    public async Task The_control_arm_is_its_own_dimension_value_rather_than_a_blank()
    {
        // A real catalog row, because cells.VariantId is a foreign key: a made-up id is refused by the
        // schema, which is the point — a result naming a variant nobody can look up is not a result.
        var hybrid = RetrievalVariant.Create("hybrid-rrf-256", string.Empty, VariantDefinitionTests.Retrieval().Ok(), Noon).Ok();
        (await new PostgresVariantCatalog(postgres.NewContext()).AddAsync(hybrid, Ct)).Failed().Should().BeFalse();

        var (runId, cells) = await SeedAsync(
            EngineKind.Qln, questions: 1, lanes: 1,
            variants: [VariantSelection.None, hybrid.Select()]);
        var store = postgres.NewResults();

        foreach (var cell in cells)
        {
            await store.SaveAsync(Result(cell.Id, recall: cell.Variant is VariantSelection.Selected ? 1 : 0), Ct);
        }

        var byVariant = await store.AverageByAsync(runId, ReportDimension.Variant, "Anchor recall", QuestionScope.All, Ct);

        // "-" rather than "": the control arm is a leg planned without a variant, and a blank would put it
        // in the same row as a variant whose name failed to store. The mark is the one a leg identity
        // already carries, so a report and a canonical leg name the arm the same way.
        byVariant.Select(x => x.Dimension).Should().BeEquivalentTo(["-", "hybrid-rrf-256"]);
        byVariant.Single(x => x.Dimension == "hybrid-rrf-256").Average.Should().Be(1);
    }

    [Fact]
    public async Task An_average_scoped_to_one_split_half_reads_only_that_halfs_questions()
    {
        var (runId, cells) = await SeedAsync(EngineKind.Qln, questions: 6, lanes: 1);
        var store = postgres.NewResults();

        // The halves are SeedSplit's, not the test's — a hash of (suite id, question id), which is why the
        // scope arrives as ids: Postgres cannot compute it, and a suite has tens of questions where a run
        // has thousands of legs.
        var halves = cells.ToDictionary(c => c.QuestionId, c => SeedSplit.Assign("s", c.QuestionId).Ok());
        var selection = halves.Where(kv => kv.Value == SplitHalf.Selection).Select(kv => kv.Key).ToList();
        var heldOut = halves.Where(kv => kv.Value == SplitHalf.HeldOut).Select(kv => kv.Key).ToList();

        selection.Should().NotBeEmpty("this test says nothing unless both halves have questions");
        heldOut.Should().NotBeEmpty();

        foreach (var cell in cells)
        {
            await store.SaveAsync(Result(cell.Id, recall: selection.Contains(cell.QuestionId) ? 1 : 0), Ct);
        }

        var onSelection = await store.AverageByAsync(
            runId, ReportDimension.Engine, "Anchor recall", QuestionScope.Only(selection), Ct);
        var onHeldOut = await store.AverageByAsync(
            runId, ReportDimension.Engine, "Anchor recall", QuestionScope.Only(heldOut), Ct);

        // A configuration scoring 1 where it was chosen and 0 where it was not is the shape of every false
        // winner this harness exists to catch. Reading the two halves apart is what makes it visible.
        onSelection[0].Average.Should().Be(1);
        onSelection[0].Legs.Should().Be(selection.Count);
        onHeldOut[0].Average.Should().Be(0);
        onHeldOut[0].Legs.Should().Be(heldOut.Count);
    }

    [Fact]
    public async Task A_half_with_no_questions_averages_nothing_rather_than_everything()
    {
        var (runId, cells) = await SeedAsync(EngineKind.Qln, questions: 2, lanes: 1);
        var store = postgres.NewResults();

        await store.SaveAsync(Result(cells[0].Id, recall: 1), Ct);
        await store.SaveAsync(Result(cells[1].Id, recall: 1), Ct);

        var nothing = await store.AverageByAsync(
            runId, ReportDimension.Engine, "Anchor recall", QuestionScope.Only([]), Ct);

        // An empty scope means what it says. Reading it as "no filter" would report the whole suite's mean
        // under the label of a half that holds nothing, which is the one wrong answer here that looks right.
        nothing.Should().BeEmpty("an empty half has nothing to average, and that is not the same as everything");
    }

    [Fact]
    public async Task A_metric_with_no_numeric_reading_is_excluded_from_the_mean_rather_than_counted_as_zero()
    {
        var (runId, cells) = await SeedAsync(EngineKind.Qln, questions: 2, lanes: 1);
        var store = postgres.NewResults();

        await store.SaveAsync(Result(cells[0].Id, recall: 1), Ct);
        await store.SaveAsync(
            LegResult.Of(cells[1].Id, "q", "a", [StoredMetric.Text("Anchor recall", "inconclusive", "", false, "Average")], Noon),
            Ct);

        var byEngine = await store.AverageByAsync(runId, ReportDimension.Engine, "Anchor recall", QuestionScope.All, Ct);

        // 1.0 over ONE leg, never 0.5 over two: "not a number" and "zero" are different facts, and the
        // projection this aggregate now uses is exactly where that rule could have been lost — the reading
        // is still StoredMetric.AsNumber's decision rather than a cast in SQL.
        byEngine[0].Average.Should().Be(1);
        byEngine[0].Legs.Should().Be(1, "a leg whose metric has no numeric reading did not contribute to the mean");
    }

    [Fact]
    public async Task A_question_no_subject_attempted_is_absent_from_the_pass_rates_rather_than_present_as_a_zero()
    {
        var (runId, cells) = await SeedAsync(EngineKind.Qln, questions: 2, lanes: 1, subjects: ["fast", "slow"]);
        var store = postgres.NewResults();

        var unattempted = cells.First(c => c.QuestionId == "q2" && c.SubjectModelId == "slow");

        foreach (var cell in cells.Where(c => c.Id != unattempted.Id))
        {
            await store.SaveAsync(Result(cell.Id, recall: cell.SubjectModelId == "fast" ? 1 : 0), Ct);
        }

        var rates = await store.PassRateByQuestionAndSubjectAsync(runId, "Anchor recall", Ct);

        // Three pairs, not four. QuestionSpread.Unmeasured names the models that never attempted a question,
        // and it can only do that if an absent pair stays absent — a zero here would read as a failure and
        // would make a question look harder than anything measured it to be.
        rates.Should().HaveCount(3);
        rates.Should().NotContain(r => r.QuestionId == "q2" && r.SubjectModelId == "slow");
        rates.Single(r => r.QuestionId == "q1" && r.SubjectModelId == "fast").PassRate.Should().Be(1);
        rates.Single(r => r.QuestionId == "q1" && r.SubjectModelId == "slow").PassRate.Should().Be(0);
    }

    [Fact]
    public async Task The_average_does_not_hydrate_every_prompt_of_the_run_to_compute_one_mean()
    {
        var (runId, cells) = await SeedAsync(EngineKind.Qln, questions: 3, lanes: 1);
        var sql = new List<string>();

        await using var db = Recording(sql);
        var store = new PostgresResultStore(db, TimeProvider.System);

        await store.SaveAsync(Result(cells[0].Id, recall: 1), Ct);
        await store.SaveAsync(Result(cells[1].Id, recall: 1), Ct);
        await store.SaveAsync(Result(cells[2].Id, recall: 0), Ct);
        sql.Clear();

        var byEngine = await store.AverageByAsync(runId, ReportDimension.Engine, "Anchor recall", QuestionScope.All, Ct);

        byEngine[0].Average.Should().BeApproximately(2d / 3, 1e-9, "the arithmetic is the point of the query");
        sql.Should().NotBeEmpty("the aggregate has to reach the database at all");

        // The same assertion, and the same reason, as The_run_summary_does_not_hydrate_the_run_to_count_it:
        // invisible at three rows and fatal at fifty thousand, which is the range a stopwatch cannot tell
        // apart. The diagnosis was written twice in this file's subject — on ScoreboardAsync and on
        // TotalsAsync — and the aggregate beside them still did it.
        sql.Should().AllSatisfy(statement => statement.Should().NotContain(
            "\"Prompt\"",
            "averaging one number must not pull every prompt of the campaign across the wire"));
        sql.Should().AllSatisfy(statement => statement.Should().NotContain("\"Answer\""));
        sql.Should().AllSatisfy(statement => statement.Should().NotContain("\"ResponseMetaJson\""));
    }

    [Fact]
    public async Task A_run_nobody_has_scored_yet_counts_zero_rather_than_failing()
    {
        var (runId, _) = await SeedAsync(EngineKind.Qln, questions: 1, lanes: 1);
        var store = postgres.NewResults();

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

    /// <summary>Seeds a run and returns its cells IN MATRIX ORDER, so a test can address one by index.
    /// <para>
    /// <paramref name="subjects"/> and <paramref name="variants"/> are widenings of the original two-axis
    /// helper rather than a second seeder: the aggregate under test groups along four axes, and a copy of
    /// this method per axis is how the four would drift apart.
    /// </para></summary>
    private async Task<(Guid RunId, IReadOnlyList<RunCell> Cells)> SeedAsync(
        EngineKind engine,
        int questions,
        int lanes,
        IReadOnlyList<string>? subjects = null,
        IReadOnlyList<VariantSelection>? variants = null)
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
            [.. (subjects ?? ["m"]).Select(m => new Subject(ModelRef.Parse(m, ModelHosting.Local).Ok(), Sampling.Deterministic(1)))],
            [.. Enumerable.Range(1, lanes).Select(i => Lane.Named($"lane{i}"))],
            variants ?? [VariantSelection.None]).Ok()
            .Select(c => RunCell.Pending(run.Id, c))
            .ToList();

        await postgres.NewStore(new TestClock(Noon)).CreateAsync(run, cells, Ct);

        return (run.Id, cells);
    }
}
