using Bench.Domain.Suites;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Suites;

/// <summary>Whether a question's scoring terms can decide anything.
/// <para>
/// Every case here is a REAL reviewer rejection from 2026-08-18, reproduced. Both of the two rejections the live
/// panel produced were substring arithmetic, which it spent three launches each to discover — so the point of
/// this class is that they never cost a launch again.
/// </para></summary>
public sealed class QuestionSanityTests
{
    private static readonly CommitSha Commit = CommitSha.Parse(new string('e', 40)).Ok();

    [Fact]
    public void A_required_term_that_is_ALREADY_IN_THE_PROMPT_cannot_discriminate()
    {
        // reviewer-3, verbatim: "the term 'branch' appears verbatim and repeatedly in the prompt ('Two
        // branches…', 'branch B'), so any on-topic answer — including a wrong one that blames the destructive
        // sweep — is guaranteed to contain it, making it a non-discriminating term."
        var question = Ask(
            "Two branches of one repository share an index. Branch B's results show branch A's code. Why?",
            "the collection namespace omits the branch",
            Contains("branch"));

        var defect = QuestionSanity.Check(question).Should().ContainSingle().Subject;

        defect.Fault.Should().Be(TermFault.LeaksIntoPrompt);
        defect.Describe.Should().Contain("including a wrong one");
    }

    [Fact]
    public void A_required_term_ABSENT_FROM_THE_REFERENCE_ANSWER_would_fail_the_gold_answer()
    {
        // reviewer-1, verbatim: "The Required AnswerContains term 'single line' does not appear in the reference
        // answer, which phrases the concept as 'one-line window' … a correct answer modeled on the gold
        // reference would fail this literal substring check."
        var question = Ask(
            "Why is a very long source line embedded incompletely?",
            "the fitter emits a one-line window and truncates what exceeds it",
            Contains("single line"));

        var defect = QuestionSanity.Check(question).Should().ContainSingle().Subject;

        defect.Fault.Should().Be(TermFault.MissingFromReference);
        defect.Describe.Should().Contain("modelled on the gold one would fail");
    }

    [Fact]
    public void A_term_in_the_reference_and_NOT_in_the_prompt_is_exactly_right()
    {
        var question = Ask(
            "What does the classifier inspect first when naming a store?",
            "the container image, falling back to the name",
            Contains("image"));

        QuestionSanity.Check(question).Should().BeEmpty(
            "this is the shape the live panel approved three times over");
    }

    [Fact]
    public void The_comparison_matches_the_SCORER_which_ignores_case()
    {
        // AnswerScoring compares with OrdinalIgnoreCase. A gate stricter than the scorer would refuse questions
        // that actually score fine, which is the one way a check like this does harm.
        var question = Ask("What does GpuGate return when the scope AlreadyHeld the lease?", "AlreadyHeld", Contains("alreadyheld"));

        QuestionSanity.Check(question).Should().ContainSingle().Which.Fault.Should().Be(TermFault.LeaksIntoPrompt);
    }

    [Fact]
    public void An_OPTIONAL_term_is_allowed_to_be_redundant()
    {
        var question = Ask(
            "Two branches share an index. Why?",
            "the namespace omits the branch",
            new Expectation(ExpectationKind.AnswerContains, SourceAnchor.File("src/A.cs", Commit), "branch", Required: false));

        // An optional term enriches a score; a required one decides pass or fail. Only the second is a defect.
        QuestionSanity.Check(question).Should().BeEmpty();
    }

    [Fact]
    public void An_EXCLUDED_term_that_is_in_the_reference_answer_makes_the_gold_answer_fail()
    {
        var question = Ask(
            "Why does the index pass die on a large repository?",
            "the sidecar refuses an oversized batch with 413, which is not a firewall problem",
            new Expectation(ExpectationKind.AnswerExcludes, SourceAnchor.File("src/A.cs", Commit), "firewall", Required: true));

        // The memorisation-trap shape: an AnswerExcludes term names the plausible wrong answer. If the reference
        // itself says the word, every faithful answer fails.
        QuestionSanity.Check(question).Should().ContainSingle()
            .Which.Fault.Should().Be(TermFault.ExcludedTermInReference);
    }

    [Fact]
    public void An_excluded_term_the_reference_avoids_is_clean() =>
        QuestionSanity.Check(Ask(
            "Why does the index pass die on a large repository?",
            "the sidecar refuses an oversized batch with 413",
            new Expectation(ExpectationKind.AnswerExcludes, SourceAnchor.File("src/A.cs", Commit), "firewall", Required: true)))
            .Should().BeEmpty();

    [Fact]
    public void A_question_with_NO_reference_answer_is_not_flagged_for_missing_the_term()
    {
        var question = new Question("q", "what does it return?", [Contains("Outcome")], string.Empty);

        // A question without a reference cannot be compared against one. Flagging every such question would
        // refuse a legitimate shape rather than a defect.
        QuestionSanity.Check(question).Should().BeEmpty();
    }

    [Fact]
    public void Retrieval_expectations_are_not_this_checks_business() =>
        QuestionSanity.Check(new Question(
            "q",
            "where is it?",
            [Expectation.Member(SourceAnchor.Member("src/A.cs", "A.M", new LineSpan(1, 9), Commit))],
            "in A"))
            .Should().BeEmpty("anchors are AnchorCheck's job, and one check per concern is the whole point");

    private static Question Ask(string prompt, string reference, params Expectation[] expectations) =>
        new("q", prompt, expectations, reference);

    private static Expectation Contains(string term) =>
        new(ExpectationKind.AnswerContains, SourceAnchor.File("src/A.cs", Commit), term, Required: true);
}
