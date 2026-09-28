using System.Text.Json.Nodes;
using Bench.Application;
using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Runs;
using Bench.Domain.Targets;
using Bench.Domain.Trace;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Git;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bench.Tests.Gate.Driver;

/// <summary>The whole driver, wired as the CLI wires it, against the fake product, a real Postgres database, a real
/// artefact root and a real git repository holding a seeded task — so every protocol and campaign test runs the product
/// path end to end. Only the product (the fake), the pin reader (scripted, so a test can move the product) and the secret
/// source (a test must not set process-wide variables) are stand-ins.</summary>
internal sealed class GateDriverRig : IAsyncDisposable
{
    public const string CredsKey = "sentinel-creds-key-rig-31";

    private readonly CancellationToken _ct;

    private GateDriverRig(PostgresFixture postgres, string connection, FakeCoai fake, DatedGitRepo repo, TempRoot root, GateTask task, CancellationToken ct)
    {
        Postgres = postgres;
        Connection = connection;
        Fake = fake;
        Repo = repo;
        Root = root;
        Task = task;
        _ct = ct;
        Artifacts = GateStoreFixtures.Store(root);
        Checkouts = new GateCloneCheckouts(
            new GitCheckoutProvider(CheckoutCacheOptions.Under(Path.Combine(root.Sibling("checkouts"))), NullLogger<GitCheckoutProvider>.Instance),
            root.Sibling("checkouts"));
    }

    public PostgresFixture Postgres { get; }

    public string Connection { get; }

    public FakeCoai Fake { get; }

    public DatedGitRepo Repo { get; }

    public TempRoot Root { get; }

    public GateTask Task { get; }

    public FileSystemGateArtifactStore Artifacts { get; }

    public GateCloneCheckouts Checkouts { get; }

    public ScriptedPins Pins { get; } = new(GateStoreFixtures.Pin('a'));

    /// <summary>The product binary the runner launches — the fake unless a live test names the real one.</summary>
    public string Product { get; set; } = FakeCoai.Executable;

    /// <summary>What the rig's references resolve to — a live test points a loopback vendor reference here.</summary>
    public IReadOnlyDictionary<string, string> References { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public static async Task<GateDriverRig> StartAsync(PostgresFixture postgres, JsonObject? script = null)
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        var connection = await postgres.NewDatabaseAsync($"gate_driver_{Guid.NewGuid():N}");
        var repo = new DatedGitRepo(ct);
        var @base = await repo.InitAsync(("src/Orders.cs", "class Orders {}"), ("base", "2026-09-01T10:00:00Z"));
        var head = await repo.CommitManyAsync([("src/Orders.cs", "class Orders { int x; }"), ("docs/plan.md", "# Plan\n\nEpic 1: orders.\n")], ("variant", "2026-09-01T11:00:00Z"));
        var task = GateTask.Of(
            GateTaskId.Parse("cs2").Ok(), "C#", HostedGates.All, false,
            GateCase.Of(CommitSha.Parse(@base).Ok(), CommitSha.Parse(head).Ok(), "docs/plan.md", "[{\"title\":\"Epic 1\",\"summary\":\"orders\"}]", "{\"pitfalls\":[\"none\"],\"blockers\":[\"none\"],\"findings\":[\"none\"]}").Ok(),
            [SeedSpec.Of("cs2-S1", "src/Orders.cs", "class Orders {}", "class Orders { int x; }", "a field", "t", "m", "c", false).Ok()],
            CloneLocation.Parse(repo.Root).Ok()).Ok();

        return new GateDriverRig(postgres, connection, new FakeCoai(script), repo, GateStoreFixtures.NewRoot(), task, ct);
    }

    /// <summary>A reviewer on the api runtime at a public (never contacted) vendor url — the fake product counts its
    /// reviews by that url.</summary>
    public static GateReviewer Reviewer(string id, string host) =>
        GateReviewer.Create(
            GateReviewerId.Parse(id).Ok(),
            ReviewerDefinition.Parse(
                ReviewerRuntime.Api, "model-" + id, ReviewerEndpoint.Parse($"https://{host}/v1").Ok(), "vendor", "BENCH_RIG_CREDS", string.Empty, string.Empty,
                ReviewerTransport.Parse("openai", "medium", 8192, 20, 3, 20, false).Ok(), ReviewerPrices.Unknown, HostedGates.All).Ok(),
            GateStoreFixtures.Noon);

    public PostgresGateStore NewStore() => new(PostgresFixture.Context(Connection), TimeProvider.System);

    /// <summary>Plans a run of <paramref name="gate"/> over the rig's task, the reviewers and the repeats.</summary>
    public async Task<(GateRun Run, IReadOnlyList<GateCell> Cells)> PlanAsync(
        GateKind gate, IReadOnlyList<GateReviewer> reviewers, int repeats, DataDirMode mode = DataDirMode.Isolated, bool allowProductChange = false)
    {
        var run = GateRun.Planned(Guid.CreateVersion7(), gate, "rig#000000000000", mode, DateTimeOffset.UtcNow) with { AllowProductChange = allowProductChange };
        var planned = GateMatrix.Plan([Task.Id], [.. reviewers.Select(r => r.Id)], repeats).Ok();
        var cells = planned.Select(c => GateCell.Pending(Guid.CreateVersion7(), run.Id, c)).ToList();
        (await NewStore().PlanAsync(run, cells, _ct)).Ok();

        return (run, cells);
    }

    public GateCellRunner Runner(IGateStore store, bool tap = false) => new(
        store, Artifacts, new McpStdioSessionFactory(), new RecordingTapFactory(), Checkouts, new RigSecrets(References), new FileGateAttemptFiles(), TimeProvider.System,
        new GateDriverSettings(Product, Artifacts.Root, tap, TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2),
            Fake.Environment(), PrivateNames.Of(["contoso-orders"])));

    public async Task<GateCampaignReport> CampaignAsync(GateRun run, IReadOnlyList<GateReviewer> reviewers, int parallel, int perEndpoint)
    {
        var inputs = new GateCampaignInputs(
            run, Pins.First, reviewers.ToDictionary(r => r.Id.Value), new Dictionary<string, GateTask> { [Task.Id.Value] = Task },
            GateRunSettings.With(new Dictionary<string, string>()).Ok(), GateStoreFixtures.Key, Product);

        var campaign = new GateCampaign(Pins, new LegDrain(NullLogger<LegDrain>.Instance), TimeProvider.System);
        var report = await campaign.RunAsync(
            inputs, new GateCampaignOptions(parallel, perEndpoint, DrainLimits.Default with { Backoff = TimeSpan.FromMilliseconds(20) }),
            n =>
            {
                var store = NewStore();
                return new GateLane(n, store, Runner(store), new LaneSlot());
            },
            _ct);

        return report.Ok();
    }

    public async ValueTask DisposeAsync()
    {
        Fake.Dispose();
        Repo.Dispose();
        Root.Dispose();

        foreach (var sibling in new[] { "checkouts", "cli-checkouts", "suite" }.Select(Root.Sibling).Where(Directory.Exists))
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(sibling, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(sibling, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A git process still closing a pack; the temp folder is the operating system's to clean.
            }
        }

        await System.Threading.Tasks.Task.CompletedTask;
    }

    /// <summary>The secret source for the rig: the vault key is a fixed sentinel, references resolve to nothing.</summary>
    private sealed class RigSecrets(IReadOnlyDictionary<string, string> references) : IGateSecrets
    {
        public Outcome<SecretValue> CredsKey(GateReviewer reviewer) => SecretValue.Of(GateDriverRig.CredsKey, "rig");

        public Outcome<ResolvedReferences> References(GateReviewer reviewer) => Outcome<ResolvedReferences>.Success(new ResolvedReferences(references));
    }
}

/// <summary>A pin reader a test can move: it answers <see cref="First"/> until <see cref="MoveTo"/> is called.</summary>
internal sealed class ScriptedPins(ProductPin first) : IProductPinReader
{
    private ProductPin _current = first;

    public ProductPin First { get; private set; } = first;

    public int Reads { get; private set; }

    public void MoveTo(ProductPin pin) => _current = pin;

    /// <summary>Pins the campaign itself to <paramref name="pin"/> — a live test measures the real binary's bytes.</summary>
    public void StartAt(ProductPin pin)
    {
        First = pin;
        _current = pin;
    }

    public Task<Outcome<ProductPin>> ReadAsync(string executable, CancellationToken cancellationToken)
    {
        Reads++;
        return System.Threading.Tasks.Task.FromResult(Outcome<ProductPin>.Success(_current));
    }
}
