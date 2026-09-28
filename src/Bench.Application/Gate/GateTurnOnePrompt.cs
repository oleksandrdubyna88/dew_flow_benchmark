using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

/// <summary>What a cell attempt's turn-1 prompt turned out to be.</summary>
public abstract record TurnOnePrompt
{
    private TurnOnePrompt()
    {
    }

    /// <summary>Read and verified: the text, and the SHA-256 it was committed under.</summary>
    public sealed record Present(string Text, string Sha256) : TurnOnePrompt;

    /// <summary>The attempt committed no prompt file — a CLI reviewer writes none, and a session that died early may not.</summary>
    public sealed record Absent : TurnOnePrompt;

    /// <summary>A prompt file is recorded, but the store refused it on reading (missing, or no longer its committed hash).</summary>
    public sealed record Unreadable(string Reason) : TurnOnePrompt;
}

/// <summary>The ONE rule for which file is a cell attempt's turn-1 prompt: among the artefacts it committed, class
/// <see cref="ArtifactClass.Prompt"/>, first by ordinal path. The driver's shim numbers its files in write order
/// (<c>NN-api-…</c>) and an import keeps the other harness's <c>01-</c> numbering, so the first by path is turn 1 in both.
/// Seed evidence (<see cref="GateSeedEvidence"/>) and the A/A check read through here, so they cannot
/// disagree on which file they read.
/// <para>
/// The caller passes the campaign's artefact list it already fetched: one <c>ArtifactsAsync</c> per campaign, never one
/// per cell.
/// </para></summary>
public static class GateTurnOnePrompt
{
    public static async Task<TurnOnePrompt> ReadAsync(
        IGateArtifactStore artifacts, IReadOnlyList<ArtifactRef> campaignArtifacts, Guid cellId, int attempt, CancellationToken cancellationToken)
    {
        var first = campaignArtifacts
            .Where(a => a.Class == ArtifactClass.Prompt && a.CellId == cellId && a.Attempt == attempt)
            .OrderBy(a => a.Path.Value, StringComparer.Ordinal)
            .Take(1)
            .ToList();

        return first.Count == 0 ? new TurnOnePrompt.Absent() : Read(await artifacts.ReadAsync(first[0], cancellationToken), first[0]);
    }

    private static TurnOnePrompt Read(Outcome<ReadOnlyMemory<byte>> read, ArtifactRef prompt) => read switch
    {
        Outcome<ReadOnlyMemory<byte>>.Ok ok => new TurnOnePrompt.Present(System.Text.Encoding.UTF8.GetString(ok.Value.Span), prompt.Sha256),
        Outcome<ReadOnlyMemory<byte>>.Fail fail => new TurnOnePrompt.Unreadable(fail.Reason),
        _ => throw new InvalidOperationException("unreachable"),
    };
}
