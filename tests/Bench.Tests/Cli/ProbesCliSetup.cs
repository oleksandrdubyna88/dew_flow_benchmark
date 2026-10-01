using System.Text.Json.Nodes;
using Bench.Application.Probes;
using Bench.Application.Registry;
using Bench.Cli;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Probes;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Infrastructure;
using Bench.Tests.Probes.Driver;

namespace Bench.Tests.Cli;

/// <summary>Everything <c>bench probes</c> is pointed at in a test: a fresh database, an artefact root and a SEPARATE work root, a
/// subjects file whose subjects are fake CLIs (one <see cref="FakeCli"/> each, keyed by its model id), and the verb's outside world
/// as values — a scripted oracle that counts its calls, the executable references as a dictionary (no process-wide variable), a
/// codex config reader that can be made to fail. The verbs run through <see cref="ProbesCommand.RunAsync(CommandLine, ProbeVerbServices, TextWriter, TextWriter, CancellationToken)"/>
/// exactly as <c>bench probes …</c> parses them.</summary>
internal sealed class ProbesCliSetup : IAsyncDisposable
{
    private readonly Dictionary<string, FakeCli> _fakes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _references = new(StringComparer.Ordinal);
    private readonly JsonArray _subjects = [];

    private ProbesCliSetup(string connection, TempRoot root)
    {
        Connection = connection;
        Root = root;
        WorkRoot = root.Sibling("work");
        SubjectsFile = Path.Combine(root.Sibling("subjects"), "subjects.json");
        Oracle = new ScriptedOracle();
    }

    public string Connection { get; }

    public TempRoot Root { get; }

    public string ArtifactRoot => Root.Path;

    public string WorkRoot { get; }

    public string SubjectsFile { get; }

    public ScriptedOracle Oracle { get; }

    public Func<Outcome<IReadOnlyList<string>>> CodexServers { get; set; } = static () => Outcome<IReadOnlyList<string>>.Success([]);

    public static async Task<ProbesCliSetup> StartAsync(PostgresFixture postgres) =>
        new(await postgres.NewDatabaseAsync($"probes_cli_{Guid.NewGuid():N}"), GateStoreFixtures.NewRoot());

    /// <summary>A CLI subject on <paramref name="runtime"/> answered by a fresh fake, its reference resolving to the fake's apphost; a claude
    /// subject under <paramref name="confinement"/> (the deny list unless the test says otherwise — S2b).</summary>
    public FakeCli AddSubject(string id, string runtime, JsonObject? script = null, bool resolvable = true, string confinement = "")
    {
        var fake = new FakeCli(script);
        var reference = "BENCH_TEST_" + id.Replace('-', '_').ToUpperInvariant();
        _fakes[id] = fake;
        var entry = new JsonObject { ["id"] = id, ["runtime"] = runtime, ["model"] = fake.Model, ["executableRef"] = reference };
        if (runtime == "claude")
        {
            entry["confinement"] = confinement.Length > 0 ? confinement : "denylist";
        }

        _subjects.Add(entry);

        if (resolvable)
        {
            _references[reference] = FakeCli.Executable;
        }

        WriteSubjects();
        return fake;
    }

    public FakeCli Fake(string id) => _fakes[id];

    public ProbeVerbServices Services() =>
        new(Oracle, new DictionaryReferences(_references), word => Outcome<string>.Failure($"'{word}' is not on PATH in this test"), CodexServers, new NoKey());

    /// <summary>The verb, with <c>--db</c>, <c>--artifact-root</c> and <c>--work-root</c> filled in unless the test passes its own.</summary>
    public async Task<(int Code, string Output, string Error)> RunAsync(params string[] args)
    {
        var withRoots = args.Contains("--db") ? args : [.. args, "--db", Connection];
        withRoots = withRoots.Contains("--artifact-root") ? withRoots : [.. withRoots, "--artifact-root", ArtifactRoot];
        withRoots = withRoots.Contains("--work-root") ? withRoots : [.. withRoots, "--work-root", WorkRoot];

        var output = new StringWriter();
        var error = new StringWriter();
        var code = await ProbesCommand.RunAsync(CommandLine.Parse(["probes", .. withRoots]), Services(), output, error, Xunit.TestContext.Current.CancellationToken);

        return (code, output.ToString(), error.ToString());
    }

    /// <summary><c>probes run</c> over the subjects file, with the given probes and repeats.</summary>
    public Task<(int Code, string Output, string Error)> PlanAndRunAsync(string probes, int repeats, params string[] extra) =>
        RunAsync(["run", "--subjects-file", SubjectsFile, "--probes", probes, "--repeats", repeats.ToString(System.Globalization.CultureInfo.InvariantCulture), .. extra]);

    public static Guid RunIdOf(string output) => Guid.Parse(output.Split("probe run ")[1][..36]);

    public PostgresProbeStore NewStore() => new(PostgresFixture.Context(Connection), TimeProvider.System);

    /// <summary>A directory a killed attempt left behind under the work root: <c>probes/&lt;run&gt;/&lt;cell&gt;/g1/a1/cwd</c>.</summary>
    public string PlantFixture(Guid runId, Guid cellId)
    {
        var path = Path.Combine(WorkRoot, ProbePaths.Folder, runId.ToString("D"), cellId.ToString("D"), "g1", "a1", ProbePaths.CwdFolder);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, ProbePaths.InsideFile), "IN-stranded\n");
        return Path.Combine(WorkRoot, ProbePaths.Folder, runId.ToString("D"), cellId.ToString("D"));
    }

    private void WriteSubjects()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SubjectsFile)!);
        File.WriteAllText(SubjectsFile, new JsonObject { ["subjects"] = _subjects.DeepClone() }.ToJsonString());
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var fake in _fakes.Values)
        {
            fake.Dispose();
        }

        Root.Dispose();
        TryDelete(WorkRoot);
        TryDelete(Path.GetDirectoryName(SubjectsFile)!);

        await Task.CompletedTask;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A child still closing a file; the temp folder is the operating system's to clean.
        }
    }

    /// <summary>An oracle whose answer the test sets and whose calls it counts — "resume never calls it" is a count of zero.</summary>
    internal sealed class ScriptedOracle : IProbeOracle
    {
        private int _calls;

        public Outcome<ProbeOracle> Answer { get; set; } = ProbeOracle.Parse("0.52.0", OracleSource.Registry);

        public int Calls => _calls;

        public Task<Outcome<ProbeOracle>> LatestAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(Answer);
        }
    }

    private sealed class DictionaryReferences(IReadOnlyDictionary<string, string> values) : ISecretSource
    {
        public Outcome<string> Resolve(string reference) =>
            values.TryGetValue(reference, out var value)
                ? Outcome<string>.Success(value)
                : Outcome<string>.Failure($"the environment variable '{reference}' is unset on this machine");
    }

    private sealed class NoKey : IProbeSecrets
    {
        public Outcome<SecretValue> CredsKey() => Outcome<SecretValue>.Failure("no vault key in this test");
    }
}
