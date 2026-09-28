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

        // Line n IS ordinal n (the driver writes one line per reply finding, in reply order) — so the raw line position is
        // the index, never a position after dropping blanks, and each line is CHECKED against the stored finding.
        var lines = System.Text.Encoding.UTF8.GetString(content.Span).Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var stranger = record.Findings.FirstOrDefault(f => f.Ordinal >= lines.Count || StableHash.Of(TextOf(lines[f.Ordinal])) != f.TextHash);

        return stranger is not null
            ? Outcome<IReadOnlyList<FindingToAssess>>.Failure(
                $"cell {record.RunId}: line {stranger.Ordinal} of its findings.jsonl does not hash to the finding the database stored for that ordinal — "
                + "refused rather than judging one finding under another's identity")
            : Outcome<IReadOnlyList<FindingToAssess>>.Success(
                [.. record.Findings.Select(f => new FindingToAssess(record.CampaignId, record.RunId, f.Ordinal, record.Task, record.Reviewer, lines[f.Ordinal]))]);
    }

    /// <summary>A line's finding text exactly as the driver hashed it — read by the one reply parser, not by a second copy
    /// of its rules.</summary>
    private static string TextOf(string line) =>
        GateReplyParser.Parse($"{{\"verdict\":\"revise\",\"findings\":[{(line.Trim().Length > 0 ? line : "{}")}]}}") is { Findings: [var finding] }
            ? finding.Text
            : string.Empty;
}
