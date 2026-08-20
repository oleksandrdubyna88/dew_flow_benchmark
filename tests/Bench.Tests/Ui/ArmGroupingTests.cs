using Bench.Contracts;
using Bench.Ui;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Ui;

/// <summary>What the console may say when it puts two compute arms beside each other.
/// <para>
/// This is the whole reason the console exists — the operator asked where the sidecar comparison was — and
/// it is also the one place a console could lie. Ranking two runs requires them to be comparable, and
/// nothing yet establishes that (<c>research/PLAN_run_report.md</c> §3.9 excluded cross-run comparison on
/// purpose). So these tests pin a REFUSAL as firmly as another suite would pin a result.
/// </para></summary>
public sealed class ArmGroupingTests
{
    [Fact]
    public void Two_arms_are_shown_side_by_side_and_deliberately_not_ranked()
    {
        var compared = ArmGrouping.Compare([
            Run("wsl-to-wsl/migraphx/R9700"),
            Run("windows-to-windows/directml/R9700"),
        ]);

        compared.Groups.Should().HaveCount(2);
        compared.Refusal.Should().NotBeEmpty("a console that ranked these would manufacture a false winner");
        compared.Refusal.Should().Contain("comparable", "the refusal must say WHY, not merely decline");
    }

    [Fact]
    public void An_engine_that_declared_no_backend_is_its_own_group_and_never_an_arm()
    {
        var compared = ArmGrouping.Compare([Run("wsl-to-wsl/migraphx/R9700"), Run("not declared")]);

        // Ordered last, because an absence is not a competitor. Folding un-echoed runs in among the arms
        // is the error this repository's three-state discipline exists to prevent.
        compared.Groups.Last().Arm.Should().Be("not declared");
        compared.Groups.Last().Declared.Should().BeFalse();
        compared.Declared.Should().Be(1, "one arm was actually echoed; the other run said nothing");
    }

    [Fact]
    public void A_single_arm_is_refused_for_a_different_reason_than_two()
    {
        var one = ArmGrouping.Compare([Run("wsl-to-wsl/migraphx/R9700")]);

        // "Nothing to compare" and "these are not comparable" are opposite pieces of news, and a reader
        // told the wrong one goes looking in the wrong place — the same rule as 400 against 404 on the API.
        one.Refusal.Should().Contain("one arm");
        one.Refusal.Should().NotContain("comparable");
    }

    [Fact]
    public void No_runs_at_all_says_so_rather_than_refusing_a_comparison_nobody_asked_for()
    {
        ArmGrouping.Compare([]).Refusal.Should().Contain("no run");
    }

    [Fact]
    public void What_actually_differs_between_the_arms_is_named_rather_than_left_to_the_reader()
    {
        var compared = ArmGrouping.Compare([
            Run("wsl-to-wsl/migraphx/R9700", suite: "s@v3#aaaaaaaaaaaa"),
            Run("windows-to-windows/directml/R9700", suite: "s@v3#bbbbbbbbbbbb"),
        ]);

        // The refusal is generic; THIS is the actionable half. Two runs on different suites differ for a
        // reason that has nothing to do with sidecars, and the reader can only act on it if it is named.
        compared.Differences.Should().ContainSingle(d => d.Contains("suite"));
    }

    [Fact]
    public void Arms_measured_the_same_way_produce_no_differences_to_report()
    {
        var compared = ArmGrouping.Compare([
            Run("wsl-to-wsl/migraphx/R9700"),
            Run("windows-to-windows/directml/R9700"),
        ]);

        // Still refused — comparability needs a scope nothing establishes yet — but the console must not
        // invent an obstacle. An empty list here is what makes a non-empty one worth reading.
        compared.Differences.Should().BeEmpty();
    }

    [Fact]
    public void Runs_within_an_arm_are_newest_first()
    {
        var older = Run("a/b/c", label: "older", at: new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));
        var newer = Run("a/b/c", label: "newer", at: new DateTimeOffset(2026, 8, 19, 0, 0, 0, TimeSpan.Zero));

        ArmGrouping.Compare([older, newer]).Groups.Single().Runs.First().Label.Should().Be("newer");
    }

    private static RunSummaryDto Run(
        string arm,
        string suite = "s@v3#abcdef012345",
        string label = "run",
        DateTimeOffset at = default) =>
        new(Guid.CreateVersion7(), label, "repo@commit", suite, $"Qln|e|1.0|fp|{arm}", arm, "Planned", "Reading", at);
}
