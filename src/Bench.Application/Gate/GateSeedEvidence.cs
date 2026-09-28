using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

/// <summary>One seed and where its evidence sat for the reviewer.</summary>
public sealed record SeedEvidenceRow(SeedId Seed, bool CrossEpic, EvidenceWhere Where);

/// <summary>Where each of a task's seeds sat in the PRODUCT's turn-1 prompt — the calibration's <c>seed_evidence</c>, read
/// off disk: the first prompt file (<c>answers/NN-…prompt…</c>, committed as a <see cref="ArtifactClass.Prompt"/>
/// artefact) of the task's earliest settled cell, re-read hash-verified, then <see cref="SeedEvidence.Classify"/>. A task
/// no run left a prompt for (a CLI reviewer writes none) reads <see cref="EvidenceWhere.Unknown"/>, never "missed".
/// Nothing is stored: it is derivable from the artefacts whenever it is asked.</summary>
public static class GateSeedEvidence
{
    public static async Task<IReadOnlyList<SeedEvidenceRow>> ReadAsync(
        IGateStore store, IGateArtifactStore artifacts, IReadOnlyList<Guid> campaigns, GateTask task, CancellationToken cancellationToken)
    {
        var prompt = await TurnOnePromptAsync(store, artifacts, campaigns, task.Id, cancellationToken);

        return [.. task.Seeds.Select(seed => new SeedEvidenceRow(seed.Id, seed.CrossEpic, SeedEvidence.Classify(seed, prompt)))];
    }

    private static async Task<string> TurnOnePromptAsync(
        IGateStore store, IGateArtifactStore artifacts, IReadOnlyList<Guid> campaigns, GateTaskId task, CancellationToken cancellationToken)
    {
        foreach (var campaign in campaigns)
        {
            var prompts = (await store.ArtifactsAsync(campaign, cancellationToken)).Where(a => a.Class == ArtifactClass.Prompt).ToList();

            foreach (var record in (await store.FactsAsync(campaign, cancellationToken)).Where(r => r.Task == task))
            {
                var first = prompts.Where(p => p.CellId == record.RunId && p.Attempt == record.Attempt)
                    .OrderBy(p => p.Path.Value, StringComparer.Ordinal)
                    .FirstOrDefault();

                if (first is not null && await artifacts.ReadAsync(first, cancellationToken) is Outcome<ReadOnlyMemory<byte>>.Ok { Value: var bytes })
                {
                    return System.Text.Encoding.UTF8.GetString(bytes.Span);
                }
            }
        }

        return string.Empty;
    }
}
