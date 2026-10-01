using Bench.Domain.Probes;
using Bench.Infrastructure.Probes;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Probes;

/// <summary>The two roots a probe verb writes under — refused when they overlap (the work root's cleanup would delete the evidence)
/// or sit inside a git checkout — and the prune's deletion, scoped to one run's folder under the artefact root.</summary>
public sealed class ProbeRootsTests
{
    [Fact]
    public void Two_separate_roots_outside_git_are_accepted_and_created()
    {
        using var root = GateStoreFixtures.NewRoot();
        var work = root.Sibling("work");

        try
        {
            var (artifacts, checkedWork) = ProbeRoots.Check(root.Path, work).Ok();

            Directory.Exists(checkedWork).Should().BeTrue();
            artifacts.Should().NotBe(checkedWork);
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    [Fact]
    public void Overlapping_roots_are_refused_either_way_round()
    {
        using var root = GateStoreFixtures.NewRoot();

        ProbeRoots.Check(root.Path, root.Path).Reason().Should().Contain("overlap");
        ProbeRoots.Check(root.Path, Path.Combine(root.Path, "work")).Reason().Should().Contain("overlap", "a work root inside the artefact root is the evidence");
        ProbeRoots.Check(Path.Combine(root.Path, "artefacts"), root.Path).Reason().Should().Contain("overlap");
    }

    [Fact]
    public void A_work_root_inside_a_git_checkout_is_refused()
    {
        using var root = GateStoreFixtures.NewRoot();
        Directory.CreateDirectory(Path.Combine(root.Path, "repo", ".git"));

        ProbeRoots.Work(Path.Combine(root.Path, "repo", "work")).Reason().Should().Contain("git checkout");
    }

    [Fact]
    public void Deleting_a_runs_artefacts_removes_that_run_folder_only()
    {
        using var root = GateStoreFixtures.NewRoot();
        var run = Guid.CreateVersion7();
        var mine = Path.Combine(root.Path, ProbePaths.Folder, run.ToString("D"), Guid.CreateVersion7().ToString("D"), "g1", "a1");
        var other = Path.Combine(root.Path, ProbePaths.Folder, Guid.CreateVersion7().ToString("D"), "x");
        Directory.CreateDirectory(mine);
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(mine, "answer.txt"), "a");
        File.SetAttributes(Path.Combine(mine, "answer.txt"), FileAttributes.ReadOnly);

        var artifacts = new ProbeArtifacts(root.Path);

        artifacts.DeleteRun(run).Ok().Should().Be(1);
        Directory.Exists(Path.Combine(root.Path, ProbePaths.Folder, run.ToString("D"))).Should().BeFalse();
        Directory.Exists(other).Should().BeTrue();
        artifacts.DeleteRun(run).Ok().Should().Be(0, "a run with no folder has nothing to delete — not a refusal");
    }
}
