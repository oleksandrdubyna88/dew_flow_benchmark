namespace Bench.Domain.Suites;

public enum TermFault
{
    /// <summary>The term the answer must contain is already in the QUESTION, so any on-topic answer contains it —
    /// including a wrong one. It measures nothing.</summary>
    LeaksIntoPrompt,

    /// <summary>The term the answer must contain is absent from the question's own reference answer, so an answer
    /// modelled on the gold one FAILS the check.</summary>
    MissingFromReference,

    /// <summary>The term the answer must NOT contain is in the reference answer — the gold answer fails its own
    /// exclusion.</summary>
    ExcludedTermInReference,
}

public sealed record TermDefect(string Term, TermFault Fault, string Detail)
{
    public string Describe => $"term '{Term}': {Detail}";
}

/// <summary>Whether a question's scoring terms can decide anything.
/// <para>
/// <b>Both of these came from real reviewer rejections, 2026-08-18</b>, and both are substring arithmetic — which
/// is the point. The panel spent three launches each to find them:
/// </para>
/// <para>
/// <i>"the Required AnswerContains term 'single line' does not appear in the reference answer, which phrases the
/// concept as 'one-line window' … a correct answer modelled on the gold reference would fail this literal
/// substring check"</i> — and <i>"the term 'branch' appears verbatim and repeatedly in the prompt ('Two
/// branches…', 'branch B'), so any on-topic answer — including a wrong one — is guaranteed to contain it, making
/// it a non-discriminating term."</i>
/// </para>
/// <para>
/// The comparison is <see cref="StringComparison.OrdinalIgnoreCase"/> because that is what
/// <c>AnswerScoring</c> uses. A gate that compared more strictly than the scorer would refuse questions that
/// actually score fine, which is the one way a check like this does harm.
/// </para>
/// <para>
/// Only <c>Required</c> terms. An optional term enriches a score and is allowed to be redundant; a required one
/// decides pass or fail.
/// </para></summary>
public static class QuestionSanity
{
    public static IReadOnlyList<TermDefect> Check(Question question) =>
        [.. question.Expectations
            .Where(expectation => expectation.Required && Textual(expectation) && expectation.Text.Trim().Length > 0)
            .SelectMany(expectation => Faults(question, expectation))];

    private static bool Textual(Expectation expectation) =>
        expectation.Kind is ExpectationKind.AnswerContains or ExpectationKind.AnswerExcludes;

    private static IEnumerable<TermDefect> Faults(Question question, Expectation expectation)
    {
        var term = expectation.Text.Trim();
        var inReference = Has(question.ReferenceAnswer, term);

        if (expectation.Kind == ExpectationKind.AnswerExcludes)
        {
            if (inReference)
            {
                yield return new TermDefect(
                    term,
                    TermFault.ExcludedTermInReference,
                    "the answer must NOT contain it, and the question's own reference answer does");
            }

            yield break;
        }

        if (Has(question.Prompt, term))
        {
            yield return new TermDefect(
                term,
                TermFault.LeaksIntoPrompt,
                "it appears in the prompt, so any on-topic answer contains it — including a wrong one");
        }

        // Guarded on a reference existing at all: a question with none cannot be compared against one, and
        // flagging every such question would refuse a legitimate shape rather than a defect.
        if (question.ReferenceAnswer.Trim().Length > 0 && !inReference)
        {
            yield return new TermDefect(
                term,
                TermFault.MissingFromReference,
                "it is absent from the reference answer, so an answer modelled on the gold one would fail");
        }
    }

    private static bool Has(string text, string term) =>
        text.Contains(term, StringComparison.OrdinalIgnoreCase);
}
