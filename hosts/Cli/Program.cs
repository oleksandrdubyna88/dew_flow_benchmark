using Bench.Application;
using Bench.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bench.Cli;

/// <summary>The entry point, kept thin on purpose: parse, dispatch, exit. Everything a command does is
/// a use case in the Application layer, so the CLI and the API can never drift into two different
/// behaviours wearing one name.</summary>
public static class Program
{
    public static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    /// <summary>The testable seam: writers are injected so the contract can be asserted without a
    /// process launch — and the exit-code contract is the part most worth asserting.</summary>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        var command = CommandLine.Parse(args);

        return command.Verb switch
        {
            "plan" => PlanCommand.Run(command, output, error),
            "telemetry" => Telemetry(command, output, error),
            "version" => Version(output),
            "" or "help" => Help(output),
            _ => Unknown(command.Verb, error),
        };
    }

    /// <summary>Telemetry is the one verb that needs a database, so it is the one that resolves a
    /// connection string — and refuses, rather than guessing at localhost, when it has none. A default
    /// connection here would silently write a benchmark's data into whatever database happened to be
    /// listening.</summary>
    private static int Telemetry(CommandLine command, TextWriter output, TextWriter error)
    {
        var connection = command.Value("connection", Environment.GetEnvironmentVariable("ConnectionStrings__bench") ?? string.Empty);
        if (connection.Length == 0)
        {
            error.WriteLine("bench: no database — pass --connection or set ConnectionStrings__bench");
            return ExitCodes.Environment;
        }

        using var db = new BenchDbContext(
            new DbContextOptionsBuilder<BenchDbContext>().UseNpgsql(connection).Options);

        try
        {
            // Migrate on the way in. The AppHost hands over an EMPTY database, and the alternative is a
            // first run that fails on a missing table with a Postgres error the operator has to
            // translate. Migrations rather than EnsureCreated: this database is meant to hold results
            // for years, and EnsureCreated works exactly once and then cannot evolve a database with
            // data in it.
            db.Database.Migrate();
        }
        catch (Npgsql.NpgsqlException ex)
        {
            error.WriteLine($"bench: database unreachable — {ex.Message}");
            return ExitCodes.Environment;
        }

        return TelemetryCommand.RunAsync(command, new PostgresTelemetryStore(db), output, error)
            .GetAwaiter().GetResult();
    }

    private static int Version(TextWriter output)
    {
        output.WriteLine("bench 0.1.0");
        return ExitCodes.Pass;
    }

    private static int Help(TextWriter output)
    {
        output.WriteLine("bench — measure any repository at any commit, through any engine");
        output.WriteLine();
        output.WriteLine("  bench plan --repo <url> --commit <40-hex> --suite-file <path>");
        output.WriteLine("             [--repeats N] [--subjects id@local,id@cloud] [--lanes a,b]");
        output.WriteLine("             [--engine qln|mindex|http|noretrieval] [--exclude glob,glob] [--json]");
        output.WriteLine("  bench telemetry ingest --spool <dir> [--connection <npgsql>] [--json]");
        output.WriteLine("  bench telemetry report [--connection <npgsql>] [--json]");
        output.WriteLine("  bench version");
        output.WriteLine();
        output.WriteLine("exit codes: 0 pass · 1 regression · 3 environment · 4 configuration · 5 no report");
        return ExitCodes.Pass;
    }

    private static int Unknown(string verb, TextWriter error)
    {
        error.WriteLine($"bench: unknown command '{verb}' — try 'bench help'");
        return ExitCodes.Configuration;
    }
}
