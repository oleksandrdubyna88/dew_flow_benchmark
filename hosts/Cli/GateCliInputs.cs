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

/// <summary>What <c>bench gate run</c> and <c>resume</c> both stand on, loaded and refused in one place, in the order a
/// person can fix them: the flags (4), the files that must exist (3), the suite (4), the database (3), the reviewers (4),
/// the product pin (3), the file-hash key (3). Nothing is planned until all of it holds.</summary>
public sealed class GateCliInputs : IAsyncDisposable
{
    private GateCliInputs(
        string connection, BenchDbContext db, GateSuite suite, FileSystemGateArtifactStore artifacts, IReadOnlyList<GateReviewer> reviewers,
        ProductPin pin, FileHashKey key, string exe, ILoggerFactory logs, GateCloneCheckouts checkouts, IGateSecrets secrets)
    {
        _connection = connection;
        _db = db;
        Suite = suite;
        Artifacts = artifacts;
        Reviewers = reviewers;
        Pin = pin;
        Key = key;
        ProductExecutable = exe;
        Logs = logs;
        _checkouts = checkouts;
        _secrets = secrets;
        Store = new PostgresGateStore(db, TimeProvider.System);
    }

    private readonly string _connection;
    private readonly BenchDbContext _db;
    private readonly GateCloneCheckouts _checkouts;
    private readonly IGateSecrets _secrets;
    private readonly List<BenchDbContext> _laneContexts = [];

    public GateSuite Suite { get; }

    public FileSystemGateArtifactStore Artifacts { get; }

    public string ArtifactRoot => Artifacts.Root;

    public IReadOnlyList<GateReviewer> Reviewers { get; }

    public ProductPin Pin { get; }

    public FileHashKey Key { get; }

    public string ProductExecutable { get; }

    public ILoggerFactory Logs { get; }

    public PostgresGateStore Store { get; }

    /// <summary>The references a reviewer resolves to on this machine NOW — a resume compares them with the run's cells.</summary>
    public Outcome<ResolvedReferences> ReferencesOf(GateReviewer reviewer) => _secrets.References(reviewer);

    public static string Connection(CommandLine command) =>
        command.Value("db", Environment.GetEnvironmentVariable("BENCH_DB") ?? string.Empty);

    public static BenchDbContext Context(string connection) =>
        new(new DbContextOptionsBuilder<BenchDbContext>().UseNpgsql(connection).Options);

    /// <summary><c>--set "COAI_X=1,COAI_Y=2"</c> as a map — a name given twice (in any case) is refused rather than
    /// resolved by a guess, and a pair without <c>=</c> is refused rather than dropped.</summary>
    public static Outcome<IReadOnlyDictionary<string, string>> Extras(CommandLine command)
    {
        var pairs = command.List("set").Select(pair => pair.Split('=', 2)).ToList();
        var malformed = pairs.FirstOrDefault(p => p.Length != 2 || p[0].Trim().Length == 0);
        var twice = pairs.Where(p => p.Length == 2).GroupBy(p => p[0].Trim(), StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);

        return (malformed, twice) switch
        {
            ({ } bad, _) => Outcome<IReadOnlyDictionary<string, string>>.Failure($"--set '{string.Join('=', bad)}' is not NAME=VALUE"),
            (_, { } group) => Outcome<IReadOnlyDictionary<string, string>>.Failure($"--set names {group.Key.ToUpperInvariant()} twice — say which value is meant"),
            _ => Outcome<IReadOnlyDictionary<string, string>>.Success(pairs.ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase)),
        };
    }

    /// <summary>The reviewers a stored run's cells name — what a resume runs with — or the exit code and the reason it
    /// cannot: no database (4), an unreachable one (3), a run id nothing knows (4).</summary>
    public static async Task<(int Code, string Refusal, IReadOnlyList<string> Reviewers)> ReviewersOfAsync(CommandLine command, Guid runId, CancellationToken cancellationToken)
    {
        if (Connection(command).Length == 0)
        {
            return (ExitCodes.Configuration, "the gate driver needs the database — pass --db or set BENCH_DB", []);
        }

        await using var db = Context(Connection(command));
        try
        {
            var store = new PostgresGateStore(db, TimeProvider.System);

            return await store.LoadAsync(runId, cancellationToken) is Outcome<GateRun>.Ok
                ? (ExitCodes.Pass, string.Empty, [.. (await store.CellsAsync(runId, cancellationToken)).Select(c => c.Reviewer.Value).Distinct(StringComparer.Ordinal)])
                : (ExitCodes.Configuration, $"no gate run {runId} is in this database", []);
        }
        catch (Exception ex) when (ex is Npgsql.NpgsqlException or InvalidOperationException or TimeoutException)
        {
            return (ExitCodes.Environment, $"the database is unreachable — {ex.Message.Split('\n')[0]}", []);
        }
    }

    public static async Task<(int Code, GateCliInputs? Inputs)> LoadAsync(
        CommandLine command, IReadOnlyList<string> reviewerIds, TextWriter error, CancellationToken cancellationToken)
    {
        var flags = FlagRefusal(command, reviewerIds);
        if (flags.Length > 0)
        {
            return (GateRunCommand.Refuse(error, ExitCodes.Configuration, flags), null);
        }

        var files = (File.Exists(SuitePath(command)), File.Exists(command.Value("coai-exe"))) switch
        {
            (false, _) => $"the suite file {Path.GetFileName(SuitePath(command))} is not there",
            (_, false) => $"the product binary {Path.GetFileName(command.Value("coai-exe"))} is not there",
            _ => string.Empty,
        };

        return files.Length > 0
            ? (GateRunCommand.Refuse(error, ExitCodes.Environment, files), null)
            : await LoadStoresAsync(command, reviewerIds, error, cancellationToken);
    }

    private static async Task<(int Code, GateCliInputs? Inputs)> LoadStoresAsync(
        CommandLine command, IReadOnlyList<string> reviewerIds, TextWriter error, CancellationToken cancellationToken)
    {
        var suite = GateSuiteFile.Parse(await File.ReadAllTextAsync(SuitePath(command), cancellationToken));
        var artifacts = FileSystemGateArtifactStore.Open(command.Value("artifact-root"), TimeProvider.System, ArtifactProbe.None);

        var refusal = (suite, artifacts) switch
        {
            (Outcome<GateSuite>.Fail f, _) => f.Reason,
            (_, Outcome<FileSystemGateArtifactStore>.Fail f) => f.Reason,
            _ => string.Empty,
        };

        if (refusal.Length > 0)
        {
            return (GateRunCommand.Refuse(error, ExitCodes.Configuration, refusal), null);
        }

        var db = Context(Connection(command));
        try
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        catch (Npgsql.NpgsqlException ex)
        {
            await db.DisposeAsync();
            return (GateRunCommand.Refuse(error, ExitCodes.Environment, $"the database is unreachable — {ex.Message.Split('\n')[0]}"), null);
        }

        return await LoadProductAsync(command, reviewerIds, db, ((Outcome<GateSuite>.Ok)suite).Value, ((Outcome<FileSystemGateArtifactStore>.Ok)artifacts).Value, error, cancellationToken);
    }

    private static async Task<(int Code, GateCliInputs? Inputs)> LoadProductAsync(
        CommandLine command, IReadOnlyList<string> reviewerIds, BenchDbContext db, GateSuite suite, FileSystemGateArtifactStore artifacts, TextWriter error, CancellationToken cancellationToken)
    {
        var ids = reviewerIds.Select(GateReviewerId.Parse).ToList();
        var badId = ids.OfType<Outcome<GateReviewerId>.Fail>().FirstOrDefault();
        var reviewers = badId is null
            ? await new PostgresGateReviewerCatalog(db).GetAsync([.. ids.OfType<Outcome<GateReviewerId>.Ok>().Select(o => o.Value)], cancellationToken)
            : Outcome<IReadOnlyList<GateReviewer>>.Failure(badId.Reason);

        if (reviewers is Outcome<IReadOnlyList<GateReviewer>>.Fail noReviewers)
        {
            await db.DisposeAsync();
            return (GateRunCommand.Refuse(error, ExitCodes.Configuration, noReviewers.Reason), null);
        }

        var secrets = new GateSecrets(new EnvironmentSecrets(), command.Has("creds-key-from-coai-settings"), GateSecrets.DefaultCoaiSettingsFile);
        var unready = Preflight(((Outcome<IReadOnlyList<GateReviewer>>.Ok)reviewers).Value, secrets);

        if (unready.Length > 0)
        {
            await db.DisposeAsync();
            return (GateRunCommand.Refuse(error, ExitCodes.Environment, unready), null);
        }

        var pin = await new ProductPinReader().ReadAsync(command.Value("coai-exe"), cancellationToken);
        var key = await GateFileHashKeys.ResolveAsync(artifacts, new PostgresGateStore(db, TimeProvider.System), cancellationToken);

        var refusal = (pin, key) switch
        {
            (Outcome<ProductPin>.Fail f, _) => f.Reason,
            (_, Outcome<FileHashKey>.Fail f) => f.Reason,
            _ => string.Empty,
        };

        if (refusal.Length > 0)
        {
            await db.DisposeAsync();
            return (GateRunCommand.Refuse(error, ExitCodes.Environment, refusal), null);
        }

        var checkoutRoot = command.Value("checkout-root", RunCommand.DefaultCheckoutRoot);
        var logs = LoggerFactory.Create(builder => builder.AddSerilog(CliLogging.Start(), dispose: false));
        var checkouts = new GateCloneCheckouts(new GitCheckoutProvider(CheckoutCacheOptions.Under(checkoutRoot), logs.CreateLogger<GitCheckoutProvider>()), checkoutRoot);
        return (ExitCodes.Pass, new GateCliInputs(
            Connection(command), db, suite, artifacts, ((Outcome<IReadOnlyList<GateReviewer>>.Ok)reviewers).Value,
            ((Outcome<ProductPin>.Ok)pin).Value, ((Outcome<FileHashKey>.Ok)key).Value, Path.GetFullPath(command.Value("coai-exe")), logs, checkouts, secrets));
    }

    /// <summary>One lane's services: its own database context (a context is not thread-safe), a store and a runner over it.</summary>
    public GateLane Lane(int number, CommandLine command)
    {
        var db = Context(_connection);
        lock (_laneContexts)
        {
            _laneContexts.Add(db);
        }

        var store = new PostgresGateStore(db, TimeProvider.System);
        var runner = new GateCellRunner(
            store, Artifacts, new McpStdioSessionFactory(), new RecordingTapFactory(), _checkouts, _secrets, new FileGateAttemptFiles(), TimeProvider.System,
            new GateDriverSettings(
                ProductExecutable, ArtifactRoot, !command.Has("no-tap"),
                TimeSpan.FromMinutes(command.Int("cell-timeout-minutes", GateRunCommand.DefaultCellTimeoutMinutes)),
                TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5), ParentEnvironment(), PrivateNames.Of(Suite.PrivateNames).WithHosts([Environment.MachineName])));

        return new GateLane(number, store, runner, new LaneSlot());
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var lane in _laneContexts)
        {
            await lane.DisposeAsync();
        }

        await _db.DisposeAsync();
        Logs.Dispose();
    }

    /// <summary>Every reviewer's references and — for an api row — its vault key, resolved on THIS machine before anything
    /// is planned: a dead environment is one refusal naming the reviewer, not a campaign of cells that each fail alone.
    /// The values are dropped at once; the runner resolves them again per cell.</summary>
    private static string Preflight(IReadOnlyList<GateReviewer> reviewers, IGateSecrets secrets)
    {
        foreach (var reviewer in reviewers)
        {
            var references = secrets.References(reviewer);
            var key = reviewer.Definition.Runtime == ReviewerRuntime.Api ? secrets.CredsKey(reviewer) : Outcome<SecretValue>.Success(SecretValue.None);

            var refusal = (references, key) switch
            {
                (Outcome<ResolvedReferences>.Fail f, _) => f.Reason,
                (_, Outcome<SecretValue>.Fail f) => f.Reason,
                _ => string.Empty,
            };

            if (refusal.Length > 0)
            {
                return $"reviewer '{reviewer.Id}' cannot run on this machine — {refusal}";
            }
        }

        return string.Empty;
    }

    private static IReadOnlyDictionary<string, string> ParentEnvironment() =>
        Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => e.Value as string ?? string.Empty, StringComparer.Ordinal);

    private static string SuitePath(CommandLine command) =>
        command.Value("suite-file", Environment.GetEnvironmentVariable("BENCH_GATE_SUITE") ?? string.Empty);

    private static string FlagRefusal(CommandLine command, IReadOnlyList<string> reviewerIds) =>
        (Connection(command).Length > 0, SuitePath(command).Length > 0, command.Value("coai-exe").Length > 0, command.Value("artifact-root").Length > 0, reviewerIds.Count > 0) switch
        {
            (false, _, _, _, _) => "the gate driver needs the database — pass --db or set BENCH_DB",
            (_, false, _, _, _) => "the gate driver needs the suite — pass --suite-file (or set BENCH_GATE_SUITE)",
            (_, _, false, _, _) => "the gate driver needs the product — pass --coai-exe with the coai-mcp you mean to measure; a default would spend somebody's quota on a guess",
            (_, _, _, false, _) => "the gate driver needs an artefact root outside git — pass --artifact-root",
            (_, _, _, _, false) => "the gate driver needs at least one reviewer — pass --reviewers <id,…>",
            _ => string.Empty,
        };
}
