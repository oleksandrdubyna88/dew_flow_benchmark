using Bench.Application;
using Bench.Domain;
using Bench.Domain.Retrieval;
using Bench.Domain.Runs;
using Bench.Domain.Splitting;
using Bench.Domain.Targets;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Application;

/// <summary>Putting two compute backends beside each other — the question this benchmark exists for.
/// <para>
/// The numbers are the easy half. What these tests mostly pin is the GUARD: the scope that must hold before
/// two averages mean anything, the runs that are excluded and counted rather than folded in, and the three
/// different refusals that must never collapse into one.
/// </para></summary>
public sealed class ArmComparisonTests
{
    private const string Metric = "Anchor recall";
    private const string Wsl = "wsl-to-wsl/migraphx/R9700";
    private const string Windows = "windows-to-windows/directml/R9700";

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Two_arms_measured_on_one_target_and_suite_are_compared()
    {
        var world = World();
        var wsl = world.Run(Wsl, "polly@v1#aaaa", 0.8);
        var windows = world.Run(Windows, "polly@v1#aaaa", 0.4);

        var view = (await Build(world)).Ok();

        var scope = view.Scopes.Should().ContainSingle().Subject;
        scope.Arms.Select(arm => arm.Arm).Should().BeEquivalentTo([Wsl, Windows]);
        scope.Arms.Single(arm => arm.Arm == Wsl).Average.Should().BeApproximately(0.8, 0.001);
        view.Refusal.Should().BeEmpty("there are two arms in one scope, so there IS a comparison to read");

        _ = (wsl, windows);
    }

    [Fact]
    public async Task Runs_on_different_suites_become_different_comparisons_rather_than_one_refusal()
    {
        var world = World();
        world.Run(Wsl, "polly@v1#aaaa", 0.8);
        world.Run(Windows, "polly@v1#aaaa", 0.4);
        world.Run(Wsl, "aspnet@v1#bbbb", 0.6);

        var view = (await Build(world)).Ok();

        // Partitioned, not refused. Refusing whenever the runs span scopes is correct and useless — the real
        // database spans three suites, so the honest answer would have been a permanent blank.
        view.Scopes.Should().HaveCount(2);
        view.Scopes.First().Arms.Should().HaveCount(2, "the richest scope is listed first");
        view.Scopes.Last().RankingRefusal.Should().Contain("one arm in this scope");
    }

    [Fact]
    public async Task Nothing_crosses_a_scope_boundary()
    {
        var world = World();
        world.Run(Wsl, "polly@v1#aaaa", 1.0);
        world.Run(Wsl, "aspnet@v1#bbbb", 0.0);

        var view = (await Build(world)).Ok();

        // The same arm in two scopes is two readings, never one average of 0.5. Mixing the scope is this
        // project's most expensive recorded mistake, and an average across it would be exactly that mistake
        // with a tidier presentation.
        view.Scopes.Should().HaveCount(2);
        view.Scopes.SelectMany(scope => scope.Arms).Select(arm => arm.Average)
            .Should().BeEquivalentTo([1.0, 0.0]);
    }

    [Fact]
    public async Task A_run_that_declared_no_backend_is_excluded_and_COUNTED()
    {
        var world = World();
        world.Run(Wsl, "polly@v1#aaaa", 0.8);
        world.Run(BackendDeclaration.None, "polly@v1#aaaa", 0.9);

        var view = (await Build(world)).Ok();

        // Folding it in would attach a measurement to hardware nobody showed did the work. Dropping it
        // silently would make the population unknowable — so it is named.
        view.Scopes.Single().Arms.Should().ContainSingle().Which.Arm.Should().Be(Wsl);
        view.Excluded.Should().ContainSingle().Which.Should().Contain("1 run(s) declared no compute backend");
    }

    [Fact]
    public async Task No_backend_anywhere_says_the_engine_must_ECHO_rather_than_that_nothing_ran()
    {
        var world = World();
        world.Run(BackendDeclaration.None, "polly@v1#aaaa", 0.9);

        var view = (await Build(world)).Ok();

        // The actionable sentence. Every run in the real database is in this state, and "no arm to compare"
        // alone would send the reader looking for missing runs instead of a sidecar that says nothing.
        view.Refusal.Should().Contain("has to ECHO");
        view.Scopes.Should().BeEmpty();
    }

    [Fact]
    public async Task One_arm_everywhere_is_a_different_refusal_from_none_declared()
    {
        var world = World();
        world.Run(Wsl, "polly@v1#aaaa", 0.8);
        world.Run(Wsl, "polly@v1#aaaa", 0.7);

        var view = (await Build(world)).Ok();

        // "Nothing echoed" and "everything echoed the same thing" are opposite pieces of news: one is a
        // wiring problem, the other means go and measure the other backend.
        view.Refusal.Should().Contain("declared the SAME one");
        view.Refusal.Should().NotContain("has to ECHO");
    }

    [Fact]
    public async Task An_arm_is_told_how_many_RUNS_it_rests_on_and_not_only_how_many_legs()
    {
        var world = World();
        world.Run(Wsl, "polly@v1#aaaa", 0.8);
        world.Run(Wsl, "polly@v1#aaaa", 0.6);
        world.Run(Windows, "polly@v1#aaaa", 0.4);

        var view = (await Build(world)).Ok();

        // An average over one run and an average over five are different evidence about a backend, and a
        // reader holding only the mean cannot tell them apart.
        view.Scopes.Single().Arms.Single(arm => arm.Arm == Wsl).Runs.Should().Be(2);
        view.Scopes.Single().Arms.Single(arm => arm.Arm == Windows).Runs.Should().Be(1);
    }

    [Fact]
    public async Task A_stated_baseline_gives_the_others_a_verdict()
    {
        var world = World();
        world.Run(Wsl, "polly@v1#aaaa", 0.9);
        world.Run(Windows, "polly@v1#aaaa", 0.3);

        var view = (await Build(world, baseline: Windows)).Ok();

        var wsl = view.Scopes.Single().Arms.Single(arm => arm.Arm == Wsl);
        wsl.Proof.Should().Be(ProofState.Confirmed, "it won on both halves against the stated baseline");
        wsl.Margin.Should().BeApproximately(0.6, 0.001);
    }

    [Fact]
    public async Task Without_a_baseline_nothing_is_a_winner_rather_than_the_top_score_becoming_one()
    {
        var world = World();
        world.Run(Wsl, "polly@v1#aaaa", 0.9);
        world.Run(Windows, "polly@v1#aaaa", 0.3);

        var view = (await Build(world)).Ok();

        // A comparison never nominates a baseline by score: an arm that is the baseline BECAUSE it won
        // cannot then be beaten, and the verdict would be circular.
        view.Scopes.Single().Arms.Should().OnlyContain(arm => arm.Proof == ProofState.NotAWinner);
        view.Scopes.Single().Baseline.Should().BeEmpty();
    }

    [Fact]
    public async Task A_baseline_this_scope_never_measured_is_not_a_baseline()
    {
        var world = World();
        world.Run(Wsl, "polly@v1#aaaa", 0.9);
        world.Run(Windows, "polly@v1#aaaa", 0.3);

        var view = (await Build(world, baseline: "wsl-to-windows/directml/890M")).Ok();

        // Otherwise every arm silently goes unjudged, which reads as "nothing won" rather than as "the
        // baseline you named is not in this comparison".
        view.Scopes.Single().Baseline.Should().BeEmpty();
    }

    [Fact]
    public async Task An_arm_with_too_few_legs_shows_its_average_and_withholds_the_RANKING()
    {
        var world = World();
        world.Run(Wsl, "polly@v1#aaaa", 0.8, questions: 4);
        world.Run(Windows, "polly@v1#aaaa", 0.4, questions: 1);

        var view = (await Build(world, baseline: Wsl)).Ok();

        var scope = view.Scopes.Single();
        scope.RankingRefusal.Should().Contain("1 leg(s)").And.Contain("below the 2");
        scope.Arms.Should().HaveCount(2, "the averages are present either way — only the ranking is withheld");
    }

    [Fact]
    public async Task A_report_without_a_metric_is_refused_rather_than_given_a_default()
    {
        var world = World();
        world.Run(Wsl, "polly@v1#aaaa", 0.8);

        var refused = await ArmComparison.BuildAsync(
            world, world, new ArmComparisonRequest(string.Empty), Ct);

        refused.Should().BeOfType<Outcome<ArmComparisonView>.Fail>()
            .Which.Reason.Should().Contain("name the metric");
    }

    [Fact]
    public async Task Both_halves_are_derived_per_leg_so_an_unmeasured_half_is_not_a_zero()
    {
        var world = World();

        // One question only. Whichever half the hash puts it in, the OTHER half has no leg at all.
        world.Run(Wsl, "polly@v1#aaaa", 0.8, questions: 1);
        world.Run(Windows, "polly@v1#aaaa", 0.8, questions: 1);

        var arm = (await Build(world)).Ok().Scopes.Single().Arms.First();

        (arm.Selection.Measured ^ arm.HeldOut.Measured).Should().BeTrue(
            "exactly one half holds the single question; the other must say UNMEASURED rather than 0");
    }

    [Fact]
    public async Task The_offered_metrics_are_the_ones_the_ARMS_carry_not_a_catalog()
    {
        var world = World();
        world.Run(Wsl, "polly@v1#aaaa", 0.8);

        var offered = await ArmComparison.MetricsAsync(world, world, 50, Ct);

        // A published list cannot know what a population measured. Offering the fix-lane or tool-lane names
        // beside a set of retrieval runs sends the reader to an empty table, which reads as a broken run
        // rather than as a metric nobody recorded.
        offered.Should().ContainSingle().Which.Should().Be("Anchor recall");
    }

    [Fact]
    public async Task A_run_that_declared_no_backend_contributes_no_metric_to_the_offer()
    {
        var world = World();
        world.Run(BackendDeclaration.None, "polly@v1#aaaa", 0.9);

        var offered = await ArmComparison.MetricsAsync(world, world, 50, Ct);

        // The offer is scoped to the same population the comparison folds. A metric only an unattributable
        // run carries would open a table this page can never fill.
        offered.Should().BeEmpty();
    }

    [Fact]
    public async Task Retrieval_time_is_a_MEAN_and_wall_time_a_SUM()
    {
        var world = World();
        world.Run(Wsl, "polly@v1#aaaa", 0.8, questions: 4);
        world.Run(Windows, "polly@v1#aaaa", 0.4, questions: 2);

        var arms = (await Build(world)).Ok().Scopes.Single().Arms;

        // Every leg costs 100 ms and 2 s in the double. A MEAN of retrieval keeps the two arms comparable
        // when one ran twice the legs; a SUM would rank whichever arm ran more, which is not a property of
        // the backend. Wall time is the opposite question -- how long this arm occupied the machine -- so it
        // sums.
        arms.Should().OnlyContain(arm => arm.Cost.RetrievalMs == 100);
        arms.Single(arm => arm.Arm == Wsl).Cost.WallSeconds.Should().Be(8);
        arms.Single(arm => arm.Arm == Windows).Cost.WallSeconds.Should().Be(4);
    }

    [Fact]
    public async Task A_vram_peak_carries_the_sample_count_that_makes_it_readable()
    {
        var world = World();
        world.Run(Wsl, "polly@v1#aaaa", 0.8, questions: 3);
        world.Run(Windows, "polly@v1#aaaa", 0.4, questions: 3);

        var wsl = (await Build(world)).Ok().Scopes.Single().Arms.Single(arm => arm.Arm == Wsl);

        // The peak is the highest any leg saw, never a mean -- and the count is what separates a peak
        // resting on one reading from one resting on thirty. Zero samples would be "not sampled", which a
        // bare 0 could never say.
        wsl.Cost.PeakVramBytes.Should().Be(1073741824);
        wsl.Cost.VramSamples.Should().Be(3, "three legs each carried one reading");
    }

    [Fact]
    public async Task Arms_measured_on_two_DIFFERENT_machines_have_the_difference_named()
    {
        var world = World();
        world.Run(Wsl, "polly@v1#aaaa", 0.8, machine: Machine("bench-01"));
        world.Run(Windows, "polly@v1#aaaa", 0.4, machine: Machine("bench-02"));

        var scope = (await Build(world)).Ok().Scopes.Single();

        // The whole point. The target and the suite were never the whole comparison scope: two runs on two
        // machines differ for a reason that has nothing to do with the backend, and folding them into one
        // table without a word is the silent merge MachineFacts exists to end.
        scope.Machines.State.Should().Be(MachineConsensus.SeveralMachines);
        scope.Machines.Describe.Should().Contain("DIFFERENT machines");
        scope.Machines.OnOneMachine.Should().BeFalse();

        // Named, never REFUSED. A fingerprint is a fingerprint and not a gate: a harness that blocked a
        // comparison spanning a hardware change could not span a driver update, which is the ordinary life
        // of a benchmark that runs for months. Both arms and their averages stay exactly where they were.
        scope.Arms.Should().HaveCount(2);
        scope.Arms.Single(arm => arm.Arm == Wsl).Average.Should().BeApproximately(0.8, 0.001);
    }

    [Fact]
    public async Task Arms_measured_on_ONE_machine_say_so_rather_than_staying_silent()
    {
        var world = World();
        world.Run(Wsl, "polly@v1#aaaa", 0.8, machine: Machine("bench-01"));
        world.Run(Windows, "polly@v1#aaaa", 0.4, machine: Machine("bench-01"));

        var scope = (await Build(world)).Ok().Scopes.Single();

        // Silence would read as agreement either way, so the clean case is stated too — and this is the one
        // state under which a gap between the arms may be read as a property of the backend.
        scope.Machines.State.Should().Be(MachineConsensus.OneMachine);
        scope.Machines.OnOneMachine.Should().BeTrue();
    }

    [Fact]
    public async Task A_comparison_over_runs_nobody_probed_says_NOT_RECORDED_rather_than_one_machine()
    {
        var world = World();
        world.Run(Wsl, "polly@v1#aaaa", 0.8);
        world.Run(Windows, "polly@v1#aaaa", 0.4);

        var scope = (await Build(world)).Ok().Scopes.Single();

        // Every run measured before the machine probe existed is in this state, and it is the state most
        // likely to be rendered as agreement: two unrecorded runs share an empty fingerprint.
        scope.Machines.State.Should().Be(MachineConsensus.NotRecorded);
        scope.Machines.Describe.Should().Contain("no run here recorded the machine");
    }

    [Fact]
    public async Task An_ARM_folded_across_two_machines_carries_that_beside_its_average()
    {
        var world = World();
        world.Run(Wsl, "polly@v1#aaaa", 0.9, machine: Machine("bench-01"));
        world.Run(Wsl, "polly@v1#aaaa", 0.1, machine: Machine("bench-02"));
        world.Run(Windows, "polly@v1#aaaa", 0.4, machine: Machine("bench-01"));

        var arms = (await Build(world)).Ok().Scopes.Single().Arms;

        // A mean of 0.5 over two machines and a mean of 0.5 over one are different evidence about a backend.
        // The scope-level reading cannot say which arm is the mixed one, so the arm carries its own.
        arms.Single(arm => arm.Arm == Wsl).Machines.State.Should().Be(MachineConsensus.SeveralMachines);
        arms.Single(arm => arm.Arm == Windows).Machines.State.Should().Be(MachineConsensus.OneMachine);
    }

    [Fact]
    public async Task A_machine_that_contributed_no_LEG_to_this_metric_does_not_raise_the_warning()
    {
        var world = World();
        world.Run(Wsl, "polly@v1#aaaa", 0.8, machine: Machine("bench-01"));
        world.Run(Windows, "polly@v1#aaaa", 0.4, machine: Machine("bench-01"));

        // A third run on a second machine that measured a DIFFERENT metric. It is in the scope and it
        // declared an arm, but no number on screen came off it.
        world.Silent(Wsl, "polly@v1#aaaa", machine: Machine("bench-02"));

        var scope = (await Build(world)).Ok().Scopes.Single();

        // A guard that fires over a machine behind none of the numbers is a false alarm, and a false alarm
        // is how a real warning stops being read. The population is the legs, which is what the averages,
        // the leg count and the run count are all already derived from.
        scope.Machines.State.Should().Be(MachineConsensus.OneMachine);
        scope.Arms.Single(arm => arm.Arm == Wsl).Machines.State.Should().Be(MachineConsensus.OneMachine);
    }

    // ---- scaffolding -------------------------------------------------------------------------------

    /// <summary>Two machines that differ in nothing a listing would show but their identity — which is the
    /// case a comparison is most likely to fold in silence.</summary>
    private static MachineFacts Machine(string hostname) => new()
    {
        Hostname = hostname,
        MachineId = $"{hostname}-id",
        Os = new OsFacts("windows", "Professional", "25H2", "10.0.26200.8653"),
        Cpu = new CpuFacts("AMD Ryzen 9", 12, 24, "High performance"),
        TotalRamBytes = 96L * 1024 * 1024 * 1024,
    };

    private Task<Outcome<ArmComparisonView>> Build(ArmWorld world, string baseline = "") =>
        ArmComparison.BuildAsync(world, world, new ArmComparisonRequest(Metric, Baseline: baseline), Ct);

    private static ArmWorld World() => new();
}

/// <summary>Runs and their legs, held as values. Both ports on one object because a comparison reads runs and
/// results together and a test that scripted them apart could describe a world where they disagree.</summary>
internal sealed class ArmWorld : IRunStore, IResultStore
{
    private readonly List<BenchRun> _runs = [];
    private readonly List<MetricLeg> _legs = [];
    private readonly Dictionary<Guid, Bench.Domain.Trace.MachineFacts> _machines = [];

    public BenchRun Run(
        string arm,
        string suiteStamp,
        double value,
        int questions = 4,
        Bench.Domain.Trace.MachineFacts? machine = null) =>
        Run(BackendDeclaration.Read(arm), suiteStamp, value, questions, machine);

    public BenchRun Run(
        BackendDeclaration backend,
        string suiteStamp,
        double value,
        int questions = 4,
        Bench.Domain.Trace.MachineFacts? machine = null)
    {
        var run = BenchRun.Planned(
            "arms",
            MeasurementTarget.At(
                RepoUrl.Parse("https://example.invalid/x.git").Ok(),
                CommitSha.Parse(new string('c', 40)).Ok()),
            new EngineRef(EngineKind.Qln, "http://localhost:5080", "1.0", "fp") { Backend = backend },
            suiteStamp,
            new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero));

        _runs.Add(run);
        _legs.AddRange(Enumerable.Range(1, questions).Select(i => new MetricLeg(run.Id, $"q{i}", value)));

        // Unprobed unless a test says otherwise — which is what every run stored before the machine probe
        // existed is, and therefore the default a comparison must handle.
        _machines[run.Id] = machine ?? Bench.Domain.Trace.MachineFacts.NotRecorded;

        return run;
    }

    /// <summary>A run that is in the scope and declared an arm but scored NO leg on the metric being
    /// compared — it measured something else. Real databases are full of them.</summary>
    public BenchRun Silent(string arm, string suiteStamp, Bench.Domain.Trace.MachineFacts machine) =>
        Run(arm, suiteStamp, value: 0, questions: 0, machine: machine);

    public Task<IReadOnlyList<BenchRun>> RecentAsync(int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BenchRun>>([.. _runs.Take(limit)]);

    public Task<IReadOnlyList<MetricLeg>> LegsAsync(
        IReadOnlyList<Guid> runIds, string metric, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MetricLeg>>(
            metric == "Anchor recall" ? [.. _legs.Where(leg => runIds.Contains(leg.RunId))] : []);

    /// <summary>Only what the scripted legs carry, so a console offering a catalog can be told apart from one
    /// offering what was measured.</summary>
    public Task<IReadOnlyList<string>> MetricNamesAsync(
        IReadOnlyList<Guid> runIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(
            _legs.Any(leg => runIds.Contains(leg.RunId)) ? ["Anchor recall"] : []);

    /// <summary>One vitals row per leg. Values chosen so a fold is checkable: every leg costs 100 ms and 2 s,
    /// surfaces 5 hits across 3 files, and carries one 1 GiB accelerator reading.</summary>
    public Task<IReadOnlyList<LegVitals>> VitalsAsync(
        IReadOnlyList<Guid> runIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<LegVitals>>(
        [
            .. _legs.Where(leg => runIds.Contains(leg.RunId))
                .Select(leg => new LegVitals(leg.RunId, 100, 2, 5, 3, 1073741824, 1)),
        ]);

    // Everything else. A comparison reads recent runs and their scored legs; a double that quietly answered
    // more would hide one that had started doing more.
    public Task<Outcome<BenchRun>> LoadAsync(Guid runId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("an arm comparison reads the recent list, never one run by id");

    public Task<IReadOnlyList<string>> QuestionIdsAsync(Guid runId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("the halves come from the LEGS, because an arm spans runs");

    public Task RecordMachineAsync(Guid runId, Bench.Domain.Trace.MachineFacts facts, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a comparison records nothing");

    public Task<Bench.Domain.Trace.MachineFacts> MachineAsync(Guid runId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a comparison reads machines in a BATCH — one query, not one per run");

    /// <summary>Every requested run answers, unprobed ones included. A double that omitted them would let a
    /// comparison pass by treating an absent key as agreement — the exact silence being fixed.</summary>
    public Task<IReadOnlyDictionary<Guid, Bench.Domain.Trace.MachineFacts>> MachinesAsync(
        IReadOnlyList<Guid> runIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, Bench.Domain.Trace.MachineFacts>>(
            runIds.Distinct().ToDictionary(
                id => id,
                id => _machines.GetValueOrDefault(id, Bench.Domain.Trace.MachineFacts.NotRecorded)));

    public Task<Outcome<BenchRun>> CreateAsync(BenchRun run, IReadOnlyList<RunCell> cells, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a comparison creates no runs");

    public Task<Outcome<RunCell>> ClaimNextAsync(Guid runId, WorkerIdentity owner, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a comparison claims no cells");

    public Task<Outcome<RunCell>> SettleAsync(Guid cellId, WorkerIdentity owner, LegOutcome outcome, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a comparison settles no cells");

    public Task<SweepReport> SweepAsync(TimeSpan staleAfter, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a comparison does not sweep");

    public Task<Outcome<RunCell>> CellAsync(Guid cellId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a comparison reads legs, never one cell");

    public Task<Outcome<RunStatus>> AdvanceStatusAsync(Guid runId, RunStatus to, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a comparison advances nothing");

    public Task<IReadOnlyList<LegPhase>> EnsurePhasesAsync(Guid cellId, IReadOnlyList<LegPhase> phases, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a comparison runs no phases");

    public Task<Outcome<int>> SavePhasesAsync(IReadOnlyList<LegPhase> phases, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a comparison runs no phases");

    public Task<IReadOnlyList<LegPhase>> PhasesAsync(Guid cellId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a comparison reads no phases");

    public Task<RunProgress> ProgressAsync(Guid runId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a comparison reads results, not progress");

    public Task<IReadOnlyList<MetricByDimension>> AverageByAsync(
        Guid runId, ReportDimension dimension, string metric, QuestionScope scope, CancellationToken cancellationToken) =>
        throw new NotSupportedException("that is the single-run report's aggregate");

    public Task<IReadOnlyList<QuestionPassRate>> PassRateByQuestionAndSubjectAsync(
        Guid runId, string metric, CancellationToken cancellationToken) =>
        throw new NotSupportedException("that is the single-run report's discrimination");

    public Task<IReadOnlyList<Bench.Domain.Trace.LegLoad>> LoadsAsync(Guid runId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a comparison does not read the sampler yet");

    public Task<RunScoreboard> ScoreboardAsync(Guid runId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a comparison counts legs per arm, not per run");

    public Task<Outcome<LegResult>> SaveAsync(LegResult result, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a comparison writes no results");

    public Task<SnippetPruning> PruneHitSnippetsAsync(DateTimeOffset olderThan, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a comparison prunes nothing");

    public Task<bool> HasResultAsync(Guid cellId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a comparison does not re-enter a leg");

    public Task<IReadOnlyList<LegResult>> ForRunAsync(Guid runId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a comparison aggregates rather than hydrating — that is the point");

    public Task<IReadOnlyList<JudgeableLeg>> WithoutMetricAsync(Guid runId, string metric, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a comparison is not the judge lane");

    public Task<Outcome<int>> AppendMetricsAsync(Guid resultId, IReadOnlyList<StoredMetric> metrics, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a comparison appends no metrics");
}
