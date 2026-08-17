using System.Text;
using Bench.Domain.Retrieval;
using Bench.Domain.Suites;

namespace Bench.Application;

/// <summary>How much of one hit's text reaches the prompt.
/// <para>
/// <b>There is deliberately no cap on the NUMBER of hits.</b> How many results a subject sees is the
/// variant's <c>limit</c> axis — a measured knob with a name, a hash and a catalog row. A second cap here
/// would be an unnamed axis applied to every arm at once, which is exactly the "silent cap" this project
/// refuses: a run would report the recipe it asked for and feed the model something else.
/// </para>
/// <para>
/// The per-snippet ceiling is a different thing: it bounds one pathological chunk (a generated file, a
/// 3000-line switch) rather than the recipe, and when it fires the prompt SAYS so — the stored prompt is
/// the artefact, so it must describe what the model actually read.
/// </para></summary>
public sealed record RagPromptLimits(int SnippetChars)
{
    /// <summary>Two thousand characters — a few hundred tokens, enough for any member-sized chunk the
    /// indexer produces, and small enough that one outlier cannot crowd out twenty real hits.</summary>
    public static RagPromptLimits Default { get; } = new(2000);
}

/// <summary>Assembling the single-shot RAG prompt: the question, plus the context that was retrieved for it.
/// <para>
/// Single-shot rather than an agentic loop, per the founding plan — the subject gets one pass over what
/// retrieval surfaced and answers. The loop is future work, and it is a different measurement: this one
/// measures the retrieval, that one measures how an agent works a surface.
/// </para>
/// <para>
/// <b>Deterministic, in rank order.</b> Repeats only mean something if the same cell produces the same
/// prompt, so nothing here iterates a dictionary or formats a time.
/// </para></summary>
public static class RagPrompt
{
    /// <summary>The whole prompt for one leg. A leg whose arm surfaced nothing gets the question alone,
    /// unchanged — a baseline that quietly acquired a preamble would no longer be the control it exists to
    /// be.</summary>
    public static string Assemble(Question question, RetrievedContext context, RagPromptLimits limits) =>
        context.WasPerformed
            ? Grounded(question, context, limits)
            : question.Prompt;

    private static string Grounded(Question question, RetrievedContext context, RagPromptLimits limits)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine(
            "Answer the question below using the retrieved code excerpts. They were found by a search over "
            + "the repository at the commit under test; they may be incomplete, and they may be irrelevant. "
            + "Say so plainly if the excerpts do not contain the answer rather than filling the gap from "
            + "memory.");
        prompt.AppendLine();
        prompt.AppendLine(Heading(context));
        prompt.AppendLine();

        foreach (var hit in context.Hits)
        {
            prompt.AppendLine(Excerpt(hit, limits));
        }

        prompt.AppendLine("## Question");
        prompt.AppendLine();
        prompt.Append(question.Prompt);

        return prompt.ToString();
    }

    /// <summary>What the search returned, said in words. "Nothing was found" is a fact about the query and
    /// has to reach the model: without it, a subject handed an empty context block cannot tell an empty
    /// index from a question the search declined to answer.</summary>
    private static string Heading(RetrievedContext context) =>
        context.Hits.Count == 0
            ? "## Retrieved context\n\nThe search returned no results for this question."
            : $"## Retrieved context ({context.Hits.Count} excerpt(s), best first)";

    private static string Excerpt(RetrievedHit hit, RagPromptLimits limits)
    {
        var header = $"### {hit.Rank}. {hit.Where}{Named(hit)}";
        var signature = hit.Signature.Length > 0 ? $"\n`{hit.Signature}`" : string.Empty;

        return $"{header}{signature}\n\n{Body(hit, limits)}\n";
    }

    private static string Named(RetrievedHit hit) => hit.Member.Length > 0 ? $" — {hit.Member}" : string.Empty;

    /// <summary>The hit's own text, or an honest line about why there is none.
    /// <para>
    /// A hit whose text the engine never sent is still worth showing: its path, span and signature are
    /// exactly what a subject would use to go and read it. Rendering that as an empty fenced block would
    /// instead read as a file that is empty.
    /// </para></summary>
    private static string Body(RetrievedHit hit, RagPromptLimits limits) =>
        hit.Snippet.HasText
            ? Fenced(hit.Snippet.Value, limits.SnippetChars)
            : $"(the engine returned no source text for this hit — {hit.Snippet.Reason})";

    private static string Fenced(string text, int cap)
    {
        var trimmed = text.TrimEnd();
        var body = trimmed.Length <= cap
            ? trimmed
            : $"{trimmed[..cap]}\n… {trimmed.Length - cap} more character(s) of this excerpt were not shown";

        return $"```\n{body}\n```";
    }
}
