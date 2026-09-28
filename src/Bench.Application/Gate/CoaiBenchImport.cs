using System.Text;
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

/// <param name="Suites">One suite per location — the cases THAT file carries — so the stamp never depends on which other
/// files were passed beside it.</param>
public sealed record CoaiBenchImportReport(IReadOnlyList<GateSuite> Suites, ImportCounts Cells, int Verdicts, int VerdictsNew, int Unjudged, int FamilyMatched);

/// <summary>S5.3 — coai-bench <c>runs.json</c> files as plan and code cells. A campaign per (location, gate) — the folder
/// the file sits in, so a file that GROWS adds its new records to the same campaign (plan round, finding 9) — and a cell per
/// RECORD (<c>arm|case|repeat|startedUtc|stage</c>), never per file and never per suite, so a byte copy elsewhere, or a
/// file that also carries another case, adds nothing for the records already there (code round). Each location's cases
/// are its suite (full shas resolved in the product's repository; no seeds; plan and code hosted). An arm is a vendor set
/// with no model recorded, so no catalog row is invented. <c>Useful</c> yes / no is a lenient verdict under
/// <c>lenient-worth-v1</c>, by the record's judge; <c>unjudged</c> stays unassessed — no row; a finding the judge later
/// answered DIFFERENTLY is refused, never kept as it was.</summary>
public sealed class CoaiBenchImport(IGateImportStore store, IGateArtifactStore artifacts, IGateVerdictStore verdicts, ICommitResolver commits)
{
    public const string CaseLanguage = "plan-and-diff";

    private sealed record Located(string Location, CoaiBenchStage Stage);

    private sealed record Tally(ImportCounts Cells, int Verdicts, int New, int Unjudged, int Family)
    {
        public static Tally None { get; } = new(ImportCounts.None, 0, 0, 0, 0);
    }

    public async Task<Outcome<CoaiBenchImportReport>> RunAsync(CoaiBenchImportRequest request, Action<string> progress, CancellationToken cancellationToken)
    {
        var parsed = request.Files.Select(f => (f.Location, Stages: CoaiBenchRecords.Parse(f.Json, request.PrivateNames))).ToList();
        if (parsed.FirstOrDefault(p => p.Stages is Outcome<IReadOnlyList<CoaiBenchStage>>.Fail) is { Stages: Outcome<IReadOnlyList<CoaiBenchStage>>.Fail bad } failed)
        {
            return Outcome<CoaiBenchImportReport>.Failure($"{failed.Location}: {bad.Reason}");
        }

        List<Located> stages = [.. parsed.SelectMany(p => ((Outcome<IReadOnlyList<CoaiBenchStage>>.Ok)p.Stages).Value.Select(s => new Located(p.Location, s)))];
        var twice = OnePerCell(stages);
        if (twice.Length > 0)
        {
            return Outcome<CoaiBenchImportReport>.Failure(twice);
        }

        var suites = await SuitesAsync(stages, cancellationToken);
        return suites is Outcome<IReadOnlyDictionary<string, GateSuite>>.Ok { Value: var byLocation }
            ? await ImportAsync(request, byLocation, stages, progress, cancellationToken)
            : Outcome<CoaiBenchImportReport>.Failure(((Outcome<IReadOnlyDictionary<string, GateSuite>>.Fail)suites).Reason);
    }

    /// <summary>Two records on one cell of one campaign — the same arm, case and repeat in one location and gate, started at
    /// different times — would be two runs under one population key, and the report would keep one of them arbitrarily.</summary>
    private static string OnePerCell(IReadOnlyList<Located> stages) =>
        stages.GroupBy(s => (s.Location, s.Stage.Gate, s.Stage.Case.Task.Value, s.Stage.Arm, s.Stage.Repeat))
            .Where(g => g.Select(s => s.Stage.Key).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(g => $"records {string.Join(", ", g.Select(s => s.Stage.Key).Distinct(StringComparer.Ordinal))} are one cell of campaign {g.Key.Location} ({g.Key.Gate}) — refused rather than one hiding the other")
            .FirstOrDefault(string.Empty);

    private async Task<Outcome<CoaiBenchImportReport>> ImportAsync(
        CoaiBenchImportRequest request, IReadOnlyDictionary<string, GateSuite> suites, IReadOnlyList<Located> stages, Action<string> progress, CancellationToken cancellationToken)
    {
        var writer = new GateImportWriter(artifacts, store);
        var starts = stages.GroupBy(s => (s.Location, s.Stage.Gate)).ToDictionary(g => g.Key, g => g.Min(s => s.Stage.Started));
        var tally = Tally.None;

        foreach (var (located, position) in stages.Select((s, i) => (s, i)))
        {
            var one = await OneAsync(request, writer, suites[located.Location], located, position, starts[(located.Location, located.Stage.Gate)], cancellationToken);
            if (one is Outcome<(ImportOutcome, int)>.Fail fail)
            {
                return Outcome<CoaiBenchImportReport>.Failure(fail.Reason);
            }

            var (outcome, fresh) = ((Outcome<(ImportOutcome, int)>.Ok)one).Value;
            tally = Tallied(tally, located.Stage, outcome, fresh);
            progress($"{(outcome == ImportOutcome.Imported ? "imported" : "unchanged"),-15}{located.Location} {located.Stage.Key}");
        }

        return Outcome<CoaiBenchImportReport>.Success(new CoaiBenchImportReport(
            [.. suites.Values.DistinctBy(s => s.Stamp)], tally.Cells, tally.Verdicts, tally.New, tally.Unjudged, tally.Family));
    }

    private async Task<Outcome<(ImportOutcome, int)>> OneAsync(
        CoaiBenchImportRequest request, GateImportWriter writer, GateSuite suite, Located located, int position, DateTimeOffset campaignStart, CancellationToken cancellationToken)
    {
        var plan = Plan(request, suite, located, position, campaignStart);
        if (plan is not Outcome<ImportPlan>.Ok { Value: var ready })
        {
            return Outcome<(ImportOutcome, int)>.Failure(((Outcome<ImportPlan>.Fail)plan).Reason);
        }

        var written = await writer.WriteAsync(ready, cancellationToken);
        if (written is not Outcome<ImportOutcome>.Ok { Value: var outcome })
        {
            return Outcome<(ImportOutcome, int)>.Failure(((Outcome<ImportOutcome>.Fail)written).Reason);
        }

        return (await VerdictsAsync(request, ready.Cell.Id, located.Stage, cancellationToken)).Match(
            fresh => Outcome<(ImportOutcome, int)>.Success((outcome, fresh)),
            Outcome<(ImportOutcome, int)>.Failure);
    }

    private static Tally Tallied(Tally tally, CoaiBenchStage stage, ImportOutcome outcome, int fresh)
    {
        var judged = stage.Worth.Count(w => w != WorthWord.Unjudged);

        return new Tally(
            tally.Cells.Add(outcome, 0),
            tally.Verdicts + judged,
            tally.New + fresh,
            tally.Unjudged + stage.Worth.Count - judged,
            tally.Family + (FamilyMatches(stage) ? judged : 0));
    }

    /// <summary>The campaign is keyed by (location, gate) and the cell by the record's key — neither by the suite stamp, which
    /// is the campaign's metadata: a campaign's stamp is its location's cases.</summary>
    private static Outcome<ImportPlan> Plan(CoaiBenchImportRequest request, GateSuite suite, Located located, int position, DateTimeOffset campaignStart)
    {
        var stage = located.Stage;
        var (reviewer, pin) = (stage.Reviewer, ProductPin.Imported(string.Empty, CapturedCount.Unavailable("coai-bench recorded no product sha"), $"{CoaiBenchRecords.Harness} {located.Location}"));
        if ((reviewer, pin) is not (Outcome<GateReviewerId>.Ok r, Outcome<ProductPin>.Ok p))
        {
            return Outcome<ImportPlan>.Failure(reviewer is Outcome<GateReviewerId>.Fail f ? $"arm '{stage.Arm}': {f.Reason}" : ((Outcome<ProductPin>.Fail)pin).Reason);
        }

        var campaign = new GateRun(
            ImportIds.Of(CoaiBenchRecords.Harness, $"campaign|{located.Location}|{stage.Gate}"), stage.Gate, suite.Stamp, DataDirMode.Isolated,
            GateRunStatus.Finished, new RunSource.Imported(CoaiBenchRecords.Harness), campaignStart);
        var findings = stage.Findings.Select(f => GateFinding.Of(f.Ordinal, f.Severity, f.Category, f.IsGating, f.Line, f.Text, f.File, request.Key))
            .OfType<Outcome<GateFinding>.Ok>().Select(o => o.Value).ToList();
        var cell = new GateCell(
            ImportIds.Of(CoaiBenchRecords.Harness, $"cell|{stage.Key}"), campaign.Id, stage.Case.Task, r.Value, stage.Repeat, Slot: 0, position,
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
    /// record's judge. One the store already holds under another reading is refused — the judge answered it differently
    /// since, and keeping the first answer silently would publish an opinion the source no longer holds.</summary>
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

        if (built.OfType<Outcome<GateVerdict>.Fail>().FirstOrDefault() is { } bad)
        {
            return Outcome<int>.Failure(bad.Reason);
        }

        List<GateVerdict> ready = [.. built.OfType<Outcome<GateVerdict>.Ok>().Select(o => o.Value)];
        var changed = await ChangedAsync(request, cellId, ready, cancellationToken);

        return changed.Length > 0 ? Outcome<int>.Failure($"record {stage.Key}: {changed}") : await verdicts.RecordAsync(ready, cancellationToken);
    }

    private async Task<string> ChangedAsync(CoaiBenchImportRequest request, Guid cellId, IReadOnlyList<GateVerdict> ready, CancellationToken cancellationToken)
    {
        if (ready.Count == 0)
        {
            return string.Empty;
        }

        var held = (await verdicts.VerdictsAsync([cellId], request.Rubrics, cancellationToken))
            .ToDictionary(v => (v.FindingOrdinal, v.Rubric.Hash, v.Assessor.Value, v.BatchId), v => v.Reading);

        return ready.Where(v => held.TryGetValue((v.FindingOrdinal, v.Rubric.Hash, v.Assessor.Value, v.BatchId), out var reading) && reading != v.Reading)
            .Select(v => $"finding {v.FindingOrdinal} was judged differently since it was imported — refused rather than kept as it was")
            .FirstOrDefault(string.Empty);
    }

    private static bool FamilyMatches(CoaiBenchStage stage) =>
        stage.JudgedBy.Length > 0 && VendorFamily.MatchesAnyOf(stage.JudgedBy, stage.Arm.Split(','));

    /// <summary>One suite per location, over the cases the file at that location carries.</summary>
    private async Task<Outcome<IReadOnlyDictionary<string, GateSuite>>> SuitesAsync(IReadOnlyList<Located> stages, CancellationToken cancellationToken)
    {
        var suites = new Dictionary<string, GateSuite>(StringComparer.Ordinal);

        foreach (var location in stages.GroupBy(s => s.Location, StringComparer.Ordinal))
        {
            var suite = await SuiteAsync(location.Select(s => s.Stage.Case), cancellationToken);
            if (suite is Outcome<GateSuite>.Fail fail)
            {
                return Outcome<IReadOnlyDictionary<string, GateSuite>>.Failure($"{location.Key}: {fail.Reason}");
            }

            suites[location.Key] = ((Outcome<GateSuite>.Ok)suite).Value;
        }

        return Outcome<IReadOnlyDictionary<string, GateSuite>>.Success(suites);
    }

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

        return (head, @base, hosts, CloneLocation.Parse("the product's own repository")) switch
        {
            (Outcome<CommitSha>.Fail f, _, _, _) => Outcome<GateTask>.Failure($"commit {@case.Commit}: {f.Reason}"),
            (_, Outcome<CommitSha>.Fail f, _, _) => Outcome<GateTask>.Failure($"base {@case.BaseRef}: {f.Reason}"),
            (Outcome<CommitSha>.Ok h, Outcome<CommitSha>.Ok b, Outcome<HostedGates>.Ok g, Outcome<CloneLocation>.Ok l) =>
                GateCase.Of(b.Value, h.Value, @case.PlanFile, string.Empty, string.Empty).Match(
                    c => GateTask.Of(@case.Task, CaseLanguage, g.Value, isCalibration: false, c, [], l.Value),
                    Outcome<GateTask>.Failure),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }
}
