using Bench.Application.Gate;
using Bench.Domain.Gate;
using Bench.Domain.Scouts;
using Bench.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
        tables["scout_claims"]!.AsArray().Single()!["Path"]!.GetValue<string>().Should().Be("src/Store.cs", "a public task keeps its claim paths");
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

    /// <summary>S4 (§3.13, D10): a PRIVATE task's rows travel through the same walk, projected (<see cref="ScoutRowPublication"/>): its
    /// <c>file://</c> url — which the url rule would refuse — becomes its label, its shas, statement, G and removals are emptied, its scouts'
    /// KEY FILES and its claims' paths too, and its commitments are published in their place. The database itself is untouched. Written RED
    /// first: the walk refused the export whole with <c>scout_tasks.Repository … carries a url</c>.</summary>
    [Fact]
    public async Task A_private_task_is_exported_as_its_label_language_and_commitments_with_no_url_path_statement_or_claim_path()
    {
        const string url = "file:////wsl.localhost/Ubuntu/home/someone/git/zqvtharnish-repo";
        const string parent = "d1e2f3a4d1e2f3a4d1e2f3a4d1e2f3a4d1e2f3a4";
        var task = ScoutTask.Frozen(ScoutTaskId.Parse("p03").Ok(), url, Bench.Domain.Targets.CommitSha.Parse(parent).Ok(), Bench.Domain.Targets.CommitSha.Parse("e5f6a7b8e5f6a7b8e5f6a7b8e5f6a7b8e5f6a7b8").Ok(),
            ScoutQuestionKind.WhereDecided, "Where is the per-tenant rate limit decided for the Vrembolix gateway?", ["src/Zqvtharnish/Kleebor.php", "src/Zqvtharnish/Throttle.php"], [], ["todo/PLAN_kleebor.md"],
            new string('c', 64), ScoutTaskPrivacy.Private, "PHP").Ok() with
        { Commitments = new ScoutTaskCommitments(new string('1', 64), new string('2', 64)) };
        var connection = await CampaignAsync(task, keyFiles: ["src/Zqvtharnish/Kleebor.php"], claimPath: "src/Zqvtharnish/Throttle.php");

        (await ViolationsAsync(connection)).Should().BeEmpty("the projection replaced the url with the label before the guard read the row");
        var export = (await ExportAsync(connection)).Ok();
        var tables = System.Text.Json.Nodes.JsonNode.Parse(export)!["tables"]!;
        var row = tables["scout_tasks"]!.AsArray().Single()!.AsObject();
        (row["Repository"]!.GetValue<string>(), row["Privacy"]!.GetValue<string>(), row["Language"]!.GetValue<string>()).Should().Be(("p03", "Private", "PHP"));
        (row["StatementCommitment"]!.GetValue<string>(), row["GroundTruthCommitment"]!.GetValue<string>()).Should().Be((new string('1', 64), new string('2', 64)));
        row["Statement"]!.GetValue<string>().Should().BeEmpty();
        row["GroundTruth"]!.AsArray().Should().BeEmpty();
        row["Removals"]!.AsArray().Should().BeEmpty();
        (row["ParentSha"]!.GetValue<string>(), row["FixSha"]!.GetValue<string>()).Should().Be((string.Empty, string.Empty), "no raw hash beside an HMAC");
        tables["scout_records"]!.AsArray().Should().OnlyContain(r => r!["KeyFiles"]!.AsArray().Count == 0);
        tables["scout_claims"]!.AsArray().Single()!["Path"]!.GetValue<string>().Should().BeEmpty();
        foreach (var planted in new[] { url, "zqvtharnish", "Kleebor", "Throttle.php", "Vrembolix", "todo/PLAN_kleebor.md", parent, "e5f6a7b8e5f6a7b8" })
        {
            export.Should().NotContain(planted);
        }

        await using var db = PostgresFixture.Context(connection);
        (await db.ScoutTasks.AsNoTracking().ToListAsync(Ct)).Single().Repository.Should().Be(url, "the walk projects copies; the local database keeps the detail");
    }

    private Task<string> CampaignAsync() => CampaignAsync(FrozenTask("t01"), keyFiles: ["src/Store.cs", "src/Other.cs"], claimPath: "src/Store.cs");

    private async Task<string> CampaignAsync(ScoutTask task, IReadOnlyList<string> keyFiles, string claimPath)
    {
        var connection = await postgres.NewDatabaseAsync($"scout_pub_{Guid.NewGuid():N}");
        var store = new PostgresScoutStore(PostgresFixture.Context(connection), new TestClock(Noon));
        var run = ScoutRun.Planned(Guid.CreateVersion7(), Noon, Pins(), seed: 7, 1, [ScoutArmId.A1, ScoutArmId.B1], [task.Id], 10m).Ok();
        var cells = ScoutSchedule.Plan([task.Id], [ScoutArmId.A1, ScoutArmId.B1], [], 1, seed: 7).Ok().Select(c => ScoutCell.Pending(Guid.CreateVersion7(), run.Id, c)).ToList();
        await store.PlanAsync(run, [task], cells, Ct);
        var owner = Here();

        var cell = (await store.ClaimNextAsync(run.Id, owner, Pin(), Ct)).Ok();
        var attempt = ((ScoutReservation.Reserved)await store.ReserveAsync(cell.Id, owner, Guid.CreateVersion7(), 3m, Ct)).Attempt;
        await store.MarkLaunchingAsync(attempt.Id, Ct);
        await store.SettleAttemptAsync(attempt.Id, 2.5m, Ct);
        await store.SettleAsync(cell.Id, owner, Scored(), [Scout(1) with { KeyFiles = keyFiles }, Scout(2) with { KeyFiles = keyFiles }], Ct);
        await new PostgresScoutVerdicts(PostgresFixture.Context(connection)).StoreVerdictsAsync(
            [new ScoutAssessedClaim(new ScoutClaimKey(cell.Id, 1, 1), run.Id, claimPath, Bench.Domain.Trace.CapturedCount.Number(3), new string('e', 64), ScoutClaimVerdict.Right, "gpt-test", ScoutAssessment.RubricSha256, Noon)], Ct);

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
