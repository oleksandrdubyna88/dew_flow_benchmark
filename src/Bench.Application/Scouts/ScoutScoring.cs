using Bench.Domain;
using Bench.Domain.Scouts;

namespace Bench.Application.Scouts;

/// <summary>What <c>score</c> did: cells rescored, cells whose stored recall it changed, cells it could not read or write, and every
/// (output, G file) pair it judged — the hand-check's pool.</summary>
public sealed record ScoutScoreReport(int Rescored, int Changed, IReadOnlyList<string> Unreadable, IReadOnlyList<HandcheckPair> Pairs);

/// <summary><c>bench scouts score</c> (§3.6): recall per scored cell RECOMPUTED from its stream — the team's KEY FILES as written, through the
/// matcher, over the task's tree at P — and stored with each scout's resolved KEY FILES. Settlement already computed it once; this is the
/// verb that re-applies the matcher after a hand-check finds it wrong (§3.7: "fix the matcher and draw 20 new pairs"), and it reads the same
/// pairs the hand-check draws from. Idempotent: a second run over the same streams writes the same numbers.</summary>
public sealed class ScoutScoring(IScoutVerdicts verdicts, IScoutTrees trees, IScoutStreams streams)
{
    private sealed record Scored(bool Stored, bool Changed, string Refusal, IReadOnlyList<HandcheckPair> Pairs);

    public async Task<ScoutScoreReport> RunAsync(
        IReadOnlyList<ScoutTask> tasks, IReadOnlyList<ScoutCell> cells, IReadOnlyList<ScoutAttempt> attempts, CancellationToken cancellationToken)
    {
        var read = ScoutSettledOutputs.Of(tasks, cells, attempts, streams);
        var unreadable = read.StreamMissing.Select(c => $"{ScoutCampaign.Name(c)}: its stream is not in the artefact root").ToList();
        var pairs = new List<HandcheckPair>();
        var (rescored, changed) = (0, 0);

        foreach (var group in read.Outputs.GroupBy(o => o.Task.Id.Value, StringComparer.Ordinal))
        {
            if (await trees.PrepareAsync(group.First().Task, cancellationToken) is not Outcome<ScoutTree>.Ok { Value: var tree })
            {
                unreadable.AddRange(group.Select(o => $"{ScoutCampaign.Name(o.Cell)}: the task's tree could not be prepared"));
                continue;
            }

            foreach (var output in group)
            {
                var scored = await RescoreAsync(output, tree, cancellationToken);
                (rescored, changed) = (rescored + (scored.Stored ? 1 : 0), changed + (scored.Changed ? 1 : 0));
                unreadable.AddRange(scored.Refusal.Length > 0 ? [$"{ScoutCampaign.Name(output.Cell)}: {scored.Refusal}"] : []);
                pairs.AddRange(scored.Pairs);
            }
        }

        return new ScoutScoreReport(rescored, changed, unreadable, pairs);
    }

    private async Task<Scored> RescoreAsync(ScoutCellOutput output, ScoutTree tree, CancellationToken cancellationToken)
    {
        var recall = RecallMatcher.Match(output.KeyFilesAsWritten, output.Task.GroundTruth, tree.Files, tree.Root);
        var rescore = new ScoutRescore(
            recall.Found.Count,
            recall.Total,
            [.. output.Reports.Select(r => new ScoutRescoredKeyFiles(r.Ordinal, ScoutCellReading.KeyFiles(ScoutOutput.KeyFiles(r.Text).Paths, tree.Files, tree.Root)))]);
        var before = output.Cell.Facts;
        var stored = await verdicts.RescoreAsync(output.Cell.Id, rescore, cancellationToken);

        return new Scored(
            stored is Outcome<ScoutCell>.Ok,
            stored is Outcome<ScoutCell>.Ok && (before.RecallFound.Value != rescore.Found || before.RecallTotal.Value != rescore.Total),
            stored is Outcome<ScoutCell>.Fail fail ? fail.Reason : string.Empty,
            ScoutHandcheck.PairsOf(output.Cell.Id, output.Task.Id.Value, output.Task.GroundTruth, output.KeyFilesAsWritten, tree.Files, tree.Root));
    }
}
