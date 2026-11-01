using ModelArms.Adapters;
using ModelArms.Core.Chain;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The chain's decision (E5.S4, C25, parent §4.7), pure: <see cref="ChainPlan.Next"/> answers the first rule that holds over facts the test builds by hand — which arms are made, done, alive, ran, checked, which markers stand over which snapshot, how long the chain has waited — and <see cref="ChainWaitCeiling"/> the bounds of its waits. Nothing here touches a file or a clock.
/// <para>
/// What this does NOT prove: that the facts are read right, that an action is carried out in the right order or that a wait sleeps what remains (<c>ChainVerbTests</c>); that a started runner really is detached (<c>ChainProcessTests</c>).
/// </para></summary>
public sealed class ChainPlanTests
{
    private const string Digest = "d1";
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(600);
    private static readonly string[] Part6Arms = ["C1", "CR1", "SRS1", "C2", "CR2", "SRS2", "C3", "CR3", "SRS3"];
    private static readonly RunSpec Part6 = new("part6", [["C1", "CR1", "SRS1"], ["C2", "CR2", "SRS2"], ["C3", "CR3", "SRS3"]], [], []);
    private static readonly RunSpec Part7 = new("part7", [["CHH1", "CHH2", "CHH3"]], Part6Arms, []);

    private static ArmFacts Arm(string arm, bool made = true, bool done = false, bool alive = false, bool ran = false, bool @checked = false) => new(arm, made, done, alive, ran, @checked);

    /// <summary>Facts for <paramref name="run"/> in which every arm is made, nothing is done or alive, the run is locked over an applied snapshot of its own digest and nothing else has happened; <paramref name="arms"/> changes single arms by name.</summary>
    private static ChainFacts Facts(RunSpec run, params (string Arm, Func<ArmFacts, ArmFacts> Change)[] arms)
    {
        var names = run.Rounds.SelectMany(round => round).ToList();
        var changes = arms.ToDictionary(each => each.Arm, each => each.Change);
        return new ChainFacts(
            run,
            [.. names.Select(name => changes.TryGetValue(name, out var change) ? change(Arm(name)) : Arm(name))],
            [],
            Locked: true,
            Unlocked: false,
            ArmsDone: false,
            new LockState.Applied(run.Name, "2026-11-01T10:00:00Z", Digest),
            Digest,
            new HashSet<string>(StringComparer.Ordinal),
            TimeSpan.Zero,
            TimeSpan.Zero,
            Grace,
            TimeSpan.Zero,
            TimeSpan.FromHours(1));
    }

    private static (string Arm, Func<ArmFacts, ArmFacts> Change) Is(string arm, Func<ArmFacts, ArmFacts> change) => (arm, change);

    private static (string Arm, Func<ArmFacts, ArmFacts> Change)[] AllDone(RunSpec run) => [.. run.Rounds.SelectMany(round => round).Select(arm => Is(arm, each => each with { Done = true }))];

    private static (string Arm, Func<ArmFacts, ArmFacts> Change)[] Done(params string[] arms) => [.. arms.Select(arm => Is(arm, each => each with { Done = true }))];

    private static T Action<T>(ChainFacts facts)
        where T : ChainAction =>
        ChainPlan.Next(facts).Should().BeOfType<T>().Subject;

    private static HashSet<string> Started(params string[] arms) => new(arms, StringComparer.Ordinal);

    [Fact]
    public void The_first_action_is_making_every_unmade_arm_of_the_run()
    {
        for (var made = 0; made < Part6Arms.Length; made++)
        {
            var facts = Facts(Part6, [.. Part6Arms.Select((arm, index) => Is(arm, each => each with { Made = index < made }))]) with { Locked = false };

            Action<ChainAction.Make>(facts).Arm.Should().Be(Part6Arms[made], $"the arms are made in round order: {made} are made, so the next is {Part6Arms[made]}");
        }
    }

    [Fact]
    public void Lock_comes_after_the_last_make_and_before_the_first_start()
    {
        var unlocked = Facts(Part6) with { Locked = false, Snapshot = new LockState.None() };
        Action<ChainAction.Lock>(unlocked);

        var oneUnmade = Facts(Part6, Is("SRS3", each => each with { Made = false })) with { Locked = false };
        Action<ChainAction.Make>(oneUnmade).Arm.Should().Be("SRS3", "a make comes before the lock even with `locked` absent");

        Action<ChainAction.Start>(Facts(Part6)).Arms.Should().Equal("C1", "CR1", "SRS1");
    }

    [Fact]
    public void An_arm_whose_runner_died_without_arm_done_stops_the_chain_naming_the_resume_command()
    {
        var here = Facts(Part6, Is("C1", each => each with { Alive = true, Ran = true }), Is("CR1", each => each with { Ran = true }), Is("SRS1", each => each with { Alive = true, Ran = true })) with { StartedHere = Started("C1", "CR1", "SRS1") };
        var stopped = Action<ChainAction.StopArmDied>(here);
        stopped.Arm.Should().Be("CR1");
        stopped.ExitCode.Should().Be(5);

        var again = here with { StartedHere = Started() };
        Action<ChainAction.StopArmDied>(again).Arm.Should().Be("CR1", "a chain started again reads the same fact: the arm Ran and is gone");

        var waiting = Facts(Part6, Is("C1", each => each with { Alive = true, Ran = true }), Is("CR1", each => each with { Ran = true })) with { StartedHere = Started("C1", "CR1") };
        Action<ChainAction.StopArmDied>(waiting).Arm.Should().Be("CR1", "SRS1 is not started while CR1 waits for the operator (E5 §8 item 14)");
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(59, false)]
    [InlineData(60, true)]
    [InlineData(61, true)]
    public void A_just_started_arm_is_awaited_until_its_runner_holds_its_lock_and_exits_5_past_the_startup_ceiling(int startingSeconds, bool expired)
    {
        ChainWaitCeiling.StartupCeiling.Should().Be(TimeSpan.FromSeconds(60));
        var starting = Facts(Part6, Is("C1", each => each with { Alive = true, Ran = true }), Is("SRS1", each => each with { Alive = true, Ran = true })) with { StartedHere = Started("C1", "CR1", "SRS1"), StartingFor = TimeSpan.FromSeconds(startingSeconds) };

        var action = Action<ChainAction.AwaitStart>(starting);

        action.Arms.Should().Equal("CR1");
        action.Expired.Should().Be(expired);
        action.ExitCode.Should().Be(expired ? 5 : null);
    }

    [Fact]
    public void A_started_arm_that_took_its_lock_is_waited_for_and_one_that_ran_and_is_gone_is_a_death_never_a_start()
    {
        var started = Started("C1", "CR1", "SRS1");

        var alive = Facts(Part6, Is("C1", each => each with { Alive = true, Ran = true }), Is("CR1", each => each with { Alive = true, Ran = true }), Is("SRS1", each => each with { Alive = true, Ran = true })) with { StartedHere = started };
        var waiting = Action<ChainAction.WaitFor>(alive);
        waiting.Arm.Should().Be("C1", "the first alive arm that is not done");
        waiting.ExitCode.Should().BeNull();

        var died = Facts(Part6, Is("C1", each => each with { Alive = true, Ran = true }), Is("CR1", each => each with { Ran = true }), Is("SRS1", each => each with { Alive = true, Ran = true })) with { StartedHere = started, StartingFor = TimeSpan.FromSeconds(3) };
        Action<ChainAction.StopArmDied>(died).Arm.Should().Be("CR1");

        var done = Facts(Part6, [.. Done("CR1"), Is("C1", each => each with { Alive = true, Ran = true }), Is("SRS1", each => each with { Alive = true, Ran = true })]) with { StartedHere = started };
        Action<ChainAction.WaitFor>(done).Arm.Should().Be("C1", "a done arm is not waited for and not started again");
    }

    [Fact]
    public void A_finished_round_starts_the_next()
    {
        var facts = Facts(Part6, Done("C1", "CR1", "SRS1")) with { StartedHere = Started("C1", "CR1", "SRS1") };

        Action<ChainAction.Start>(facts).Arms.Should().Equal("C2", "CR2", "SRS2");
    }

    [Fact]
    public void The_next_round_waits_for_the_last_rounds_done_runners_to_exit_before_it_starts()
    {
        var both = Facts(Part6, Is("C1", each => each with { Done = true, Alive = true, Ran = true }), Is("CR1", each => each with { Done = true, Alive = true, Ran = true }), Is("SRS1", each => each with { Done = true, Ran = true })) with { StartedHere = Started("C1", "CR1", "SRS1") };

        var waiting = Action<ChainAction.AwaitExit>(both);
        waiting.Arms.Should().Equal("C1", "CR1");
        waiting.Expired.Should().BeFalse();
        waiting.ExitCode.Should().BeNull();

        var expired = Action<ChainAction.AwaitExit>(both with { AwaitedFor = Grace });
        expired.Expired.Should().BeTrue();
        expired.ExitCode.Should().Be(5);

        var gone = Facts(Part6, Done("C1", "CR1", "SRS1"));
        Action<ChainAction.Start>(gone).Arms.Should().Equal("C2", "CR2", "SRS2");
    }

    [Fact]
    public void A_round_is_waited_for_at_most_its_arms_own_walls_then_exits_5()
    {
        using var root = new ExperimentRoot();
        var experiment = root.ExperimentOf();
        var limits = experiment.Limits;
        var maximum = TimeSpan.FromDays(49);

        // (a) the formula, computed here from the file's own numbers: per step minutes·60 + grace + tries·(wait + floor); a gate adds its tries·waits and the lock's queue wait; plus the startup ceiling.
        long Step(string id) => (experiment.Steps[new StepId(id)].Minutes * 60L) + limits.KillGraceSeconds + (limits.Tries * (limits.WaitSeconds + (long)limits.MinimumRemainingSeconds));
        long Gate(int queue) => (limits.GateTries * (long)limits.GateWaitSeconds) + (Math.Max(1, queue - 1) * Step("gate")) + 60;
        var c = new[] { "code", "review", "fix", "gate" }.Sum(Step) + Gate(3);
        var srs = new[] { "split", "splitFix", "hreview", "review", "fix", "gate" }.Sum(Step) + (experiment.Tasks.Max * Step("taskCode")) + Gate(3);
        var cr = new[] { "code", "hreview", "review", "fix", "gate" }.Sum(Step) + Gate(3);

        ChainWaitCeiling.OfArm(experiment, "C1", 9, maximum).Should().Be(TimeSpan.FromSeconds(c));
        ChainWaitCeiling.OfArm(experiment, "SRS1", 9, maximum).Should().Be(TimeSpan.FromSeconds(srs), "a taskCode step is counted tasks.max times");

        // (b) a stranger's minutes: clamped, never wrapped.
        var huge = experiment with { Steps = experiment.Steps.ToDictionary(pair => pair.Key, pair => pair.Value with { Minutes = int.MaxValue }) };
        ChainWaitCeiling.OfArm(huge, "SRS1", 9, GateStep.MaxLockCeiling).Should().Be(GateStep.MaxLockCeiling);

        // (c) a round is its longest arm.
        ChainWaitCeiling.OfRound(experiment, ["C1", "CR1", "SRS1"], 9, maximum).Should().Be(TimeSpan.FromSeconds(new[] { c, cr, srs }.Max()));

        // (d) the plan: below the ceiling the wait goes on, at it the chain ends.
        var alive = Facts(Part6, Is("C1", each => each with { Alive = true, Ran = true }), Is("CR1", each => each with { Done = true, Ran = true }), Is("SRS1", each => each with { Done = true, Ran = true })) with { StartedHere = Started("C1", "CR1", "SRS1"), WaitCeiling = TimeSpan.FromSeconds(c) };
        var below = Action<ChainAction.WaitFor>(alive with { WaitedFor = TimeSpan.FromSeconds(c - 1) });
        (below.Arm, below.Expired, below.ExitCode).Should().Be(("C1", false, null));
        var at = Action<ChainAction.WaitFor>(alive with { WaitedFor = TimeSpan.FromSeconds(c) });
        (at.Arm, at.Expired, at.ExitCode).Should().Be(("C1", true, 5));
    }

    [Fact]
    public void Unlock_precedes_every_check()
    {
        var done = Facts(Part6, AllDone(Part6));
        Action<ChainAction.Unlock>(done);

        Action<ChainAction.Check>(done with { Unlocked = true }).Arm.Should().Be("C1");
    }

    [Theory]
    [InlineData("one arm not done", typeof(ChainAction.Start))]
    [InlineData("all done, one of the run alive", typeof(ChainAction.AwaitExit))]
    [InlineData("all done, an other arm alive", typeof(ChainAction.StopOtherRunAlive))]
    [InlineData("all done, none alive", typeof(ChainAction.Unlock))]
    public void Unlock_is_reached_only_when_every_arm_of_the_run_is_done_and_none_of_the_map_is_alive(string row, Type expected)
    {
        var facts = row switch
        {
            "one arm not done" => Facts(Part6, [.. AllDone(Part6).Where(each => each.Arm != "SRS3")]),
            "all done, one of the run alive" => Facts(Part6, [.. AllDone(Part6).Where(each => each.Arm != "CR3"), Is("CR3", each => each with { Done = true, Alive = true, Ran = true })]),
            "all done, an other arm alive" => Facts(Part7, AllDone(Part7)) with { OthersAlive = ["C1"] },
            _ => Facts(Part6, AllDone(Part6)),
        };

        ChainPlan.Next(facts).Should().BeOfType(expected, row);
    }

    [Fact]
    public void A_restarted_chain_skips_every_marked_stage()
    {
        var everyArmDone = Facts(Part6, AllDone(Part6)) with { Snapshot = new LockState.Applied("part6", "2026-11-01T10:00:00Z", Digest) };
        Action<ChainAction.Unlock>(everyArmDone);

        var checking = everyArmDone with { Unlocked = true, Arms = [.. everyArmDone.Arms.Select(arm => arm with { Checked = arm.Arm is "C1" or "CR1" })] };
        Action<ChainAction.Check>(checking).Arm.Should().Be("SRS1", "the third arm is the first one not checked");

        ChainPlan.Next(checking with { ArmsDone = true }).Should().BeOfType<ChainAction.Finished>();
    }

    [Fact]
    public void Two_runs_on_one_root_keep_their_own_stage_markers_and_share_the_per_arm_ones()
    {
        var part7 = Facts(Part7, [.. Part7.Rounds.SelectMany(round => round).Select(arm => Is(arm, each => each with { Made = false }))]) with { Locked = false };
        Action<ChainAction.Make>(part7).Arm.Should().Be("CHH1", "part7's own markers decide, whatever part6's three say");

        var paths = new RootPaths("/r");
        paths.RunMarker("part6", RunStage.Locked).Should().NotBe(paths.RunMarker("part7", RunStage.Locked));
        paths.ChainLock("part6").Should().NotBe(paths.ChainLock("part7"));
        paths.Made("C1").Should().NotContain("part", "an arm is made once whatever run names it");
        paths.ObjectiveMarker("C1").Should().NotContain("part");
    }

    [Fact]
    public void Unlock_waits_for_this_runs_done_runners_to_exit_and_exits_5_when_one_outlives_the_grace()
    {
        var facts = Facts(Part6, [.. AllDone(Part6).Where(each => each.Arm != "C1"), Is("C1", each => each with { Done = true, Alive = true, Ran = true })]);

        var waiting = Action<ChainAction.AwaitExit>(facts);
        waiting.Arms.Should().Equal("C1");
        (waiting.Expired, waiting.ExitCode).Should().Be((false, null));

        var expired = Action<ChainAction.AwaitExit>(facts with { AwaitedFor = Grace });
        expired.Arms.Should().Equal("C1");
        (expired.Expired, expired.ExitCode).Should().Be((true, 5));
    }

    [Fact]
    public void Every_arm_of_the_run_done_and_an_arm_of_another_run_alive_exits_5_leaving_the_isolation_applied()
    {
        var facts = Facts(Part7, AllDone(Part7)) with { OthersAlive = ["C1"] };

        var stop = Action<ChainAction.StopOtherRunAlive>(facts);

        stop.Arm.Should().Be("C1");
        stop.ExitCode.Should().Be(5);
    }

    public static TheoryData<string> Snapshots() => new() { "none", "another run", "another digest", "applying", "inconsistent" };

    [Theory]
    [MemberData(nameof(Snapshots))]
    public void A_locked_marker_whose_runs_snapshot_is_not_applied_is_refused_never_skipped(string which)
    {
        LockState snapshot = which switch
        {
            "none" => new LockState.None(),
            "another run" => new LockState.Applied("part6", "2026-11-01T10:00:00Z", Digest),
            "another digest" => new LockState.Applied("part7", "2026-11-01T10:00:00Z", "other"),
            "applying" => new LockState.Applying("part7", "2026-11-01T10:00:00Z"),
            _ => new LockState.Inconsistent("two files of different runs"),
        };
        var facts = Facts(Part7) with { Snapshot = snapshot };

        var refused = Action<ChainAction.RefuseMarker>(facts);

        refused.ExitCode.Should().Be(4);
        refused.Stage.Should().Be(RunStage.Locked);
        refused.State.Should().Contain(IsolationLine.Of(snapshot), "the state is named");
        (refused.State.Contains("with another closed set", StringComparison.Ordinal)).Should().Be(which == "another digest", "only this run's name over another digest says so");

        ChainPlan.Next(Facts(Part7) with { Digest = null, Snapshot = new LockState.None() }).Should().BeOfType<ChainAction.Start>("no isolation: `locked` stands over none");
        ChainPlan.Next(Facts(Part7) with { Digest = null, Snapshot = new LockState.Applied("part6", "x", Digest) }).Should().BeOfType<ChainAction.RefuseMarker>("a stale snapshot is refused, never run under");
    }

    public static TheoryData<string, int?> Actions() => new()
    {
        { "make", null }, { "lock", null }, { "start", null }, { "died", 5 }, { "await-start", null }, { "await-start-expired", 5 }, { "wait", null }, { "wait-expired", 5 },
        { "await-exit", null }, { "await-exit-expired", 5 }, { "other", 5 }, { "refuse", 4 }, { "unlock", null }, { "check", null }, { "finished", 0 },
    };

    [Theory]
    [MemberData(nameof(Actions))]
    public void Every_action_that_ends_the_chain_names_its_exit_code(string which, int? code)
    {
        ChainAction action = which switch
        {
            "make" => new ChainAction.Make("C1"),
            "lock" => new ChainAction.Lock(),
            "start" => new ChainAction.Start(["C1"]),
            "died" => new ChainAction.StopArmDied("C1"),
            "await-start" => new ChainAction.AwaitStart(["C1"], false),
            "await-start-expired" => new ChainAction.AwaitStart(["C1"], true),
            "wait" => new ChainAction.WaitFor("C1", false),
            "wait-expired" => new ChainAction.WaitFor("C1", true),
            "await-exit" => new ChainAction.AwaitExit(["C1"], false),
            "await-exit-expired" => new ChainAction.AwaitExit(["C1"], true),
            "other" => new ChainAction.StopOtherRunAlive("C1"),
            "refuse" => new ChainAction.RefuseMarker(RunStage.Locked, "none"),
            "unlock" => new ChainAction.Unlock(),
            "check" => new ChainAction.Check("C1"),
            _ => new ChainAction.Finished(),
        };

        action.ExitCode.Should().Be(code);
        typeof(ChainAction).GetNestedTypes().Where(type => type.IsSubclassOf(typeof(ChainAction))).Should().HaveCount(12, "the theory names every case of the closed hierarchy");
    }
}
