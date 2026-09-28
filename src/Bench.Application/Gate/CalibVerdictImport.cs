using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

public sealed record VerdictImportCounts(int Verdicts, int New, int Batches, int FamilyMatched)
{
    public static VerdictImportCounts None { get; } = new(0, 0, 0, 0);
}

/// <summary>S5.2 — the calibration's <c>assess.jsonl</c> + <c>key.json</c> as verdicts under <c>strict-v1</c>, written
/// through the E4 ingestion contract exactly as a native pass writes them: the assessor's lock held; the blinded ids
/// entered into THIS root's key (an id it already holds for another finding is refused); per batch the verdict log first
/// (the notes and the cluster text, so a person can hand-check imported verdicts later), then ONE database call. A
/// verdict already stored is skipped before anything is written, so a second import appends no line and stores no row.
/// The prompt hash is empty: the other harness archived no batch prompt, and a hash of nothing would be a guess.</summary>
public sealed class CalibVerdictImport(IGateVerdictStore verdicts, IGateAssessmentFiles files)
{
    public async Task<Outcome<VerdictImportCounts>> RunAsync(
        CalibImportRequest request, CalibSource source, IReadOnlyDictionary<string, Guid> cellIds, IReadOnlyDictionary<string, GateReviewer> byHash,
        IReadOnlyDictionary<int, GateRun> campaigns, Action<string> progress, CancellationToken cancellationToken)
    {
        var assessor = ((Outcome<GateReviewer>.Ok)request.Assessor).Value;
        var foreign = source.Verdicts.FirstOrDefault(v => !Names(assessor, v.Assessor));
        if (foreign is not null)
        {
            return Outcome<VerdictImportCounts>.Failure(
                $"verdict {foreign.Row.Id} was written by assessor '{foreign.Assessor}', and --assessor {assessor.Id} is a {assessor.Definition.Runtime.Word()} row — attribute each assessor's verdicts to its own row");
        }

        var locked = await files.LockAssessorAsync(assessor.Id, cancellationToken);
        if (locked is not Outcome<IAsyncDisposable>.Ok { Value: var hold })
        {
            return Outcome<VerdictImportCounts>.Failure(((Outcome<IAsyncDisposable>.Fail)locked).Reason);
        }

        await using (hold)
        {
            var joined = Join(request, source, cellIds, byHash, campaigns);
            var keyed = await KeyAsync(joined, cancellationToken);
            return keyed is Outcome<int>.Fail refused
                ? Outcome<VerdictImportCounts>.Failure(refused.Reason)
                : await BatchesAsync(request, assessor, joined, progress, cancellationToken);
        }
    }

    /// <summary>The assessor a line names is the row's runtime word (<c>codex</c>) or the row's own id.</summary>
    private static bool Names(GateReviewer row, string word) =>
        string.Equals(word, row.Definition.Runtime.Word(), StringComparison.OrdinalIgnoreCase) || string.Equals(word, row.Id.Value, StringComparison.Ordinal);

    private sealed record Joined(CalibVerdictLine Line, BlindKeyEntry Entry, GateReviewer Reviewer);

    private static IReadOnlyList<Joined> Join(
        CalibImportRequest request, CalibSource source, IReadOnlyDictionary<string, Guid> cellIds, IReadOnlyDictionary<string, GateReviewer> byHash,
        IReadOnlyDictionary<int, GateRun> campaigns)
    {
        var cells = source.Cells.ToDictionary(c => c.Record.Id, StringComparer.Ordinal);
        var key = source.Key.ToDictionary(k => k.Id.Value, StringComparer.Ordinal);

        return [.. source.Verdicts.Select(v =>
        {
            var entry = key[v.Row.Id.Value];
            var cell = cells[entry.Run];
            var reviewer = byHash[cell.Definition.Hash];
            return new Joined(v, new BlindKeyEntry(v.Row.Id, campaigns[cell.Record.Phase].Id, cellIds[entry.Run], entry.Index, entry.Task, reviewer.Id), reviewer);
        })];
    }

    /// <summary>Enters the imported ids into the root's key. An id the key holds for ANOTHER finding, or a finding the key
    /// holds under ANOTHER id, is refused before anything is written — either would join a verdict to the wrong finding.</summary>
    private async Task<Outcome<int>> KeyAsync(IReadOnlyList<Joined> joined, CancellationToken cancellationToken)
    {
        var held = await files.ReadKeyAsync(cancellationToken);
        if (held is not Outcome<IReadOnlyList<BlindKeyEntry>>.Ok { Value: var entries })
        {
            return Outcome<int>.Failure(((Outcome<IReadOnlyList<BlindKeyEntry>>.Fail)held).Reason);
        }

        var byId = entries.ToDictionary(e => e.Id.Value, StringComparer.Ordinal);
        var byFinding = entries.GroupBy(e => (e.RunId, e.Ordinal)).ToDictionary(g => g.Key, g => g.First().Id.Value);
        var clash = joined.Select(j => j.Entry).FirstOrDefault(e =>
            (byId.TryGetValue(e.Id.Value, out var other) && (other.RunId, other.Ordinal) != (e.RunId, e.Ordinal))
            || (byFinding.TryGetValue((e.RunId, e.Ordinal), out var id) && id != e.Id.Value));

        if (clash is not null)
        {
            return Outcome<int>.Failure($"blinded id {clash.Id} clashes with this root's key — it names another finding there, or its finding is already blinded under another id");
        }

        var extended = await files.ExtendKeyAsync(
            current => [.. joined.Select(j => j.Entry).Where(e => !current.Any(c => c.Id.Value == e.Id.Value))], cancellationToken);

        return extended.Match(k => Outcome<int>.Success(k.Count), Outcome<int>.Failure);
    }

    private async Task<Outcome<VerdictImportCounts>> BatchesAsync(
        CalibImportRequest request, GateReviewer assessor, IReadOnlyList<Joined> joined, Action<string> progress, CancellationToken cancellationToken)
    {
        var stored = (await verdicts.VerdictsAsync([.. joined.Select(j => j.Entry.RunId).Distinct()], request.Rubrics, cancellationToken))
            .Select(v => (v.RunId, v.FindingOrdinal, v.Rubric.Hash, v.Assessor.Value, v.BatchId)).ToHashSet();
        var logged = (await files.ReadVerdictLinesAsync(cancellationToken)).Select(l => (l.Id, l.BatchId)).ToHashSet();
        var counts = VerdictImportCounts.None with { Verdicts = joined.Count };

        foreach (var batch in joined.GroupBy(j => j.Line.Batch, StringComparer.Ordinal))
        {
            var fresh = batch.Where(j => !stored.Contains((j.Entry.RunId, j.Entry.Ordinal, request.Strict.Rubric.Hash, assessor.Id.Value, j.Line.Batch))).ToList();
            var recorded = await RecordAsync(request, assessor, batch.Key, fresh, logged, cancellationToken);
            if (recorded is Outcome<int>.Fail fail)
            {
                return Outcome<VerdictImportCounts>.Failure($"batch {batch.Key}: {fail.Reason}");
            }

            counts = counts with
            {
                New = counts.New + ((Outcome<int>.Ok)recorded).Value,
                Batches = counts.Batches + 1,
                FamilyMatched = counts.FamilyMatched + fresh.Count(j => VendorFamily.Matches(assessor.Definition, j.Reviewer.Definition)),
            };
            progress($"verdicts       batch {batch.Key}: {fresh.Count} new of {batch.Count()}");
        }

        return Outcome<VerdictImportCounts>.Success(counts);
    }

    /// <summary>The E4 order: the log line (with its text) first, flushed; then the verdicts in one call — a crash between
    /// the two leaves an orphan line the next import does not append again, and the verdict it did not store.</summary>
    private async Task<Outcome<int>> RecordAsync(
        CalibImportRequest request, GateReviewer assessor, string batchId, IReadOnlyList<Joined> fresh, IReadOnlySet<(string, string)> logged, CancellationToken cancellationToken)
    {
        if (fresh.Count == 0)
        {
            return Outcome<int>.Success(0);
        }

        var lines = fresh.Where(j => !logged.Contains((j.Line.Row.Id.Value, batchId)))
            .Select(j => VerdictLine.Of(j.Line.Row, assessor.Id, batchId, request.Strict.Rubric, j.Line.Utc)).ToList();
        var built = fresh.Select(j => GateVerdict.Under(
                request.Rubrics, request.Strict.Rubric.Hash, j.Entry.RunId, j.Entry.Ordinal, j.Line.Row.ToVerdict(request.Key),
                assessor.Id, batchId, promptHash: string.Empty, VendorFamily.Matches(assessor.Definition, j.Reviewer.Definition)))
            .ToList();

        if (built.OfType<Outcome<GateVerdict>.Fail>().FirstOrDefault() is { } bad)
        {
            return Outcome<int>.Failure(bad.Reason);
        }

        await files.AppendVerdictLinesAsync(assessor.Id, lines, cancellationToken);
        return await verdicts.RecordAsync([.. built.OfType<Outcome<GateVerdict>.Ok>().Select(o => o.Value)], cancellationToken);
    }
}
