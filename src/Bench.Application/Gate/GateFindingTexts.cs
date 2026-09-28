using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

/// <summary>The TEXT of a campaign's findings — read from the artefact store, never the database (which holds none).</summary>
public static class GateFindingTexts
{
    /// <summary>Every finding of the campaigns' settled cells, with its text from the cell's committed
    /// <c>findings.jsonl</c> — re-read and refused unless its bytes still hash to the ref. Line <i>n</i> is ordinal <i>n</i>;
    /// only ordinals the database holds a finding for are taken.</summary>
    public static async Task<Outcome<IReadOnlyList<FindingToAssess>>> ReadAsync(
        IGateStore store, IGateArtifactStore artifacts, IReadOnlyList<Guid> campaigns, CancellationToken cancellationToken)
    {
        var all = new List<FindingToAssess>();

        foreach (var campaign in campaigns)
        {
            var refs = (await store.ArtifactsAsync(campaign, cancellationToken)).Where(r => r.Class == ArtifactClass.Findings).ToList();

            foreach (var record in (await store.FactsAsync(campaign, cancellationToken)).Where(r => r.Findings.Count > 0))
            {
                var read = await TextsAsync(artifacts, record, refs, cancellationToken);
                if (read is Outcome<IReadOnlyList<FindingToAssess>>.Fail fail)
                {
                    return fail;
                }

                all.AddRange(((Outcome<IReadOnlyList<FindingToAssess>>.Ok)read).Value);
            }
        }

        return Outcome<IReadOnlyList<FindingToAssess>>.Success(all);
    }

    private static async Task<Outcome<IReadOnlyList<FindingToAssess>>> TextsAsync(IGateArtifactStore artifacts, GateRunRecord record, IReadOnlyList<ArtifactRef> refs, CancellationToken cancellationToken)
    {
        var file = refs.FirstOrDefault(r => r.CellId == record.RunId && r.Attempt == record.Attempt);
        if (file is null)
        {
            return Outcome<IReadOnlyList<FindingToAssess>>.Failure($"cell {record.RunId} has findings and no committed findings.jsonl — its text cannot be read");
        }

        var bytes = await artifacts.ReadAsync(file, cancellationToken);
        if (bytes is not Outcome<ReadOnlyMemory<byte>>.Ok { Value: var content })
        {
            return Outcome<IReadOnlyList<FindingToAssess>>.Failure($"cell {record.RunId}: {((Outcome<ReadOnlyMemory<byte>>.Fail)bytes).Reason}");
        }

        var lines = System.Text.Encoding.UTF8.GetString(content.Span).Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();
        var ordinals = record.Findings.Select(f => f.Ordinal).ToHashSet();

        return Outcome<IReadOnlyList<FindingToAssess>>.Success(
            [.. lines.Select((json, ordinal) => (json, ordinal)).Where(l => ordinals.Contains(l.ordinal))
                .Select(l => new FindingToAssess(record.CampaignId, record.RunId, l.ordinal, record.Task, record.Reviewer, l.json))]);
    }

}
