using System.Text.Json;
using Bench.Application;
using Bench.Domain.Telemetry;

namespace Bench.Cli;

/// <summary>`bench telemetry ingest`, `report` and `prune` — draining the spools an MCP server writes,
/// reading what they say, and retiring what has been read.
/// <para>
/// The spool is the transport on purpose. A server that had to reach this host to record a call would
/// couple a product's tool surface to a benchmark being up; a file it appends to and someone else
/// drains couples them to nothing. Ingest is therefore idempotent and resumable: a file is retired
/// only after every line of it is committed, and re-reading one changes nothing.
/// </para></summary>
public static class TelemetryCommand
{
    /// <summary>Marks a spool file as drained. A rename rather than a delete: the data is somebody's
    /// only copy until they say otherwise, and a benchmark is not the right component to decide that.</summary>
    public const string IngestedSuffix = ".ingested";

    public static async Task<int> RunAsync(
        CommandLine command,
        ITelemetryStore store,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken) =>
        command.Operand(0) switch
        {
            "ingest" => await IngestAsync(command, store, output, error, cancellationToken),
            "report" => await ReportAsync(command, store, output, cancellationToken),
            "prune" => Prune(command, output, error),
            var other => Fail(
                error,
                other.Length == 0
                    ? "bench telemetry needs an action — 'ingest', 'report' or 'prune'"
                    : $"unknown telemetry action '{other}' — try 'ingest', 'report' or 'prune'",
                ExitCodes.Configuration),
        };

    private static async Task<int> IngestAsync(
        CommandLine command,
        ITelemetryStore store,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var spool = command.Value("spool");
        if (spool.Length == 0)
        {
            return Fail(error, "--spool is required", ExitCodes.Configuration);
        }

        if (!Directory.Exists(spool))
        {
            return Fail(error, $"spool directory not found: {spool}", ExitCodes.Environment);
        }

        var files = Directory.GetFiles(spool, "*.jsonl", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();
        if (files.Count == 0)
        {
            output.WriteLine("nothing to ingest");
            return ExitCodes.Pass;
        }

        var report = IngestReport.Empty;
        foreach (var file in files)
        {
            report = report.Plus(await IngestFileAsync(file, store, cancellationToken));
        }

        Write(command, output, report);

        // A refused line is a real finding — an emitter writing a version this build cannot read, or a
        // corrupt spool — and it must not read as a clean pass. Everything else that went wrong is an
        // environment problem, which the two guards above already separated out.
        return report.Refused > 0 ? ExitCodes.Regression : ExitCodes.Pass;
    }

    /// <summary>One file: read, store, then retire it — unless a later build could still read it.
    /// <para>
    /// Read-store-rename is the resume guarantee: a host killed between the store and the rename
    /// re-reads the file next time and finds every record already known, which is why
    /// <see cref="IngestReport.Duplicate"/> is an ordinary outcome rather than a warning.
    /// </para>
    /// <para>
    /// A file carrying a RETRYABLE refusal is left alone. Refusing an unknown schema version by name so
    /// somebody can ship a build that reads it, and then retiring the file that held those records, is
    /// a promise broken in the same breath as it is made — the upgrade would arrive to find nothing to
    /// re-read but a <c>.ingested</c> file it no longer looks at. A half-written last line is the
    /// opposite case and retires normally: nothing will ever read it, so keeping the file would make
    /// every ordinary spool immortal.
    /// </para></summary>
    private static async Task<IngestReport> IngestFileAsync(
        string file, ITelemetryStore store, CancellationToken cancellationToken)
    {
        var (records, refused) = SpoolIngest.Read(await File.ReadAllTextAsync(file, cancellationToken));
        var stored = await store.AppendAsync(records, cancellationToken);

        var retryable = refused.Any(r => r.Retryable);
        if (!retryable)
        {
            File.Move(file, file + IngestedSuffix, overwrite: true);
        }

        var name = Path.GetFileName(file);
        var reasons = refused.Select(r => $"{name} {r}").ToList();
        if (retryable)
        {
            reasons.Add($"{name} kept for a later build — it holds records this one cannot read");
        }

        return stored.Plus(new IngestReport(0, 0, refused.Count, retryable ? 1 : 0, reasons));
    }

    /// <summary>Retires drained spool files older than a cut-off.
    /// <para>
    /// A separate action, never a step of ingest, and never automatic. Payload retention was decided in
    /// the schema — byte budgets applied at emit — but the FILES are a second question with a different
    /// owner: they sit on the emitting server's disk, and this benchmark is the component that renamed
    /// them, not the one that gets to decide unprompted that somebody's only copy has expired.
    /// </para>
    /// <para>Only <c>*.ingested</c> is ever touched. A spool still holding unread records is not a
    /// candidate for deletion at any age.</para></summary>
    private static int Prune(CommandLine command, TextWriter output, TextWriter error)
    {
        var spool = command.Value("spool");
        var days = command.Int("older-than", 0);

        if (spool.Length == 0)
        {
            return Fail(error, "--spool is required", ExitCodes.Configuration);
        }

        if (days <= 0)
        {
            return Fail(error, "--older-than <days> is required, and must be positive — pruning has no default", ExitCodes.Configuration);
        }

        if (!Directory.Exists(spool))
        {
            return Fail(error, $"spool directory not found: {spool}", ExitCodes.Environment);
        }

        var cutoff = DateTime.UtcNow.AddDays(-days);
        var stale = Directory
            .GetFiles(spool, "*" + IngestedSuffix, SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .Where(f => f.LastWriteTimeUtc < cutoff)
            .ToList();

        var bytes = stale.Sum(f => f.Length);
        foreach (var file in stale)
        {
            file.Delete();
        }

        output.WriteLine(command.Has("json")
            ? JsonSerializer.Serialize(new { pruned = stale.Count, bytes, olderThanDays = days })
            : $"pruned {stale.Count} drained file(s), {bytes / 1024} KB, older than {days} day(s)");

        return ExitCodes.Pass;
    }

    private static async Task<int> ReportAsync(
        CommandLine command, ITelemetryStore store, TextWriter output, CancellationToken cancellationToken)
    {
        var days = command.Int("days", 0);
        var since = days > 0 ? DateTimeOffset.UtcNow.AddDays(-days) : DateTimeOffset.MinValue;
        var window = days > 0 ? $"last {days} day(s)" : "all time";

        var totals = await store.TotalsAsync(since, cancellationToken);

        if (command.Has("json"))
        {
            output.WriteLine(JsonSerializer.Serialize(
                new { window, since = days > 0 ? since : (DateTimeOffset?)null, totals },
                new JsonSerializerOptions { WriteIndented = true }));
            return totals.Count == 0 ? ExitCodes.NoReport : ExitCodes.Pass;
        }

        if (totals.Count == 0)
        {
            output.WriteLine($"no telemetry stored ({window})");
            return ExitCodes.NoReport;
        }

        output.WriteLine($"{"tool",-28} {"caller",-34} {"calls",6} {"ans",5} {"ref",5} {"err",5} {"p50ms",8} {"p95ms",8}");
        foreach (var row in totals)
        {
            output.WriteLine(
                $"{Clip(row.Tool, 28),-28} {Clip(row.Caller, 34),-34} {row.Calls,6} {row.Answered,5} " +
                $"{row.Refused,5} {row.Errored,5} {row.MedianServerMs,8:F1} {row.P95ServerMs,8:F1}");
        }

        // "?" in a caller key is a field the transport could not tell us — an unknown, never a default.
        // The window is printed even when it is "all time": a total whose span is not stated is a
        // number two readers will scale differently.
        output.WriteLine();
        output.WriteLine($"window {window}; caller key = client/model/transport; '?' = not captured ({ToolTelemetry.ContractVersion})");
        return ExitCodes.Pass;
    }

    private static void Write(CommandLine command, TextWriter output, IngestReport report)
    {
        if (command.Has("json"))
        {
            output.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
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

    private static string Clip(string value, int width) =>
        value.Length <= width ? value : value[..(width - 1)] + "…";

    private static int Fail(TextWriter error, string reason, int code)
    {
        error.WriteLine($"bench: {reason}");
        return code;
    }
}
