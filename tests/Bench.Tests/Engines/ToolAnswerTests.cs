using Bench.Domain.Engines;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Engines;

/// <summary>The three-state answer, on its own.
/// <para>
/// A closed union with three cases is only worth the ceremony if consumers cannot collapse it back
/// into two, so the projections every consumer reaches for — the text, the refused flag, the match —
/// are pinned here rather than left to whichever caller uses them first.
/// </para></summary>
public sealed class ToolAnswerTests
{
    [Fact]
    public void The_three_cases_are_distinguishable_and_stay_that_way()
    {
        ToolAnswer.Success("content").Should().BeOfType<ToolAnswer.Ok>();
        ToolAnswer.Refusal("outside").Should().BeOfType<ToolAnswer.Refused>();
        ToolAnswer.Failure("the disk went away").Should().BeOfType<ToolAnswer.Failed>();
    }

    [Fact]
    public void Only_a_refusal_is_refused()
    {
        // The distinction the whole union exists for: a guard that worked is not a component that
        // broke, and a ledger that files both under one flag can count neither.
        ToolAnswer.Refusal("outside").WasRefused.Should().BeTrue();
        ToolAnswer.Failure("the disk went away").WasRefused.Should().BeFalse();
        ToolAnswer.Success("content").WasRefused.Should().BeFalse();
    }

    [Fact]
    public void The_text_of_an_answer_is_readable_whichever_case_it_is()
    {
        // The one projection every consumer wants, present so none of them writes a fourth three-arm
        // match of its own — and each divergent one is a place the cases can quietly become two.
        ToolAnswer.Success("content").Text.Should().Be("content");
        ToolAnswer.Refusal("outside").Text.Should().Be("outside");
        ToolAnswer.Failure("broken").Text.Should().Be("broken");
    }

    [Fact]
    public void Match_dispatches_each_case_to_its_own_arm()
    {
        static string Describe(ToolAnswer answer) =>
            answer.Match(ok => $"ok:{ok}", refused => $"refused:{refused}", failed => $"failed:{failed}");

        Describe(ToolAnswer.Success("c")).Should().Be("ok:c");
        Describe(ToolAnswer.Refusal("r")).Should().Be("refused:r");
        Describe(ToolAnswer.Failure("f")).Should().Be("failed:f");
    }

    [Fact]
    public void An_answer_carrying_the_same_content_equals_another()
    {
        // Value equality, so a test asserting on a tool's answer compares what it said rather than
        // which object said it.
        ToolAnswer.Success("content").Should().Be(ToolAnswer.Success("content"));
        ToolAnswer.Success("content").Should().NotBe(ToolAnswer.Refusal("content"));
    }
}
