using System.Text;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Runs;

namespace Bench.Application.Gate;

/// <summary>What <c>bench gate import calib</c> is asked to do.</summary>
/// <param name="Assessor">The catalog row the verdicts are attributed to — or the refusal to give when the workspace HAS
/// verdicts and none was named (plan round, finding 13).</param>
public sealed record CalibImportRequest(
    IImportSource Source,
    GateSuite Suite,
    IReadOnlyDictionary<string, CalibModel> Models,
    Outcome<GateReviewer> Assessor,
    FileHashKey Key,
    LoadedRubric Strict,
    RubricCatalog Rubrics,
    PrivateNames PrivateNames);

public sealed record ImportCounts(int Records, int Imported, int Unchanged, int FilesSkipped)
{
    public static ImportCounts None { get; } = new(0, 0, 0, 0);

    public ImportCounts Add(ImportOutcome outcome, int skipped) =>
        outcome == ImportOutcome.Imported ? this with { Records = Records + 1, Imported = Imported + 1, FilesSkipped = FilesSkipped + skipped }
            : this with { Records = Records + 1, Unchanged = Unchanged + 1 };
}

public sealed record CalibImportReport(ImportCounts Cells, IReadOnlyList<Guid> Campaigns, IReadOnlyList<string> ReviewersAdded, VerdictImportCounts Verdicts);

/// <summary><c>bench gate import calib</c> — the calibration workspace (the other harness's <c>runs.jsonl</c>, its run
/// directories, its blinded assessment) imported into the gate's tables and the artefact root, READ-ONLY over the source,
/// idempotent by derived ids. One campaign per phase; a reviewer row per (model, preset), matched by hash; a cell per
/// record (a second attempt is a second cell); the run directory copied file by file under <c>source/</c>; the findings
/// through the one reply parser; then the verdicts through the E4 ingestion contract.</summary>
public sealed class CalibImport(
    IGateImportStore store,
    IGateArtifactStore artifacts,
    IGateReviewerCatalog catalog,
    CalibVerdictImport verdicts,
    TimeProvider clock)
{
    public async Task<Outcome<CalibImportReport>> RunAsync(CalibImportRequest request, Action<string> progress, CancellationToken cancellationToken)
    {
        var read = await CalibPreflight.ReadAsync(request.Source, request.Suite, request.Models, request.PrivateNames, cancellationToken);
        if (read is not Outcome<CalibSource>.Ok { Value: var source })
        {
            return Outcome<CalibImportReport>.Failure(((Outcome<CalibSource>.Fail)read).Reason);
        }

        if (source.HasVerdicts && request.Assessor is Outcome<GateReviewer>.Fail noAssessor)
        {
            return Outcome<CalibImportReport>.Failure($"the workspace has a blinded assessment and {noAssessor.Reason}");
        }

        progress($"read           {source.Cells.Count} record(s), {source.Key.Count} key entr(ies), {source.Verdicts.Count} verdict(s) — every one checked before anything is written");

        var reviewers = await ReviewersAsync(source.Cells, cancellationToken);
        return reviewers is Outcome<(IReadOnlyDictionary<string, GateReviewer>, IReadOnlyList<string>)>.Ok { Value: var (byHash, added) }
            ? await CellsAsync(request, source, byHash, added, progress, cancellationToken)
            : Outcome<CalibImportReport>.Failure(((Outcome<(IReadOnlyDictionary<string, GateReviewer>, IReadOnlyList<string>)>.Fail)reviewers).Reason);
    }

    private async Task<Outcome<CalibImportReport>> CellsAsync(
        CalibImportRequest request, CalibSource source, IReadOnlyDictionary<string, GateReviewer> byHash, IReadOnlyList<string> added,
        Action<string> progress, CancellationToken cancellationToken)
    {
        var writer = new GateImportWriter(artifacts, store);
        var campaigns = Campaigns(request.Suite, source.Cells);
        var counts = ImportCounts.None;

        foreach (var (cell, position) in source.Cells.Select((c, i) => (c, i)))
        {
            var planned = await PlanAsync(request, campaigns[cell.Record.Phase], cell, byHash[cell.Definition.Hash], position, cancellationToken);
            var written = await writer.WriteAsync(planned.Plan, cancellationToken);
            if (written is Outcome<ImportOutcome>.Fail fail)
            {
                return Outcome<CalibImportReport>.Failure(fail.Reason);
            }

            counts = counts.Add(((Outcome<ImportOutcome>.Ok)written).Value, planned.Skipped);
            progress($"{Word(((Outcome<ImportOutcome>.Ok)written).Value),-15}{cell.Record.Id}");
        }

        var cellIds = source.Cells.ToDictionary(c => c.Record.Id, c => CellId(request.Suite, c.Record), StringComparer.Ordinal);
        var assessed = source.HasVerdicts
            ? await verdicts.RunAsync(request, source, cellIds, byHash, campaigns, progress, cancellationToken)
            : Outcome<VerdictImportCounts>.Success(VerdictImportCounts.None);

        return assessed.Match(
            v => Outcome<CalibImportReport>.Success(new CalibImportReport(counts, [.. campaigns.Values.Select(c => c.Id)], added, v)),
            Outcome<CalibImportReport>.Failure);
    }

    public static Guid CellId(GateSuite suite, CalibRecord record) => ImportIds.Of(CalibRecords.Harness, $"{suite.Stamp}|cell|{record.Id}");

    /// <summary>A campaign per phase, keyed by the suite and the phase, created at the earliest start its records name — so
    /// the same workspace imports into the same campaign, whenever it is imported.</summary>
    private static IReadOnlyDictionary<int, GateRun> Campaigns(GateSuite suite, IReadOnlyList<CalibCell> cells) =>
        cells.GroupBy(c => c.Record.Phase).ToDictionary(
            g => g.Key,
            g => new GateRun(
                ImportIds.Of(CalibRecords.Harness, $"{suite.Stamp}|campaign|phase-{g.Key}"),
                GateKind.Feature,
                suite.Stamp,
                DataDirMode.Isolated,
                GateRunStatus.Finished,
                new RunSource.Imported(CalibRecords.Harness),
                g.Min(c => c.Record.Started)));

    private async Task<(ImportPlan Plan, int Skipped)> PlanAsync(
        CalibImportRequest request, GateRun campaign, CalibCell cell, GateReviewer reviewer, int position, CancellationToken cancellationToken)
    {
        var record = cell.Record;
        var copied = await ImportedFiles.CopyAsync(request.Source, $"{CalibPreflight.RunsFolder}/{record.Id}", cancellationToken);
        var findings = cell.Reply.Findings
            .Select(f => GateFinding.Of(f.Ordinal, f.Severity, f.Category, f.IsGating, f.Line, f.Text, f.File, request.Key))
            .OfType<Outcome<GateFinding>.Ok>().Select(o => o.Value).ToList();

        var settlement = new GateSettlement.Completed(record.Facts, findings, ImportedSettings.Hash(CalibRecords.Harness), ImportedFiles.TurnOnePromptHash(copied.Files));
        var gateCell = new GateCell(
            CellId(request.Suite, record), campaign.Id, record.Task, reviewer.Id, record.Repeat, Slot: 0, position,
            Claimable.Stored(CellState.Settled, record.Attempt, WorkerIdentity.Nobody, record.Started),
            record.Pin.Match(p => p, _ => ProductPin.None), GateCellOutcomeKind.Completed, string.Empty);

        ImportFile[] files = [new("findings.jsonl", ArtifactClass.Findings, Encoding.UTF8.GetBytes(string.Concat(cell.Reply.Findings.Select(f => f.Json + "\n")))), .. copied.Files];

        return (new ImportPlan(campaign, gateCell, settlement, record.Id, record.SourceJson, files), copied.Skipped);
    }

    /// <summary>A row per distinct definition: the catalog's own when one already hashes alike (under any name), else a new
    /// row named <c>&lt;model&gt;-&lt;hash8&gt;</c>.</summary>
    private async Task<Outcome<(IReadOnlyDictionary<string, GateReviewer>, IReadOnlyList<string>)>> ReviewersAsync(
        IReadOnlyList<CalibCell> cells, CancellationToken cancellationToken)
    {
        var rows = await catalog.ListAsync(includeRetired: true, cancellationToken);
        var byHash = new Dictionary<string, GateReviewer>(StringComparer.Ordinal);
        var added = new List<string>();

        foreach (var cell in cells.DistinctBy(c => c.Definition.Hash))
        {
            var row = CalibReviewers.Existing(rows, cell.Definition) is { } existing ? Outcome<GateReviewer>.Success(existing) : await AddAsync(cell, added, cancellationToken);
            if (row is Outcome<GateReviewer>.Fail fail)
            {
                return Outcome<(IReadOnlyDictionary<string, GateReviewer>, IReadOnlyList<string>)>.Failure(fail.Reason);
            }

            byHash[cell.Definition.Hash] = ((Outcome<GateReviewer>.Ok)row).Value;
        }

        return Outcome<(IReadOnlyDictionary<string, GateReviewer>, IReadOnlyList<string>)>.Success((byHash, added));
    }

    private async Task<Outcome<GateReviewer>> AddAsync(CalibCell cell, List<string> added, CancellationToken cancellationToken)
    {
        var id = CalibReviewers.NewId(cell.Model.Model, cell.Definition);
        if (id is not Outcome<GateReviewerId>.Ok { Value: var reviewerId })
        {
            return Outcome<GateReviewer>.Failure(((Outcome<GateReviewerId>.Fail)id).Reason);
        }

        var row = await catalog.AddAsync(GateReviewer.Create(reviewerId, cell.Definition, clock.GetUtcNow()), cancellationToken);
        if (row is Outcome<GateReviewer>.Ok)
        {
            added.Add(reviewerId.Value);
        }

        return row;
    }

    private static string Word(ImportOutcome outcome) => outcome == ImportOutcome.Imported ? "imported" : "unchanged";
}

/// <summary>The settings hash of an imported cell. The other harnesses kept no snapshot of the <c>COAI_*</c> they sent, so
/// the hash says exactly that — one value per harness, so its imported runs share a scope and never share one with a
/// native run whose snapshot WAS hashed.</summary>
public static class ImportedSettings
{
    public static string Hash(string harness) => StableHash.Of($"imported:{harness}:settings-not-recorded");
}
