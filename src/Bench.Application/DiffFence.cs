using Bench.Domain;

namespace Bench.Application;

/// <summary>Getting a solver's DIFF out of a solver's answer — the diff-shaped sibling of
/// <see cref="Bank.AgentJson"/>, and the same rule: extraction, never repair.
/// <para>
/// A fenced <c>```diff</c> block wins; any other fence whose body LOOKS like a unified diff (opens with
/// <c>diff --git</c> or <c>---&#160;</c>) is accepted, because models label the fence inconsistently;
/// bare text that opens like a diff is accepted whole. Prose with no diff anywhere is a refusal carrying
/// a sample — the next edit to the solve prompt is made from exactly that text.
/// </para></summary>
public static class DiffFence
{
    public static Outcome<string> Extract(string answer)
    {
        var text = (answer ?? string.Empty).Trim();

        foreach (var fence in Fences(text))
        {
            if (LooksLikeDiff(fence))
            {
                return Outcome<string>.Success(fence);
            }
        }

        return LooksLikeDiff(text)
            ? Outcome<string>.Success(text)
            : Outcome<string>.Failure(
                $"the answer carries no unified diff — it said: {(text.Length <= 200 ? text : text[..200] + "…")}");
    }

    /// <summary>Every fenced block's body, in order. The language tag on the opening fence is ignored —
    /// what decides is the body's own first line.</summary>
    private static IEnumerable<string> Fences(string text)
    {
        var from = 0;

        while (true)
        {
            var open = text.IndexOf("```", from, StringComparison.Ordinal);

            if (open < 0)
            {
                yield break;
            }

            var bodyStart = text.IndexOf('\n', open);

            if (bodyStart < 0)
            {
                yield break;
            }

            // The closing fence must sit at the START of a line. A diff that touches markdown or a
            // prompt file carries `+```…` lines INSIDE the body — those begin with '+', not with a
            // backtick, so the line-start rule skips them where a bare IndexOf("```") truncated the
            // diff mid-hunk and scored a valid solution "does not apply". Found by review.
            var close = text.IndexOf("\n```", bodyStart, StringComparison.Ordinal);

            if (close < 0)
            {
                yield break;
            }

            yield return text[(bodyStart + 1)..(close + 1)].Trim();
            from = close + 4;
        }
    }

    private static bool LooksLikeDiff(string candidate) =>
        candidate.StartsWith("diff --git ", StringComparison.Ordinal)
        || candidate.StartsWith("--- ", StringComparison.Ordinal);
}
