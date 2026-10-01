using Bench.Domain.Gate;
using Bench.Domain.Probes;
using Bench.Domain.Runs;
using Bench.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;
using static Bench.Tests.Infrastructure.ProbeStoreFixtures;

namespace Bench.Tests.Infrastructure;

/// <summary>The probe store's durability guarantees against a real database — the gate store's three (persist before enqueue,
/// one winner per claim, an ownership-checked sweep) plus the plan's own: a claim FOR A SUBJECT in matrix order, an unmeasured
/// hand-back that keeps the attempt and never abandons (D8), a re-run that appends a generation (D2), a run with no status (D3),
/// and reads that answer the highest SETTLED generation.
/// <para>
/// The sweep is database-wide; every test that asserts a sweep's COUNT runs on a database of its own.
/// </para></summary>
[Collection("postgres")]
public sealed class PostgresProbeStoreTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly IReadOnlyList<ProbeKind> TwoProbes = [ProbeKind.ReadInside, ProbeKind.ReadOutsideBare];

    private PostgresProbeStore NewStore(TimeProvider clock) => new(postgres.NewContext(), clock);

    [Fact]
    public async Task A_run_is_written_whole_with_every_cell_and_reads_back_with_its_frozen_subjects_and_oracle()
    {
        var run = Run(Subject("claude-sonnet", "claude"), Subject("codex-astra", "codex"));
        var cells = ProbeMatrix.Plan(TwoProbes, run.Subjects, repeats: 3).Ok().Cells.Select(c => ProbeCell.Pending(Guid.CreateVersion7(), run.Id, c)).ToList();
        var store = NewStore(new TestClock(Noon));

        (await store.PlanAsync(run, cells, Ct)).Ok().Id.Should().Be(run.Id);

        (await store.CellsAsync(run.Id, Ct)).Should().HaveCount(cells.Count).And.OnlyContain(c => c.State == CellState.Pending && c.Generation == 1);
        var loaded = (await store.LoadAsync(run.Id, Ct)).Ok();
        loaded.Subjects.Should().Equal(run.Subjects, "the subjects are FROZEN on the run — names, never values (D4)");
        loaded.Subjects.Should().OnlyContain(s => s.ExecutableRef == "BENCH_CLAUDE");
        (loaded.Oracle, loaded.Repeats, loaded.CreatedAt, loaded.ArtifactsPruned).Should().Be((run.Oracle, 3, Noon, false));
        (await store.PlanAsync(run, cells, Ct)).Reason().Should().Contain("already exists");
        (await store.PlanAsync(Run(), [], Ct)).Reason().Should().Contain("no cells");
        (await store.PlanAsync(Run(), cells, Ct)).Reason().Should().Contain("names that run");
    }

    /// <summary>S2: the api subject's transport (vendor, public endpoint, dialect) survives the row — <c>resume</c> launches the
    /// product from the run, never from a file that may have changed.</summary>
    [Fact]
    public async Task An_api_subjects_vendor_endpoint_and_dialect_are_frozen_on_the_run_and_read_back()
    {
        var grok = ProbeSubject.Parse("grok-api", "api", "grok-4.7", "BENCH_GATE_COAI_EXE", string.Empty, "grok", "https://api.x.ai/v1", "xai").Ok();
        var run = Run(Subject("claude-sonnet", "claude", "restricted"), grok);
        var cells = ProbeMatrix.Plan(ProbeWord.All, run.Subjects, repeats: 1).Ok().Cells.Select(c => ProbeCell.Pending(Guid.CreateVersion7(), run.Id, c)).ToList();
        var store = NewStore(new TestClock(Noon));

        (await store.PlanAsync(run, cells, Ct)).Ok();

        var loaded = (await store.LoadAsync(run.Id, Ct)).Ok();
        loaded.Subjects.Should().Equal(run.Subjects, "the transport is part of the frozen subject");
        var stored = loaded.Subject(grok.Id).Ok();
        (stored.Vendor, stored.Endpoint, stored.Dialect).Should().Be(("grok", "https://api.x.ai/v1", "xai"));
        loaded.Subject(run.Subjects[0].Id).Ok().Endpoint.Should().BeEmpty("a CLI subject has none");
    }

    /// <summary>S4: the probes a run was ASKED for are frozen in the order asked — the report recomputes the planner's dropped pairs
    /// from them — and a row naming something that is not a probe was edited, and is refused by name.</summary>
    [Fact]
    public async Task The_asked_probes_are_frozen_on_the_run_in_order_and_a_hand_edited_name_is_refused()
    {
        var run = Run(Subject("claude-sonnet", "claude")) with { Probes = [ProbeKind.WebSearch, ProbeKind.ReadInside, ProbeKind.ApiReachable] };
        var cells = ProbeMatrix.Plan(run.Probes, run.Subjects, repeats: 1).Ok().Cells.Select(c => ProbeCell.Pending(Guid.CreateVersion7(), run.Id, c)).ToList();
        var store = NewStore(new TestClock(Noon));
        (await store.PlanAsync(run, cells, Ct)).Ok();

        (await store.LoadAsync(run.Id, Ct)).Ok().Probes.Should().Equal([ProbeKind.WebSearch, ProbeKind.ReadInside, ProbeKind.ApiReachable],
            "api-reachable planned no cell here, and only the frozen list still names it");

        await using (var db = postgres.NewContext())
        {
            await db.ProbeRuns.Where(r => r.Id == run.Id).ExecuteUpdateAsync(u => u.SetProperty(r => r.Probes, ["ReadInside", "Frobnicate"]), Ct);
        }

        (await store.LoadAsync(run.Id, Ct)).Reason().Should().Contain("'Frobnicate' is not a probe");
    }

    [Fact]
    public async Task Two_workers_racing_for_one_cell_produce_exactly_one_winner()
    {
        var (run, cells) = Planned(count: 1);
        await NewStore(new TestClock(Noon)).PlanAsync(run, cells, Ct);

        var claims = await AllAtOnceAsync(
            Enumerable.Range(0, 16).Select(_ => NewStore(new TestClock(Noon))),
            (store, i) => store.ClaimNextAsync(run.Id, run.Subjects[0].Id, WorkerIdentity.Here($"lane-{i}"), Pin(), Ct));

        claims.Count(c => !c.Failed()).Should().Be(1, "State == Pending is in the WHERE of one UPDATE");
        claims.Where(c => c.Failed()).Should().OnlyContain(c => c.Reason().Contains(PostgresProbeStore.NoPendingCell) || c.Reason().Contains("claim races"));
        (await NewStore(new TestClock(Noon)).CellAsync(cells[0].Id, Ct)).Ok().Attempts.Should().Be(1, "the winner's claim counted once");
    }

    [Fact]
    public async Task Claims_come_in_matrix_order_for_the_subject_asked_and_a_terminal_cell_is_never_claimed()
    {
        var (run, plan, cells) = PlannedMatrix(TwoProbes, Subject("claude-sonnet", "claude"), Subject("codex-astra", "codex"));
        var store = NewStore(new TestClock(Noon));
        await store.PlanAsync(run, cells, Ct);
        var claude = run.Subjects[0].Id;

        var claimed = new List<string>();
        for (var i = 0; i < plan.Cells.Count(c => c.Subject == claude); i++)
        {
            var cell = (await store.ClaimNextAsync(run.Id, claude, Here(), Pin(), Ct)).Ok();
            cell.Subject.Should().Be(claude, "a lane claims for ITS subject only");
            claimed.Add($"{ProbeWord.Of(cell.Probe)} r{cell.Repeat}");
            (await store.SettleAsync(cell.Id, Here(), Settlement(), Ct)).Ok();
        }

        claimed.Should().Equal(plan.Cells.Where(c => c.Subject == claude).OrderBy(c => c.Slot).ThenBy(c => c.Position).Select(c => $"{ProbeWord.Of(c.Probe)} r{c.Repeat}"),
            "the next cell of the PLAN — slot, then position — repeats outermost");
        (await store.ClaimNextAsync(run.Id, claude, Here(), Pin(), Ct)).Reason().Should().Contain(PostgresProbeStore.NoPendingCell, "every claude cell is settled, and a settled cell is never claimed again");
        (await store.ClaimNextAsync(run.Id, run.Subjects[1].Id, Here(), Pin(), Ct)).Ok().Subject.Should().Be(run.Subjects[1].Id, "the other lane's cells are untouched");
    }

    [Fact]
    public async Task A_claim_records_its_pin_and_counts_the_attempt_and_is_refused_without_a_pin_or_an_owner()
    {
        var (run, cells) = Planned(count: 1);
        var store = NewStore(new TestClock(Noon));
        await store.PlanAsync(run, cells, Ct);

        (await store.ClaimNextAsync(run.Id, run.Subjects[0].Id, Here(), ProductPin.None, Ct)).Reason().Should().Contain("product pin");
        (await store.ClaimNextAsync(run.Id, run.Subjects[0].Id, new WorkerIdentity("lane", string.Empty, 0), Pin(), Ct)).Reason().Should().Contain("host and a pid");

        var claimed = (await store.ClaimNextAsync(run.Id, run.Subjects[0].Id, Here(), Pin('b'), Ct)).Ok();

        claimed.Pin.Should().Be(Pin('b'), "the pin is stored on the cell at claim time (D9)");
        claimed.Attempts.Should().Be(1);
        claimed.ClaimedAt.Should().Be(Noon);
        claimed.Owner.Pid.Should().Be(Environment.ProcessId);
    }

    [Fact]
    public async Task A_settled_cell_reads_back_with_its_facts_and_its_artefact_refs()
    {
        var (run, cells) = Planned(count: 1);
        var store = NewStore(new TestClock(Noon));
        await store.PlanAsync(run, cells, Ct);
        var owner = Here();
        await store.ClaimNextAsync(run.Id, run.Subjects[0].Id, owner, Pin(), Ct);
        var settlement = Settlement(ProbeFact.No);

        var settled = (await store.SettleAsync(cells[0].Id, owner, settlement, Ct)).Ok();

        settled.State.Should().Be(CellState.Settled);
        settled.Facts.Should().Be(settlement.Facts, "every fact survives the store in its three states, the exit code's capture flag included");
        settled.Artifacts.Should().Equal(settlement.Artifacts, "path, hash and length — never the bytes");
        settled.Reason.Should().Be(ProbeReason.None);
        (await store.CellAsync(cells[0].Id, Ct)).Ok().Should().BeEquivalentTo(settled, "a second read is the same fact");
        (await store.SettleAsync(cells[0].Id, owner, settlement, Ct)).Reason().Should().Contain("is Settled, not Claimed");
    }

    [Fact]
    public async Task A_settle_from_a_worker_that_does_not_hold_the_cell_is_refused_naming_both_and_writes_nothing()
    {
        var (run, cells) = Planned(count: 1);
        var store = NewStore(new TestClock(Noon));
        await store.PlanAsync(run, cells, Ct);
        await store.ClaimNextAsync(run.Id, run.Subjects[0].Id, WorkerIdentity.Here("lane-a"), Pin(), Ct);

        (await store.SettleAsync(cells[0].Id, WorkerIdentity.Here("lane-b"), Settlement(), Ct)).Reason().Should().Contain("held by lane-a").And.Contain("not lane-b");

        var cell = (await store.CellAsync(cells[0].Id, Ct)).Ok();
        cell.State.Should().Be(CellState.Claimed);
        cell.Artifacts.Should().BeEmpty("a refused settle writes no artefact ref");
        cell.Facts.Should().Be(ProbeFacts.None);
    }

    [Fact]
    public async Task An_unmeasured_kind_is_never_settled_even_by_the_owner()
    {
        var (run, cells) = Planned(count: 1);
        var store = NewStore(new TestClock(Noon));
        await store.PlanAsync(run, cells, Ct);
        var owner = Here();
        await store.ClaimNextAsync(run.Id, run.Subjects[0].Id, owner, Pin(), Ct);

        (await store.SettleAsync(cells[0].Id, owner, new ProbeSettlement(ProbeFacts.NothingCaptured(ProbeAttemptKind.Unmeasured, Bench.Domain.Trace.CapturedCount.Number(0)), []), Ct))
            .Reason().Should().Contain("handed back, never settled");
        (await store.CellAsync(cells[0].Id, Ct)).Ok().State.Should().Be(CellState.Claimed);
    }

    /// <summary>D8: a cell whose attempt ran into an empty account goes back Pending with the attempt COUNTED (its directory
    /// exists, so the next claim takes attempt 2), the kind <i>unmeasured</i> and the reason on the row — and never abandons.</summary>
    [Fact]
    public async Task A_hand_back_keeps_the_attempt_counted_records_the_reason_and_never_abandons()
    {
        var (run, cells) = Planned(count: 1);
        var store = NewStore(new TestClock(Noon));
        await store.PlanAsync(run, cells, Ct);
        var owner = Here();

        for (var attempt = 1; attempt <= Claimable.MaxAttempts + 1; attempt++)
        {
            (await store.ClaimNextAsync(run.Id, run.Subjects[0].Id, owner, Pin(), Ct)).Ok().Attempts.Should().Be(attempt, "each claim is a fresh attempt");
            var handed = (await store.HandBackUnmeasuredAsync(cells[0].Id, owner, attempt, ProbeReason.AccountOut, Ct)).Ok();
            handed.State.Should().Be(CellState.Pending, $"attempt {attempt}: an empty account is not the cell's fault");
            handed.Attempts.Should().Be(attempt, "the attempt ran and has a directory; it is not given back");
            handed.Owner.Should().Be(WorkerIdentity.Nobody);
            handed.Facts.Kind.Should().Be(ProbeAttemptKind.Unmeasured);
            handed.Reason.Should().Be(ProbeReason.AccountOut);
        }
    }

    [Fact]
    public async Task A_hand_back_from_a_worker_that_does_not_hold_the_cell_or_at_another_attempt_changes_nothing()
    {
        var (run, cells) = Planned(count: 1);
        var store = NewStore(new TestClock(Noon));
        await store.PlanAsync(run, cells, Ct);
        await store.ClaimNextAsync(run.Id, run.Subjects[0].Id, WorkerIdentity.Here("lane-a"), Pin(), Ct);

        (await store.HandBackUnmeasuredAsync(cells[0].Id, WorkerIdentity.Here("lane-b"), attempt: 1, ProbeReason.AccountOut, Ct)).Reason().Should().Contain("not claimed by");
        (await store.HandBackUnmeasuredAsync(cells[0].Id, WorkerIdentity.Here("lane-a"), attempt: 2, ProbeReason.AccountOut, Ct)).Reason().Should().Contain("at attempt 2");
        (await store.CellAsync(cells[0].Id, Ct)).Ok().State.Should().Be(CellState.Claimed);
    }

    [Fact]
    public async Task A_stale_claim_by_a_dead_pid_on_this_host_is_requeued_and_a_live_one_or_another_hosts_is_left_alone()
    {
        var clock = new TestClock(Noon);
        var (run, cells) = Planned(count: 3);
        var store = NewStore(clock);
        await store.PlanAsync(run, cells, Ct);
        var subject = run.Subjects[0].Id;
        await store.ClaimNextAsync(run.Id, subject, TestWorkers.Dead("dead-here"), Pin(), Ct);
        await store.ClaimNextAsync(run.Id, subject, WorkerIdentity.Stored("elsewhere", "another-host", 4242), Pin(), Ct);
        await store.ClaimNextAsync(run.Id, subject, Here(), Pin(), Ct);

        clock.Now = Noon.AddHours(2);
        await NewStore(clock).SweepAsync(TimeSpan.FromMinutes(30), Ct);

        var after = await store.CellsAsync(run.Id, Ct);
        after.Single(c => c.Id == cells[0].Id).State.Should().Be(CellState.Pending, "a dead pid on this host is provably gone");
        after.Single(c => c.Id == cells[0].Id).Attempts.Should().Be(1, "a hand-back keeps the count; only the next claim moves it");
        after.Single(c => c.Id == cells[1].Id).State.Should().Be(CellState.Claimed, "we cannot see another host's process table");
        after.Single(c => c.Id == cells[2].Id).State.Should().Be(CellState.Claimed, "a live owner's claim is never handed back, however stale the clock says it is");
    }

    [Fact]
    public async Task The_third_hand_back_by_the_sweep_abandons_the_cell_with_its_reason()
    {
        var connection = await postgres.NewDatabaseAsync($"probe_abandon_{Guid.NewGuid():N}");
        var clock = new TestClock(Noon);
        var (run, cells) = Planned(count: 1);
        var store = new PostgresProbeStore(PostgresFixture.Context(connection), clock);
        await store.PlanAsync(run, cells, Ct);

        for (var i = 0; i < Claimable.MaxAttempts; i++)
        {
            clock.Now = Noon.AddHours(i * 2);
            (await store.ClaimNextAsync(run.Id, run.Subjects[0].Id, TestWorkers.Dead($"dead-{i}"), Pin(), Ct)).Ok();
            clock.Now = Noon.AddHours((i * 2) + 1);
            var report = await store.SweepAsync(TimeSpan.FromMinutes(30), Ct);
            (report.Requeued, report.Abandoned).Should().Be(i < Claimable.MaxAttempts - 1 ? (1, 0) : (0, 1));
        }

        var cell = (await store.CellAsync(cells[0].Id, Ct)).Ok();
        cell.State.Should().Be(CellState.Abandoned);
        cell.Reason.Should().Be(ProbeReason.Abandoned);
        cell.Owner.Should().Be(WorkerIdentity.Nobody);
        (await store.ClaimNextAsync(run.Id, run.Subjects[0].Id, Here(), Pin(), Ct)).Reason().Should().Contain(PostgresProbeStore.NoPendingCell, "abandoned is terminal");
    }

    [Fact]
    public async Task Two_sweepers_racing_over_one_dead_owner_hand_the_cell_back_once()
    {
        var connection = await postgres.NewDatabaseAsync($"probe_sweep_{Guid.NewGuid():N}");
        var clock = new TestClock(Noon);
        var (run, cells) = Planned(count: 1);
        var planner = new PostgresProbeStore(PostgresFixture.Context(connection), clock);
        await planner.PlanAsync(run, cells, Ct);
        await planner.ClaimNextAsync(run.Id, run.Subjects[0].Id, TestWorkers.Dead("dead"), Pin(), Ct);
        clock.Now = Noon.AddHours(2);

        var reports = await AllAtOnceAsync(
            Enumerable.Range(0, 8).Select(_ => new PostgresProbeStore(PostgresFixture.Context(connection), clock)),
            (store, _) => store.SweepAsync(TimeSpan.FromMinutes(30), Ct));

        reports.Sum(r => r.Total).Should().Be(1, "the hand-back re-checks state, owner, claim time and attempts in its WHERE");
        (await planner.CellAsync(cells[0].Id, Ct)).Ok().Attempts.Should().Be(1);
        (await planner.ClaimNextAsync(run.Id, run.Subjects[0].Id, Here(), Pin(), Ct)).Ok().Attempts.Should().Be(2, "the next claim moves it exactly once");
    }

    /// <summary>D2: <c>rerun</c> appends generation 2 beside generation 1 — the settled verdict stays — and a resume claims only
    /// what is not settled: the new generation and the cells never measured.</summary>
    [Fact]
    public async Task A_rerun_appends_generation_two_beside_generation_one_and_a_resume_claims_only_the_unsettled()
    {
        var (run, cells) = Planned(count: 3);
        var store = NewStore(new TestClock(Noon));
        await store.PlanAsync(run, cells, Ct);
        var subject = run.Subjects[0].Id;
        var owner = Here();
        var first = (await store.ClaimNextAsync(run.Id, subject, owner, Pin(), Ct)).Ok();
        await store.SettleAsync(first.Id, owner, Settlement(ProbeFact.Yes), Ct);

        var second = (await store.NextGenerationAsync(first.Id, Guid.CreateVersion7(), Ct)).Ok();

        second.Generation.Should().Be(2);
        second.State.Should().Be(CellState.Pending);
        (second.Probe, second.Subject, second.Repeat).Should().Be((first.Probe, first.Subject, first.Repeat));
        var all = await store.CellsAsync(run.Id, Ct);
        all.Should().HaveCount(4, "a generation is appended, nothing is rewritten");
        all.Single(c => c.Id == first.Id).Should().Match<ProbeCell>(c => c.State == CellState.Settled && c.Facts.CanaryRead == ProbeFact.Yes, "generation 1 is still a readable fact");

        (await store.NextGenerationAsync(first.Id, Guid.CreateVersion7(), Ct)).Reason().Should().Contain("generation 2").And.Contain("is Pending");
        (await store.NextGenerationAsync(cells[1].Id, Guid.CreateVersion7(), Ct)).Reason().Should().Contain("is Pending", "a cell never measured is not re-run; it is resumed");
        (await store.NextGenerationAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), Ct)).Reason().Should().Contain("no probe cell");

        var resumed = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            resumed.Add((await store.ClaimNextAsync(run.Id, subject, owner, Pin(), Ct)).Ok().Id);
        }

        resumed.Should().BeEquivalentTo([second.Id, cells[1].Id, cells[2].Id], "exactly the unsettled cells — the new generation and the two never measured");
        (await store.ClaimNextAsync(run.Id, subject, owner, Pin(), Ct)).Reason().Should().Contain(PostgresProbeStore.NoPendingCell);
    }

    [Fact]
    public async Task Reads_answer_the_highest_settled_generation_per_lineage_and_every_generation_for_status()
    {
        var (run, cells) = Planned(count: 2);
        var store = NewStore(new TestClock(Noon));
        await store.PlanAsync(run, cells, Ct);
        var reads = new PostgresProbeReads(postgres.NewContext());
        var subject = run.Subjects[0].Id;
        var owner = Here();
        var first = (await store.ClaimNextAsync(run.Id, subject, owner, Pin(), Ct)).Ok();
        await store.SettleAsync(first.Id, owner, Settlement(ProbeFact.Yes), Ct);
        var second = (await store.NextGenerationAsync(first.Id, Guid.CreateVersion7(), Ct)).Ok();

        (await reads.LatestSettledAsync(run.Id, Ct)).Select(c => (c.Id, c.Generation)).Should().Equal([(first.Id, 1)], "a pending re-run does not hide the verdict it re-measures; a lineage never settled is not listed");
        (await reads.CellsAsync(run.Id, Ct)).Should().HaveCount(3, "status lists every generation");

        var claimed = (await store.ClaimNextAsync(run.Id, subject, owner, Pin(), Ct)).Ok();
        claimed.Id.Should().Be(second.Id);
        await store.SettleAsync(second.Id, owner, Settlement(ProbeFact.No), Ct);

        (await reads.LatestSettledAsync(run.Id, Ct)).Select(c => (c.Id, c.Generation, c.Facts.CanaryRead)).Should().Equal([(second.Id, 2, ProbeFact.No)]);
        (await reads.RunAsync(run.Id, Ct)).Ok().Subjects.Should().Equal(run.Subjects);
        (await reads.RunAsync(Guid.CreateVersion7(), Ct)).Reason().Should().Contain("no probe run");
        (await reads.RecentRunsAsync(1000, Ct)).Should().Contain(r => r.Id == run.Id);
    }

    [Fact]
    public async Task Marking_the_artefacts_pruned_is_refused_while_a_cell_is_pending_or_claimed()
    {
        var (run, cells) = Planned(count: 1);
        var store = NewStore(new TestClock(Noon));
        await store.PlanAsync(run, cells, Ct);
        var owner = Here();

        (await store.MarkArtifactsPrunedAsync(run.Id, Ct)).Reason().Should().Contain("1 Pending", "a prune while something is still to be measured would delete the evidence of a cell nobody has read");
        await store.ClaimNextAsync(run.Id, run.Subjects[0].Id, owner, Pin(), Ct);
        (await store.MarkArtifactsPrunedAsync(run.Id, Ct)).Reason().Should().Contain("1 Claimed");
        await store.SettleAsync(cells[0].Id, owner, Settlement(), Ct);

        (await store.MarkArtifactsPrunedAsync(run.Id, Ct)).Ok().ArtifactsPruned.Should().BeTrue();
        (await store.LoadAsync(run.Id, Ct)).Ok().ArtifactsPruned.Should().BeTrue();
        (await store.MarkArtifactsPrunedAsync(Guid.CreateVersion7(), Ct)).Reason().Should().Contain("no probe run");
    }

    [Fact]
    public void The_probe_migration_creates_the_two_probe_tables_with_the_generation_index_and_touches_no_other()
    {
        using var db = postgres.NewContext();
        var migrations = db.GetService<IMigrationsAssembly>();
        var id = migrations.Migrations.Keys.Should().ContainSingle(k => k.EndsWith("_ProbeTables", StringComparison.Ordinal)).Subject;

        var operations = migrations.CreateMigration(migrations.Migrations[id], db.Database.ProviderName!).UpOperations;

        operations.OfType<CreateTableOperation>().Select(o => o.Name).Should().BeEquivalentTo(["probe_runs", "probe_cells"]);
        operations.OfType<ITableMigrationOperation>().Select(o => o.Table).Should().OnlyContain(t => t.StartsWith("probe_", StringComparison.Ordinal),
            "no existing table is touched — the probes are a sibling context in the same database");
        operations.OfType<CreateIndexOperation>().Should().Contain(i => i.Table == "probe_cells" && i.IsUnique
            && i.Columns.SequenceEqual(new[] { "RunId", "Probe", "SubjectId", "Repeat", "Generation" }), "a generation is appended, never duplicated (D2)");
        PostgresGatePublicationSource.ProbeEntities(db).Select(e => e.GetTableName()).Should().Equal(["probe_cells", "probe_runs"]);
    }

    /// <summary>S1 acceptance 5: the migration applies on an empty database and on one already holding the gate's tables.</summary>
    [Fact]
    public async Task The_probe_migration_applies_on_a_database_already_holding_the_gate_tables_and_on_an_empty_one()
    {
        var connection = await postgres.NewEmptyDatabaseAsync($"probe_mig_{Guid.NewGuid():N}");

        await using (var db = PostgresFixture.Context(connection))
        {
            await db.GetService<IMigrator>().MigrateAsync("20260929184826_GateThinkingThreeStates", Ct);
            (await TablesAsync(db)).Should().Contain("gate_cells").And.NotContain("probe_cells", "the database holds the gate's tables and not yet the probes'");

            await db.Database.MigrateAsync(Ct);
            (await TablesAsync(db)).Should().Contain(["gate_cells", "probe_runs", "probe_cells"]);
            (await db.ProbeRuns.CountAsync(Ct)).Should().Be(0);
        }

        var fresh = await postgres.NewDatabaseAsync($"probe_fresh_{Guid.NewGuid():N}");
        await using var empty = PostgresFixture.Context(fresh);
        (await TablesAsync(empty)).Should().Contain(["probe_runs", "probe_cells"], "every migration from the first applies on an empty database");
    }

    private static async Task<IReadOnlyList<string>> TablesAsync(BenchDbContext db) =>
        await db.Database.SqlQueryRaw<string>("select table_name as \"Value\" from information_schema.tables where table_schema = 'public'").ToListAsync(Ct);

    /// <summary>Runs one call per store at the same instant — the gate store tests' gate, so a "race" is a race and not a queue.</summary>
    private static async Task<T[]> AllAtOnceAsync<T>(IEnumerable<PostgresProbeStore> stores, Func<PostgresProbeStore, int, Task<T>> call)
    {
        var warmed = stores.ToList();
        await Task.WhenAll(warmed.Select(store => store.CellsAsync(Guid.Empty, Ct)));
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var calls = warmed.Select(async (store, i) =>
        {
            await gate.Task;
            return await call(store, i);
        }).ToList();

        gate.SetResult();
        return await Task.WhenAll(calls);
    }
}
