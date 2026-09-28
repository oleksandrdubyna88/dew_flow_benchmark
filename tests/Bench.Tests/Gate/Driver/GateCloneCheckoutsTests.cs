using Bench.Infrastructure.Git;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate.Driver;

/// <summary>The gate's own clones: made once per run and task, detached at the variant head — and a clone left half-made
/// (a checkout that failed or was interrupted after the clone) is repaired, never reused as it lies.</summary>
[Collection("postgres")]
public sealed class GateCloneCheckoutsTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_clone_is_detached_at_the_variant_head_and_the_shared_checkout_gets_no_ref()
    {
        await using var rig = await GateDriverRig.StartAsync(postgres);
        var run = Guid.NewGuid();

        var clone = (await rig.Checkouts.EnsureAsync(run, rig.Task, Ct)).Ok();
        (await rig.Checkouts.CreateRefAsync(clone, "bench/gate/x/rev/cs2-r1-a1", rig.Task, Ct)).Ok();

        (await GitCommand.ReadAsync(clone, TimeSpan.FromMinutes(1), Ct, "rev-parse", "HEAD")).Ok().Trim().Should().Be(rig.Task.Case.VariantHead.Value);
        (await GitCommand.ReadAsync(clone, TimeSpan.FromMinutes(1), Ct, "rev-parse", "bench/gate/x/rev/cs2-r1-a1")).Ok().Trim().Should().Be(rig.Task.Case.VariantHead.Value);
        (await rig.Repo.GitAsync("branch", "--list", "bench/*")).Trim().Should().BeEmpty("the source repository is never written");
    }

    [Fact]
    public async Task A_clone_whose_checkout_is_not_at_the_variant_head_is_repaired_before_it_is_reused()
    {
        await using var rig = await GateDriverRig.StartAsync(postgres);
        var run = Guid.NewGuid();
        var clone = (await rig.Checkouts.EnsureAsync(run, rig.Task, Ct)).Ok();
        (await GitCommand.RunAsync(clone, TimeSpan.FromMinutes(1), Ct, "checkout", "--quiet", "--detach", rig.Task.Case.Base.Value)).Ok();

        var again = (await rig.Checkouts.EnsureAsync(run, rig.Task, Ct)).Ok();

        (await GitCommand.ReadAsync(again, TimeSpan.FromMinutes(1), Ct, "rev-parse", "HEAD")).Ok().Trim().Should().Be(rig.Task.Case.VariantHead.Value,
            "a clone interrupted between its clone and its checkout would otherwise fail every later cell of the task");
        File.Exists(Path.Combine(again, "docs", "plan.md")).Should().BeTrue();
    }
}
