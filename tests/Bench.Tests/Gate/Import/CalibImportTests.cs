using System.Text.Json.Nodes;
using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Gate;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bench.Tests.Gate.Import;

/// <summary>S5.1 and S5.2 end to end over a real database and a real artefact root: the redacted calibration — the whole
/// phase-2 population — imported, re-imported, resumed after a kill, and read back through the report.</summary>
[Collection("postgres")]
public sealed class CalibImportTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_whole_population_is_imported_as_settled_cells_of_one_finished_campaign()
    {
        using var rig = await ImportRig.NewAsync(postgres);

        var report = (await rig.ImportAsync(Ct)).Ok();
        var records = await rig.RecordsAsync(Ct);

        report.Cells.Imported.Should().Be(92);
        report.Campaigns.Should().ContainSingle("the fixture is phase 2 only — one campaign per phase");
        report.ReviewersAdded.Should().HaveCount(4, "one row per (model, preset): the four calibrated models");
        records.Should().HaveCount(92, "a second attempt is a second cell");
        records.Select(r => r.Scope).Distinct().Should().ContainSingle("ten product commits, one population — the other harness's comparison unit");
        records.Should().OnlyContain(r => r.Source.Label == "calib-py");
        records.Where(r => r.Attempt == 2).Should().HaveCount(8);
        records.Sum(r => r.Findings.Count).Should().Be(340);

        var line = ImportFixture.Line("p2-grok-4.7-js3-r1");
        var run = records.Single(r => r.RunId == CalibImport.CellId(ImportFixture.Suite, CalibRecords.Parse(line.ToJsonString(), 1, ImportFixture.Names).Ok()));
        run.Facts.Should().BeEquivalentTo(CalibRecords.Parse(line.ToJsonString(), 1, ImportFixture.Names).Ok().Facts, "every fact round-trips through the store");
    }

    [Fact]
    public async Task A_second_import_changes_nothing()
    {
        using var rig = await ImportRig.NewAsync(postgres);
        (await rig.ImportAsync(Ct)).Ok();
        var before = await rig.CountsAsync(Ct);
        var logBefore = await File.ReadAllTextAsync(Path.Combine(rig.Root, "assess", "verdicts", "codex-astra.jsonl"), Ct);

        var again = (await rig.ImportAsync(Ct)).Ok();

        again.Cells.Unchanged.Should().Be(92);
        again.Cells.Imported.Should().Be(0);
        again.ReviewersAdded.Should().BeEmpty("rows are matched by definition hash");
        again.Verdicts.New.Should().Be(0);
        (await rig.CountsAsync(Ct)).Should().Be(before);
        (await File.ReadAllTextAsync(Path.Combine(rig.Root, "assess", "verdicts", "codex-astra.jsonl"), Ct)).Should().Be(logBefore, "no log line is appended twice");
    }

    [Fact]
    public async Task A_record_that_changed_since_it_was_imported_is_refused_never_overwritten()
    {
        using var rig = await ImportRig.NewAsync(postgres);
        (await rig.ImportAsync(Ct)).Ok();

        var lines = File.ReadAllLines(Path.Combine(rig.Source, "runs.jsonl"));
        var edited = (JsonObject)JsonNode.Parse(lines[0])!;
        edited["seconds_total"] = 1.0;
        lines[0] = edited.ToJsonString();
        File.WriteAllLines(Path.Combine(rig.Source, "runs.jsonl"), lines);

        (await rig.ImportAsync(Ct)).Reason().Should().Contain((string)edited["id"]!).And.Contain("reads differently");
    }

    [Fact]
    public async Task An_import_killed_between_the_files_and_the_row_resumes_on_the_next_run()
    {
        using var rig = await ImportRig.NewAsync(postgres);

        var killed = async () => await rig.ImportAsync(Ct, new DiesAfter(new PostgresGateImportStore(rig.Db(), TimeProvider.System), cells: 5));
        await killed.Should().ThrowAsync<SimulatedCrash>();

        var resumed = (await rig.ImportAsync(Ct)).Ok();

        resumed.Cells.Unchanged.Should().Be(5);
        resumed.Cells.Imported.Should().Be(87, "the sixth cell's files were on disk with no row — adopted, not refused");
        (await rig.RecordsAsync(Ct)).Should().HaveCount(92);
    }

    [Fact]
    public async Task A_file_left_by_a_killed_import_with_other_bytes_is_refused_naming_it()
    {
        using var rig = await ImportRig.NewAsync(postgres);
        var killed = async () => await rig.ImportAsync(Ct, new DiesAfter(new PostgresGateImportStore(rig.Db(), TimeProvider.System), cells: 0));
        await killed.Should().ThrowAsync<SimulatedCrash>();

        var findings = Directory.EnumerateFiles(Path.Combine(rig.Root, "runs"), "findings.jsonl", SearchOption.AllDirectories).Single();
        File.AppendAllText(findings, "{\"planted\": true}\n");

        (await rig.ImportAsync(Ct)).Reason().Should().Contain("findings.jsonl").And.Contain("OTHER bytes");
    }

    [Fact]
    public async Task The_import_never_writes_to_its_source()
    {
        using var rig = await ImportRig.NewAsync(postgres);
        var before = ImportFixture.Snapshot(rig.Source);

        (await rig.ImportAsync(Ct)).Ok();

        ImportFixture.Snapshot(rig.Source).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task A_torn_source_line_refuses_the_whole_import_before_anything_is_written()
    {
        using var rig = await ImportRig.NewAsync(postgres);
        File.AppendAllText(Path.Combine(rig.Source, "runs.jsonl"), "{\"id\": \"p2-torn");

        (await rig.ImportAsync(Ct)).Reason().Should().Contain("line 93");
        (await rig.CountsAsync(Ct)).Cells.Should().Be(0, "the pre-flight reads every record before the first write");
        Directory.Exists(Path.Combine(rig.Root, "runs")).Should().BeFalse();
    }

    [Fact]
    public async Task Verdicts_need_an_assessor_row_when_the_workspace_has_an_assessment()
    {
        using var rig = await ImportRig.NewAsync(postgres);

        (await rig.ImportAsync(Ct, assessor: false)).Reason().Should().Contain("blinded assessment").And.Contain("--assessor");
        (await rig.CountsAsync(Ct)).Cells.Should().Be(0);
    }

    [Fact]
    public async Task An_assessor_row_the_verdict_lines_do_not_name_is_refused_before_a_single_cell_is_written()
    {
        using var rig = await ImportRig.NewAsync(postgres);
        var claude = GateReviewer.Create(
            GateReviewerId.Parse("claude-judge").Ok(),
            GateReviewerTests.Definition(model: "claude-opus-5", runtime: ReviewerRuntime.Claude, endpoint: string.Empty, credsKeyRef: string.Empty, keyName: string.Empty).Ok(),
            GateStoreFixtures.Noon);

        var refused = await rig.Calib().RunAsync(rig.Request() with { Assessor = Outcome<GateReviewer>.Success(claude) }, _ => { }, Ct);

        refused.Reason().Should().Contain("'codex'").And.Contain("claude-judge");
        (await rig.CountsAsync(Ct)).Cells.Should().Be(0, "a refusal the files alone decide is a pre-flight refusal — before the first byte");
    }

    [Fact]
    public async Task The_verdicts_land_under_strict_v1_through_the_ingestion_contract_and_their_text_in_the_root()
    {
        using var rig = await ImportRig.NewAsync(postgres);

        var report = (await rig.ImportAsync(Ct)).Ok();
        var verdicts = await rig.VerdictsAsync(await rig.RecordsAsync(Ct), Ct);

        report.Verdicts.New.Should().Be(340);
        report.Verdicts.FamilyMatched.Should().Be(0, "an OpenAI assessor judged xAI, Alibaba, DeepSeek and Zhipu models");
        verdicts.Should().HaveCount(340).And.OnlyContain(v => v.Rubric.Id.Value == "strict-v1" && v.PromptHash.Length == 0);
        var key = await new FileSystemGateAssessmentFiles(rig.Root).ReadKeyAsync(Ct);
        key.Ok().Should().HaveCount(340, "the imported ids are this root's key now — a person can hand-check them");
        (await File.ReadAllLinesAsync(Path.Combine(rig.Root, "assess", "verdicts", "codex-astra.jsonl"), Ct)).Should().HaveCount(340);
    }

    [Fact]
    public async Task The_imported_rows_pass_the_publication_guard()
    {
        using var rig = await ImportRig.NewAsync(postgres);
        (await rig.ImportAsync(Ct)).Ok();

        var source = new PostgresGatePublicationSource(rig.Db());
        var violations = GatePublication.Check(await source.ReadAsync(Ct), ImportFixture.Names.WithHosts([Environment.MachineName]), source.PublicUrlColumns);

        violations.Should().BeEmpty();
    }

    /// <summary>The DoD: the imported feature runs show the per-model numbers the other harness's results.json published the
    /// same day. Four numbers per model are pinned exactly (valid %, seconds p50, cost per run, tokens in per run), plus the
    /// population columns (runs, attempts, failed attempts) that decide which run of a cell is read.</summary>
    [Fact]
    public async Task The_imported_population_shows_the_published_per_model_numbers()
    {
        using var rig = await ImportRig.NewAsync(postgres);
        (await rig.ImportAsync(Ct)).Ok();
        var records = await rig.RecordsAsync(Ct);
        var input = new GateReportInput([.. ImportFixture.Suite.Tasks.Select(t => t.Summary)], records, await rig.VerdictsAsync(records, Ct));
        await using var db = rig.Db();
        var reviewers = await db.GateReviewers.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Model, Ct);

        var table = GateReport.PerModel(records[0].Scope, rig.Strict.Rubric, input);

        table.AllTasks.Should().HaveCount(4);
        foreach (var row in table.AllTasks)
        {
            var expected = ImportFixture.ExpectedFor(reviewers[row.Reviewer.Value]);
            (row.Runs, row.Attempts, row.AttemptsFailed).Should().Be(((int)expected["runs"]!, (int)expected["attempts"]!, (int)expected["attempts_failed"]!), row.Reviewer.Value);
            row.ValidPct.Value.Should().Be((double)expected["valid_pct"]!, row.Reviewer.Value);
            row.SecondsP50.Value.Should().Be((double)expected["sec_p50"]!, row.Reviewer.Value);
            row.CostPerRun.Value.Should().Be((double)expected["cost_per_run"]!, row.Reviewer.Value);
            row.TokensInPerRun.Value.Should().Be((double)expected["tokens_in_per_run"]!, row.Reviewer.Value);
        }
    }
}
