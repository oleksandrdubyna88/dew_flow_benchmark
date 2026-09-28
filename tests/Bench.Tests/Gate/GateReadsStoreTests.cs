using Bench.Application.Gate;
using Bench.Contracts;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Gate.Import;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bench.Tests.Gate;

/// <summary>The read side over a real database (E6): the ninth table's idempotent, all-or-none record, the reads a report is
/// computed from — the rubric catalog built from the ROWS, with no prompt folder anywhere — and the imported calibration's
/// phase-2 population pinned THROUGH the query, the object the API answers and the page renders.</summary>
[Collection("postgres")]
public sealed class GateReadsStoreTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_suite_is_recorded_once_and_recording_it_again_changes_nothing()
    {
        var connection = await ImportRig.DatabaseAsync(postgres, "tasks");
        var suite = ImportFixture.Suite;

        (await Tasks(connection).RecordAsync(suite, Ct)).Ok().Should().Be(suite.Tasks.Count);
        (await Tasks(connection).RecordAsync(suite, Ct)).Ok().Should().Be(0, "a second import of the same suite is a no-op");

        var read = await Reads(connection).TasksAsync(suite.Stamp, Ct);
        read.Select(t => t.Canonical).Should().BeEquivalentTo(suite.Tasks.Select(t => t.Summary.Canonical));
        (await Reads(connection).RecordedStampsAsync(Ct)).Should().Equal(suite.Stamp);
        (await Reads(connection).TasksAsync("some-other#000000000000", Ct)).Should().BeEmpty("a stamp nobody recorded reads as not recorded");
    }

    [Fact]
    public async Task A_different_row_under_a_stamp_already_held_is_refused_and_nothing_of_that_record_is_written()
    {
        var connection = await ImportRig.DatabaseAsync(postgres, "tasks");
        var suite = ImportFixture.Suite;
        var planted = suite.Tasks[0].Summary;
        await using (var db = PostgresFixture.Context(connection))
        {
            db.GateSuiteTasks.Add(new GateSuiteTaskRow
            {
                SuiteStamp = suite.Stamp,
                TaskId = planted.Id.Value,
                Language = "Cobol",
                IsCalibration = planted.IsCalibration,
                Hosts = planted.Hosts.Canonical,
                SeedIds = [.. planted.Seeds.Select(s => s.Id.Value)],
                SeedCrossEpic = [.. planted.Seeds.Select(s => s.CrossEpic)],
            });
            await db.SaveChangesAsync(Ct);
        }

        var refused = await Tasks(connection).RecordAsync(suite, Ct);

        refused.Should().BeOfType<Outcome<int>.Fail>().Which.Reason.Should().Contain(planted.Id.Value).And.Contain(suite.Stamp);
        await using var check = PostgresFixture.Context(connection);
        (await check.GateSuiteTasks.CountAsync(Ct)).Should().Be(1, "all or none — the other tasks did not land beside the refused one");
    }

    [Fact]
    public async Task The_reads_return_every_imported_cell_and_build_their_rubric_catalog_from_the_rows()
    {
        using var rig = await ImportRig.NewAsync(postgres);
        (await rig.ImportAsync(Ct)).Ok();
        var reads = Reads(rig.Connection);

        var records = await reads.RecordsAsync(Ct);
        var rubrics = await reads.RubricsAsync(Ct);
        var verdicts = await reads.VerdictsAsync([.. records.Select(r => r.RunId)], new RubricCatalog(rubrics), Ct);

        records.Select(r => r.RunId).Should().BeEquivalentTo((await rig.RecordsAsync(Ct)).Select(r => r.RunId));
        rubrics.Should().ContainSingle().Which.Should().Be(rig.Strict.Rubric, "the stored verdicts carry exactly the rubric they were ingested under");
        verdicts.Should().HaveCount((await rig.VerdictsAsync(records, Ct)).Count);
        (await reads.PromptHashAsync(Guid.CreateVersion7(), Ct)).Should().BeEmpty();
    }

    /// <summary>The DoD of E5 re-read through E6's query — the object the API answers and the page renders: the phase-2
    /// population's per-model numbers as the other harness published them (all tasks), the calibration tasks apart in the
    /// default reading, every turn-level column KNOWN for a harness that kept a ledger, and every strict percentage
    /// withheld as <c>not-hand-checked</c> while its counts stay.</summary>
    [Fact]
    public async Task The_imported_phase_two_population_reads_through_the_query_as_the_other_harness_published_it()
    {
        using var rig = await ImportRig.NewAsync(postgres);
        (await rig.ImportAsync(Ct)).Ok();
        (await Tasks(rig.Connection).RecordAsync(ImportFixture.Suite, Ct)).Ok();
        var reads = Reads(rig.Connection);
        var scope = ((GateAnswer<IReadOnlyList<GateScopeDto>>.Answered)await GateReportQuery.ScopesAsync(reads, "feature", Ct)).Value.Single();
        await using var db = rig.Db();
        var models = await db.GateReviewers.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Model, Ct);

        var table = ((GateAnswer<GateModelTableDto>.Answered)await GateReportQuery.ModelsAsync(reads, "feature", scope.Id, "strict-v1", Ct)).Value;

        scope.TasksRecorded.Should().BeTrue();
        scope.Sources.Should().Equal("calib-py");
        table.AllTaskRows.Should().HaveCount(4);
        foreach (var row in table.AllTaskRows)
        {
            var expected = ImportFixture.ExpectedFor(models[row.ReviewerId]);
            (row.Runs, row.Attempts, row.AttemptsFailed).Should().Be(((int)expected["runs"]!, (int)expected["attempts"]!, (int)expected["attempts_failed"]!), row.ReviewerId);
            row.ValidPct.Should().Be(GateFigureDto.Of((double)expected["valid_pct"]!), row.ReviewerId);
            row.SecondsP50.Should().Be(GateFigureDto.Of((double)expected["sec_p50"]!), row.ReviewerId);
            row.CostPerRun.Should().Be(GateFigureDto.Of((double)expected["cost_per_run"]!), row.ReviewerId);
            row.TokensInPerRun.Should().Be(GateFigureDto.Of((double)expected["tokens_in_per_run"]!), row.ReviewerId);
            row.RepairRuns.Known.Should().BeTrue("the calibration kept a ledger — its repair columns are counts, not unknowns");
            row.SupportedPct.Should().Be(GateFigureDto.NotHandChecked, "no person has hand-checked these verdicts");
            row.Supported.Should().BeGreaterThan(0, "the counts stay visible beside the withheld percentage");
        }

        var measured = table.Rows.ToDictionary(r => r.ReviewerId);
        foreach (var calibration in table.CalibrationRows)
        {
            (measured[calibration.ReviewerId].Runs + calibration.Runs).Should().Be(table.AllTaskRows.Single(a => a.ReviewerId == calibration.ReviewerId).Runs,
                "the default reading and the calibration rows partition every task, and neither holds the other's");
        }

        table.CalibrationRows.Should().NotBeEmpty("the fixture carries the calibration's two transport-setting tasks");
        table.PerTask.Where(t => t.Calibration).Select(t => t.TaskId).Distinct().Should().BeEquivalentTo(
            ImportFixture.Suite.Tasks.Where(t => t.IsCalibration).Select(t => t.Id.Value));
    }

    private static PostgresGateReads Reads(string connection) => new(PostgresFixture.Context(connection), TimeProvider.System);

    private static PostgresGateSuiteTasks Tasks(string connection) => new(PostgresFixture.Context(connection), TimeProvider.System);
}
