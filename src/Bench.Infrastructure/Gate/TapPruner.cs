using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bench.Application.Gate;
using Bench.Domain.Gate;

namespace Bench.Infrastructure.Gate;

/// <summary>Releases tap BODIES past the retention window — the biggest thing on the artefact disk — and keeps the
/// facts, so every number the tap contributed survives.
/// <para>
/// The tap writes three files per HTTP call under an attempt's <c>tap/</c> folder: <c>call-NN.request.json</c>,
/// <c>call-NN.response.json</c> (the bodies) and <c>call-NN.json</c> (the facts: status, finish reason, wall, the
/// token counts). The order per call is the guarantee: the facts file is flushed to disk, the release is appended
/// to <c>tap/pruned.jsonl</c> (what went, its SHA-256 and its length) and flushed, and only then is a body
/// deleted. A crash anywhere leaves the facts plus whichever bodies were not reached — never neither — and the
/// next prune finishes the job. A call with bodies and NO facts file keeps its bodies: deleting them would leave
/// nothing at all.
/// </para>
/// <para>
/// Only an attempt with a committed <c>run.json</c> is pruned. One without is a session that never finished — it
/// is listed and not touched — and one a later attempt replaced is kept whole, as the evidence of what went wrong.
/// </para></summary>
public sealed class TapPruner(string root, ArtifactProbe probe)
{
    public const string PrunedLog = "pruned.jsonl";
    private const string RequestSuffix = ".request.json";
    private const string ResponseSuffix = ".response.json";

    public TapPruneReport Prune(DateTimeOffset cutoff, bool dryRun, DateTimeOffset now) =>
        new([.. Attempts().Select(attempt => Decide(attempt, cutoff, dryRun, now))]);

    private IEnumerable<(Guid Run, Guid Cell, int Attempt, string Path)> Attempts()
    {
        var runs = Path.Combine(root, CellPaths.RunsFolder);

        if (!Directory.Exists(runs))
        {
            yield break;
        }

        foreach (var run in Children(runs))
        {
            foreach (var cell in Children(Path.Combine(run.Path, CellPaths.CellsFolder)))
            {
                foreach (var attempt in AttemptDirectories(cell.Path))
                {
                    yield return (run.Id, cell.Id, attempt.Number, attempt.Path);
                }
            }
        }
    }

    private TapPruneEntry Decide((Guid Run, Guid Cell, int Attempt, string Path) at, DateTimeOffset cutoff, bool dryRun, DateTimeOffset now)
    {
        var state = (
            Unlinked(Path.Combine(at.Path, CellPaths.TapFolder)),
            File.Exists(Path.Combine(at.Path, CellPaths.InterruptedFile)),
            File.Exists(Path.Combine(at.Path, CellPaths.RunRecordFile)));

        return state switch
        {
            (false, _, _) => new TapPruneEntry(at.Run, at.Cell, at.Attempt, TapPruneState.Linked, 0, 0),
            (_, true, _) => new TapPruneEntry(at.Run, at.Cell, at.Attempt, TapPruneState.Interrupted, 0, 0),
            (_, _, false) => new TapPruneEntry(at.Run, at.Cell, at.Attempt, TapPruneState.Unfinished, 0, 0),
            _ => Release(at, cutoff, dryRun, now),
        };
    }

    private TapPruneEntry Release((Guid Run, Guid Cell, int Attempt, string Path) at, DateTimeOffset cutoff, bool dryRun, DateTimeOffset now)
    {
        var tap = Path.Combine(at.Path, CellPaths.TapFolder);
        var calls = Calls(tap, cutoff);
        var missingFacts = calls.Any(c => !File.Exists(c.Facts));
        var releasable = calls.Where(c => File.Exists(c.Facts)).ToList();

        var (bodies, bytes) = (releasable.Sum(c => c.Bodies.Count), releasable.Sum(c => c.Bodies.Sum(b => new FileInfo(b).Length)));

        if (!dryRun)
        {
            foreach (var call in releasable)
            {
                ReleaseCall(tap, call, now);
            }
        }

        var state = (bodies, dryRun, missingFacts) switch
        {
            (0, _, true) => TapPruneState.FactsMissing,
            (0, _, false) => TapPruneState.Recent,
            (_, true, _) => TapPruneState.WouldRelease,
            _ => TapPruneState.Released,
        };

        return new TapPruneEntry(at.Run, at.Cell, at.Attempt, state, bodies, bytes);
    }

    /// <summary>Whether a tap folder is exactly where its ids say, inside the root, with no link anywhere on the way —
    /// a prune DELETES, and a junction planted at <c>tap/</c> would otherwise point it at somebody else's files.</summary>
    private bool Unlinked(string tap) =>
        ArtifactContainment.Resolve(tap).Match(
            resolved => ArtifactContainment.Same(resolved, tap) && ArtifactContainment.IsWithin(resolved, root),
            _ => false);

    /// <summary>The per-call order: facts durable, release logged durably, THEN the bodies.</summary>
    private void ReleaseCall(string tap, (string Facts, IReadOnlyList<string> Bodies) call, DateTimeOffset now)
    {
        using (var facts = new FileStream(call.Facts, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
        {
            facts.Flush(flushToDisk: true);
        }

        AppendDurably(Path.Combine(tap, PrunedLog), call.Bodies.Select(body => LogLine(body, now)));
        probe.Reached(ArtifactStep.FactsDurable);

        foreach (var body in call.Bodies)
        {
            File.Delete(body);
            probe.Reached(ArtifactStep.BodyDeleted);
        }
    }

    /// <summary>Every call with a body older than the cutoff: its facts file and the bodies to go.</summary>
    private static List<(string Facts, IReadOnlyList<string> Bodies)> Calls(string tap, DateTimeOffset cutoff) =>
        !Directory.Exists(tap)
            ? []
            : [.. Directory.EnumerateFiles(tap)
                .Where(IsBody)
                .Where(body => new FileInfo(body).LinkTarget is null)
                .Where(body => File.GetLastWriteTimeUtc(body) < cutoff.UtcDateTime)
                .GroupBy(CallStem, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => (Facts: g.Key + ".json", Bodies: (IReadOnlyList<string>)[.. g.Order(StringComparer.Ordinal)]))];

    private static bool IsBody(string file) =>
        file.EndsWith(RequestSuffix, StringComparison.Ordinal) || file.EndsWith(ResponseSuffix, StringComparison.Ordinal);

    private static string CallStem(string body) =>
        body.EndsWith(RequestSuffix, StringComparison.Ordinal) ? body[..^RequestSuffix.Length] : body[..^ResponseSuffix.Length];

    private static string LogLine(string body, DateTimeOffset now)
    {
        var bytes = File.ReadAllBytes(body);

        return JsonSerializer.Serialize(new
        {
            file = Path.GetFileName(body),
            sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            length = bytes.LongLength,
            releasedAt = now.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
        });
    }

    private static void AppendDurably(string path, IEnumerable<string> lines)
    {
        using var log = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
        log.Seek(0, SeekOrigin.End);

        var text = Encoding.UTF8.GetBytes(string.Concat(lines.Select(l => l + "\n")));
        log.Write(text);
        log.Flush(flushToDisk: true);
    }

    private static IEnumerable<(Guid Id, string Path)> Children(string directory) =>
        !Directory.Exists(directory)
            ? []
            : Directory.EnumerateDirectories(directory)
                .Select(d => (Parsed: Guid.TryParse(Path.GetFileName(d), out var id), Id: id, Path: d))
                .Where(c => c.Parsed)
                .Select(c => (c.Id, c.Path))
                .OrderBy(c => c.Path, StringComparer.Ordinal);

    public static IEnumerable<(int Number, string Path)> AttemptDirectories(string cellDirectory) =>
        !Directory.Exists(cellDirectory)
            ? []
            : Directory.EnumerateDirectories(cellDirectory, "attempt-*")
                .Select(d => (Parsed: int.TryParse(Path.GetFileName(d)["attempt-".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var n), Number: n, Path: d))
                .Where(a => a.Parsed && a.Number > 0)
                .Select(a => (a.Number, a.Path))
                .OrderBy(a => a.Number);
}
