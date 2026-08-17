using Bench.Domain.Retrieval;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Retrieval;

/// <summary>Every axis a run ASKED for, present in the engine's echo with the same value.
/// <para>
/// The enforcement the whole echo discipline exists for. An engine that does not know an axis answers 200 and
/// echoes a well-formed set that simply LACKS it, so a run records <c>wsum</c> beside numbers rank fusion
/// produced and nothing anywhere disagrees. That is the reranker scar: a stale pinned port left four measured
/// arms running with no reranker while the settings page reported one.
/// </para>
/// <para>
/// <b>The by-meaning comparison is not a nicety.</b> Measured against a live daemon on 2026-08-17: this side
/// sends <c>dense=true</c> and the engine echoes <c>dense=True</c>, its own <c>ToString</c>. A string
/// comparison would have flagged every boolean axis on every cell — the guard would have blocked the entire
/// matrix on its first run, and a guard that does that gets deleted rather than fixed.
/// </para></summary>
public sealed class EchoAssertionTests
{
    [Fact]
    public void An_axis_the_echo_does_not_carry_AT_ALL_is_refused_by_name()
    {
        var asked = Axes(("limit", "5"), ("fusion", "wsum"));
        var applied = Axes(("limit", "5"));

        var refused = asked.AssertAppliedIn(applied);

        refused.Reason().Should().Contain("'fusion'").And.Contain("wsum");
        refused.Reason().Should().Contain("does not carry it at all");
    }

    [Fact]
    public void An_axis_the_engine_applied_DIFFERENTLY_is_refused_with_both_values()
    {
        var refused = Axes(("rerankPool", "50")).AssertAppliedIn(Axes(("rerankPool", "500")));

        // Clamping is legitimate and visible precisely because the echo carries it — but a cell measured under
        // a clamped pool is a cell whose row names a pool it did not run.
        refused.Reason().Should().Contain("50").And.Contain("500");
        refused.Reason().Should().Contain("describe a different configuration");
    }

    [Theory]
    [InlineData("true", "True")]
    [InlineData("false", "False")]
    [InlineData("True", "true")]
    public void A_boolean_in_the_engines_own_casing_is_the_same_boolean(string asked, string echoed)
    {
        // The live finding. Both sides are correct in their own language, and the pair means one thing.
        Axes(("dense", asked)).AssertAppliedIn(Axes(("dense", echoed))).Failed().Should().BeFalse();
    }

    [Theory]
    [InlineData("1", "1.0")]
    [InlineData("0.7", "0.70")]
    [InlineData("60", "60")]
    public void A_number_written_two_ways_is_the_same_number(string asked, string echoed) =>
        Axes(("denseWeight", asked)).AssertAppliedIn(Axes(("denseWeight", echoed))).Failed().Should().BeFalse();

    [Fact]
    public void A_number_that_genuinely_differs_is_still_refused()
    {
        // The pair above must not be loosened into "any two numbers agree".
        Axes(("denseWeight", "0.7")).AssertAppliedIn(Axes(("denseWeight", "0.3"))).Failed().Should().BeTrue();
    }

    [Fact]
    public void The_echo_may_carry_MORE_than_was_asked()
    {
        var asked = Axes(("limit", "5"), ("fusion", "rrf"));
        var applied = Axes(
            ("limit", "5"), ("fusion", "rrf"), ("denseWidth", "100"), ("rerankFloor", "-1000"),
            ("wsumNorm", "minmax"), ("collapseMembers", "True"));

        // Every one of those is real in the engine's own defaults, and `wsumNorm` is one this side deliberately
        // does NOT send under rank fusion. Demanding they match something never requested would block every
        // cell of every run.
        asked.AssertAppliedIn(applied).Failed().Should().BeFalse();
    }

    [Fact]
    public void The_axis_name_is_matched_case_insensitively()
    {
        Axes(("rrfK", "60")).AssertAppliedIn(Axes(("rrfk", "60"))).Failed().Should().BeFalse(
            "the name is the engine's own spelling of a field, and this side must not block on its casing");
    }

    [Fact]
    public void A_run_that_asked_for_nothing_asserts_nothing()
    {
        // The tool-surface path sends a limit alone, and the baseline arm sends no axes at all.
        EngineAxes.None.AssertAppliedIn(Axes(("limit", "20"))).Failed().Should().BeFalse();
    }

    [Fact]
    public void An_empty_echo_refuses_the_first_axis_that_was_asked_for()
    {
        // What an engine too old to echo its axes would produce. Storing the request beside an empty echo and
        // calling it verified is exactly the failure this assertion closes.
        Axes(("limit", "5")).AssertAppliedIn(EngineAxes.None).Reason().Should().Contain("'limit'");
    }

    private static EngineAxes Axes(params (string Name, string Value)[] values) =>
        new([.. values.Select(v => new Axis(v.Name, v.Value))]);
}
