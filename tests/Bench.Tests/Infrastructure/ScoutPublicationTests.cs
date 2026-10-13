using Bench.Application.Gate;
using Bench.Domain.Gate;
using Bench.Domain.Scouts;
using Bench.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Infrastructure.ScoutStoreFixtures;

namespace Bench.Tests.Infrastructure;

/// <summary>The scout tables travel in the same public export under the same <see cref="PublicationGuard"/> as the gate's and the
/// probes' (§3.13): a database holding a settled scout cell with its per-scout records and a paid attempt passes, the claim owner never
/// reaches a public row, and a path planted in a scout row is refused naming the table, the column and the row. Each test runs on a
/// database of its own: the guard reads everything.</summary>
[Collection("postgres")]
public sealed class ScoutPublicationTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_database_holding_a_scout_run_passes_the_guard_and_its_rows_reach_the_export_without_the_claim_owner()
    {
        var connection = await CampaignAsync();

        (await ViolationsAsync(connection)).Should().BeEmpty("no path, url, host or private name can reach a scout row");

        var tables = System.Text.Json.Nodes.JsonNode.Parse((await ExportAsync(connection)).Ok())!["tables"]!;
        tables["scout_cells"]!.AsArray().Should().HaveCount(2);
        tables["scout_records"]!.AsArray().Should().HaveCount(2);
        tables["scout_attempts"]!.AsArray().Should().ContainSingle();
        tables["scout_tasks"]!.AsArray().Should().ContainSingle();
        tables["scout_cells"]!.AsArray().Select(c => c!.AsObject()).Should().OnlyContain(c => !c.ContainsKey("Owner") && !c.ContainsKey("OwnerHost") && !c.ContainsKey("OwnerPid"));
        tables.ToJsonString().Should().NotContain(Environment.MachineName);
    }

    [Fact]
    public async Task A_path_planted_in_a_scout_row_is_refused_naming_table_column_and_row()
    {
        var connection = await CampaignAsync();
        Guid recordId;
        await using (var db = PostgresFixture.Context(connection))
        {
            var record = db.ScoutRecords.OrderBy(r => r.Ordinal).First();
            record.KeyFiles = [@"C:\work\tree\src\a.cs"];
            recordId = record.Id;
            await db.SaveChangesAsync(Ct);
        }

        (await ViolationsAsync(connection)).Select(v => v.Describe).Should().Equal($"scout_records.KeyFiles row {recordId}: {PublicationGuard.DriveRule}");
        (await ExportAsync(connection)).Reason().Should().Contain("scout_records.KeyFiles");
    }

    private async Task<string> CampaignAsync()
    {
        var connection = await postgres.NewDatabaseAsync($"scout_pub_{Guid.NewGuid():N}");
        var store = new PostgresScoutStore(PostgresFixture.Context(connection), new TestClock(Noon));
        var (run, tasks, cells) = Planned(arms: [ScoutArmId.A1, ScoutArmId.B1]);
        await store.PlanAsync(run, tasks, cells, Ct);
        var owner = Here();

        var cell = (await store.ClaimNextAsync(run.Id, owner, Pin(), Ct)).Ok();
        var attempt = ((ScoutReservation.Reserved)await store.ReserveAsync(cell.Id, owner, Guid.CreateVersion7(), 3m, Ct)).Attempt;
        await store.MarkLaunchingAsync(attempt.Id, Ct);
        await store.SettleAttemptAsync(attempt.Id, 2.5m, Ct);
        await store.SettleAsync(cell.Id, owner, Scored(), [Scout(1), Scout(2)], Ct);

        return connection;
    }

    private static async Task<IReadOnlyList<GuardViolation>> ViolationsAsync(string connection)
    {
        await using var db = PostgresFixture.Context(connection);
        var source = new PostgresGatePublicationSource(db);

        return GatePublication.Check(await source.ReadAsync(Ct), Names(), source.PublicUrlColumns);
    }

    private static async Task<Bench.Domain.Outcome<string>> ExportAsync(string connection)
    {
        await using var db = PostgresFixture.Context(connection);
        var source = new PostgresGatePublicationSource(db);

        return GatePublication.Export(await source.ReadAsync(Ct), Names(), source.PublicUrlColumns, Noon);
    }

    private static PrivateNames Names() => GatePublicationTests.SampleNames().WithHosts([Environment.MachineName]);
}
