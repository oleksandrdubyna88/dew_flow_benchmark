using Bench.Application.Gate;
using Bench.Domain.Gate;
using Bench.Domain.Probes;
using Bench.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Infrastructure.ProbeStoreFixtures;

namespace Bench.Tests.Infrastructure;

/// <summary>S1 acceptance 5: the publication guard passes over the probe rows — ids, enum names, hashes, numbers, references
/// and artefact paths relative to the root — and the probe tables travel in the same export under the same guard as the
/// gate's. A planted path is refused naming the table, the column and the row. Each test runs on a database of its own:
/// the guard reads everything.</summary>
[Collection("postgres")]
public sealed class ProbePublicationTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_database_holding_a_probe_run_passes_the_guard_and_its_rows_reach_the_export_without_the_claim_owner()
    {
        var connection = await CampaignAsync();

        (await ViolationsAsync(connection)).Should().BeEmpty("no path, url, host or private name can reach a probe row");

        var document = System.Text.Json.Nodes.JsonNode.Parse((await ExportAsync(connection)).Ok())!;
        var cells = document["tables"]!["probe_cells"]!.AsArray();
        cells.Should().HaveCount(2, "the probe tables are published under the same rule as the gate's (D11)");
        cells.Select(c => c!.AsObject()).Should().OnlyContain(c => !c.ContainsKey("Owner") && !c.ContainsKey("OwnerHost") && !c.ContainsKey("OwnerPid"),
            "the claim owner is sweep state, never a public field");
        document["tables"]!["probe_runs"]!.AsArray().Should().ContainSingle();
        document.ToJsonString().Should().NotContain(Environment.MachineName);
    }

    [Fact]
    public async Task A_path_planted_in_a_probe_row_is_refused_naming_table_column_and_row()
    {
        var connection = await CampaignAsync();
        Guid runId;
        Guid cellId;
        await using (var db = PostgresFixture.Context(connection))
        {
            var run = db.ProbeRuns.Single();
            run.SubjectExecutableRefs = ["/home/someone/bin/claude"];
            var cell = db.ProbeCells.OrderBy(c => c.Slot).First();
            cell.ArtifactPaths = [@"D:\work\probes\answer.txt"];
            (runId, cellId) = (run.Id, cell.Id);
            await db.SaveChangesAsync(Ct);
        }

        var violations = (await ViolationsAsync(connection)).Select(v => v.Describe).ToList();

        violations.Should().BeEquivalentTo(
        [
            $"probe_runs.SubjectExecutableRefs row {runId}: " + PublicationGuard.HomeRule,
            $"probe_cells.ArtifactPaths row {cellId}: " + PublicationGuard.DriveRule,
        ]);
        (await ExportAsync(connection)).Reason().Should().Contain("refused").And.Contain("probe_cells.ArtifactPaths");
    }

    /// <summary>A fresh database holding one probe run: a settled cell with three artefact refs and a cell handed back unmeasured.</summary>
    private async Task<string> CampaignAsync()
    {
        var connection = await postgres.NewDatabaseAsync($"probe_pub_{Guid.NewGuid():N}");
        var store = new PostgresProbeStore(PostgresFixture.Context(connection), new TestClock(Noon));
        var (run, cells) = Planned(count: 2);
        await store.PlanAsync(run, cells, Ct);
        var owner = Here();

        var first = (await store.ClaimNextAsync(run.Id, run.Subjects[0].Id, owner, Pin(), Ct)).Ok();
        await store.SettleAsync(first.Id, owner, Settlement(), Ct);
        var second = (await store.ClaimNextAsync(run.Id, run.Subjects[0].Id, owner, Pin(), Ct)).Ok();
        await store.HandBackUnmeasuredAsync(second.Id, owner, attempt: 1, ProbeReason.AccountOut, Ct);

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
