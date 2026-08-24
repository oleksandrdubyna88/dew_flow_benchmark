using Bench.Application;
using Bench.Application.Delivered;
using Bench.Delivered;
using Bench.Domain;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Delivered;

/// <summary>Recomputing a delivered-work score from stored payloads.
/// <para>
/// The property under test is the one the permanent payload table exists to buy: a published score can be
/// re-derived later with <b>no model call</b>. It is asserted structurally rather than by inspection —
/// <see cref="DeliveredRescore"/> takes no runtime, so it cannot reach one — and the counting test below
/// exists anyway, because "we did not call a model" is exactly the kind of promise that decays silently.
/// </para></summary>
public sealed class DeliveredRescoreTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid Result = Guid.CreateVersion7();

    [Fact]
    public void A_stored_run_recomputes_to_the_same_total_it_was_scored_at()
    {
        var recomputed = DeliveredRescore.Recompute(Result, [
            Payload(DeliveredStage.Decompose, 0, Steps(("s1", "src/A.cs"), ("s2", "src/B.cs"))),
            Payload(DeliveredStage.Weigh, 0, Scores(("s1", 5, "real"), ("s2", 3, "real"))),
        ]);

        // The whole point: the same inputs produce the same number, forever, without paying for it twice.
        recomputed.Ok().Recomputed.Total.Should().Be(8);
    }

    [Fact]
    public void The_POLICY_is_applied_again_rather_than_the_stored_total_being_echoed()
    {
        var recomputed = DeliveredRescore.Recompute(Result, [
            Payload(DeliveredStage.Decompose, 0, Steps(("s1", "src/M.cs"), ("s2", "src/M.cs"))),
            Payload(DeliveredStage.Weigh, 0, Scores(("s1", 5, "the original"), ("s2", 5, "mirrors s1"))),
        ]);

        // A rescore that echoed a stored number would be a lookup, not a recomputation — and would go on
        // agreeing with itself after the policy changed, which is the one thing it must not do.
        recomputed.Ok().Recomputed.Total.Should().Be(5 + Inherited.NearDuplicateCap);
        recomputed.Ok().Recomputed.Adjustments.Should().ContainSingle()
            .Which.Rule.Should().Be(DeliveredWorkPolicy.NearDuplicateRule);
    }

    [Fact]
    public void The_LAST_decomposition_attempt_is_the_one_rescored()
    {
        var recomputed = DeliveredRescore.Recompute(Result, [
            Payload(DeliveredStage.Decompose, 0, Steps(("s1", "src/A.cs"))),
            Payload(DeliveredStage.Decompose, 1, Steps(("s1", "src/A.cs"), ("s2", "src/B.cs"))),
            Payload(DeliveredStage.Weigh, 0, Scores(("s1", 4, "real"), ("s2", 4, "real"))),
        ]);

        // A re-ask REPLACES its predecessor rather than adding to it, so reading the first would rescore a
        // decomposition the run itself rejected — and would then fail on keys the weighing never had.
        recomputed.Ok().Recomputed.Applied.Should().HaveCount(2);
        recomputed.Ok().Recomputed.Total.Should().Be(8);
    }

    [Fact]
    public void The_PROTOCOL_comes_from_the_payload_never_from_whatever_is_current()
    {
        var recomputed = DeliveredRescore.Recompute(Result, [
            Payload(DeliveredStage.Decompose, 0, Steps(("s1", "src/A.cs"))) with { Protocol = "delivered-work-v0" },
            Payload(DeliveredStage.Weigh, 0, Scores(("s1", 5, "real"))),
        ]);

        // A rescore under a different scale is a NEW measurement, not a correction. Reading the current
        // protocol would silently re-label a historical run the day the scale moves.
        recomputed.Ok().Protocol.Should().Be("delivered-work-v0");
    }

    [Fact]
    public void A_result_with_NO_payloads_is_named_as_unrescorable_rather_than_scored_zero()
    {
        var recomputed = DeliveredRescore.Recompute(Result, []);

        // Every result measured before payloads were kept is in this state. Scoring it zero would put a
        // fabricated number beside real ones; refusing silently would look like a broken store.
        recomputed.Failed().Should().BeTrue();
        recomputed.Reason().Should().Contain("not rescorable");
    }

    [Fact]
    public void An_INCOMPLETE_pair_says_which_half_is_missing()
    {
        DeliveredRescore.Recompute(Result, [Payload(DeliveredStage.Decompose, 0, Steps(("s1", "src/A.cs")))])
            .Reason().Should().Contain("no weighing");

        DeliveredRescore.Recompute(Result, [Payload(DeliveredStage.Weigh, 0, Scores(("s1", 5, "real")))])
            .Reason().Should().Contain("no decomposition");
    }

    [Fact]
    public void A_payload_that_NO_LONGER_READS_is_a_defect_and_says_so()
    {
        var recomputed = DeliveredRescore.Recompute(Result, [
            Payload(DeliveredStage.Decompose, 0, "this was never JSON"),
            Payload(DeliveredStage.Weigh, 0, Scores(("s1", 5, "real"))),
        ]);

        // Distinct from "no payloads", and it must stay distinct: one is an ordinary history, the other is
        // a parser that changed under stored data — which is the failure a rescore is meant to surface.
        recomputed.Reason().Should().Contain("no longer reads");
    }

    [Fact]
    public async Task Rescoring_a_whole_run_calls_a_model_ZERO_times()
    {
        var payloads = new CountingPayloads([
            Payload(DeliveredStage.Decompose, 0, Steps(("s1", "src/A.cs"))),
            Payload(DeliveredStage.Weigh, 0, Scores(("s1", 7, "real"))),
        ]);

        var report = await new DeliveredRescore(payloads).ForResultsAsync(
            [Result], TestContext.Current.CancellationToken);

        // Structural: DeliveredRescore takes no IModelRuntime, so there is nothing to call. Asserted anyway
        // — the day somebody adds a runtime parameter "just for the judge", this is what goes red.
        typeof(DeliveredRescore).GetConstructors().Single().GetParameters()
            .Should().ContainSingle().Which.ParameterType.Name.Should().Be("IStagePayloadStore");

        report.Rescored.Should().ContainSingle().Which.Recomputed.Total.Should().Be(7);
        report.Describe.Should().Contain("no model was called");
    }

    [Fact]
    public async Task A_run_mixing_rescorable_and_not_reports_BOTH_rather_than_the_happier_half()
    {
        var payloads = new CountingPayloads([
            Payload(DeliveredStage.Decompose, 0, Steps(("s1", "src/A.cs"))),
            Payload(DeliveredStage.Weigh, 0, Scores(("s1", 6, "real"))),
        ]);

        var report = await new DeliveredRescore(payloads).ForResultsAsync(
            [Result, Guid.CreateVersion7()], TestContext.Current.CancellationToken);

        report.Rescored.Should().ContainSingle();
        report.Skipped.Should().ContainSingle().Which.Reason.Should().Contain("not rescorable");
    }

    // ---- scaffolding -------------------------------------------------------------------------------

    private static StagePayload Payload(DeliveredStage stage, int ordinal, string json) =>
        StagePayload.Of(Result, stage, ordinal, json, "hash", WeightingProtocol.Protocol, Noon);

    private static string Steps(params (string Key, string Anchor)[] steps) =>
        $$"""{"steps":[{{string.Join(',', steps.Select(s =>
            $$"""{"key":"{{s.Key}}","what":"did it","anchor":"{{s.Anchor}}"}"""))}}]}""";

    private static string Scores(params (string Key, int Score, string Why)[] scores) =>
        $$"""{"scores":[{{string.Join(',', scores.Select(s =>
            $$"""{"key":"{{s.Key}}","score":{{s.Score}},"why":"{{s.Why}}"}"""))}}]}""";

    /// <summary>Answers the given payloads for <see cref="Result"/> and nothing for any other id.</summary>
    private sealed class CountingPayloads(IReadOnlyList<StagePayload> stored) : IStagePayloadStore
    {
        public Task<Outcome<StagePayload>> AppendAsync(StagePayload payload, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("a rescore must never write a payload");

        public Task<IReadOnlyList<StagePayload>> ForResultAsync(Guid resultId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StagePayload>>(
                resultId == Result ? stored : []);

        public Task<StagePayloadFootprint> FootprintAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new StagePayloadFootprint(stored.Count, 1, 0));
    }
}
