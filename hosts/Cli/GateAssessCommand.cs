using Bench.Application;
using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Git;
using Bench.Infrastructure.Models;
using Bench.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Bench.Cli;

/// <summary><c>bench gate assess</c> and <c>bench gate hand-check sample | record</c> — the blinded strict assessment (E4).
/// Refusals in the order a person fixes them: flags 4 · the suite file 3 · the suite, the artefact root 4 · the rubric
/// files 3 · the database 3 · the runs (unknown, another suite) 4 · the assessor (not in the catalog, not codex or claude)
/// 4 · its executable reference 3 · the file-hash key 3. <c>assess</c> exits 0 when every finding has a reading, 5 when
/// some are left unassessed (resumable), 3 when the assessor never answered at all.</summary>
public static class GateAssessCommand
{
    public const int DefaultWallMinutes = 90;

    public static async Task<int> AssessAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var flags = AssessFlags(command);
        if (flags.Length > 0)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, flags);
        }

        if (!File.Exists(Suite(command)))
        {
            return GateRunCommand.Refuse(error, ExitCodes.Environment, $"the suite file {Path.GetFileName(Suite(command))} is not there");
        }

        var suite = GateSuiteFile.Parse(await File.ReadAllTextAsync(Suite(command), cancellationToken));
        if (suite is not Outcome<GateSuite>.Ok { Value: var parsed })
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, ((Outcome<GateSuite>.Fail)suite).Reason);
        }

        var (code, session) = await Session.OpenAsync(command, error, cancellationToken);
        if (session is null)
        {
            return code;
        }

        await using (session)
        {
            return await AssessWithAsync(command, session, parsed, output, error, cancellationToken);
        }
    }

    public static async Task<int> HandCheckAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var verb = command.Operand(1);
        var flags = (verb, HandCheckFlags(command)) switch
        {
            ("sample" or "record", var f) => f,
            _ => "gate hand-check needs a sub-verb — sample or record",
        };

        if (flags.Length > 0 || (verb == "record" && command.Value("file").Length == 0))
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, flags.Length > 0 ? flags : "gate hand-check record needs --file <the answered sample>");
        }

        var (code, session) = await Session.OpenAsync(command, error, cancellationToken);
        if (session is null)
        {
            return code;
        }

        await using (session)
        {
            var scope = await HandCheckScopeAsync(command, session, error, cancellationToken);
            return scope.Scope is null
                ? scope.Code
                : verb == "sample"
                    ? await SampleAsync(command, session, scope.Scope, output, error, cancellationToken)
                    : await RecordAsync(command, session, scope.Scope, output, error, cancellationToken);
        }
    }

    private static async Task<int> AssessWithAsync(
        CommandLine command, Session session, GateSuite suite, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var campaigns = await CampaignsAsync(command, session.Store, suite.Stamp, cancellationToken);
        if (campaigns is not Outcome<IReadOnlyList<Guid>>.Ok { Value: var runs })
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, ((Outcome<IReadOnlyList<Guid>>.Fail)campaigns).Reason);
        }

        var launch = await LaunchAsync(command, session, error, cancellationToken);
        if (launch.Launch is null)
        {
            return launch.Code;
        }

        var reviewers = await ReviewersAsync(session, runs, cancellationToken);
        if (reviewers is not Outcome<IReadOnlyDictionary<string, GateReviewer>>.Ok { Value: var byId })
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, ((Outcome<IReadOnlyDictionary<string, GateReviewer>>.Fail)reviewers).Reason);
        }

        var key = await GateFileHashKeys.ResolveAsync(session.Artifacts, session.Store, cancellationToken);
        if (key is not Outcome<FileHashKey>.Ok { Value: var fileKey })
        {
            return GateRunCommand.Refuse(error, ExitCodes.Environment, ((Outcome<FileHashKey>.Fail)key).Reason);
        }

        var size = BatchSize.Of(command.Int("batch-size", BatchSize.Max)) is Outcome<BatchSize>.Ok { Value: var s } ? s : BatchSize.Default;
        var request = new AssessmentRequest(runs, suite, launch.Launch, GateRubrics.Catalog(session.Rubrics), size, fileKey, byId);
        var progress = new AssessmentProgress(
            started => output.WriteLine($"sent           {started.BatchId}: {started.Asked} finding(s) of task {started.Task} to '{launch.Launch.Assessor.Id}' — a batch can take many minutes"),
            batch => output.WriteLine(Describe(batch)));
        var report = await session.Pass(Checkouts(command, session.Logs)).RunAsync(request, progress, cancellationToken);

        return report switch
        {
            Outcome<AssessmentReport>.Ok ok => await PrintAsync(session, suite, runs, ok.Value, output, cancellationToken),
            Outcome<AssessmentReport>.Fail fail => GateRunCommand.Refuse(error, ExitCodes.Environment, fail.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    private static async Task<int> PrintAsync(
        Session session, GateSuite suite, IReadOnlyList<Guid> runs, AssessmentReport report, TextWriter output, CancellationToken cancellationToken)
    {
        foreach (var refusal in report.Refusals)
        {
            output.WriteLine($"refused        {refusal}");
        }

        foreach (var task in suite.Tasks.Where(t => t.IsSeeded))
        {
            var evidence = await GateSeedEvidence.ReadAsync(session.Store, session.Artifacts, runs, task, cancellationToken);
            output.WriteLine($"evidence       {task.Id}: {string.Join(", ", evidence.Select(e => $"{e.Seed}{(e.CrossEpic ? "*" : string.Empty)} {Word(e.Where)}"))}");
        }

        output.WriteLine($"assessed       {report.Assessed} finding(s) · assessment failed {report.Failed} · left unassessed {report.Unassessed} · "
                         + $"{report.Exported} newly blinded · {report.FamilyMatched} read by the reviewer's own family (counted apart)");

        var neverAnswered = report.Batches.Count > 0 && report.Batches.All(b => b.Failure == nameof(AssessmentFailureCause.NoAnswer));

        return (neverAnswered, report.Unassessed) switch
        {
            (true, _) => ExitCodes.Environment,
            (_, > 0) => ExitCodes.NoReport,
            _ => ExitCodes.Pass,
        };
    }

    private static async Task<int> SampleAsync(CommandLine command, Session session, HandCheckScope scope, TextWriter output, TextWriter error, CancellationToken cancellationToken) =>
        await session.HandChecks().SampleAsync(scope, command.Int("count", HandCheck.MinVerdicts), cancellationToken) switch
        {
            Outcome<string>.Ok ok => Printed(output, $"sampled        {ok.Value}\n               read each row against the code, set \"agree\" to true or false, then: bench gate hand-check record --file <it>"),
            Outcome<string>.Fail fail => GateRunCommand.Refuse(error, ExitCodes.Configuration, fail.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };

    private static async Task<int> RecordAsync(CommandLine command, Session session, HandCheckScope scope, TextWriter output, TextWriter error, CancellationToken cancellationToken) =>
        await session.HandChecks().RecordAsync(scope, command.Value("file"), cancellationToken) switch
        {
            Outcome<HandCheck>.Ok ok => Printed(output, $"hand-checked   {ok.Value.Read} verdict(s) of '{ok.Value.Assessor}' under {ok.Value.Rubric.Stamp}: {ok.Value.Agreed} agreed"),
            Outcome<HandCheck>.Fail fail => GateRunCommand.Refuse(error, ExitCodes.Configuration, fail.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };

    private static int Printed(TextWriter output, string line)
    {
        output.WriteLine(line);
        return ExitCodes.Pass;
    }

    private static async Task<(int Code, HandCheckScope? Scope)> HandCheckScopeAsync(CommandLine command, Session session, TextWriter error, CancellationToken cancellationToken)
    {
        var campaigns = await CampaignsAsync(command, session.Store, command.Value("scope"), cancellationToken);
        var rubric = GateRubrics.Asked(session.Rubrics, command.Value("rubric", GateRubrics.StrictId));
        var assessor = GateReviewerId.Parse(command.Value("assessor"));

        return (campaigns, rubric, assessor) switch
        {
            (Outcome<IReadOnlyList<Guid>>.Fail f, _, _) => (GateRunCommand.Refuse(error, ExitCodes.Configuration, f.Reason), null),
            (_, Outcome<LoadedRubric>.Fail f, _) => (GateRunCommand.Refuse(error, ExitCodes.Configuration, f.Reason), null),
            (_, _, Outcome<GateReviewerId>.Fail f) => (GateRunCommand.Refuse(error, ExitCodes.Configuration, f.Reason), null),
            (Outcome<IReadOnlyList<Guid>>.Ok c, Outcome<LoadedRubric>.Ok r, Outcome<GateReviewerId>.Ok a) =>
                (ExitCodes.Pass, new HandCheckScope(c.Value, a.Value, r.Value.Rubric, GateRubrics.Catalog(session.Rubrics))),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    /// <summary>The assessor's catalog row, as a launch: a codex or claude row, its executable resolved on this machine
    /// (the row's reference, or the CLI's own name on PATH), the rubric it is asked under.</summary>
    private static async Task<(int Code, AssessorLaunch? Launch)> LaunchAsync(CommandLine command, Session session, TextWriter error, CancellationToken cancellationToken)
    {
        var rubric = GateRubrics.Asked(session.Rubrics, command.Value("rubric", GateRubrics.StrictId));
        var assessor = GateReviewerId.Parse(command.Value("assessor")) is Outcome<GateReviewerId>.Ok { Value: var id }
            ? await new PostgresGateReviewerCatalog(session.Db).GetAsync([id], cancellationToken)
            : Outcome<IReadOnlyList<GateReviewer>>.Failure($"'{command.Value("assessor")}' is not a reviewer id");

        var row = assessor is Outcome<IReadOnlyList<GateReviewer>>.Ok { Value: [var one] } ? one : null;
        var kind = row is null ? Outcome<Domain.Registry.ModelRuntimeKind>.Failure(((Outcome<IReadOnlyList<GateReviewer>>.Fail)assessor).Reason) : FindingAssessor.KindOf(row);

        if ((rubric, kind) is not (Outcome<LoadedRubric>.Ok { Value: var loaded }, Outcome<Domain.Registry.ModelRuntimeKind>.Ok { Value: var cli }))
        {
            return (GateRunCommand.Refuse(error, ExitCodes.Configuration, rubric is Outcome<LoadedRubric>.Fail r ? r.Reason : ((Outcome<Domain.Registry.ModelRuntimeKind>.Fail)kind).Reason), null);
        }

        var executable = Executable(row!);
        return executable is Outcome<string>.Ok { Value: var exe }
            ? (ExitCodes.Pass, new AssessorLaunch(row!, cli, exe, TimeSpan.FromMinutes(command.Int("wall-minutes", DefaultWallMinutes)), loaded))
            : (GateRunCommand.Refuse(error, ExitCodes.Environment, ((Outcome<string>.Fail)executable).Reason), null);
    }

    private static Outcome<string> Executable(GateReviewer assessor) =>
        assessor.Definition.ExecutableRef.Length == 0
            ? Outcome<string>.Success(assessor.Definition.Runtime.Word())
            : new EnvironmentSecrets().Resolve(assessor.Definition.ExecutableRef)
                .Match(Outcome<string>.Success, reason => Outcome<string>.Failure($"assessor '{assessor.Id}' names {assessor.Definition.ExecutableRef} for its CLI — {reason}"));

    /// <summary>The campaigns an invocation names: <c>--run</c> ids that exist and were planned against THIS suite, or every
    /// campaign of the <c>--scope</c> suite stamp (which must be the suite file's own when a suite is given).</summary>
    private static async Task<Outcome<IReadOnlyList<Guid>>> CampaignsAsync(CommandLine command, PostgresGateStore store, string stamp, CancellationToken cancellationToken)
    {
        if (command.Value("scope").Length > 0)
        {
            var scoped = await store.RunsOfSuiteAsync(command.Value("scope"), cancellationToken);

            return (scoped.Count, command.Value("scope") == stamp) switch
            {
                (_, false) => Outcome<IReadOnlyList<Guid>>.Failure($"--scope {command.Value("scope")} is not the suite file's stamp ({stamp}) — the seeds and the checkouts would be another suite's"),
                (0, _) => Outcome<IReadOnlyList<Guid>>.Failure($"no gate run in this database was planned against suite {command.Value("scope")}"),
                _ => Outcome<IReadOnlyList<Guid>>.Success(scoped),
            };
        }

        var ids = new List<Guid>();
        foreach (var value in command.List("run"))
        {
            var known = Guid.TryParse(value, out var id) ? await store.LoadAsync(id, cancellationToken) : Outcome<GateRun>.Failure($"'{value}' is not a run id");

            var refusal = known switch
            {
                Outcome<GateRun>.Fail f => f.Reason,
                Outcome<GateRun>.Ok { Value.SuiteStamp: var s } when stamp.Length > 0 && s != stamp => $"run {id} was planned against suite {s}, not {stamp} — its seeds and checkouts are another suite's",
                _ => string.Empty,
            };

            if (refusal.Length > 0)
            {
                return Outcome<IReadOnlyList<Guid>>.Failure(refusal);
            }

            ids.Add(id);
        }

        return Outcome<IReadOnlyList<Guid>>.Success(ids);
    }

    private static async Task<Outcome<IReadOnlyDictionary<string, GateReviewer>>> ReviewersAsync(Session session, IReadOnlyList<Guid> runs, CancellationToken cancellationToken)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var run in runs)
        {
            ids.UnionWith((await session.Store.CellsAsync(run, cancellationToken)).Select(c => c.Reviewer.Value));
        }

        var rows = await new PostgresGateReviewerCatalog(session.Db).GetAsync(
            [.. ids.Order(StringComparer.Ordinal).Select(GateReviewerId.Parse).OfType<Outcome<GateReviewerId>.Ok>().Select(o => o.Value)], cancellationToken);

        return rows.Match(
            list => Outcome<IReadOnlyDictionary<string, GateReviewer>>.Success(list.ToDictionary(r => r.Id.Value, StringComparer.Ordinal)),
            Outcome<IReadOnlyDictionary<string, GateReviewer>>.Failure);
    }

    private static GateCloneCheckouts Checkouts(CommandLine command, ILoggerFactory logs)
    {
        var root = command.Value("checkout-root", RunCommand.DefaultCheckoutRoot);
        return new GateCloneCheckouts(new GitCheckoutProvider(CheckoutCacheOptions.Under(root), logs.CreateLogger<GitCheckoutProvider>()), root);
    }

    private static string Describe(BatchOutcome batch) =>
        batch.Failure.Length > 0
            ? $"batch          {batch.BatchId}: {batch.Asked} finding(s) recorded as assessment failed — {batch.Failure}"
            : $"batch          {batch.BatchId}: {batch.Assessed} of {batch.Asked} read" + (batch.Missing > 0 ? $" · {batch.Missing} missing" : string.Empty);

    private static string Word(EvidenceWhere where) => where switch
    {
        EvidenceWhere.Pack => "pack",
        EvidenceWhere.PackByFile => "pack (by file)",
        EvidenceWhere.OnRequest => "on request",
        EvidenceWhere.Withheld => "withheld",
        _ => "unknown",
    };

    private static string AssessFlags(CommandLine command) =>
        (Common(command), Suite(command).Length > 0, BatchSize.Of(command.Int("batch-size", BatchSize.Max))) switch
        {
            ({ Length: > 0 } common, _, _) => common,
            (_, false, _) => "gate assess needs the suite — pass --suite-file (or set BENCH_GATE_SUITE); the seeds and the checkouts are its",
            (_, _, Outcome<BatchSize>.Fail f) => $"--batch-size: {f.Reason}",
            _ => string.Empty,
        };

    private static string HandCheckFlags(CommandLine command) => Common(command);

    private static string Common(CommandLine command) =>
        (GateCliInputs.Connection(command).Length > 0, command.Value("artifact-root").Length > 0, command.Value("assessor").Length > 0,
            command.List("run").Count > 0, command.Value("scope").Length > 0) switch
        {
            (false, _, _, _, _) => "the assessment needs the database — pass --db or set BENCH_DB",
            (_, false, _, _, _) => "the assessment needs the artefact root the runs wrote — pass --artifact-root",
            (_, _, false, _, _) => "the assessment needs --assessor <reviewer id> — a codex or claude row of the reviewer catalog",
            (_, _, _, false, false) => "name what to assess — --run <id>[,<id>…] or --scope <suite stamp>",
            (_, _, _, true, true) => "--run and --scope name the same thing twice — pass one",
            _ => string.Empty,
        };

    private static string Suite(CommandLine command) =>
        command.Value("suite-file", Environment.GetEnvironmentVariable("BENCH_GATE_SUITE") ?? string.Empty);

    /// <summary>The stores one invocation stands on, opened in the order a person fixes them.</summary>
    private sealed class Session(BenchDbContext db, FileSystemGateArtifactStore artifacts, IReadOnlyList<LoadedRubric> rubrics, ILoggerFactory logs) : IAsyncDisposable
    {
        public BenchDbContext Db { get; } = db;

        public PostgresGateStore Store { get; } = new(db, TimeProvider.System);

        public FileSystemGateArtifactStore Artifacts { get; } = artifacts;

        public FileSystemGateAssessmentFiles Files { get; } = new(artifacts.Root);

        public IReadOnlyList<LoadedRubric> Rubrics { get; } = rubrics;

        public ILoggerFactory Logs { get; } = logs;

        public static async Task<(int Code, Session? Session)> OpenAsync(CommandLine command, TextWriter error, CancellationToken cancellationToken)
        {
            var artifacts = FileSystemGateArtifactStore.Open(command.Value("artifact-root"), TimeProvider.System, ArtifactProbe.None);
            var rubrics = GateRubrics.Load(command.Value("prompts", "prompts"));

            if ((artifacts, rubrics) is not (Outcome<FileSystemGateArtifactStore>.Ok { Value: var store }, Outcome<IReadOnlyList<LoadedRubric>>.Ok { Value: var loaded }))
            {
                return artifacts is Outcome<FileSystemGateArtifactStore>.Fail a
                    ? (GateRunCommand.Refuse(error, ExitCodes.Configuration, a.Reason), null)
                    : (GateRunCommand.Refuse(error, ExitCodes.Environment, ((Outcome<IReadOnlyList<LoadedRubric>>.Fail)rubrics).Reason), null);
            }

            var db = GateCliInputs.Context(GateCliInputs.Connection(command));
            try
            {
                await db.Database.MigrateAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is Npgsql.NpgsqlException or InvalidOperationException or TimeoutException)
            {
                await db.DisposeAsync();
                return (GateRunCommand.Refuse(error, ExitCodes.Environment, $"the database is unreachable — {ex.Message.Split('\n')[0]}"), null);
            }

            return (ExitCodes.Pass, new Session(db, store, loaded, LoggerFactory.Create(builder => builder.AddSerilog(CliLogging.Start(), dispose: false))));
        }

        public GateAssessmentPass Pass(IGateCheckouts checkouts) =>
            new(Store, Artifacts, new PostgresGateVerdictStore(Db, TimeProvider.System), Files, checkouts,
                new FindingAssessor(new CliAgentRuntime(Logs.CreateLogger<CliAgentRuntime>()), Files), TimeProvider.System, Random.Shared);

        public GateHandChecks HandChecks() =>
            new(Store, Artifacts, new PostgresGateVerdictStore(Db, TimeProvider.System), Files, TimeProvider.System, Random.Shared);

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            Files.Dispose();
            Logs.Dispose();
        }
    }
}
