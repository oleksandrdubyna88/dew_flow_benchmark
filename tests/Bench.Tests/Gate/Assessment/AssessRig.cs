using System.Text;
using System.Text.Json.Nodes;
using Bench.Application;
using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Registry;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Cli;
using Bench.Tests.Infrastructure;
using static Bench.Tests.Infrastructure.GateStoreFixtures;

namespace Bench.Tests.Gate.Assessment;

/// <summary>A real store, a real artefact root and a SCRIPTED assessor: settled cells whose findings.jsonl was committed
/// through the artefact store exactly as the driver commits it, then an assessment pass over them. The scripted
/// assessor answers from the ids it finds in the prompt — so what it was asked is what the test can read back.</summary>
internal sealed class AssessRig : IDisposable
{
    public const string NeutralCheckout = "C:/bench/checkouts/worktrees/5f1e/2222222222222222222222222222222222222222";

    private readonly TempRoot _root = NewRoot();

    private AssessRig(PostgresFixture postgres, GateSuite suite, GateReviewer reviewer)
    {
        Postgres = postgres;
        Suite = suite;
        Reviewer = reviewer;
        Artifacts = Store(_root);
        Files = new FileSystemGateAssessmentFiles(_root.Path);
        Rubrics = GateRubrics.Load(Path.Combine(Repository.Root, "prompts")).Ok();
    }

    public PostgresFixture Postgres { get; }

    public GateSuite Suite { get; }

    public GateReviewer Reviewer { get; }

    public FileSystemGateArtifactStore Artifacts { get; }

    public FileSystemGateAssessmentFiles Files { get; }

    public IReadOnlyList<LoadedRubric> Rubrics { get; }

    public LoadedRubric Strict => Rubrics[0];

    public RubricCatalog Catalog => GateRubrics.Catalog(Rubrics);

    public Guid Campaign { get; private set; }

    public IReadOnlyList<Guid> Cells { get; private set; } = [];

    public string Root => _root.Path;

    public static GateReviewer Assessor(string model = "gpt-6-astra") =>
        GateReviewer.Create(
            GateReviewerId.Parse("codex-astra").Ok(),
            GateReviewerTests.Definition(model: model, runtime: ReviewerRuntime.Codex, endpoint: string.Empty, credsKeyRef: string.Empty, keyName: string.Empty).Ok(),
            Noon);

    /// <summary>A campaign of <paramref name="cells"/> settled cells over task cs2, each with <paramref name="findings"/> findings.</summary>
    public static async Task<AssessRig> SettledAsync(PostgresFixture postgres, int cells, int findings, string reviewerModel = "grok-4.7", CancellationToken ct = default)
    {
        var suite = GateSuiteFile.Parse(SuiteJson).Ok();
        var reviewer = GateReviewer.Create(GateReviewerId.Parse("grok-medium").Ok(), GateReviewerTests.Definition(model: reviewerModel).Ok(), Noon);
        var rig = new AssessRig(postgres, suite, reviewer);
        await rig.SettleAsync(cells, findings, ct);
        return rig;
    }

    public PostgresGateStore NewStore() => new(Postgres.NewContext(), TimeProvider.System);

    public PostgresGateVerdictStore NewVerdicts() => new(Postgres.NewContext(), TimeProvider.System);

    public GateAssessmentPass Pass(ICliAgentRuntime assessor, int seed = 5) =>
        new(NewStore(), Artifacts, NewVerdicts(), Files, new NeutralCheckouts(), new FindingAssessor(assessor, Files), TimeProvider.System, new Random(seed));

    public AssessmentRequest Request(GateReviewer? assessor = null, int batchSize = BatchSize.Max) =>
        new([Campaign], Suite, new AssessorLaunch(assessor ?? Assessor(), ModelRuntimeKind.CliCodex, "codex", TimeSpan.FromMinutes(1), Strict), Catalog,
            BatchSize.Of(batchSize).Ok(), Key, new Dictionary<string, GateReviewer> { [Reviewer.Id.Value] = Reviewer });

    public async Task<IReadOnlyList<GateVerdict>> VerdictsAsync() => await NewVerdicts().VerdictsAsync(Cells, Catalog, CancellationToken.None);

    public async Task<ModelTable> ReportAsync(IReadOnlyList<HandCheck>? checks = null)
    {
        var runs = await NewStore().FactsAsync(Campaign, CancellationToken.None);
        var input = new GateReportInput([.. Suite.Tasks.Select(t => t.Summary)], runs, await VerdictsAsync()) { HandChecks = checks ?? [] };

        return GateReport.PerModel(runs[0].Scope, Strict.Rubric, input);
    }

    private async Task SettleAsync(int count, int findings, CancellationToken ct)
    {
        var store = NewStore();
        var run = GateRun.Planned(Guid.CreateVersion7(), GateKind.Feature, Suite.Stamp, DataDirMode.Isolated, Noon);
        var planned = Enumerable.Range(0, count)
            .Select(i => GateCell.Pending(Guid.CreateVersion7(), run.Id, new GateMatrixCell(GateTaskId.Parse("cs2").Ok(), Reviewer.Id, Repeat: i + 1, Slot: i, Position: 0)))
            .ToList();
        (await store.PlanAsync(run, planned, ct)).Ok();

        var owner = Here();
        foreach (var _ in planned)
        {
            var cell = (await store.ClaimNextAsync(run.Id, owner, Pin(), ct)).Ok();
            var scope = ArtifactScope.Of(run, cell).Ok();
            (await Artifacts.BeginAttemptAsync(scope, ct)).Ok();

            var text = string.Concat(Enumerable.Range(0, findings).Select(i => AssessmentFixtures.FindingJson(i) + "\n"));
            var written = (await Artifacts.WriteAsync(scope, ArtifactClass.Findings, CellPaths.AttemptRoot(scope).Then("findings.jsonl").Ok(), Encoding.UTF8.GetBytes(text), ct)).Ok();
            (await store.RecordArtifactsAsync([written], ct)).Ok();
            (await store.SettleAsync(cell.Id, owner, Completed(findings), ct)).Ok();
        }

        Campaign = run.Id;
        Cells = [.. planned.Select(c => c.Id)];
    }

    public void Dispose() => _root.Dispose();

    public const string SuiteJson = """
        { "id": "gate-assess-test", "privateNames": ["contoso-orders"],
          "tasks": [ { "id": "cs2", "language": "C#", "gates": ["feature"], "repository": "C:/nowhere/cs2",
                       "base": "1111111111111111111111111111111111111111", "variantHead": "2222222222222222222222222222222222222222",
                       "planPath": "docs/plan.md", "epics": ["one"], "lessons": "",
                       "seeds": [ { "id": "cs2-S1", "file": "src/Orders/Export0.cs", "old": "a", "new": "b", "what": "planted",
                                    "trigger": "t", "mechanism": "m", "consequence": "c", "crossEpic": true },
                                  { "id": "cs2-S2", "file": "src/Orders/Export1.cs", "old": "a", "new": "b", "what": "planted",
                                    "trigger": "t", "mechanism": "m", "consequence": "c", "crossEpic": false } ] } ] }
        """;

    /// <summary>The read-only checkout at a path built from nothing but a url hash and a commit — the provider's shape.</summary>
    private sealed class NeutralCheckouts : IGateCheckouts
    {
        public Task<Outcome<string>> ReadOnlyAsync(GateTask task, CancellationToken cancellationToken) => Task.FromResult(Outcome<string>.Success(NeutralCheckout));

        public Task<Outcome<string>> EnsureAsync(Guid runId, GateTask task, CancellationToken cancellationToken) => throw new NotSupportedException("the assessor never uses a gate clone");

        public Task<Outcome<string>> CreateRefAsync(string clone, string branch, GateTask task, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<int> RemoveFinishedAsync(IReadOnlyCollection<Guid> finishedRuns, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

/// <summary>An assessor that answers from the prompt it was given: <paramref name="answer"/> maps the ids of the batch (in
/// prompt order) and the call number to the text it answers with. Every prompt is kept.</summary>
internal sealed class ScriptedAssessor(Func<IReadOnlyList<string>, int, string> answer) : ICliAgentRuntime
{
    public List<string> Prompts { get; } = [];

    public List<AgentAsk> Asks { get; } = [];

    public static ScriptedAssessor AllSupported() => new((ids, _) => AssessmentFixtures.Answer([.. ids.Select(id => AssessmentFixtures.AnswerRow(id))]));

    public static IReadOnlyList<string> IdsIn(string prompt) =>
        [.. prompt[(prompt.IndexOf("INPUT ROWS", StringComparison.Ordinal))..].Split('\n').Skip(1)
            .Select(l => JsonNode.Parse(l)!["id"]!.GetValue<string>())];

    public Task<Outcome<AgentAnswer>> AskAsync(AgentAsk ask, CancellationToken cancellationToken)
    {
        Prompts.Add(ask.Prompt);
        Asks.Add(ask);
        var text = answer(IdsIn(ask.Prompt), Prompts.Count);

        return Task.FromResult(text.Length == 0
            ? Outcome<AgentAnswer>.Failure("the codex agent exited 1: rate limited")
            : Outcome<AgentAnswer>.Success(new AgentAnswer(text, TimeSpan.FromSeconds(1), text.Length)));
    }
}
