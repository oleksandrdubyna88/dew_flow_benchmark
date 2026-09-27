using Bench.Domain.Gate;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate;

/// <summary>The one path function, and the one containment rule it implies — pure, before any disk is involved.</summary>
public sealed class CellPathsTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid RunId = new("0199a1b2-c3d4-7e5f-8a9b-000000000001");

    private static readonly Guid CellId = new("0199a1b2-c3d4-7e5f-8a9b-0000000000c1");

    [Fact]
    public void An_isolated_cell_attempt_has_a_data_directory_of_its_own_and_a_later_attempt_a_new_one()
    {
        var run = Run(DataDirMode.Isolated);

        CellPaths.DataDirFor(run, CellId, 1).Value.Should().Be($"runs/{RunId}/cells/{CellId}/attempt-1/data");
        CellPaths.DataDirFor(run, CellId, 2).Value.Should().Be($"runs/{RunId}/cells/{CellId}/attempt-2/data");
    }

    [Fact]
    public void Every_cell_of_a_shared_run_resolves_to_the_one_shared_directory()
    {
        var run = Run(DataDirMode.Shared);

        CellPaths.DataDirFor(run, CellId, 1).Value.Should().Be($"runs/{RunId}/data-shared");
        CellPaths.DataDirFor(run, Guid.CreateVersion7(), 3).Should().Be(CellPaths.DataDirFor(run, CellId, 1));
    }

    [Fact]
    public void Two_isolated_cells_never_resolve_to_one_directory()
    {
        var run = Run(DataDirMode.Isolated);
        var others = Enumerable.Range(0, 50).Select(_ => CellPaths.DataDirFor(run, Guid.CreateVersion7(), 1).Value).ToList();

        others.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void The_containment_rule_is_by_segment_and_by_mode()
    {
        var isolated = new ArtifactScope(Run(DataDirMode.Isolated), CellId, 1);
        var shared = new ArtifactScope(Run(DataDirMode.Shared), CellId, 1);

        CellPaths.Allows(isolated, Path($"runs/{RunId}/cells/{CellId}/attempt-1/data/usage.jsonl")).Should().BeTrue();
        CellPaths.Allows(isolated, Path($"runs/{RunId}/data-shared/usage.jsonl")).Should().BeFalse("an isolated cell cannot reach data-shared");
        CellPaths.Allows(shared, Path($"runs/{RunId}/cells/{CellId}/attempt-1/data/usage.jsonl")).Should().BeFalse("a shared run cannot reach a cell's private dir");
        CellPaths.Allows(shared, Path($"runs/{RunId}/data-shared/usage.jsonl")).Should().BeTrue();
        CellPaths.Allows(isolated, Path($"runs/{RunId}/cells/{CellId}/attempt-10/reply.json")).Should().BeFalse("attempt-10 is not under attempt-1");
        CellPaths.Allows(isolated, Path($"runs/{RunId}/cells/{CellId}/attempt-1x/reply.json")).Should().BeFalse();
        CellPaths.Allows(isolated, Path($"runs/{RunId}/run.json")).Should().BeFalse("the run's own folder is not a cell's");
    }

    [Fact]
    public void A_scope_needs_a_claimed_cell_of_the_same_run()
    {
        var run = Run(DataDirMode.Isolated);
        var pending = GateCell.Pending(CellId, RunId, new GateMatrixCell(GateTaskId.Parse("cs2").Ok(), GateReviewerId.Parse("rev-1").Ok(), 1, 0, 0));

        ArtifactScope.Of(run, pending).Reason().Should().Contain("CLAIMED");
        ArtifactScope.Of(run with { Id = Guid.CreateVersion7() }, pending with { Claim = pending.Claim with { Attempts = 1 } }).Reason().Should().Contain("CLAIMED");
        ArtifactScope.Of(run, pending with { Claim = pending.Claim with { Attempts = 2 } }).Ok().Attempt.Should().Be(2);
    }

    [Fact]
    public void A_ref_names_a_file_its_own_attempt_wrote_and_a_real_sha()
    {
        var scope = new ArtifactScope(Run(DataDirMode.Isolated), CellId, 1);

        ArtifactRef.Of(scope, ArtifactClass.Reply, Path($"runs/{RunId}/data-shared/x"), new string('a', 64), 1).Reason().Should().Contain("outside cell");
        ArtifactRef.Of(scope, ArtifactClass.Reply, Path($"runs/{RunId}/cells/{CellId}/attempt-1/x"), "nope", 1).Reason().Should().Contain("SHA-256");
        ArtifactRef.Stored(RunId, CellId, 1, ArtifactClass.Reply, "runs/../x", new string('a', 64), 1).Reason().Should().Contain("artefact path",
            "a row edited by hand to climb out of the root is refused on read");
    }

    private static ArtifactPath Path(string value) => ArtifactPath.Parse(value).Ok();

    private static GateRun Run(DataDirMode mode) => new(RunId, GateKind.Feature, "s#0", mode, GateRunStatus.Running, new RunSource.Native(), Noon);
}
