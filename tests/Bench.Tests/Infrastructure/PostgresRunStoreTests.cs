using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The durability guarantees, against a real database.
/// <para>
/// Three of these tests are the reason the store exists at all: a run is written whole before any work
/// starts, exactly one worker can own a cell however many race for it, and a cell stranded by a dead host
/// comes back — but only so many times.
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
            postgres.NewStore(new TestClock(Noon)).ClaimNextAsync(run.Id, $"worker-{i}", Ct)));

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
        var cell = (await store.ClaimNextAsync(run.Id, "worker-a", Ct)).Ok();

        var byStranger = await store.SettleAsync(cell.Id, "worker-b", new LegOutcome.Completed(), Ct);
        var byOwner = await store.SettleAsync(cell.Id, "worker-a", new LegOutcome.Completed(), Ct);

        byStranger.Reason().Should().Contain("held by 'worker-a'").And.Contain("must not overwrite the retry");
        byOwner.Ok().State.Should().Be(CellState.Settled);
        (await store.SettleAsync(cell.Id, "worker-a", new LegOutcome.Completed(), Ct))
            .Reason().Should().Contain("is Settled, not Claimed");
    }

    [Fact]
    public async Task A_cell_stranded_by_a_dead_host_is_handed_back()
    {
        var clock = new TestClock(Noon);
        var (run, cells) = Plan(1, 1, 1);
        var store = postgres.NewStore(clock);
        await store.CreateAsync(run, cells, Ct);
        await store.ClaimNextAsync(run.Id, "host-that-dies", Ct);

        clock.Now = Noon.AddHours(2);
        var report = await store.SweepAsync(TimeSpan.FromMinutes(30), Ct);

        report.Requeued.Should().Be(1);
        report.Abandoned.Should().Be(0);
        (await store.ProgressAsync(run.Id, Ct)).Pending.Should().Be(1, "it is available again");
    }

    [Fact]
    public async Task A_fresh_claim_is_left_alone()
    {
        var clock = new TestClock(Noon);
        var (run, cells) = Plan(1, 1, 1);
        var store = postgres.NewStore(clock);
        await store.CreateAsync(run, cells, Ct);
        await store.ClaimNextAsync(run.Id, "worker-a", Ct);

        clock.Now = Noon.AddMinutes(5);

        (await store.SweepAsync(TimeSpan.FromMinutes(30), Ct)).Total
            .Should().Be(0, "a worker that is still working is not a dead host");
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
            (await store.ClaimNextAsync(run.Id, $"host-{attempt}", Ct)).Failed().Should().BeFalse();
            clock.Now = clock.Now.AddHours(2);
            await store.SweepAsync(TimeSpan.FromMinutes(30), Ct);
        }

        var progress = await store.ProgressAsync(run.Id, Ct);
        progress.Abandoned.Should().Be(1);
        progress.Pending.Should().Be(0, "an unbounded sweep is a loop that survives reboots");
        progress.Describe.Should().Contain("ABANDONED");

        clock.Now = clock.Now.AddDays(1);
        (await store.SweepAsync(TimeSpan.FromMinutes(30), Ct)).Total.Should().Be(0, "abandoned is terminal");
    }

    [Fact]
    public async Task An_outcome_survives_the_round_trip_with_its_kind_intact()
    {
        var (run, cells) = Plan(1, 1, 1);
        var store = postgres.NewStore(new TestClock(Noon));
        await store.CreateAsync(run, cells, Ct);
        var cell = (await store.ClaimNextAsync(run.Id, "w", Ct)).Ok();

        var settled = (await store.SettleAsync(
            cell.Id, "w", new LegOutcome.CapExceeded(BudgetKind.Wall, BudgetScope.Phase, 600m, 731m), Ct)).Ok();

        settled.OutcomeKind.Should().Be(LegOutcomeKind.CapExceeded);
        settled.OutcomeDetail.Should().Contain("Wall/Phase").And.Contain("731");
    }

    [Fact]
    public async Task Claiming_a_run_that_does_not_exist_is_an_answer_not_an_exception()
    {
        (await postgres.NewStore(new TestClock(Noon)).ClaimNextAsync(Guid.CreateVersion7(), "w", Ct))
            .Reason().Should().Contain("no pending cell");

        (await postgres.NewStore(new TestClock(Noon)).LoadAsync(Guid.CreateVersion7(), Ct))
            .Reason().Should().Contain("no run");
    }

    private async Task<List<Guid>> DrainAsync(Guid runId, string owner)
    {
        var store = postgres.NewStore(new TestClock(Noon));
        List<Guid> mine = [];

        while (true)
        {
            var claim = await store.ClaimNextAsync(runId, owner, Ct);

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
