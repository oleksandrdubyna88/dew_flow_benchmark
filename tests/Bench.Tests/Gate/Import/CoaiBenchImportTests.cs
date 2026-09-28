using System.Text.Json.Nodes;
using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Targets;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Bench.Tests.Infrastructure.GateStoreFixtures;

namespace Bench.Tests.Gate.Import;

/// <summary>S5.3 and S5.4 end to end: coai-bench records become plan and code cells with lenient verdicts, a copied file adds
/// nothing, a grown file adds to its campaign; a results table becomes summary-only numbers, once.</summary>
[Collection("postgres")]
public sealed class CoaiBenchImportTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class PaddedShas : ICommitResolver
    {
        public Task<Outcome<CommitSha>> ResolveAsync(string shortSha, CancellationToken cancellationToken) =>
            Task.FromResult(CommitSha.Parse(shortSha.PadRight(40, '0')));
    }

    private static JsonObject Record(int repeat, string useful, string startedUtc, string caseName = "made-up-case") => (JsonObject)JsonNode.Parse($$"""
        {"case": {"name": "{{caseName}}", "planFile": "docs/plan.md", "commit": "1234567", "baseRef": "abcdef0"},
         "arm": "codex,local", "repeat": {{repeat}}, "lane": 1, "startedUtc": "{{startedUtc}}", "judgedBy": "claude-opus-5",
         "stages": [
          {"stage": "plan-1", "seconds": 50.8, "verdict": "proceed", "error": "", "tokensIn": 100, "tokensOut": 20, "costUsd": null,
           "findings": [
             {"severity": "Major", "category": "Security", "title": "t0", "why": "w", "fix": "f", "isGating": true, "useful": "{{useful}}", "verdict": "why"},
             {"severity": "Minor", "category": "Clarity", "title": "t1", "why": "w", "fix": "f", "isGating": false, "useful": "unjudged", "verdict": ""}]},
          {"stage": "code", "seconds": 90.0, "verdict": "proceed", "error": "", "tokensIn": 1, "tokensOut": 2, "costUsd": 0.5,
           "findings": [{"severity": "Minor", "category": "Reliability", "title": "c0", "why": "w", "fix": "f", "useful": "no", "verdict": "meh"}]}]}
        """)!;

    private static async Task<(string Connection, TempRoot Root)> NewAsync(PostgresFixture postgres) =>
        (await ImportRig.DatabaseAsync(postgres, "cb"), NewRoot());

    private static async Task<CoaiBenchImportReport> ImportAsync(string connection, TempRoot root, params CoaiBenchFile[] files)
    {
        var rubrics = GateRubrics.Load(Path.Combine(Bench.Tests.Cli.Repository.Root, "prompts")).Ok();
        var import = new CoaiBenchImport(
            new PostgresGateImportStore(PostgresFixture.Context(connection), TimeProvider.System), Store(root),
            new PostgresGateVerdictStore(PostgresFixture.Context(connection), TimeProvider.System), new PaddedShas());

        return (await import.RunAsync(new CoaiBenchImportRequest(files, Key, GateRubrics.Labelling(rubrics, GateRubrics.LenientId).Ok(), GateRubrics.Catalog(rubrics), PrivateNames.None), _ => { }, Ct)).Ok();
    }

    [Fact]
    public async Task Stages_become_plan_and_code_cells_and_only_judged_findings_become_lenient_verdicts()
    {
        var (connection, root) = await NewAsync(postgres);
        using var _ = root;

        var report = await ImportAsync(connection, root, new CoaiBenchFile("bench-2026-09-06", new JsonArray(Record(1, "yes", "2026-09-05T20:39:22Z")).ToJsonString()));

        report.Cells.Imported.Should().Be(2);
        report.Verdicts.Should().Be(2);
        report.Unjudged.Should().Be(1, "unjudged stays unassessed — no row at all");
        await using var db = PostgresFixture.Context(connection);
        (await db.GateRuns.Select(r => r.Gate).ToListAsync(Ct)).Should().BeEquivalentTo([GateKind.Plan, GateKind.Code]);
        var verdicts = await db.GateVerdicts.AsNoTracking().ToListAsync(Ct);
        verdicts.Should().HaveCount(2).And.OnlyContain(v => v.RubricId == "lenient-worth-v1" && v.AssessorId == "claude-opus-5" && !v.AssessorFamilyMatches);
        verdicts.Select(v => v.WorthHaving).Should().BeEquivalentTo([true, false]);
        (await db.GateCells.AsNoTracking().Select(c => c.TurnFactsCaptured).Distinct().ToListAsync(Ct)).Should().Equal(false);
        (await db.GateReviewers.CountAsync(Ct)).Should().Be(0, "an arm is a vendor set with no model recorded — no catalog row is invented");
        Directory.EnumerateFiles(root.Path, "*.suite.json", SearchOption.AllDirectories).Should().BeEmpty("the suite file is the CLI's to write");
    }

    [Fact]
    public async Task A_copied_file_adds_nothing_and_a_grown_file_adds_its_new_records_to_the_same_campaign()
    {
        var (connection, root) = await NewAsync(postgres);
        using var _ = root;
        var one = new JsonArray(Record(1, "yes", "2026-09-05T20:39:22Z")).ToJsonString();
        var two = new JsonArray(Record(1, "yes", "2026-09-05T20:39:22Z"), Record(2, "no", "2026-09-05T21:00:00Z")).ToJsonString();

        await ImportAsync(connection, root, new CoaiBenchFile("bench-2026-09-06", one));
        var copy = await ImportAsync(connection, root, new CoaiBenchFile("matrix-v0.18.0", one));
        var grown = await ImportAsync(connection, root, new CoaiBenchFile("bench-2026-09-06", two));

        copy.Cells.Imported.Should().Be(0, "a cell is keyed by the RECORD, never by the file");
        copy.VerdictsNew.Should().Be(0);
        grown.Cells.Imported.Should().Be(2);
        grown.Cells.Unchanged.Should().Be(2);
        await using var db = PostgresFixture.Context(connection);
        (await db.GateRuns.CountAsync(Ct)).Should().Be(2, "one campaign per (location, gate); the copy created none");
    }

    [Fact]
    public async Task A_file_that_also_carries_another_case_never_imports_the_records_already_there_again()
    {
        var (connection, root) = await NewAsync(postgres);
        using var _ = root;
        var first = Record(1, "yes", "2026-09-05T20:39:22Z");

        await ImportAsync(connection, root, new CoaiBenchFile("epic1", new JsonArray(first).ToJsonString()));
        var wider = await ImportAsync(connection, root, new CoaiBenchFile("matrix", new JsonArray((JsonObject)first.DeepClone(), Record(1, "no", "2026-09-06T10:00:00Z", "another-case")).ToJsonString()));

        wider.Cells.Unchanged.Should().Be(2, "a record's cell is the record's, whichever other cases the file it came in carries");
        wider.Cells.Imported.Should().Be(2);
        await using var db = PostgresFixture.Context(connection);
        (await db.GateCells.CountAsync(Ct)).Should().Be(4);
    }

    private static async Task<Outcome<CoaiBenchImportReport>> TryImportAsync(string connection, TempRoot root, params CoaiBenchFile[] files)
    {
        var rubrics = GateRubrics.Load(Path.Combine(Bench.Tests.Cli.Repository.Root, "prompts")).Ok();
        var import = new CoaiBenchImport(
            new PostgresGateImportStore(PostgresFixture.Context(connection), TimeProvider.System), Store(root),
            new PostgresGateVerdictStore(PostgresFixture.Context(connection), TimeProvider.System), new PaddedShas());

        return await import.RunAsync(new CoaiBenchImportRequest(files, Key, GateRubrics.Labelling(rubrics, GateRubrics.LenientId).Ok(), GateRubrics.Catalog(rubrics), PrivateNames.None), _ => { }, Ct);
    }

    [Fact]
    public async Task Two_records_on_one_cell_of_a_campaign_are_refused_rather_than_one_hiding_the_other()
    {
        var (connection, root) = await NewAsync(postgres);
        using var _ = root;

        var twice = await TryImportAsync(connection, root, new CoaiBenchFile("f", new JsonArray(Record(1, "yes", "2026-09-05T20:39:22Z"), Record(1, "no", "2026-09-05T23:00:00Z")).ToJsonString()));

        twice.Reason().Should().Contain("one cell").And.Contain("2026-09-05T23:00:00Z");
    }

    [Fact]
    public async Task A_finding_re_judged_after_the_first_import_is_refused_never_silently_kept_as_it_was()
    {
        var (connection, root) = await NewAsync(postgres);
        using var _ = root;

        await ImportAsync(connection, root, new CoaiBenchFile("f", new JsonArray(Record(1, "yes", "2026-09-05T20:39:22Z")).ToJsonString()));
        var rejudged = await TryImportAsync(connection, root, new CoaiBenchFile("f", new JsonArray(Record(1, "no", "2026-09-05T20:39:22Z")).ToJsonString()));

        rejudged.Reason().Should().Contain("judged").And.Contain("differently");
    }

    [Fact]
    public async Task A_summary_table_imported_again_under_another_gate_is_refused_not_kept_under_the_first()
    {
        var connection = await ImportRig.DatabaseAsync(postgres, "sg");
        var table = SummaryTables.Parse("## T\n| model | run 1 |\n|---|---|\n| Model-A | 6 |\n", "T").Ok();
        var citation = new SummaryCitation("coai-results", "RESULTS_made_up.md", new string('d', 64));

        (await new PostgresGateImportStore(PostgresFixture.Context(connection), TimeProvider.System).RecordSummaryAsync(citation, GateKind.Plan, table, Ct)).Ok();
        var again = await new PostgresGateImportStore(PostgresFixture.Context(connection), TimeProvider.System).RecordSummaryAsync(citation, GateKind.Code, table, Ct);

        again.Reason().Should().Contain("Plan").And.Contain("Code");
    }

    [Fact]
    public async Task A_judge_pass_after_the_first_import_adds_verdicts_instead_of_reading_as_a_changed_run()
    {
        var (connection, root) = await NewAsync(postgres);
        using var _ = root;

        await ImportAsync(connection, root, new CoaiBenchFile("f", new JsonArray(Record(1, "unjudged", "2026-09-05T20:39:22Z")).ToJsonString()));
        var judged = await ImportAsync(connection, root, new CoaiBenchFile("f", new JsonArray(Record(1, "yes", "2026-09-05T20:39:22Z")).ToJsonString()));

        judged.Cells.Unchanged.Should().Be(2);
        judged.VerdictsNew.Should().Be(1, "the plan finding judged since");
    }

    [Fact]
    public async Task A_results_table_is_stored_once_as_summary_only_numbers()
    {
        var connection = await ImportRig.DatabaseAsync(postgres, "sm");
        var table = SummaryTables.Parse("## T\n| model | run 1 | ~$ |\n|---|---|---|\n| Model-A | 6 | — |\n| Model-B | 4 | $0.1 |\n", "T").Ok();
        var citation = new SummaryCitation("coai-results", "RESULTS_made_up.md", new string('c', 64));

        var first = await new PostgresGateImportStore(PostgresFixture.Context(connection), TimeProvider.System).RecordSummaryAsync(citation, GateKind.Plan, table, Ct);
        var again = await new PostgresGateImportStore(PostgresFixture.Context(connection), TimeProvider.System).RecordSummaryAsync(citation, GateKind.Plan, table, Ct);

        first.Ok().Should().Be(4);
        again.Ok().Should().Be(0, "a re-import of one document's table is a no-op");
        await using var db = PostgresFixture.Context(connection);
        var rows = await db.GateSummaries.AsNoTracking().OrderBy(s => s.RowOrdinal).ThenBy(s => s.Metric).ToListAsync(Ct);
        rows.Should().HaveCount(4);
        rows.Single(r => r.Label == "model-a" && r.Metric == "run-1").Value.Should().Be(6);
        rows.Single(r => r.Label == "model-a" && r.Metric != "run-1").Captured.Should().BeFalse();
    }
}
