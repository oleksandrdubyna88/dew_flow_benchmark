using Bench.Domain.Retrieval;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The durability guarantees, against a real database.
/// <para>
/// Three of these tests are the reason the store exists at all: a run is written whole before any work
/// starts, exactly one worker can own a cell however many race for it, and a cell stranded by a dead host
/// comes back — but only so many times.
/// </para>
/// <para>
/// <b>The store is shared and the sweep has no run filter</b> — it is a database-wide repair, deliberately,
/// because a dead host strands cells across every run it touched. So no assertion here counts what the
/// SWEEP did globally: that number includes the cells other tests stranded in the same container, and an
/// assertion about it passes once and fails forever after. Every guarantee is stated about THIS run's
/// cells, through <c>ProgressAsync(run.Id)</c>.
/// </para></summary>
[Collection("postgres")]
public sealed class PostgresRunStoreTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_run_is_written_whole_with_every_cell_before_anything_starts()
    {
        var (run, cells) = Plan(questions: 3, repeats: 2, lanes: 2);
        var store = postgres.NewStore(new TestClock(Noon));

        (await store.CreateAsync(run, cells, Ct)).Ok().Id.Should().Be(run.Id);

        var progress = await store.ProgressAsync(run.Id, Ct);
        progress.Total.Should().Be(12);
        progress.Pending.Should().Be(12, "every cell is durable before the first one is attempted");
        progress.IsFinished.Should().BeFalse();
    }

    [Fact]
    public async Task A_stored_run_can_prove_what_it_measured()
    {
        var (run, cells) = Plan(1, 1, 1);
        var store = postgres.NewStore(new TestClock(Noon));
        await store.CreateAsync(run, cells, Ct);

        var loaded = (await store.LoadAsync(run.Id, Ct)).Ok();

        loaded.Target.Canonical.Should().Be(run.Target.Canonical, "including the exclusions — a commit read with different blind spots is a different measurement");
        loaded.Engine.Canonical.Should().Be(run.Engine.Canonical);
        loaded.SuiteStamp.Should().Be(run.SuiteStamp);
        loaded.Scope.Should().Be(run.Scope);
    }

    [Fact]
    public async Task The_arm_an_engine_served_on_survives_the_store()
    {
        var (planned, cells) = Plan(1, 1, 1);
        var run = planned with
        {
            Engine = new EngineRef(EngineKind.Qln, "http://localhost:5080", "1.0", "fp")
            {
                Backend = BackendDeclaration.Read("wsl/migraphx/R9700"),
            },
        };

        var store = postgres.NewStore(new TestClock(Noon));
        await store.CreateAsync(run, cells, Ct);

        var loaded = (await store.LoadAsync(run.Id, Ct)).Ok();

        // Without this column the echo is compared at plan time and then lost, so a report months later
        // cannot group by the arm at all — the two sidecars measured on 2026-08-18 would be one row again,
        // which is the whole defect this axis exists to end.
        loaded.Engine.Backend.Describe.Should().Be("wsl/migraphx/R9700");
        loaded.Engine.Canonical.Should().Be(run.Engine.Canonical);
    }

    [Fact]
    public async Task A_run_against_an_engine_that_declared_nothing_loads_as_NOT_DECLARED_rather_than_as_an_arm()
    {
        var (run, cells) = Plan(1, 1, 1);
        var store = postgres.NewStore(new TestClock(Noon));
        await store.CreateAsync(run, cells, Ct);

        // Every run ever stored is in this state, and the empty column they carry must read as "nothing is
        // known" rather than parse into some arm — silence is not agreement.
        (await store.LoadAsync(run.Id, Ct)).Ok().Engine.Backend
            .Should().BeOfType<BackendDeclaration.NotDeclared>();
    }

    [Fact]
    public async Task The_arm_a_cell_runs_survives_the_store()
    {
        var (run, _) = Plan(1, 1, 1);
        var commit = CommitSha.Parse(new string('a', 40)).Ok();
        var question = new Question(
            "q1", "prompt", [Expectation.File(SourceAnchor.File("src/F.cs", commit))], string.Empty);
        var subjects = new[] { new Subject(ModelRef.Parse("m", ModelHosting.Local).Ok(), Sampling.Deterministic(1)) };

        var cells = Matrix.Plan(
                [question], repeats: 1, subjects, [Lane.Named("lane1")],
                [Bench.Domain.Variants.VariantSelection.None], [FixArm.InvestigateOnly]).Ok()
            .Select(c => RunCell.Pending(run.Id, c))
            .ToList();

        var store = postgres.NewStore(new TestClock(Noon));
        await store.CreateAsync(run, cells, Ct);

        var claimed = (await store.ClaimNextAsync(run.Id, WorkerIdentity.Here("worker-arm"), Ct)).Ok();

        // Without this column the arm is compared at plan time and then lost, so a report could never
        // group investigate-only legs against full ones — the split measurement the axis exists for.
        claimed.Arm.Should().Be(FixArm.InvestigateOnly);
        claimed.Leg.Should().EndWith("!investigate-only");
    }

    [Fact]
    public async Task The_machine_a_run_measured_on_survives_the_store()
    {
        var (run, cells) = Plan(1, 1, 1);
        var store = postgres.NewStore(new TestClock(Noon));
        await store.CreateAsync(run, cells, Ct);

        var facts = new MachineFacts
        {
            Hostname = "bench-01",
            Os = new OsFacts("windows", "Professional", "25H2", "10.0.26200.8653"),
            Wsl = new WslFacts("2.7.10.0", "1.611.1-81528511", "10.0.26100.1"),
            Cpu = new CpuFacts("AMD Ryzen AI 9 HX 370", 12, 24, "Balanced"),
            TotalRamBytes = 98_374_103_040,
            Adapters = [new AdapterFacts("AMD Radeon AI PRO R9700", 34_208_743_424, "32.0.31035.1003", "2026-07-24", 0)],
            Volume = new VolumeFacts("D:\\", "NTFS", 4096, 900, 2000),
        };

        await store.RecordMachineAsync(run.Id, facts, Ct);
        var read = await store.MachineAsync(run.Id, Ct);

        read.Fingerprint.Should().Be(facts.Fingerprint, "the fingerprint is what a report compares by");
        read.Os.Build.Should().Be("10.0.26200.8653", "the patch is the half a version without it cannot carry");
        read.Wsl.Direct3D.Should().Be("1.611.1-81528511");
        read.Adapters.Single().VramBytes.Should().Be(34_208_743_424, "the registry size, not WMI's saturated 4 GiB");
    }

    [Fact]
    public async Task A_run_whose_machine_was_never_read_answers_NOT_RECORDED_rather_than_an_empty_machine()
    {
        var (run, cells) = Plan(1, 1, 1);
        var store = postgres.NewStore(new TestClock(Noon));
        await store.CreateAsync(run, cells, Ct);

        // Every run stored before this table existed is in this state, and it is a different fact from a
        // machine that was read and answered nothing.
        (await store.MachineAsync(run.Id, Ct)).Recorded.Should().BeFalse();
    }

    [Fact]
    public async Task The_machine_is_written_ONCE_and_a_later_read_cannot_relabel_a_run_already_under_way()
    {
        var (run, cells) = Plan(1, 1, 1);
        var store = postgres.NewStore(new TestClock(Noon));
        await store.CreateAsync(run, cells, Ct);

        await store.RecordMachineAsync(run.Id, new MachineFacts { Hostname = "bench-01" }, Ct);
        await store.RecordMachineAsync(run.Id, new MachineFacts { Hostname = "somebody-elses-box" }, Ct);

        (await store.MachineAsync(run.Id, Ct)).Hostname.Should().Be("bench-01",
            "the first read describes the machine the run started on; a second would re-label a measurement already in flight");
    }

    [Fact]
    public async Task A_run_with_no_cells_is_refused()
    {
        var (run, _) = Plan(1, 1, 1);

        (await postgres.NewStore(new TestClock(Noon)).CreateAsync(run, [], Ct))
            .Reason().Should().Contain("could never finish");
    }

    [Fact]
    public async Task Many_workers_racing_for_one_cell_produce_exactly_one_winner()
    {
        var (run, cells) = Plan(1, 1, 1);
        await postgres.NewStore(new TestClock(Noon)).CreateAsync(run, cells, Ct);

        var claims = await Task.WhenAll(Enumerable.Range(0, 12).Select(i =>
            postgres.NewStore(new TestClock(Noon)).ClaimNextAsync(run.Id, WorkerIdentity.Here($"worker-{i}"), Ct)));

        claims.Count(c => !c.Failed()).Should().Be(1, "the guard is in the WHERE clause, so only one update can see a row change");
        claims.Where(c => c.Failed()).Should().OnlyContain(c => c.Reason().Contains("no pending cell"));
    }

    [Fact]
    public async Task Parallel_workers_drain_a_queue_without_any_cell_being_done_twice()
    {
        var (run, cells) = Plan(questions: 8, repeats: 2, lanes: 2);
        await postgres.NewStore(new TestClock(Noon)).CreateAsync(run, cells, Ct);

        var claimed = await Task.WhenAll(Enumerable.Range(0, 6).Select(w => DrainAsync(run.Id, $"worker-{w}")));

        var all = claimed.SelectMany(c => c).ToList();
        all.Should().HaveCount(32, "every cell is claimed");
        all.Distinct().Should().HaveCount(32, "and no cell is claimed twice");
        (await postgres.NewStore(new TestClock(Noon)).ProgressAsync(run.Id, Ct)).Pending.Should().Be(0);
    }

    [Fact]
    public async Task Settling_requires_still_holding_the_claim()
    {
        var (run, cells) = Plan(1, 1, 1);
        var store = postgres.NewStore(new TestClock(Noon));
        await store.CreateAsync(run, cells, Ct);
        var cell = (await store.ClaimNextAsync(run.Id, WorkerIdentity.Here("worker-a"), Ct)).Ok();

        var byStranger = await store.SettleAsync(cell.Id, WorkerIdentity.Here("worker-b"), new LegOutcome.Completed(), Ct);
        var byOwner = await store.SettleAsync(cell.Id, WorkerIdentity.Here("worker-a"), new LegOutcome.Completed(), Ct);

        byStranger.Reason().Should().Contain("held by 'worker-a'").And.Contain("must not overwrite the retry");
        byOwner.Ok().State.Should().Be(CellState.Settled);
        (await store.SettleAsync(cell.Id, WorkerIdentity.Here("worker-a"), new LegOutcome.Completed(), Ct))
            .Reason().Should().Contain("is Settled, not Claimed");
    }

    [Fact]
    public async Task A_cell_stranded_by_a_dead_host_is_handed_back()
    {
        var clock = new TestClock(Noon);
        var (run, cells) = Plan(1, 1, 1);
        var store = postgres.NewStore(clock);
        await store.CreateAsync(run, cells, Ct);
        await store.ClaimNextAsync(run.Id, TestWorkers.Dead("host-that-dies"), Ct);

        clock.Now = Noon.AddHours(2);
        await store.SweepAsync(TimeSpan.FromMinutes(30), Ct);

        var progress = await store.ProgressAsync(run.Id, Ct);
        progress.Pending.Should().Be(1, "it is available again");
        progress.Claimed.Should().Be(0, "the host that held it is gone");
        progress.Abandoned.Should().Be(0, "one hand-back is nowhere near the attempt cap");
    }

    [Fact]
    public async Task A_cell_whose_owner_is_still_running_is_not_handed_back()
    {
        var clock = new TestClock(Noon);
        var (run, cells) = Plan(1, 1, 1);
        var store = postgres.NewStore(clock);
        await store.CreateAsync(run, cells, Ct);
        await store.ClaimNextAsync(run.Id, WorkerIdentity.Here("worker-alive"), Ct);

        clock.Now = Noon.AddHours(2);
        await store.SweepAsync(TimeSpan.FromMinutes(30), Ct);

        (await store.ProgressAsync(run.Id, Ct)).Claimed.Should().Be(1,
            "the owner is this very process and it is running — elapsed time is a margin, not a death certificate");
    }

    [Fact]
    public async Task A_cell_claimed_on_another_machine_is_left_for_that_machine_to_sweep()
    {
        var clock = new TestClock(Noon);
        var (run, cells) = Plan(1, 1, 1);
        var store = postgres.NewStore(clock);
        await store.CreateAsync(run, cells, Ct);
        await store.ClaimNextAsync(run.Id, WorkerIdentity.Stored("worker-elsewhere", "some-other-machine", 4242), Ct);

        clock.Now = Noon.AddHours(2);
        await store.SweepAsync(TimeSpan.FromMinutes(30), Ct);

        (await store.ProgressAsync(run.Id, Ct)).Claimed.Should().Be(1,
            "pid 4242 on THIS host is a different process from pid 4242 on that one — ending a live worker's "
            + "leg is a worse error than leaving a stale row for its own host to sweep");
    }

    [Fact]
    public async Task A_fresh_claim_is_left_alone()
    {
        var clock = new TestClock(Noon);
        var (run, cells) = Plan(1, 1, 1);
        var store = postgres.NewStore(clock);
        await store.CreateAsync(run, cells, Ct);
        await store.ClaimNextAsync(run.Id, TestWorkers.Dead("worker-a"), Ct);

        clock.Now = Noon.AddMinutes(5);
        await store.SweepAsync(TimeSpan.FromMinutes(30), Ct);

        (await store.ProgressAsync(run.Id, Ct)).Claimed.Should().Be(1,
            "the window has not passed, so the owner is not even asked about — a worker inside it is working");
    }

    [Fact]
    public async Task A_cell_that_keeps_killing_its_host_is_abandoned_rather_than_requeued_forever()
    {
        var clock = new TestClock(Noon);
        var (run, cells) = Plan(1, 1, 1);
        var store = postgres.NewStore(clock);
        await store.CreateAsync(run, cells, Ct);

        for (var attempt = 1; attempt <= CellLifecycle.MaxAttempts; attempt++)
        {
            (await store.ClaimNextAsync(run.Id, TestWorkers.Dead($"host-{attempt}"), Ct)).Failed().Should().BeFalse();
            clock.Now = clock.Now.AddHours(2);
            await store.SweepAsync(TimeSpan.FromMinutes(30), Ct);
        }

        var progress = await store.ProgressAsync(run.Id, Ct);
        progress.Abandoned.Should().Be(1);
        progress.Pending.Should().Be(0, "an unbounded sweep is a loop that survives reboots");
        progress.Describe.Should().Contain("ABANDONED");

        clock.Now = clock.Now.AddDays(1);
        await store.SweepAsync(TimeSpan.FromMinutes(30), Ct);

        (await store.ProgressAsync(run.Id, Ct)).Should().Be(progress, "abandoned is terminal");
    }

    [Fact]
    public async Task An_outcome_survives_the_round_trip_with_its_kind_intact()
    {
        var (run, cells) = Plan(1, 1, 1);
        var store = postgres.NewStore(new TestClock(Noon));
        await store.CreateAsync(run, cells, Ct);
        var worker = WorkerIdentity.Here("w");
        var cell = (await store.ClaimNextAsync(run.Id, worker, Ct)).Ok();

        var settled = (await store.SettleAsync(
            cell.Id, worker, new LegOutcome.CapExceeded(BudgetKind.Wall, BudgetScope.Phase, 600m, 731m), Ct)).Ok();

        settled.OutcomeKind.Should().Be(LegOutcomeKind.CapExceeded);
        settled.OutcomeDetail.Should().Contain("Wall/Phase").And.Contain("731");
    }

    [Fact]
    public async Task Claiming_a_run_that_does_not_exist_is_an_answer_not_an_exception()
    {
        (await postgres.NewStore(new TestClock(Noon)).ClaimNextAsync(Guid.CreateVersion7(), WorkerIdentity.Here("w"), Ct))
            .Reason().Should().Contain("no pending cell");

        (await postgres.NewStore(new TestClock(Noon)).LoadAsync(Guid.CreateVersion7(), Ct))
            .Reason().Should().Contain("no run");
    }

    private async Task<List<Guid>> DrainAsync(Guid runId, string owner)
    {
        var store = postgres.NewStore(new TestClock(Noon));
        var identity = WorkerIdentity.Here(owner);
        List<Guid> mine = [];

        while (true)
        {
            var claim = await store.ClaimNextAsync(runId, identity, Ct);

            if (claim.Failed())
            {
                return mine;
            }

            mine.Add(claim.Ok().Id);
        }
    }

    private static (BenchRun Run, IReadOnlyList<RunCell> Cells) Plan(int questions, int repeats, int lanes)
    {
        var commit = CommitSha.Parse(new string('a', 40)).Ok();
        var target = MeasurementTarget
            .At(RepoUrl.Parse("https://example.invalid/x.git").Ok(), commit)
            .Excluding("research/**");

        var questionList = Enumerable.Range(1, questions)
            .Select(i => new Question($"q{i}", $"prompt {i}", [Expectation.File(SourceAnchor.File($"src/F{i}.cs", commit))], string.Empty))
            .ToList();

        var subjects = new[] { new Subject(ModelRef.Parse("m", ModelHosting.Local).Ok(), Sampling.Deterministic(1)) };
        var laneList = Enumerable.Range(1, lanes).Select(i => Lane.Named($"lane{i}")).ToList();

        var run = BenchRun.Planned("t", target, EngineRef.Filesystem(), "suite@v1#abc", Noon);
        var cells = Matrix.Plan(questionList, repeats, subjects, laneList).Ok()
            .Select(c => RunCell.Pending(run.Id, c))
            .ToList();

        return (run, cells);
    }
}
