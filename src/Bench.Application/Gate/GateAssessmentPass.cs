using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

/// <summary>What one assessment pass is asked to do: which campaigns' findings, against which suite (its seeds, its
/// checkouts), by which assessor under which rubric, in batches of what size — and the catalog rows of every reviewer
/// whose findings it reads, so a verdict can say whether its assessor is of the reviewer's own family.</summary>
public sealed record AssessmentRequest(
    IReadOnlyList<Guid> Campaigns,
    GateSuite Suite,
    AssessorLaunch Launch,
    RubricCatalog Catalog,
    BatchSize Size,
    FileHashKey Key,
    IReadOnlyDictionary<string, GateReviewer> Reviewers);

/// <param name="Failure">The cause every finding of the batch was recorded under, or empty when it answered.</param>
public sealed record BatchOutcome(string BatchId, GateTaskId Task, int Asked, int Assessed, int Missing, string Failure, IReadOnlyList<string> Refusals)
{
    public int Failed => Failure.Length > 0 ? Asked : 0;
}

/// <summary>What a pass did. <see cref="Unassessed"/> are findings still without a reading after their one re-ask —
/// no row was written for them, and the next pass asks again.</summary>
public sealed record AssessmentReport(int Exported, int Pending, IReadOnlyList<BatchOutcome> Batches, int Unassessed, int FamilyMatched, IReadOnlyList<string> Refusals)
{
    public int Assessed => Batches.Sum(b => b.Assessed);

    public int Failed => Batches.Sum(b => b.Failed);
}

/// <summary>The blinded strict assessment, end to end — the calibration's <c>export</c> + <c>run_task</c>, over the ports.
/// <list type="number">
/// <item><b>Export</b>: every finding of the campaigns' settled cells, its text read hash-verified from the artefact
/// store; a finding not yet in the key gets a fresh blinded id, and the key is replaced atomically BEFORE any id is
/// sent anywhere.</item>
/// <item><b>Pending</b>: what this assessor has not read under this rubric, or read only as a failure.</item>
/// <item><b>Batches</b> of at most the batch size, per task. An answer that fails (does not parse, is cut, names an id it
/// was not given, is nothing) is asked again ONCE as a whole; failing again, every finding of it is recorded as an
/// <see cref="Verdict.AssessmentFailure"/> by cause. An answer with ids missing keeps what it answered and asks for the
/// missing ones once more; still missing, they are left unassessed.</item>
/// <item><b>Per batch</b>, the verdict log (text) is appended and flushed, then the database rows are written in one
/// transaction — the database is the commit point; a log line whose batch never reached it is an orphan.</item>
/// </list></summary>
public sealed class GateAssessmentPass(
    IGateStore store,
    IGateArtifactStore artifacts,
    IGateVerdictStore verdicts,
    IGateAssessmentFiles files,
    IGateCheckouts checkouts,
    FindingAssessor assessor,
    TimeProvider clock,
    Random random)
{
    public async Task<Outcome<AssessmentReport>> RunAsync(AssessmentRequest request, Action<BatchOutcome> progress, CancellationToken cancellationToken)
    {
        var found = await GateFindingTexts.ReadAsync(store, artifacts, request.Campaigns, cancellationToken);
        if (found is Outcome<IReadOnlyList<FindingToAssess>>.Fail unread)
        {
            return Outcome<AssessmentReport>.Failure(unread.Reason);
        }

        var locked = await files.LockAssessorAsync(request.Launch.Assessor.Id, cancellationToken);
        if (locked is not Outcome<IAsyncDisposable>.Ok { Value: var hold })
        {
            return Outcome<AssessmentReport>.Failure(((Outcome<IAsyncDisposable>.Fail)locked).Reason);
        }

        await using (hold)
        {
            var findings = ((Outcome<IReadOnlyList<FindingToAssess>>.Ok)found).Value;
            var exported = 0;
            var key = await files.ExtendKeyAsync(
                held =>
                {
                    var fresh = BlindExport.Plan(findings, held, random);
                    exported = fresh.Count;
                    return fresh;
                },
                cancellationToken);

            return key switch
            {
                Outcome<IReadOnlyList<BlindKeyEntry>>.Ok ok => Outcome<AssessmentReport>.Success(
                    await AssessAsync(request, findings, ok.Value, exported, progress, cancellationToken)),
                Outcome<IReadOnlyList<BlindKeyEntry>>.Fail bad => Outcome<AssessmentReport>.Failure(bad.Reason),
                _ => throw new InvalidOperationException("unreachable"),
            };
        }
    }

    private async Task<AssessmentReport> AssessAsync(
        AssessmentRequest request, IReadOnlyList<FindingToAssess> findings, IReadOnlyList<BlindKeyEntry> key, int exported,
        Action<BatchOutcome> progress, CancellationToken cancellationToken)
    {
        var selected = findings.Select(f => f.Key).ToHashSet();
        var entries = key.Where(e => selected.Contains((e.RunId, e.Ordinal))).ToList();
        var existing = await verdicts.VerdictsAsync([.. entries.Select(e => e.RunId).Distinct()], request.Catalog, cancellationToken);
        var pending = AssessmentPending.Of(entries, existing, request.Launch.Assessor.Id, request.Launch.Rubric.Rubric);
        var context = new PassContext(request, findings.ToDictionary(f => f.Key, f => f.FindingJson), await PriorClustersAsync(request, cancellationToken));
        var outcomes = new List<BatchOutcome>();
        var refusals = new List<string>();

        foreach (var task in pending.GroupBy(e => e.Task.Value, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var evidence = await EvidenceAsync(request.Suite, task.First().Task, cancellationToken);

            if (evidence is Outcome<(TaskEvidence, IReadOnlyCollection<SeedId>)>.Fail noEvidence)
            {
                refusals.Add(noEvidence.Reason);
                continue;
            }

            foreach (var batch in AssessmentPending.Batches([.. task], request.Size))
            {
                foreach (var outcome in await BatchAsync(context, batch, ((Outcome<(TaskEvidence, IReadOnlyCollection<SeedId>)>.Ok)evidence).Value, cancellationToken))
                {
                    outcomes.Add(outcome);
                    progress(outcome);
                }
            }
        }

        var assessed = outcomes.Sum(o => o.Assessed + o.Failed);

        return new AssessmentReport(exported, pending.Count, outcomes, pending.Count - assessed, context.FamilyMatched, [.. refusals, .. outcomes.SelectMany(o => o.Refusals)]);
    }

    /// <summary>One batch: asked (and asked again once if it failed), persisted; then its missing ids asked once more as a
    /// batch of their own, under the same failure rule.</summary>
    private async Task<IReadOnlyList<BatchOutcome>> BatchAsync(
        PassContext context, IReadOnlyList<BlindKeyEntry> batch, (TaskEvidence Evidence, IReadOnlyCollection<SeedId> Seeds) task, CancellationToken cancellationToken)
    {
        var first = await PersistAsync(context, batch, await AskWithRetryAsync(context, batch, task, cancellationToken), cancellationToken);

        if (first.Missing.Count == 0)
        {
            return [first.Outcome];
        }

        var again = batch.Where(e => first.Missing.Contains(e.Id.Value)).ToList();
        var second = await PersistAsync(context, again, await AskWithRetryAsync(context, again, task, cancellationToken), cancellationToken);

        return [first.Outcome, second.Outcome];
    }

    private async Task<(string BatchId, BatchAnswer Answer)> AskWithRetryAsync(
        PassContext context, IReadOnlyList<BlindKeyEntry> batch, (TaskEvidence Evidence, IReadOnlyCollection<SeedId> Seeds) task, CancellationToken cancellationToken)
    {
        var rows = batch.Select(e => AssessmentRow.Of(e.Id, e.Task, context.Texts[(e.RunId, e.Ordinal)], task.Evidence)).ToList();
        var baseId = $"{batch[0].Task}-{random.NextInt64(0, 1L << 32):x8}";
        var answer = await AskOnceAsync(context, $"{baseId}-a1", batch[0].Task, rows, task.Seeds, cancellationToken);

        if (answer.Answer.Reading is not BatchReading.Failed)
        {
            return answer;
        }

        return await AskOnceAsync(context, $"{baseId}-a2", batch[0].Task, rows, task.Seeds, cancellationToken);
    }

    private async Task<(string BatchId, BatchAnswer Answer)> AskOnceAsync(
        PassContext context, string batchId, GateTaskId task, IReadOnlyList<AssessmentRow> rows, IReadOnlyCollection<SeedId> seeds, CancellationToken cancellationToken) =>
        (batchId, await assessor.AskAsync(context.Request.Launch, new BatchAsk(batchId, task, rows, seeds, context.Prior(task)), cancellationToken));

    private async Task<(BatchOutcome Outcome, IReadOnlySet<string> Missing)> PersistAsync(
        PassContext context, IReadOnlyList<BlindKeyEntry> batch, (string BatchId, BatchAnswer Answer) asked, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var (lines, readings, missing, failure, refusals) = Readings(context, batch, asked, now);
        var built = readings.Select(r => Verdict(context, r.Entry, r.Reading, asked)).ToList();

        await files.AppendVerdictLinesAsync(context.Request.Launch.Assessor.Id, lines, cancellationToken);
        var recorded = await verdicts.RecordAsync(built, cancellationToken);

        context.Remember(batch[0].Task, lines.Select(l => l.Cluster));
        context.FamilyMatched += built.Count(v => v.AssessorFamilyMatches && v.Reading.CountsInRates);

        var stored = recorded is Outcome<int>.Fail refused ? [$"batch {asked.BatchId}: {refused.Reason}"] : Array.Empty<string>();

        return (new BatchOutcome(asked.BatchId, batch[0].Task, batch.Count, failure.Length > 0 ? 0 : readings.Count, missing.Count, failure, [.. refusals, .. stored]), missing);
    }

    private static (IReadOnlyList<VerdictLine> Lines, IReadOnlyList<(BlindKeyEntry Entry, Verdict Reading)> Readings, IReadOnlySet<string> Missing, string Failure, IReadOnlyList<string> Refusals) Readings(
        PassContext context, IReadOnlyList<BlindKeyEntry> batch, (string BatchId, BatchAnswer Answer) asked, DateTimeOffset now)
    {
        var assessorId = context.Request.Launch.Assessor.Id;
        var rubric = context.Request.Launch.Rubric.Rubric;

        if (asked.Answer.Reading is BatchReading.Failed failed)
        {
            return (
                [.. batch.Select(e => VerdictLine.FailureOf(e.Id, e.Task, failed.Cause, assessorId, asked.BatchId, rubric, now))],
                [.. batch.Select(e => (e, (Verdict)new Verdict.AssessmentFailure(failed.Cause)))],
                new HashSet<string>(StringComparer.Ordinal),
                failed.Cause.ToString(),
                [$"batch {asked.BatchId}: {failed.Cause} after one retry — {failed.Reason}"]);
        }

        var answered = (BatchReading.Answered)asked.Answer.Reading;
        var byId = batch.ToDictionary(e => e.Id.Value, StringComparer.Ordinal);

        return (
            [.. answered.Rows.Select(r => VerdictLine.Of(r, assessorId, asked.BatchId, rubric, now))],
            [.. answered.Rows.Select(r => (byId[r.Id.Value], (Verdict)r.ToVerdict(context.Request.Key)))],
            answered.Missing.Select(m => m.Value).ToHashSet(StringComparer.Ordinal),
            string.Empty,
            [.. answered.Refusals.Select(r => $"batch {asked.BatchId}: {r}")]);
    }

    private static GateVerdict Verdict(PassContext context, BlindKeyEntry entry, Verdict reading, (string BatchId, BatchAnswer Answer) asked)
    {
        var launch = context.Request.Launch;
        var reviewer = context.Request.Reviewers.GetValueOrDefault(entry.Reviewer.Value);
        var family = reviewer is not null && VendorFamily.Matches(launch.Assessor.Definition, reviewer.Definition);

        return GateVerdict.Under(context.Request.Catalog, launch.Rubric.Rubric.Hash, entry.RunId, entry.Ordinal, reading, launch.Assessor.Id,
                asked.BatchId, asked.Answer.PromptHash, family)
            .Match(v => v, reason => throw new InvalidOperationException($"a verdict the pass built was refused by its own catalog — {reason}"));
    }

    /// <summary>The read-only checkout at the task's variant head and the seed list the assessor reads — or a refusal
    /// naming the task, which leaves its findings unassessed rather than asking about code the assessor cannot see.</summary>
    private async Task<Outcome<(TaskEvidence, IReadOnlyCollection<SeedId>)>> EvidenceAsync(GateSuite suite, GateTaskId taskId, CancellationToken cancellationToken)
    {
        var task = suite.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null)
        {
            return Outcome<(TaskEvidence, IReadOnlyCollection<SeedId>)>.Failure($"task '{taskId}' is not in the suite this pass was given — its findings stay unassessed");
        }

        var checkout = await checkouts.ReadOnlyAsync(task, cancellationToken);
        if (checkout is not Outcome<string>.Ok { Value: var repo })
        {
            return Outcome<(TaskEvidence, IReadOnlyCollection<SeedId>)>.Failure(
                $"task '{taskId}': the read-only checkout at the variant head could not be made — {((Outcome<string>.Fail)checkout).Reason}");
        }

        var seeds = await files.WriteSeedSpecAsync(task, cancellationToken);

        return Outcome<(TaskEvidence, IReadOnlyCollection<SeedId>)>.Success(
            (new TaskEvidence(repo, task.Case.Base, task.Case.VariantHead, seeds), [.. task.Seeds.Select(s => s.Id)]));
    }

    /// <summary>The cluster keys earlier rows of each task already used under this rubric — carried into every batch of
    /// the task so one issue keeps one key.</summary>
    private async Task<Dictionary<string, HashSet<string>>> PriorClustersAsync(AssessmentRequest request, CancellationToken cancellationToken) =>
        (await files.ReadVerdictLinesAsync(cancellationToken))
            .Where(l => l.RubricHash == request.Launch.Rubric.Rubric.Hash && l.Cluster.Length > 0)
            .GroupBy(l => l.Task, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(l => l.Cluster).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);

    private sealed class PassContext(AssessmentRequest request, IReadOnlyDictionary<(Guid, int), string> texts, Dictionary<string, HashSet<string>> prior)
    {
        public AssessmentRequest Request { get; } = request;

        public IReadOnlyDictionary<(Guid, int), string> Texts { get; } = texts;

        public int FamilyMatched { get; set; }

        public IReadOnlyList<string> Prior(GateTaskId task) => prior.TryGetValue(task.Value, out var keys) ? [.. keys] : [];

        public void Remember(GateTaskId task, IEnumerable<string> clusters)
        {
            if (!prior.TryGetValue(task.Value, out var keys))
            {
                keys = new HashSet<string>(StringComparer.Ordinal);
                prior[task.Value] = keys;
            }

            keys.UnionWith(clusters.Where(c => c.Length > 0));
        }
    }
}
