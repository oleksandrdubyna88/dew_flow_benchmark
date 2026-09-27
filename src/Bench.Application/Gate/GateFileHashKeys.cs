using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

/// <summary>The one decision about the file-hash key: read it, create it on first use, or REFUSE.
/// <para>
/// The key exists so a published <see cref="GateFinding.FileHash"/> cannot be confirmed by hashing guessed paths,
/// and it only works if every hash in the database was made under the same key. So a root that has lost its key
/// while the database still holds hashes is refused rather than given a fresh one: a silently regenerated key
/// makes every later "same file" comparison against the earlier findings false, with nothing on the page to say
/// so. The fix is to restore the file from wherever the artefact root is backed up, which is a person's decision.
/// </para></summary>
public static class GateFileHashKeys
{
    public static async Task<Outcome<FileHashKey>> ResolveAsync(
        IGateArtifactStore artifacts, IGateStore store, CancellationToken cancellationToken) =>
        await artifacts.ReadFileHashKeyAsync(cancellationToken) switch
        {
            FileHashKeyRead.Present present => Outcome<FileHashKey>.Success(present.Key),
            FileHashKeyRead.Unusable unusable => Outcome<FileHashKey>.Failure(
                $"the artefact root's file-hash key cannot be used — {unusable.Reason}; restore it from the root's backup"),
            _ => await CreateUnlessHashedAsync(artifacts, store, cancellationToken),
        };

    private static async Task<Outcome<FileHashKey>> CreateUnlessHashedAsync(
        IGateArtifactStore artifacts, IGateStore store, CancellationToken cancellationToken) =>
        await store.HasFindingsAsync(cancellationToken)
            ? Outcome<FileHashKey>.Failure(
                "the artefact root has no file-hash key, and the database already holds findings hashed under one — "
                + "a new key would make every file hash stored so far incomparable with every later one; restore the "
                + "key file from the root's backup, or point --artifact-root at the root those findings were written from")
            : await CreateOrReadWinnerAsync(artifacts, cancellationToken);

    /// <summary>Two workers starting on one fresh root both see no key; the file is created exclusively, so one
    /// of them loses — and the loser reads the winner's key rather than failing the campaign.</summary>
    private static async Task<Outcome<FileHashKey>> CreateOrReadWinnerAsync(
        IGateArtifactStore artifacts, CancellationToken cancellationToken)
    {
        var created = await artifacts.CreateFileHashKeyAsync(cancellationToken);

        return created is Outcome<FileHashKey>.Ok
            ? created
            : await artifacts.ReadFileHashKeyAsync(cancellationToken) is FileHashKeyRead.Present winner
                ? Outcome<FileHashKey>.Success(winner.Key)
                : created;
    }
}
