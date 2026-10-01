using Bench.Application.Probes;
using Bench.Domain;
using Bench.Domain.Probes;
using Bench.Infrastructure.Gate;

namespace Bench.Infrastructure.Probes;

/// <summary>The probes' artefacts under the artefact root — <see cref="IProbeArtifacts"/>' adapter, over the same
/// <see cref="ArtifactCommit"/> protocol the gate store uses (stage → flush → hash → rename; committed once, never overwritten)
/// and the same containment (<see cref="ArtifactContainment"/>: resolved on the real filesystem, links included, inside the
/// root or refused). The root is the gate's artefact root, already checked to sit outside every git checkout by
/// <see cref="FileSystemGateArtifactStore.Open"/> — the probes add a <c>probes/</c> tree beside its <c>runs/</c>.</summary>
public sealed class ProbeArtifacts(string artifactRoot) : IProbeArtifacts
{
    private readonly string _root = Path.GetFullPath(artifactRoot);

    public string Root => _root;

    public async Task<Outcome<ProbeArtifact>> CommitAsync(ProbeAttemptScope scope, ProbeArtifactKind kind, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        var path = ProbePaths.Artifact(scope, kind);
        var contained = ArtifactContainment.Resolve(Path.Combine([_root, .. path.Segments]));

        if (contained is not Outcome<string>.Ok { Value: var full } || !ArtifactContainment.IsWithin(full, _root))
        {
            return Outcome<ProbeArtifact>.Failure($"{path} resolves outside the artefact root — a link on the way leads elsewhere, and a write through it is refused");
        }

        return (await ArtifactCommit.CommitAsync(full, bytes, ArtifactProbe.None, cancellationToken)).Match(
            committed => ProbeArtifact.Of(kind, path, committed.Sha256, committed.Length),
            reason => Outcome<ProbeArtifact>.Failure($"{path} {reason}"));
    }

    /// <summary><c>bench probes prune --run</c>'s deletion: every cell folder under <c>probes/&lt;run&gt;/</c> of the artefact root, then
    /// that folder — nothing above it, no link followed (<see cref="ProbeTrees"/>). The caller has already flagged the run pruned
    /// through the guarded UPDATE that refuses an open run (finding 5). Answers how many cell folders went; a run with no folder
    /// (nothing was ever committed) is zero, not a refusal.</summary>
    public Outcome<int> DeleteRun(Guid runId)
    {
        var runRoot = Path.Combine([_root, .. ProbePaths.RunRoot(runId).Segments]);

        return (Directory.Exists(runRoot), Directory.Exists(runRoot) && ProbeTrees.IsUnder(runRoot, _root)) switch
        {
            (false, _) => Outcome<int>.Success(0),
            (_, false) => Outcome<int>.Failure($"{ProbePaths.RunRoot(runId)} resolves outside the artefact root — a link on the way leads elsewhere, and nothing was deleted through it"),
            _ => ProbeTrees.RemoveAll(runRoot),
        };
    }
}
