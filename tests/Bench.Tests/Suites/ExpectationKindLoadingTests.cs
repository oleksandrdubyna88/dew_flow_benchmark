using Bench.Application;
using Bench.Domain;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Suites;

/// <summary>
/// An expectation kind this build does not know must REFUSE the question, not become a file expectation.
///
/// <para>Every other unknown value in this system is refused by name — an unknown axis field, an unknown
/// funnel stage, an unknown telemetry version. This one silently fell back, so <c>"ToolUsedd"</c> became a
/// <c>File</c> expectation against an empty path and then scored as a retrieval miss the author never
/// wrote. Adding two new kind names is exactly the change that turns a latent typo trap into a live
/// one.</para>
///
/// <para>The fallback is reached by TWO paths, not the one §3.7 of the plan describes: a suite loaded from
/// JSON and a question rehydrated from a bank row both go through <c>QuestionJson.ToExpectation</c>. Both
/// are asserted here, because a guard on one of them would leave the trap open on the other.</para>
/// </summary>
public sealed class ExpectationKindLoadingTests
{
    private static readonly CommitSha Authored =
        CommitSha.Parse(new string('a', 40)).Ok();

    [Theory]
    [InlineData("ToolUsedd")]
    [InlineData("answercontian")]
    [InlineData("")]
    public void A_stored_question_with_an_unknown_expectation_kind_is_refused_by_name(string kind)
    {
        var refusal = QuestionJson.ReadExpectations(
            $$"""[{"kind":"{{kind}}","file":"src/A.cs","text":"","required":true}]""", Authored);

        refusal.Should().BeOfType<Outcome<IReadOnlyList<Expectation>>.Fail>()
            .Which.Reason.Should().Contain(kind.Length > 0 ? kind : "empty")
            .And.Contain("File", "a refusal that does not name the legal kinds leaves the author guessing");
    }

    [Fact]
    public void A_misspelt_kind_does_NOT_become_a_file_expectation_against_an_empty_path()
    {
        // The whole defect, in one assertion. A File expectation with no path is a retrieval anchor that can
        // never be surfaced, so the question scores as a miss forever and the author is told nothing.
        var read = QuestionJson.ReadExpectations(
            """[{"kind":"ToolUsedd","file":"","text":"rt_read_local_file","required":true}]""", Authored);

        read.Should().NotBeOfType<Outcome<IReadOnlyList<Expectation>>.Ok>();
    }

    [Theory]
    [InlineData("ToolUsed")]
    [InlineData("toolused")]
    [InlineData("ToolNotUsed")]
    [InlineData("Member")]
    [InlineData("answercontains")]
    public void Every_kind_this_build_knows_still_loads_case_insensitively(string kind)
    {
        var read = QuestionJson.ReadExpectations(
            $$"""[{"kind":"{{kind}}","file":"src/A.cs","text":"rt_read_local_file","required":true}]""",
            Authored);

        read.Should().BeOfType<Outcome<IReadOnlyList<Expectation>>.Ok>()
            .Which.Value.Should().ContainSingle();
    }
}
