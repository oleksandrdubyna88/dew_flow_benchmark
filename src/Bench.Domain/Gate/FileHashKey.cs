using System.Security.Cryptography;
using System.Text;

namespace Bench.Domain.Gate;

/// <summary>The key a finding's file hash is computed under — HMAC-SHA256, never a plain digest.
/// <para>
/// A plain SHA-256 of a repository path is not private: the paths of a repository are few and guessable
/// (<c>src/Orders/OrderService.cs</c>), so anyone holding a published hash can confirm which file a finding
/// named by hashing candidates until one matches. Keyed, the same guess needs the key, and the key lives ONLY
/// in the artefact root — never in git, never in the database, never on a page — so a published
/// <see cref="GateFinding.FileHash"/> stays good for "same file or not" and useless for "which file".
/// </para>
/// <para>
/// The domain takes the key as a VALUE. Creating it on first use, reading it back from the artefact root and
/// refusing a root without one are the store's and the driver's jobs (E2/E3); a class rather than a record
/// because it holds a secret — no value equality, no generated <c>ToString</c> that could print it.
/// </para></summary>
public sealed class FileHashKey
{
    /// <summary>HMAC-SHA256's own block-sized minimum is not required, but a key shorter than the digest is a
    /// key somebody can search.</summary>
    public const int MinBytes = 32;

    private readonly byte[] _key;

    private FileHashKey(byte[] key) => _key = key;

    public static Outcome<FileHashKey> Of(ReadOnlySpan<byte> key) =>
        key.Length >= MinBytes
            ? Outcome<FileHashKey>.Success(new FileHashKey(key.ToArray()))
            : Outcome<FileHashKey>.Failure(
                $"a file-hash key is at least {MinBytes} bytes, got {key.Length} — a short key is one a published hash can be searched under");

    /// <summary>Lower-case hex HMAC-SHA256 of <paramref name="text"/> under this key.</summary>
    public string Hash(string text) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(text)));

    public override string ToString() => "FileHashKey(redacted)";
}

/// <summary>The one normal form of a repository-relative path a finding names, so "same file" does not depend
/// on how the reviewer spelled it: <c>.\src\A.cs</c>, <c>./src/A.cs</c>, <c>/src/A.cs</c> and
/// <c>src/A.cs/</c> are one file. Backslashes become slashes, and empty and <c>.</c> segments are dropped —
/// which strips a leading <c>./</c>, trims leading and trailing slashes and folds a doubled one.</summary>
public static class FindingPath
{
    public static string Normalise(string? path) =>
        string.Join('/', (path ?? string.Empty).Trim().Replace('\\', '/')
            .Split('/')
            .Where(segment => segment.Length > 0 && segment != "."));
}
