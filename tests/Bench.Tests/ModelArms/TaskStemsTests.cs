using FluentAssertions;
using ModelArms.Core.Tasks;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>E4.S1: the step names the task pipeline owns (<c>t-lib.sh:53</c>, <c>:80</c>, <c>:84</c>). A scheme entry may not take one (<c>StepContractTests</c>); the pipeline names its files by them.
/// <para>
/// What this does NOT prove: that the pipeline writes its timing lines under them (E4.S3).
/// </para></summary>
public sealed class TaskStemsTests
{
    [Fact]
    public void The_stems_are_the_shells_and_a_coder_stem_is_made_only_of_a_task_id()
    {
        TaskStems.Split.Should().Be("1-split");
        TaskStems.SplitFix.Should().Be("1b-split-fix");
        TaskStems.Coder("t3").Should().Be("2-t3-code");
        TaskStems.Coder("t12").Should().Be("2-t12-code");
        foreach (var bad in new[] { "T3", "x", "t3\n", "", "t" })
        {
            var make = () => TaskStems.Coder(bad);
            make.Should().Throw<ArgumentException>($"'{bad}' is not an id the pipeline names a file by");
        }
    }

    [Theory]
    [InlineData("1-split", true)]
    [InlineData("1-SPLIT", true)]
    [InlineData("1b-split-fix", true)]
    [InlineData("1B-Split-Fix", true)]
    [InlineData("2-t1-code", true)]
    [InlineData("2-T12-CODE", true)]
    [InlineData("2-code", false)]
    [InlineData("2-t-code", false)]
    [InlineData("2-t1-code\n", false)]
    [InlineData("3-t1-code", false)]
    [InlineData("1-splits", false)]
    [InlineData("", false)]
    public void A_pipeline_stem_is_one_of_three_shapes_without_regard_to_case(string name, bool expected)
    {
        TaskStems.IsPipelineStem(name).Should().Be(expected);
    }
}
