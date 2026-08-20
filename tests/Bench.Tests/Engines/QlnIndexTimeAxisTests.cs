using Bench.Domain;
using Bench.Domain.Retrieval;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Engines;

/// <summary>Which axes a SEARCH echo may be held to, and which are verified elsewhere.
///
/// <para><b>The defect this pins, found by running.</b> From 2026-08-17 16:07 — when the echo assertion was
/// wired — until 2026-08-20, <em>every</em> retrieval leg against QLN was blocked, and nobody saw it because
/// every run in between was a no-retrieval control. The refusal read
/// <c>axis 'textShape' was sent as 'GraphHeader' and the engine's echo does not carry it at all</c>, which
/// accused the engine of ignoring the recipe when the engine had honoured it.</para>
///
/// <para><b>Why the accusation was wrong.</b> <c>textShape</c> selects a COLLECTION. It is index-time, it
/// travels at the top level of the request rather than under <c>axes</c>, and the engine keeps it out of its
/// applied-axes echo deliberately: an axis that cannot be swept without re-indexing is not a query axis. It
/// IS verified — against <c>/index-state</c>, which reports it, through <c>CorpusSpec.Refuse</c>. So the axis
/// was checked twice and the second check demanded a field that will never arrive.</para>
///
/// <para>Two sets, therefore: <see cref="RetrievedContext.Requested"/> records the whole recipe, and
/// <see cref="RetrievedContext.QueryTimeRequested"/> is what the echo answers for.</para>
/// </summary>
public sealed class QlnIndexTimeAxisTests
{
    [Fact]
    public void An_index_time_selector_missing_from_the_search_echo_does_not_block_the_leg()
    {
        var context = Context(
            requested: [new Axis("limit", "5"), new Axis("textShape", "GraphHeader")],
            queryTime: [new Axis("limit", "5")],
            applied: [new Axis("limit", "5")]);

        // What LegRunner asserts. Before the split this refused, and refusing here means no qln run can
        // measure anything at all.
        context.QueryTimeRequested.AssertAppliedIn(context.Applied)
            .Should().BeOfType<Outcome<EngineAxes>.Ok>();
    }

    [Fact]
    public void The_whole_recipe_is_still_RECORDED_including_the_axis_that_is_not_asserted()
    {
        var context = Context(
            requested: [new Axis("limit", "5"), new Axis("textShape", "GraphHeader")],
            queryTime: [new Axis("limit", "5")],
            applied: [new Axis("limit", "5")]);

        // The fix must not become "stop recording it". A stored run has to say everything it asked for —
        // that is what makes a published row re-readable.
        context.Requested.Values.Should().ContainSingle(axis => axis.Name == "textShape");
        context.QueryTimeRequested.Values.Should().NotContain(axis => axis.Name == "textShape");
    }

    [Fact]
    public void A_QUERY_axis_the_engine_applied_differently_is_still_blocked()
    {
        var context = Context(
            requested: [new Axis("rerankPool", "50")],
            queryTime: [new Axis("rerankPool", "50")],
            applied: [new Axis("rerankPool", "500")]);

        // The guard that matters is untouched. Narrowing WHICH axes are asserted must not narrow how
        // strictly the asserted ones are held — a variant whose recipe was ignored would otherwise be
        // recorded as the recipe it asked for.
        context.QueryTimeRequested.AssertAppliedIn(context.Applied)
            .Should().BeOfType<Outcome<EngineAxes>.Fail>();
    }

    [Fact]
    public void A_query_axis_the_echo_omits_entirely_is_still_blocked()
    {
        var context = Context(
            requested: [new Axis("fusion", "wsum")],
            queryTime: [new Axis("fusion", "wsum")],
            applied: [new Axis("limit", "5")]);

        // The original rule, kept: for a QUERY axis a missing echo means this build cannot tell whether it
        // applied. That was always right — it was only ever wrong about which axes are query axes.
        context.QueryTimeRequested.AssertAppliedIn(context.Applied)
            .Should().BeOfType<Outcome<EngineAxes>.Fail>();
    }

    private static RetrievedContext Context(Axis[] requested, Axis[] queryTime, Axis[] applied) =>
        RetrievedContext.Of(
            "code_ab12",
            [],
            RetrievalFunnel.None,
            string.Empty,
            new EngineAxes(requested),
            new EngineAxes(queryTime),
            new EngineAxes(applied),
            payloadBytes: 0,
            elapsedMs: 0);
}
