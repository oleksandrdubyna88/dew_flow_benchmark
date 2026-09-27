using System.Globalization;
using System.Text.RegularExpressions;

namespace Bench.Domain.Gate;

/// <summary>A path RELATIVE to the artefact root, in its one spelling: <c>/</c>-separated segments of letters,
/// digits, <c>.</c>, <c>_</c> and <c>-</c>, none of them empty, <c>.</c> or <c>..</c>.
/// <para>
/// The artefact root holds private text — prompts, answers, findings that quote code — so a path that reaches
/// outside it is not a bug but a leak. Refusing <c>..</c>, a rooted path, a drive, a url and every character a
/// shell or a filesystem gives meaning to at PARSE time is the first line; the store resolves the result
/// against the real filesystem (links included) as the second. The segments are the only place an id enters
/// a path, and they are why a task id or a file name can never walk up the tree.
/// </para></summary>
public sealed partial record ArtifactPath
{
    private ArtifactPath(IReadOnlyList<string> segments) => Segments = segments;

    public IReadOnlyList<string> Segments { get; }

    public string Value => string.Join('/', Segments);

    [GeneratedRegex("^[A-Za-z0-9_-][A-Za-z0-9._-]{0,127}$")]
    private static partial Regex Segment { get; }

    public static Outcome<ArtifactPath> Parse(string? relative)
    {
        var text = relative ?? string.Empty;
        var segments = text.Split('/');

        var refusal = (text.Length, segments.FirstOrDefault(s => !IsSegment(s))) switch
        {
            (0, _) => "an artefact path names a file under the root — got nothing",
            (_, { } bad) => $"'{Printable(text)}' is not an artefact path — segment '{Printable(bad)}' is empty, '.', '..', "
                            + "or carries a character outside letters, digits, '.', '_' and '-'; a path under the artefact "
                            + "root is relative, '/'-separated, and never climbs",
            _ => string.Empty,
        };

        return refusal.Length > 0
            ? Outcome<ArtifactPath>.Failure(refusal)
            : Outcome<ArtifactPath>.Success(new ArtifactPath(segments));
    }

    /// <summary>This path with more segments under it — each one parsed, so a joined name cannot climb either.</summary>
    public Outcome<ArtifactPath> Then(string relative) => Parse($"{Value}/{relative}");

    /// <summary>Whether this path is <paramref name="root"/> or lies under it, segment by segment — never by
    /// string prefix, which would put <c>attempt-10</c> under <c>attempt-1</c>.</summary>
    public bool IsUnder(ArtifactPath root) =>
        Segments.Count >= root.Segments.Count
        && root.Segments.Select((segment, i) => string.Equals(segment, Segments[i], StringComparison.OrdinalIgnoreCase)).All(same => same);

    public override string ToString() => Value;

    /// <summary>Two paths are one path when they spell one — value equality over the segments, not over the list
    /// object that holds them.</summary>
    public bool Equals(ArtifactPath? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    private static bool IsSegment(string segment) => segment is not ("." or "..") && Segment.IsMatch(segment);

    private static string Printable(string text) => text.Length <= 80 ? text : text[..80] + "…";
}

/// <summary>Which cell attempt an artefact write belongs to — the run (for its data-directory mode), the cell,
/// and the attempt number the claim gave it. Everything the store may write for this attempt is derived from
/// it by <see cref="CellPaths"/>.</summary>
public sealed record ArtifactScope(GateRun Run, Guid CellId, int Attempt)
{
    public static Outcome<ArtifactScope> Of(GateRun run, GateCell cell) =>
        cell.Attempts >= 1 && cell.RunId == run.Id
            ? Outcome<ArtifactScope>.Success(new ArtifactScope(run, cell.Id, cell.Attempts))
            : Outcome<ArtifactScope>.Failure(
                $"{cell.Subject} has no attempt of run {run.Id} to write for — an artefact belongs to a CLAIMED cell's attempt");
}

/// <summary>The one place a gate artefact's location is decided. Every path is relative to the artefact root
/// and built from ids alone:
/// <code>
/// runs/&lt;runId&gt;/cells/&lt;cellId&gt;/attempt-&lt;n&gt;/            one cell attempt: request, reply, stderr, run.json, tap/
/// runs/&lt;runId&gt;/cells/&lt;cellId&gt;/attempt-&lt;n&gt;/data       COAI_DATA_DIR of an ISOLATED run
/// runs/&lt;runId&gt;/data-shared                               COAI_DATA_DIR of a SHARED run, every cell
/// </code>
/// A later attempt of a cell is a NEW directory; the earlier one is kept and marked, never reused.</summary>
public static class CellPaths
{
    public const string RunsFolder = "runs";
    public const string CellsFolder = "cells";
    public const string DataFolder = "data";
    public const string SharedDataFolder = "data-shared";
    public const string TapFolder = "tap";

    /// <summary>The file whose presence says an attempt's product session was written out whole. It is the LAST
    /// artefact an attempt commits.</summary>
    public const string RunRecordFile = "run.json";

    /// <summary>The marker a later attempt leaves in an earlier one it replaced: kept, never continued.</summary>
    public const string InterruptedFile = "interrupted.json";

    public static ArtifactPath RunRoot(Guid runId) => Path(RunsFolder, Id(runId));

    public static ArtifactPath CellRoot(Guid runId, Guid cellId) => Path(RunsFolder, Id(runId), CellsFolder, Id(cellId));

    public static ArtifactPath AttemptRoot(ArtifactScope scope) =>
        Path(RunsFolder, Id(scope.Run.Id), CellsFolder, Id(scope.CellId), AttemptFolder(scope.Attempt));

    public static string AttemptFolder(int attempt) => $"attempt-{attempt.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>The cell attempt's <c>COAI_DATA_DIR</c>, from the run's STORED mode — the one function that decides
    /// it, so an isolated cell and a shared run can never resolve to each other's directory.</summary>
    public static ArtifactPath DataDirFor(GateRun run, Guid cellId, int attempt) =>
        run.Mode == DataDirMode.Shared
            ? Path(RunsFolder, Id(run.Id), SharedDataFolder)
            : Path(RunsFolder, Id(run.Id), CellsFolder, Id(cellId), AttemptFolder(attempt), DataFolder);

    public static ArtifactPath DataDirFor(ArtifactScope scope) => DataDirFor(scope.Run, scope.CellId, scope.Attempt);

    /// <summary>Whether this attempt may write <paramref name="path"/>: under its own attempt root or its own data
    /// directory, and nowhere else. An isolated cell cannot reach <c>data-shared</c>; a shared run cannot reach a
    /// cell's private <c>data</c> directory, even its own; no cell can reach another cell or another attempt.</summary>
    public static bool Allows(ArtifactScope scope, ArtifactPath path) =>
        path.IsUnder(DataDirFor(scope))
        || (path.IsUnder(AttemptRoot(scope)) && !path.IsUnder(PrivateDataDir(scope)));

    /// <summary>The roots an attempt may write under — what the store resolves on disk and holds a write to.</summary>
    public static IReadOnlyList<ArtifactPath> WritableRoots(ArtifactScope scope) =>
        scope.Run.Mode == DataDirMode.Shared ? [AttemptRoot(scope), DataDirFor(scope)] : [AttemptRoot(scope)];

    private static ArtifactPath PrivateDataDir(ArtifactScope scope) =>
        Path(RunsFolder, Id(scope.Run.Id), CellsFolder, Id(scope.CellId), AttemptFolder(scope.Attempt), DataFolder);

    private static string Id(Guid id) => id.ToString("D", CultureInfo.InvariantCulture);

    private static ArtifactPath Path(params string[] segments) =>
        ArtifactPath.Parse(string.Join('/', segments)).Match(
            path => path,
            reason => throw new InvalidOperationException($"a path built from ids alone failed to parse — {reason}"));
}
