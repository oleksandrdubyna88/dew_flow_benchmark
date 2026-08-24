using Bench.Application;
using Bench.Application.Delivered;
using Bench.Delivered;
using Bench.Domain;
using Bench.Domain.Models;
using Bench.Domain.Runs;
using Bench.Domain.Trace;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Delivered;

/// <summary>The one orchestration: decompose, gate, weigh, correct.
/// <para>
/// The runtime is scripted rather than faked loosely, because what these tests are about is the SEQUENCE —
/// how many times a model is asked, what is stored before each answer is read, and which refusals end the
/// stage rather than spending another attempt. A stub that answered anything to anything could not observe
/// any of that.
/// </para></summary>
public sealed class DeliveredWorkStageTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    private static readonly string Diff = string.Join('\n', [
        "diff --git a/src/Policy.cs b/src/Policy.cs",
        "--- a/src/Policy.cs",
        "+++ b/src/Policy.cs",
        .. Enumerable.Range(1, 40).Select(i => $"+    var line{i} = Compute({i});"),
    ]);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_clean_run_decomposes_weighs_and_corrects_in_TWO_asks()
    {
        var runtime = new ScriptedRuntime([Decomposition(30), Scores(30, 3)]);
        var payloads = new InMemoryPayloads();

        var scored = await Stage(runtime, payloads).ScoreAsync(Request(), Ct);

        // Two asks, not three: the gate only costs a call when it refuses.
        runtime.Asks.Should().Be(2);
        var score = scored.Ok();
        score.Policy.Total.Should().Be(90);
        score.Coverage.Status.Should().Be(CoverageStatus.Passed);
        score.Protocol.Should().Be(WeightingProtocol.Protocol);
    }

    [Fact]
    public async Task The_FIGURES_survive_a_run_whose_model_half_refuses()
    {
        var runtime = new ScriptedRuntime([]);

        var scored = await Stage(runtime, new InMemoryPayloads()).ScoreAsync(Request(), Ct);

        // The figures cost no call to produce, so a refusal that answered nothing at all would be
        // discarding evidence. The stage still fails — but the diff's own numbers were computed first.
        scored.Failed().Should().BeTrue();
        LocCalculator.Compute(Diff).Cleaned.Should().Be(40);
    }

    [Fact]
    public async Task A_THIN_decomposition_is_re_asked_exactly_once()
    {
        // 4 steps over 40 cleaned lines is 10 % — far under the band. The re-ask brings 30.
        var runtime = new ScriptedRuntime([Decomposition(4), Decomposition(30), Scores(30, 2)]);
        var payloads = new InMemoryPayloads();

        var scored = await Stage(runtime, payloads).ScoreAsync(Request(), Ct);

        scored.Ok().Policy.Total.Should().Be(60);
        runtime.Asks.Should().Be(3, "one re-ask, then the weighing");
        payloads.Stored.Count(p => p.Stage == DeliveredStage.Decompose).Should().Be(2);
    }

    [Fact]
    public async Task A_re_ask_that_does_not_help_FAILS_rather_than_asking_a_third_time()
    {
        var runtime = new ScriptedRuntime([Decomposition(4), Decomposition(5)]);

        var scored = await Stage(runtime, new InMemoryPayloads()).ScoreAsync(Request(), Ct);

        // The budget is one re-ask. A third would be a different instrument, and an unbounded one would
        // let a bad decomposition cost whatever it liked.
        scored.Failed().Should().BeTrue();
        runtime.Asks.Should().Be(2);
    }

    [Fact]
    public async Task The_re_ask_NAMES_the_shortfall_in_the_numbers_it_was_judged_by()
    {
        var runtime = new ScriptedRuntime([Decomposition(4), Decomposition(30), Scores(30, 1)]);

        await Stage(runtime, new InMemoryPayloads()).ScoreAsync(Request(), Ct);

        // "Do better" produces a differently-worded version of the same answer. The figures are what makes
        // it actionable — and the instruction not to pad is there because padding is the cheap way to
        // satisfy a coverage number.
        runtime.Prompts[1].Should().Contain("accounts for").And.Contain("Do not split existing steps");
    }

    [Fact]
    public async Task An_UNREADABLE_reply_spends_an_attempt_rather_than_retrying_for_free()
    {
        var runtime = new ScriptedRuntime(["I cannot do that.", "still not JSON"]);

        var scored = await Stage(runtime, new InMemoryPayloads()).ScoreAsync(Request(), Ct);

        // The budget is about how many times a model is ASKED, not about why an answer failed. Otherwise a
        // model that never produces JSON would be asked forever.
        scored.Failed().Should().BeTrue();
        runtime.Asks.Should().Be(2);
    }

    [Fact]
    public async Task An_unreadable_reply_is_STORED_because_it_is_the_evidence()
    {
        var payloads = new InMemoryPayloads();

        await Stage(new ScriptedRuntime(["I cannot do that.", "nor this"]), payloads).ScoreAsync(Request(), Ct);

        // Exactly what somebody wants when asking why a leg scored nothing. A stage that stored only the
        // replies it could parse would throw that away.
        payloads.Stored.Should().HaveCount(2);
        payloads.Stored[0].PayloadJson.Should().Be("I cannot do that.");
    }

    [Fact]
    public async Task Every_payload_carries_the_PROTOCOL_and_the_prompt_hash()
    {
        var payloads = new InMemoryPayloads();

        await Stage(new ScriptedRuntime([Decomposition(30), Scores(30, 3)]), payloads).ScoreAsync(Request(), Ct);

        var asks = payloads.Stored.Where(p => p.Stage != DeliveredStage.Coverage).ToList();
        asks.Should().OnlyContain(p => p.Protocol == WeightingProtocol.Protocol);
        asks.Should().OnlyContain(p => p.PromptHash.Length == 64);
    }

    [Fact]
    public async Task The_GATE_records_its_own_verdict_as_a_payload()
    {
        var payloads = new InMemoryPayloads();

        await Stage(new ScriptedRuntime([Decomposition(30), Scores(30, 3)]), payloads).ScoreAsync(Request(), Ct);

        // So a rescore sees what was decided without re-deriving it, and a reader can tell a re-ask that
        // helped from one that did not.
        var coverage = payloads.Stored.Single(p => p.Stage == DeliveredStage.Coverage);
        coverage.PayloadJson.Should().Contain("Accept").And.Contain("passed");
    }

    [Fact]
    public async Task A_weighing_that_MISSES_a_step_is_refused_rather_than_scored_short()
    {
        var runtime = new ScriptedRuntime([Decomposition(30), Scores(29, 3)]);

        var scored = await Stage(runtime, new InMemoryPayloads()).ScoreAsync(Request(), Ct);

        // A key silently dropped here would silently drop work from the score — and the number would look
        // exactly like an honest one.
        scored.Failed().Should().BeTrue();
        scored.Reason().Should().Contain("missing:");
    }

    [Fact]
    public async Task The_near_duplicate_CAP_reaches_the_score_through_the_stage()
    {
        // Two steps on ONE file, the second declaring itself a mirror; the other 28 sit on files of their
        // own so nothing else groups. Written explicitly rather than through Steps(), because that helper
        // gives every step its own anchor — which is the cross-file case the rule deliberately EXEMPTS.
        var steps = $$"""{"steps":[{{string.Join(',', Enumerable.Range(1, 30).Select(i =>
            $$"""{"key":"s{{i}}","what":"did {{i}}","anchor":"src/{{(i <= 2 ? "Shared" : $"F{i}")}}.cs"}"""))}}]}""";
        var scores = string.Join(',', Enumerable.Range(1, 30).Select(i =>
            i == 2
                ? """{"key":"s2","score":5,"why":"mirrors s1 — same established pattern"}"""
                : $$"""{"key":"s{{i}}","score":5,"why":"real work"}"""));

        var runtime = new ScriptedRuntime([steps, $$"""{"scores":[{{scores}}]}"""]);

        var scored = await Stage(runtime, new InMemoryPayloads()).ScoreAsync(Request(), Ct);

        // 29 x 5 + the capped 2. The module owns the rule; this asserts the stage actually consults it.
        scored.Ok().Policy.Total.Should().Be((29 * 5) + Inherited.NearDuplicateCap);
        scored.Ok().Policy.Adjustments.Should().ContainSingle()
            .Which.Rule.Should().Be(DeliveredWorkPolicy.NearDuplicateRule);
    }

    // ---- scaffolding -------------------------------------------------------------------------------

    private static DeliveredWorkStage Stage(IModelRuntime runtime, IStagePayloadStore payloads) =>
        new(runtime, payloads, new TestClock(Noon));

    private static DeliveredWorkRequest Request() => new(
        Guid.CreateVersion7(),
        Diff,
        ModelEndpoint.Cli(ModelRef.Parse("m", ModelHosting.Local).Ok()),
        Sampling.Deterministic(1),
        []);

    /// <summary>A decomposition of <paramref name="steps"/> anchored steps, each on its own file so the
    /// near-duplicate rule has nothing to group.</summary>
    private static string Steps(int steps) =>
        $$"""{"steps":[{{string.Join(',', Enumerable.Range(1, steps).Select(i =>
            $$"""{"key":"s{{i}}","what":"did {{i}}","anchor":"src/F{{i}}.cs"}"""))}}]}""";

    private static string Decomposition(int steps) => Steps(steps);

    private static string Scores(int steps, int each) =>
        $$"""{"scores":[{{string.Join(',', Enumerable.Range(1, steps).Select(i =>
            $$"""{"key":"s{{i}}","score":{{each}},"why":"real work"}"""))}}]}""";

    /// <summary>Answers a fixed script, in order, and counts what it was asked.</summary>
    private sealed class ScriptedRuntime(IReadOnlyList<string> replies) : IModelRuntime
    {
        private readonly List<string> _prompts = [];

        public ModelHosting Hosting => ModelHosting.Local;

        public int Asks { get; private set; }

        public IReadOnlyList<string> Prompts => _prompts;

        public Task<Outcome<string>> AcceptBudgetAsync(Budget budget, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<string>.Success("scripted"));

        public Task<Outcome<ModelAnswer>> AskAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            _prompts.Add(request.UserPrompt);

            if (Asks >= replies.Count)
            {
                Asks++;
                return Task.FromResult(Outcome<ModelAnswer>.Failure("the script ran out of replies"));
            }

            var reply = replies[Asks++];

            return Task.FromResult(Outcome<ModelAnswer>.Success(new ModelAnswer(
                Captured.Text(reply),
                CapturedCount.Number(0),
                CapturedCount.Number(0),
                TimeSpan.Zero,
                SamplingAsSent.From(request.Sampling, "scripted"),
                StopReason.Completed,
                string.Empty)));
        }
    }

    private sealed class InMemoryPayloads : IStagePayloadStore
    {
        private readonly List<StagePayload> _stored = [];

        public IReadOnlyList<StagePayload> Stored => _stored;

        public Task<Outcome<StagePayload>> AppendAsync(StagePayload payload, CancellationToken cancellationToken)
        {
            _stored.Add(payload);
            return Task.FromResult(Outcome<StagePayload>.Success(payload));
        }

        public Task<IReadOnlyList<StagePayload>> ForResultAsync(Guid resultId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StagePayload>>(
                [.. _stored.Where(p => p.ResultId == resultId).OrderBy(p => p.Stage).ThenBy(p => p.Ordinal)]);

        public Task<StagePayloadFootprint> FootprintAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new StagePayloadFootprint(
                _stored.Count,
                _stored.Select(p => p.ResultId).Distinct().Count(),
                _stored.Sum(p => (long)p.PayloadJson.Length)));
    }
}
