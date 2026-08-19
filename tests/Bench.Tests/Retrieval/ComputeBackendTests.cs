using Bench.Domain;
using Bench.Domain.Retrieval;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Retrieval;

/// <summary>The compute backend as an axis, and the rule that decides whether a cell may run on it.
/// <para>
/// The measurement this exists for was taken on 2026-08-18 — WSL/MIGraphX against Windows/DirectML on one
/// R9700 — and could not be stored, because two engines differing only in their sidecar were one result row.
/// Everything here is the three-state discipline `IndexCommit` and `CorpusIdentity` already carry, applied to
/// a second field: matched · mismatched · <b>not declared</b>.
/// </para></summary>
public sealed class ComputeBackendTests
{
    [Fact]
    public void An_arm_reads_back_as_the_name_the_measurement_calls_it()
    {
        var parsed = ComputeBackend.Parse("wsl/migraphx/R9700").Ok();

        parsed.Host.Should().Be("wsl");
        parsed.Provider.Should().Be("migraphx");
        parsed.Device.Should().Be("R9700");
        parsed.Canonical.Should().Be("wsl/migraphx/R9700",
            "a row in a result table and a column in the write-up have to be the same string");
    }

    [Theory]
    [InlineData("wsl/migraphx")]
    [InlineData("wsl")]
    [InlineData("")]
    [InlineData("wsl//R9700")]
    [InlineData("wsl/migraphx/R9700/extra")]
    public void A_value_that_is_not_three_named_segments_is_refused(string value)
    {
        ComputeBackend.Parse(value).Reason().Should()
            .Contain("host/provider/device")
            .And.Contain("cannot be read apart on this hardware");
    }

    [Fact]
    public void A_cpu_arm_is_an_ordinary_value_because_it_is_the_only_one_that_isolates_the_host()
    {
        // The CPU provider exists on both hosts, which makes windows/cpu against wsl/cpu the ONLY pair that
        // holds the execution provider constant — the only evidence in the whole plan about the operating
        // system itself. It would be a poor special case.
        ComputeBackend.Parse("windows/cpu/—").Ok().Canonical.Should().Be("windows/cpu/—");
        ComputeBackend.Parse("wsl/cpu/—").Ok().Canonical.Should().Be("wsl/cpu/—");
    }

    [Fact]
    public void A_backend_this_build_never_heard_of_is_ACCEPTED_rather_than_erased()
    {
        // Refusing it would record a real declaration as "nothing known", which is a claim about the engine
        // rather than about this build's vocabulary — in a benchmark whose premise is ANY engine. A typo is
        // still caught, and better: by the mismatch below, which names both values.
        ComputeBackend.Parse("macos/coreml/M3").Ok().Canonical.Should().Be("macos/coreml/M3");
    }

    [Fact]
    public void Host_and_provider_are_lowered_while_the_device_keeps_the_case_it_was_reported_in()
    {
        var parsed = ComputeBackend.Parse("WSL/MIGraphX/R9700").Ok();

        parsed.Canonical.Should().Be("wsl/migraphx/R9700");
        parsed.Same(ComputeBackend.Parse("wsl/migraphx/r9700").Ok()).Should().BeTrue(
            "an operator typing r9700 against an engine reporting R9700 has not named a different card");
        parsed.Same(ComputeBackend.Parse("wsl/migraphx/R9700X").Ok()).Should().BeFalse(
            "case folding must not merge two device names that differ by more than case");
    }

    [Fact]
    public void An_unreadable_echo_is_NOT_DECLARED_rather_than_an_error()
    {
        // The IndexCommit.Read rule: a value this side cannot read is a thing it does not know, and that
        // state already exists. An exception here would fail a run over an engine's spelling.
        BackendDeclaration.Read("nonsense").Should().BeOfType<BackendDeclaration.NotDeclared>();
        BackendDeclaration.Read(null).Should().BeOfType<BackendDeclaration.NotDeclared>();
        BackendDeclaration.Read("windows/dml/R9700").Describe.Should().Be("windows/dml/R9700");
    }

    // ---- the rule, four rows ------------------------------------------------------------------------

    [Fact]
    public void A_recipe_that_names_no_arm_runs_on_anything_and_the_echo_is_still_recorded()
    {
        var served = BackendDeclaration.Read("wsl/migraphx/R9700");

        served.Refuse(BackendDeclaration.None, allowUndeclared: false).Should().BeEmpty();
        served.Canonical.Should().Be("wsl/migraphx/R9700",
            "an axis nobody planned is still an axis a report can group by afterwards — but only if it was kept");
    }

    [Fact]
    public void A_recipe_and_an_echo_that_name_the_same_arm_run()
    {
        var wanted = BackendDeclaration.Read("windows/dml/R9700");

        BackendDeclaration.Read("windows/dml/R9700").Refuse(wanted, allowUndeclared: false).Should().BeEmpty();
    }

    [Fact]
    public void An_echo_that_names_a_DIFFERENT_arm_refuses_with_both_values_printed()
    {
        var wanted = BackendDeclaration.Read("wsl/migraphx/R9700");

        var refusal = BackendDeclaration.Read("windows/dml/R9700").Refuse(wanted, allowUndeclared: false);

        // This is the whole axis. The numbers would be real; the row naming them would describe other
        // hardware — which is indistinguishable from a correct measurement once it is published.
        refusal.Should()
            .Contain("wsl/migraphx/R9700")
            .And.Contain("windows/dml/R9700")
            .And.Contain("would describe different hardware");
    }

    [Fact]
    public void An_engine_that_declares_nothing_against_a_recipe_that_names_an_arm_is_refused_and_passable()
    {
        var wanted = BackendDeclaration.Read("wsl/migraphx/R9700");

        BackendDeclaration.None.Refuse(wanted, allowUndeclared: false).Should()
            .Contain("declares no backend")
            .And.Contain("--allow-undeclared-backend")
            .And.Contain("UNVERIFIED");

        // The --allow-unstamped-index precedent: passable, and the run keeps saying what it could not verify.
        BackendDeclaration.None.Refuse(wanted, allowUndeclared: true).Should().BeEmpty();
    }

    [Fact]
    public void Not_declared_never_compares_equal_to_a_matching_declaration()
    {
        BackendDeclaration.None.Canonical.Should().BeEmpty();
        BackendDeclaration.None.Describe.Should().Be("not declared");
        BackendDeclaration.None.Should().NotBe(BackendDeclaration.Read("windows/dml/R9700"),
            "silence is not agreement, and an implementation that let it pass as one would fold an "
            + "unattributed row into an arm's aggregate");
    }
}
