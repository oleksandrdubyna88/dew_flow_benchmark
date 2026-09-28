using System.Security.Cryptography;
using System.Text;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Infrastructure;
using static Bench.Tests.Infrastructure.GateStoreFixtures;

namespace Bench.Tests.Gate;

/// <summary>One cell of a <see cref="PromptRig"/> campaign: its task and reviewer, the turn-1 prompt the product wrote
/// (<see cref="string.Empty"/> = no prompt file, as a CLI reviewer leaves), the pin it was claimed under, and whether its
/// session failed.</summary>
internal sealed record PromptCell(string Task, string Reviewer, string Prompt, ProductPin Pin, bool Failed = false, string LaterPrompt = "", string StoredHash = "")
{
    public static PromptCell Api(string prompt, string task = "cs2", string reviewer = "grok-medium") => new(task, reviewer, prompt, GateStoreFixtures.Pin());
}

/// <summary>A real store and a real artefact root holding campaigns whose cells committed their turn-1 prompt the way the
/// driver commits it (<c>answers/NN-api-…prompt</c>, class <see cref="ArtifactClass.Prompt"/>, the settlement's
/// <c>PromptHash</c> the file's SHA-256) — so the A/A verb and seed evidence read what a real run leaves on disk.</summary>
internal sealed class PromptRig : IDisposable
{
    private readonly TempRoot _root = NewRoot();

    public PromptRig(PostgresFixture postgres)
    {
        Postgres = postgres;
        Artifacts = Store(_root);
    }

    public PostgresFixture Postgres { get; }

    public FileSystemGateArtifactStore Artifacts { get; }

    public string Root => _root.Path;

    public PostgresGateStore NewStore() => new(Postgres.NewContext(), TimeProvider.System);

    /// <summary>The catalog rows the cells name: an API row per id, or a claude CLI row for an id starting <c>claude</c>.</summary>
    public async Task AddReviewersAsync(IEnumerable<string> ids, CancellationToken ct)
    {
        var catalog = new PostgresGateReviewerCatalog(Postgres.NewContext());
        var present = (await catalog.ListAsync(includeRetired: true, ct)).Select(r => r.Id.Value).ToHashSet(StringComparer.Ordinal);
        foreach (var id in ids.Distinct(StringComparer.Ordinal).Where(id => !present.Contains(id)))
        {
            var definition = id.StartsWith("claude", StringComparison.Ordinal)
                ? GateReviewerTests.Definition(model: "claude-fable-5-1", runtime: ReviewerRuntime.Claude, endpoint: string.Empty, credsKeyRef: string.Empty, keyName: string.Empty)
                : GateReviewerTests.Definition();
            (await catalog.AddAsync(GateReviewer.Create(GateReviewerId.Parse(id).Ok(), definition.Ok(), Noon), ct)).Ok();
        }
    }

    /// <summary>A campaign of <paramref name="cells"/>, each claimed, its prompt committed, settled; Finished unless told otherwise.
    /// Returns the campaign and its cell ids in the order given.</summary>
    public async Task<(Guid Campaign, IReadOnlyList<Guid> Cells)> CampaignAsync(
        IReadOnlyList<PromptCell> cells, string suiteStamp, CancellationToken ct, GateKind gate = GateKind.Feature, bool finish = true, int leavePending = 0)
    {
        await AddReviewersAsync(cells.Select(c => c.Reviewer), ct);
        var store = NewStore();
        var run = GateRun.Planned(Guid.CreateVersion7(), gate, suiteStamp, DataDirMode.Isolated, Noon);
        var planned = cells.Select((c, i) => GateCell.Pending(Guid.CreateVersion7(), run.Id,
            new GateMatrixCell(GateTaskId.Parse(c.Task).Ok(), GateReviewerId.Parse(c.Reviewer).Ok(), Repeat: i + 1, Slot: i, Position: 0))).ToList();
        (await store.PlanAsync(run, planned, ct)).Ok();

        var owner = Here();
        foreach (var spec in cells.Take(cells.Count - leavePending))
        {
            var cell = (await store.ClaimNextAsync(run.Id, owner, spec.Pin, ct)).Ok();
            (await store.SettleAsync(cell.Id, owner, await SettlementAsync(run, cell, cells[planned.FindIndex(p => p.Id == cell.Id)], store, ct), ct)).Ok();
        }

        await AdvanceAsync(store, run.Id, finish, ct);
        return (run.Id, [.. planned.Select(p => p.Id)]);
    }

    /// <summary>Overwrites a committed prompt on disk — the artefact changed under its ref, so read-and-verify refuses it.</summary>
    public async Task TamperAsync(Guid campaign, Guid cell, CancellationToken ct)
    {
        var prompt = (await NewStore().ArtifactsAsync(campaign, ct)).Single(a => a.CellId == cell && a.Class == ArtifactClass.Prompt);
        await File.WriteAllTextAsync(Path.Combine(Root, prompt.Path.Value), "tampered after commit", ct);
    }

    public static string Sha(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private async Task<GateSettlement> SettlementAsync(GateRun run, GateCell cell, PromptCell spec, PostgresGateStore store, CancellationToken ct)
    {
        var scope = ArtifactScope.Of(run, cell).Ok();
        (await Artifacts.BeginAttemptAsync(scope, ct)).Ok();

        if (spec.Prompt.Length > 0)
        {
            var path = CellPaths.AttemptRoot(scope).Then("answers/01-api-FeatureReview-0a1b.prompt").Ok();
            var written = (await Artifacts.WriteAsync(scope, ArtifactClass.Prompt, path, Encoding.UTF8.GetBytes(spec.Prompt), ct)).Ok();
            (await store.RecordArtifactsAsync([written], ct)).Ok();
        }

        if (spec.LaterPrompt.Length > 0)
        {
            var later = CellPaths.AttemptRoot(scope).Then("answers/04-api-FeatureReview-9f9f.prompt").Ok();
            (await store.RecordArtifactsAsync([(await Artifacts.WriteAsync(scope, ArtifactClass.Prompt, later, Encoding.UTF8.GetBytes(spec.LaterPrompt), ct)).Ok()], ct)).Ok();
        }

        return spec.Failed
            ? new GateSettlement.Failed(new FailureCause(FailureKind.ProcessDied, "the product exited before its first turn"))
            : Completed() with { PromptHash = (spec.StoredHash.Length, spec.Prompt.Length) switch { ( > 0, _) => spec.StoredHash, (_, > 0) => Sha(spec.Prompt), _ => string.Empty } };
    }

    private static async Task AdvanceAsync(PostgresGateStore store, Guid run, bool finish, CancellationToken ct)
    {
        await store.AdvanceAsync(run, GateRunStatus.Running, ct);
        if (finish)
        {
            (await store.AdvanceAsync(run, GateRunStatus.Finished, ct)).Ok();
        }
    }

    public void Dispose() => _root.Dispose();
}
