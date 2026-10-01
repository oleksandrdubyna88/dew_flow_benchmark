using Bench.Domain.Probes;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Probes;

/// <summary>S2 — the ANSWER a CLI gave, read off each grammar's transcript: the verdict's canary check runs over what the model
/// SAID, never over the whole transcript, because a codex <c>command_execution</c> item or an agy tool step can carry a
/// file's bytes the model never put in its answer. Pinned on the LIVE transcripts of 2026-10-01 (S2b): claude's <c>result</c>
/// envelope, codex's <c>agent_message</c> items, agy's <c>result.response</c>. A transcript that is not the grammar at all is its
/// own answer — a CLI that printed plain text still answered.</summary>
public sealed class ProbeAnswerTests
{
    [Theory]
    [InlineData(ProbeRuntime.Claude, "claude-2.1.258-denylist-web-search.ndjson", "The latest published version of `@openai/codex` is **0.159.3**.")]
    [InlineData(ProbeRuntime.Claude, "claude-2.1.258-denylist-read-denied.ndjson", "The Read tool is disabled for this session")]
    [InlineData(ProbeRuntime.Claude, "claude-2.1.258-json-read-denied.json", "OUT-5d39fc027929")]
    [InlineData(ProbeRuntime.Codex, "codex-0.156.1-web-search.jsonl", "I’ll check the npm registry directly to confirm the currently published version.\nLatest `@openai/codex` version: **0.159.3**.")]
    [InlineData(ProbeRuntime.Codex, "codex-0.156.1-read-outside-bare.jsonl", "OUT-3dd3c2e23ce7")]
    [InlineData(ProbeRuntime.Antigravity, "agy-1.2.14-read-inside.ndjson", "IN-d5fe02153e26")]
    public void Each_grammar_reader_extracts_the_final_message_as_the_answer(ProbeRuntime runtime, string fixture, string start) =>
        ProbeTranscripts.Answer(runtime, ProbeVerdictsTests.Fixture(fixture)).Should().StartWith(start);

    [Fact]
    public void The_answer_never_includes_a_tool_result_or_a_tool_input_the_model_did_not_repeat()
    {
        ProbeTranscripts.Answer(ProbeRuntime.Claude, ProbeVerdictsTests.Fixture("claude-2.1.258-denylist-web-search.ndjson"))
            .Should().NotContain("Web search results for query", "that is the tool's output, not the model's answer");
        ProbeTranscripts.Answer(ProbeRuntime.Codex, ProbeVerdictsTests.Fixture("codex-0.156.1-read-outside-bare.jsonl"))
            .Should().Be("OUT-3dd3c2e23ce7", "the agent_message alone — not the pwsh command, not its aggregated_output with its line endings");
        ProbeTranscripts.Answer(ProbeRuntime.Antigravity, ProbeVerdictsTests.Fixture("agy-1.2.14-web-search.ndjson"))
            .Should().BeEmpty("headless agy auto-denied read_url and answered nothing — the search_web step's query is not the answer");
    }

    [Theory]
    [InlineData(ProbeRuntime.Claude)]
    [InlineData(ProbeRuntime.Codex)]
    [InlineData(ProbeRuntime.Antigravity)]
    public void A_transcript_that_is_not_the_grammar_is_its_own_answer_and_a_grammar_with_no_message_answers_nothing(ProbeRuntime runtime)
    {
        ProbeTranscripts.Answer(runtime, "  The file says IN-7f3a9c2e1b.  \n").Should().Be("The file says IN-7f3a9c2e1b.", "a plain-text answer is still an answer");
        ProbeTranscripts.Answer(runtime, string.Empty).Should().BeEmpty();
        ProbeTranscripts.Answer(runtime, """{"type":"system","subtype":"init","tools":[]}""" + "\n").Should().BeEmpty("a JSON line is the grammar — one that carries no message answered nothing");
    }
}
