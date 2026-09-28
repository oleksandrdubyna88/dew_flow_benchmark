using Bench.Domain.Gate;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate.Driver;

/// <summary>S3.4 — the reply parser, the ledger reader, the stderr facts and the tap facts, each a port of the
/// calibration harness's function, feeding <see cref="GateRunFacts"/> and <see cref="FailureCauses"/>.</summary>
public sealed class GateReplyParserTests
{
    [Fact]
    public void A_non_json_reply_is_tool_answered_non_json_and_never_valid()
    {
        var parsed = GateReplyParser.Parse("the server crashed with a stack trace");

        parsed.Reply.Should().BeOfType<GateReply.NotJson>();
        var facts = GateRunFacts.From(parsed.Reply, [Ok()], [], 10, 0, 0);
        facts.Valid.Should().BeFalse();
        facts.Failure.Kind.Should().Be(FailureKind.NonJsonReply);
        facts.Failure.Text.Should().StartWith("tool answered non-JSON");
    }

    [Fact]
    public void A_refusal_object_is_the_tool_refusing_and_says_what_it_said()
    {
        var parsed = GateReplyParser.Parse("""{"error":"no plan round has reached 'proceed' in this session — the plan gate comes first (review_plan)"}""");

        parsed.Reply.Should().BeOfType<GateReply.Refused>();
        var facts = GateRunFacts.From(parsed.Reply, [], [], 1, 0, 0);
        facts.Failure.Kind.Should().Be(FailureKind.ToolRefused);
        facts.Failure.Text.Should().Contain("the plan gate comes first");
    }

    [Fact]
    public void A_call_human_verdict_is_not_valid_and_says_why()
    {
        var parsed = GateReplyParser.Parse("""{"verdict":"call_human","gatingCount":9,"findings":[],"reviewers":"2 of 3 reviewers answered"}""");

        var facts = GateRunFacts.From(parsed.Reply, [Ok()], [], 10, 0, 0);

        facts.Valid.Should().BeFalse();
        facts.Failure.Kind.Should().Be(FailureKind.VerdictNotPassing);
        facts.Failure.Text.Should().Contain("CallHuman").And.Contain("the product stopped");
    }

    [Fact]
    public void A_skipped_feature_review_is_a_word_of_its_own_and_not_valid()
    {
        var parsed = GateReplyParser.Parse("""{"verdict":"skipped","findings":[]}""");

        ((GateReply.Answered)parsed.Reply).Verdict.Should().Be(GateVerdictWord.Skipped,
            "a plan with fewer epics than COAI_FEATURE_MIN_EPICS is SKIPPED by the product, not reviewed");
        GateRunFacts.From(parsed.Reply, [Ok()], [], 1, 0, 0).Valid.Should().BeFalse();
    }

    [Fact]
    public void Findings_are_read_with_the_products_words_and_their_text_kept_for_the_artefact_store()
    {
        var parsed = GateReplyParser.Parse("""
            {"verdict":"revise","findings":[
              {"severity":"Blocking","category":"Feasibility","file":"src/A.cs","line":12,"title":"t","why":"w","fix":"f","isGating":true,"providers":["grok"]},
              {"severity":"Nit","category":"Ux","file":"","line":0,"title":"only a title"}
            ]}
            """);

        parsed.Findings.Should().HaveCount(2);
        parsed.Findings[0].Should().Match<ParsedFinding>(f =>
            f.Severity == FindingSeverity.Blocking && f.Category == FindingCategory.Feasibility && f.IsGating && f.Line == 12 && f.File == "src/A.cs");
        parsed.Findings[0].Text.Should().Contain("t").And.Contain("w").And.Contain("f");
        parsed.Findings[1].IsGating.Should().BeFalse("a Nit is not gating unless the product said so");
        ((GateReply.Answered)parsed.Reply).Findings.Value.Should().Be(2);
    }

    [Fact]
    public void A_ledger_row_without_reasoning_tokens_or_cost_reads_as_not_captured_and_only_this_stages_review_rows_count()
    {
        var rows = LedgerRows.Parse(
            """
            {"provider":"grok","model":"m","role":"PlanCritique","stage":"PlanReview","seconds":5.5,"tokensIn":100,"tokensOut":10,"costUsd":null,"outcome":"ok","tokensCached":64}
            {"provider":"grok","model":"m","role":"Architecture","stage":"CodeReview","seconds":7.25,"tokensIn":900,"tokensOut":90,"costUsd":0.02,"outcome":"ok","tokensCached":0,"tokensReasoning":512}
            {"provider":"codex","model":"m","role":"consult","stage":"Consultation","seconds":50,"tokensIn":5,"tokensOut":5,"costUsd":null,"outcome":"ok","kind":"consult"}
            not a json line
            """,
            GateKind.Code);

        rows.Turns.Should().ContainSingle("only the CodeReview stage is the code reviewer's spend");
        rows.Turns[0].TokensReasoning.Value.Should().Be(512);
        rows.Turns[0].CostUsd.Value.Should().Be(0.02m);
        rows.Unparsed.Should().Be(1, "an unreadable line is counted, never dropped silently");

        var plan = LedgerRows.Parse("""{"provider":"grok","stage":"PlanReview","seconds":5.5,"tokensIn":100,"tokensOut":10,"costUsd":null,"outcome":"ok"}""", GateKind.Plan);
        plan.Turns[0].TokensReasoning.WasCaptured.Should().BeFalse("0.39.0 writes no tokensReasoning — absent is not zero");
        plan.Turns[0].CostUsd.WasCaptured.Should().BeFalse("a null cost is unknown, never free");
        plan.Turns[0].TokensCached.WasCaptured.Should().BeFalse();
    }

    [Fact]
    public void A_length_cut_call_is_named_as_such_when_it_cost_the_run_its_validity()
    {
        var call = TapCallFacts.From(
            """{"call":1,"status":200,"wall_s":61.2}""",
            """{"choices":[{"finish_reason":"length","message":{"content":""}}],"usage":{"prompt_tokens":9000,"completion_tokens":8192,"completion_tokens_details":{"reasoning_tokens":8100}}}""");

        var facts = GateRunFacts.From(GateReplyParser.Parse("""{"verdict":"revise","findings":[]}""").Reply, [Ok() with { Outcome = "no answer" }], [call], 70, 0, 0);

        call.FinishReason.Should().Be("length");
        facts.Failure.Kind.Should().Be(FailureKind.LengthCut);
        facts.Failure.Text.Should().Contain("call 1: finish_reason=length").And.Contain("reasoning=8100");
    }

    [Fact]
    public void A_call_the_tap_forwarded_and_never_saw_answered_is_status_zero_and_a_failure()
    {
        var call = TapCallFacts.From(factsJson: string.Empty, responseJson: string.Empty);

        call.Status.Should().Be(0);
        GateRunFacts.From(GateReplyParser.Parse("""{"verdict":"proceed","findings":[]}""").Reply, [Ok() with { Outcome = "timeout" }], [call], 5, 0, 0).Failure.Kind
            .Should().Be(FailureKind.HttpError);
    }

    [Fact]
    public void The_served_and_refused_counts_and_the_turn_one_prompt_come_off_the_servers_stderr()
    {
        var stderr = string.Join('\n',
            "\u001b[32minfo\u001b[0m launching api shim --prompt-file /tmp/coai-answers-1/api-1.prompt.md --schema-file /tmp/s.json --out /tmp/coai-answers-1/api-1.answer.json",
            "reviewer grok answered in 312.4s over 3 turns; source: turn 1: served a.cs (1-80 of 200), b.cs (10-20 of 30); not served c.cs - outside the diff - too big turn 2: served d.cs (1-5 of 5)");

        var facts = StderrFacts.Of(stderr);

        facts.Served.Should().Be(3);
        facts.Refused.Should().Be(2);
        facts.PromptFiles.Should().Equal(["/tmp/coai-answers-1/api-1.prompt.md"]);
        StderrFacts.Of("nothing about a reviewer here").PromptFiles.Should().BeEmpty("a CLI reviewer launches no shim — absent, never a crash");
    }

    private static LedgerTurn Ok() => new(1.0, Bench.Domain.Trace.CapturedCount.Number(1), Bench.Domain.Trace.CapturedCount.Number(1),
        Bench.Domain.Trace.CapturedCount.Number(0), Bench.Domain.Trace.CapturedCount.Unavailable("-"), Bench.Domain.Trace.CapturedUsd.Unavailable("-"), "ok");
}
