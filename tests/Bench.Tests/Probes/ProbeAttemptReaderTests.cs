using Bench.Application;
using Bench.Domain.Probes;
using Bench.Domain.Trace;
using Bench.Infrastructure.Probes;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Probes;

/// <summary>S2c, review finding 5: the quota reading used to scan the WHOLE stdout on a non-zero exit or a timeout — so a web page quoting
/// "You've hit your usage limit" inside a tool result could bench a subject. The reading sees stderr, the answer and the CLI's OWN
/// result/error envelope (claude's <c>result</c> event, codex's <c>error</c>/<c>turn.failed</c>, agy's <c>result</c>/<c>error</c>) — never a
/// tool result or fetched content.</summary>
public sealed class ProbeAttemptReaderTests
{
    private static readonly ProbeSubject Claude = ProbeStoreFixtures.Subject("claude-sonnet", "claude");
    private static readonly ProbeRun Run = ProbeStoreFixtures.Run(Claude);
    private static readonly ProbeTokens Tokens = ProbeTokens.Of("IN-7f3a9c2e1b", "OUT-4d8e6f0a2c").Ok();

    [Fact]
    public void A_quota_sentence_inside_a_tool_result_never_benches_the_subject_even_on_a_non_zero_exit_or_a_timeout()
    {
        var quoted = WithToolResult(ProbeVerdictsTests.Fixture("claude-2.1.258-denylist-web-search.ndjson"),
            "Forum post: \"You've hit your usage limit. Visit https://chatgpt.com/codex/settings/usage to purchase more credits.\" — Claude AI usage limit reached|1759340000 is the other CLI's wording");

        var failed = ProbeAttemptReader.Live.Read(Run, Claude, ProbeKind.WebSearch, Tokens, new AgentTranscript(CapturedCount.Number(1), false, quoted, string.Empty, TimeSpan.FromSeconds(3)));
        var timedOut = ProbeAttemptReader.Live.Read(Run, Claude, ProbeKind.WebSearch, Tokens, new AgentTranscript(CapturedCount.Unavailable("the wall"), true, quoted, string.Empty, TimeSpan.FromMinutes(5)));

        failed.QuotaLine.Should().BeEmpty("a fetched page is not the CLI's own voice — the stream's tool results are never scanned");
        timedOut.QuotaLine.Should().BeEmpty();
    }

    [Fact]
    public void A_quota_sentence_in_the_clis_own_envelope_or_on_stderr_still_benches_the_subject()
    {
        var envelope = ProbeVerdictsTests.Fixture("claude-2.1.258-denylist-read-denied.ndjson")
            .Replace("\"result\":\"The Read tool is disabled for this session", "\"result\":\"Claude AI usage limit reached|1759340000 — The Read tool is disabled for this session", StringComparison.Ordinal);

        ProbeAttemptReader.Live.Read(Run, Claude, ProbeKind.ReadDenied, Tokens, new AgentTranscript(CapturedCount.Number(1), false, envelope, string.Empty, TimeSpan.FromSeconds(3))).QuotaLine
            .Should().Contain("usage limit reached", "the result event is the CLI's own envelope");
        ProbeAttemptReader.Live.Read(Run, Claude, ProbeKind.ReadDenied, Tokens, new AgentTranscript(CapturedCount.Number(1), false, string.Empty, "Claude AI usage limit reached|1759340000", TimeSpan.FromSeconds(1))).QuotaLine
            .Should().Contain("usage limit reached", "stderr is always read");
        ProbeAttemptReader.Live.Read(Run, Claude, ProbeKind.ReadDenied, Tokens, new AgentTranscript(CapturedCount.Number(1), false, "You've hit your usage limit.\n", string.Empty, TimeSpan.FromSeconds(1))).QuotaLine
            .Should().Contain("usage limit", "a transcript that is not the grammar at all is the CLI's own voice, whole");
    }

    /// <summary>A <c>user</c> event with one tool result, in the live grammar's shape, inserted before the stream's final event.</summary>
    private static string WithToolResult(string stream, string content)
    {
        var lines = stream.Split('\n').Where(l => l.Trim().Length > 0).ToList();
        var result = "{\"type\":\"user\",\"message\":{\"role\":\"user\",\"content\":[{\"tool_use_id\":\"toolu_01Quoted\",\"type\":\"tool_result\",\"content\":"
                     + System.Text.Json.JsonSerializer.Serialize(content) + ",\"is_error\":false}]},\"session_id\":\"s\"}";

        return string.Join('\n', [.. lines.Take(lines.Count - 1), result, lines[^1]]) + "\n";
    }
}
