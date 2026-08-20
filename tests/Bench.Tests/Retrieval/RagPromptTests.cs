using Bench.Application;
using Bench.Domain.Retrieval;
using Bench.Domain.Suites;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Retrieval;

/// <summary>The single-shot RAG prompt. It is stored on the result and never regenerated, so it is the
/// artefact: whatever it claims, the model read.</summary>
public sealed class RagPromptTests
{
    private static readonly Question Question = new("q", "How is the retry delay computed?", [], string.Empty);

    [Fact]
    public void The_baseline_arm_gets_the_question_and_nothing_else()
    {
        var prompt = RagPrompt.Assemble(Question, RetrievedContext.NotPerformed, RagPromptLimits.Default);

        // A control that quietly acquired a preamble is no longer the control every retrieval claim is
        // measured against.
        prompt.Should().Be(Question.Prompt);
    }

    [Fact]
    public void Excerpts_arrive_in_rank_order_with_the_place_a_subject_would_go_and_read()
    {
        var prompt = RagPrompt.Assemble(
            Question,
            Context(
                Hit(1, "src/A.cs", 10, 20, "A.First", "first body"),
                Hit(2, "src/B.cs", 30, 40, "B.Second", "second body")),
            RagPromptLimits.Default);

        prompt.Should().Contain("src/A.cs:10-20").And.Contain("A.First").And.Contain("first body");
        prompt.IndexOf("src/A.cs", StringComparison.Ordinal)
            .Should().BeLessThan(prompt.IndexOf("src/B.cs", StringComparison.Ordinal), "rank order IS the measurement");
    }

    [Fact]
    public void The_same_context_always_produces_the_same_prompt()
    {
        var context = Context(Hit(1, "src/A.cs", 10, 20, "A.First", "body"));

        RagPrompt.Assemble(Question, context, RagPromptLimits.Default)
            .Should().Be(RagPrompt.Assemble(Question, context, RagPromptLimits.Default),
                "repeats only mean something if the same cell produces the same prompt");
    }

    [Fact]
    public void A_search_that_returned_nothing_says_so_in_the_prompt()
    {
        var prompt = RagPrompt.Assemble(Question, Context(), RagPromptLimits.Default);

        // Without the sentence, a subject handed an empty context block cannot tell an empty index from a
        // question the search declined to answer.
        prompt.Should().Contain("returned no results");
    }

    [Fact]
    public void A_hit_with_no_source_text_shows_its_place_and_says_the_text_was_not_returned()
    {
        var prompt = RagPrompt.Assemble(
            Question,
            Context(new RetrievedHit(
                1, "src/A.cs", 10, 20, "A.First", "engine|key", "public void First()", 0.9, "rerank", ["dense"], [1],
                HitSnippet.NotReported("the engine's hit carried an empty text field"))),
            RagPromptLimits.Default);

        // Path, span and signature are exactly what a subject needs to go and read it; an empty fenced block
        // would instead read as a file that is empty.
        prompt.Should().Contain("src/A.cs:10-20").And.Contain("public void First()");
        prompt.Should().Contain("no source text").And.Contain("empty text field");
    }

    [Fact]
    public void A_truncated_excerpt_SAYS_it_was_truncated_and_by_how_much()
    {
        var prompt = RagPrompt.Assemble(
            Question,
            Context(Hit(1, "src/A.cs", 1, 900, "A.Huge", new string('x', 3000))),
            new RagPromptLimits(SnippetChars: 100));

        // The stored prompt is what the model read, so it has to describe what the model read.
        prompt.Should().Contain("2900 more character(s)");
        prompt.Should().NotContain(new string('x', 200));
    }

    [Fact]
    public void Every_hit_the_engine_returned_reaches_the_prompt()
    {
        var hits = Enumerable.Range(1, 20)
            .Select(i => Hit(i, $"src/F{i}.cs", i, i + 5, $"F{i}.M", $"body {i}"))
            .ToArray();

        var prompt = RagPrompt.Assemble(Question, Context(hits), RagPromptLimits.Default);

        // How many results a subject sees is the variant's `limit` axis — a catalog row with a hash. A second
        // cap here would be an unnamed axis applied to every arm, and the run would report the recipe it
        // asked for while feeding the model something else.
        foreach (var hit in hits)
        {
            prompt.Should().Contain(hit.RelativePath);
        }
    }

    [Fact]
    public void The_question_comes_last_so_the_instruction_is_what_the_model_ends_on()
    {
        var prompt = RagPrompt.Assemble(
            Question, Context(Hit(1, "src/A.cs", 10, 20, "A.First", "body")), RagPromptLimits.Default);

        prompt.TrimEnd().Should().EndWith(Question.Prompt);
        prompt.Should().Contain("may be incomplete", "a subject told the excerpts are authoritative will invent from them");
    }

    private static RetrievedHit Hit(int rank, string path, int start, int end, string member, string text) =>
        new(rank, path, start, end, member, $"engine|{member}", $"public void {member}()", 0.9, "rerank", ["dense"], [1],
            HitSnippet.Text(text));

    private static RetrievedContext Context(params RetrievedHit[] hits) =>
        RetrievedContext.Of("code_x", hits, RetrievalFunnel.None, string.Empty, EngineAxes.None, EngineAxes.None, EngineAxes.None, 0, 0);
}
