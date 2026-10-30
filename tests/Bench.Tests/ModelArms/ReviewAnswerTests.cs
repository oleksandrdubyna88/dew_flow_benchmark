using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Domain;
using Bench.Domain.Trace;
using FluentAssertions;
using ModelArms.Core.Launch;
using ModelArms.Core.Steps;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The review step's verdict file, from the LAST result only (plan C10, D21, D25; E2 §5 E2.S5, §8 item 7). The text of <c>notes/review.json</c> (or <c>hA.json</c>, <c>hB.json</c> — the bound output is the
/// runner's business, never this type's) is the shell's span of the last result — its first <c>{</c> to its last <c>}</c> — when that span is a JSON object, else an honest <c>unparsed</c> with the raw text. The
/// fixtures are the 2026-10-28 lines of the part-6 experiment (see <c>ClaudeResultEnvelopeTests</c>); every stream a test builds goes through the real reader, <c>ClaudeResultEnvelope.LastOf</c>, so no fixture is a
/// reading of the envelope's type.
/// <para>
/// Scenarios 1–4 are the story's. Scenarios 5–9 are ADDED by the coder, in the story's style (Given / When / Then), because the story is silent on them and its own instruction is to attack the parser: 5 — only the LAST
/// result decides; 6 — a result is read for a verdict only when the classifier calls it finished, and the answer for every <c>StepEnd</c> kind is pinned; 7 — the attacks on the span; 8 — a megabyte; 9 — a lone
/// surrogate never throws.
/// </para>
/// What this does NOT prove: what a real review model prints (the span rule is the shell's, and a reviewer that writes two objects gets <c>unparsed</c>, said in the module doc), that the file is written (E2.S4's
/// <c>DerivedOutputs</c>), or anything about the schema of the verdict (the fixer and the analysis read that).
/// </summary>
public sealed class ReviewAnswerTests
{
    private const string ThreeResults = "stream-2026-10-28-review-three-results.jsonl";

    private const string Waiting = "I'm waiting for the full MCP suite. When it finishes I will send the verdict.";

    private const string Ready = """{"verdict":"ready","findings":[]}""";

    private static string Read(string name) => ModelArmsFixtures.Read(name);

    /// <summary>One result line carrying <paramref name="text"/>, built like the recorded success line.</summary>
    private static string ResultLine(string text) =>
        new JsonObject { ["type"] = "result", ["subtype"] = "success", ["is_error"] = false, ["result"] = text }.ToJsonString();

    private static Outcome<string> Extract(string stream) => ReviewAnswer.Extract(ClaudeResultEnvelope.LastOf(stream));

    private static Outcome<string> AnswerFor(string resultText) => Extract(ResultLine(resultText));

    private static string Unparsed(string raw) =>
        new JsonObject { ["verdict"] = "unparsed", ["raw"] = raw }.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    /// <summary>The answer read back: a JSON object whose <c>verdict</c> is <c>unparsed</c> and whose <c>raw</c> is <paramref name="raw"/>, character for character.</summary>
    private static void ShouldBeUnparsedWith(string answer, string raw)
    {
        answer.Should().StartWith("{\"verdict\":\"unparsed\",\"raw\":", "the unparsed answer says so first");
        using var document = JsonDocument.Parse(answer);
        document.RootElement.ValueKind.Should().Be(JsonValueKind.Object, "the verdict file is always a JSON object");
        document.RootElement.EnumerateObject().Select(member => member.Name).Should().Equal("verdict", "raw");
        document.RootElement.GetProperty("verdict").GetString().Should().Be("unparsed");
        document.RootElement.GetProperty("raw").GetString().Should().Be(raw, "the raw text is kept whole");
    }

    // ---- Scenario 1 -------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Given a last result whose text holds a JSON object (alone, or between prose), When the answer is extracted, Then it is the object, byte for byte — the prose around it is not part of it.</summary>
    [Fact]
    public void The_json_block_of_the_last_result_is_the_review()
    {
        AnswerFor("Reviewed.\n" + Ready).Ok().Should().Be(Ready, "the recorded success line's text is prose, a newline and the object");
        Extract(Read("result-success.jsonl")).Ok().Should().Be(Ready);

        const string object2 = """{"verdict":"fix-then-ready","findings":[{"id":"R1"}]}""";
        AnswerFor("Here it is:\n" + object2 + "\nDone.").Ok().Should().Be(object2, "the object, byte for byte, without the prose either side");
    }

    // ---- Scenario 2 -------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Given the recorded :310 text of the 2026-10-28 review, which has no braces at all, Then the answer is an honest <c>unparsed</c> carrying that text — a reader that finds no verdict says so.</summary>
    [Fact]
    public void No_block_is_unparsed_with_the_raw_text()
    {
        var first = Read(ThreeResults).Split('\n')[0];

        Extract(first).Ok().Should().Be("{\"verdict\":\"unparsed\",\"raw\":\"" + Waiting + "\"}", "the apostrophe stays an apostrophe: the file is read by a person and by a fixer");
    }

    // ---- Scenario 3 -------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Given a review whose last result is an error (the 2026-10-28 429 line; a 400), or no result at all, Then there is no answer: no verdict file is written, so a limit is never mistaken for a review that found nothing.</summary>
    [Fact]
    public void An_error_result_produces_no_review_file()
    {
        const string reason = "the review's last result is an error or absent — no review.json is written";

        foreach (var stream in new[] { Read("result-429-usage-limit-reached.jsonl"), Read("result-400-model-unsupported.jsonl"), Read("stream-no-result.jsonl") })
        {
            var answer = Extract(stream);
            answer.Failed().Should().BeTrue("an error result is not a review, and no result is none");
            answer.Reason().Should().Be(reason);
        }
    }

    // ---- Scenario 4 -------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Given <c>{a} and {b}</c>: the shell's greedy span is the whole text, which is not JSON. Then <c>unparsed</c> with the raw text — the shell wrote the invalid span as it was, and a reader crashing on it is worse.</summary>
    [Fact]
    public void A_span_that_is_not_json_is_unparsed_with_the_raw_text()
    {
        AnswerFor("{a} and {b}").Ok().Should().Be(Unparsed("{a} and {b}"));
    }

    // ---- Scenario 5 (added) -----------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Given a stream of three results where the first is a success and the last a 429 (the recorded one), Then there is no answer: the first result is not the review, and prose is not "unparsed" when the last word was a limit.
    /// And given an earlier result that holds a verdict while the last is prose, Then the answer is the LAST one's <c>unparsed</c> — the earlier verdict is nowhere in it.</summary>
    [Fact]
    public void Only_the_last_result_decides()
    {
        Extract(Read(ThreeResults)).Failed().Should().BeTrue("the last result is a 429; the success before it is not the review");

        var stream = ResultLine(Ready) + "\n" + ResultLine("Still checking.");
        var answer = Extract(stream).Ok();
        ShouldBeUnparsedWith(answer, "Still checking.");
        answer.Should().NotContain("ready", "an earlier result's verdict must not leak into the last one's answer");

        Extract(ResultLine("Still checking.") + "\n" + ResultLine(Ready)).Ok().Should().Be(Ready, "and the other way round: the last result's object");
    }

    // ---- Scenario 6 (added) -----------------------------------------------------------------------------------------------------------------------------------------

    private static readonly AttemptFacts Killed = new(string.Empty, true, false);

    private static readonly AttemptFacts Refused = new("the executable /x/bin/claude does not exist", false, false);

    private static string Cut(string line, int length) => line.TrimEnd('\n')[..length];

    private static string EditedLimit429(Action<JsonObject> edit)
    {
        var node = JsonNode.Parse(Read("result-429-usage-limit-reached.jsonl"))!.AsObject();
        edit(node);
        return node.ToJsonString();
    }

    /// <summary>One row per way an attempt can end: the stream, what the launcher knew, the classifier's word for it, and whether a review is read from it. The word is asserted with the classifier, so the table cannot
    /// drift from <c>StepEnd</c>: a verdict is read only from <c>finished</c>.</summary>
    public static TheoryData<string, string, string, bool> Ends
    {
        get
        {
            var success = Read("result-success.jsonl");
            return new TheoryData<string, string, string, bool>
            {
                { "a success", success, "finished", true },
                { "a 429 with is_error true", Read("result-429-usage-limit-reached.jsonl"), "limited", false },
                { "a 429 whose is_error is false", EditedLimit429(o => o["is_error"] = false), "limited", false },
                { "a 429 with no is_error", EditedLimit429(o => o.Remove("is_error")), "limited", false },
                { "an error that is not a limit", Read("result-400-model-unsupported.jsonl"), "errored", false },
                { "a success, then a cut result line", success + "\n" + Cut(Read("result-429-usage-limit-reached.jsonl"), 1300), "interrupted", false },
                { "no result", Read("stream-no-result.jsonl"), "interrupted", false },
                { "no stream at all", string.Empty, "interrupted", false },
            };
        }
    }

    /// <summary>Given a stream, When the classifier calls its end <paramref name="word"/>, Then a review is read from it only for <c>finished</c> — a limit, an error, a cut last line and a stream with no result give no answer. Each row
    /// is read through the same reader and the same classifier the runner uses; <c>hard-deadline</c> and <c>not-started</c> carry no result by their definition and are the last two tests below.</summary>
    [Theory]
    [MemberData(nameof(Ends))]
    public void A_review_is_read_only_from_a_result_the_classifier_calls_finished(string name, string stream, string word, bool reads)
    {
        var last = ClaudeResultEnvelope.LastOf(stream);
        StepClassifier.Classify(last, string.Empty, AttemptFacts.Ran).Word.Should().Be(word, $"{name}: the row says how the attempt ended");

        var answer = ReviewAnswer.Extract(last);

        answer.Failed().Should().Be(!reads, $"{name}: a verdict file comes from a finished attempt only");
        if (reads)
        {
            answer.Ok().Should().Be(Ready);
        }
    }

    /// <summary>Given an attempt the wall killed with no result, or one that was never started, Then there is no result and so no answer: the runner hands <c>Extract</c> the reader's failure, which is a failure.</summary>
    [Fact]
    public void A_killed_attempt_and_a_refused_launch_have_no_result_and_so_no_review()
    {
        var none = ClaudeResultEnvelope.LastOf(Read("stream-no-result.jsonl"));

        StepClassifier.Classify(none, string.Empty, Killed).Should().BeOfType<StepEnd.HardDeadline>();
        ReviewAnswer.Extract(none).Failed().Should().BeTrue("a hard deadline with no result has no review");

        var refused = ClaudeResultEnvelope.LastOf(string.Empty);
        StepClassifier.Classify(refused, string.Empty, Refused).Should().BeOfType<StepEnd.NotStarted>();
        ReviewAnswer.Extract(refused).Failed().Should().BeTrue("a launch that was refused ran nothing");
    }

    // ---- Scenario 7 (added): the attacks on the span -----------------------------------------------------------------------------------------------------------------

    /// <summary>The expected span per attack: <c>null</c> when the answer must be <c>unparsed</c>. The decisions: the shell's greedy span, decided by whether IT parses as a JSON object (comments, trailing commas, a second
    /// definition of one name — at any depth — and single quotes are all refused); nothing is looked for inside a fence, around a second object, or past a stray brace.</summary>
    public static TheoryData<string, string, string?> Attacks =>
        new()
        {
            { "prose, the object, prose with no braces", "Verdict follows.\n" + Ready + "\nThat is all.", Ready },
            { "two objects with prose between", """{"a":1} and {"b":2}""", null },
            { "two objects on two lines", "{\"a\":1}\n{\"b\":2}", null },
            { "an object in a json fence", "```json\n" + Ready + "\n```", Ready },
            { "an object in a fence, then prose with braces", "```json\n" + Ready + "\n```\nSee {the notes}.", null },
            { "braces in prose before the object", "I wrote {braces} here: " + Ready, null },
            { "braces inside strings", """{"note":"} and {","verdict":"ready"}""", """{"note":"} and {","verdict":"ready"}""" },
            { "an escaped quote and a brace in a string", """{"note":"say \"}\" ","verdict":"ready"}""", """{"note":"say \"}\" ","verdict":"ready"}""" },
            { "an unbalanced open brace", """{"verdict":"ready","findings":[]""", null },
            { "an extra closing brace", Ready + "}", null },
            { "only a closing brace before an opening one", "} then {", null },
            { "a lone open brace", "{", null },
            { "an object of one array item", """[{"verdict":"ready"}]""", """{"verdict":"ready"}""" },
            { "an array of two objects", """[{"id":"R1"},{"id":"R2"}]""", null },
            { "an empty object", "{}", "{}" },
            { "a comment inside the object", """{"verdict":"ready" /* ok */}""", null },
            { "a trailing comma", """{"verdict":"ready",}""", null },
            { "single quotes", "{'verdict':'ready'}", null },
            { "a name defined twice with different values", """{"verdict":"ready","verdict":"fix-then-ready"}""", null },
            { "a name defined twice in a nested object", """{"verdict":"ready","findings":[{"id":"R1","id":"R2"}]}""", null },
            { "an empty result", string.Empty, null },
            { "a result of white space", " \n\t ", null },
            { "text with a lone surrogate escape inside the object", """{"verdict":"ready","note":"\ud800"}""", """{"verdict":"ready","note":"\ud800"}""" },
        };

    /// <summary>Given each attack, Then the answer is the span byte for byte when the greedy span is a JSON object, and otherwise an <c>unparsed</c> carrying the whole result — never an exception, never a half-reading.</summary>
    [Theory]
    [MemberData(nameof(Attacks))]
    public void The_span_rule_decides_every_attack_and_never_throws(string name, string result, string? span)
    {
        var answer = AnswerFor(result).Ok();

        if (span is null)
        {
            ShouldBeUnparsedWith(answer, result);
        }
        else
        {
            answer.Should().Be(span, $"{name}: the span itself is the answer");
        }
    }

    /// <summary>Given text that needs escaping — a quote, a backslash, a newline, a tab, a control character, an apostrophe, an angle bracket, an ampersand, a non-ASCII letter — Then the answer is valid JSON that reads back to the text,
    /// and the readable characters stay readable (no <c>'</c>).</summary>
    [Fact]
    public void The_raw_text_of_an_unparsed_answer_reads_back_exactly_and_stays_readable()
    {
        const string text = "It's <done> & \"quoted\" \\ path\nline two\ttab \u0001 é · 日本 😀 {not json";

        var answer = AnswerFor(text).Ok();

        ShouldBeUnparsedWith(answer, text);
        answer.Should().Contain("It's <done> & ").And.Contain("é · 日本 ", "the file is read by a person (a character outside the basic plane, the emoji, is escaped as a surrogate pair — valid JSON that reads back)");
        answer.Should().NotContain("\\u0027").And.NotContain("\\u003C");
    }

    // ---- Scenario 8 (added): a megabyte --------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Given results of about one megabyte, Then each is answered by the same rule: a megabyte of prose with the object at the end, a megabyte object, a megabyte with no braces, a megabyte of nested braces
    /// (deeper than the JSON reader allows) and a megabyte of brace pairs. Nothing throws, nothing is cut, and a result too big to be JSON is <c>unparsed</c> with ALL of its text (the raw evidence is the point).</summary>
    [Fact]
    public void A_result_of_a_megabyte_is_answered_by_the_same_rule()
    {
        var prose = new string('x', 1_000_000);
        AnswerFor(prose + "\n" + Ready).Ok().Should().Be(Ready, "an object after a megabyte of prose");

        var big = "{\"verdict\":\"fix-then-ready\",\"findings\":[" + string.Join(',', Enumerable.Range(0, 12_000).Select(i => "{\"id\":\"R" + i + "\",\"note\":\"" + new string('n', 60) + "\"}")) + "]}";
        big.Length.Should().BeGreaterThan(900_000);
        AnswerFor("Here:\n" + big).Ok().Should().Be(big, "a megabyte object, byte for byte");

        ShouldBeUnparsedWith(AnswerFor(prose).Ok(), prose);

        var nested = new string('{', 500_000) + new string('}', 500_000);
        ShouldBeUnparsedWith(AnswerFor(nested).Ok(), nested);

        var pairs = string.Concat(Enumerable.Repeat("{}", 500_000));
        ShouldBeUnparsedWith(AnswerFor(pairs).Ok(), pairs);
    }

    // ---- Scenario 9 (added): a lone surrogate ---------------------------------------------------------------------------------------------------------------------------

    private static ClaudeResultEnvelope WithText(string text) =>
        new(false, CapturedCount.Unavailable("no api_error_status"), string.Empty, text, [], "success", 1);

    /// <summary>Given a result text with a lone surrogate (the envelope is a public record, so one can be built that no line could produce), Then nothing throws: the text has no span — or a span that cannot be parsed —
    /// and the answer is an <c>unparsed</c> in which the lone surrogate is written as U+FFFD — the one change made to the raw text, made by the JSON writer because JSON text cannot hold one.</summary>
    [Fact]
    public void A_lone_surrogate_in_the_result_never_throws()
    {
        const string cleaned = "bad � text";

        var prose = ReviewAnswer.Extract(Outcome<ClaudeResultEnvelope>.Success(WithText("bad \ud800 text"))).Ok();
        ShouldBeUnparsedWith(prose, cleaned);

        var inSpan = ReviewAnswer.Extract(Outcome<ClaudeResultEnvelope>.Success(WithText("{\"note\":\"x\ud800y\"}"))).Ok();
        ShouldBeUnparsedWith(inSpan, "{\"note\":\"x�y\"}");

        var high = ReviewAnswer.Extract(Outcome<ClaudeResultEnvelope>.Success(WithText("a\udc00b {")));
        ShouldBeUnparsedWith(high.Ok(), "a�b {");
    }

    /// <summary>The envelope a caller built by hand with <c>FollowedByUnreadResult</c> set is not trusted either, even though its text is a perfect verdict.</summary>
    [Fact]
    public void An_envelope_followed_by_an_unread_result_gives_no_review_even_with_a_perfect_verdict()
    {
        ReviewAnswer.Extract(Outcome<ClaudeResultEnvelope>.Success(WithText(Ready) with { FollowedByUnreadResult = true })).Failed().Should().BeTrue();
        ReviewAnswer.Extract(Outcome<ClaudeResultEnvelope>.Success(WithText(Ready))).Ok().Should().Be(Ready, "the same envelope without the flag is the review");
    }
}
