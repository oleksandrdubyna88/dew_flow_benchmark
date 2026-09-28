using System.Text;
using System.Text.Json.Nodes;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Runs;
using Bench.Domain.Targets;
using Bench.Domain.Trace;

namespace Bench.Application.Gate;

/// <summary>A short sha of the product's own repository, resolved to the full commit — the suite stamp is over full shas,
/// because an abbreviation is ambiguous.</summary>
public interface ICommitResolver
{
    Task<Outcome<CommitSha>> ResolveAsync(string shortSha, CancellationToken cancellationToken);
}

/// <summary>One coai-bench file to import: where it sits (its folder is the campaign's LOCATION label) and its bytes.</summary>
public sealed record CoaiBenchFile(string Location, string Json);

public sealed record CoaiBenchImportRequest(
    IReadOnlyList<CoaiBenchFile> Files,
    FileHashKey Key,
    LoadedRubric Lenient,
    RubricCatalog Rubrics,
    PrivateNames PrivateNames);

public sealed record CoaiBenchImportReport(GateSuite Suite, ImportCounts Cells, int Verdicts, int VerdictsNew, int Unjudged, int FamilyMatched);

/// <summary>S5.3 — coai-bench <c>runs.json</c> files as plan and code cells. A campaign per (location, gate) — the folder
/// the file sits in, so a file that GROWS adds its new records to the same campaign (plan round, finding 9) and a byte copy
/// elsewhere adds nothing, because a cell is keyed by the RECORD (<c>arm|case|repeat|startedUtc|stage</c>), never the file.
/// The cases become a suite (full shas resolved in the product's repository; no seeds; plan and code hosted). An arm is a
/// vendor set with no model recorded, so no catalog row is invented. <c>Useful</c> yes / no is a lenient verdict under
/// <c>lenient-worth-v1</c>, by the record's judge; <c>unjudged</c> stays unassessed — no row at all.</summary>
public sealed class CoaiBenchImport(IGateImportStore store, IGateArtifactStore artifacts, IGateVerdictStore verdicts, ICommitResolver commits)
{
    public const string CaseLanguage = "plan-and-diff";

    public async Task<Outcome<CoaiBenchImportReport>> RunAsync(CoaiBenchImportRequest request, Action<string> progress, CancellationToken cancellationToken)
    {
        var parsed = request.Files.Select(f => (f.Location, Stages: CoaiBenchRecords.Parse(f.Json, request.PrivateNames))).ToList();
        if (parsed.FirstOrDefault(p => p.Stages is Outcome<IReadOnlyList<CoaiBenchStage>>.Fail) is { Stages: Outcome<IReadOnlyList<CoaiBenchStage>>.Fail bad } failed)
        {
            return Outcome<CoaiBenchImportReport>.Failure($"{failed.Location}: {bad.Reason}");
        }

        var stages = parsed.SelectMany(p => ((Outcome<IReadOnlyList<CoaiBenchStage>>.Ok)p.Stages).Value.Select(s => (p.Location, Stage: s))).ToList();
        var suite = await SuiteAsync(stages.Select(s => s.Stage.Case), cancellationToken);

        return suite is Outcome<GateSuite>.Ok { Value: var frozen }
            ? await ImportAsync(request, frozen, stages, progress, cancellationToken)
            : Outcome<CoaiBenchImportReport>.Failure(((Outcome<GateSuite>.Fail)suite).Reason);
    }

    private async Task<Outcome<CoaiBenchImportReport>> ImportAsync(
        CoaiBenchImportRequest request, GateSuite suite, IReadOnlyList<(string Location, CoaiBenchStage Stage)> stages, Action<string> progress, CancellationToken cancellationToken)
    {
        var writer = new GateImportWriter(artifacts, store);
        var counts = ImportCounts.None;
        var (verdictsSeen, verdictsNew, unjudged, family) = (0, 0, 0, 0);

        foreach (var ((location, stage), position) in stages.Select((s, i) => (s, i)))
        {
            var plan = Plan(request, suite, location, stage, position, stages.Where(s => s.Location == location && s.Stage.Gate == stage.Gate).Select(s => s.Stage));
            if (plan is not Outcome<ImportPlan>.Ok { Value: var ready })
            {
                return Outcome<CoaiBenchImportReport>.Failure(((Outcome<ImportPlan>.Fail)plan).Reason);
            }

            var written = await writer.WriteAsync(ready, cancellationToken);
            var judged = written is Outcome<ImportOutcome>.Ok ? await VerdictsAsync(request, ready.Cell.Id, stage, cancellationToken) : Outcome<int>.Failure(((Outcome<ImportOutcome>.Fail)written).Reason);
            if (judged is Outcome<int>.Fail fail)
            {
                return Outcome<CoaiBenchImportReport>.Failure(fail.Reason);
            }

            counts = counts.Add(((Outcome<ImportOutcome>.Ok)written).Value, 0);
            (verdictsSeen, verdictsNew, unjudged) = (verdictsSeen + stage.Worth.Count(w => w != WorthWord.Unjudged), verdictsNew + ((Outcome<int>.Ok)judged).Value, unjudged + stage.Worth.Count(w => w == WorthWord.Unjudged));
            family += FamilyMatches(stage) ? stage.Worth.Count(w => w != WorthWord.Unjudged) : 0;
            progress($"{(((Outcome<ImportOutcome>.Ok)written).Value == ImportOutcome.Imported ? "imported" : "unchanged"),-15}{location} {stage.Key}");
        }

        return Outcome<CoaiBenchImportReport>.Success(new CoaiBenchImportReport(suite, counts, verdictsSeen, verdictsNew, unjudged, family));
    }

    private static Outcome<ImportPlan> Plan(
        CoaiBenchImportRequest request, GateSuite suite, string location, CoaiBenchStage stage, int position, IEnumerable<CoaiBenchStage> campaignStages)
    {
        var (reviewer, pin) = (stage.Reviewer, ProductPin.Imported(string.Empty, CapturedCount.Unavailable("coai-bench recorded no product sha"), $"{CoaiBenchRecords.Harness} {location}"));
        if ((reviewer, pin) is not (Outcome<GateReviewerId>.Ok r, Outcome<ProductPin>.Ok p))
        {
            return Outcome<ImportPlan>.Failure(reviewer is Outcome<GateReviewerId>.Fail f ? $"arm '{stage.Arm}': {f.Reason}" : ((Outcome<ProductPin>.Fail)pin).Reason);
        }

        var campaign = new GateRun(
            ImportIds.Of(CoaiBenchRecords.Harness, $"{suite.Stamp}|campaign|{location}|{stage.Gate}"), stage.Gate, suite.Stamp, DataDirMode.Isolated,
            GateRunStatus.Finished, new RunSource.Imported(CoaiBenchRecords.Harness), campaignStages.Min(s => s.Started));
        var findings = stage.Findings.Select(f => GateFinding.Of(f.Ordinal, f.Severity, f.Category, f.IsGating, f.Line, f.Text, f.File, request.Key))
            .OfType<Outcome<GateFinding>.Ok>().Select(o => o.Value).ToList();
        var cell = new GateCell(
            ImportIds.Of(CoaiBenchRecords.Harness, $"{suite.Stamp}|cell|{stage.Key}"), campaign.Id, stage.Case.Task, r.Value, stage.Repeat, Slot: 0, position,
            Claimable.Stored(CellState.Settled, 1, WorkerIdentity.Nobody, stage.Started), p.Value, GateCellOutcomeKind.Completed, string.Empty);

        ImportFile[] files =
        [
            new("findings.jsonl", ArtifactClass.Findings, Encoding.UTF8.GetBytes(string.Concat(stage.Findings.Select(f => f.Json + "\n")))),
            new($"{ImportedFiles.SourceFolder}/stage.json", ArtifactClass.Reply, Encoding.UTF8.GetBytes(stage.SourceJson)),
        ];

        return Outcome<ImportPlan>.Success(new ImportPlan(
            campaign, cell, new GateSettlement.Completed(stage.Facts, findings, ImportedSettings.Hash(CoaiBenchRecords.Harness), PromptHash: string.Empty),
            stage.Key, stage.RunJson, files));
    }

    /// <summary>A lenient verdict per judged finding, in a batch named for the RECORD (so a copy is the same batch), by the
    /// record's judge.</summary>
    private async Task<Outcome<int>> VerdictsAsync(CoaiBenchImportRequest request, Guid cellId, CoaiBenchStage stage, CancellationToken cancellationToken)
    {
        var judge = GateReviewerId.Parse(stage.JudgedBy.Length > 0 ? ImportSlug.Of(stage.JudgedBy) : CoaiBenchRecords.UnrecordedJudge);
        if (judge is not Outcome<GateReviewerId>.Ok { Value: var assessor })
        {
            return Outcome<int>.Failure($"judge '{stage.JudgedBy}': {((Outcome<GateReviewerId>.Fail)judge).Reason}");
        }

        var built = stage.Worth.Select((w, ordinal) => (w, ordinal)).Where(p => p.w != WorthWord.Unjudged)
            .Select(p => GateVerdict.Under(request.Rubrics, request.Lenient.Rubric.Hash, cellId, p.ordinal, new Verdict.Lenient(p.w == WorthWord.Yes), assessor,
                $"coai-bench-{HashText.Short(StableHash.Of(stage.Key))}", promptHash: string.Empty, FamilyMatches(stage)))
            .ToList();

        return built.OfType<Outcome<GateVerdict>.Fail>().FirstOrDefault() is { } bad
            ? Outcome<int>.Failure(bad.Reason)
            : await verdicts.RecordAsync([.. built.OfType<Outcome<GateVerdict>.Ok>().Select(o => o.Value)], cancellationToken);
    }

    private static bool FamilyMatches(CoaiBenchStage stage) =>
        stage.JudgedBy.Length > 0 && VendorFamily.MatchesAnyOf(stage.JudgedBy, stage.Arm.Split(','));

    private async Task<Outcome<GateSuite>> SuiteAsync(IEnumerable<CoaiBenchCase> cases, CancellationToken cancellationToken)
    {
        var tasks = new List<GateTask>();

        foreach (var @case in cases.DistinctBy(c => c.Task.Value).OrderBy(c => c.Task.Value, StringComparer.Ordinal))
        {
            var task = await TaskAsync(@case, cancellationToken);
            if (task is Outcome<GateTask>.Fail fail)
            {
                return Outcome<GateSuite>.Failure($"case {@case.Task}: {fail.Reason}");
            }

            tasks.Add(((Outcome<GateTask>.Ok)task).Value);
        }

        return GateSuite.Freeze("coai-bench-cases", tasks, []);
    }

    private async Task<Outcome<GateTask>> TaskAsync(CoaiBenchCase @case, CancellationToken cancellationToken)
    {
        var (head, @base) = (await commits.ResolveAsync(@case.Commit, cancellationToken), await commits.ResolveAsync(@case.BaseRef, cancellationToken));
        var hosts = HostedGates.Of([GateKind.Plan, GateKind.Code]);

        return (head, @base, hosts) switch
        {
            (Outcome<CommitSha>.Fail f, _, _) => Outcome<GateTask>.Failure($"commit {@case.Commit}: {f.Reason}"),
            (_, Outcome<CommitSha>.Fail f, _) => Outcome<GateTask>.Failure($"base {@case.BaseRef}: {f.Reason}"),
            (Outcome<CommitSha>.Ok h, Outcome<CommitSha>.Ok b, Outcome<HostedGates>.Ok g) => GateCase.Of(b.Value, h.Value, @case.PlanFile, string.Empty, string.Empty).Match(
                c => GateTask.Of(@case.Task, CaseLanguage, g.Value, isCalibration: false, c, [], CloneLocation.Parse("the product's own repository").Match(l => l, _ => throw new InvalidOperationException("unreachable"))),
                Outcome<GateTask>.Failure),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }
}
