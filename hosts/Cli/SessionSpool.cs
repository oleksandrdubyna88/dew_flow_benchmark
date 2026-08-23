using System.Text.Json;
using Bench.Application.Sessions;
using Bench.Contracts;

namespace Bench.Cli;

/// <summary>`bench sessions ingest` — draining what a hook client could not deliver.
/// <para>
/// One spool, on the EMITTER's side, and none on the collector's. A hook that could not post has the event
/// in its hand and a disk under it; a collector that could not store one has to have received it first. So
/// the single fallback covers both failures — collector down and database down — and this is its single
/// drain.
/// </para>
/// <para>
/// Read-store-rename, exactly as the telemetry spool does, and for the same guarantee: a host killed
/// between the store and the rename re-reads the file and finds every event already known, which is why a
/// duplicate is an ordinary outcome here rather than a warning.
/// </para></summary>
public static class SessionSpool
{
    /// <summary>Marks a file as drained. A rename rather than a delete: these events are somebody's only
    /// record of an afternoon until they say otherwise.</summary>
    public const string IngestedSuffix = ".ingested";

    public static async Task<int> IngestAsync(
        CommandLine command,
        ISessionStore store,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var spool = command.Value("spool", Default());

        if (!Directory.Exists(spool))
        {
            return SessionsCommand.Fail(error, $"spool directory not found: {spool}", ExitCodes.Environment);
        }

        var files = Directory.GetFiles(spool, "*.jsonl", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToList();

        if (files.Count == 0)
        {
            output.WriteLine($"nothing to ingest ({spool})");
            return ExitCodes.Pass;
        }

        var report = new Tally();
        foreach (var file in files)
        {
            await DrainAsync(file, store, report, cancellationToken);
        }

        Write(command, output, report, spool);

        // A refused event is a real finding — a client writing a version this build cannot read, or a
        // corrupt spool — and it must not read as a clean pass.
        return report.Refused > 0 ? ExitCodes.Regression : ExitCodes.Pass;
    }

    /// <summary>One file: read, store, then retire it — unless a LATER build could still read it.
    /// <para>
    /// A file carrying a retryable refusal is left alone. Refusing an unknown schema version by name so
    /// somebody can ship a build that reads it, and then retiring the file that held those events, is a
    /// promise broken in the same breath as it is made.
    /// </para></summary>
    private static async Task DrainAsync(
        string file, ISessionStore store, Tally report, CancellationToken cancellationToken)
    {
        var retryable = false;
        var name = Path.GetFileName(file);

        foreach (var (line, number) in Lines(file))
        {
            retryable |= await AcceptAsync(line, number, name, store, report, cancellationToken);
        }

        if (!retryable)
        {
            File.Move(file, file + IngestedSuffix, overwrite: true);
            return;
        }

        report.Retained++;
        report.Reasons.Add($"{name} kept for a later build — it holds events this one cannot read");
    }

    /// <summary>True when this line means the FILE must survive.</summary>
    private static async Task<bool> AcceptAsync(
        string line,
        int number,
        string file,
        ISessionStore store,
        Tally report,
        CancellationToken cancellationToken)
    {
        switch (SessionCodec.ReadLine(line))
        {
            case SessionVerdict.Read read:
                var outcome = await store.RecordAsync(read.Record, cancellationToken);
                report.Count(outcome.Duplicate);
                return false;

            case SessionVerdict.UnknownVersion unknown:
                report.Refuse($"{file} line {number}: {unknown.Reason}");
                return true;

            case SessionVerdict.Unreadable unreadable:
                report.Refuse($"{file} line {number}: {unreadable.Reason}");
                return false;

            default:
                report.Refuse($"{file} line {number}: unclassified session event");
                return false;
        }
    }

    private static IEnumerable<(string Line, int Number)> Lines(string file) =>
        File.ReadLines(file)
            .Select((line, index) => (Line: line.Trim(), Number: index + 1))
            .Where(x => x.Line.Length > 0);

    private static void Write(CommandLine command, TextWriter output, Tally report, string spool)
    {
        if (command.Has("json"))
        {
            output.WriteLine(JsonSerializer.Serialize(
                new { spool, report.Ingested, report.Duplicate, report.Refused, report.Retained, report.Reasons },
                new JsonSerializerOptions { WriteIndented = true }));
            return;
        }

        output.WriteLine(
            $"ingested {report.Ingested}, duplicate {report.Duplicate}, refused {report.Refused}, " +
            $"retained {report.Retained} file(s)");

        foreach (var reason in report.Reasons)
        {
            output.WriteLine($"refused  {reason}");
        }
    }

    /// <summary>Where the hook client writes when it cannot deliver. The same default on both sides,
    /// resolved from one constant, so an operator who set neither still has one path rather than two.</summary>
    public static string Default() =>
        Environment.GetEnvironmentVariable(SessionCollector.SpoolVariable) is { Length: > 0 } configured
            ? configured
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "bench",
                "sessions-spool");

    /// <summary>The running totals. A class rather than a record struct because it is accumulated across
    /// files — and because the equality trap the telemetry <c>IngestReport</c> documents is not worth
    /// inheriting for something nothing ever compares.</summary>
    private sealed class Tally
    {
        public int Ingested { get; private set; }

        public int Duplicate { get; private set; }

        public int Refused { get; private set; }

        public int Retained { get; set; }

        public List<string> Reasons { get; } = [];

        public void Count(bool duplicate)
        {
            if (duplicate)
            {
                Duplicate++;
                return;
            }

            Ingested++;
        }

        public void Refuse(string reason)
        {
            Refused++;
            Reasons.Add(reason);
        }
    }
}
