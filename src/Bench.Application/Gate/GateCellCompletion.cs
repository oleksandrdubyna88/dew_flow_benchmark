using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Runs;

namespace Bench.Application.Gate;

/// <summary>One file an attempt commits: where under the attempt it goes, what it is, and its bytes.</summary>
public sealed record PendingArtifact(ArtifactClass Class, ArtifactPath Path, ReadOnlyMemory<byte> Bytes);

/// <summary>The commit protocol for a cell attempt — the ORDER is the guarantee.
/// <list type="number">
/// <item>every artefact is staged, flushed to disk, hashed and renamed into place, the run record LAST;</item>
/// <item>every <see cref="ArtifactRef"/> is persisted, in one transaction;</item>
/// <item>only then is the cell settled.</item>
/// </list>
/// A process that dies anywhere in there leaves the cell CLAIMED, never settled over artefacts that are not all
/// on disk and recorded — and a claimed cell whose owner is gone is swept, re-claimed at the next attempt number,
/// and begun in a NEW attempt directory, while the interrupted one is marked and kept. No step can leave a cell
/// settled-but-missing-its-evidence, and none can leave it stuck.
/// </summary>
public sealed class GateCellCompletion(IGateArtifactStore artifacts, IGateStore store)
{
    public async Task<Outcome<GateCell>> CompleteAsync(
        ArtifactScope scope,
        WorkerIdentity owner,
        IReadOnlyList<PendingArtifact> files,
        PendingArtifact runRecord,
        GateSettlement settlement,
        CancellationToken cancellationToken)
    {
        if (runRecord.Class != ArtifactClass.RunRecord)
        {
            return Outcome<GateCell>.Failure(
                $"the last artefact an attempt commits is its run record, got {runRecord.Class} — its presence is what says the attempt was written out whole");
        }

        var written = await WriteAllAsync(scope, [.. files, runRecord], cancellationToken);

        return written switch
        {
            Outcome<IReadOnlyList<ArtifactRef>>.Ok refs => await RecordThenSettleAsync(scope, owner, refs.Value, settlement, cancellationToken),
            Outcome<IReadOnlyList<ArtifactRef>>.Fail fail => Outcome<GateCell>.Failure(
                $"cell {scope.CellId} attempt {scope.Attempt} was not settled — {fail.Reason}"),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    private async Task<Outcome<GateCell>> RecordThenSettleAsync(
        ArtifactScope scope, WorkerIdentity owner, IReadOnlyList<ArtifactRef> refs, GateSettlement settlement, CancellationToken cancellationToken) =>
        await store.RecordArtifactsAsync(refs, cancellationToken) switch
        {
            Outcome<int>.Ok => await store.SettleAsync(scope.CellId, owner, settlement, cancellationToken),
            Outcome<int>.Fail fail => Outcome<GateCell>.Failure(
                $"cell {scope.CellId} attempt {scope.Attempt} was not settled — its artefact refs were not recorded: {fail.Reason}"),
            _ => throw new InvalidOperationException("unreachable"),
        };

    private async Task<Outcome<IReadOnlyList<ArtifactRef>>> WriteAllAsync(
        ArtifactScope scope, IReadOnlyList<PendingArtifact> files, CancellationToken cancellationToken)
    {
        var refs = new List<ArtifactRef>(files.Count);

        foreach (var file in files)
        {
            var written = await artifacts.WriteAsync(scope, file.Class, file.Path, file.Bytes, cancellationToken);

            if (written is Outcome<ArtifactRef>.Fail fail)
            {
                return Outcome<IReadOnlyList<ArtifactRef>>.Failure($"{file.Path} was not committed: {fail.Reason}");
            }

            refs.Add(((Outcome<ArtifactRef>.Ok)written).Value);
        }

        return Outcome<IReadOnlyList<ArtifactRef>>.Success(refs);
    }
}
