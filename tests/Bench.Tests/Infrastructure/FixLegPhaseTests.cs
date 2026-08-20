using Bench.Application;
using Bench.Domain;
using Bench.Domain.Models;
using Bench.Domain.Retrieval;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Domain.Trace;
using Bench.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>Fix legs run PHASES (todo/PLAN_investigate_vs_implement.md step 4): the investigate-only
/// arm records Investigate → Judge around the ordinary ask, and the arms that produce a diff are
/// refused BY NAME before any phase row exists — the sandbox does not exist, and a cell whose arm the
/// runner cannot honour must not look started.</summary>
[Collection("postgres")]
public sealed class FixLegPhaseTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private static readonly CommitSha Commit = CommitSha.Parse(new string('a', 40)).Ok();

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_investigate_only_leg_runs_and_records_its_phases()
    {
        var (runId, plan, runner, runs) = await ArrangeAsync(FixArm.InvestigateOnly);

        var result = await runner.RunNextAsync(runId, WorkerIdentity.Here("phase-test"), plan, Ct);

        result.Failed().Should().BeFalse("the investigate arm runs the ordinary single ask");

        var cell = await ClaimedCellAsync(runs, runId);
        var phases = await runs.PhasesAsync(cell, Ct);

        phases.Should().HaveCount(2, "investigate-only is Investigate then Judge");
        phases[0].Kind.Should().Be(PhaseKind.Investigate);
        phases[0].State.Should().Be(PhaseState.Done);
        phases[0].Outcome.Should().Be(LegOutcomeKind.Completed);
        phases[1].Kind.Should().Be(PhaseKind.Judge);
        phases[1].State.Should().Be(PhaseState.Pending, "the judge pass closes it later, never this runner");
    }

    [Fact]
    public async Task A_full_arm_is_blocked_by_name_before_any_model_is_asked()
    {
        var runtime = new CountingRuntime();
        var (runId, plan, runner, runs) = await ArrangeAsync(FixArm.Full, runtime);

        var result = await runner.RunNextAsync(runId, WorkerIdentity.Here("phase-test"), plan, Ct);

        result.Reason().Should().Contain("blocked").And.Contain("investigate-only");
        runtime.Asked.Should().Be(0, "a leg whose arm cannot be honoured must not cost a completion");

        var cell = await ClaimedCellAsync(runs, runId);
        (await runs.CellAsync(cell, Ct)).Ok().OutcomeKind.Should().Be(LegOutcomeKind.Blocked);
        (await runs.PhasesAsync(cell, Ct)).Should().BeEmpty("a refused arm must not look started");
    }

    [Fact]
    public async Task An_investigate_leg_carries_the_contract_and_a_valid_diagnosis_scores()
    {
        var runtime = new CountingRuntime(
            """
            I read the retry loop first.

            ```json
            { "anchors": [{ "path": "src/Policy.cs", "member": "Policy.NextDelay" }],
              "mechanism": "the delay is recomputed from scratch on every attempt" }
            ```
            """);
        var (runId, plan, runner, _) = await ArrangeAsync(FixArm.InvestigateOnly, runtime);

        (await runner.RunNextAsync(runId, WorkerIdentity.Here("phase-test"), plan, Ct))
            .Failed().Should().BeFalse();

        runtime.SeenPrompt.Should().StartWith("Retries stop honouring the cap.")
            .And.Contain("```json").And.Contain("CAUSED",
                "the fix leg's prompt is the statement plus the diagnosis contract");

        var stored = (await postgres.NewResults().ForRunAsync(runId, Ct)).Should().ContainSingle().Subject;
        Metric(stored, DiagnosisScoring.Parses).Should().Be("true");
        Metric(stored, DiagnosisScoring.AnchorRecall).Should().Be(
            "1", "the diagnosis named the question's own causal anchor");
        Metric(stored, DiagnosisScoring.Precision).Should().Be("1");
    }

    [Fact]
    public async Task A_prose_answer_scores_no_anchor_numbers_only_the_broken_contract()
    {
        var runtime = new CountingRuntime("It is somewhere in the retry logic, probably.");
        var (runId, plan, runner, _) = await ArrangeAsync(FixArm.InvestigateOnly, runtime);

        await runner.RunNextAsync(runId, WorkerIdentity.Here("phase-test"), plan, Ct);

        var stored = (await postgres.NewResults().ForRunAsync(runId, Ct)).Should().ContainSingle().Subject;
        Metric(stored, DiagnosisScoring.Parses).Should().Be("false");
        stored.Metrics.Should().NotContain(m => m.Name == DiagnosisScoring.AnchorRecall,
            "malformed and wrong are different facts, and zeros would merge them");
    }

    [Fact]
    public async Task Phases_materialise_once_and_a_second_ensure_returns_the_stored_record()
    {
        var (runId, _, _, runs) = await ArrangeAsync(FixArm.InvestigateOnly);
        var cellId = await ClaimedCellAsync(runs, runId);

        var fresh = PhasePlan.Materialise(cellId, TaskKind.Fix, FixArm.InvestigateOnly).Ok();
        var first = await runs.EnsurePhasesAsync(cellId, fresh, Ct);
        var second = await runs.EnsurePhasesAsync(
            cellId, PhasePlan.Materialise(cellId, TaskKind.Fix, FixArm.InvestigateOnly).Ok(), Ct);

        second.Select(p => p.Id).Should().Equal(first.Select(p => p.Id),
            "a swept-back leg must resume the record its crash left, not mint a second one");
    }

    [Fact]
    public async Task A_transition_on_a_phase_nobody_materialised_is_refused()
    {
        var (runId, _, _, runs) = await ArrangeAsync(FixArm.InvestigateOnly);
        var cellId = await ClaimedCellAsync(runs, runId);

        var ghost = LegPhase.Pending(cellId, PhaseKind.Investigate, 0);

        (await runs.SavePhasesAsync([ghost with { State = PhaseState.Running }], Ct))
            .Reason().Should().Contain("never materialised");
    }

    private async Task<(Guid RunId, LegPlan Plan, LegRunner Runner, PostgresRunStore Runs)> ArrangeAsync(
        FixArm arm, IModelRuntime? runtime = null)
    {
        var suite = Suite.Draft($"fix-{Guid.NewGuid():N}"[..20]).With(new Question(
            "fix-q1",
            "Retries stop honouring the cap. Investigate.",
            [Expectation.Member(SourceAnchor.Member("src/Policy.cs", "Policy.NextDelay", new LineSpan(10, 20), Commit))],
            string.Empty)).Ok().Freeze().Ok();

        var target = MeasurementTarget.At(RepoUrl.Parse("https://example.invalid/x.git").Ok(), Commit);
        var run = BenchRun.Planned("fix-phases", target, EngineRef.Filesystem(), suite.Stamp, Noon);

        var roster = SubjectRoster.Of([new RosterEntry(
            ModelEndpoint.Parse(ModelRef.Parse("qwen3-coder:latest", ModelHosting.Local).Ok(), "http://127.0.0.1:11434/v1").Ok(),
            Sampling.Deterministic(7))]);

        var cells = Matrix.Plan(
                suite.Questions, repeats: 1, roster.Subjects, [Lane.Named("no-tools")],
                [Bench.Domain.Variants.VariantSelection.None], [arm]).Ok()
            .Select(c => RunCell.Pending(run.Id, c)).ToList();

        var clock = new TestClock(Noon);
        var runs = new PostgresRunStore(postgres.NewContext(), clock);
        await runs.CreateAsync(run, cells, Ct);

        var model = runtime ?? new CountingRuntime();
        var runner = new LegRunner(
            runs, postgres.NewResults(), model, new NoRetriever(), new NoHardwareSampler(),
            new ToolLoopRunner(model, NullLogger<ToolLoopRunner>.Instance), clock,
            NullLogger<LegRunner>.Instance);

        return (run.Id, new LegPlan(suite, roster, [], TaskKind.Fix), runner, runs);
    }

    private static string Metric(LegResult stored, string name) =>
        stored.Metrics.Single(m => m.Name == name).Value;

    private async Task<Guid> ClaimedCellAsync(PostgresRunStore runs, Guid runId)
    {
        await using var db = postgres.NewContext();
        return db.Cells.Where(c => c.RunId == runId).Select(c => c.Id).Single();
    }

    /// <summary>Answers with whatever the test scripted, counts how often it was reached — the
    /// blocked-arm assertion is about the ask that must never happen — and keeps the prompt it saw,
    /// because the diagnosis contract can only be observed on the request itself.</summary>
    private sealed class CountingRuntime(string answer = "the delay is recomputed without carrying state")
        : IModelRuntime
    {
        public int Asked { get; private set; }

        public string SeenPrompt { get; private set; } = string.Empty;

        public ModelHosting Hosting => ModelHosting.Local;

        public Task<Outcome<string>> AcceptBudgetAsync(Budget budget, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<string>.Success("fake"));

        public Task<Outcome<ModelAnswer>> AskAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            Asked++;
            SeenPrompt = request.UserPrompt;

            return Task.FromResult(Outcome<ModelAnswer>.Success(new ModelAnswer(
                Captured.Text(answer),
                CapturedCount.Number(100),
                CapturedCount.Number(20),
                TimeSpan.FromMilliseconds(250),
                SamplingAsSent.From(request.Sampling, "request-body"),
                StopReason.Completed,
                "stop")));
        }
    }
}
