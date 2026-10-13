using Bench.Domain.Runs;
using Bench.Domain.Scouts;
using Bench.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;
using static Bench.Tests.Infrastructure.ScoutStoreFixtures;

namespace Bench.Tests.Infrastructure;

/// <summary>The scout store against a real Postgres — the probe store's durability guarantees over scout cells (persist before enqueue,
/// one winner per claim, schedule order, an ownership-checked sweep, appended generations) and the SPEND LEDGER's: a reservation that does
/// not fit is refused as a value, the ledger survives a restart, an unknown charge is the whole reservation and is never released, only an
/// attempt without a launching mark is released, and each attempt settles exactly once.
/// <para>The sweep is database-wide; a test that counts a sweep runs on a database of its own.</para></summary>
[Collection("postgres")]
public sealed class PostgresScoutStoreTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private PostgresScoutStore NewStore(TimeProvider? clock = null) => new(postgres.NewContext(), clock ?? new TestClock(Noon));

    [Fact]
    public async Task A_run_is_written_whole_with_its_frozen_tasks_and_every_cell_and_reads_back_exactly()
    {
        var (run, tasks, cells) = Planned(tasks: 2);
        var store = NewStore();

        (await store.PlanAsync(run, tasks, cells, Ct)).Ok();

        var loaded = (await store.LoadAsync(run.Id, Ct)).Ok();
        (loaded.Pins, loaded.Seed, loaded.Repeats, loaded.CreatedAt, loaded.Ledger).Should().Be((run.Pins, 7, 1, Noon, SpendLedger.Empty(10m)));
        loaded.Arms.Should().Equal(run.Arms);
        loaded.Tasks.Should().Equal(run.Tasks);
        (await store.TasksAsync(run.Id, Ct)).Should().BeEquivalentTo(tasks, "the statement, G, the removals and the tree hash are frozen with the run");
        (await store.CellsAsync(run.Id, Ct)).Should().BeEquivalentTo(cells, o => o.WithStrictOrdering(), "cells read back in schedule order, exactly as planned");
    }

    [Fact]
    public async Task A_plan_is_refused_whole_naming_what_is_wrong()
    {
        var (run, tasks, cells) = Planned();
        var store = NewStore();
        await store.PlanAsync(run, tasks, cells, Ct);

        (await store.PlanAsync(run, tasks, cells, Ct)).Reason().Should().Contain("already exists");
        var (other, otherTasks, otherCells) = Planned();
        (await store.PlanAsync(other, otherTasks, [], Ct)).Reason().Should().Contain("no cells");
        (await store.PlanAsync(other, otherTasks, cells, Ct)).Reason().Should().Contain("names that run");
        (await store.PlanAsync(other, [], otherCells, Ct)).Reason().Should().Contain("frozen task list");
    }

    [Fact]
    public async Task Cells_are_claimed_one_at_a_time_in_schedule_order_and_a_settled_cell_is_never_claimed_again()
    {
        var (run, tasks, cells) = Planned(tasks: 2);
        var store = NewStore();
        await store.PlanAsync(run, tasks, cells, Ct);
        var owner = Here();

        var claimed = new List<Guid>();
        foreach (var _ in cells)
        {
            var cell = (await store.ClaimNextAsync(run.Id, owner, Pin(), Ct)).Ok();
            claimed.Add(cell.Id);
            (await store.SettleAsync(cell.Id, owner, Scored(), [], Ct)).Ok();
        }

        claimed.Should().Equal(cells.OrderBy(c => c.Slot).ThenBy(c => c.Position).Select(c => c.Id));
        (await store.ClaimNextAsync(run.Id, owner, Pin(), Ct)).Reason().Should().Contain(PostgresScoutStore.NoPendingCell);
        (await store.ClaimNextAsync(run.Id, owner, Bench.Domain.Gate.ProductPin.None, Ct)).Reason().Should().Contain("product pin");
    }

    [Fact]
    public async Task Two_workers_racing_for_one_cell_produce_exactly_one_winner()
    {
        var (run, tasks, cells) = Planned(arms: [ScoutArmId.A1]);
        await NewStore().PlanAsync(run, tasks, cells, Ct);
        var stores = Enumerable.Range(0, 12).Select(_ => NewStore()).ToList();
        await Task.WhenAll(stores.Select(s => s.CellsAsync(Guid.Empty, Ct)));
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var claims = stores.Select(async (s, i) => { await gate.Task; return await s.ClaimNextAsync(run.Id, WorkerIdentity.Here($"w{i}"), Pin(), Ct); }).ToList();
        gate.SetResult();
        var results = await Task.WhenAll(claims);

        results.Count(r => !r.Failed()).Should().Be(1, "State == Pending is in the WHERE of one UPDATE");
    }

    [Fact]
    public async Task A_settled_cell_carries_its_outcome_facts_and_per_scout_records_and_a_stranger_cannot_settle_it()
    {
        var (run, tasks, cells) = Planned(arms: [ScoutArmId.B1]);
        var store = NewStore();
        await store.PlanAsync(run, tasks, cells, Ct);
        var cell = (await store.ClaimNextAsync(run.Id, Here(), Pin(), Ct)).Ok();

        (await store.SettleAsync(cell.Id, WorkerIdentity.Here("stranger"), Scored(), [], Ct)).Reason().Should().Contain("held by");
        var settled = (await store.SettleAsync(cell.Id, Here(), Scored(), [Scout(1), Scout(2)], Ct)).Ok();

        (settled.State, settled.Outcome, settled.Reason).Should().Be((CellState.Settled, ScoutCellOutcome.Scored, ScoutReason.None));
        settled.Facts.Should().Be(Scored().Facts);
        (await new PostgresScoutReads(postgres.NewContext()).ScoutsAsync(cell.Id, Ct)).Should().BeEquivalentTo(new[] { Scout(1), Scout(2) }, o => o.WithStrictOrdering());
        (await store.SettleAsync(cell.Id, Here(), Scored(), [], Ct)).Reason().Should().Contain("not Claimed");
    }

    [Fact]
    public async Task A_void_cell_is_rerun_as_an_appended_generation_and_the_reads_answer_the_latest_settled_one()
    {
        var (run, tasks, cells) = Planned(arms: [ScoutArmId.A1]);
        var store = NewStore();
        await store.PlanAsync(run, tasks, cells, Ct);
        var first = (await store.ClaimNextAsync(run.Id, Here(), Pin(), Ct)).Ok();

        (await store.NextGenerationAsync(first.Id, Guid.CreateVersion7(), Ct)).Reason().Should().Contain("still being measured");
        await store.SettleAsync(first.Id, Here(), new ScoutSettlement(ScoutCellOutcome.Void, ScoutReason.ChildModelMismatch, ScoutCellFacts.None), [], Ct);
        var second = (await store.NextGenerationAsync(first.Id, Guid.CreateVersion7(), Ct)).Ok();

        (second.Generation, second.State).Should().Be((2, CellState.Pending));
        (await new PostgresScoutReads(postgres.NewContext()).LatestSettledAsync(run.Id, Ct)).Single().Id.Should().Be(first.Id, "a pending re-run never hides the verdict it re-measures");
        var claimedAgain = (await store.ClaimNextAsync(run.Id, Here(), Pin(), Ct)).Ok();
        claimedAgain.Id.Should().Be(second.Id);
    }

    [Fact]
    public async Task A_dead_owners_claim_is_handed_back_by_the_sweep_and_a_live_one_is_left_alone()
    {
        var connection = await postgres.NewDatabaseAsync($"scout_sweep_{Guid.NewGuid():N}");
        var store = new PostgresScoutStore(PostgresFixture.Context(connection), new TestClock(Noon));
        var (run, tasks, cells) = Planned(arms: [ScoutArmId.A1, ScoutArmId.B1]);
        await store.PlanAsync(run, tasks, cells, Ct);
        var dead = (await store.ClaimNextAsync(run.Id, TestWorkers.Dead("crashed"), Pin(), Ct)).Ok();
        var live = (await store.ClaimNextAsync(run.Id, Here(), Pin(), Ct)).Ok();

        (await store.SweepAsync(TimeSpan.Zero, Ct)).Should().Be(new Bench.Application.Scouts.ScoutSweepReport(1, 0));

        (await store.CellAsync(dead.Id, Ct)).Ok().Should().Match<ScoutCell>(c => c.State == CellState.Pending && c.Attempts == 1);
        (await store.CellAsync(live.Id, Ct)).Ok().State.Should().Be(CellState.Claimed);
    }

    [Fact]
    public async Task A_cell_handed_back_three_times_is_abandoned()
    {
        var connection = await postgres.NewDatabaseAsync($"scout_abandon_{Guid.NewGuid():N}");
        var store = new PostgresScoutStore(PostgresFixture.Context(connection), new TestClock(Noon));
        var (run, tasks, cells) = Planned(arms: [ScoutArmId.A1]);
        await store.PlanAsync(run, tasks, cells, Ct);

        for (var i = 0; i < Claimable.MaxAttempts; i++)
        {
            (await store.ClaimNextAsync(run.Id, TestWorkers.Dead("crashed"), Pin(), Ct)).Ok();
            await store.SweepAsync(TimeSpan.Zero, Ct);
        }

        (await store.CellAsync(cells[0].Id, Ct)).Ok().Should().Match<ScoutCell>(c => c.State == CellState.Abandoned && c.Reason == ScoutReason.Abandoned);
    }

    [Fact]
    public async Task A_reservation_that_does_not_fit_the_ceiling_is_refused_as_a_value_naming_what_was_free()
    {
        var (run, tasks, cells) = Planned(arms: [ScoutArmId.A1], ceilingUsd: 5m);
        var store = NewStore();
        await store.PlanAsync(run, tasks, cells, Ct);
        var cell = (await store.ClaimNextAsync(run.Id, Here(), Pin(), Ct)).Ok();

        var refused = await store.ReserveAsync(cell.Id, Here(), Guid.CreateVersion7(), 6m, Ct);

        refused.Should().Be(new ScoutReservation.DoesNotFit(5m, 6m));
        (await store.LoadAsync(run.Id, Ct)).Ok().Ledger.Should().Be(SpendLedger.Empty(5m), "a refused reservation moves no money");
        (await store.AttemptsAsync(run.Id, Ct)).Should().BeEmpty();
        (await store.ReserveAsync(cell.Id, Here(), Guid.CreateVersion7(), 5m, Ct)).Should().BeOfType<ScoutReservation.Reserved>("exactly the ceiling fits");
    }

    [Fact]
    public async Task The_ledger_survives_a_restart_because_it_lives_on_the_run_row()
    {
        var (run, tasks, cells) = Planned(arms: [ScoutArmId.A1, ScoutArmId.B1], ceilingUsd: 10m);
        await NewStore().PlanAsync(run, tasks, cells, Ct);
        var owner = Here();
        var store = NewStore();
        var cell = (await store.ClaimNextAsync(run.Id, owner, Pin(), Ct)).Ok();
        var attempt = ((ScoutReservation.Reserved)await store.ReserveAsync(cell.Id, owner, Guid.CreateVersion7(), 6m, Ct)).Attempt;

        var restarted = NewStore();

        (await restarted.LoadAsync(run.Id, Ct)).Ok().Ledger.Should().Be(new SpendLedger(10m, 6m, 0m, 0m));
        (await restarted.AttemptsAsync(run.Id, Ct)).Single().Should().Be(attempt);
        var next = (await restarted.ClaimNextAsync(run.Id, owner, Pin(), Ct)).Ok();
        (await restarted.ReserveAsync(next.Id, owner, Guid.CreateVersion7(), 6m, Ct)).Should().Be(new ScoutReservation.DoesNotFit(4m, 6m),
            "the first reservation is still held after the restart");
    }

    [Fact]
    public async Task Two_reservations_racing_for_one_headroom_cannot_both_fit()
    {
        var (run, tasks, cells) = Planned(arms: [ScoutArmId.A1, ScoutArmId.B1], ceilingUsd: 10m);
        await NewStore().PlanAsync(run, tasks, cells, Ct);
        var (a, b) = (WorkerIdentity.Here("a"), WorkerIdentity.Here("b"));
        var first = (await NewStore().ClaimNextAsync(run.Id, a, Pin(), Ct)).Ok();
        var second = (await NewStore().ClaimNextAsync(run.Id, b, Pin(), Ct)).Ok();
        var (one, two) = (NewStore(), NewStore());
        await Task.WhenAll(one.CellsAsync(Guid.Empty, Ct), two.CellsAsync(Guid.Empty, Ct));

        var results = await Task.WhenAll(one.ReserveAsync(first.Id, a, Guid.CreateVersion7(), 7m, Ct), two.ReserveAsync(second.Id, b, Guid.CreateVersion7(), 7m, Ct));

        results.Count(r => r is ScoutReservation.Reserved).Should().Be(1, "the ceiling is in the WHERE of one UPDATE, re-evaluated on the row the winner committed");
        results.Count(r => r is ScoutReservation.DoesNotFit).Should().Be(1);
        (await NewStore().LoadAsync(run.Id, Ct)).Ok().Ledger.ReservedUsd.Should().Be(7m);
    }

    [Fact]
    public async Task An_attempt_launched_and_lost_is_charged_its_whole_reservation_and_that_charge_is_never_released()
    {
        var (store, run, attempt) = await ReservedAsync(reservationUsd: 3m);

        (await store.MarkLaunchingAsync(attempt.Id, Ct)).Ok().State.Should().Be(ScoutAttemptState.Launching);
        (await store.ReleaseAsync(attempt.Id, Ct)).Reason().Should().Contain("never marked launching", "a launching mark means it may have spent");
        (await store.ChargeUnknownAsync(attempt.Id, Ct)).Ok().State.Should().Be(ScoutAttemptState.ChargedUnknown);

        (await store.LoadAsync(run.Id, Ct)).Ok().Ledger.Should().Be(new SpendLedger(10m, 0m, 0m, 3m));
        (await store.ReleaseAsync(attempt.Id, Ct)).Failed().Should().BeTrue();
        (await store.SettleAttemptAsync(attempt.Id, 0.5m, Ct)).Failed().Should().BeTrue("an attempt settles exactly once");
        (await store.LoadAsync(run.Id, Ct)).Ok().Ledger.UnknownUsd.Should().Be(3m, "booked for good");
    }

    [Fact]
    public async Task Only_an_attempt_with_no_launching_mark_is_released()
    {
        var (store, run, attempt) = await ReservedAsync(reservationUsd: 3m);

        (await store.ReleaseAsync(attempt.Id, Ct)).Ok().State.Should().Be(ScoutAttemptState.Released);
        (await store.LoadAsync(run.Id, Ct)).Ok().Ledger.Should().Be(SpendLedger.Empty(10m));
        (await store.MarkLaunchingAsync(attempt.Id, Ct)).Reason().Should().Contain("Released");
    }

    [Fact]
    public async Task A_launched_attempt_settles_once_from_its_result_and_the_ledger_identity_holds()
    {
        var (store, run, attempt) = await ReservedAsync(reservationUsd: 3m);
        await store.MarkLaunchingAsync(attempt.Id, Ct);

        (await store.SettleAttemptAsync(attempt.Id, 2.75m, Ct)).Ok().Should().Match<ScoutAttempt>(a => a.State == ScoutAttemptState.Settled && a.SettledUsd == 2.75m);
        (await store.SettleAttemptAsync(attempt.Id, 2.75m, Ct)).Reason().Should().Contain("settles, once");
        (await store.ChargeUnknownAsync(attempt.Id, Ct)).Failed().Should().BeTrue();

        var ledger = (await store.LoadAsync(run.Id, Ct)).Ok().Ledger;
        ledger.Should().Be(new SpendLedger(10m, 0m, 2.75m, 0m));
        ledger.SpentUsd.Should().Be((await store.AttemptsAsync(run.Id, Ct)).Sum(a => a.ChargedUsd), "ledger = Σ settled + Σ unknown");
    }

    [Fact]
    public async Task A_claim_attempt_holds_one_reservation_and_only_its_holder_may_take_it()
    {
        var (run, tasks, cells) = Planned(arms: [ScoutArmId.A1]);
        var store = NewStore();
        await store.PlanAsync(run, tasks, cells, Ct);
        var cell = (await store.ClaimNextAsync(run.Id, Here(), Pin(), Ct)).Ok();

        (await store.ReserveAsync(cell.Id, WorkerIdentity.Here("stranger"), Guid.CreateVersion7(), 1m, Ct)).Should().BeOfType<ScoutReservation.Refused>();
        (await store.ReserveAsync(cell.Id, Here(), Guid.CreateVersion7(), 1m, Ct)).Should().BeOfType<ScoutReservation.Reserved>();
        (await store.ReserveAsync(cell.Id, Here(), Guid.CreateVersion7(), 1m, Ct)).Should().BeOfType<ScoutReservation.Refused>()
            .Which.Reason.Should().Contain("already holds a reservation");
        (await store.LoadAsync(run.Id, Ct)).Ok().Ledger.ReservedUsd.Should().Be(1m, "the refused second reservation rolled its ledger move back");
    }

    [Fact]
    public void The_scout_migration_creates_the_six_scout_tables_and_touches_no_other()
    {
        using var db = postgres.NewContext();
        var migrations = db.GetService<IMigrationsAssembly>();
        var id = migrations.Migrations.Keys.Should().ContainSingle(k => k.EndsWith("_ScoutTables", StringComparison.Ordinal)).Subject;

        var operations = migrations.CreateMigration(migrations.Migrations[id], db.Database.ProviderName!).UpOperations;

        operations.OfType<CreateTableOperation>().Select(o => o.Name).Should().BeEquivalentTo(["scout_runs", "scout_tasks", "scout_cells", "scout_records", "scout_attempts", "scout_claims"]);
        operations.OfType<ITableMigrationOperation>().Select(o => o.Table).Should().OnlyContain(t => t.StartsWith("scout_", StringComparison.Ordinal));
        operations.OfType<CreateIndexOperation>().Should().Contain(i => i.Table == "scout_cells" && i.IsUnique
            && i.Columns.SequenceEqual(new[] { "RunId", "TaskId", "Arm", "Repeat", "Generation" }));
        operations.OfType<CreateIndexOperation>().Should().Contain(i => i.Table == "scout_attempts" && i.IsUnique && i.Columns.SequenceEqual(new[] { "CellId", "Number" }));
        operations.OfType<CreateIndexOperation>().Should().Contain(i => i.Table == "scout_claims" && i.IsUnique && i.Columns.SequenceEqual(new[] { "CellId", "Ordinal", "Index" }),
            "one verdict per claim — the index is what makes assess idempotent under a race");
        PostgresGatePublicationSource.ScoutEntities(db).Select(e => e.GetTableName()).Should().Equal(["scout_attempts", "scout_cells", "scout_claims", "scout_records", "scout_runs", "scout_tasks"]);
    }

    [Fact]
    public async Task The_scout_migration_applies_on_a_database_already_holding_the_gate_and_probe_tables_and_on_an_empty_one()
    {
        var connection = await postgres.NewEmptyDatabaseAsync($"scout_mig_{Guid.NewGuid():N}");

        await using (var db = PostgresFixture.Context(connection))
        {
            await db.GetService<IMigrator>().MigrateAsync("20261001181318_ProbeTables", Ct);
            (await TablesAsync(db)).Should().Contain(["gate_cells", "probe_cells"]).And.NotContain("scout_cells");

            await db.Database.MigrateAsync(Ct);
            (await TablesAsync(db)).Should().Contain(["gate_cells", "probe_cells", "scout_runs", "scout_tasks", "scout_cells", "scout_records", "scout_attempts"]);
        }

        var fresh = await postgres.NewDatabaseAsync($"scout_fresh_{Guid.NewGuid():N}");
        await using var empty = PostgresFixture.Context(fresh);
        (await TablesAsync(empty)).Should().Contain(["scout_runs", "scout_attempts"], "every migration from the first applies on an empty database");
    }

    private async Task<(PostgresScoutStore Store, ScoutRun Run, ScoutAttempt Attempt)> ReservedAsync(decimal reservationUsd)
    {
        var (run, tasks, cells) = Planned(arms: [ScoutArmId.A1], ceilingUsd: 10m);
        var store = NewStore();
        await store.PlanAsync(run, tasks, cells, Ct);
        var cell = (await store.ClaimNextAsync(run.Id, Here(), Pin(), Ct)).Ok();
        var reserved = (ScoutReservation.Reserved)await store.ReserveAsync(cell.Id, Here(), Guid.CreateVersion7(), reservationUsd, Ct);

        (await store.LoadAsync(run.Id, Ct)).Ok().Ledger.ReservedUsd.Should().Be(reservationUsd);
        return (store, run, reserved.Attempt);
    }

    private static async Task<IReadOnlyList<string>> TablesAsync(BenchDbContext db) =>
        await db.Database.SqlQueryRaw<string>("select table_name as \"Value\" from information_schema.tables where table_schema = 'public'").ToListAsync(Ct);
}
