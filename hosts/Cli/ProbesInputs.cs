using Bench.Application;
using Bench.Application.Probes;
using Bench.Application.Registry;
using Bench.Domain;
using Bench.Domain.Probes;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Models;
using Bench.Infrastructure.Persistence;
using Bench.Infrastructure.Probes;
using Bench.Infrastructure.Process;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Bench.Cli;

/// <summary>What the probe verbs reach outside the process through — the oracle, the references, the CLI word lookup, the codex
/// config, the product's vault key — as one value, so a test hands in a counting oracle and a dictionary of references instead of
/// setting process-wide variables and reaching the network.</summary>
public sealed record ProbeVerbServices(
    IProbeOracle Oracle,
    ISecretSource References,
    Func<string, Outcome<string>> ResolveWord,
    Func<Outcome<IReadOnlyList<string>>> CodexServers,
    IProbeSecrets ApiKey)
{
    /// <summary>This machine: the npm registry, the environment, <c>PATH</c>, <c>$CODEX_HOME/config.toml</c>, coai's settings.</summary>
    public static ProbeVerbServices Machine(HttpClient http) => new(
        new NpmRegistryOracle(http), new EnvironmentSecrets(), CliExecutable.OnThisMachine, CodexMcpServers.Declared,
        new CoaiSettingsProbeSecrets(GateSecrets.DefaultCoaiSettingsFile));
}

/// <summary>What every probe verb that measures stands on, loaded and refused in one place, in the order a person fixes things:
/// the roots (4 — inside a git checkout, or overlapping), the database (3 — unreachable; migrated on entry, as the gate's driver
/// does). Each lane gets its OWN database context (a context is not thread-safe), as <see cref="GateCliInputs.Lane"/> does.</summary>
public sealed class ProbesInputs : IAsyncDisposable
{
    private readonly string _connection;
    private readonly BenchDbContext _db;
    private readonly List<BenchDbContext> _laneContexts = [];

    private ProbesInputs(string connection, BenchDbContext db, string artifactRoot, string workRoot, ILoggerFactory logs)
    {
        _connection = connection;
        _db = db;
        ArtifactRoot = artifactRoot;
        WorkRoot = workRoot;
        Logs = logs;
        Store = new PostgresProbeStore(db, TimeProvider.System);
        Reads = new PostgresProbeReads(db);
        Fixtures = new ProbeFixtures(workRoot);
    }

    /// <summary>The artefact root, or empty for a verb that writes none (<c>sweep</c> without <c>--artifact-root</c>).</summary>
    public string ArtifactRoot { get; }

    public string WorkRoot { get; }

    public ILoggerFactory Logs { get; }

    public PostgresProbeStore Store { get; }

    public PostgresProbeReads Reads { get; }

    public ProbeFixtures Fixtures { get; }

    /// <summary>The artefact adapter — only for a verb that loaded with an artefact root (every verb but a root-less sweep).</summary>
    public ProbeArtifacts Artifacts => ArtifactRoot.Length > 0
        ? new ProbeArtifacts(ArtifactRoot)
        : throw new InvalidOperationException("this verb was loaded without an artefact root and writes none");

    public static string Connection(CommandLine command) =>
        command.Value("db", Environment.GetEnvironmentVariable("BENCH_DB") ?? string.Empty);

    /// <summary><c>--artifact-root</c>, or <c>BENCH_ARTIFACT_ROOT</c> — so the copyable <c>rerun</c> command the report prints runs
    /// as printed on a machine that sets it.</summary>
    public static string ArtifactRootOf(CommandLine command) =>
        command.Value("artifact-root", Environment.GetEnvironmentVariable("BENCH_ARTIFACT_ROOT") ?? string.Empty);

    public static string WorkRootOf(CommandLine command) => command.Value("work-root", ProbeRoots.DefaultWorkRoot);

    /// <summary>How quiet a claim must be before the entry step asks whether its owner is gone. Zero by default, as the gate's
    /// resume and sweep do: ownership decides — a dead pid on this host is handed back at once, a live owner and another host's
    /// claim never — so a resume right after a crash measures the cell the crash stranded.</summary>
    public static TimeSpan StaleAfter(CommandLine command) => TimeSpan.FromMinutes(command.Int("stale-after-minutes", 0));

    public static BenchDbContext Context(string connection) =>
        new(new DbContextOptionsBuilder<BenchDbContext>().UseNpgsql(connection).Options);

    /// <param name="artifactsRequired">False only for <c>sweep</c>, which deletes fixtures and writes no artefact.</param>
    public static async Task<(int Code, ProbesInputs? Inputs)> LoadAsync(
        CommandLine command, bool artifactsRequired, TextWriter error, CancellationToken cancellationToken)
    {
        var flags = (Connection(command).Length > 0, ArtifactRootOf(command).Length > 0 || !artifactsRequired) switch
        {
            (false, _) => "the probes need the database — pass --db or set BENCH_DB",
            (_, false) => "the probes need an artefact root outside git — pass --artifact-root or set BENCH_ARTIFACT_ROOT",
            _ => string.Empty,
        };

        if (flags.Length > 0)
        {
            return (GateRunCommand.Refuse(error, ExitCodes.Configuration, flags), null);
        }

        var roots = ArtifactRootOf(command).Length > 0
            ? ProbeRoots.Check(ArtifactRootOf(command), WorkRootOf(command))
            : ProbeRoots.Work(WorkRootOf(command)).Match(work => Outcome<(string, string)>.Success((string.Empty, work)), Outcome<(string, string)>.Failure);

        return roots switch
        {
            Outcome<(string Artifacts, string Work)>.Ok ok => await MigratedAsync(Connection(command), ok.Value.Artifacts, ok.Value.Work, error, cancellationToken),
            Outcome<(string Artifacts, string Work)>.Fail fail => (GateRunCommand.Refuse(error, ExitCodes.Configuration, fail.Reason), null),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    private static async Task<(int Code, ProbesInputs? Inputs)> MigratedAsync(
        string connection, string artifactRoot, string workRoot, TextWriter error, CancellationToken cancellationToken)
    {
        var db = Context(connection);

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
        return (ExitCodes.Pass, new ProbesInputs(connection, db, artifactRoot, workRoot, logs));
    }

    /// <summary>The run, or the exit code and the reason it cannot be measured: a run id nothing knows (4); a pruned run (4) — its
    /// artefacts are gone, so a new generation would sit beside verdicts nobody can audit any more.</summary>
    public async Task<Outcome<ProbeRun>> OpenRunAsync(Guid runId, CancellationToken cancellationToken) =>
        await Store.LoadAsync(runId, cancellationToken) switch
        {
            Outcome<ProbeRun>.Ok { Value.ArtifactsPruned: true } => Outcome<ProbeRun>.Failure(
                $"probe run {runId} was pruned — its artefacts are gone and nothing more is measured under it; plan a new run"),
            var loaded => loaded,
        };

    /// <summary>Every subject's executable resolved on THIS machine, before anything is claimed: the reference's value, and a bare
    /// word (<c>claude</c>) looked up on <c>PATH</c> as a shell would; and, when a codex subject is among them, the codex config
    /// its MCP servers are switched off from. Each refusal names the subject and the variable — a configuration fact (4).</summary>
    public static Outcome<IReadOnlyDictionary<string, string>> Executables(IReadOnlyList<ProbeSubject> subjects, ProbeVerbServices services)
    {
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var subject in subjects)
        {
            var executable = services.References.Resolve(subject.ExecutableRef).Match(value => Executable(value, services), Outcome<string>.Failure);

            if (executable is Outcome<string>.Fail fail)
            {
                return Outcome<IReadOnlyDictionary<string, string>>.Failure($"subject '{subject.Id}' cannot run on this machine — {subject.ExecutableRef}: {fail.Reason}");
            }

            resolved[subject.Id.Value] = ((Outcome<string>.Ok)executable).Value;
        }

        return CodexReady(subjects, services) is { Length: > 0 } refusal
            ? Outcome<IReadOnlyDictionary<string, string>>.Failure(refusal)
            : Outcome<IReadOnlyDictionary<string, string>>.Success(resolved);
    }

    private static Outcome<string> Executable(string value, ProbeVerbServices services) =>
        (IsBareWord(value), File.Exists(value)) switch
        {
            (true, _) => services.ResolveWord(value),
            (_, true) => Outcome<string>.Success(Path.GetFullPath(value)),
            _ => Outcome<string>.Failure("the variable names a file that is not there"),
        };

    private static bool IsBareWord(string value) => !Path.IsPathRooted(value) && value.IndexOfAny(['/', '\\', ':']) < 0;

    /// <summary>A codex launch switches off every MCP server its config declares; a config that exists and cannot be read would
    /// launch them — refused here, before anything is planned or claimed, rather than as one failed cell after another.</summary>
    private static string CodexReady(IReadOnlyList<ProbeSubject> subjects, ProbeVerbServices services) =>
        subjects.FirstOrDefault(s => s.Runtime == ProbeRuntime.Codex) is { } codex && services.CodexServers() is Outcome<IReadOnlyList<string>>.Fail unreadable
            ? $"subject '{codex.Id}' cannot launch with its MCP servers off — {unreadable.Reason}"
            : string.Empty;

    /// <summary>The entry step every verb runs before it claims (S2's <see cref="ProbeCampaign.PrepareAsync"/>).</summary>
    public ProbeCampaign Campaign() =>
        new(new ProductPinReader(), Fixtures, new LegDrain(Logs.CreateLogger<LegDrain>()), TimeProvider.System,
            new OwnerLiveness(WorkerLiveness.ThisHost, WorkerLiveness.ProcessIsAlive));

    /// <summary>One subject's lane: its own context and store, the CLI runner or the product runner by runtime.</summary>
    public ProbeLane Lane(ProbeSubject subject, IReadOnlyDictionary<string, string> executables, TimeSpan wall, ProbeVerbServices services)
    {
        var db = Context(_connection);
        lock (_laneContexts)
        {
            _laneContexts.Add(db);
        }

        IProbeRunner runner = subject.Runtime == ProbeRuntime.Api
            ? new CoaiApiProbeRunner(Artifacts, services.ApiKey, new CoaiApiProbeSettings(executables[subject.Id.Value], ParentEnvironment(), wall), Logs.CreateLogger<CoaiApiProbeRunner>())
            : new CliProbeRunner(new CliAgentRuntime(Logs.CreateLogger<CliAgentRuntime>()), Fixtures, Artifacts, new CliProbeSettings(executables, wall, ParentEnvironment()), Logs.CreateLogger<CliProbeRunner>());

        return new ProbeLane(subject, new PostgresProbeStore(db, TimeProvider.System), runner);
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

    private static IReadOnlyDictionary<string, string> ParentEnvironment() =>
        Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => e.Value as string ?? string.Empty, StringComparer.Ordinal);
}
