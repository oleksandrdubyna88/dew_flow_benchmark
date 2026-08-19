using Bench.Application;
using Bench.Application.Bank;
using Bench.Application.Registry;
using Bench.Infrastructure.Models;
using Bench.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bench.Cli;

/// <summary>The entry point, kept thin on purpose: parse, dispatch, exit. Everything a command does is
/// a use case in the Application layer, so the CLI and the API can never drift into two different
/// behaviours wearing one name.</summary>
public static class Program
{
    public static int Main(string[] args)
    {
        // The first live run printed every separator as a replacement character: the default Windows
        // console codepage is not UTF-8, and this output is read by an agent. Set on the streams the
        // process actually owns — Run() takes writers, so a test never reaches this.
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Ctrl+C and SIGTERM become the root token every verb runs under, so a planned stop leaves a
        // resumable run instead of a stranded one.
        using var shutdown = ShutdownSignal.Install(Console.Error);

        try
        {
            return Run(args, Console.Out, Console.Error, shutdown.Token);
        }
        catch (OperationCanceledException)
        {
            // A signal that arrived while the verb was still setting itself up. Not a fault, and not a
            // pass either: whatever was already claimed comes back at the next run's startup sweep.
            Console.Error.WriteLine("bench: stopped before the measurement finished — the next run's sweep reclaims what was held");
            return ExitCodes.NoReport;
        }
        catch (Exception ex)
        {
            // The last frame before "nobody above me". Without it an unhandled fault leaves a stack trace
            // on a terminal nobody is watching and an exit code nothing agreed on.
            Serilog.Log.Fatal(ex, "bench exited on an unhandled exception");
            Console.Error.WriteLine($"bench: unhandled {ex.GetType().Name} — {ex.Message.Split('\n')[0]}");
            return ExitCodes.Environment;
        }
        finally
        {
            // A file sink that is never closed loses whatever the last seconds had to say — which is
            // reliably the part somebody needs.
            Serilog.Log.CloseAndFlush();
        }
    }

    /// <summary>The testable seam: writers are injected so the contract can be asserted without a
    /// process launch — and the exit-code contract is the part most worth asserting.</summary>
    public static int Run(string[] args, TextWriter output, TextWriter error, CancellationToken stopping = default)
    {
        var command = CommandLine.Parse(args);

        return command.Verb switch
        {
            "plan" => PlanCommand.Run(command, output, error),
            "run" => RunCommand.RunAsync(command, output, error, stopping).GetAwaiter().GetResult(),
            "judge" => JudgeCommand.RunAsync(command, output, error, stopping).GetAwaiter().GetResult(),
            "report" => Report(command, output, error, stopping),
            "sweep" => SweepCommand.RunAsync(command, output, error, stopping).GetAwaiter().GetResult(),
            "prune" => PruneCommand.RunAsync(command, output, error, stopping).GetAwaiter().GetResult(),
            "telemetry" => Telemetry(command, output, error, stopping),
            "variants" => Variants(command, output, error, stopping),
            "lanes" => Lanes(command, output, error, stopping),
            "questions" => Questions(command, output, error, stopping),
            "models" => Models(command, output, error, stopping),
            "version" => Version(output),
            "" or "help" => Help(output),
            _ => Unknown(command.Verb, error),
        };
    }

    /// <summary>The one READ verb over the store, and the only one here that does not migrate on the way
    /// in.
    /// <para>
    /// Deliberate: migrating from a report would create an empty schema for a run that cannot exist in it,
    /// and then answer "no run" — which reads as "your id is wrong" when the truth is "this is the wrong
    /// database". An unreachable or unmigrated store surfaces as an environment failure naming what
    /// happened, which is what an operator can act on.
    /// </para></summary>
    private static int Report(CommandLine command, TextWriter output, TextWriter error, CancellationToken stopping)
    {
        var connection = command.Value("db", Environment.GetEnvironmentVariable("BENCH_DB") ?? string.Empty);
        if (connection.Length == 0)
        {
            error.WriteLine("bench: no database — pass --db or set BENCH_DB");
            return ExitCodes.Environment;
        }

        using var db = new BenchDbContext(
            new DbContextOptionsBuilder<BenchDbContext>().UseNpgsql(connection).Options);

        return ReportCommand
            .RunAsync(
                command,
                new PostgresRunStore(db, TimeProvider.System),
                new PostgresResultStore(db, TimeProvider.System),
                output,
                error,
                stopping)
            .GetAwaiter().GetResult();
    }

    /// <summary>Telemetry is the one verb that needs a database, so it is the one that resolves a
    /// connection string — and refuses, rather than guessing at localhost, when it has none. A default
    /// connection here would silently write a benchmark's data into whatever database happened to be
    /// listening.</summary>
    private static int Telemetry(CommandLine command, TextWriter output, TextWriter error, CancellationToken stopping)
    {
        // Pruning is a file operation and touches no database, so it must not demand a connection
        // string. Requiring one would make retiring drained files impossible on a machine that has the
        // spool but not the store — which is every machine that emits.
        if (command.Operand(0) == "prune")
        {
            return TelemetryCommand.RunAsync(command, new NoTelemetryStore(), output, error, stopping)
                .GetAwaiter().GetResult();
        }

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

        return TelemetryCommand.RunAsync(command, new PostgresTelemetryStore(db), output, error, stopping)
            .GetAwaiter().GetResult();
    }

    /// <summary>The catalog lives in the database, so this verb needs one — and refuses rather than
    /// guessing at localhost, for the same reason telemetry does: a default connection would write a
    /// configuration into whatever database happened to be listening.</summary>
    private static int Variants(CommandLine command, TextWriter output, TextWriter error, CancellationToken stopping)
    {
        var connection = command.Value("db", Environment.GetEnvironmentVariable("BENCH_DB") ?? string.Empty);
        if (connection.Length == 0)
        {
            error.WriteLine("bench: no database — pass --db or set BENCH_DB");
            return ExitCodes.Environment;
        }

        using var db = new BenchDbContext(
            new DbContextOptionsBuilder<BenchDbContext>().UseNpgsql(connection).Options);

        try
        {
            db.Database.Migrate();
        }
        catch (Npgsql.NpgsqlException ex)
        {
            error.WriteLine($"bench: database unreachable — {ex.Message}");
            return ExitCodes.Environment;
        }

        return VariantsCommand
            .RunAsync(command, new PostgresVariantCatalog(db), TimeProvider.System, output, error, stopping)
            .GetAwaiter().GetResult();
    }

    /// <summary>The lane catalog, same shape and same refusal as the variant one above: a default
    /// connection would write a tool surface into whatever database happened to be listening, and a lane is
    /// an identity every future result quotes.</summary>
    private static int Lanes(CommandLine command, TextWriter output, TextWriter error, CancellationToken stopping)
    {
        var connection = command.Value("db", Environment.GetEnvironmentVariable("BENCH_DB") ?? string.Empty);
        if (connection.Length == 0)
        {
            error.WriteLine("bench: no database — pass --db or set BENCH_DB");
            return ExitCodes.Environment;
        }

        using var db = new BenchDbContext(
            new DbContextOptionsBuilder<BenchDbContext>().UseNpgsql(connection).Options);

        try
        {
            db.Database.Migrate();
        }
        catch (Npgsql.NpgsqlException ex)
        {
            error.WriteLine($"bench: database unreachable — {ex.Message}");
            return ExitCodes.Environment;
        }

        return LanesCommand
            .RunAsync(command, new PostgresLaneCatalog(db), TimeProvider.System, output, error, stopping)
            .GetAwaiter().GetResult();
    }

    /// <summary>The bank lives in the database, so this verb needs one — and refuses rather than guessing,
    /// for the same reason the other two do: a default connection would import somebody's question set into
    /// whatever database happened to be listening.</summary>
    private static int Questions(CommandLine command, TextWriter output, TextWriter error, CancellationToken stopping)
    {
        var connection = command.Value("db", Environment.GetEnvironmentVariable("BENCH_DB") ?? string.Empty);
        if (connection.Length == 0)
        {
            error.WriteLine("bench: no database — pass --db or set BENCH_DB");
            return ExitCodes.Environment;
        }

        using var provider = CliContainer.ForBank(connection, CheckoutRoot(command), CliLogging.Start());
        using var scope = provider.CreateScope();

        try
        {
            scope.ServiceProvider.GetRequiredService<BenchDbContext>().Database.Migrate();
        }
        catch (Npgsql.NpgsqlException ex)
        {
            error.WriteLine($"bench: database unreachable — {ex.Message}");
            return ExitCodes.Environment;
        }

        return QuestionsCommand.RunAsync(
                command,
                scope.ServiceProvider.GetRequiredService<IQuestionBank>(),
                scope.ServiceProvider.GetRequiredService<ICliAgentRuntime>(),
                scope.ServiceProvider.GetRequiredService<IModelRegistry>(),
                scope.ServiceProvider.GetRequiredService<ISecretSource>(),
                scope.ServiceProvider.GetRequiredService<ICheckoutProvider>(),
                scope.ServiceProvider.GetRequiredService<WorkspaceTrust>(),
                TimeProvider.System,
                output,
                error,
                stopping)
            .GetAwaiter().GetResult();
    }

    /// <summary>The registry every role draws from. Same refusal as the other stateful verbs: no default
    /// connection, because one would write a benchmark's configuration into whatever database happened to
    /// be listening.</summary>
    private static int Models(CommandLine command, TextWriter output, TextWriter error, CancellationToken stopping)
    {
        var connection = command.Value("db", Environment.GetEnvironmentVariable("BENCH_DB") ?? string.Empty);
        if (connection.Length == 0)
        {
            error.WriteLine("bench: no database — pass --db or set BENCH_DB");
            return ExitCodes.Environment;
        }

        using var provider = CliContainer.ForBank(connection, CheckoutRoot(command), CliLogging.Start());
        using var scope = provider.CreateScope();

        try
        {
            scope.ServiceProvider.GetRequiredService<BenchDbContext>().Database.Migrate();
        }
        catch (Npgsql.NpgsqlException ex)
        {
            error.WriteLine($"bench: database unreachable — {ex.Message}");
            return ExitCodes.Environment;
        }

        return ModelsCommand.RunAsync(
                command,
                scope.ServiceProvider.GetRequiredService<IModelRegistry>(),
                scope.ServiceProvider.GetRequiredService<ISecretSource>(),
                scope.ServiceProvider.GetRequiredService<ICliAgentRuntime>(),
                TimeProvider.System,
                output,
                error,
                stopping)
            .GetAwaiter().GetResult();
    }

    /// <summary>Where the read-only checkout cache lives. The same default `bench run` uses, so a target
    /// mirrored for a run is the tree an authoring pass reads rather than a second clone of it.</summary>
    private static string CheckoutRoot(CommandLine command) =>
        command.Value(
            "checkout-root",
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "bench", "checkouts"));

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
        output.WriteLine("  bench telemetry ingest --spool <dir> [--connection <npgsql>] [--chunk-size 500] [--json]");
        output.WriteLine("  bench telemetry report [--days N] [--connection <npgsql>] [--json]");
        output.WriteLine("  bench telemetry prune  --spool <dir> --older-than <days> [--json]");
        output.WriteLine();
        output.WriteLine("  bench variants add    --name <slug> [--display <text>] --db <connection>");
        output.WriteLine("             --engine qln|mindex|http|noretrieval --channels dense|sparse|hybrid");
        output.WriteLine("             --fusion rrf|wsum [--k 60] [--dense-weight 1] [--sparse-weight 1]");
        output.WriteLine("             [--norm none|minmax] --text-shape <name> --chunk-tokens N --embed-model <id>");
        output.WriteLine("             [--rerank-pool N] [--limit 20]   (or --definition-file <path> | --definition <json>)");
        output.WriteLine("  bench variants list   [--all] --db <connection>");
        output.WriteLine("  bench variants retire --name <slug> --db <connection>");
        output.WriteLine("             a variant is added and retired, never edited: results name what they ran under");
        output.WriteLine();
        output.WriteLine("  bench lanes add       --name <slug> [--display <text>] --db <connection>");
        output.WriteLine("             --presentation none|bridge|mcpstdio|clinative|clinativewithmcp");
        output.WriteLine("             [--tools a,b,c] [--descriptions <set>] [--max-turns 1]");
        output.WriteLine("             [--doctrine <text> | --doctrine-file <path>]");
        output.WriteLine("             (or --definition-file <path> | --definition <json>)");
        output.WriteLine("  bench lanes list      [--all] --db <connection>");
        output.WriteLine("  bench lanes retire    --name <slug> --db <connection>");
        output.WriteLine("             a lane is the TOOL SURFACE a leg ran under — which tools, in which words,");
        output.WriteLine("             under which ordering doctrine, through which shape, for how many turns");
        output.WriteLine();
        output.WriteLine("  bench models add     --key <slug> --model-id <id> [--display <text>]");
        output.WriteLine("             [--runtime openaiendpoint|cliclaude|clicodex|cligemini|bridgelocal]");
        output.WriteLine("             [--hosting local|cloud] --base-url-ref <ENV_VAR_NAME> [--api-key-ref <NAME>]");
        output.WriteLine("             [--executable-ref <NAME>] [--seed N] [--input-cost X] [--output-cost X] --db <connection>");
        output.WriteLine("             REFERENCES, never values: this database is published unedited, so an");
        output.WriteLine("             endpoint or a key stored here would leave with the results");
        output.WriteLine("  bench models list    [--all] --db <connection>   says which references resolve HERE");
        output.WriteLine("  bench models probe   --key <slug> [--wall-seconds 90] --db <connection>");
        output.WriteLine("             asks ONE CLI agent a trivial question through the same path authoring uses,");
        output.WriteLine("             and writes nothing. A timeout means the headless flag is wrong for that CLI");
        output.WriteLine("             (it opened an interactive session); a non-zero exit usually means no login");
        output.WriteLine("  bench models disable|enable --key <slug> --db <connection>");
        output.WriteLine();
        output.WriteLine("  bench questions import --file <path> --db <connection>");
        output.WriteLine("  bench questions author --group <key> --authors <registry keys> --repo <url>");
        output.WriteLine("             --commit <40-hex> [--count 5] [--ordinal 1] [--wall-seconds 600]");
        output.WriteLine("             [--prompts prompts] [--no-trust] --db <connection>   a CLI agent writes");
        output.WriteLine("             candidates:");
        output.WriteLine("             they land Proposed, through the same admission rules an import passes,");
        output.WriteLine("             and the prompt that wrote them is recorded by hash");
        output.WriteLine("  bench questions vet    --group <key> --repo <url> --commit <40-hex> [--limit 10]");
        output.WriteLine("             [--wall-seconds 300] [--allow-self-review] [--prompts prompts] --db <connection>");
        output.WriteLine("             [--no-trust]   every BOUND reviewer slot marks the group's proposed questions;");
        output.WriteLine("             a question");
        output.WriteLine("             becomes selectable only when every configured slot approved it.");
        output.WriteLine("             Both verbs pre-trust the checked-out tree in the agent CLI's own config —");
        output.WriteLine("             an untrusted workspace makes the agent wait for a dialog nothing can");
        output.WriteLine("             answer, which costs the whole wall. Only paths under --checkout-root.");
        output.WriteLine("  bench questions list   [--group <key>] [--from N] [--to N] [--accepted] --db <connection>");
        output.WriteLine("  bench questions groups --db <connection>");
        output.WriteLine("  bench questions reviewers --db <connection>   the slots, and which model answers each");
        output.WriteLine("  bench questions bind   --reviewer <key> [--model <registry key>] --db <connection>");
        output.WriteLine("             an empty --model hands the slot back to a person");
        output.WriteLine("  bench questions review --question <id> --reviewer <key> [--verdict approved|rejected]");
        output.WriteLine("             [--note <text>] --db <connection>   one mark per reviewer per question");
        output.WriteLine("  bench questions accept|reject --question <id> --db <connection>");
        output.WriteLine("  bench questions move   --question <id> --to <group> --reason <text> --db <connection>");
        output.WriteLine("             a move never touches a finished test: its snapshot keeps the group it froze");
        output.WriteLine();
        output.WriteLine("  bench run  --repo <url> --commit <40-hex> --suite-file <path>");
        output.WriteLine("             --subjects <registry keys> [--judges <registry keys, in order>]");
        output.WriteLine("             (or the ad-hoc pair --model <id> --model-url <openai-compatible base>)");
        output.WriteLine("             --db <connection>");
        output.WriteLine("             (or --bank-group <key> [--bank-from N] [--bank-to N] [--suite-id <name>]");
        output.WriteLine("              to freeze a selection from the bank instead of reading a file)");
        output.WriteLine("             [--engine-url <qln base> --engine-project <guid> [--engine-branch main]");
        output.WriteLine("              --variants <catalog names>]  single-shot RAG: the harness retrieves per cell,");
        output.WriteLine("             assembles the prompt from the hits, and stores the funnel, the hits and the");
        output.WriteLine("             axes the engine echoed. Without them the run measures the no-retrieval baseline");
        output.WriteLine("             the corpus that will ANSWER is read before any cell exists and a recipe it");
        output.WriteLine("             disagrees with ends the run; [--allow-unstamped-index] measures an index whose");
        output.WriteLine("             commit the engine never recorded, and says so");
        output.WriteLine("             a variant may also name the COMPUTE BACKEND it measures (host/provider/device,");
        output.WriteLine("             e.g. wsl/migraphx/R9700). The engine echoes what it served on and a disagreement");
        output.WriteLine("             ends the run naming both — two sidecars are two arms, never one result row;");
        output.WriteLine("             [--allow-undeclared-backend] measures against an engine that declares none, and");
        output.WriteLine("             keeps printing that which arm the numbers describe is UNVERIFIED");
        output.WriteLine("             [--hit-retention-days 7]  how long a hit keeps its source TEXT; ranks, scores");
        output.WriteLine("             and spans are kept forever, so every retrieval metric stays recomputable");
        output.WriteLine("             [--lane no-tools] [--repeats N] [--seed N] [--label X] [--json]");
        output.WriteLine("             [--stale-after-minutes 30] [--max-consecutive-failures 20]");
        output.WriteLine("             [--checkout-root <dir>] [--no-checkout]  the target is mirrored read-only and");
        output.WriteLine("             checked out at its commit before anything is measured; --no-checkout skips");
        output.WriteLine("             that and says plainly that the commit is UNVERIFIED");
        output.WriteLine("             [--leg-wall-seconds 600]  one ceiling for the WHOLE leg, not per call:");
        output.WriteLine("             a looping lane must not multiply it by a turn count nobody bounded");
        output.WriteLine("             sweeps what a crash left claimed before it drains; Ctrl+C / SIGTERM stop it");
        output.WriteLine("             cleanly — the leg in flight settles and the rest stay Pending for the next run");
        output.WriteLine("  bench sweep --db <connection> [--stale-after-minutes 30] [--json]");
        output.WriteLine("             hands back the cells a dead host left claimed; run it after any kill -9");
        output.WriteLine("  bench prune --db <connection> [--hit-retention-days 7] [--json]");
        output.WriteLine("             releases the source TEXT of old retrieved hits, keeping every row: the corpus");
        output.WriteLine("             at the pinned commit reproduces it. `bench run` does this at startup too");
        output.WriteLine("  bench judge --run <id> --suite-file <path> --db <connection>");
        output.WriteLine("             --judge-model <id> --judge-url <openai-compatible base> [--seed N] [--json]");
        output.WriteLine("             re-scores STORED answers: a second arbiter never re-runs a leg");
        output.WriteLine("  bench report --run <id> --metric <name> --db <connection>");
        output.WriteLine("             [--min-legs 2] [--min-spread 0.25] [--baseline <arm>] [--json]");
        output.WriteLine("             the comparison, along every axis and on BOTH halves of the split. A");
        output.WriteLine("             configuration that won only where it was chosen renders UNPROVEN — not as a");
        output.WriteLine("             smaller win. --metric has no default: the one that answers a retrieval");
        output.WriteLine("             question means nothing for the control arm. A low score still exits 0");
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

    /// <summary>Stands in for the store on the one path that has no database. Every member throws
    /// rather than returning an empty result: a silent empty answer here would render as "no telemetry
    /// stored", which is a claim about the data instead of an admission that nothing was consulted.</summary>
    private sealed class NoTelemetryStore : ITelemetryStore
    {
        public Task<IngestReport> AppendAsync(
            IReadOnlyList<Bench.Domain.Telemetry.ToolTelemetry> records, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("this command does not use a store");

        public Task<IReadOnlyList<ToolTelemetryTotals>> TotalsAsync(
            DateTimeOffset since, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("this command does not use a store");

        public Task<IReadOnlyList<PhaseTelemetryTotals>> ByPhaseAsync(
            string leg, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("this command does not use a store");
    }
}
