using Bench.Domain.Gate;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate;

/// <summary>The frozen suite of seeded tasks and its stamp. The one guarantee that matters most is that the
/// stamp says nothing about THIS machine: the operator's suite file names absolute clone paths, and a suite
/// whose clones moved is the same suite — a stamp that changed with a directory would re-identify every run.</summary>
public sealed class GateSuiteTests
{
    private static readonly CommitSha Base = CommitSha.Parse(new string('a', 40)).Ok();
    private static readonly CommitSha Variant = CommitSha.Parse(new string('b', 40)).Ok();

    [Fact]
    public void A_moved_clone_keeps_its_stamp()
    {
        var here = Suite(Task("cs2", clone: @"D:\work\clones\cs2"));
        var moved = Suite(Task("cs2", clone: "/home/someone/clones/cs2"));

        moved.Stamp.Should().Be(here.Stamp, "paths are OUT of the canonical form by construction — the stamp is about the case, not the machine");
        moved.Hash.Should().Be(here.Hash);
    }

    [Fact]
    public void Private_names_are_a_guard_input_and_not_part_of_the_stamp()
    {
        var suite = GateSuite.Freeze("gate-seeded", [Task("cs2")], []).Ok();
        var guarded = GateSuite.Freeze("gate-seeded", [Task("cs2")], ["acme-corp", "acme-payments"]).Ok();

        guarded.Stamp.Should().Be(suite.Stamp);
        guarded.PrivateNames.Should().Equal(["acme-corp", "acme-payments"]);
    }

    [Fact]
    public void Changing_a_seed_or_the_variant_head_changes_the_stamp()
    {
        var suite = Suite(Task("cs2"));
        var otherSeed = Suite(Task("cs2", seedTrigger: "a request with an empty body"));
        var otherHead = Suite(Task("cs2", variant: CommitSha.Parse(new string('c', 40)).Ok()));

        otherSeed.Stamp.Should().NotBe(suite.Stamp, "the seed's trigger is ground truth the assessor judges against");
        otherHead.Stamp.Should().NotBe(suite.Stamp, "a different head is a different planted tree");
    }

    [Fact]
    public void The_stamp_is_the_id_and_twelve_hex_of_the_hash_like_every_other_stamp_here()
    {
        var suite = Suite(Task("cs2"));

        suite.Stamp.Should().Be($"gate-seeded#{suite.Hash[..12]}");
        suite.Hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void A_plan_only_task_asked_for_the_code_gate_is_refused_by_name()
    {
        var suite = Suite(Task("plan8", hosts: [GateKind.Plan]), Task("cs2"));

        suite.Task(GateTaskId.Parse("plan8").Ok(), GateKind.Code).Reason()
            .Should().Contain("'plan8'").And.Contain("code gate").And.Contain("hosts plan");
        suite.Task(GateTaskId.Parse("plan8").Ok(), GateKind.Plan).Ok().Id.Value.Should().Be("plan8");
    }

    [Fact]
    public void Tasks_for_a_gate_are_only_the_ones_that_host_it()
    {
        var suite = Suite(Task("plan8", hosts: [GateKind.Plan]), Task("cs2"), Task("rs3"));

        suite.TasksFor(GateKind.Code).Ok().Select(t => t.Id.Value).Should().Equal(["cs2", "rs3"]);
        suite.TasksFor(GateKind.Plan).Ok().Should().HaveCount(3);

        var planOnly = Suite(Task("plan8", hosts: [GateKind.Plan]));
        planOnly.TasksFor(GateKind.Feature).Reason().Should().Contain("no task").And.Contain("feature gate");
    }

    [Fact]
    public void A_task_with_no_seeds_is_refused_a_seeded_recall_column()
    {
        var suite = Suite(Task("real1", seeds: 0), Task("cs2"));

        suite.SeedsOf(GateTaskId.Parse("real1").Ok()).Reason()
            .Should().Contain("'real1'").And.Contain("no seeds").And.Contain("seeded-recall");
        suite.SeedsOf(GateTaskId.Parse("cs2").Ok()).Ok().Should().HaveCount(2);
    }

    [Fact]
    public void An_unknown_task_is_refused_naming_what_the_suite_has()
    {
        Suite(Task("cs2")).Task(GateTaskId.Parse("php1").Ok(), GateKind.Plan).Reason()
            .Should().Contain("no task 'php1'").And.Contain("cs2");
    }

    [Fact]
    public void A_suite_without_tasks_with_a_duplicate_or_with_a_blank_private_name_is_refused()
    {
        GateSuite.Freeze("gate-seeded", [], []).Reason().Should().Contain("no tasks");
        GateSuite.Freeze("gate-seeded", [Task("cs2"), Task("cs2")], []).Reason().Should().Contain("'cs2' twice");
        GateSuite.Freeze("gate-seeded", [Task("cs2")], ["acme", " "]).Reason().Should().Contain("blank private name");
        GateSuite.Freeze("Gate Seeded", [Task("cs2")], []).Reason().Should().Contain("suite id");
    }

    [Fact]
    public void A_seed_without_its_trigger_mechanism_and_consequence_is_refused()
    {
        SeedSpec.Of("cs2-S1", "src/Orders.cs", "old", "new", "what", "trigger", "", "consequence", crossEpic: false).Reason()
            .Should().Contain("trigger, a mechanism and a consequence").And.Contain("strict rubric");
        SeedSpec.Of("cs2-S1", "", "old", "new", "what", "trigger", "mechanism", "consequence", crossEpic: false).Reason()
            .Should().Contain("names no file");
        SeedSpec.Of("cs2 S1", "f", "old", "new", "what", "t", "m", "c", crossEpic: false).Reason().Should().Contain("seed id");
    }

    [Fact]
    public void A_case_whose_variant_head_is_its_base_or_whose_plan_path_is_absolute_is_refused()
    {
        GateCase.Of(Base, Base, "todo/PLAN_x.md", string.Empty, string.Empty).Reason().Should().Contain("no diff to review");
        GateCase.Of(Base, Variant, @"C:\Users\someone\PLAN_x.md", string.Empty, string.Empty).Reason()
            .Should().Contain("REPOSITORY-relative");
        GateCase.Of(Base, Variant, " ", string.Empty, string.Empty).Reason().Should().Contain("names the plan");
    }

    [Fact]
    public void A_task_names_a_language_and_a_clone_and_carries_no_seed_twice()
    {
        var seed = SeedSpec.Of("cs2-S1", "src/A.cs", "a", "b", "w", "t", "m", "c", crossEpic: false).Ok();
        var @case = GateCase.Of(Base, Variant, "todo/PLAN_x.md", string.Empty, string.Empty).Ok();
        var clone = CloneLocation.Parse(@"D:\clones\cs2").Ok();

        GateTask.Of(GateTaskId.Parse("cs2").Ok(), " ", HostedGates.All, false, @case, [seed], clone).Reason().Should().Contain("no language");
        GateTask.Of(GateTaskId.Parse("cs2").Ok(), "C#", HostedGates.All, false, @case, [seed, seed], clone).Reason().Should().Contain("'cs2-S1' twice");
        CloneLocation.Parse("  ").Reason().Should().Contain("where its clone is");
        HostedGates.Of([]).Reason().Should().Contain("at least one gate");
    }

    [Fact]
    public void The_summary_a_database_holds_carries_seed_refs_and_no_text()
    {
        var summary = Task("cs2").Summary;

        summary.Seeds.Select(s => s.Id.Value).Should().Equal(["cs2-S1", "cs2-S2"]);
        summary.Seeds.Select(s => s.CrossEpic).Should().Equal([false, true]);
        summary.Language.Should().Be("C#");
        summary.IsSeeded.Should().BeTrue();
    }

    private static GateSuite Suite(params GateTask[] tasks) => GateSuite.Freeze("gate-seeded", tasks, []).Ok();

    private static GateTask Task(
        string id,
        string clone = @"D:\clones\task",
        IReadOnlyList<GateKind>? hosts = null,
        int seeds = 2,
        string seedTrigger = "a null order line",
        CommitSha? variant = null)
    {
        var specs = Enumerable.Range(1, seeds).Select(n => SeedSpec.Of(
            $"{id}-S{n}", $"src/{id}/File{n}.cs", $"old {n}", $"new {n}", $"what {n}", seedTrigger, $"mechanism {n}",
            $"consequence {n}", crossEpic: n % 2 == 0).Ok()).ToList();

        return GateTask.Of(
            GateTaskId.Parse(id).Ok(),
            "C#",
            HostedGates.Of(hosts ?? [GateKind.Plan, GateKind.Code, GateKind.Feature]).Ok(),
            isCalibration: false,
            GateCase.Of(Base, variant ?? Variant, "todo/PLAN_feature.md", "[{\"title\":\"E1\"}]", "{\"pitfalls\":[]}").Ok(),
            specs,
            CloneLocation.Parse(clone).Ok()).Ok();
    }
}
