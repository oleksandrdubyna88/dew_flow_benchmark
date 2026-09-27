using System.Text.Json;
using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bench.Cli;

/// <summary><c>bench gate …</c> — the coai gate-model benchmark's verbs. E2 lands the two that own its storage:
/// the PUBLIC export (database rows through the publication guard, never an artefact) and the tap PRUNE (bodies
/// past the window released, facts kept). The driver's verbs — run, resume, status, sweep — arrive with E3.</summary>
public static class CoaiGateCommand
{
    /// <summary>How long a tap request/response body is kept, in days. The facts file of every call is kept forever.</summary>
    public const int DefaultTapRetentionDays = 30;

    public static async Task<int> RunAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken) =>
        command.Operand(0) switch
        {
            "export" => await ExportAsync(command, output, error, cancellationToken),
            "prune" => await PruneAsync(command, output, error, cancellationToken),
            var other => Unknown(other, error),
        };

    /// <summary><c>bench gate export --public --db … --suite-file … --out …</c>. The export is built from database
    /// rows ONLY; the artefact root is not an input and is never opened.</summary>
    private static async Task<int> ExportAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var refusal = ExportRefusal(command);

        if (refusal.Length > 0)
        {
            error.WriteLine($"bench: {refusal}");
            return ExitCodes.Configuration;
        }

        var names = ReadPrivateNames(Suite(command));

        if (names is Outcome<PrivateNames>.Fail failed)
        {
            error.WriteLine($"bench: {failed.Reason}");
            return ExitCodes.Configuration;
        }

        return await ExportRowsAsync(command, ((Outcome<PrivateNames>.Ok)names).Value, output, error, cancellationToken);
    }

    private static async Task<int> ExportRowsAsync(
        CommandLine command, PrivateNames names, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        await using var db = new BenchDbContext(new DbContextOptionsBuilder<BenchDbContext>().UseNpgsql(Connection(command)).Options);

        var source = new PostgresGatePublicationSource(db);
        IReadOnlyList<PublishedTable> tables;

        try
        {
            tables = await source.ReadAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is Npgsql.NpgsqlException or InvalidOperationException)
        {
            error.WriteLine($"bench: the store is not reachable or not migrated — {ex.Message.Split('\n')[0]}");
            return ExitCodes.Environment;
        }

        var exported = GatePublication.Export(tables, names, source.PublicUrlColumns, DateTimeOffset.UtcNow);

        return exported switch
        {
            Outcome<string>.Ok document => await WriteAsync(command.Value("out"), document.Value, tables, output, cancellationToken),
            Outcome<string>.Fail fail => Refused(fail.Reason, error),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    /// <summary><c>bench gate prune --artifact-root … [--tap-retention-days 30] [--dry-run] [--json]</c>.</summary>
    private static async Task<int> PruneAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var days = command.Int("tap-retention-days", DefaultTapRetentionDays);

        if (days <= 0)
        {
            error.WriteLine("bench: --tap-retention-days must be positive — a prune that releases every body ever recorded is not a guess this verb makes");
            return ExitCodes.Configuration;
        }

        var opened = FileSystemGateArtifactStore.Open(command.Value("artifact-root"), TimeProvider.System, ArtifactProbe.None);

        if (opened is Outcome<FileSystemGateArtifactStore>.Fail refused)
        {
            error.WriteLine($"bench: {refused.Reason}");
            return ExitCodes.Configuration;
        }

        var store = ((Outcome<FileSystemGateArtifactStore>.Ok)opened).Value;
        var dryRun = command.Has("dry-run");
        var report = await store.PruneTapAsync(DateTimeOffset.UtcNow.AddDays(-days), dryRun, cancellationToken);

        await PrintPruneAsync(store, report, days, dryRun, command.Has("json"), output, cancellationToken);
        return ExitCodes.Pass;
    }

    private static async Task PrintPruneAsync(
        IGateArtifactStore store, TapPruneReport report, int days, bool dryRun, bool json, TextWriter output, CancellationToken cancellationToken)
    {
        var footprints = new List<(Guid Run, ArtifactFootprint Footprint)>();

        foreach (var run in await store.RunsAsync(cancellationToken))
        {
            footprints.Add((run, await store.FootprintAsync(run, cancellationToken)));
        }

        if (json)
        {
            output.WriteLine(JsonSerializer.Serialize(
                new
                {
                    retentionDays = days,
                    dryRun,
                    bodiesReleased = report.BodiesReleased,
                    bytesReleased = report.BytesReleased,
                    attempts = report.Entries.Select(e => new { runId = e.RunId, cellId = e.CellId, attempt = e.Attempt, state = e.State.ToString(), bodies = e.Bodies, bytes = e.Bytes }),
                    runs = footprints.Select(f => new { runId = f.Run, footprint = f.Footprint.Describe }),
                },
                new JsonSerializerOptions { WriteIndented = true }));
            return;
        }

        foreach (var entry in report.Entries.Where(e => e.State != TapPruneState.Recent))
        {
            output.WriteLine($"{Word(entry.State),-14} run {entry.RunId} cell {entry.CellId} attempt {entry.Attempt}"
                             + (entry.Bodies > 0 ? $" — {entry.Bodies} body file(s), {entry.Bytes} B" : string.Empty));
        }

        foreach (var (run, footprint) in footprints)
        {
            output.WriteLine($"footprint      run {run}: {footprint.Describe}");
        }

        output.WriteLine(dryRun
            ? $"dry run        {report.Entries.Where(e => e.State == TapPruneState.WouldRelease).Sum(e => e.Bodies)} tap body file(s) past {days} day(s) WOULD be released; nothing was deleted"
            : $"pruned         {report.BodiesReleased} tap body file(s), {report.BytesReleased} B, past {days} day(s); every facts file kept");
    }

    private static string Word(TapPruneState state) => state switch
    {
        TapPruneState.Released => "released",
        TapPruneState.WouldRelease => "would release",
        TapPruneState.Unfinished => "unfinished",
        TapPruneState.Interrupted => "interrupted",
        TapPruneState.FactsMissing => "facts missing",
        TapPruneState.Linked => "link refused",
        _ => "recent",
    };

    private static string ExportRefusal(CommandLine command) =>
        (command.Has("public"), Connection(command).Length > 0, command.Value("out").Length > 0, Suite(command).Length > 0) switch
        {
            (false, _, _, _) => "gate export writes the PUBLIC export only — pass --public; the private record is the artefact root itself, and it is never exported",
            (_, false, _, _) => "gate export reads the database — pass --db or set BENCH_DB",
            (_, _, false, _) => "gate export needs --out <file.json>",
            (_, _, _, false) => "gate export needs the suite's private names — pass --suite-file or set BENCH_GATE_SUITE; an export that "
                                + "does not know which names are private cannot prove it carries none",
            _ => string.Empty,
        };

    private static Outcome<PrivateNames> ReadPrivateNames(string suitePath)
    {
        try
        {
            return GatePrivateNames.Read(File.ReadAllText(suitePath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome<PrivateNames>.Failure($"the suite file could not be read — {ex.GetType().Name}");
        }
    }

    private static async Task<int> WriteAsync(
        string path, string document, IReadOnlyList<PublishedTable> tables, TextWriter output, CancellationToken cancellationToken)
    {
        var staging = path + ".partial";
        await File.WriteAllTextAsync(staging, document, cancellationToken);
        File.Move(staging, path, overwrite: true);

        output.WriteLine($"exported       {tables.Sum(t => t.Rows.Count)} row(s) from {tables.Count} gate table(s), through the publication guard");
        return ExitCodes.Pass;
    }

    /// <summary>The guard refused: nothing was written, and the exit code says no report was produced — never a
    /// pass, and never "the environment is broken", because the database answered and the answer was private.</summary>
    private static int Refused(string reason, TextWriter error)
    {
        error.WriteLine($"bench: {reason}");
        return ExitCodes.NoReport;
    }

    private static int Unknown(string operand, TextWriter error)
    {
        error.WriteLine(operand.Length == 0
            ? "bench: gate needs a sub-verb — export or prune"
            : $"bench: unknown gate sub-verb '{operand}' — export or prune (run, resume, status and sweep arrive with the driver)");
        return ExitCodes.Configuration;
    }

    private static string Connection(CommandLine command) =>
        command.Value("db", Environment.GetEnvironmentVariable("BENCH_DB") ?? string.Empty);

    private static string Suite(CommandLine command) =>
        command.Value("suite-file", Environment.GetEnvironmentVariable("BENCH_GATE_SUITE") ?? string.Empty);
}
