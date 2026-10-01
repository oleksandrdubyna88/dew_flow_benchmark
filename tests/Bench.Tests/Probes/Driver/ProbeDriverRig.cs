using System.Text.Json.Nodes;
using Bench.Application;
using Bench.Application.Probes;
using Bench.Domain.Probes;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Models;
using Bench.Infrastructure.Persistence;
using Bench.Infrastructure.Probes;
using Bench.Infrastructure.Process;
using Bench.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bench.Tests.Probes.Driver;

/// <summary>The whole probe driver wired as the verbs will wire it (S3), against the fake CLI, a real Postgres database, a real
/// work root and a real artefact root — on the pattern of <see cref="Gate.Driver.GateDriverRig"/>. The pin reader is the REAL
/// <see cref="ProductPinReader"/>: the fake answers <c>--version</c>, so every claim carries the build that answered (D9).
/// One fake per subject, each with its own script.</summary>
internal sealed class ProbeDriverRig : IAsyncDisposable
{
    private readonly CancellationToken _ct;
    private readonly Dictionary<string, FakeCli> _fakes = new(StringComparer.Ordinal);
    private readonly List<ProbeSubject> _subjects = [];

    private ProbeDriverRig(PostgresFixture postgres, string connection, TempRoot root, CancellationToken ct)
    {
        Postgres = postgres;
        Connection = connection;
        Root = root;
        _ct = ct;
        WorkRoot = root.Sibling("work");
        Directory.CreateDirectory(WorkRoot);
        Fixtures = new ProbeFixtures(WorkRoot);
        Artifacts = new ProbeArtifacts(root.Path);
    }

    public PostgresFixture Postgres { get; }

    public string Connection { get; }

    public TempRoot Root { get; }

    public string WorkRoot { get; }

    public ProbeFixtures Fixtures { get; }

    public ProbeArtifacts Artifacts { get; }

    public TimeSpan Wall { get; set; } = TimeSpan.FromSeconds(60);

    public IReadOnlyList<ProbeSubject> Subjects => _subjects;

    public IReadOnlyDictionary<string, string> Executables => _fakes.ToDictionary(f => f.Key, _ => FakeCli.Executable, StringComparer.Ordinal);

    public static async Task<ProbeDriverRig> StartAsync(PostgresFixture postgres)
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        var connection = await postgres.NewDatabaseAsync($"probe_driver_{Guid.NewGuid():N}");

        return new ProbeDriverRig(postgres, connection, GateStoreFixtures.NewRoot(), ct);
    }

    /// <summary>A CLI subject on <paramref name="runtime"/> whose executable is the fake, scripted by <paramref name="script"/>.</summary>
    public ProbeSubject AddSubject(string id, string runtime, JsonObject? script = null)
    {
        var fake = new FakeCli(script);
        var subject = ProbeSubject.Parse(id, runtime, fake.Model, "BENCH_FAKE_CLI").Ok();
        _fakes[id] = fake;
        _subjects.Add(subject);

        return subject;
    }

    public FakeCli Fake(ProbeSubject subject) => _fakes[subject.Id.Value];

    public PostgresProbeStore NewStore() => new(PostgresFixture.Context(Connection), TimeProvider.System);

    public async Task<(ProbeRun Run, IReadOnlyList<ProbeCell> Cells)> PlanAsync(IReadOnlyList<ProbeKind> probes, int repeats)
    {
        var run = ProbeRun.Planned(Guid.CreateVersion7(), ProbeStoreFixtures.Oracle(), _subjects, repeats, DateTimeOffset.UtcNow).Ok();
        var cells = ProbeMatrix.Plan(probes, _subjects, repeats).Ok().Cells.Select(c => ProbeCell.Pending(Guid.CreateVersion7(), run.Id, c)).ToList();
        (await NewStore().PlanAsync(run, cells, _ct)).Ok();

        return (run, cells);
    }

    public CliProbeRunner Runner() => new(
        new CliAgentRuntime(NullLogger<CliAgentRuntime>.Instance), Fixtures, Artifacts, new CliProbeSettings(Executables, Wall), NullLogger<CliProbeRunner>.Instance);

    public ProbeCampaign Campaign() =>
        new(new ProductPinReader(), Fixtures, new LegDrain(NullLogger<LegDrain>.Instance), TimeProvider.System, new OwnerLiveness(WorkerLiveness.ThisHost, WorkerLiveness.ProcessIsAlive));

    public Task<ProbePrepareReport> PrepareAsync(ProbeRun run, TimeSpan? staleAfter = null) =>
        Campaign().PrepareAsync(NewStore(), run, staleAfter ?? ProbeCampaignOptions.DefaultStaleAfter, _ct);

    public async Task<ProbeCampaignReport> CampaignAsync(ProbeRun run, Action<string>? progress = null)
    {
        var options = new ProbeCampaignOptions(DrainLimits.Default with { Backoff = TimeSpan.FromMilliseconds(20) }, ProbeCampaignOptions.DefaultStaleAfter)
        {
            Progress = progress ?? (_ => { }),
        };

        var report = await Campaign().RunAsync(new ProbeCampaignInputs(run, Executables), options, subject => new ProbeLane(subject, NewStore(), Runner()), _ct);

        return report.Ok();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var fake in _fakes.Values)
        {
            fake.Dispose();
        }

        Root.Dispose();
        Gate.Driver.GateDriverRig.DeleteSiblings(Root);
        TryDelete(WorkRoot);

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
}
