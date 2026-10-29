using FluentAssertions;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The one reading of an arm's name: letters then digits and nothing else. <c>ArmMap</c> and the flow section both
/// ask this class, so what it refuses is refused everywhere.
/// <para>
/// What this does NOT prove: that every caller asks it — that is the one place the rule is written, not a check on its callers.
/// </para></summary>
public sealed class ArmNamesTests
{
    [Theory]
    [InlineData("C1", "C")]
    [InlineData("SRS12", "SRS")]
    [InlineData("cr3", "cr")]
    public void An_arm_is_letters_then_digits_and_its_scheme_is_the_letters(string arm, string scheme) =>
        ArmNames.SchemeOf(arm).Ok().Should().Be(scheme);

    [Theory]
    [InlineData("C1\n", "a trailing newline is not part of a name ($ would let it through, \\z does not)")]
    [InlineData("\nC1", "nor is a leading one")]
    [InlineData("C1 ", "nor a trailing space")]
    [InlineData("", "an empty name")]
    [InlineData("C", "letters alone")]
    [InlineData("12", "digits alone")]
    [InlineData("c-1", "a hyphen")]
    [InlineData("1C", "digits first")]
    public void Anything_else_is_refused_and_named(string arm, string why) =>
        ArmNames.SchemeOf(arm).Failed().Should().BeTrue(why);

    [Theory]
    [InlineData("C1", true)]
    [InlineData("SRS12", true)]
    [InlineData("C1\n", false)]
    [InlineData("c-1", false)]
    [InlineData("", false)]
    public void The_predicate_agrees_with_the_scheme_reading(string arm, bool expected)
    {
        ArmNames.IsArmName(arm).Should().Be(expected);
        ArmNames.SchemeOf(arm).Failed().Should().Be(!expected, "one rule, two questions");
    }
}
