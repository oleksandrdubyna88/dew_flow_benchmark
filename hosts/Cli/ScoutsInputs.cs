using Bench.Application.Probes;
using Bench.Application.Scouts;
using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Git;
using Bench.Infrastructure.Models;
using Bench.Infrastructure.Persistence;
using Bench.Infrastructure.Process;
using Bench.Infrastructure.Scouts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Bench.Cli;

/// <summary>What the scout verbs reach outside the process through, as one value a test hands in: the CLI word lookup, the checkpoint
/// port (a test's crash), the variables a TEST adds to the child's allow-listed environment (the fake CLI's own home), and who claims (a
/// test plays a crashed harness by claiming as an owner whose pid is gone). Production (<see cref="Machine"/>) adds no variable — its argv
/// and environment are exactly what the runner composes — and claims as this process.</summary>
public sealed record ScoutVerbServices(
    Func<string, Outcome<string>> ResolveWord, IScoutCheckpoints Checkpoints, IReadOnlyDictionary<string, string> TestChildVariables, WorkerIdentity Owner)
{
    public static ScoutVerbServices Machine => new(
        CliExecutable.OnThisMachine, NoScoutCheckpoints.Instance, new Dictionary<string, string>(StringComparer.Ordinal), WorkerIdentity.Here("scouts"));
}

/// <summary>What every scout verb that touches the store stands on, loaded and refused in one place, in the order a person fixes things:
/// the flags (4), the roots and the grant guard (4 — <see cref="ScoutRoots"/>), the database (3 — unreachable; migrated on entry, as the
/// probes' driver does). One context: a scout campaign runs one cell at a time.</summary>
public sealed class ScoutsInputs : IAsyncDisposable
{
    private readonly BenchDbContext _db;

    private ScoutsInputs(BenchDbContext db, ScoutRootSet roots, ScoutPinSources pins, string repositoryUrl, ILoggerFactory logs)
    {
        _db = db;
        Roots = roots;
        PinSources = pins;
        Logs = logs;
        Store = new PostgresScoutStore(db, TimeProvider.System);
        Reads = new PostgresScoutReads(db);
        Verdicts = new PostgresScoutVerdicts(db);
        Mirrors = new GitCheckoutProvider(CheckoutCacheOptions.Under(roots.Mirrors), logs.CreateLogger<GitCheckoutProvider>());
        TreeSettings = new ScoutTreeSettings(roots.Trees, repositoryUrl, TimeSpan.FromMinutes(20));
        Trees = new ScoutTrees(TreeSettings, Mirrors, logs.CreateLogger<ScoutTrees>());
    }

    public ScoutRootSet Roots { get; }

    public ScoutPinSources PinSources { get; }

    public ILoggerFactory Logs { get; }

    public PostgresScoutStore Store { get; }

    public PostgresScoutReads Reads { get; }

    /// <summary>The analysis's writes — claim verdicts and recomputed recall (S3).</summary>
    public PostgresScoutVerdicts Verdicts { get; }

    public GitCheckoutProvider Mirrors { get; }

    public ScoutTreeSettings TreeSettings { get; }

    public ScoutTrees Trees { get; }

    public ScoutArtifacts Artifacts => Roots.Artifacts.Length > 0
        ? new ScoutArtifacts(Roots.Artifacts)
        : throw new InvalidOperationException("this verb was loaded without an artefact root and writes none");

    public static string Connection(CommandLine command) => command.Value("db", Environment.GetEnvironmentVariable("BENCH_DB") ?? string.Empty);

    public static string ArtifactRootOf(CommandLine command) =>
        command.Value("artifact-root", Environment.GetEnvironmentVariable("BENCH_ARTIFACT_ROOT") ?? string.Empty);

    public static string CheckoutRootOf(CommandLine command) => command.Value("checkout-root", RunCommand.DefaultCheckoutRoot);

    public static string TreesRootOf(CommandLine command) => command.Value("trees-root", ScoutRoots.DefaultTreesRoot(CheckoutRootOf(command)));

    public static string RepositoryUrlOf(CommandLine command) => command.Value("repo-url-template", ScoutTreeSettings.GitHub);

    /// <summary><c>--plugin-cache</c> and <c>--claude-settings</c>, defaulting to this machine's — the settings file is the settings pin AND
    /// the file the grant guard reads.</summary>
    public static ScoutPinSources PinSourcesOf(CommandLine command) => new(
        command.Value("plugin-cache", ScoutPinSources.Machine.PluginCache),
        command.Has("claude-settings") ? [command.Value("claude-settings")] : ScoutPinSources.Machine.SettingsFiles);

    /// <summary>How quiet a claim must be before the entry step asks whether its owner is gone — zero by default: ownership decides.</summary>
    public static TimeSpan StaleAfter(CommandLine command) => TimeSpan.FromMinutes(command.Int("stale-after-minutes", 0));

    /// <summary>The roots (with the grant guard), then the database. Without a database (<c>tasks</c>) only the roots are checked.</summary>
    public static async Task<(int Code, ScoutRootSet? Roots)> RootsAsync(CommandLine command, bool artifactsRequired, TextWriter error, CancellationToken cancellationToken)
    {
        if (artifactsRequired && ArtifactRootOf(command).Length == 0)
        {
            return (GateRunCommand.Refuse(error, ExitCodes.Configuration, "the scouts need an artefact root outside git — pass --artifact-root or set BENCH_ARTIFACT_ROOT"), null);
        }

        var roots = await ScoutRoots.CheckAsync(artifactsRequired ? ArtifactRootOf(command) : string.Empty, TreesRootOf(command), CheckoutRootOf(command), PinSourcesOf(command).SettingsFiles, cancellationToken);

        return roots switch
        {
            Outcome<ScoutRootSet>.Ok ok => (ExitCodes.Pass, ok.Value),
            Outcome<ScoutRootSet>.Fail fail => (GateRunCommand.Refuse(error, ExitCodes.Configuration, fail.Reason), null),
            _ => (ExitCodes.Configuration, null),
        };
    }

    public static async Task<(int Code, ScoutsInputs? Inputs)> LoadAsync(CommandLine command, bool artifactsRequired, TextWriter error, CancellationToken cancellationToken)
    {
        if (Connection(command).Length == 0)
        {
            return (GateRunCommand.Refuse(error, ExitCodes.Configuration, "the scouts need the database — pass --db or set BENCH_DB"), null);
        }

        var (code, roots) = await RootsAsync(command, artifactsRequired, error, cancellationToken);

        return roots is null ? (code, null) : await MigratedAsync(command, roots, error, cancellationToken);
    }

    private static async Task<(int Code, ScoutsInputs? Inputs)> MigratedAsync(CommandLine command, ScoutRootSet roots, TextWriter error, CancellationToken cancellationToken)
    {
        var db = ProbesInputs.Context(Connection(command));

        try
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is Npgsql.NpgsqlException or TimeoutException)
        {
            await db.DisposeAsync();
            return (GateRunCommand.Refuse(error, ExitCodes.Environment, $"the database is unreachable — {ex.Message.Split('\n')[0]}"), null);
        }

        var logs = LoggerFactory.Create(builder => builder.AddSerilog(CliLogging.Start(), dispose: false));
        return (ExitCodes.Pass, new ScoutsInputs(db, roots, PinSourcesOf(command), RepositoryUrlOf(command), logs));
    }

    /// <summary>The claude CLI on this machine: <c>--claude-exe</c> (a path, or a bare word looked up on PATH), else <c>claude</c>.</summary>
    public static Outcome<string> Executable(CommandLine command, ScoutVerbServices services) => Executable(command, services, "claude-exe", "claude");

    /// <summary>A CLI on this machine named by <paramref name="flag"/> (a path, or a bare word looked up on PATH), else <paramref name="word"/> —
    /// the claude CLI for the cells, the codex CLI for <c>assess</c>.</summary>
    public static Outcome<string> Executable(CommandLine command, ScoutVerbServices services, string flag, string word)
    {
        var value = command.Value(flag, word);

        return !Path.IsPathRooted(value) && value.IndexOfAny(['/', '\\', ':']) < 0
            ? services.ResolveWord(value)
            : File.Exists(value) ? Outcome<string>.Success(Path.GetFullPath(value)) : Outcome<string>.Failure($"--{flag} {value} is not there");
    }

    public ScoutRecoveryStep Recovery() =>
        new(Store, Artifacts, new OwnerLiveness(WorkerLiveness.ThisHost, WorkerLiveness.ProcessIsAlive));

    public ScoutCampaign Campaign(string executable, PriceTable prices, ScoutVerbServices services)
    {
        var runner = new CliScoutRunner(
            new CliAgentRuntime(Logs.CreateLogger<CliAgentRuntime>()), Trees, Artifacts, Store, services.Checkpoints,
            new CliScoutSettings(executable, prices, ParentEnvironment()) { TestChildVariables = services.TestChildVariables },
            Logs.CreateLogger<CliScoutRunner>());

        return new ScoutCampaign(Store, runner, new ScoutPinReader(new ProductPinReader(), PinSources), services.Checkpoints);
    }

    public static IReadOnlyDictionary<string, string> ParentEnvironment() =>
        Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => e.Value as string ?? string.Empty, StringComparer.Ordinal);

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        Logs.Dispose();
    }
}
