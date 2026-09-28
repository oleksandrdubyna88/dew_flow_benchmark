using System.Security.Cryptography;
using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Git;
using Bench.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bench.Cli;

/// <summary><c>bench gate import calib | coai-bench | summary</c> (E5) — today's measurements brought in READ-ONLY, without
/// re-spending anything. Refusals in the order a person fixes them: flags 4 · a source that is not there 3 · the suite, the
/// models, the artefact root, the rubric 4 · the database 3 · the source's own records (a line that does not read, a
/// record that changed since it was imported) 4. Exit 0 when everything was imported or was already there unchanged.</summary>
public static class GateImportCommand
{
    public static async Task<int> RunAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken) =>
        command.Operand(1) switch
        {
            "calib" => await CalibAsync(command, output, error, cancellationToken),
            "coai-bench" => await CoaiBenchAsync(command, output, error, cancellationToken),
            "summary" => await SummaryAsync(command, output, error, cancellationToken),
            _ => GateRunCommand.Refuse(error, ExitCodes.Configuration, "gate import needs a source — calib, coai-bench or summary"),
        };

    private static async Task<int> CalibAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var flags = (command.Value("calib").Length, Suite(command).Length, command.Value("calib-models").Length, command.Value("artifact-root").Length, GateCliInputs.Connection(command).Length) switch
        {
            (0, _, _, _, _) => "gate import calib needs --calib <the calibration workspace>",
            (_, 0, _, _, _) => "gate import calib needs --suite-file (or BENCH_GATE_SUITE) — the suite the calibration's tasks are",
            (_, _, 0, _, _) => "gate import calib needs --calib-models <models.json> — the endpoints, key names and prices runs.jsonl does not record",
            (_, _, _, 0, _) => "gate import needs --artifact-root — the copied run directories are private and live there",
            (_, _, _, _, 0) => "gate import needs the database — pass --db or set BENCH_DB",
            _ => string.Empty,
        };

        if (flags.Length > 0)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, flags);
        }

        var inputs = await CalibInputsAsync(command, error, cancellationToken);
        if (inputs.Request is null)
        {
            return inputs.Code;
        }

        await using var session = await Session.OpenAsync(command, error, cancellationToken);
        if (session.Db is null)
        {
            return session.Code;
        }

        try
        {
            return await RunCalibAsync(command, session, inputs.Request, output, error, cancellationToken);
        }
        catch (Exception ex) when (IsStoreFailure(ex))
        {
            return StoreFailed(error, ex);
        }
    }

    private static async Task<int> RunCalibAsync(
        CommandLine command, Session session, Func<Outcome<GateReviewer>, FileHashKey, LoadedRubric, RubricCatalog, CalibImportRequest> request,
        TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var assessor = command.Value("assessor").Length == 0
            ? Outcome<GateReviewer>.Failure("no --assessor was named — the verdicts are attributed to a catalog row (bench gate reviewers add)")
            : GateReviewerId.Parse(command.Value("assessor")) is Outcome<GateReviewerId>.Ok { Value: var id }
                ? (await new PostgresGateReviewerCatalog(session.Db!).GetAsync([id], cancellationToken)).Match(rows => Outcome<GateReviewer>.Success(rows[0]), Outcome<GateReviewer>.Failure)
                : Outcome<GateReviewer>.Failure($"'{command.Value("assessor")}' is not a reviewer id");

        if (command.Value("assessor").Length > 0 && assessor is Outcome<GateReviewer>.Fail unknown)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, unknown.Reason);
        }

        var key = await GateFileHashKeys.ResolveAsync(session.Artifacts!, new PostgresGateStore(session.Db!, TimeProvider.System), cancellationToken);
        if (key is not Outcome<FileHashKey>.Ok { Value: var fileKey })
        {
            return GateRunCommand.Refuse(error, ExitCodes.Environment, ((Outcome<FileHashKey>.Fail)key).Reason);
        }

        var files = new FileSystemGateAssessmentFiles(session.Artifacts!.Root);
        try
        {
            var import = new CalibImport(
                session.Imports!, session.Artifacts, new PostgresGateReviewerCatalog(session.Db!),
                new CalibVerdictImport(new PostgresGateVerdictStore(session.Db!, TimeProvider.System), files), TimeProvider.System);
            var asked = request(assessor, fileKey, session.Strict!, GateRubrics.Catalog(session.Rubrics));

            // The suite's tasks first (E6): a report cannot put the calibration tasks apart without them, and the suite file
            // is the one input that has them.
            var recorded = await GateReportCommand.RecordAsync(new PostgresGateSuiteTasks(session.Db!, TimeProvider.System), asked.Suite, output, cancellationToken);
            if (recorded.Length > 0)
            {
                return GateRunCommand.Refuse(error, ExitCodes.Configuration, recorded);
            }

            var report = await import.RunAsync(asked, output.WriteLine, cancellationToken);

            return report switch
            {
                Outcome<CalibImportReport>.Ok ok => Printed(output, Describe(ok.Value)),
                Outcome<CalibImportReport>.Fail fail => GateRunCommand.Refuse(error, ExitCodes.Configuration, fail.Reason),
                _ => throw new InvalidOperationException("unreachable"),
            };
        }
        finally
        {
            files.Dispose();
        }
    }

    private static async Task<(int Code, Func<Outcome<GateReviewer>, FileHashKey, LoadedRubric, RubricCatalog, CalibImportRequest>? Request)> CalibInputsAsync(
        CommandLine command, TextWriter error, CancellationToken cancellationToken)
    {
        var source = DirectoryImportSource.Open(command.Value("calib"));
        var missing = (source, File.Exists(Suite(command)), File.Exists(command.Value("calib-models"))) switch
        {
            (Outcome<DirectoryImportSource>.Fail f, _, _) => f.Reason,
            (_, false, _) => $"the suite file {Path.GetFileName(Suite(command))} is not there",
            (_, _, false) => $"the calibration models file {Path.GetFileName(command.Value("calib-models"))} is not there",
            _ => string.Empty,
        };

        if (missing.Length > 0)
        {
            return (GateRunCommand.Refuse(error, ExitCodes.Environment, missing), null);
        }

        var suiteText = await File.ReadAllTextAsync(Suite(command), cancellationToken);
        var (suite, names, models) = (GateSuiteFile.Parse(suiteText), GatePrivateNames.Read(suiteText), CalibModels.Parse(await File.ReadAllTextAsync(command.Value("calib-models"), cancellationToken)));

        return (suite, names, models) switch
        {
            (Outcome<GateSuite>.Fail f, _, _) => (GateRunCommand.Refuse(error, ExitCodes.Configuration, f.Reason), null),
            (_, Outcome<PrivateNames>.Fail f, _) => (GateRunCommand.Refuse(error, ExitCodes.Configuration, f.Reason), null),
            (_, _, Outcome<IReadOnlyDictionary<string, CalibModel>>.Fail f) => (GateRunCommand.Refuse(error, ExitCodes.Configuration, f.Reason), null),
            (Outcome<GateSuite>.Ok s, Outcome<PrivateNames>.Ok n, Outcome<IReadOnlyDictionary<string, CalibModel>>.Ok m) =>
                (ExitCodes.Pass, (assessor, key, strict, rubrics) => new CalibImportRequest(
                    ((Outcome<DirectoryImportSource>.Ok)source).Value, s.Value, m.Value, assessor, key, strict, rubrics,
                    n.Value.WithHosts([Environment.MachineName]))),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    private static async Task<int> CoaiBenchAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var flags = (command.List("runs").Count, command.Value("repo").Length, command.Value("artifact-root").Length, GateCliInputs.Connection(command).Length) switch
        {
            (0, _, _, _) => "gate import coai-bench needs --runs <runs.json>[,<runs.json>…]",
            (_, 0, _, _) => "gate import coai-bench needs --repo <the product's checkout> — the cases' short shas are resolved there",
            (_, _, 0, _) => "gate import needs --artifact-root — the findings' text is private and lives there",
            (_, _, _, 0) => "gate import needs the database — pass --db or set BENCH_DB",
            _ => string.Empty,
        };

        var absent = command.List("runs").FirstOrDefault(f => !File.Exists(f));
        if (flags.Length > 0 || absent is not null || !Directory.Exists(command.Value("repo")))
        {
            return flags.Length > 0
                ? GateRunCommand.Refuse(error, ExitCodes.Configuration, flags)
                : GateRunCommand.Refuse(error, ExitCodes.Environment, absent is not null ? $"{Path.GetFileName(absent)} is not there" : "--repo is not a directory");
        }

        await using var session = await Session.OpenAsync(command, error, cancellationToken);
        if (session.Db is null)
        {
            return session.Code;
        }

        var key = await GateFileHashKeys.ResolveAsync(session.Artifacts!, new PostgresGateStore(session.Db, TimeProvider.System), cancellationToken);
        if (key is not Outcome<FileHashKey>.Ok { Value: var fileKey } || GateRubrics.Labelling(session.Rubrics, GateRubrics.LenientId) is not Outcome<LoadedRubric>.Ok { Value: var lenient })
        {
            return GateRunCommand.Refuse(error, ExitCodes.Environment, key is Outcome<FileHashKey>.Fail f ? f.Reason : "the lenient rubric is not in the prompt catalog");
        }

        var files = new List<CoaiBenchFile>();
        foreach (var path in command.List("runs"))
        {
            files.Add(new CoaiBenchFile(new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path))!).Name, await File.ReadAllTextAsync(path, cancellationToken)));
        }

        var import = new CoaiBenchImport(session.Imports!, session.Artifacts!, new PostgresGateVerdictStore(session.Db, TimeProvider.System), new GitCommitResolver(command.Value("repo")));
        Outcome<CoaiBenchImportReport> report;
        try
        {
            report = await import.RunAsync(new CoaiBenchImportRequest(files, fileKey, lenient, GateRubrics.Catalog(session.Rubrics), PrivateNames.None.WithHosts([Environment.MachineName])), output.WriteLine, cancellationToken);
        }
        catch (Exception ex) when (IsStoreFailure(ex))
        {
            return StoreFailed(error, ex);
        }

        return report switch
        {
            Outcome<CoaiBenchImportReport>.Ok ok => await RecordedAsync(new PostgresGateSuiteTasks(session.Db, TimeProvider.System), ok.Value, output, cancellationToken) is { Length: > 0 } refused
                ? GateRunCommand.Refuse(error, ExitCodes.Configuration, refused)
                : await PrintedAsync(output, ok.Value, session.Artifacts!.Root, command.Value("repo"), cancellationToken),
            Outcome<CoaiBenchImportReport>.Fail fail => GateRunCommand.Refuse(error, ExitCodes.Configuration, fail.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    private static async Task<int> SummaryAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var gate = Enum.TryParse<GateKind>(command.Value("gate"), ignoreCase: true, out var g) && Enum.IsDefined(g) && !int.TryParse(command.Value("gate"), out _) ? g : (GateKind?)null;
        var flags = (command.Value("document").Length, command.Value("section").Length, gate, GateCliInputs.Connection(command).Length) switch
        {
            (0, _, _, _) => "gate import summary needs --document <RESULTS_*.md>",
            (_, 0, _, _) => "gate import summary needs --section \"<the heading the table sits under>\"",
            (_, _, null, _) => "gate import summary needs --gate plan|code|feature",
            (_, _, _, 0) => "gate import needs the database — pass --db or set BENCH_DB",
            _ => string.Empty,
        };

        if (flags.Length > 0 || !File.Exists(command.Value("document")))
        {
            return flags.Length > 0 ? GateRunCommand.Refuse(error, ExitCodes.Configuration, flags) : GateRunCommand.Refuse(error, ExitCodes.Environment, $"{Path.GetFileName(command.Value("document"))} is not there");
        }

        var bytes = await File.ReadAllBytesAsync(command.Value("document"), cancellationToken);
        var table = SummaryTables.Parse(System.Text.Encoding.UTF8.GetString(bytes), command.Value("section"));
        if (table is not Outcome<SummaryTable>.Ok { Value: var parsed })
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, ((Outcome<SummaryTable>.Fail)table).Reason);
        }

        await using var db = GateCliInputs.Context(GateCliInputs.Connection(command));
        try
        {
            await db.Database.MigrateAsync(cancellationToken);
            var citation = new SummaryCitation(command.Value("source", "coai-results"), Path.GetFileName(command.Value("document")), Convert.ToHexStringLower(SHA256.HashData(bytes)));
            return await new PostgresGateImportStore(db, TimeProvider.System).RecordSummaryAsync(citation, gate!.Value, parsed, cancellationToken) switch
            {
                Outcome<int>.Ok stored => Printed(output, $"summary        {citation.Document} § {parsed.Section}: {parsed.Rows.Count} row(s), {stored.Value} new number(s) — summary only, never averaged with runs"),
                Outcome<int>.Fail refused => GateRunCommand.Refuse(error, ExitCodes.Configuration, refused.Reason),
                _ => throw new InvalidOperationException("unreachable"),
            };
        }
        catch (Exception ex) when (ex is Npgsql.NpgsqlException or InvalidOperationException or TimeoutException)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Environment, $"the database is unreachable — {ex.Message.Split('\n')[0]}");
        }
    }

    /// <summary>Each location's suite recorded (E6): empty when all were, else why the first could not be.</summary>
    private static async Task<string> RecordedAsync(IGateSuiteTasks tasks, CoaiBenchImportReport report, TextWriter output, CancellationToken cancellationToken)
    {
        foreach (var suite in report.Suites)
        {
            var refused = await GateReportCommand.RecordAsync(tasks, suite, output, cancellationToken);
            if (refused.Length > 0)
            {
                return refused;
            }
        }

        return string.Empty;
    }

    private static async Task<int> PrintedAsync(TextWriter output, CoaiBenchImportReport report, string artifactRoot, string repo, CancellationToken cancellationToken)
    {
        // The cases' suites, so a report can be asked for these runs by suite file like any other: each names the product's
        // checkout, a path on this machine, so it lives in the artefact root with everything else private.
        var folder = Path.Combine(artifactRoot, "imports");
        Directory.CreateDirectory(folder);
        foreach (var suite in report.Suites)
        {
            await File.WriteAllTextAsync(Path.Combine(folder, $"coai-bench-cases-{suite.Hash[..12]}.suite.json"), GateSuiteFile.Json(suite, _ => repo), cancellationToken);
        }

        return Printed(output,
            $"imported       {report.Cells.Imported} cell(s), {report.Cells.Unchanged} already there unchanged · suite(s) {string.Join(", ", report.Suites.Select(s => s.Stamp))} (written to imports/)\n"
            + $"verdicts       {report.Verdicts} judged ({report.VerdictsNew} new) under lenient-worth-v1 · {report.Unjudged} unjudged, left unassessed · {report.FamilyMatched} by the arm's own family");
    }

    private static string Describe(CalibImportReport report) =>
        $"imported       {report.Cells.Imported} cell(s), {report.Cells.Unchanged} already there unchanged, {report.Cells.FilesSkipped} file(s) not copied (a name outside the artefact path alphabet)\n"
        + $"campaigns      {string.Join(", ", report.Campaigns)}\n"
        + $"reviewers      {(report.ReviewersAdded.Count > 0 ? string.Join(", ", report.ReviewersAdded) + " added" : "every one already in the catalog")}\n"
        + $"verdicts       {report.Verdicts.Verdicts} under strict-v1 ({report.Verdicts.New} new, {report.Verdicts.Batches} batch(es), {report.Verdicts.FamilyMatched} by the reviewer's own family) — "
        + "strict % stays 'not hand-checked' until a person records a hand-check";

    /// <summary>The database going away mid-import is the environment (3), never a crash: each cell is its own transaction, so
    /// what was committed stays and the next import resumes the rest.</summary>
    private static bool IsStoreFailure(Exception ex) => ex is Npgsql.NpgsqlException or DbUpdateException or TimeoutException;

    private static int StoreFailed(TextWriter error, Exception ex) =>
        GateRunCommand.Refuse(error, ExitCodes.Environment,
            $"the database failed during the import — {ex.Message.Split('\n')[0]}; what was committed stays, and the next import resumes the rest");

    private static int Printed(TextWriter output, string text)
    {
        output.WriteLine(text);
        return ExitCodes.Pass;
    }

    private static string Suite(CommandLine command) =>
        command.Value("suite-file", Environment.GetEnvironmentVariable("BENCH_GATE_SUITE") ?? string.Empty);

    /// <summary>The stores an import stands on, opened in the order a person fixes them: the artefact root and the rubrics
    /// (4 / 3), then the database, migrated (3).</summary>
    private sealed class Session : IAsyncDisposable
    {
        private Session(int code, BenchDbContext? db, FileSystemGateArtifactStore? artifacts, IReadOnlyList<LoadedRubric> rubrics)
        {
            Code = code;
            Db = db;
            Artifacts = artifacts;
            Rubrics = rubrics;
            Imports = db is null ? null : new PostgresGateImportStore(db, TimeProvider.System);
            Strict = GateRubrics.Asked(rubrics, GateRubrics.StrictId) is Outcome<LoadedRubric>.Ok { Value: var strict } ? strict : null;
        }

        public int Code { get; }

        public BenchDbContext? Db { get; }

        public FileSystemGateArtifactStore? Artifacts { get; }

        public IReadOnlyList<LoadedRubric> Rubrics { get; }

        public PostgresGateImportStore? Imports { get; }

        public LoadedRubric? Strict { get; }

        public static async Task<Session> OpenAsync(CommandLine command, TextWriter error, CancellationToken cancellationToken)
        {
            var artifacts = FileSystemGateArtifactStore.Open(command.Value("artifact-root"), TimeProvider.System, ArtifactProbe.None);
            var rubrics = GateRubrics.Load(command.Value("prompts", "prompts"));

            if ((artifacts, rubrics) is not (Outcome<FileSystemGateArtifactStore>.Ok { Value: var store }, Outcome<IReadOnlyList<LoadedRubric>>.Ok { Value: var loaded }))
            {
                return artifacts is Outcome<FileSystemGateArtifactStore>.Fail a
                    ? new Session(GateRunCommand.Refuse(error, ExitCodes.Configuration, a.Reason), null, null, [])
                    : new Session(GateRunCommand.Refuse(error, ExitCodes.Environment, ((Outcome<IReadOnlyList<LoadedRubric>>.Fail)rubrics).Reason), null, null, []);
            }

            var db = GateCliInputs.Context(GateCliInputs.Connection(command));
            try
            {
                await db.Database.MigrateAsync(cancellationToken);
                return new Session(ExitCodes.Pass, db, store, loaded);
            }
            catch (Exception ex) when (ex is Npgsql.NpgsqlException or InvalidOperationException or TimeoutException)
            {
                await db.DisposeAsync();
                return new Session(GateRunCommand.Refuse(error, ExitCodes.Environment, $"the database is unreachable — {ex.Message.Split('\n')[0]}"), null, null, []);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Db is not null)
            {
                await Db.DisposeAsync();
            }
        }
    }
}
