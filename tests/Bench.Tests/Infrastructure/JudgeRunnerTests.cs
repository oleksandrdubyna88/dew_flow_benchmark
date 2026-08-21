using Bench.Application;
using Bench.Domain;
using Bench.Domain.Models;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Infrastructure.Models;
using Bench.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>Re-scoring a stored run with an arbiter, against the real store — because the whole claim
/// being made here ("a second judge costs its own inference and nothing else") is a claim about what the
/// database does, and an in-memory fake would agree with it for free.</summary>
[Collection("postgres")]
public sealed class JudgeRunnerTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly CommitSha Commit = CommitSha.Parse(new string('c', 40)).Ok();

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_arbiter_appends_its_verdict_without_touching_the_mechanical_score()
    {
        var (runId, suite, results) = await ArrangeAsync("the model named the wrong helper");

        var report = (await RunnerFor(results).JudgeRunAsync(runId, suite, Judge("opus", yes: false), Ct)).Ok();

        report.Judged.Should().Be(1);
        var stored = (await results.ForRunAsync(runId, Ct)).Single();
        stored.Answer.Should().Be("the model named the wrong helper", "the evidence is never rewritten");
        stored.Metrics.Should().HaveCount(3, "two mechanical metrics plus the arbiter's, side by side");
        stored.Metrics.Single(m => m.Name.Contains("Judge")).Value.Should().Be("false");
    }

    [Fact]
    public async Task A_second_arbiter_re_scores_the_same_answers_and_the_first_verdict_survives()
    {
        var (runId, suite, results) = await ArrangeAsync("DecorrelatedJitter, decorrelated and stateful");
        await RunnerFor(results).JudgeRunAsync(runId, suite, Judge("strict", yes: false), Ct);

        var second = (await RunnerFor(results).JudgeRunAsync(runId, suite, Judge("lenient", yes: true), Ct)).Ok();

        second.Judged.Should().Be(1, "a different arbiter sees every leg, because its metric name differs");
        var metrics = (await results.ForRunAsync(runId, Ct)).Single().Metrics;
        metrics.Single(m => m.Name.Contains("strict")).Value.Should().Be("false");
        metrics.Single(m => m.Name.Contains("lenient")).Value.Should().Be("true", "two arbiters may disagree and both readings stay");
    }

    [Fact]
    public async Task The_same_arbiter_run_twice_judges_nothing_the_second_time()
    {
        var (runId, suite, results) = await ArrangeAsync("DecorrelatedJitter");
        await RunnerFor(results).JudgeRunAsync(runId, suite, Judge("opus", yes: true), Ct);

        var again = await RunnerFor(results).JudgeRunAsync(runId, suite, Judge("opus", yes: true), Ct);

        again.Reason().Should().Contain("already read them all");
        (await results.ForRunAsync(runId, Ct)).Single().Metrics
            .Count(m => m.Name.Contains("opus")).Should().Be(1, "re-running an arbiter must not accumulate duplicates");
    }

    [Fact]
    public async Task An_unreachable_arbiter_leaves_the_leg_NOT_JUDGED_rather_than_failed()
    {
        var (runId, suite, results) = await ArrangeAsync("DecorrelatedJitter");

        var report = (await RunnerFor(results).JudgeRunAsync(runId, suite, FakeJudge.Unreachable("opus", "connection refused"), Ct)).Ok();

        report.Silent.Should().Be(1);
        report.Judged.Should().Be(0);
        var verdict = (await results.ForRunAsync(runId, Ct)).Single().Metrics.Single(m => m.Name.Contains("Judge"));
        verdict.Value.Should().Be("not judged");
        verdict.Failed.Should().BeFalse("an arbiter that said nothing must not be recorded as one that said no");
    }

    [Fact]
    public async Task The_wrong_suite_is_refused_whole_rather_than_judged_leg_by_leg()
    {
        var (runId, _, results) = await ArrangeAsync("DecorrelatedJitter");
        var other = SuiteOf("different", "some-other-question", "a reference for a question this run never asked");

        var refused = await RunnerFor(results).JudgeRunAsync(runId, other, Judge("opus", yes: true), Ct);

        refused.Reason().Should().Contain("is not the suite that produced run");
        (await results.ForRunAsync(runId, Ct)).Single().Metrics
            .Should().NotContain(m => m.Name.Contains("Judge"),
                "a verdict against a reference from a DIFFERENT suite would look exactly like a normal one");
    }

    [Fact]
    public async Task Self_judging_is_allowed_counted_and_marked()
    {
        var (runId, suite, results) = await ArrangeAsync("DecorrelatedJitter");

        var report = (await RunnerFor(results).JudgeRunAsync(
            runId, suite, Judge("qwen3-coder:latest", yes: true), Ct)).Ok();

        report.SelfJudged.Should().Be(1, "the subject of this run IS qwen3-coder:latest");
        report.Describe.Should().Contain("SELF-JUDGED");
        (await results.ForRunAsync(runId, Ct)).Single().Metrics
            .Single(m => m.Name.Contains("Judge")).Metadata["selfJudged"].Should().Be("true");
    }

    [Fact]
    public async Task Judging_a_leg_closes_its_pending_Judge_phase()
    {
        var (runId, suite, results) = await ArrangeAsync("the mechanism, stated");
        var runs = RunStore();
        var cellId = await CellAsync(runId);

        // A fix leg's shape: Investigate already Done, Judge waiting for exactly this pass.
        var phases = await runs.EnsurePhasesAsync(
            cellId, PhasePlan.Materialise(cellId, TaskKind.Fix, FixArm.InvestigateOnly).Ok(), Ct);
        var started = PhasePlan.Start(phases[0], phases).Ok();
        (await runs.SavePhasesAsync([started], Ct)).Ok();
        var (ended, _) = PhasePlan.End(started, phases, LegOutcomeKind.Completed, "investigated");
        (await runs.SavePhasesAsync([ended], Ct)).Ok();

        var report = (await RunnerFor(results).JudgeRunAsync(runId, suite, Judge("opus", yes: true), Ct)).Ok();

        report.Judged.Should().Be(1);
        (await runs.PhasesAsync(cellId, Ct)).Single(p => p.Kind == PhaseKind.Judge).State.Should().Be(
            PhaseState.Done, "the Judge phase waits for exactly this pass, and nothing else ever closes it");
    }

    private JudgeRunner RunnerFor(PostgresResultStore results) =>
        new(results, RunStore(), NullLogger<JudgeRunner>.Instance);

    private PostgresRunStore RunStore() => new(postgres.NewContext(), new TestClock(Noon));

    private async Task<Guid> CellAsync(Guid runId)
    {
        await using var db = postgres.NewContext();
        return db.Cells.Where(c => c.RunId == runId).Select(c => c.Id).Single();
    }

    private static IJudge Judge(string id, bool yes) => new FakeJudge(id, yes);

    private async Task<(Guid RunId, Suite Suite, PostgresResultStore Results)> ArrangeAsync(string answer)
    {
        var clock = new TestClock(Noon);
        var suite = SuiteOf("polly-smoke", "retry-jitter-formula",
            "RetryHelper.DecorrelatedJitterBackoffV2 carries state between attempts");
        var target = MeasurementTarget.At(RepoUrl.Parse("https://github.com/App-vNext/Polly.git").Ok(), Commit);
        var run = BenchRun.Planned("judged", target, EngineRef.Filesystem(), suite.Stamp, Noon);

        var cells = Matrix.Plan(
            suite.Questions, repeats: 1,
            [new Subject(ModelRef.Parse("qwen3-coder:latest", ModelHosting.Local).Ok(), Sampling.Deterministic(7))],
            [Lane.Named("no-tools")]).Ok()
            .Select(c => RunCell.Pending(run.Id, c)).ToList();

        var runs = new PostgresRunStore(postgres.NewContext(), clock);
        var results = postgres.NewResults();
        await runs.CreateAsync(run, cells, Ct);

        await results.SaveAsync(LegResult.Of(cells[0].Id, suite.Questions[0].Prompt, answer,
            [StoredMetric.Boolean("Answer contains 'DecorrelatedJitter'", true, "present", false, "Good"),
             StoredMetric.Text(AnswerScoring.AnchorRecall, "not applicable", "no tools", false, "Unknown")], Noon), Ct);

        return (run.Id, suite, results);
    }

    private static Suite SuiteOf(string suiteId, string questionId, string reference) =>
        Suite.Draft(suiteId).With(new Question(
            questionId,
            "How does Polly compute the delay for an exponential retry with jitter?",
            [new Expectation(ExpectationKind.AnswerContains, SourceAnchor.File("", Commit), "DecorrelatedJitter", true)],
            reference)).Ok().Freeze().Ok();

    private sealed class FakeJudge(string id, bool yes, string failure = "") : IJudge
    {
        public static IJudge Unreachable(string id, string why) => new FakeJudge(id, false, why);

        public ModelRef Model => ModelRef.Parse(id, ModelHosting.Cloud).Ok();

        public Task<Outcome<JudgeVerdict>> JudgeAsync(
            string question, string answer, string reference, CancellationToken cancellationToken) =>
            Task.FromResult(failure.Length > 0
                ? Outcome<JudgeVerdict>.Failure(failure)
                : Outcome<JudgeVerdict>.Success(new JudgeVerdict(yes, yes ? "states the same mechanism" : "names the wrong helper")));
    }
}
