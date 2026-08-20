using Bench.Application;
using Bench.Domain;
using Bench.Domain.Retrieval;
using Bench.Domain.Runs;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Api;

/// <summary>The run summary the console lists, and the one field it groups by.
/// <para>
/// Backfilled: <see cref="RunReportContract.From(BenchRun)"/> has been the body of <c>GET /api/bench/runs</c>
/// since the read surface existed and was asserted by nothing — the shape this repository keeps finding, one
/// step short of "built and never called".
/// </para></summary>
public sealed class RunSummaryContractTests
{
    [Fact]
    public void The_compute_arm_is_its_own_field_rather_than_a_segment_of_the_canonical_string()
    {
        var summary = RunReportContract.From(Run(BackendDeclaration.Read("windows-to-wsl/migraphx/R9700")));

        // The axis the console groups by must be readable without splitting a string that exists for
        // equality. EngineCanonical joins five facts with pipes; a page parsing it would be parsing a
        // format nobody promised it.
        summary.ComputeArm.Should().Be("windows-to-wsl/migraphx/R9700");
        summary.EngineCanonical.Should().Contain("windows-to-wsl/migraphx/R9700",
            "the canonical form still carries it — this field is a second reading, not a move");
    }

    [Fact]
    public void An_engine_that_declared_no_backend_says_so_rather_than_arriving_blank()
    {
        var summary = RunReportContract.From(Run(BackendDeclaration.None));

        // The third state, carried to the wire. A blank label would group every un-echoed run under
        // whatever the console renders for empty, which is the one error indistinguishable from a
        // correct measurement afterwards.
        summary.ComputeArm.Should().Be("not declared");
    }

    private static BenchRun Run(BackendDeclaration backend) => BenchRun.Planned(
        "arms",
        MeasurementTarget.At(
            RepoUrl.Parse("https://example.invalid/x.git").Ok(),
            CommitSha.Parse(new string('c', 40)).Ok()),
        new EngineRef(EngineKind.Qln, "http://localhost:5080", "1.0", "fp") { Backend = backend },
        "s@v3#abcdef012345",
        new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero));
}
