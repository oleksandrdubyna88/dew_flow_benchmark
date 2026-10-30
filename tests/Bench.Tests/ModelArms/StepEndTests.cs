using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Domain;
using FluentAssertions;
using ModelArms.Core.Launch;
using ModelArms.Core.Steps;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The one decision of how an attempt ended, in the order of the plan's criterion 2 (E2 §5, E2.S1), pinned on the lines recorded on 2026-10-28 (the incident:
/// <c>repair-limit-2026-10-28.sh:2-5</c> of the part-6 experiment — nine steps were marked done in seconds because a limit was read by its words), on the product's envelope
/// fixtures and on the shell's limit pattern (<c>run-arm.sh:56</c>). The fixtures are those lines byte for byte with the arm's token replaced by <c>0123456789ab</c>; the sources and
/// the sha256 of the verbatim lines are in <c>ClaudeResultEnvelopeTests</c>.
/// <para>
/// What this does NOT prove: that claude prints a result before the wall kills it, or what a real 529 looks like — the 529 line is SYNTHETIC (the 429 line with another status) and
/// says so. Nor that the words list is complete: it is the shell's list, and an account-out message in other words would be an <c>Errored</c> until the list learns it.
/// </para></summary>
public sealed class StepEndTests
{
    private const string LimitText =
        "You've hit your individual spend limit · run /usage-credits to ask your admin for a higher limit · your session limit resets 1:20am (Europe/Paris)";

    private const string Interrupted = "no result line and no limit — the process ended without saying how";

    private const string FixLimitedHead = "stream-2026-10-28-fix-limited-head.jsonl";

    private static readonly AttemptFacts Killed = new(string.Empty, true, false);

    private static readonly AttemptFacts Stopped = new(string.Empty, false, true);

    private static string Limit429 => Read("result-429-usage-limit-reached.jsonl");

    private static string NoResult => Read("stream-no-result.jsonl");

    private static string Untrusted => Read("err-2026-10-28-untrusted-workspace.txt");

    private static string Read(string name) => ModelArmsFixtures.Read(name);

    private static string Sha(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static string LineOf(string text, int n) => text.Split('\n')[n - 1] + "\n";

    private static StepEnd End(string stream, string stderr = "", AttemptFacts? attempt = null) =>
        StepClassifier.Classify(ClaudeResultEnvelope.LastOf(stream), stderr, attempt ?? AttemptFacts.Ran);

    /// <summary>The verbatim line with ONE fact changed or removed — the only way to vary a recorded line without retyping it.</summary>
    private static string Edited(string line, Action<JsonObject> edit)
    {
        var node = JsonNode.Parse(line)!.AsObject();
        edit(node);
        return node.ToJsonString();
    }

    private static string ErrorResult(string result) =>
        "{\"type\":\"result\",\"is_error\":true,\"result\":" + JsonSerializer.Serialize(result) + "}";

    public static TheoryData<string> Successes =>
    [
        Read("result-success.jsonl"),
        """{"type":"result","subtype":"success","is_error":false,"result":"{\"findings\":[{\"title\":\"a spend limit is not handled\"}]}"}""",
    ];

    private static readonly (StepEnd End, string Word, bool Done)[] EndRows =
    [
        (new StepEnd.Finished(), "finished", true),
        (new StepEnd.HardDeadline(), "hard-deadline", true),
        (new StepEnd.Limited("x"), "limited", false),
        (new StepEnd.Errored("x"), "errored", false),
        (new StepEnd.Interrupted("x"), "interrupted", false),
        (new StepEnd.NotStarted("x"), "not-started", false),
    ];

    public static TheoryData<StepEnd, string, bool> Ends
    {
        get
        {
            var data = new TheoryData<StepEnd, string, bool>();
            foreach (var (end, word, done) in EndRows)
            {
                data.Add(end, word, done);
            }

            return data;
        }
    }

    [Fact]
    public void The_2026_10_28_line_is_limited_by_its_structured_fields_whatever_the_words_say()
    {
        var verbatim = End(Limit429);
        verbatim.Should().Be(new StepEnd.Limited("api_error_status 429 — " + LimitText));
        var reason = verbatim.Should().BeOfType<StepEnd.Limited>().Subject.Reason;
        reason.Should().StartWith("api_error_status 429", "the structured fields decide before the words");

        End(Edited(Limit429, o =>
        {
            o["result"] = "Request failed.";
            o.Remove("api_error");
        })).Should().Be(new StepEnd.Limited("api_error_status 429 — Request failed."));

        End(Edited(Limit429, o =>
        {
            o.Remove("api_error_status");
            o["result"] = "Request failed.";
        })).Should().Be(new StepEnd.Limited("api_error 'usage_limit_reached' — Request failed."));

        End(Edited(Limit429, o =>
        {
            o.Remove("api_error_status");
            o.Remove("api_error");
        })).Should().Be(new StepEnd.Limited("the words 'hit your individual spend limit' in the result — " + LimitText));

        End(Edited(Limit429, o =>
        {
            o.Remove("api_error_status");
            o.Remove("api_error");
            o["result"] = "Request failed.";
        })).Should().Be(new StepEnd.Errored("Request failed."), "nothing structured and no words is not a limit");
    }

    [Fact]
    public void A_monthly_spend_limit_429_is_limited()
    {
        var end = End(Read("result-429-monthly-spend-limit.jsonl"));

        end.Should().Be(new StepEnd.Limited("api_error_status 429 — You've hit your monthly spend limit. Switch to another model to continue."));
        end.IsDone.Should().BeFalse();
    }

    [Fact]
    public void A_string_429_is_limited()
    {
        var line = Read("result-429-string-status.jsonl");

        End(line).Should().Be(new StepEnd.Limited("api_error_status 429 — You've hit your monthly spend limit."));
        End(Edited(line, o => o["result"] = "Request failed.")).Should().Be(
            new StepEnd.Limited("api_error_status 429 — Request failed."), "the string status alone decides; the words are not needed");
    }

    /// <summary>SYNTHETIC: no real 529 was recorded. It is the recorded 429 line with the status and the text of an overload.</summary>
    [Fact]
    public void A_529_overload_is_limited()
    {
        var line = Edited(Limit429, o =>
        {
            o["api_error_status"] = 529;
            o.Remove("api_error");
            o["result"] = "API Error: 529 Overloaded";
        });

        End(line).Should().Be(new StepEnd.Limited("api_error_status 529 — API Error: 529 Overloaded"));
        End(Edited(line, o => o.Remove("api_error_status"))).Should().Be(new StepEnd.Limited("the words '529' in the result — API Error: 529 Overloaded"), "the leftmost match is reported");
    }

    [Fact]
    public void An_error_result_that_is_not_a_limit_is_errored_never_finished()
    {
        var end = End(Read("result-400-model-unsupported.jsonl"));

        end.Should().Be(new StepEnd.Errored(
            "API Error: 400 Claude Code 2.1.258 does not support this model; version 2.1.280 or newer is required. Run 'claude update', or update the Claude desktop app, then try again."));
        end.IsDone.Should().BeFalse();
        end.Should().NotBeOfType<StepEnd.Finished>("an error result is never done — the repair script's rule, repair-limit-2026-10-28.sh:4");
    }

    [Fact]
    public void Limit_words_on_stderr_without_a_result_are_limited()
    {
        End(NoResult, "Claude AI usage limit reached. Your limit will reset at 1am (Europe/Paris).\n").Should().Be(new StepEnd.Limited("the words 'usage limit' on stderr"));
        End(NoResult, "API Error: 529 Overloaded").Should().Be(new StepEnd.Limited("the words '529' on stderr"));
    }

    [Fact]
    public void The_stderr_tail_is_the_last_4000_characters()
    {
        StepClassifier.StderrTailCharacters.Should().Be(4000);

        End(NoResult, "rate limit\n" + new string('x', 4000)).Should().Be(new StepEnd.Interrupted(Interrupted), "the words sit before the tail — the shell's slice(-4000)");
        End(NoResult, new string('x', 4000) + "\nrate limit").Should().Be(new StepEnd.Limited("the words 'rate limit' on stderr"));
    }

    [Theory]
    [MemberData(nameof(Successes))]
    public void A_success_result_is_finished(string stream)
    {
        var end = End(stream);

        end.Should().Be(new StepEnd.Finished());
        end.IsDone.Should().BeTrue();
        end.Word.Should().Be("finished");
    }

    [Fact]
    public void A_killed_tree_is_the_hard_deadline()
    {
        var end = End(NoResult, string.Empty, Killed);

        end.Should().Be(new StepEnd.HardDeadline());
        end.IsDone.Should().BeTrue();
        end.Word.Should().Be("hard-deadline");
    }

    [Fact]
    public void No_result_and_no_limit_is_interrupted()
    {
        var end = End(NoResult, Untrusted);

        end.Should().Be(new StepEnd.Interrupted(Interrupted));
        end.IsDone.Should().BeFalse();
    }

    [Fact]
    public void A_launch_the_executable_refused_is_not_started()
    {
        const string reason = "the executable /x/bin/claude does not exist";
        var refused = new AttemptFacts(reason, false, false);
        var none = Outcome<ClaudeResultEnvelope>.Failure(reason);

        var end = StepClassifier.Classify(none, string.Empty, refused);

        end.Should().Be(new StepEnd.NotStarted(reason));
        end.Word.Should().Be("not-started");
        StepClassifier.Classify(none, string.Empty, refused with { WallKilled = true }).Should().Be(new StepEnd.NotStarted(reason), "a launch that was refused wins over every other fact");
        AttemptFacts.Ran.Started.Should().BeTrue();
        refused.Started.Should().BeFalse();
    }

    /// <summary>D8's measured case: C2's review wrote its verdict and then hit its deadline — a session that printed its own end has ended.</summary>
    [Fact]
    public void A_result_printed_before_the_wall_killed_the_tree_is_finished()
    {
        End(Read("result-success.jsonl"), string.Empty, Killed).Should().Be(new StepEnd.Finished());
        End(Limit429, string.Empty, Killed).Should().Be(new StepEnd.Limited("api_error_status 429 — " + LimitText), "a limit result is a limit, not a deadline");
        End(Read("result-400-model-unsupported.jsonl"), string.Empty, Stopped).Should().BeOfType<StepEnd.Errored>("an error result is an error, not an interruption");
    }

    [Fact]
    public void A_success_result_is_finished_whatever_stderr_says()
    {
        End(Read("result-success.jsonl"), "Claude AI usage limit reached").Should().Be(new StepEnd.Finished());
        End(Read("result-400-model-unsupported.jsonl"), "Claude AI usage limit reached").Should().BeOfType<StepEnd.Errored>("stderr is read only when there is no result (run-arm.sh:60-62)");
    }

    [Fact]
    public void The_real_untrusted_workspace_warning_on_stderr_is_not_a_limit()
    {
        LimitWords.FirstIn(Untrusted).Should().BeEmpty();
        End(NoResult, Untrusted).Should().Be(new StepEnd.Interrupted(Interrupted));
        End(NoResult, Untrusted, Killed).Should().Be(new StepEnd.HardDeadline());
        Untrusted.Should().Contain("projects[\"/home/jinx/git/coai-exp2/r/0123456789ab/arm\"]", "the token was replaced");
    }

    [Theory]
    [InlineData("usage limit reached for this org", "usage limit")]
    [InlineData("usage_limit_reached", "usage_limit")]
    [InlineData("You've hit your monthly spend limit.", "hit your monthly spend limit")]
    [InlineData("session limit resets 1:20am", "session limit")]
    [InlineData("rate limit exceeded", "rate limit")]
    [InlineData("rate_limit", "rate_limit")]
    [InlineData("hit your individual spend limit", "hit your individual spend limit")]
    [InlineData("limit reached", "limit reached")]
    [InlineData("Your limit will reset at 3pm", "limit will reset")]
    [InlineData("resets at 1:20am", "resets at")]
    [InlineData("reset at 1:20am", "reset at")]
    [InlineData("API Error: 529 Overloaded", "529")]
    [InlineData("Overloaded", "Overloaded")]
    [InlineData("HTTP 429", "429")]
    [InlineData("Too Many Requests", "Too Many Requests")]
    [InlineData("API Error: 400 Claude Code 2.1.258 does not support this model; version 2.1.280 or newer is required. Run 'claude update', or update the Claude desktop app, then try again.", "")]
    [InlineData("Authentication failed. Run claude login and retry.", "")]
    [InlineData("error 4290 of 5290", "")]
    [InlineData("an unlimited retry", "")]
    [InlineData("", "")]
    public void Every_word_of_the_shells_limit_pattern_is_a_limit_and_a_plain_error_is_not(string text, string match)
    {
        var first = LimitWords.FirstIn(text);

        first.Should().Be(match, "the matched text, in its original case");

        End(ErrorResult(text)).Should().Be(match.Length > 0
            ? new StepEnd.Limited("the words '" + match + "' in the result — " + text)
            : new StepEnd.Errored(text.Length > 0 ? text : "an error result with no text"));
    }

    [Fact]
    public void A_stop_by_the_operator_without_a_result_is_interrupted()
    {
        End(NoResult, string.Empty, Stopped).Should().Be(new StepEnd.Interrupted("stopped by the operator"));
        End(NoResult, string.Empty, new AttemptFacts(string.Empty, true, true)).Should().Be(new StepEnd.HardDeadline(), "a tree the wall killed is on its deadline whoever also pressed Ctrl+C");
    }

    /// <summary>The epic's Definition of Done: the 2026-10-28 review attempt, read by its last result and never finished by its first.</summary>
    [Fact]
    public void The_2026_10_28_review_attempt_is_limited_by_its_last_result_never_finished_by_its_first()
    {
        const string file = "stream-2026-10-28-review-three-results.jsonl";
        var stream = Read(file);

        var end = End(stream);

        end.Should().Be(new StepEnd.Limited("api_error_status 429 — " + LimitText));
        end.IsDone.Should().BeFalse();
        End(LineOf(stream, 1)).Should().Be(new StepEnd.Finished(), "what the day's detector saw, and what timing.jsonl recorded as result true, limited false: the first result would have marked the step done");
    }

    /// <summary>Criterion 4: the <c>rate_limit_event</c> and the synthetic assistant line (which carries <c>api_error_status</c> 429 at ITS top level) are facts for the record, not inputs.</summary>
    [Fact]
    public void The_limited_fix_stream_is_read_from_its_result_line_and_never_from_the_rate_limit_event()
    {
        var stream = Read(FixLimitedHead);
        Sha(LineOf(stream, 2)).Should().Be("1ce54cee01e1ad8e934fbb02d38409e4b31b1d8ea1a5a87eed295b1f8e61c03e");
        Sha(LineOf(stream, 3)).Should().Be("5a2a200b22b52aadb7f8658eeb5e33ca6ad9e472cc024836e66a463213677395");
        Sha(LineOf(stream, 4)).Should().Be("895d7980445470e7ad4588cbdea7a0b7878b47582544b58b82d008c59df52a52");

        var expected = new StepEnd.Limited("api_error_status 429 — " + LimitText);
        ClaudeResultEnvelope.LastOf(stream).Ok().LineNumber.Should().Be(4);
        End(stream).Should().Be(expected);

        var lines = stream.Split('\n');
        End(string.Join('\n', lines.Where((_, i) => i != 1))).Should().Be(expected, "without the event the result decides the same");

        var withoutResult = string.Join('\n', lines[..3]);
        ClaudeResultEnvelope.LastOf(withoutResult).Failed().Should().BeTrue();
        End(withoutResult).Should().Be(new StepEnd.Interrupted(Interrupted), "the event and the assistant line are not inputs");
    }

    [Theory]
    [MemberData(nameof(Ends))]
    public void Every_end_has_its_word_and_only_finished_and_hard_deadline_are_done(StepEnd end, string word, bool done)
    {
        end.Word.Should().Be(word);
        end.IsDone.Should().Be(done);

        EndRows.Select(row => row.End.Word).Distinct().Should().HaveCount(6, "the six words are the spelling S2 writes into .done and the end line");
    }

    [Fact]
    public void An_errored_ends_text_is_the_results_first_200_characters_or_says_there_is_none()
    {
        var head = new string('a', 200) + "…";

        End(ErrorResult(new string('a', 300))).Should().Be(new StepEnd.Errored(head));
        End("""{"type":"result","is_error":true}""").Should().Be(new StepEnd.Errored("an error result with no text"));
        End(ErrorResult("   ")).Should().Be(new StepEnd.Errored("an error result with no text"));
        End("""{"type":"result","is_error":true,"result":42}""").Should().Be(new StepEnd.Errored("an error result with no text"));
        End(Edited(Limit429, o => o["result"] = new string('a', 300))).Should().Be(new StepEnd.Limited("api_error_status 429 — " + head), "the same head on a limit's reason");
    }
}
