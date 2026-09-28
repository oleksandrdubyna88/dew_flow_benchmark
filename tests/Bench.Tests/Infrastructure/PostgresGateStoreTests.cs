using Bench.Domain.Gate;
using Bench.Domain.Runs;
using Bench.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;
using static Bench.Tests.Infrastructure.GateStoreFixtures;

namespace Bench.Tests.Infrastructure;

/// <summary>The gate store's durability guarantees, against a real database — the same three the run store makes
/// (persist before enqueue, one winner per claim, an ownership-checked sweep), plus what is the gate's own: a claim
/// under a pin, a settle that carries its facts and findings, a hand-back that is ONE guarded statement, and a
/// sweep that never reaches into a run that already ended.
/// <para>
/// The sweep is database-wide, as the run store's is; every test that asserts a sweep's COUNT runs on a database of
/// its own (<see cref="PostgresFixture.NewDatabaseAsync"/>), and the rest assert only about their own cells.
/// </para></summary>
[Collection("postgres")]
public sealed class PostgresGateStoreTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private PostgresGateStore NewStore(TimeProvider clock) => new(postgres.NewContext(), clock);

    [Fact]
    public async Task A_run_is_written_whole_with_every_cell_before_anything_starts()
    {
        var (run, cells) = Planned(count: 3);
        var store = NewStore(new TestClock(Noon));

        (await store.PlanAsync(run, cells, Ct)).Ok().Id.Should().Be(run.Id);

        (await store.CellsAsync(run.Id, Ct)).Should().HaveCount(3).And.OnlyContain(c => c.State == CellState.Pending);
        (await store.LoadAsync(run.Id, Ct)).Ok().Should().Be(run, "the run reads back as it was planned — mode, suite stamp, source and all");
        (await store.PlanAsync(run, cells, Ct)).Reason().Should().Contain("already exists");
        (await store.PlanAsync(Run(), [], Ct)).Reason().Should().Contain("no cells");
    }

    [Fact]
    public async Task Two_workers_racing_for_one_cell_produce_exactly_one_winner()
    {
        var (run, cells) = Planned(count: 1);
        await NewStore(new TestClock(Noon)).PlanAsync(run, cells, Ct);

        var claims = await AllAtOnceAsync(
            Enumerable.Range(0, 16).Select(_ => NewStore(new TestClock(Noon))),
            (store, i) => store.ClaimNextAsync(run.Id, WorkerIdentity.Here($"lane-{i}"), Pin(), Ct));

        claims.Count(c => !c.Failed()).Should().Be(1, "State == Pending is in the WHERE of one UPDATE, so one statement can see the row change");
        claims.Where(c => c.Failed()).Should().OnlyContain(c => c.Reason().Contains(PostgresGateStore.NoPendingCell) || c.Reason().Contains("claim races"));
        (await NewStore(new TestClock(Noon)).CellAsync(cells[0].Id, Ct)).Ok().Attempts.Should().Be(1, "the winner's claim counted once");
    }

    /// <summary>The store hands cells out in the MATRIX's order — slot, then the reviewer's position inside the slot.
    /// It used to order by position first, which reversed the nesting: one task, reviewers A and B, two repeats was
    /// planned A1 B1 B2 A2 and claimed A1 B2 B1 A2, so B's second repeat ran before its first. The fixture that hid it
    /// planned every cell in slot 0; this one plans through <see cref="GateMatrix.Plan"/>.</summary>
    [Fact]
    public async Task Cells_are_claimed_in_the_order_the_matrix_planned_them()
    {
        var run = Run();
        var planned = GateMatrix.Plan(
            [GateTaskId.Parse("cs2").Ok()],
            [GateReviewerId.Parse("rev-a").Ok(), GateReviewerId.Parse("rev-b").Ok()],
            repeats: 2).Ok();
        var cells = planned.Select(c => GateCell.Pending(Guid.CreateVersion7(), run.Id, c)).ToList();
        var store = NewStore(new TestClock(Noon));
        await store.PlanAsync(run, cells, Ct);

        var claimed = new List<string>();
        for (var i = 0; i < cells.Count; i++)
        {
            var cell = (await store.ClaimNextAsync(run.Id, Here(), Pin(), Ct)).Ok();
            claimed.Add($"{cell.Reviewer.Value}{cell.Repeat}");
            (await store.SettleAsync(cell.Id, Here(), Completed(), Ct)).Ok();
        }

        claimed.Should().Equal(planned.Select(c => $"{c.Reviewer.Value}{c.Repeat}"), "a lane takes the next cell of the PLAN, repeats outermost");
    }

    /// <summary>Runs one call per store at the same instant. Each store opens its connection FIRST, and every call
    /// waits on one gate — without that, the first call finishes before the last connection is open and a "race" is
    /// a queue, which is how a guard that is missing can pass a concurrency test.</summary>
    private static async Task<T[]> AllAtOnceAsync<T>(IEnumerable<PostgresGateStore> stores, Func<PostgresGateStore, int, Task<T>> call)
    {
        var warmed = stores.ToList();
        await Task.WhenAll(warmed.Select(store => store.RecentAsync(1, Ct)));
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var calls = warmed.Select(async (store, i) =>
        {
            await gate.Task;
            return await call(store, i);
        }).ToList();

        gate.SetResult();
        return await Task.WhenAll(calls);
    }

    [Fact]
    public async Task A_claim_records_its_pin_and_counts_the_attempt_and_is_refused_without_a_pin()
    {
        var (run, cells) = Planned(count: 1);
        var store = NewStore(new TestClock(Noon));
        await store.PlanAsync(run, cells, Ct);

        (await store.ClaimNextAsync(run.Id, Here(), ProductPin.None, Ct)).Reason().Should().Contain("product pin");

        var claimed = (await store.ClaimNextAsync(run.Id, Here(), Pin('b'), Ct)).Ok();

        claimed.Pin.Should().Be(Pin('b'), "the pin is stored on the cell at claim time");
        claimed.Attempts.Should().Be(1);
        claimed.Owner.Pid.Should().Be(Environment.ProcessId);
    }

    [Fact]
    public async Task A_settled_session_reads_back_with_its_facts_and_its_findings_as_hashes()
    {
        var (run, cells) = Planned(count: 1);
        var store = NewStore(new TestClock(Noon));
        await store.PlanAsync(run, cells, Ct);
        var owner = Here();
        await store.ClaimNextAsync(run.Id, owner, Pin(), Ct);
        var settlement = Completed(findings: 2);

        var settled = (await store.SettleAsync(cells[0].Id, owner, settlement, Ct)).Ok();

        settled.State.Should().Be(CellState.Settled);
        settled.OutcomeKind.Should().Be(GateCellOutcomeKind.Completed);

        var record = (await store.FactsAsync(run.Id, Ct)).Should().ContainSingle().Subject;
        record.CampaignId.Should().Be(run.Id);
        record.RunId.Should().Be(cells[0].Id, "a session is one cell's settled attempt — the cell names it");
        record.Scope.Should().Be(GateScope.Of(run.SuiteStamp, run.Gate, Pin(), settlement.SettingsHash));
        record.Facts.Should().BeEquivalentTo(settlement.Facts, o => o.Excluding(f => f.Failure), "every fact survives the store, not-captured flags included");
        record.Findings.Select(f => (f.Ordinal, f.TextHash, f.FileHash)).Should().Equal(settlement.Findings.Select(f => (f.Ordinal, f.TextHash, f.FileHash)));
        (await store.HasFindingsAsync(Ct)).Should().BeTrue();
    }

    [Fact]
    public async Task A_settle_from_a_worker_that_does_not_hold_the_cell_is_refused_naming_both()
    {
        var (run, cells) = Planned(count: 1);
        var store = NewStore(new TestClock(Noon));
        await store.PlanAsync(run, cells, Ct);
        await store.ClaimNextAsync(run.Id, WorkerIdentity.Here("lane-a"), Pin(), Ct);

        (await store.SettleAsync(cells[0].Id, WorkerIdentity.Here("lane-b"), Completed(), Ct)).Reason()
            .Should().Contain("held by lane-a").And.Contain("not lane-b");
        (await store.FactsAsync(run.Id, Ct)).Should().BeEmpty("a refused settle writes no facts and no findings");
    }

    [Fact]
    public async Task A_failed_session_is_stored_with_its_cause_and_read_back_invalid()
    {
        var (run, cells) = Planned(count: 1);
        var store = NewStore(new TestClock(Noon));
        await store.PlanAsync(run, cells, Ct);
        var owner = Here();
        await store.ClaimNextAsync(run.Id, owner, Pin(), Ct);

        var settled = (await store.SettleAsync(cells[0].Id, owner,
            new GateSettlement.Failed(new FailureCause(FailureKind.ProcessDied, "the coai process died on turn 2")), Ct)).Ok();

        settled.OutcomeKind.Should().Be(GateCellOutcomeKind.Failed);
        settled.OutcomeDetail.Should().Be("the coai process died on turn 2");
        var facts = (await store.FactsAsync(run.Id, Ct)).Should().ContainSingle().Subject.Facts;
        facts.Valid.Should().BeFalse("a failed run stays in the denominator");
        facts.CostUsd.WasCaptured.Should().BeFalse("unknown, never free");
        facts.Failure.Kind.Should().Be(FailureKind.ProcessDied);
    }

    [Fact]
    public async Task A_stale_claim_by_a_dead_pid_on_this_host_is_handed_back_and_one_on_another_host_is_left_alone()
    {
        var clock = new TestClock(Noon);
        var (run, cells) = Planned(count: 2);
        var store = NewStore(clock);
        await store.PlanAsync(run, cells, Ct);
        await store.ClaimNextAsync(run.Id, TestWorkers.Dead("dead-here"), Pin(), Ct);
        await store.ClaimNextAsync(run.Id, WorkerIdentity.Stored("elsewhere", "another-host", 4242), Pin(), Ct);

        clock.Now = Noon.AddHours(2);
        await NewStore(clock).SweepAsync(TimeSpan.FromMinutes(30), Ct);

        var after = await store.CellsAsync(run.Id, Ct);
        after.Single(c => c.Id == cells[0].Id).State.Should().Be(CellState.Pending, "a dead pid on this host is provably gone");
        after.Single(c => c.Id == cells[0].Id).Attempts.Should().Be(1, "a hand-back keeps the count; only the next claim moves it");
        after.Single(c => c.Id == cells[1].Id).State.Should().Be(CellState.Claimed, "we cannot see another host's process table");
    }

    [Fact]
    public async Task Two_sweepers_racing_over_one_dead_owner_hand_the_cell_back_once()
    {
        var connection = await postgres.NewDatabaseAsync($"gate_sweep_{Guid.NewGuid():N}");
        var clock = new TestClock(Noon);
        var (run, cells) = Planned(count: 1);
        var planner = new PostgresGateStore(PostgresFixture.Context(connection), clock);
        await planner.PlanAsync(run, cells, Ct);
        await planner.ClaimNextAsync(run.Id, TestWorkers.Dead("dead"), Pin(), Ct);
        clock.Now = Noon.AddHours(2);

        var reports = await AllAtOnceAsync(
            Enumerable.Range(0, 8).Select(_ => new PostgresGateStore(PostgresFixture.Context(connection), clock)),
            (store, _) => store.SweepAsync(TimeSpan.FromMinutes(30), Ct));

        reports.Sum(r => r.Total).Should().Be(1, "the hand-back re-checks state, owner, claim time and attempts in its WHERE — the second sweeper matches nothing");
        var cell = (await planner.CellAsync(cells[0].Id, Ct)).Ok();
        cell.State.Should().Be(CellState.Pending);
        cell.Attempts.Should().Be(1, "no sweeper moved the count");

        var reclaimed = (await planner.ClaimNextAsync(run.Id, Here(), Pin(), Ct)).Ok();
        reclaimed.Attempts.Should().Be(2, "the next claim moves it exactly once — attempt 2 is a new attempt directory");
    }

    [Fact]
    public async Task The_third_hand_back_abandons_the_cell_with_its_cause()
    {
        var connection = await postgres.NewDatabaseAsync($"gate_abandon_{Guid.NewGuid():N}");
        var clock = new TestClock(Noon);
        var (run, cells) = Planned(count: 1);
        var store = new PostgresGateStore(PostgresFixture.Context(connection), clock);
        await store.PlanAsync(run, cells, Ct);

        for (var i = 0; i < Claimable.MaxAttempts; i++)
        {
            clock.Now = Noon.AddHours(i * 2);
            (await store.ClaimNextAsync(run.Id, TestWorkers.Dead($"dead-{i}"), Pin(), Ct)).Ok();
            clock.Now = Noon.AddHours((i * 2) + 1);
            await store.SweepAsync(TimeSpan.FromMinutes(30), Ct);
        }

        var cell = (await store.CellAsync(cells[0].Id, Ct)).Ok();
        cell.State.Should().Be(CellState.Abandoned);
        cell.OutcomeKind.Should().Be(GateCellOutcomeKind.Failed);
        cell.OutcomeDetail.Should().Contain("abandoned after 3 attempts");
    }

    [Theory]
    [InlineData(GateRunStatus.Finished)]
    [InlineData(GateRunStatus.Failed)]
    public async Task A_cell_of_a_run_that_ended_is_never_swept(GateRunStatus ended)
    {
        var clock = new TestClock(Noon);
        var (run, cells) = Planned(count: 1);
        var store = NewStore(clock);
        await store.PlanAsync(run, cells, Ct);
        await store.ClaimNextAsync(run.Id, TestWorkers.Dead("dead"), Pin(), Ct);
        (await store.AdvanceAsync(run.Id, ended, Ct)).Ok().Should().Be(ended);

        clock.Now = Noon.AddDays(3);
        await NewStore(clock).SweepAsync(TimeSpan.FromMinutes(30), Ct);

        var cell = (await store.CellAsync(cells[0].Id, Ct)).Ok();
        cell.State.Should().Be(CellState.Claimed, $"a {ended} run is a record, not a queue — its cells are never handed back or abandoned");
        cell.OutcomeKind.Should().Be(GateCellOutcomeKind.None);
    }

    [Fact]
    public async Task A_run_that_ended_is_not_claimed_from_and_does_not_start_again()
    {
        var (run, cells) = Planned(count: 2);
        var store = NewStore(new TestClock(Noon));
        await store.PlanAsync(run, cells, Ct);
        (await store.AdvanceAsync(run.Id, GateRunStatus.Running, Ct)).Ok().Should().Be(GateRunStatus.Running);
        (await store.AdvanceAsync(run.Id, GateRunStatus.Finished, Ct)).Ok().Should().Be(GateRunStatus.Finished);

        (await store.ClaimNextAsync(run.Id, Here(), Pin(), Ct)).Reason().Should().Contain(PostgresGateStore.NoPendingCell);
        (await store.AdvanceAsync(run.Id, GateRunStatus.Running, Ct)).Reason().Should().Contain("does not move again");
    }

    [Fact]
    public async Task An_artefact_path_is_recorded_once()
    {
        var (run, cells) = Planned(count: 1);
        var store = NewStore(new TestClock(Noon));
        await store.PlanAsync(run, cells, Ct);
        var claimed = (await store.ClaimNextAsync(run.Id, Here(), Pin(), Ct)).Ok();
        var scope = ArtifactScope.Of(run, claimed).Ok();
        var path = CellPaths.AttemptRoot(scope).Then("reply.json").Ok();
        var artifact = ArtifactRef.Of(scope, ArtifactClass.Reply, path, new string('a', 64), 12).Ok();

        (await store.RecordArtifactsAsync([artifact], Ct)).Ok().Should().Be(1);
        (await store.RecordArtifactsAsync([artifact], Ct)).Reason().Should().Contain("already recorded");
        (await store.ArtifactsAsync(run.Id, Ct)).Should().ContainSingle().Which.Path.Value.Should().Be(path.Value);
    }

    [Fact]
    public async Task The_migration_creates_the_six_gate_tables_and_touches_no_other()
    {
        await using var db = postgres.NewContext();
        var migrations = db.GetService<IMigrationsAssembly>();
        var id = migrations.Migrations.Keys.Should().ContainSingle(k => k.EndsWith("_GateTables", StringComparison.Ordinal)).Subject;

        var operations = migrations.CreateMigration(migrations.Migrations[id], db.Database.ProviderName!).UpOperations;

        operations.OfType<CreateTableOperation>().Select(o => o.Name).Should().BeEquivalentTo(
            ["gate_runs", "gate_cells", "gate_findings", "gate_verdicts", "gate_reviewers", "gate_artifacts"]);
        operations.OfType<ITableMigrationOperation>().Select(o => o.Table).Should().OnlyContain(t => t.StartsWith("gate_", StringComparison.Ordinal),
            "no existing table is touched — the gate is a sibling context in the same database");
        PostgresGatePublicationSource.GateEntities(db).Select(e => e.GetTableName()).Should().HaveCount(8, "six from this migration, gate_hand_checks from E4's, gate_summaries from E5's");
        (await db.GateRuns.CountAsync(Ct)).Should().BeGreaterThanOrEqualTo(0, "the tables exist on a migrated database");
    }

    /// <summary>Every run of a suite stamp, chosen in the database — the assessment's <c>--scope</c> used to filter the
    /// newest 10 000 runs in memory, which silently dropped every campaign older than that.</summary>
    [Fact]
    public async Task The_runs_of_a_suite_stamp_are_every_run_planned_against_it_and_no_other()
    {
        var store = NewStore(new TestClock(Noon));
        var stamp = $"scope-{Guid.NewGuid():N}"[..20] + "#000000000000";
        var (first, firstCells) = Planned(count: 1);
        var (second, secondCells) = Planned(count: 1);
        var (other, otherCells) = Planned(count: 1);
        await store.PlanAsync(first with { SuiteStamp = stamp }, firstCells, Ct);
        await store.PlanAsync(second with { SuiteStamp = stamp }, secondCells, Ct);
        await store.PlanAsync(other, otherCells, Ct);

        (await store.RunsOfSuiteAsync(stamp, Ct)).Should().BeEquivalentTo([first.Id, second.Id]);
        (await store.RunsOfSuiteAsync("never-planned#000000000000", Ct)).Should().BeEmpty();
    }

    /// <summary>The assessment's migration (E4) adds the hand-check table and the verdict replay index, on gate tables only.</summary>
    [Fact]
    public async Task The_assessment_migration_adds_the_hand_check_table_and_the_verdict_replay_index_and_touches_no_other()
    {
        await using var db = postgres.NewContext();
        var migrations = db.GetService<IMigrationsAssembly>();
        var id = migrations.Migrations.Keys.Should().ContainSingle(k => k.EndsWith("_GateAssessment", StringComparison.Ordinal)).Subject;

        var operations = migrations.CreateMigration(migrations.Migrations[id], db.Database.ProviderName!).UpOperations;

        operations.OfType<CreateTableOperation>().Select(o => o.Name).Should().Equal(["gate_hand_checks"]);
        operations.OfType<CreateIndexOperation>().Should().Contain(i => i.Table == "gate_verdicts" && i.IsUnique
            && i.Columns.SequenceEqual(new[] { "CellId", "FindingOrdinal", "RubricHash", "AssessorId", "BatchId" }));
        operations.OfType<ITableMigrationOperation>().Select(o => o.Table).Should().OnlyContain(t => t.StartsWith("gate_", StringComparison.Ordinal));
        (await db.GateHandChecks.CountAsync(Ct)).Should().BeGreaterThanOrEqualTo(0, "the table exists on a migrated database");
    }

    /// <summary>The driver's migration adds columns to gate tables only, and what it adds reads back: the prediction's
    /// hash and the product-change flag on the run, the session notes on the settled cell.</summary>
    [Fact]
    public async Task The_driver_migration_touches_only_gate_tables_and_its_columns_read_back()
    {
        await using (var db = postgres.NewContext())
        {
            var migrations = db.GetService<IMigrationsAssembly>();
            var id = migrations.Migrations.Keys.Should().ContainSingle(k => k.EndsWith("_GateDriver", StringComparison.Ordinal)).Subject;
            migrations.CreateMigration(migrations.Migrations[id], db.Database.ProviderName!).UpOperations
                .OfType<ITableMigrationOperation>().Select(o => o.Table).Should().OnlyContain(t => t.StartsWith("gate_", StringComparison.Ordinal));
        }

        var (planned, cells) = Planned(count: 1);
        var run = planned with { PredictionHash = new string('7', 64), AllowProductChange = true };
        var store = NewStore(new TestClock(Noon));
        await store.PlanAsync(run, cells, Ct);
        var claimed = (await store.ClaimNextAsync(run.Id, Here(), Pin(), Ct)).Ok();
        (await store.SettleAsync(claimed.Id, Here(), Completed() with { Notes = new GateSessionNotes("0.39.0", new string('8', 64), 3, 1) }, Ct)).Ok();

        (await store.LoadAsync(run.Id, Ct)).Ok().Should().Match<GateRun>(r => r.PredictionHash == run.PredictionHash && r.AllowProductChange);
        await using var read = postgres.NewContext();
        var row = await read.GateCells.SingleAsync(c => c.Id == claimed.Id, Ct);
        (row.ServerVersion, row.ReferencesHash, row.SettingsChecked, row.SettingsMismatches).Should().Be(("0.39.0", new string('8', 64), 3, 1));
    }
}
