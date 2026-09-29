using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Cli;
using Bench.Tests.Gate.Assessment;
using Bench.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static Bench.Tests.Infrastructure.GateStoreFixtures;

namespace Bench.Tests.Gate.Import;

/// <summary>A database of its own (imported ids are DERIVED, so two tests importing one fixture into one database would
/// meet each other's cells), a temporary artefact root, and the redacted calibration written out in its own layout.</summary>
internal sealed class ImportRig : IDisposable
{
    private readonly TempRoot _root = NewRoot();
    private readonly TempRoot _source = NewRoot();

    private ImportRig(string connection)
    {
        Connection = connection;
        Artifacts = Store(_root);
        Rubrics = GateRubrics.Load(Path.Combine(Repository.Root, "prompts")).Ok();
    }

    public string Connection { get; }

    public FileSystemGateArtifactStore Artifacts { get; }

    public IReadOnlyList<LoadedRubric> Rubrics { get; }

    public LoadedRubric Strict => GateRubrics.Asked(Rubrics, GateRubrics.StrictId).Ok();

    public LoadedRubric Lenient => GateRubrics.Labelling(Rubrics, GateRubrics.LenientId).Ok();

    public string Source => _source.Path;

    public string Root => _root.Path;

    public static async Task<ImportRig> NewAsync(PostgresFixture postgres, bool withAssessment = true)
    {
        var rig = new ImportRig(await DatabaseAsync(postgres, "imp"));
        ImportFixture.Materialize(rig.Source, withAssessment);
        await new PostgresGateReviewerCatalog(rig.Db()).AddAsync(AssessRig.Assessor(), CancellationToken.None);
        return rig;
    }

    public BenchDbContext Db() => PostgresFixture.Context(Connection);

    /// <summary>A database of this test's own, reached WITHOUT a pool: a pool per database kept idle connections open until the
    /// shared server ran out of clients for the rest of the suite (observed: <c>53300: sorry, too many clients already</c>).</summary>
    public static async Task<string> DatabaseAsync(PostgresFixture postgres, string prefix)
    {
        var pooled = await postgres.NewDatabaseAsync($"{prefix}_{Guid.NewGuid():N}");
        await using (var migrated = new Npgsql.NpgsqlConnection(pooled))
        {
            // The fixture migrated the new database through a POOLED connection; that idle connection is released here.
            Npgsql.NpgsqlConnection.ClearPool(migrated);
        }

        return new Npgsql.NpgsqlConnectionStringBuilder(pooled) { Pooling = false }.ToString();
    }

    public CalibImport Calib(IGateImportStore? store = null) =>
        new(store ?? new PostgresGateImportStore(Db(), TimeProvider.System), Artifacts, new PostgresGateReviewerCatalog(Db()),
            new CalibVerdictImport(new PostgresGateVerdictStore(Db(), TimeProvider.System), new FileSystemGateAssessmentFiles(Root)), TimeProvider.System);

    public CalibImportRequest Request(bool assessor = true) =>
        new(DirectoryImportSource.Open(Source).Ok(), ImportFixture.Suite, ImportFixture.Models,
            assessor ? Outcome<GateReviewer>.Success(AssessRig.Assessor()) : Outcome<GateReviewer>.Failure("no --assessor was named"),
            Key, Strict, GateRubrics.Catalog(Rubrics), ImportFixture.Names);

    public async Task<Outcome<CalibImportReport>> ImportAsync(CancellationToken ct, IGateImportStore? store = null, bool assessor = true) =>
        await Calib(store).RunAsync(Request(assessor), _ => { }, ct);

    /// <summary>Every run record of every campaign in this database.</summary>
    public async Task<IReadOnlyList<GateRunRecord>> RecordsAsync(CancellationToken ct)
    {
        await using var db = Db();
        var campaigns = await db.GateRuns.AsNoTracking().Select(r => r.Id).ToListAsync(ct);
        var store = new PostgresGateStore(Db(), TimeProvider.System);
        var records = new List<GateRunRecord>();

        foreach (var campaign in campaigns)
        {
            records.AddRange(await store.FactsAsync(campaign, ct));
        }

        return records;
    }

    public async Task<IReadOnlyList<GateVerdict>> VerdictsAsync(IReadOnlyList<GateRunRecord> records, CancellationToken ct) =>
        await new PostgresGateVerdictStore(Db(), TimeProvider.System).VerdictsAsync([.. records.Select(r => r.RunId)], GateRubrics.Catalog(Rubrics), ct);

    /// <summary>How many rows each gate table holds — what "a second import changes nothing" is checked against.</summary>
    public async Task<(int Runs, int Cells, int Findings, int Verdicts, int Reviewers, int Artifacts)> CountsAsync(CancellationToken ct)
    {
        await using var db = Db();
        return (await db.GateRuns.CountAsync(ct), await db.GateCells.CountAsync(ct), await db.GateFindings.CountAsync(ct),
            await db.GateVerdicts.CountAsync(ct), await db.GateReviewers.CountAsync(ct), await db.GateArtifacts.CountAsync(ct));
    }

    public void Dispose()
    {
        _root.Dispose();
        _source.Dispose();
    }
}

/// <summary>An import store that dies after N cells — the process-kill stand-in between the files and the transaction.</summary>
internal sealed class DiesAfter(IGateImportStore inner, int cells) : IGateImportStore
{
    private int _written;

    public Task<ImportedCellState> CellStateAsync(Guid cellId, CancellationToken cancellationToken) => inner.CellStateAsync(cellId, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, string>> StoredReviewersAsync(IReadOnlyCollection<Guid> cellIds, CancellationToken cancellationToken) =>
        inner.StoredReviewersAsync(cellIds, cancellationToken);

    public async Task<Outcome<ImportedCell>> ImportCellAsync(ImportedCell cell, CancellationToken cancellationToken) =>
        ++_written > cells ? throw new SimulatedCrash(ArtifactStep.Renamed) : await inner.ImportCellAsync(cell, cancellationToken);

    public Task<Outcome<int>> RecordSummaryAsync(SummaryCitation citation, GateKind gate, SummaryTable table, CancellationToken cancellationToken) =>
        inner.RecordSummaryAsync(citation, gate, table, cancellationToken);
}

