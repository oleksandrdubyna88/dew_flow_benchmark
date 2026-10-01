using Bench.Domain.Probes;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Probes;

/// <summary>S2 — the ANSWER a CLI gave, read off each grammar's transcript: the verdict's canary check runs over what the model
/// SAID, never over the whole transcript, because a codex <c>command_execution</c> item or an agy <c>tool_result</c> can carry a
/// file's bytes the model never put in its answer. A transcript that is not the grammar at all is its own answer — a CLI that
/// printed plain text still answered.</summary>
public sealed class ProbeAnswerTests
{
    [Theory]
    [InlineData(ProbeRuntime.Claude, "claude-web-search.json", "The latest published version of @openai/codex on npm is 0.52.0. I read it from https://www.npmjs.com/package/@openai/codex.")]
    [InlineData(ProbeRuntime.Codex, "codex-web-search.jsonl", "The latest @openai/codex on npm is 0.52.0 (https://www.npmjs.com/package/@openai/codex).")]
    [InlineData(ProbeRuntime.Antigravity, "agy-web-search.ndjson", "The latest version is 0.52.0, read from https://www.npmjs.com/package/@openai/codex.")]
    [InlineData(ProbeRuntime.Codex, "codex-read-denied.jsonl", "The sandbox refused the read, so I cannot report the file's contents.")]
    [InlineData(ProbeRuntime.Antigravity, "agy-read-denied.ndjson", "I am not allowed to read that file, so I cannot show its contents.")]
    public void Each_grammar_reader_extracts_the_final_message_as_the_answer(ProbeRuntime runtime, string fixture, string answer) =>
        ProbeTranscripts.Answer(runtime, ProbeVerdictsTests.Fixture(fixture)).Should().Be(answer);

    [Fact]
    public void The_answer_never_includes_a_tool_result_the_model_did_not_repeat()
    {
        var codex = ProbeVerdictsTests.Fixture("codex-read-denied.jsonl");

        ProbeTranscripts.Answer(ProbeRuntime.Codex, codex).Should().NotContain("Operation not permitted", "that is the command's output, not the model's answer");
        ProbeTranscripts.Answer(ProbeRuntime.Antigravity, ProbeVerdictsTests.Fixture("agy-read-denied.ndjson")).Should().NotContain("not allowed in this session");
        ProbeTranscripts.Answer(ProbeRuntime.Antigravity, ProbeVerdictsTests.Fixture("agy-read-denied.ndjson")).Should().NotContain("Print the contents", "the user's own prompt is not the answer");
    }

    [Theory]
    [InlineData(ProbeRuntime.Claude)]
    [InlineData(ProbeRuntime.Codex)]
    [InlineData(ProbeRuntime.Antigravity)]
    public void A_transcript_that_is_not_the_grammar_is_its_own_answer_and_a_grammar_with_no_message_answers_nothing(ProbeRuntime runtime)
    {
        ProbeTranscripts.Answer(runtime, "  The file says IN-7f3a9c2e1b.  \n").Should().Be("The file says IN-7f3a9c2e1b.", "a plain-text answer is still an answer");
        ProbeTranscripts.Answer(runtime, string.Empty).Should().BeEmpty();
        ProbeTranscripts.Answer(runtime, ProbeVerdictsTests.Fixture("claude-missing-fields.json").Replace("\"result\": \"The latest version of @openai/codex is 0.52.0.\",", string.Empty, StringComparison.Ordinal))
            .Should().Be(runtime == ProbeRuntime.Claude ? string.Empty : ProbeVerdictsTests.Fixture("claude-missing-fields.json").Replace("\"result\": \"The latest version of @openai/codex is 0.52.0.\",", string.Empty, StringComparison.Ordinal).Trim(),
                "claude's grammar parsed and carried no result — nothing was said; the other readers see no line of theirs and hand the text back whole");
    }
}
