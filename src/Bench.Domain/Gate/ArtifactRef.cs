using System.Globalization;
using System.Text.RegularExpressions;
using Bench.Domain.Trace;

namespace Bench.Domain.Gate;

/// <summary>What kind of file an artefact is. The database groups and sizes by this; it never holds the bytes.</summary>
public enum ArtifactClass
{
    Request,
    Reply,
    Stderr,
    Ledger,
    Prompt,
    Answer,
    Findings,
    TapRequest,
    TapResponse,
    TapFacts,
    RunRecord,
    Other,
}

/// <summary>A file in the artefact root, as the database knows it: where it is (relative to the root), which cell
/// attempt wrote it, and the SHA-256 and length of the bytes that were committed.
/// <para>
/// The database holds THIS, never the file: the text inside is private — prompts, answers, findings that quote
/// code — and stays outside git and outside every published table. A ref re-read later hashes to what was
/// written, or the artefact changed under it and the store says so.
/// </para></summary>
public sealed partial record ArtifactRef
{
    private ArtifactRef(Guid runId, Guid cellId, int attempt, ArtifactClass kind, ArtifactPath path, string sha256, long length)
    {
        RunId = runId;
        CellId = cellId;
        Attempt = attempt;
        Class = kind;
        Path = path;
        Sha256 = sha256;
        Length = length;
    }

    public Guid RunId { get; }

    public Guid CellId { get; }

    public int Attempt { get; }

    public ArtifactClass Class { get; }

    public ArtifactPath Path { get; }

    /// <summary>Lower-case hex SHA-256 of the committed bytes.</summary>
    public string Sha256 { get; }

    public long Length { get; }

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Sha256Hex { get; }

    public static Outcome<ArtifactRef> Of(ArtifactScope scope, ArtifactClass kind, ArtifactPath path, string? sha256, long length)
    {
        var digest = (sha256 ?? string.Empty).Trim().ToLowerInvariant();

        var refusal = (CellPaths.Allows(scope, path), Sha256Hex.IsMatch(digest), length >= 0) switch
        {
            (false, _, _) => $"{path} is outside cell {scope.CellId}'s attempt {scope.Attempt} — a ref names a file its own attempt wrote",
            (_, false, _) => $"'{digest}' is not a SHA-256 — 64 hex characters of the committed bytes",
            (_, _, false) => $"a length is zero or more bytes, got {length}",
            _ => string.Empty,
        };

        return refusal.Length > 0
            ? Outcome<ArtifactRef>.Failure(refusal)
            : Outcome<ArtifactRef>.Success(new ArtifactRef(scope.Run.Id, scope.CellId, scope.Attempt, kind, path, digest, length));
    }

    /// <summary>Rebuilt from storage. The path is re-parsed rather than trusted: a row edited by hand to climb out
    /// of the root is refused by name rather than followed.</summary>
    public static Outcome<ArtifactRef> Stored(Guid runId, Guid cellId, int attempt, ArtifactClass kind, string? path, string? sha256, long length) =>
        ArtifactPath.Parse(path).Match(
            parsed => Outcome<ArtifactRef>.Success(new ArtifactRef(runId, cellId, attempt, kind, parsed, (sha256 ?? string.Empty).Trim(), length)),
            Outcome<ArtifactRef>.Failure);
}

/// <summary>How much of the disk a run's artefacts hold — or the honest statement that it could not be measured.
/// A footprint that failed to read is <i>unknown</i>, never zero: zero reads as "this run costs nothing", which is
/// the one wrong answer about the largest growth surface this benchmark has.</summary>
public sealed record ArtifactFootprint(CapturedCount Bytes, CapturedCount Files, CapturedCount TapBytes)
{
    public static ArtifactFootprint Unknown(string reason) =>
        new(CapturedCount.Unavailable(reason), CapturedCount.Unavailable(reason), CapturedCount.Unavailable(reason));

    /// <summary>What every run prints: <c>18.4 MB in 212 file(s), 16.9 MB of it tap bodies</c>, or <c>unknown (why)</c>.</summary>
    public string Describe => (Bytes.WasCaptured, Files.WasCaptured, TapBytes.WasCaptured) switch
    {
        (true, true, true) => $"{Size(Bytes.Value)} in {Files.Value.ToString(CultureInfo.InvariantCulture)} file(s), {Size(TapBytes.Value)} of it tap bodies",
        (true, true, false) => $"{Size(Bytes.Value)} in {Files.Value.ToString(CultureInfo.InvariantCulture)} file(s), tap share unknown",
        _ => $"unknown ({(Bytes.WasCaptured ? Files.Reason : Bytes.Reason)})",
    };

    private static string Size(long bytes) => bytes switch
    {
        < 1024 => $"{bytes.ToString(CultureInfo.InvariantCulture)} B",
        < 1024 * 1024 => $"{(bytes / 1024d).ToString("0.0", CultureInfo.InvariantCulture)} KB",
        < 1024L * 1024 * 1024 => $"{(bytes / (1024d * 1024)).ToString("0.0", CultureInfo.InvariantCulture)} MB",
        _ => $"{(bytes / (1024d * 1024 * 1024)).ToString("0.00", CultureInfo.InvariantCulture)} GB",
    };
}
