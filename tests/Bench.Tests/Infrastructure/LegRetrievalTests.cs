using Bench.Application;
using Bench.Domain;
using Bench.Domain.Models;
using Bench.Domain.Retrieval;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Domain.Trace;
using Bench.Domain.Variants;
using Bench.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The retrieval lane assembled: claim a cell, retrieve for its variant, build the prompt from the
/// hits, ask, score against the anchors, store the evidence, settle.
/// <para>
/// Against the real stores, because every piece of this chain passes its own tests in isolation and the whole
/// point of a leg runner is that they are driven in order. What this class adds over
/// <see cref="LegRunnerTests"/> is the second axis: a cell's VARIANT decides what retrieval it gets, exactly
/// as its subject decides where it is sent.
/// </para></summary>
[Collection("postgres")]
public sealed class LegRetrievalTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly CommitSha Commit = CommitSha.Parse(new string('d', 40)).Ok();

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_retrieval_leg_puts_the_hits_in_the_prompt_and_stores_what_served_it()
    {
        var runtime = new EchoRuntime();
        var (runId, plan, runner, _) = await ArrangeAsync(runtime, Surfacing());

        var result = (await runner.RunNextAsync(runId, Worker(), plan, Ct)).Ok();

        // The prompt IS the artefact: whatever it says, the model read.
        result.Prompt.Should().Contain("src/Retry/RetryHelper.cs:70-120");
        result.Prompt.Should().Contain("internal static TimeSpan Backoff");
        result.Prompt.Should().Contain("How does Polly compute");
        runtime.Prompt.Should().Be(result.Prompt, "a stored prompt that is not the one sent is not evidence");

        result.Retrieval.WasPerformed.Should().BeTrue();
        result.Retrieval.Hits.Should().ContainSingle();
        (await Stored(runId)).Retrieval.Funnel.ContractVersion.Should().Be(TraceContract.V0);
    }

    [Fact]
    public async Task Anchor_recall_becomes_a_REAL_number_the_moment_a_lane_surfaces_something()
    {
        var (runId, plan, runner, _) = await ArrangeAsync(new EchoRuntime(), Surfacing());

        var result = (await runner.RunNextAsync(runId, Worker(), plan, Ct)).Ok();

        // Before this lane every leg reported "not applicable" — honestly, because nothing surfaced anything.
        var recall = result.Metrics.Single(m => m.Name == AnswerScoring.AnchorRecall);
        recall.Kind.Should().Be(MetricKind.Numeric);
        recall.AsNumber().Ok().Should().Be(1.0);

        result.Metrics.Single(m => m.Name == RetrievalScoring.FirstHitRank).AsNumber().Ok().Should().Be(1);
        result.Metrics.Single(m => m.Name == RetrievalScoring.RecallAt(5)).AsNumber().Ok().Should().Be(1.0);
    }

    [Fact]
    public async Task A_retrieval_that_missed_the_anchor_scores_zero_recall_rather_than_not_applicable()
    {
        var (runId, plan, runner, _) = await ArrangeAsync(new EchoRuntime(), Missing());

        var result = (await runner.RunNextAsync(runId, Worker(), plan, Ct)).Ok();

        var recall = result.Metrics.Single(m => m.Name == AnswerScoring.AnchorRecall);
        recall.AsNumber().Ok().Should().Be(0.0);
        recall.Failed.Should().BeTrue("a lane that CAN surface the anchor and did not is a miss, which is the measurement");
    }

    [Fact]
    public async Task The_control_arm_keeps_exactly_the_metrics_it_had_before_this_lane_existed()
    {
        // A run planned WITHOUT the variant axis — which is what every run planned before this lane existed
        // is. Reusing a variant run's cells and simply emptying the roster is a different scenario, and the
        // roster rightly refuses it: a cell that names a variant has nowhere honest to go without its recipe.
        var (runId, plan, runner, _) = await ArrangeAsync(new EchoRuntime(), Surfacing(), withVariant: false);

        var result = (await runner.RunNextAsync(runId, Worker(), plan, Ct)).Ok();

        result.Retrieval.WasPerformed.Should().BeFalse();
        result.Prompt.Should().NotContain("Retrieved context", "a control that acquired a preamble is not a control");
        result.Metrics.Single(m => m.Name == AnswerScoring.AnchorRecall).Value.Should().Be("not applicable");
        result.Metrics.Should().NotContain(m => m.Name == RetrievalScoring.Mrr);
    }

    [Fact]
    public async Task An_engine_that_could_not_retrieve_settles_the_cell_and_stores_nothing()
    {
        var (runId, plan, runner, runs) = await ArrangeAsync(
            new EchoRuntime(), new FakeRetriever("unreachable: the engine actively refused it"));

        var refused = await runner.RunNextAsync(runId, Worker(), plan, Ct);

        // An engine that is down is an environment fact this cell records, not a subject that answered badly.
        refused.Reason().Should().Contain("actively refused");
        (await postgres.NewResults().ForRunAsync(runId, Ct)).Should().BeEmpty();
        (await runs.ProgressAsync(runId, Ct)).Settled.Should().Be(1);
    }

    [Fact]
    public async Task A_cell_whose_variant_this_run_has_no_recipe_for_is_settled_rather_than_measured_under_another()
    {
        var (runId, plan, runner, runs) = await ArrangeAsync(new EchoRuntime(), Surfacing());

        // The run planned `hybrid-rrf` cells and the plan holds a DIFFERENT variant: there is nowhere honest
        // to send this leg. Measuring it under the other recipe would label the result with this one.
        var mismatched = plan with { Variants = VariantRoster.Of([Other()]) };

        var refused = await runner.RunNextAsync(runId, Worker(), mismatched, Ct);

        refused.Reason().Should().Contain("no recipe for variant").And.Contain("hybrid-rrf");
        (await runs.ProgressAsync(runId, Ct)).Settled.Should().Be(1);
    }

    [Fact]
    public async Task A_variant_this_engine_cannot_express_is_refused_by_the_leg_rather_than_measured_wrongly()
    {
        var (runId, plan, runner, _) = await ArrangeAsync(
            new EchoRuntime(),
            new FakeRetriever(
                "this engine has no corpus text shape called 'member' — it serves SourceOnly, GraphHeader"));

        var refused = await runner.RunNextAsync(runId, Worker(), plan, Ct);

        refused.Reason().Should().Contain("no corpus text shape");
        (await postgres.NewResults().ForRunAsync(runId, Ct)).Should().BeEmpty(
            "a cell measured under a recipe the engine ignored would be a permanently mislabelled number");
    }

    [Fact]
    public async Task A_retrieval_that_outlives_the_legs_wall_is_a_CAP_rather_than_a_crash()
    {
        var clock = new TestClock(Noon);
        var (runId, plan, runner, runs) = await ArrangeAsync(
            new EchoRuntime(), SlowRetriever.Taking(clock, TimeSpan.FromSeconds(90)), withVariant: true, clock);

        await runner.RunNextAsync(runId, Worker(), plan with { Budgets = Wall(60) }, Ct);

        // A retrieval is a wait INSIDE a leg, so it shares the leg's ceiling. Settled as a crash it would
        // report this harness as broken over a merely slow index; asked anyway it would spend a completion
        // under no budget at all and score whatever came back.
        var cell = await CellAsync(runId);
        cell.OutcomeKind.Should().Be(LegOutcomeKind.CapExceeded);
        cell.OutcomeDetail.Should().Contain("Wall").And.Contain("60");
        (await runs.ProgressAsync(runId, Ct)).Settled.Should().Be(1);
    }

    [Fact]
    public async Task A_retrieval_call_is_handed_the_legs_remaining_wall_rather_than_a_transport_default()
    {
        var hanging = new HangingRetriever();
        var (runId, plan, runner, _) = await ArrangeAsync(
            new EchoRuntime(), hanging, withVariant: true, new TestClock(DateTimeOffset.UtcNow));

        // A one-second ceiling against a retriever that never answers. Real time, deliberately: this is the
        // guarantee a fake clock cannot observe — the token the engine is handed has to carry the leg's
        // remainder, or a hung engine adds its own timeout on top of the model's.
        var capped = await runner.RunNextAsync(runId, Worker(), plan with { Budgets = Wall(1) }, Ct);

        hanging.WasCancelled.Should().BeTrue("the retriever must be given a token that fires");
        capped.Reason().Should().Contain("did not answer inside");
    }

    [Fact]
    public async Task Slow_retrieval_eats_into_the_models_time_rather_than_extending_the_leg()
    {
        var clock = new TestClock(Noon);
        var runtime = new EchoRuntime();
        var (runId, plan, runner, _) = await ArrangeAsync(
            runtime, SlowRetriever.Taking(clock, TimeSpan.FromSeconds(20), Hit("src/Retry/RetryHelper.cs", 70, 120)),
            withVariant: true, clock);

        await runner.RunNextAsync(runId, Worker(), plan with { Budgets = Wall(60) }, Ct);

        runtime.WallSeen.Should().BeLessThanOrEqualTo(
            40m, "the model gets the REMAINDER — twenty seconds of retrieval is twenty seconds off the leg");
        runtime.WallSeen.Should().BeGreaterThan(0m, "and it must still be given a ceiling at all");
    }

    private static IReadOnlyList<Budget> Wall(int seconds) =>
        [Budget.Of(BudgetKind.Wall, BudgetScope.Question, seconds).ConfirmedBy("fake")];

    private async Task<CellRow> CellAsync(Guid runId)
    {
        await using var db = postgres.NewContext();
        return await Task.FromResult(db.Cells.First(c => c.RunId == runId && c.State == CellState.Settled));
    }

    [Fact]
    public async Task The_reasoning_a_runtime_returns_is_stored_beside_the_answer()
    {
        var (runId, plan, runner, _) = await ArrangeAsync(
            new EchoRuntime { Thinking = "first I look at the backoff helper" }, Surfacing());

        await runner.RunNextAsync(runId, Worker(), plan, Ct);

        var stored = await Stored(runId);
        stored.Thinking.WasCaptured.Should().BeTrue();
        stored.Thinking.Value.Should().Contain("backoff helper");
        stored.Meta.Sampling.Seed.Should().Be(7, "the sampling AS SENT is the only evidence it was applied");
    }

    private async Task<LegResult> Stored(Guid runId) =>
        (await postgres.NewResults().ForRunAsync(runId, Ct)).Single();

    private static WorkerIdentity Worker() => WorkerIdentity.Here("retrieval-worker");

    private static VariantChoice Choice(RetrievalVariant variant) =>
        new(variant.Select(), variant.Definition);

    private static VariantChoice Other() => Choice(Variant("sparse-only", RetrievalChannels.Sparse));

    /// <summary>A catalog variant. Its NAME is unique per test because the catalog holds one row per name and
    /// this fixture's database is shared with the rest of the suite.</summary>
    private static RetrievalVariant Variant(string name, RetrievalChannels channels) =>
        RetrievalVariant.Create(
            $"{name}-{Guid.NewGuid():N}"[..24],
            name,
            VariantDefinition.Retrieval(
                EngineKind.Qln,
                channels,
                FusionSpec.Rrf(60).Ok(),
                CorpusSpec.Parse("GraphHeader", 512, "bge-m3").Ok(),
                RerankSpec.Pooled(50).Ok(),
                20).Ok(),
            Noon).Ok();

    /// <summary>A retriever whose one hit COVERS the suite's anchor.</summary>
    private static FakeRetriever Surfacing() => new(Hit("src/Retry/RetryHelper.cs", 70, 120));

    /// <summary>A retriever that answers, in the wrong file. The engine worked; the recall did not.</summary>
    private static FakeRetriever Missing() => new(Hit("src/Timeout/TimeoutHelper.cs", 10, 40));

    private static RetrievedHit Hit(string path, int start, int end) => new(
        1, path, start, end, "RetryHelper.Backoff", "csharp|Polly.Retry|RetryHelper|Backoff`0|(int)",
        "internal static TimeSpan Backoff(int attempt)", 0.91, "rerank", ["dense", "sparse"], [1, 4],
        HitSnippet.Text("internal static TimeSpan Backoff(int attempt) => ..."));

    private Task<(Guid RunId, LegPlan Plan, LegRunner Runner, PostgresRunStore Runs)> ArrangeAsync(
        IModelRuntime runtime, IRetriever retriever) => ArrangeAsync(runtime, retriever, withVariant: true);

    private Task<(Guid RunId, LegPlan Plan, LegRunner Runner, PostgresRunStore Runs)> ArrangeAsync(
        IModelRuntime runtime, IRetriever retriever, bool withVariant) =>
        ArrangeAsync(runtime, retriever, withVariant, new TestClock(Noon));

    private async Task<(Guid RunId, LegPlan Plan, LegRunner Runner, PostgresRunStore Runs)> ArrangeAsync(
        IModelRuntime runtime, IRetriever retriever, bool withVariant, TestClock clock)
    {
        var suite = SuiteOf();
        var roster = SubjectRoster.Of(
            ModelEndpoint.Parse(ModelRef.Parse("qwen3-coder:latest", ModelHosting.Local).Ok(), "http://127.0.0.1:11434/v1").Ok(),
            Sampling.Deterministic(7));

        var variants = VariantRoster.Baseline;

        if (withVariant)
        {
            // The catalog row goes in FIRST: a cell carries a foreign key to the variant it ran under, which
            // is what stops a measurement from naming a configuration nobody can look up.
            var hybrid = Variant("hybrid-rrf", RetrievalChannels.Hybrid);
            (await new PostgresVariantCatalog(postgres.NewContext()).AddAsync(hybrid, Ct)).Failed().Should().BeFalse();
            variants = VariantRoster.Of([Choice(hybrid)]);
        }

        var target = MeasurementTarget.At(RepoUrl.Parse("https://github.com/App-vNext/Polly.git").Ok(), Commit);
        var run = BenchRun.Planned(
            "retrieval", target, new EngineRef(EngineKind.Qln, "http://127.0.0.1:5311", string.Empty, string.Empty),
            suite.Stamp, Noon);

        var planned = variants.Entries.Count == 0 ? [VariantSelection.None] : variants.Selections;
        var cells = Matrix.Plan(suite.Questions, repeats: 2, roster.Subjects, [Lane.Named("no-tools")], planned)
            .Ok().Select(c => RunCell.Pending(run.Id, c)).ToList();

        var runs = postgres.NewStore(clock);
        await runs.CreateAsync(run, cells, Ct);

        var plan = LegPlan.Reading(suite, roster) with { Variants = variants };
        var runner = new LegRunner(
            runs, postgres.NewResults(), runtime, retriever, clock, NullLogger<LegRunner>.Instance);

        return (run.Id, plan, runner, runs);
    }

    private static Suite SuiteOf() =>
        Suite.Draft("polly-retrieval").With(new Question(
            "retry-jitter-formula",
            "How does Polly compute the delay for an exponential retry with jitter?",
            [
                Expectation.Member(SourceAnchor.Member(
                    "src/Retry/RetryHelper.cs", "RetryHelper.DecorrelatedJitterBackoffV2", new LineSpan(75, 111), Commit)),
                new Expectation(ExpectationKind.AnswerContains, SourceAnchor.File("", Commit), "Backoff", true),
            ],
            string.Empty)).Ok().Freeze().Ok();

    /// <summary>A retriever that answers with a fixed list, or refuses with a fixed reason.</summary>
    private sealed class FakeRetriever : IRetriever
    {
        private readonly RetrievedHit[] _hits;
        private readonly string _refusal;

        public FakeRetriever(params RetrievedHit[] hits)
        {
            _hits = hits;
            _refusal = string.Empty;
        }

        public FakeRetriever(string refusal)
        {
            _hits = [];
            _refusal = refusal;
        }

        public EngineRef Describe => new(EngineKind.Qln, "http://fake", string.Empty, string.Empty);

        public Outcome<string> CanServe(VariantDefinition.RetrievalRecipe recipe) =>
            _refusal.Length > 0 ? Outcome<string>.Failure(_refusal) : Outcome<string>.Success(recipe.Canonical);

        /// <summary>Never called: the leg runner does not inspect an index — the CLI does, once per variant,
        /// while planning. A refusal rather than a fabricated state, so a caller that appears later fails
        /// loudly instead of measuring against a corpus this fake invented.</summary>
        public Task<Outcome<IndexState>> InspectAsync(
            VariantDefinition.RetrievalRecipe recipe, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<IndexState>.Failure("this fake serves the leg runner, which never inspects an index"));

        public Task<Outcome<RetrievedContext>> RetrieveAsync(
            RetrievalRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(_refusal.Length > 0
                ? Outcome<RetrievedContext>.Failure(_refusal)
                : Outcome<RetrievedContext>.Success(RetrievedContext.Of(
                    "code_ab12",
                    _hits,
                    new RetrievalFunnel(TraceContract.V0, [new FunnelStage("rerank", 50, 20, 900)], 1200, []),
                    string.Empty,
                    new EngineAxes([new Axis("limit", request.Recipe.Limit.ToString())]),
                    new EngineAxes([new Axis("limit", request.Recipe.Limit.ToString())]),
                    2048,
                    1300)));
    }

    /// <summary>A retriever that BURNS TIME. A wall budget is a statement about elapsed time, so the only
    /// fake that can test it is one that moves the clock the runner reads — a fake that answers instantly
    /// proves the ceiling was configured, never that it was enforced.</summary>
    private sealed class SlowRetriever(TestClock clock, TimeSpan takes, RetrievedHit[] hits) : IRetriever
    {
        public static SlowRetriever Taking(TestClock clock, TimeSpan takes, params RetrievedHit[] hits) =>
            new(clock, takes, hits);

        public EngineRef Describe => new(EngineKind.Qln, "http://slow", string.Empty, string.Empty);

        public Outcome<string> CanServe(VariantDefinition.RetrievalRecipe recipe) =>
            Outcome<string>.Success(recipe.Canonical);

        /// <summary>Never called: the leg runner does not inspect an index — the CLI does, once per variant,
        /// while planning. A refusal rather than a fabricated state, so a caller that appears later fails
        /// loudly instead of measuring against a corpus this fake invented.</summary>
        public Task<Outcome<IndexState>> InspectAsync(
            VariantDefinition.RetrievalRecipe recipe, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<IndexState>.Failure("this fake serves the leg runner, which never inspects an index"));

        public async Task<Outcome<RetrievedContext>> RetrieveAsync(
            RetrievalRequest request, CancellationToken cancellationToken)
        {
            clock.Now += takes;

            // The token the runner narrowed to the leg's remainder. A real engine would notice it mid-flight;
            // this one checks once, which is the same observation without the wall clock.
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            return Outcome<RetrievedContext>.Success(RetrievedContext.Of(
                "code_ab12",
                hits,
                new RetrievalFunnel(TraceContract.V0, [new FunnelStage("rerank", 50, 20, 900)], 1200, []),
                string.Empty,
                EngineAxes.None,
                EngineAxes.None,
                2048,
                (long)takes.TotalMilliseconds));
        }
    }

    /// <summary>An engine that never answers. The only fake that can prove the token it was handed actually
    /// fires — a fake clock moves the runner's own view of time but not a real cancellation.</summary>
    private sealed class HangingRetriever : IRetriever
    {
        public bool WasCancelled { get; private set; }

        public EngineRef Describe => new(EngineKind.Qln, "http://hangs", string.Empty, string.Empty);

        public Outcome<string> CanServe(VariantDefinition.RetrievalRecipe recipe) =>
            Outcome<string>.Success(recipe.Canonical);

        /// <summary>Never called: the leg runner does not inspect an index — the CLI does, once per variant,
        /// while planning. A refusal rather than a fabricated state, so a caller that appears later fails
        /// loudly instead of measuring against a corpus this fake invented.</summary>
        public Task<Outcome<IndexState>> InspectAsync(
            VariantDefinition.RetrievalRecipe recipe, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<IndexState>.Failure("this fake serves the leg runner, which never inspects an index"));

        public async Task<Outcome<RetrievedContext>> RetrieveAsync(
            RetrievalRequest request, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                WasCancelled = true;
                throw;
            }

            return Outcome<RetrievedContext>.Success(RetrievedContext.NotPerformed);
        }
    }

    /// <summary>A runtime that answers with the term the suite requires and REMEMBERS what it was asked, since
    /// the prompt is the thing under test here.</summary>
    private sealed class EchoRuntime : IModelRuntime
    {
        public string Prompt { get; private set; } = string.Empty;

        /// <summary>The wall ceiling this runtime was handed, in seconds. What proves the model got the leg's
        /// REMAINDER rather than a fresh budget of its own.</summary>
        public decimal WallSeen { get; private set; }

        public string Thinking { get; init; } = string.Empty;

        public ModelHosting Hosting => ModelHosting.Local;

        public Task<Outcome<string>> AcceptBudgetAsync(Budget budget, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<string>.Success("fake"));

        public Task<Outcome<ModelAnswer>> AskAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            Prompt = request.UserPrompt;
            WallSeen = request.Budgets.FirstOrDefault(b => b.Kind == BudgetKind.Wall)?.Limit ?? 0m;

            return Task.FromResult(Outcome<ModelAnswer>.Success(new ModelAnswer(
                Captured.Text("the delay uses a Backoff helper"),
                CapturedCount.Number(1500),
                CapturedCount.Number(60),
                TimeSpan.FromSeconds(2),
                SamplingAsSent.From(request.Sampling, "request-body"),
                StopReason.Completed,
                "stop")
            {
                Thinking = Thinking.Length > 0
                    ? Captured.Text(Thinking)
                    : Captured.Unavailable("the response carried no reasoning field"),
                ResponseBytes = 2048,
            }));
        }
    }
}
