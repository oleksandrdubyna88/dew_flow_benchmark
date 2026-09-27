using System.Text;
using Bench.Application.Gate;
using Bench.Domain.Gate;
using Bench.Domain.Runs;
using Bench.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Infrastructure.GateStoreFixtures;

namespace Bench.Tests.Infrastructure;

/// <summary>The publication guard's second line, over REAL rows: every row of every <c>gate_*</c> table and every
/// artefact path is re-read from the database and refused if it carries a url, a machine path or a private name —
/// the <c>ModelConfigTests</c> re-read precedent. The sample suite's private names are always loaded, and the
/// operator's own suite too when <c>BENCH_GATE_SUITE</c> names one. Each test runs on a database of its own: the
/// guard reads everything, so a dirty row planted by one test would be every other test's failure.</summary>
[Collection("postgres")]
public sealed class GatePublicationTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_database_holding_a_whole_campaign_passes_the_guard()
    {
        var connection = await CampaignAsync();

        var violations = await ViolationsAsync(connection);

        violations.Should().BeEmpty("ids, hashes, enum names, numbers, a public vendor url and a redacted cause are all publishable");
    }

    [Fact]
    public async Task Every_dirty_row_is_named_by_table_column_and_row_id()
    {
        var connection = await CampaignAsync();
        await using (var db = PostgresFixture.Context(connection))
        {
            var path = Reviewer("dirty-path");
            path.Model = @"C:\Users\x\models\m.gguf";
            var url = Reviewer("dirty-url");
            url.RemoteVendor = "https://internal.example/team";
            var name = Reviewer("dirty-name");
            name.KeyName = "vault:contoso-orders-key";
            var home = Reviewer("dirty-home");
            home.ExecutableRef = "/home/someone/bin/codex";
            db.GateReviewers.AddRange(path, url, name, home);
            var run = db.GateRuns.First();
            db.GateArtifacts.Add(new GateArtifactRow { RunId = run.Id, CellId = Guid.Empty, Attempt = 1, Class = ArtifactClass.Other, RelativePath = "D:/work/x.json", Sha256 = new string('1', 64) });
            await db.SaveChangesAsync(Ct);
        }

        var violations = (await ViolationsAsync(connection)).Select(v => v.Describe).ToList();

        violations.Should().BeEquivalentTo(
        [
            "gate_reviewers.Model row dirty-path: " + PublicationGuard.DriveRule,
            "gate_reviewers.Model row dirty-path: " + PublicationGuard.UsersRule,
            "gate_reviewers.RemoteVendor row dirty-url: " + PublicationGuard.UrlRule,
            "gate_reviewers.KeyName row dirty-name: carries private name #1 of the suite",
            "gate_reviewers.ExecutableRef row dirty-home: " + PublicationGuard.HomeRule,
            "gate_artifacts.RelativePath row " + ArtifactRowId(connection) + ": " + PublicationGuard.DriveRule,
        ]);
        violations.Should().NotContain(v => v.Contains("contoso", StringComparison.OrdinalIgnoreCase),
            "the refusal names the rule, never the private text — CI logs are public");
    }

    [Fact]
    public async Task A_failure_cause_is_guarded_like_every_other_column()
    {
        var connection = await CampaignAsync();
        var store = new PostgresGateStore(PostgresFixture.Context(connection), new TestClock(Noon));
        var (run, cells) = Planned(count: 1);
        await store.PlanAsync(run, cells, Ct);
        var owner = Here();
        var claimed = (await store.ClaimNextAsync(run.Id, owner, Pin(), Ct)).Ok();
        await store.SettleAsync(cells[0].Id, owner, new GateSettlement.Failed(new FailureCause(FailureKind.ProcessDied,
            "coai died reading D:\\work\\Fabrikam\\plan.md")), Ct);

        var violations = (await ViolationsAsync(connection)).Where(v => v.RowId == cells[0].Id.ToString()).Select(v => v.Rule).ToList();

        violations.Should().BeEquivalentTo([PublicationGuard.DriveRule, "carries private name #2 of the suite"],
            "the failure cause is the one free-text column, and the guard is its second line after the redaction");
        claimed.Should().NotBeNull();
    }

    [Fact]
    public async Task A_redacted_failure_cause_passes_the_guard()
    {
        var connection = await CampaignAsync();
        var store = new PostgresGateStore(PostgresFixture.Context(connection), new TestClock(Noon));
        var (run, cells) = Planned(count: 1);
        await store.PlanAsync(run, cells, Ct);
        var owner = Here();
        await store.ClaimNextAsync(run.Id, owner, Pin(), Ct);
        var cause = FailureRedaction.Redact(new FailureCause(FailureKind.HttpError, "call 2: HTTP 502 from https://gw.contoso-orders.example/v1 at D:\\work\\Fabrikam\\x"), SampleNames());
        await store.SettleAsync(cells[0].Id, owner, new GateSettlement.Failed(cause), Ct);

        (await ViolationsAsync(connection)).Should().BeEmpty();
        cause.Text.Should().Be("call 2: HTTP 502 from <url> at <path>", "the call number and the status survive; the url and the path do not");
    }

    [Fact]
    public async Task A_loopback_endpoint_is_refused_and_a_public_vendor_url_is_not()
    {
        var connection = await CampaignAsync();
        await using (var db = PostgresFixture.Context(connection))
        {
            var local = Reviewer("local-endpoint");
            local.EndpointUrl = "http://127.0.0.1:11434/v1";
            db.GateReviewers.Add(local);
            await db.SaveChangesAsync(Ct);
        }

        (await ViolationsAsync(connection)).Select(v => v.Describe).Should().Equal(
            "gate_reviewers.EndpointUrl row local-endpoint: " + PublicationGuard.EndpointRule);
    }

    [Fact]
    public async Task The_public_export_is_refused_whole_by_one_dirty_row()
    {
        var connection = await CampaignAsync();
        await using (var db = PostgresFixture.Context(connection))
        {
            var dirty = Reviewer("northwind-billing-reviewer");
            db.GateReviewers.Add(dirty);
            await db.SaveChangesAsync(Ct);
        }

        var exported = await ExportAsync(connection);

        exported.Reason().Should().Contain("refused").And.Contain("gate_reviewers.Id row <private>-reviewer",
            "a row id that itself carries a private name is redacted in the refusal that names it");
        exported.Reason().Should().NotContain("northwind", "the refusal never repeats the private text");
    }

    [Fact]
    public async Task The_public_export_is_built_from_rows_only_and_a_private_name_inside_an_artefact_cannot_reach_it()
    {
        var connection = await CampaignAsync();
        using var temp = NewRoot();
        var artifacts = Store(temp);
        var store = new PostgresGateStore(PostgresFixture.Context(connection), new TestClock(Noon));
        var (run, cells) = Planned(count: 1);
        await store.PlanAsync(run, cells, Ct);
        var owner = Here();
        var scope = ArtifactScope.Of(run, (await store.ClaimNextAsync(run.Id, owner, Pin(), Ct)).Ok()).Ok();
        await artifacts.BeginAttemptAsync(scope, Ct);
        var body = Encoding.UTF8.GetBytes("{\"title\":\"contoso-orders leaks its Fabrikam key at C:\\\\Users\\\\x\\\\secrets\"}");
        await new GateCellCompletion(artifacts, store).CompleteAsync(scope, owner,
            [new PendingArtifact(ArtifactClass.Findings, CellPaths.AttemptRoot(scope).Then("findings.jsonl").Ok(), body)],
            new PendingArtifact(ArtifactClass.RunRecord, CellPaths.AttemptRoot(scope).Then(CellPaths.RunRecordFile).Ok(), "{}"u8.ToArray()),
            Completed(), Ct);

        var document = (await ExportAsync(connection)).Ok();

        document.Should().Contain("findings.jsonl", "the artefact's REF is a row — path, hash, length");
        document.Should().Contain(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(body)));
        foreach (var leak in new[] { "contoso", "Fabrikam", "\\Users\\", "leaks its", "quotes private code" })
        {
            document.Should().NotContain(leak, "the export never opens an artefact; nothing inside one can reach it");
        }
    }

    [Fact]
    public async Task The_file_hash_key_never_reaches_a_row()
    {
        var connection = await CampaignAsync();
        await using var db = PostgresFixture.Context(connection);

        var words = GatePublication.Texts(await new PostgresGatePublicationSource(db).ReadAsync(Ct)).Select(t => t.Text).ToList();

        words.Should().NotBeEmpty();
        words.Should().NotContain(w => w.Contains(Convert.ToHexStringLower(KeyBytes), StringComparison.OrdinalIgnoreCase),
            "the key lives in the artefact root and nowhere else");
    }

    private static string ArtifactRowId(string connection)
    {
        using var db = PostgresFixture.Context(connection);
        return db.GateArtifacts.Single(a => a.RelativePath == "D:/work/x.json").Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static readonly byte[] KeyBytes = [.. Enumerable.Range(0, 32).Select(i => (byte)(i * 11 + 5))];

    /// <summary>A fresh database holding one whole, clean campaign: a run, a settled cell with facts and findings, a
    /// failed cell, an artefact ref, a reviewer row with a public vendor url, a verdict.</summary>
    private async Task<string> CampaignAsync()
    {
        var connection = await postgres.NewDatabaseAsync($"gate_pub_{Guid.NewGuid():N}");
        var store = new PostgresGateStore(PostgresFixture.Context(connection), new TestClock(Noon));
        var (run, cells) = Planned(count: 2);
        await store.PlanAsync(run, cells, Ct);
        var owner = Here();

        var first = ArtifactScope.Of(run, (await store.ClaimNextAsync(run.Id, owner, Pin(), Ct)).Ok()).Ok();
        await store.RecordArtifactsAsync([ArtifactRef.Of(first, ArtifactClass.Reply, CellPaths.AttemptRoot(first).Then("reply.json").Ok(), new string('c', 64), 42).Ok()], Ct);
        await store.SettleAsync(first.CellId, owner, Completed(), Ct);
        var second = (await store.ClaimNextAsync(run.Id, owner, Pin(), Ct)).Ok();
        await store.SettleAsync(second.Id, owner, new GateSettlement.Failed(new FailureCause(FailureKind.ProcessDied, "the coai process died on turn 2")), Ct);

        await using var db = PostgresFixture.Context(connection);
        db.GateReviewers.Add(Reviewer("grok-medium"));
        db.GateVerdicts.Add(new GateVerdictRow
        {
            CellId = first.CellId,
            FindingOrdinal = 0,
            RubricId = "strict-v1",
            RubricKind = RubricKind.Strict,
            RubricHash = new string('7', 64),
            Kind = "Strict",
            Reading = StrictReading.Supported,
            Value = ValueLevel.High,
            ClusterHash = new string('8', 64),
            SeedHit = "cs2-S1",
            AssessorId = "codex-astra",
            BatchId = "b-0001",
            PromptHash = new string('9', 64),
            RecordedAt = Noon,
        });
        await db.SaveChangesAsync(Ct);

        return connection;
    }

    private static GateReviewerRow Reviewer(string id) => new()
    {
        Id = id,
        Hash = new string('4', 64),
        Runtime = ReviewerRuntime.Api,
        Model = "grok-4.7",
        EndpointUrl = "https://api.x.ai/v1",
        KeyName = "xai",
        CredsKeyRef = "COAI_CREDS_KEY_REF",
        Dialect = "xai",
        ReasoningEffort = "medium",
        MaxTokens = 32000,
        TimeoutMinutes = 12,
        FollowUps = 2,
        ReviewMinutesCap = 20,
        PricesKnown = true,
        InPerMTok = 3m,
        OutPerMTok = 15m,
        GateFeature = true,
        AddedAt = Noon,
    };

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

    /// <summary>The sample's names, always — and the operator's own suite's too when <c>BENCH_GATE_SUITE</c> names one.</summary>
    private static PrivateNames Names()
    {
        var operatorSuite = Environment.GetEnvironmentVariable("BENCH_GATE_SUITE") ?? string.Empty;

        return operatorSuite.Length > 0 && File.Exists(operatorSuite)
            ? SampleNames().With(GatePrivateNames.Read(File.ReadAllText(operatorSuite)).Ok())
            : SampleNames();
    }

    public static PrivateNames SampleNames() =>
        GatePrivateNames.Read(File.ReadAllText(Path.Combine(RepositoryRoot(), "samples", "gate-suite.sample.json"))).Ok();

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (directory.GetFiles("*.slnx").Length > 0)
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("no *.slnx above the test binary — the guard test reads the checked-in sample suite and cannot guess where it is");
    }
}
