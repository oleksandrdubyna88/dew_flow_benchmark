using Bench.Domain;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Trace;

/// <summary>The contract itself: what a funnel may name, and what an unknown version costs.
/// <para>
/// These names are the ones a working engine emits. They are asserted here rather than left implicit
/// because the whole value of a named contract is that a renamed stage breaks a build instead of
/// silently producing a column of zeroes in a benchmark row.
/// </para></summary>
public sealed class TraceContractTests
{
    [Fact]
    public void The_contract_names_the_stages_a_shipping_engine_actually_emits()
    {
        // Reconciled 2026-08-15 against dew_flow_rag_qln's SearchService, which is the first engine to
        // emit a funnel. Five of the seven drafted names were wrong; where the draft and the emitter
        // disagreed, the emitter won — a contract whose names no producer uses produces empty columns.
        //
        // 'collapse' was added later the same day, after the emitter shipped it and this contract refused
        // EVERY funnel it produced. Both suites stayed green throughout, because each compared its own
        // hand-typed list against itself. Reconciling by hand is what failed; the live test below is the
        // only check that actually compares the two mirrors.
        TraceContract.Stages.Should().Equal(
            "collection", "embed-query", "retrieve", "fuse", "collapse", "rerank", "cut", "graph-enrich");
    }

    [Fact]
    public void The_deviation_from_the_first_draft_is_recorded_rather_than_smoothed_away()
    {
        // The plan's condition for shipping a contract before its first emitter existed: when a real
        // payload arrives, the difference is written down. The reason is the valuable part, and a diff
        // does not carry it.
        TraceContract.Deviation.Should().Contain("semantic").And.Contain("assemble").And.Contain("cut");
    }

    [Fact]
    public void An_unknown_version_degrades_to_black_box_rather_than_failing_the_run()
    {
        TraceContract.IsWhiteBox(TraceContract.V0).Should().BeTrue();
        TraceContract.IsWhiteBox("trace/v9").Should().BeFalse();

        // Refusing to measure an engine because its trace is too new would make the benchmark unable to
        // measure exactly the engines most worth measuring.
        TraceContract.DegradationReason("trace/v9").Should().Contain("trace/v9").And.Contain(TraceContract.V0);
        TraceContract.DegradationReason(string.Empty).Should().Contain("no trace contract");
        TraceContract.DegradationReason(TraceContract.V0).Should().BeEmpty();
    }

    [Fact]
    public void A_qualifier_belongs_to_its_base_stage()
    {
        TraceContract.BaseStage("retrieve:dense").Should().Be("retrieve");
        TraceContract.BaseStage("fuse").Should().Be("fuse");
        TraceContract.Defines("retrieve:sparse").Should().BeTrue();

        // The qualifier does not license an invented base.
        TraceContract.Defines("vibes:dense").Should().BeFalse();
    }

    [Fact]
    public void A_funnel_naming_a_stage_the_contract_does_not_define_is_refused()
    {
        var invented = new RetrievalFunnel(TraceContract.V0, [new FunnelStage("vibes", 10, 5, 1)], 10, []);

        TraceContract.Validate(invented).Should().BeOfType<Outcome<RetrievalFunnel>.Fail>()
            .Which.Reason.Should().Contain("vibes");
    }

    [Fact]
    public void An_absent_list_naming_an_undefined_stage_is_refused_too()
    {
        // The absent list is a claim about the CONTRACT — "I do not perform this defined stage". An
        // engine declaring absence of something the contract never had is describing a different
        // contract, and reading it as agreement is how two versions quietly become one.
        var funnel = new RetrievalFunnel(TraceContract.V0, [new FunnelStage("fuse", 10, 5, 1)], 10, ["telepathy"]);

        TraceContract.Validate(funnel).Should().BeOfType<Outcome<RetrievalFunnel>.Fail>()
            .Which.Reason.Should().Contain("telepathy");
    }

    [Fact]
    public void A_funnel_from_the_declared_version_with_known_stages_is_accepted()
    {
        var funnel = new RetrievalFunnel(
            TraceContract.V0,
            [new FunnelStage("retrieve", 200, 145, 210), new FunnelStage("retrieve:dense", 100, 100, 0)],
            400,
            ["graph-enrich"]);

        TraceContract.Validate(funnel).Should().BeOfType<Outcome<RetrievalFunnel>.Ok>();
    }

    [Fact]
    public void A_funnel_from_a_version_this_build_does_not_read_is_refused_by_name()
    {
        var future = new RetrievalFunnel("trace/v9", [new FunnelStage("fuse", 10, 5, 1)], 10, []);

        TraceContract.Validate(future).Should().BeOfType<Outcome<RetrievalFunnel>.Fail>()
            .Which.Reason.Should().Contain("trace/v9");
    }

    [Fact]
    public void An_absent_funnel_accounts_for_nothing_and_claims_nothing()
    {
        RetrievalFunnel.None.IsPresent.Should().BeFalse();
        RetrievalFunnel.None.TotalMs.Should().Be(0);
        RetrievalFunnel.None.UnattributedMs.Should().Be(0, "a funnel nobody reported has no remainder to explain");
        RetrievalFunnel.None.Absent.Should().BeEmpty();
    }
}
