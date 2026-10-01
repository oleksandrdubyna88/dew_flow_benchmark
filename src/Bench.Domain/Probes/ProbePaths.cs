using System.Globalization;
using Bench.Domain.Gate;
using Bench.Domain.Runs;

namespace Bench.Domain.Probes;

/// <summary>Which attempt of which cell a fixture or an artefact belongs to — the run, the cell, the GENERATION and the
/// attempt the claim gave it (<see cref="ArtifactScope"/>'s shape over the probe axes). Only a CLAIMED cell has one.</summary>
public sealed record ProbeAttemptScope(Guid RunId, Guid CellId, int Generation, int Attempt)
{
    public static Outcome<ProbeAttemptScope> Of(ProbeCell cell) =>
        cell.State == CellState.Claimed && cell.Attempts >= 1
            ? Outcome<ProbeAttemptScope>.Success(new ProbeAttemptScope(cell.RunId, cell.Id, cell.Generation, cell.Attempts))
            : Outcome<ProbeAttemptScope>.Failure($"{cell.Label} is {cell.State} at attempt {cell.Attempts} — a fixture and an artefact belong to a CLAIMED cell's attempt");
}

/// <summary>Which files an attempt's fixture holds: <c>cwd/inside.txt</c> (the IN token) and <c>outside/canary.txt</c> (the OUT token).</summary>
public readonly record struct ProbeFixtureLayout(bool Inside, bool Outside);

/// <summary>The one place a probe attempt's location is decided (§4, §6) — under the WORK root for the fixture and under the
/// ARTEFACT root for the files it leaves, the same relative path in both:
/// <code>
/// probes/&lt;runId&gt;/&lt;cellId&gt;/g&lt;generation&gt;/a&lt;attempt&gt;/            one attempt
/// probes/&lt;runId&gt;/&lt;cellId&gt;/g&lt;generation&gt;/a&lt;attempt&gt;/cwd         the CLI's working directory (inside.txt where the probe wants it)
/// probes/&lt;runId&gt;/&lt;cellId&gt;/g&lt;generation&gt;/a&lt;attempt&gt;/outside     canary.txt — outside the working directory
/// </code>
/// Built from ids alone, as <see cref="CellPaths"/> builds the gate's, so nothing a subject answers can reach a path. The
/// cleanup of §6 (finding 3) is keyed to the CELL folder: every folder under a run's root names a cell id, and one whose cell
/// is not Claimed by a live owner on this host is deleted whole.</summary>
public static class ProbePaths
{
    public const string Folder = "probes";
    public const string CwdFolder = "cwd";
    public const string InsideFile = "inside.txt";
    public const string OutsideFolder = "outside";
    public const string CanaryFile = "canary.txt";

    public static ArtifactPath RunRoot(Guid runId) => Path(Folder, Id(runId));

    public static ArtifactPath CellRoot(Guid runId, Guid cellId) => Path(Folder, Id(runId), Id(cellId));

    public static ArtifactPath AttemptRoot(ProbeAttemptScope scope) =>
        Path(Folder, Id(scope.RunId), Id(scope.CellId), "g" + Invariant(scope.Generation), "a" + Invariant(scope.Attempt));

    /// <summary>Where one of the attempt's three artefacts is committed, under the artefact root.</summary>
    public static ArtifactPath Artifact(ProbeAttemptScope scope, ProbeArtifactKind kind) =>
        AttemptRoot(scope).Then(FileName(kind)).Match(p => p, reason => throw new InvalidOperationException($"a path built from ids alone failed to parse — {reason}"));

    public static string FileName(ProbeArtifactKind kind) => kind switch
    {
        ProbeArtifactKind.Answer => "answer.txt",
        ProbeArtifactKind.Stdout => "stdout.txt",
        ProbeArtifactKind.Stderr => "stderr.txt",
        ProbeArtifactKind.Argv => "argv.json",
        ProbeArtifactKind.Prompt => "prompt.txt",
        ProbeArtifactKind.Tools => "tools.json",
        _ => "fault.txt",
    };

    /// <summary>§4: the read probes have both files; the web search runs in an EMPTY <c>cwd/</c> with no canary anywhere; the
    /// control and the confined row run in an empty <c>cwd/</c> with the canary outside it.</summary>
    public static ProbeFixtureLayout Layout(ProbeKind probe) => probe switch
    {
        ProbeKind.ReadInside or ProbeKind.ReadOutsideBare or ProbeKind.ReadOutsideGranted => new(Inside: true, Outside: true),
        ProbeKind.ReadDenied or ProbeKind.WebConfined => new(Inside: false, Outside: true),
        _ => new(Inside: false, Outside: false),
    };

    /// <summary>The cell a folder under a run's root belongs to — its name is the cell id, or it is not a cell folder at all.</summary>
    public static Outcome<Guid> CellOf(string folderName) =>
        Guid.TryParseExact(folderName, "D", out var id) ? Outcome<Guid>.Success(id) : Outcome<Guid>.Failure($"'{folderName}' is not a cell folder — a cell folder is named by its id");

    private static string Id(Guid id) => id.ToString("D", CultureInfo.InvariantCulture);

    private static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static ArtifactPath Path(params string[] segments) =>
        ArtifactPath.Parse(string.Join('/', segments)).Match(
            path => path,
            reason => throw new InvalidOperationException($"a path built from ids alone failed to parse — {reason}"));
}
