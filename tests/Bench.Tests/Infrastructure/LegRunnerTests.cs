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

        var result = (await runner.RunNextAsync(runId, Worker("worker-1"), plan, Ct)).Ok();

        result.Answer.Should().Contain("DecorrelatedJitter");
        result.Passed.Should().BeTrue();
        (await runs.ProgressAsync(runId, Ct)).Settled.Should().Be(1);
        (await results.ForRunAsync(runId, Ct)).Should().ContainSingle();
    }

    [Fact]
    public async Task An_answer_that_misses_the_required_term_is_scored_as_a_failure()
    {
        var (runId, plan, runner, _, _) = await ArrangeAsync(new FakeRuntime("it multiplies the delay by a random factor"));

        var result = (await runner.RunNextAsync(runId, Worker("worker-1"), plan, Ct)).Ok();

        result.Passed.Should().BeFalse();
        result.Metrics.Single(m => m.Name.Contains("contains")).Reason
            .Should().Be("'DecorrelatedJitter' was absent, and the answer had to contain it",
                "an agent reads this line, so it must not also parse as the opposite verdict");
    }

    [Fact]
    public async Task A_forbidden_term_fails_the_leg_even_when_the_required_one_is_present()
    {
        var (runId, plan, runner, _, _) = await ArrangeAsync(
            new FakeRuntime("DecorrelatedJitter, applied after N consecutive failures"));

        var result = (await runner.RunNextAsync(runId, Worker("worker-1"), plan, Ct)).Ok();

        result.Metrics.Single(m => m.Name.Contains("excludes")).Failed.Should().BeTrue(
            "the memorisation trap is the point of that expectation");
    }

    [Fact]
    public async Task An_anchor_in_a_lane_that_surfaces_nothing_is_NOT_APPLICABLE_rather_than_a_miss()
    {
        var (runId, plan, runner, _, _) = await ArrangeAsync(new FakeRuntime("DecorrelatedJitter"));

        var result = (await runner.RunNextAsync(runId, Worker("worker-1"), plan, Ct)).Ok();

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

        var refused = await runner.RunNextAsync(runId, Worker("worker-1"), plan, Ct);

        refused.Reason().Should().Contain("connection refused");
        (await results.ForRunAsync(runId, Ct)).Should().BeEmpty("a subject that was never reached did not get anything wrong");
        (await runs.ProgressAsync(runId, Ct)).Settled.Should().Be(1);
    }

    [Fact]
    public async Task An_answer_cut_off_at_a_ceiling_settles_as_a_CAP_rather_than_a_completion()
    {
        var (runId, plan, runner, runs, _) = await ArrangeAsync(
            new FakeRuntime("DecorrelatedJitter", StopReason.LengthCapped));

        await runner.RunNextAsync(runId, Worker("worker-1"), plan, Ct);

        var cell = await CellAsync(runId);
        cell.OutcomeKind.Should().Be(LegOutcomeKind.CapExceeded, "scored as a wrong answer it would measure the ceiling");
        (await runs.ProgressAsync(runId, Ct)).Settled.Should().Be(1);
    }

    [Fact]
    public async Task A_leg_stopped_by_its_own_wall_budget_is_a_cap_rather_than_a_crash()
    {
        var clock = new TestClock(Noon);
        var (runId, plan, runner, runs, results) = await ArrangeAsync(
            SlowRuntime.Taking(clock, TimeSpan.FromSeconds(90), "qwen3-coder:latest did not answer within 60s"), clock);

        await runner.RunNextAsync(runId, Worker("worker-1"), Capped(plan, seconds: 60), Ct);

        var cell = await CellAsync(runId);
        cell.OutcomeKind.Should().Be(
            LegOutcomeKind.CapExceeded,
            "a leg its OWN ceiling stopped is a recorded outcome the campaign continues past, not a broken harness");
        cell.OutcomeDetail.Should().Contain("Wall").And.Contain("60", "the ceiling that stopped it is the fact worth storing");
        (await results.ForRunAsync(runId, Ct)).Should().BeEmpty("a subject cut off at a ceiling did not get anything wrong");
        (await runs.ProgressAsync(runId, Ct)).Settled.Should().Be(1);
    }

    [Fact]
    public async Task A_leg_that_failed_INSIDE_its_wall_budget_is_still_a_crash()
    {
        var clock = new TestClock(Noon);
        var (runId, plan, runner, _, _) = await ArrangeAsync(
            SlowRuntime.Taking(clock, TimeSpan.FromSeconds(2), "connection refused"), clock);

        await runner.RunNextAsync(runId, Worker("worker-1"), Capped(plan, seconds: 60), Ct);

        (await CellAsync(runId)).OutcomeKind.Should().Be(
            LegOutcomeKind.Crashed,
            "an endpoint that refused in two seconds is broken — calling that a ceiling would hide the real fault");
    }

    [Fact]
    public async Task Every_call_of_a_leg_is_handed_what_the_LEG_has_left_rather_than_the_whole_budget()
    {
        var clock = new TestClock(Noon);
        var runtime = SlowRuntime.Answering(clock, TimeSpan.FromSeconds(10), "DecorrelatedJitter");
        var (runId, plan, runner, _, _) = await ArrangeAsync(runtime, clock);

        // The leg starts 0s in with a 60s ceiling, so the first call may spend all 60. What this asserts is
        // that the ceiling travels as a value at all — the loop that will call twice reads the remainder
        // from the same deadline rather than starting a fresh 60s per turn.
        await runner.RunNextAsync(runId, Worker("worker-1"), Capped(plan, seconds: 60), Ct);

        runtime.WallSeen.Should().BeGreaterThan(0m, "a call sent with no wall entry falls back to the runtime's 10-minute default");
        runtime.WallSeen.Should().BeLessThanOrEqualTo(60m, "no single call may be given more than the leg has left");
    }

    private static LegPlan Capped(LegPlan plan, int seconds) =>
        plan with { Budgets = [Budget.Of(BudgetKind.Wall, BudgetScope.Question, seconds).ConfirmedBy("fake")] };

    [Fact]
    public async Task Each_subjects_leg_is_sent_to_THAT_subjects_endpoint()
    {
        var clock = new TestClock(Noon);
        var runtime = new RecordingRuntime(clock);
        var (runId, plan, runner, _, _) = await ArrangeAsync(runtime, clock, TwoSubjects());

        await runner.RunNextAsync(runId, Worker("worker-1"), plan, Ct);
        await runner.RunNextAsync(runId, Worker("worker-1"), plan, Ct);

        // A run has always been able to plan several subjects while the runner held ONE endpoint — so both
        // legs went to the first model and were labelled with the cell's subject. Nothing in the report
        // would have shown it: two models named, one measured.
        runtime.Sent.Select(s => s.BaseUrl).Should().BeEquivalentTo(
            ["http://127.0.0.1:11434/v1", "http://127.0.0.1:11435/v1"],
            "each cell names its subject, and the endpoint is looked up rather than assumed");
        runtime.Sent.Select(s => s.ModelId).Should().BeEquivalentTo(["qwen3-coder:latest", "gemma3:latest"]);
    }

    [Fact]
    public async Task A_cell_whose_subject_this_run_cannot_reach_is_settled_rather_than_sent_somewhere_else()
    {
        var clock = new TestClock(Noon);
        var runtime = new RecordingRuntime(clock);
        var (runId, plan, runner, runs, _) = await ArrangeAsync(runtime, clock, TwoSubjects());

        // A roster holding only the FIRST subject: the second subject's cell has nowhere honest to go.
        var partial = plan with { Subjects = SubjectRoster.Of([plan.Subjects.Entries[0]]) };

        await runner.RunNextAsync(runId, Worker("worker-1"), partial, Ct);
        var second = await runner.RunNextAsync(runId, Worker("worker-1"), partial, Ct);

        second.Reason().Should().Contain("no endpoint for subject",
            "a leg sent to another subject's endpoint would carry this cell's label and be invisible afterwards");
        runtime.Sent.Should().ContainSingle("the unreachable subject's leg is settled, never sent");
        (await runs.ProgressAsync(runId, Ct)).Settled.Should().Be(2);
    }

    [Fact]
    public async Task A_leg_scored_but_never_settled_is_finished_rather_than_measured_twice()
    {
        var (runId, plan, runner, runs, results) = await ArrangeAsync(new FakeRuntime("DecorrelatedJitter"));
        await runner.RunNextAsync(runId, Worker("worker-1"), plan, Ct);

        // The crash window: the result is durable, the cell is not settled. The sweep hands it back.
        var cell = await CellAsync(runId);
        await ReopenAsync(cell.Id);

        var second = await runner.RunNextAsync(runId, Worker("worker-2"), plan, Ct);

        second.Reason().Should().Contain("already scored");
        (await results.ForRunAsync(runId, Ct)).Should().ContainSingle("the leg is measured once, not once per crash");
        (await runs.ProgressAsync(runId, Ct)).Settled.Should().Be(1);
    }

    [Fact]
    public async Task Nothing_to_claim_is_an_answer_rather_than_a_fault()
    {
        var (runId, plan, runner, _, _) = await ArrangeAsync(new FakeRuntime("DecorrelatedJitter"));
        await runner.RunNextAsync(runId, Worker("worker-1"), plan, Ct);

        (await runner.RunNextAsync(runId, Worker("worker-2"), plan, Ct))
            .Reason().Should().Contain("no pending cell", "several workers draining a queue is the expected shape");
    }

    [Fact]
    public async Task A_cell_naming_a_question_the_suite_does_not_have_is_settled_not_left_claimed()
    {
        var (runId, plan, runner, runs, _) = await ArrangeAsync(new FakeRuntime("x"));
        var otherSuite = plan with { Suite = SuiteOf("different", "other-question") };

        var refused = await runner.RunNextAsync(runId, Worker("worker-1"), otherSuite, Ct);

        refused.Reason().Should().Contain("has no question");
        (await runs.ProgressAsync(runId, Ct)).Pending.Should().Be(0, "a cell nobody can run must not stay claimed forever");
    }

    /// <summary>A worker on this machine — a real host and a real pid, because a claim that records
    /// neither is one the sweep would take back the moment the window passed.</summary>
    private static WorkerIdentity Worker(string label) => WorkerIdentity.Here(label);

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
        cell.OwnerHost = string.Empty;
        cell.OwnerPid = 0;
        await db.SaveChangesAsync(Ct);
    }

    private Task<(Guid RunId, LegPlan Plan, LegRunner Runner, PostgresRunStore Runs, PostgresResultStore Results)>
        ArrangeAsync(IModelRuntime runtime) => ArrangeAsync(runtime, new TestClock(Noon));

    private Task<(Guid RunId, LegPlan Plan, LegRunner Runner, PostgresRunStore Runs, PostgresResultStore Results)>
        ArrangeAsync(IModelRuntime runtime, TestClock clock) => ArrangeAsync(runtime, clock, OneSubject());

    private async Task<(Guid RunId, LegPlan Plan, LegRunner Runner, PostgresRunStore Runs, PostgresResultStore Results)>
        ArrangeAsync(IModelRuntime runtime, TestClock clock, SubjectRoster roster)
    {
        var suite = SuiteOf("polly-smoke", "retry-jitter-formula");
        var target = MeasurementTarget.At(RepoUrl.Parse("https://github.com/App-vNext/Polly.git").Ok(), Commit);
        var run = BenchRun.Planned("first", target, EngineRef.Filesystem(), suite.Stamp, Noon);

        var cells = Matrix.Plan(suite.Questions, repeats: 1, roster.Subjects, [Lane.Named("no-tools")]).Ok()
            .Select(c => RunCell.Pending(run.Id, c)).ToList();

        var runs = new PostgresRunStore(postgres.NewContext(), clock);
        var results = postgres.NewResults();
        await runs.CreateAsync(run, cells, Ct);

        var plan = LegPlan.Reading(suite, roster);

        // NoRetriever, because every cell here is planned without a variant and therefore runs the control
        // arm: the runner must not ask a retriever anything, and a retriever that refuses everything is the
        // fake that proves it.
        return (
            run.Id,
            plan,
            new LegRunner(
                runs, results, runtime, new NoRetriever(), new NoHardwareSampler(), Loop(runtime), clock,
                NullLogger<LegRunner>.Instance),
            runs,
            results);
    }

    private static SubjectRoster OneSubject() => Roster(("qwen3-coder:latest", "http://127.0.0.1:11434/v1"));

    /// <summary>Two subjects at two endpoints — the shape a test with a model registry actually has, and
    /// the one a single-endpoint runner measured wrong while naming both.</summary>
    private static SubjectRoster TwoSubjects() => Roster(
        ("qwen3-coder:latest", "http://127.0.0.1:11434/v1"),
        ("gemma3:latest", "http://127.0.0.1:11435/v1"));

    private static SubjectRoster Roster(params (string ModelId, string BaseUrl)[] subjects) =>
        SubjectRoster.Of([.. subjects.Select(s => new RosterEntry(
            ModelEndpoint.Parse(ModelRef.Parse(s.ModelId, ModelHosting.Local).Ok(), s.BaseUrl).Ok(),
            Sampling.Deterministic(7)))]);

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

    /// <summary>A runtime that records WHERE each leg was sent. The multi-subject defect is invisible in a
    /// result — both legs succeed and both carry their cell's label — so the only place it can be observed
    /// is the request itself.</summary>
    private sealed class RecordingRuntime(TestClock clock) : IModelRuntime
    {
        public List<(string ModelId, string BaseUrl)> Sent { get; } = [];

        public ModelHosting Hosting => ModelHosting.Local;

        public Task<Outcome<string>> AcceptBudgetAsync(Budget budget, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<string>.Success("fake"));

        public Task<Outcome<ModelAnswer>> AskAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            Sent.Add((request.Endpoint.Model.Id, request.Endpoint.BaseUrl));
            clock.Now += TimeSpan.FromSeconds(1);

            return Task.FromResult(Outcome<ModelAnswer>.Success(new ModelAnswer(
                Captured.Text("DecorrelatedJitter"),
                CapturedCount.Number(10),
                CapturedCount.Number(5),
                TimeSpan.FromSeconds(1),
                SamplingAsSent.From(request.Sampling, "request-body"),
                StopReason.Completed,
                "stop")));
        }
    }

    /// <summary>A runtime that BURNS TIME. The wall budget is a statement about elapsed time, so the only
    /// fake that can test it is one that moves the clock the runner reads — a fake that answers instantly
    /// proves the ceiling was configured, never that it was enforced.</summary>
    private sealed class SlowRuntime(TestClock clock, TimeSpan takes, string answer, string failure) : IModelRuntime
    {
        public static SlowRuntime Taking(TestClock clock, TimeSpan takes, string failure) =>
            new(clock, takes, string.Empty, failure);

        public static SlowRuntime Answering(TestClock clock, TimeSpan takes, string answer) =>
            new(clock, takes, answer, string.Empty);

        /// <summary>The wall ceiling this runtime was actually handed, in seconds. Zero means the request
        /// carried none — which is the defect, since the runtime then falls back to its own default.</summary>
        public decimal WallSeen { get; private set; }

        public ModelHosting Hosting => ModelHosting.Local;

        public Task<Outcome<string>> AcceptBudgetAsync(Budget budget, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<string>.Success("fake"));

        public Task<Outcome<ModelAnswer>> AskAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            WallSeen = request.Budgets.FirstOrDefault(b => b.Kind == BudgetKind.Wall)?.Limit ?? 0m;
            clock.Now += takes;

            return Task.FromResult(failure.Length > 0
                ? Outcome<ModelAnswer>.Failure(failure)
                : Outcome<ModelAnswer>.Success(new ModelAnswer(
                    Captured.Text(answer),
                    CapturedCount.Number(100),
                    CapturedCount.Number(20),
                    takes,
                    SamplingAsSent.From(request.Sampling, "request-body"),
                    StopReason.Completed,
                    "stop")));
        }
    }

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
    /// <summary>The tool loop, which no test here reaches: every cell in this file is planned without a
    /// lane, so it resolves to the floor and the runner takes the single-completion path it always took.
    /// Constructed rather than faked for exactly that reason — a fake would assert something about a
    /// collaborator these tests deliberately never use.</summary>
    private static ToolLoopRunner Loop(IModelRuntime runtime) =>
        new(runtime, NullLogger<ToolLoopRunner>.Instance);

}
