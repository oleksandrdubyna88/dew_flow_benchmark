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
}
