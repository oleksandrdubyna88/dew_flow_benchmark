using System.Text.Json;
using Bench.Application;
using Bench.Domain.Telemetry;

namespace Bench.Cli;

/// <summary>`bench telemetry ingest` and `bench telemetry report` — draining the spools an MCP server
/// writes, and reading what they say.
/// <para>
/// The spool is the transport on purpose. A server that had to reach this host to record a call would
/// couple a product's tool surface to a benchmark being up; a file it appends to and someone else
/// drains couples them to nothing. Ingest is therefore idempotent and resumable: a file is renamed
/// only after every line of it is committed, and re-reading one changes nothing.
/// </para></summary>
public static class TelemetryCommand
{
    /// <summary>Marks a spool file as drained. A rename rather than a delete: the data is somebody's
    /// only copy until they say otherwise, and a benchmark is not the right component to decide that.</summary>
    public const string IngestedSuffix = ".ingested";

    public static async Task<int> RunAsync(
        CommandLine command, ITelemetryStore store, TextWriter output, TextWriter error) =>
        command.Operand(0) switch
        {
            "ingest" => await IngestAsync(command, store, output, error),
            "report" => await ReportAsync(command, store, output),
            var other => Fail(
                error,
                other.Length == 0
                    ? "bench telemetry needs an action — 'ingest' or 'report'"
                    : $"unknown telemetry action '{other}' — try 'ingest' or 'report'",
                ExitCodes.Configuration),
        };

    private static async Task<int> IngestAsync(
        CommandLine command, ITelemetryStore store, TextWriter output, TextWriter error)
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
            report = report.Plus(await IngestFileAsync(file, store));
        }

        Write(command, output, report);

        // A refused line is a real finding — an emitter writing a version this build cannot read, or a
        // corrupt spool — and it must not read as a clean pass. Everything else that went wrong is an
        // environment problem, which the two guards above already separated out.
        return report.Refused > 0 ? ExitCodes.Regression : ExitCodes.Pass;
    }

    /// <summary>One file: read, store, then rename. The order is the resume guarantee — a host killed
    /// between the store and the rename re-reads the file next time and finds every record already
    /// known, which is why <see cref="IngestReport.Duplicate"/> is an ordinary outcome rather than a
    /// warning.</summary>
    private static async Task<IngestReport> IngestFileAsync(string file, ITelemetryStore store)
    {
        var (records, refused) = SpoolIngest.Read(await File.ReadAllTextAsync(file));
        var stored = await store.AppendAsync(records, CancellationToken.None);

        File.Move(file, file + IngestedSuffix, overwrite: true);

        return stored.Plus(new IngestReport(0, 0, refused.Count, [.. refused.Select(r => $"{Path.GetFileName(file)} {r}")]));
    }

    private static async Task<int> ReportAsync(CommandLine command, ITelemetryStore store, TextWriter output)
    {
        var totals = await store.TotalsAsync(CancellationToken.None);

        if (command.Has("json"))
        {
            output.WriteLine(JsonSerializer.Serialize(totals, new JsonSerializerOptions { WriteIndented = true }));
            return totals.Count == 0 ? ExitCodes.NoReport : ExitCodes.Pass;
        }

        if (totals.Count == 0)
        {
            output.WriteLine("no telemetry stored");
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
        output.WriteLine();
        output.WriteLine($"caller key = client/model/transport; '?' = not captured ({ToolTelemetry.ContractVersion})");
        return ExitCodes.Pass;
    }

    private static void Write(CommandLine command, TextWriter output, IngestReport report)
    {
        if (command.Has("json"))
        {
            output.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            return;
        }

        output.WriteLine($"ingested {report.Ingested}, duplicate {report.Duplicate}, refused {report.Refused}");
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
