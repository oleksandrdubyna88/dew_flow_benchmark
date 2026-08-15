using Bench.Application;
using Bench.Domain;
using Bench.Domain.Models;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Domain.Trace;
using Bench.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The assembly: claim a cell, ask the model, score, store, settle. Against the REAL stores,
/// because every piece of this chain passed its own tests in isolation and none of them had ever been
/// driven in order.</summary>
[Collection("postgres")]
public sealed class LegRunnerTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly CommitSha Commit = CommitSha.Parse(new string('c', 40)).Ok();

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_leg_runs_end_to_end_and_leaves_a_scored_settled_cell()
    {
        var (runId, plan, runner, runs, results) = await ArrangeAsync(
            new FakeRuntime("the retry uses a DecorrelatedJitter backoff"));

        var result = (await runner.RunNextAsync(runId, "worker-1", plan, Ct)).Ok();

        result.Answer.Should().Contain("DecorrelatedJitter");
        result.Passed.Should().BeTrue();
        (await runs.ProgressAsync(runId, Ct)).Settled.Should().Be(1);
        (await results.ForRunAsync(runId, Ct)).Should().ContainSingle();
    }

    [Fact]
    public async Task An_answer_that_misses_the_required_term_is_scored_as_a_failure()
    {
        var (runId, plan, runner, _, _) = await ArrangeAsync(new FakeRuntime("it multiplies the delay by a random factor"));

        var result = (await runner.RunNextAsync(runId, "worker-1", plan, Ct)).Ok();

        result.Passed.Should().BeFalse();
        result.Metrics.Single(m => m.Name.Contains("contains")).Reason.Should().Contain("must not have been");
    }

    [Fact]
    public async Task A_forbidden_term_fails_the_leg_even_when_the_required_one_is_present()
    {
        var (runId, plan, runner, _, _) = await ArrangeAsync(
            new FakeRuntime("DecorrelatedJitter, applied after N consecutive failures"));

        var result = (await runner.RunNextAsync(runId, "worker-1", plan, Ct)).Ok();

        result.Metrics.Single(m => m.Name.Contains("excludes")).Failed.Should().BeTrue(
            "the memorisation trap is the point of that expectation");
    }

    [Fact]
    public async Task An_anchor_in_a_lane_that_surfaces_nothing_is_NOT_APPLICABLE_rather_than_a_miss()
    {
        var (runId, plan, runner, _, _) = await ArrangeAsync(new FakeRuntime("DecorrelatedJitter"));

        var result = (await runner.RunNextAsync(runId, "worker-1", plan, Ct)).Ok();

        var recall = result.Metrics.Single(m => m.Name == AnswerScoring.AnchorRecall);
        recall.Kind.Should().Be(MetricKind.Text);
        recall.Value.Should().Be("not applicable");
        recall.Failed.Should().BeFalse("scoring it zero would make the no-tools baseline look worse than it is");
        recall.AsNumber().Failed().Should().BeTrue("and it must stay out of the numeric aggregate rather than dilute it");
    }

    [Fact]
    public async Task A_model_that_could_not_be_reached_settles_the_cell_and_stores_nothing()
    {
        var (runId, plan, runner, runs, results) = await ArrangeAsync(FakeRuntime.Unreachable("connection refused"));

        var refused = await runner.RunNextAsync(runId, "worker-1", plan, Ct);

        refused.Reason().Should().Contain("connection refused");
        (await results.ForRunAsync(runId, Ct)).Should().BeEmpty("a subject that was never reached did not get anything wrong");
        (await runs.ProgressAsync(runId, Ct)).Settled.Should().Be(1);
    }

    [Fact]
    public async Task An_answer_cut_off_at_a_ceiling_settles_as_a_CAP_rather_than_a_completion()
    {
        var (runId, plan, runner, runs, _) = await ArrangeAsync(
            new FakeRuntime("DecorrelatedJitter", StopReason.LengthCapped));

        await runner.RunNextAsync(runId, "worker-1", plan, Ct);

        var cell = await CellAsync(runId);
        cell.OutcomeKind.Should().Be(LegOutcomeKind.CapExceeded, "scored as a wrong answer it would measure the ceiling");
        (await runs.ProgressAsync(runId, Ct)).Settled.Should().Be(1);
    }

    [Fact]
    public async Task A_leg_scored_but_never_settled_is_finished_rather_than_measured_twice()
    {
        var (runId, plan, runner, runs, results) = await ArrangeAsync(new FakeRuntime("DecorrelatedJitter"));
        await runner.RunNextAsync(runId, "worker-1", plan, Ct);

        // The crash window: the result is durable, the cell is not settled. The sweep hands it back.
        var cell = await CellAsync(runId);
        await ReopenAsync(cell.Id);

        var second = await runner.RunNextAsync(runId, "worker-2", plan, Ct);

        second.Reason().Should().Contain("already scored");
        (await results.ForRunAsync(runId, Ct)).Should().ContainSingle("the leg is measured once, not once per crash");
        (await runs.ProgressAsync(runId, Ct)).Settled.Should().Be(1);
    }

    [Fact]
    public async Task Nothing_to_claim_is_an_answer_rather_than_a_fault()
    {
        var (runId, plan, runner, _, _) = await ArrangeAsync(new FakeRuntime("DecorrelatedJitter"));
        await runner.RunNextAsync(runId, "worker-1", plan, Ct);

        (await runner.RunNextAsync(runId, "worker-2", plan, Ct))
            .Reason().Should().Contain("no pending cell", "several workers draining a queue is the expected shape");
    }

    [Fact]
    public async Task A_cell_naming_a_question_the_suite_does_not_have_is_settled_not_left_claimed()
    {
        var (runId, plan, runner, runs, _) = await ArrangeAsync(new FakeRuntime("x"));
        var otherSuite = plan with { Suite = SuiteOf("different", "other-question") };

        var refused = await runner.RunNextAsync(runId, "worker-1", otherSuite, Ct);

        refused.Reason().Should().Contain("has no question");
        (await runs.ProgressAsync(runId, Ct)).Pending.Should().Be(0, "a cell nobody can run must not stay claimed forever");
    }

    private async Task<CellRow> CellAsync(Guid runId)
    {
        await using var db = postgres.NewContext();
        return await Task.FromResult(db.Cells.First(c => c.RunId == runId));
    }

    private async Task ReopenAsync(Guid cellId)
    {
        await using var db = postgres.NewContext();
        var cell = db.Cells.First(c => c.Id == cellId);
        cell.State = CellState.Pending;
        cell.Owner = string.Empty;
        await db.SaveChangesAsync(Ct);
    }

    private async Task<(Guid RunId, LegPlan Plan, LegRunner Runner, PostgresRunStore Runs, PostgresResultStore Results)>
        ArrangeAsync(IModelRuntime runtime)
    {
        var clock = new TestClock(Noon);
        var suite = SuiteOf("polly-smoke", "retry-jitter-formula");
        var target = MeasurementTarget.At(RepoUrl.Parse("https://github.com/App-vNext/Polly.git").Ok(), Commit);
        var run = BenchRun.Planned("first", target, EngineRef.Filesystem(), suite.Stamp, Noon);

        var cells = Matrix.Plan(
            suite.Questions, repeats: 1,
            [new Subject(ModelRef.Parse("qwen3-coder:latest", ModelHosting.Local).Ok(), Sampling.Deterministic(7))],
            [Lane.Named("no-tools")]).Ok()
            .Select(c => RunCell.Pending(run.Id, c)).ToList();

        var runs = new PostgresRunStore(postgres.NewContext(), clock);
        var results = new PostgresResultStore(postgres.NewContext());
        await runs.CreateAsync(run, cells, Ct);

        var plan = LegPlan.Reading(
            suite,
            ModelEndpoint.Parse(ModelRef.Parse("qwen3-coder:latest", ModelHosting.Local).Ok(), "http://127.0.0.1:11434/v1").Ok(),
            Sampling.Deterministic(7));

        return (run.Id, plan, new LegRunner(runs, results, runtime, clock, NullLogger<LegRunner>.Instance), runs, results);
    }

    private static Suite SuiteOf(string suiteId, string questionId) =>
        Suite.Draft(suiteId).With(new Question(
            questionId,
            "How does Polly compute the delay for an exponential retry with jitter?",
            [
                Expectation.Member(SourceAnchor.Member(
                    "src/Polly.Core/Retry/RetryHelper.cs", "RetryHelper.DecorrelatedJitterBackoffV2", new LineSpan(75, 111), Commit)),
                new Expectation(ExpectationKind.AnswerContains, SourceAnchor.File("", Commit), "DecorrelatedJitter", true),
                new Expectation(ExpectationKind.AnswerExcludes, SourceAnchor.File("", Commit), "consecutive", true),
            ],
            string.Empty)).Ok().Freeze().Ok();

    private sealed class FakeRuntime(string answer, StopReason stop = StopReason.Completed, string failure = "")
        : IModelRuntime
    {
        public static FakeRuntime Unreachable(string why) => new(string.Empty, failure: why);

        public ModelHosting Hosting => ModelHosting.Local;

        public Task<Outcome<string>> AcceptBudgetAsync(Budget budget, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<string>.Success("fake"));

        public Task<Outcome<ModelAnswer>> AskAsync(ModelRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(failure.Length > 0
                ? Outcome<ModelAnswer>.Failure(failure)
                : Outcome<ModelAnswer>.Success(new ModelAnswer(
                    Captured.Text(answer),
                    CapturedCount.Number(100),
                    CapturedCount.Number(20),
                    TimeSpan.FromMilliseconds(250),
                    SamplingAsSent.From(request.Sampling, "request-body"),
                    stop,
                    stop.ToString())));
    }
}
