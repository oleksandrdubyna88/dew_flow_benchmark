using Bench.Domain.Gate;
using Bench.Domain.Targets;
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

    /// <summary>E7's A/A, 2026-09-28: the machine's global <c>core.autocrlf=true</c> rode into the clone, the plan reached the
    /// product CRLF, and every line of it differed from the calibration's prompt. The clone carries the setting itself, so
    /// the bytes a reviewer reads are the committed bytes on every machine — the config is asserted, not only the file, so
    /// a machine with no global setting cannot pass this vacuously.</summary>
    [Fact]
    public async Task A_clone_checks_out_the_committed_bytes_whatever_the_machine_s_line_ending_setting()
    {
        await using var rig = await GateDriverRig.StartAsync(postgres);

        var clone = (await rig.Checkouts.EnsureAsync(Guid.NewGuid(), rig.Task, Ct)).Ok();

        (await GitCommand.ReadAsync(clone, TimeSpan.FromMinutes(1), Ct, "config", "--local", "--get", "core.autocrlf")).Ok().Trim().Should().Be("false");
        (await File.ReadAllBytesAsync(Path.Combine(clone, "docs", "plan.md"), Ct)).Should().Equal("# Plan\n\nEpic 1: orders.\n"u8.ToArray(),
            "the plan is committed with LF and the product quotes it byte for byte");
    }

    /// <summary>E7's A/A, 2026-09-28: the task pins its shared rules as a submodule, the clone never initialised it, and the
    /// product told the reviewer none of the rules were there — the calibration's checkout had eight of twelve.</summary>
    [Fact]
    public async Task A_clone_carries_the_submodules_the_variant_head_pins()
    {
        await using var rig = await GateDriverRig.StartAsync(postgres);
        using var rules = new DatedGitRepo(Ct);
        await rules.InitAsync(("common/rule.md", "# A rule\n"), ("rules", "2026-09-01T09:00:00Z"));
        await rig.Repo.GitAsync("-c", "protocol.file.allow=always", "submodule", "add", "--quiet", rules.Root, ".claude/rules/shared");
        var head = await rig.Repo.CommitAsync(("docs/plan.md", "# Plan\n\nEpic 1: orders.\n\nRules pinned.\n"), ("pin the rules", "2026-09-01T12:00:00Z"));
        var t = rig.Task;
        var @case = GateCase.Of(t.Case.Base, CommitSha.Parse(head).Ok(), t.Case.PlanPath, t.Case.Epics, t.Case.Lessons).Ok();
        var task = GateTask.Of(t.Id, t.Language, t.Hosts, t.IsCalibration, @case, t.Seeds, t.Repository).Ok();

        var clone = (await rig.Checkouts.EnsureAsync(Guid.NewGuid(), task, Ct)).Ok();

        File.Exists(Path.Combine(clone, ".claude", "rules", "shared", "common", "rule.md")).Should().BeTrue(
            "the product reads the rules from the tree it is given, so an uninitialised submodule is a different task");
    }
}
