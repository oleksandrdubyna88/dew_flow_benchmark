using Bench.Domain.Gate;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate;

/// <summary>The run's one-line facts and the <c>valid</c> rule — a port of the other harness's
/// <c>summarise</c> and <c>failure_cause</c>, so an imported row and a native one are computed by one function.
/// The rule: verdict in {proceed, revise}, at least one ledger turn, every turn <c>ok</c>, and a findings LIST.</summary>
public sealed class GateRunFactsTests
{
    [Fact]
    public void A_reply_that_parses_with_zero_turns_is_not_valid()
    {
        var facts = GateRunFacts.From(Answered(GateVerdictWord.Proceed, 3), ledger: [], calls: [], wallSeconds: 12.0, served: 0, refused: 0);

        facts.Valid.Should().BeFalse("a verdict nobody was asked for is the product answering, not a reviewer");
        facts.Failure.Kind.Should().Be(FailureKind.NoTurns);
        facts.Failure.Text.Should().Contain("no turn");
    }

    [Fact]
    public void A_ledger_with_no_cost_gives_cost_unknown_never_zero()
    {
        var facts = GateRunFacts.From(Answered(GateVerdictWord.Proceed, 2), [Turn(cost: CapturedUsd.Unavailable("the CLI reports no cost"))], [], 30.0, 4, 0);

        facts.CostUsd.WasCaptured.Should().BeFalse("a CLI reviewer reports no cost — unknown is a state, and a zero would make it the cheapest reviewer");
        facts.Valid.Should().BeTrue("an unknown cost does not invalidate the run");
    }

    [Fact]
    public void A_valid_run_needs_a_passing_verdict_ok_turns_and_a_findings_list()
    {
        var ok = GateRunFacts.From(Answered(GateVerdictWord.Revise, 0), [Turn(), Turn()], [Call(), Call()], 180.7, 6, 0);
        ok.Valid.Should().BeTrue("revise with zero findings is a valid, empty review");
        ok.Failure.Should().Be(FailureCause.None);

        GateRunFacts.From(Answered(GateVerdictWord.CallHuman, 1), [Turn()], [Call()], 10, 0, 0).Failure.Kind
            .Should().Be(FailureKind.VerdictNotPassing);
        GateRunFacts.From(Answered(GateVerdictWord.Proceed, 1), [Turn(), Turn(outcome: "timeout")], [Call(), Call()], 10, 0, 0).Failure
            .Should().Match<FailureCause>(f => f.Kind == FailureKind.TurnFailed && f.Text.Contains("turn 2: timeout"));
        GateRunFacts.From(new GateReply.Answered(GateVerdictWord.Proceed, CapturedCount.Unavailable("findings was a string")), [Turn()], [Call()], 10, 0, 0).Failure.Kind
            .Should().Be(FailureKind.NoFindingsList);
    }

    [Fact]
    public void A_non_json_reply_is_named_as_such_and_never_valid()
    {
        var facts = GateRunFacts.From(new GateReply.NotJson(812), [Turn()], [Call()], 10, 0, 0);

        facts.Valid.Should().BeFalse();
        facts.ReplyParsed.Should().BeFalse();
        facts.Failure.Kind.Should().Be(FailureKind.NonJsonReply);
        facts.Failure.Text.Should().Be("tool answered non-JSON (812 chars)", "the text itself goes to the artefact store, never into the cause");
    }

    [Fact]
    public void A_length_cut_call_is_named_with_its_call_number_and_an_http_error_with_its_status()
    {
        var facts = GateRunFacts.From(
            Answered(GateVerdictWord.CallHuman, 0),
            [Turn(), Turn(outcome: "failed")],
            [Call(), Call(finishReason: "length", reasoning: 10_674, contentChars: 0), Call(status: 429)],
            600, 0, 0);

        facts.Failure.Kind.Should().Be(FailureKind.LengthCut, "the first reason names the kind");
        facts.Failure.Text.Should().Contain("call 2: finish_reason=length (reasoning=10674, content=0 chars)")
            .And.Contain("call 3: HTTP 429")
            .And.Contain("verdict CallHuman")
            .And.Contain("turn 2: failed");
        facts.ExtraCalls.Should().Be(1, "three calls for two turns is one repair or retry — the count proves the call, not its cause");
    }

    [Fact]
    public void Tokens_sum_over_the_ledger_and_reasoning_is_unknown_when_no_turn_carried_it()
    {
        var facts = GateRunFacts.From(
            Answered(GateVerdictWord.Proceed, 3),
            [Turn(tokensIn: 100_000, tokensOut: 8_000, cached: 2_048), Turn(seconds: 37.5, tokensIn: 63_752, tokensOut: 4_456, cached: 80_384)],
            [Call(cached: 2_048), Call(cached: 80_384)],
            180.7, 6, 0);

        facts.TokensIn.Should().Be(CapturedCount.Number(163_752));
        facts.TokensOut.Should().Be(CapturedCount.Number(12_456));
        facts.TokensCached.Should().Be(CapturedCount.Number(82_432));
        facts.TokensReasoning.WasCaptured.Should().BeFalse("no turn reported reasoning tokens — not captured, not zero");
        facts.SecondsPerTurn.Should().Equal([140.1, 37.5]);
        facts.ReviewSeconds.Should().Be(177.6);
        facts.IsWarm.Should().BeFalse("turn 1 read 2 048 cached tokens, under the warm threshold");
        facts.CostUsd.Should().Be(CapturedUsd.Amount(0.190087m));
    }

    [Fact]
    public void A_run_whose_first_call_read_a_large_cache_is_warm()
    {
        var facts = GateRunFacts.From(Answered(GateVerdictWord.Proceed, 1), [Turn()], [Call(cached: 80_384)], 40, 0, 0);

        facts.IsWarm.Should().BeTrue("the prompt was served from a vendor cache rather than read afresh — reported apart");
    }

    [Theory]
    [InlineData("proceed", GateVerdictWord.Proceed)]
    [InlineData("GOOD_ENOUGH", GateVerdictWord.GoodEnough)]
    [InlineData("continue_anyway", GateVerdictWord.ContinueAnyway)]
    [InlineData("call_human", GateVerdictWord.CallHuman)]
    [InlineData("escalated", GateVerdictWord.Escalated)]
    [InlineData("something-new", GateVerdictWord.Unknown)]
    [InlineData("", GateVerdictWord.Unknown)]
    public void The_products_verdict_words_parse_by_name_and_an_unknown_one_is_a_state(string word, GateVerdictWord expected)
    {
        GateVerdictWords.Parse(word).Should().Be(expected);
    }

    private static GateReply Answered(GateVerdictWord verdict, int findings) =>
        new GateReply.Answered(verdict, CapturedCount.Number(findings));

    private static LedgerTurn Turn(
        double seconds = 140.1, long tokensIn = 1000, long tokensOut = 100, long cached = 0,
        CapturedUsd? cost = null, string outcome = "ok") =>
        new(
            seconds,
            CapturedCount.Number(tokensIn),
            CapturedCount.Number(tokensOut),
            CapturedCount.Number(cached),
            CapturedCount.Unavailable("the ledger row carried no reasoning tokens"),
            cost ?? CapturedUsd.Amount(0.0950435m),
            outcome);

    private static HttpCallFacts Call(int status = 200, string finishReason = "stop", long cached = 0, long reasoning = -1, int contentChars = 900) =>
        new(
            status,
            finishReason,
            CapturedCount.Number(cached),
            reasoning < 0 ? CapturedCount.Unavailable("no usage block") : CapturedCount.Number(reasoning),
            37.5,
            contentChars);
}
