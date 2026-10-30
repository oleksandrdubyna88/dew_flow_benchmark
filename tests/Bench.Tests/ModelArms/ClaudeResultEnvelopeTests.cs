using System.Security.Cryptography;
using System.Text;
using Bench.Domain.Trace;
using FluentAssertions;
using ModelArms.Core.Launch;
using ModelArms.Core.Steps;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The reader of a <c>stream-json</c> attempt: the LAST result line, by its <c>type</c> wherever the key sits, never by a prefix and never trusting the first. Pinned on the
/// lines recorded on 2026-10-28 in the part-6 experiment's first arm (<c>3-fix.a1.jsonl</c> lines 6-8, <c>2-review.a1.jsonl</c> lines 310, 317 and 323; each fixture is those lines
/// byte for byte with the arm's token replaced by <c>0123456789ab</c> — the same length, so the JSON stays valid — and a line that holds no token is committed unchanged; the sha256 of every
/// verbatim line is asserted below) and on the product's envelope fixtures (<c>src_mcp/tests/AClaudeReasonInItsEnvelopeTests.cs</c> at 80b829ef, lines 23, 27 and 87).
/// <para>
/// What this does NOT prove: that claude 2.1.x still prints these shapes (the lines are a record, not a contract), and anything about a stream's non-result lines — the reader skips them.
/// </para></summary>
public sealed class ClaudeResultEnvelopeTests
{
    private const string ThreeResults = "stream-2026-10-28-review-three-results.jsonl";

    private const string Limit429 = "result-429-usage-limit-reached.jsonl";

    private static string Read(string name) => ModelArmsFixtures.Read(name);

    /// <summary>The one result line of <c>result-success.jsonl</c>.</summary>
    private static string Success => Read("result-success.jsonl").Split('\n')[2];

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>The n-th line (1-based) of the fixture's BYTES, with its newline: the verbatim guard reads bytes, never a re-encoded text.</summary>
    private static byte[] Line(string file, int n)
    {
        var bytes = File.ReadAllBytes(ModelArmsFixtures.Path(file));
        var lines = new List<byte[]>();
        var start = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == 0x0A)
            {
                lines.Add(bytes[start..(i + 1)]);
                start = i + 1;
            }
        }

        return lines[n - 1];
    }

    private static string LineText(string file, int n) => Encoding.UTF8.GetString(Line(file, n));

    /// <summary>The 2026-10-28 review attempt (2-review.a1.jsonl): a first result that says the reviewer is waiting for the MCP suite (a success), then two limit results. The detector of
    /// that day read the first — the shape of the incident.</summary>
    [Fact]
    public void The_last_result_line_is_read_whatever_lines_precede_it()
    {
        var stream = Read(ThreeResults);

        var envelope = ClaudeResultEnvelope.LastOf(stream).Ok();

        envelope.LineNumber.Should().Be(3, "the third line is the last result; the first is a success that came before the limit");
        envelope.IsError.Should().BeTrue();
        envelope.ApiErrorStatus.Should().Be(CapturedCount.Number(429));
        envelope.ApiError.Should().Be("usage_limit_reached");
        envelope.Result.Should().StartWith("You've hit your individual spend limit");
        envelope.Subtype.Should().Be("success");
        envelope.Errors.Should().BeEmpty();

        var first = ClaudeResultEnvelope.LastOf(LineText(ThreeResults, 1)).Ok();

        first.LineNumber.Should().Be(1);
        first.IsError.Should().BeFalse();
        first.Result.Should().Be("I'm waiting for the full MCP suite. When it finishes I will send the verdict.");
        first.ApiErrorStatus.WasCaptured.Should().BeFalse("the recorded success line carries \"api_error_status\":null");

        // The bytes are the record: lines 2 and 3 hold no token and are committed byte for byte; line 1 holds the token's place.
        Sha(Line(ThreeResults, 2)).Should().Be("20d59284b73beaba53c2127b6dba5e18738813edace799f290a7b8c522551dd8");
        Sha(Line(ThreeResults, 3)).Should().Be("3087925f6188b168051cda9f95d59d14160b7e5f2c3262524bf870fec715d494");
        LineText(ThreeResults, 1).Should().Contain("/r/0123456789ab/arm", "the token was replaced and the line still parses");
    }

    /// <summary>The product's 2.1.284 envelope carries the status as a number (line 23); a string status is the product's own fixture too (line 87).</summary>
    [Fact]
    public void Api_error_status_reads_as_a_number_and_as_a_string()
    {
        var read = new[] { "result-429-monthly-spend-limit.jsonl", "result-429-string-status.jsonl", "result-400-model-unsupported.jsonl" }
            .Select(file => ClaudeResultEnvelope.LastOf(Read(file)).Ok()).ToList();

        var captured = read.Select(envelope => envelope.ApiErrorStatus.WasCaptured).ToList();
        captured.Should().OnlyContain(wasCaptured => wasCaptured, "the product's 2.1.284 envelope carries the status as a number, and its string form is read as one");

        read.Select(envelope => envelope.ApiErrorStatus).Should().Equal(CapturedCount.Number(429), CapturedCount.Number(429), CapturedCount.Number(400));

        foreach (var raw in new[] { "\"4a9\"", "429.5", "null", "\"\"", "true", "\"1234567890123456789\"", "absent" })
        {
            var line = raw == "absent"
                ? """{"type":"result","is_error":true,"result":"x"}"""
                : "{\"type\":\"result\",\"is_error\":true,\"api_error_status\":" + raw + ",\"result\":\"x\"}";

            var status = ClaudeResultEnvelope.LastOf(line).Ok().ApiErrorStatus;

            status.WasCaptured.Should().BeFalse($"{raw} is not a status number and must never read as 0");
            status.Reason.Should().Contain("api_error_status", $"the reason for {raw} names the field");
        }
    }

    /// <summary>A real 429 prints <c>"subtype":"success"</c> beside <c>"is_error":true</c> (the product's lesson, AClaudeReasonInItsEnvelopeTests.cs:17-18): is_error decides.</summary>
    [Fact]
    public void Subtype_success_on_an_error_result_is_ignored()
    {
        var limited = ClaudeResultEnvelope.LastOf(Read(Limit429));
        var errored = ClaudeResultEnvelope.LastOf(Read("result-400-model-unsupported.jsonl"));

        var both = new[] { limited.Ok(), errored.Ok() };
        both.Select(envelope => envelope.IsError).Should().OnlyContain(isError => isError, "subtype \"success\" sits beside is_error true on a real 429");
        both.Select(envelope => envelope.Subtype).Should().OnlyContain(subtype => subtype == "success");

        StepClassifier.Classify(limited, string.Empty, AttemptFacts.Ran).Should().BeOfType<StepEnd.Limited>("subtype is kept and decides nothing");
        StepClassifier.Classify(errored, string.Empty, AttemptFacts.Ran).Should().BeOfType<StepEnd.Errored>("an error result is never finished, whatever its subtype says");
    }

    [Fact]
    public void A_stream_without_a_result_reads_as_none()
    {
        var withoutThird = string.Join('\n', Read("result-success.jsonl").Split('\n')[..2]);

        foreach (var stream in new[] { Read("stream-no-result.jsonl"), string.Empty, "\n\n", withoutThird })
        {
            var read = ClaudeResultEnvelope.LastOf(stream);

            read.Failed().Should().BeTrue("a stream with no result line has no result to read");
            read.Reason().Should().Be("no line of the stream is a result object");
        }
    }

    /// <summary>The recorded 429 line (3-fix.a1.jsonl:8) has its <c>type</c> key 20th of 25, at character 1264 of 1418: a reader that looks at the start of the line never sees it.</summary>
    [Fact]
    public void A_result_whose_type_key_comes_last_is_still_read()
    {
        Read(Limit429).IndexOf("\"type\":\"result\"", StringComparison.Ordinal).Should().BeGreaterThan(1000, "the fixture keeps the property this test needs");

        var envelope = ClaudeResultEnvelope.LastOf(Read(Limit429)).Ok();

        envelope.LineNumber.Should().Be(1);
        envelope.IsError.Should().BeTrue();
        envelope.ApiError.Should().Be("usage_limit_reached");

        ClaudeResultEnvelope.LastOf("""{"is_error":false,"result":"ok","type":"result"}""").Ok().Result.Should().Be("ok");

        // The line is the record, byte for byte (3-fix.a1.jsonl:8, no token in it).
        Sha(File.ReadAllBytes(ModelArmsFixtures.Path(Limit429))).Should().Be("895d7980445470e7ad4588cbdea7a0b7878b47582544b58b82d008c59df52a52");
    }

    /// <summary>Every shape here is a line that was seen, or could be seen, in a real stream: none is a result, and none may hide the real one or throw.</summary>
    [Theory]
    [InlineData("""["is_error",true]""")]
    [InlineData("""{"type":"result","is_error":true,"resu""")]
    [InlineData("Error: something went wrong before any JSON")]
    [InlineData("""{"type":"assistant","message":{}}""")]
    [InlineData("""{"type":42}""")]
    [InlineData("""{"type":null}""")]
    [InlineData("""{"is_error":true,"result":"no type"}""")]
    [InlineData("""{"type":"result_x","result":"a decoy"}""")]
    [InlineData("")]
    [InlineData("   ")]
    public void Every_shape_that_is_not_a_result_object_is_skipped_and_nothing_throws(string shape)
    {
        var before = () => ClaudeResultEnvelope.LastOf(shape + "\n" + Success);
        var after = () => ClaudeResultEnvelope.LastOf(Success + "\n" + shape);
        var alone = () => ClaudeResultEnvelope.LastOf(shape);

        before.Should().NotThrow();
        after.Should().NotThrow();
        alone.Should().NotThrow();

        var envelope = before().Ok();
        envelope.LineNumber.Should().Be(2);
        envelope.Result.Should().Be("Reviewed.\n{\"verdict\":\"ready\",\"findings\":[]}");

        after().Ok().LineNumber.Should().Be(1, "a bad LAST line never hides the real last result");
        alone().Failed().Should().BeTrue("a shape that is not a result is not one alone either");
    }

    /// <summary>The product's codex lesson in this reader: <c>GetString</c> on a number throws <c>InvalidOperationException</c>, not <c>JsonException</c>, and this runs on the path of an
    /// attempt that has ALREADY failed. The last row is well-formed JSON whose string holds a lone surrogate escape: <c>GetString</c> throws on it too, and the reader reads it as its raw text.</summary>
    [Theory]
    [InlineData("""{"type":"result","is_error":true}""", true, "", "", "", "")]
    [InlineData("""{"type":"result","is_error":true,"result":42}""", true, "", "", "", "")]
    [InlineData("""{"type":"result","is_error":"true","result":"a string that says true is not a boolean"}""", false, "a string that says true is not a boolean", "", "", "")]
    [InlineData("""{"type":"result","result":"no is_error at all"}""", false, "no is_error at all", "", "", "")]
    [InlineData("""{"type":"result","is_error":true,"errors":["rate limit exceeded",7,null,{"a":1}]}""", true, "", "", "rate limit exceeded", "the words 'rate limit' in the result — an error result with no text")]
    [InlineData("""{"type":"result","errors":"x","api_error":5}""", false, "", "", "", "")]
    [InlineData("""{"type":"result","is_error":true,"result":"\ud800"}""", true, "\\ud800", "", "", "")]
    public void An_odd_field_in_a_result_object_reads_as_its_safe_default_and_nothing_throws(string line, bool isError, string result, string apiError, string errors, string limitedReason)
    {
        var read = () => ClaudeResultEnvelope.LastOf(line);
        read.Should().NotThrow();

        var envelope = read().Ok();

        envelope.LineNumber.Should().Be(1, "the object is the stream's one line");
        envelope.IsError.Should().Be(isError, "only the JSON literal true is an error");
        envelope.Result.Should().Be(result);
        envelope.ApiError.Should().Be(apiError);
        envelope.Subtype.Should().BeEmpty();
        string[] expectedErrors = errors.Length == 0 ? [] : errors.Split('|');
        envelope.Errors.Should().Equal(expectedErrors);
        envelope.ApiErrorStatus.WasCaptured.Should().BeFalse();

        if (limitedReason.Length > 0)
        {
            StepClassifier.Classify(read(), string.Empty, AttemptFacts.Ran).Should().Be(new StepEnd.Limited(limitedReason), "the errors items join the words");
        }
    }
}
