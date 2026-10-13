using Bench.Application.Probes;
using Bench.Application.Scouts;
using Bench.Domain;
using Bench.Domain.Gate;
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
using Microsoft.Extensions.Logging.Abstractions;
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

/// <summary>A scout launch outside a run (S5): the artefacts the files live under, the pin sources with the pinned settings file named, the
/// three launch files, and the local pins as they stand.</summary>
public sealed record ScoutLaunchContext(ScoutArtifacts Artifacts, ScoutPinSources Pins, ScoutLaunchFiles Files, ScoutLocalPins Local);

/// <summary>What every scout verb that touches the store stands on, loaded and refused in one place, in the order a person fixes things:
/// the flags (4), the roots and the grant guard (4 — <see cref="ScoutRoots"/>), the database (3 — unreachable; migrated on entry, as the
/// probes' driver does). One context: a scout campaign runs one cell at a time.</summary>
public sealed class ScoutsInputs : IAsyncDisposable
{
    private readonly BenchDbContext _db;

    private ScoutsInputs(CommandLine command, BenchDbContext db, ScoutRootSet roots, ScoutPinSources pins, string repositoryUrl, ILoggerFactory logs)
    {
        _db = db;
        Command = command;
        Roots = roots;
        // The pinned settings file lives under the artefact root (S5): a verb loaded with one reads the settings pin from there.
        PinSources = roots.Artifacts.Length > 0 ? pins with { PinnedSettingsFile = new ScoutArtifacts(roots.Artifacts).PinnedSettingsFile } : pins;
        Logs = logs;
        Store = new PostgresScoutStore(db, TimeProvider.System);
        Reads = new PostgresScoutReads(db);
        Verdicts = new PostgresScoutVerdicts(db);
        // Silent on purpose (S4, §3.13): the provider logs every url it mirrors, and a private task's url is one — its refusals come back
        // as values and are wrapped by label before any public writer sees them (ScoutWithheld).
        Mirrors = new GitCheckoutProvider(CheckoutCacheOptions.Under(roots.Mirrors), NullLogger<GitCheckoutProvider>.Instance);
        TreeSettings = new ScoutTreeSettings(roots.Trees, repositoryUrl, TimeSpan.FromMinutes(20));
        Trees = new ScoutTrees(TreeSettings, Mirrors, logs.CreateLogger<ScoutTrees>());
    }

    /// <summary>The command the verb was given — what a later decision (the campaign key, a deny-list) reads its flags from.</summary>
    public CommandLine Command { get; }

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

    /// <summary><c>--plugin-cache</c>, <c>--plugin-registry</c> and <c>--claude-settings</c>, defaulting to this machine's — the settings file
    /// is what the grant guard reads and what the pinned settings file is projected from (S5); the registry says which version folder of the
    /// cache is the pinned plugin directory.</summary>
    public static ScoutPinSources PinSourcesOf(CommandLine command) => new(
        command.Value("plugin-cache", ScoutPinSources.Machine.PluginCache),
        command.Has("claude-settings") ? [command.Value("claude-settings")] : ScoutPinSources.Machine.SettingsFiles)
    {
        PluginRegistry = command.Value("plugin-registry", ScoutPinSources.Machine.PluginRegistry),
    };

    /// <summary>What a scout launch outside a run (<c>alias-check</c>, <c>access-check</c>) stands on, resolved in the order a person fixes
    /// things (every refusal is configuration, 4): the pinned settings file written once or verified (<see cref="ScoutArtifacts.PinnedSettings"/>),
    /// the pinned plugin directory, the empty MCP config, then the local pins read with the CLI binary — the same launch files a cell gets.</summary>
    public static async Task<Outcome<ScoutLaunchContext>> LaunchContextAsync(CommandLine command, ScoutRootSet roots, string executable, CancellationToken cancellationToken)
    {
        var artifacts = new ScoutArtifacts(roots.Artifacts);
        var pins = PinSourcesOf(command) with { PinnedSettingsFile = artifacts.PinnedSettingsFile };
        var settings = artifacts.PinnedSettings(pins.SettingsFiles);
        var plugin = settings is Outcome<string>.Ok ? ScoutPluginDirectory.Resolve(pins) : Outcome<string>.Failure(string.Empty);
        var mcp = plugin is Outcome<string>.Ok ? artifacts.EmptyMcpConfig() : Outcome<string>.Failure(string.Empty);

        if ((settings, plugin, mcp) is not (Outcome<string>.Ok { Value: var pinned }, Outcome<string>.Ok { Value: var dir }, Outcome<string>.Ok { Value: var empty }))
        {
            return Outcome<ScoutLaunchContext>.Failure(new[] { settings, plugin, mcp }.OfType<Outcome<string>.Fail>().First().Reason);
        }

        return (await new ScoutPinReader(new ProductPinReader(), pins).ReadAsync(executable, cancellationToken)).Match(
            local => Outcome<ScoutLaunchContext>.Success(new ScoutLaunchContext(artifacts, pins, new ScoutLaunchFiles(empty, dir, pinned), local)),
            reason => Outcome<ScoutLaunchContext>.Failure($"the pins could not be read — {reason}"));
    }

    /// <summary>How quiet a claim must be before the entry step asks whether its owner is gone — zero by default: ownership decides.</summary>
    public static TimeSpan StaleAfter(CommandLine command) => TimeSpan.FromMinutes(command.Int("stale-after-minutes", 0));

    public const string DenyListVariable = "BENCH_SCOUT_DENYLIST";

    /// <summary>The operator's local deny-list (§3.13) — never committed; none when neither flag nor variable names a file. A path that IS
    /// named must load: a missing or unreadable file refuses the verb (4) before anything is scored or written, because a silently empty
    /// deny-list publishes exactly the names it exists to hide.</summary>
    public static Outcome<ScrubDenyList> DenyList(CommandLine command) =>
        command.Value("deny-list", Environment.GetEnvironmentVariable(DenyListVariable) ?? string.Empty) switch
        {
            { Length: 0 } => Outcome<ScrubDenyList>.Success(ScrubDenyList.None),
            var file => LoadDenyList(file, command.Has("deny-list") ? "--deny-list" : DenyListVariable),
        };

    /// <summary>Whether a deny-list is NAMED at all — the hygiene scan refuses to run without one.</summary>
    public static bool DenyListNamed(CommandLine command) => command.Value("deny-list", Environment.GetEnvironmentVariable(DenyListVariable) ?? string.Empty).Length > 0;

    private static Outcome<ScrubDenyList> LoadDenyList(string file, string source)
    {
        var refusal = $"the deny-list {file} (named by {source}) does not load — fix the path; nothing was scored or written, because an empty deny-list would publish the private names it exists to hide";

        try
        {
            return File.Exists(file) ? Outcome<ScrubDenyList>.Success(ScrubDenyList.Parse(File.ReadAllText(file))) : Outcome<ScrubDenyList>.Failure(refusal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome<ScrubDenyList>.Failure($"{refusal} ({ex.Message})");
        }
    }

    /// <summary>The campaign key (S4): named by <c>--campaign-key</c> or <c>BENCH_SCOUT_CAMPAIGN_KEY</c>, placed outside every checkout, the trees
    /// root, the mirror root and every granted directory, and PRESENT — a missing key is a refusal (4) that creates nothing; only
    /// <c>bench scouts key init</c> creates one.</summary>
    public static Outcome<FileHashKey> CampaignKey(CommandLine command)
    {
        var path = ScoutCampaignKey.PathOf(command.Value("campaign-key"));
        var refusal = ScoutCampaignKey.Refusal(path, TreesRootOf(command), CheckoutRootOf(command), PinSourcesOf(command).SettingsFiles);

        return refusal.Length > 0 ? Outcome<FileHashKey>.Failure(refusal) : ScoutCampaignKey.Read(path);
    }

    /// <summary>The committer every private commitment of <paramref name="campaign"/> is made with — the key, and the campaign's seed string,
    /// which must be named.</summary>
    public static Outcome<ScoutCommitter> Committer(CommandLine command, string campaign) =>
        campaign.Trim().Length == 0
            ? Outcome<ScoutCommitter>.Failure("a private task's commitments are bound to the campaign's seed string, and none is named — pass --campaign <seed string> (tasks.json carries it from then on)")
            : CampaignKey(command).Match(key => Outcome<ScoutCommitter>.Success(new ScoutCommitter(key, campaign)), Outcome<ScoutCommitter>.Failure);

    /// <summary>A LOCAL-only file's path (the private tasks file, the hand-check companion): the flag's value, resolved, refused inside any git
    /// checkout — a file of private plaintext is one <c>git add</c> from a public repository.</summary>
    public static Outcome<string> LocalOnlyFile(string flag, string value)
    {
        if (value.Length == 0)
        {
            return Outcome<string>.Failure($"{flag} <file> is needed — a private task's detail lives in a local file that never enters git");
        }

        return ArtifactContainment.Resolve(value).Match(
            full => FileSystemGateArtifactStore.GitCheckoutAbove(Path.GetDirectoryName(full) ?? full) is { Length: > 0 } checkout
                ? Outcome<string>.Failure($"{flag} {full} lies inside the git checkout at {checkout} — a file of private plaintext never sits where a 'git add' can reach it; choose a path outside every repository")
                : Outcome<string>.Success(full),
            reason => Outcome<string>.Failure($"{flag} {value} cannot be resolved — {reason}"));
    }

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
        return (ExitCodes.Pass, new ScoutsInputs(command, db, roots, PinSourcesOf(command), RepositoryUrlOf(command), logs));
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
            new CliScoutSettings(executable, prices, ParentEnvironment(), PinSources) { TestChildVariables = services.TestChildVariables },
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
