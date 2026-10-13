using System.Globalization;
using Bench.Application.Scouts;
using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;

namespace Bench.Cli;

/// <summary><c>bench scouts tasks | run | resume | rerun | sweep | prune | alias-check | status | score | assess | report</c> — the scout model
/// calibration (<c>todo/PLAN_scout_model_calibration.md</c>, E1 S2 and S3). The measuring verbs are here; <see cref="ScoutsTasksCommand"/> freezes the tasks,
/// <see cref="ScoutsHousekeepingCommand"/> sweeps and prunes, <see cref="ScoutsAliasCommand"/> watches the alias; S3 reads and judges a run —
/// <see cref="ScoutsReadCommand"/> (<c>status</c>, <c>report</c>), <see cref="ScoutsScoreCommand"/> and <see cref="ScoutsAssessCommand"/>.
/// <para>
/// Exit codes, as the probes': 0 cells produced · 3 resumable — a pin moved, the next reservation does not fit, a cell stopped the campaign
/// (path audit, cost check, changed tree, moved alias), a spend limit, the environment (database, git, an unreadable pin) · 4 configuration —
/// flags, files, roots (the grant guard), an unknown run · 5 nothing produced. Every verb that claims runs the entry step FIRST — the
/// owner-checked sweep, then recovery of every orphaned attempt (<see cref="ScoutRecoveryStep"/>).
/// </para></summary>
public static class ScoutsCommand
{
    public const int DefaultCellTimeoutMinutes = ScoutArmBudget.DefaultWallSeconds / 60;

    private const string SubVerbs = "tasks, run, resume, rerun, sweep, prune, alias-check, status, score, assess or report";

    public static Task<int> RunAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken) =>
        RunAsync(command, ScoutVerbServices.Machine, output, error, cancellationToken);

    /// <summary>The seam a test drives: the verb with its outside world handed in.</summary>
    public static async Task<int> RunAsync(CommandLine command, ScoutVerbServices services, TextWriter output, TextWriter error, CancellationToken cancellationToken) =>
        command.Operand(0) switch
        {
            "tasks" => await ScoutsTasksCommand.RunAsync(command, output, error, cancellationToken),
            "run" => await PlanAndRunAsync(command, services, output, error, cancellationToken),
            "resume" => await ResumeAsync(command, services, output, error, cancellationToken),
            "rerun" => await RerunAsync(command, services, output, error, cancellationToken),
            "sweep" => await ScoutsHousekeepingCommand.SweepAsync(command, output, error, cancellationToken),
            "prune" => await ScoutsHousekeepingCommand.PruneAsync(command, output, error, cancellationToken),
            "alias-check" => await ScoutsAliasCommand.RunAsync(command, services, output, error, cancellationToken),
            "status" => await ScoutsReadCommand.StatusAsync(command, output, error, cancellationToken),
            "score" => await ScoutsScoreCommand.RunAsync(command, output, error, cancellationToken),
            "assess" => await ScoutsAssessCommand.RunAsync(command, services, output, error, cancellationToken),
            "report" => await ScoutsReadCommand.ReportAsync(command, output, error, cancellationToken),
            var other => GateRunCommand.Refuse(error, ExitCodes.Configuration, other.Length == 0
                ? $"scouts needs a sub-verb — {SubVerbs}"
                : $"unknown scouts sub-verb '{other}' — {SubVerbs}"),
        };

    /// <summary>What a measuring verb needs besides the store: the CLI and the price table the cost check runs on.</summary>
    private sealed record Measuring(string Executable, PriceTable Prices);

    private static async Task<Outcome<Measuring>> MeasuringAsync(CommandLine command, ScoutVerbServices services, CancellationToken cancellationToken)
    {
        var prices = command.Value("prices");

        if (prices.Length == 0 || !File.Exists(prices))
        {
            return Outcome<Measuring>.Failure("a measuring scout verb needs --prices <prices.json> — the vendor price table the cost check (§3.6) prices every cell with");
        }

        var table = ScoutFiles.Prices(await File.ReadAllTextAsync(prices, cancellationToken));

        return (table, ScoutsInputs.Executable(command, services)) switch
        {
            (Outcome<PriceTable>.Fail fail, _) => Outcome<Measuring>.Failure($"{Path.GetFileName(prices)}: {fail.Reason}"),
            (_, Outcome<string>.Fail fail) => Outcome<Measuring>.Failure($"the claude CLI cannot run on this machine — {fail.Reason}"),
            (Outcome<PriceTable>.Ok p, Outcome<string>.Ok exe) => Outcome<Measuring>.Success(new Measuring(exe.Value, p.Value)),
            _ => Outcome<Measuring>.Failure("unreachable"),
        };
    }

    /// <summary><c>bench scouts run --tasks-file … --opus-id … --sonnet-id … --ceiling-usd … --cap-usd … --prices …</c>.</summary>
    private static async Task<int> PlanAndRunAsync(CommandLine command, ScoutVerbServices services, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var asked = await ScoutRunAsk.OfAsync(command, cancellationToken);
        var measuring = await MeasuringAsync(command, services, cancellationToken);

        if ((asked, measuring) is not (Outcome<ScoutRunAsk>.Ok { Value: var ask }, Outcome<Measuring>.Ok { Value: var how }))
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, asked is Outcome<ScoutRunAsk>.Fail f ? f.Reason : ((Outcome<Measuring>.Fail)measuring).Reason);
        }

        var (code, inputs) = await ScoutsInputs.LoadAsync(command, artifactsRequired: true, error, cancellationToken);
        if (inputs is null)
        {
            return code;
        }

        await using (inputs)
        {
            var planned = await ScoutRunPlanner.PlanAsync(inputs, ask, how.Executable, output, cancellationToken);

            return planned switch
            {
                Outcome<ScoutRun>.Ok run => await DriveAsync(command, inputs, run.Value, how, services, output, error, cancellationToken),
                Outcome<ScoutRun>.Fail fail => GateRunCommand.Refuse(error, fail.Reason.StartsWith(ScoutRunPlanner.EnvironmentMark, StringComparison.Ordinal) ? ExitCodes.Environment : ExitCodes.Configuration, fail.Reason),
                _ => ExitCodes.Configuration,
            };
        }
    }

    /// <summary><c>bench scouts resume --run &lt;id&gt;</c> — the run's frozen tasks, pins and budgets; exactly the cells not settled.</summary>
    private static async Task<int> ResumeAsync(CommandLine command, ScoutVerbServices services, TextWriter output, TextWriter error, CancellationToken cancellationToken) =>
        await WithRunAsync(command, services, "resume", error, cancellationToken, (inputs, run, how) => DriveAsync(command, inputs, run, how, services, output, error, cancellationToken));

    /// <summary><c>bench scouts rerun --cell &lt;id&gt;</c> — the next generation of that cell's lineage, measured alone: refused while any cell of
    /// the run is still open (resume first), so the campaign that follows claims exactly the new generation.</summary>
    private static async Task<int> RerunAsync(CommandLine command, ScoutVerbServices services, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(command.Value("cell"), out var cellId))
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, "scouts rerun needs --cell <guid>");
        }

        var measuring = await MeasuringAsync(command, services, cancellationToken);
        var (code, inputs) = measuring is Outcome<Measuring>.Ok ? await ScoutsInputs.LoadAsync(command, artifactsRequired: true, error, cancellationToken) : (ExitCodes.Configuration, null);

        if (inputs is null)
        {
            return measuring is Outcome<Measuring>.Fail fail ? GateRunCommand.Refuse(error, ExitCodes.Configuration, fail.Reason) : code;
        }

        await using (inputs)
        {
            var appended = await AppendAsync(inputs, cellId, cancellationToken);

            return appended switch
            {
                Outcome<ScoutRun>.Ok run => await AppendedAsync(command, inputs, run.Value, cellId, ((Outcome<Measuring>.Ok)measuring).Value, services, output, error, cancellationToken),
                Outcome<ScoutRun>.Fail fail => GateRunCommand.Refuse(error, ExitCodes.Configuration, fail.Reason),
                _ => ExitCodes.Configuration,
            };
        }
    }

    private static async Task<int> AppendedAsync(
        CommandLine command, ScoutsInputs inputs, ScoutRun run, Guid seed, Measuring how, ScoutVerbServices services, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        output.WriteLine($"appended       the next generation of cell {seed} — generation 1 and every earlier one stay");
        return await DriveAsync(command, inputs, run, how, services, output, error, cancellationToken);
    }

    private static async Task<Outcome<ScoutRun>> AppendAsync(ScoutsInputs inputs, Guid cellId, CancellationToken cancellationToken)
    {
        var cell = await inputs.Store.CellAsync(cellId, cancellationToken);

        if (cell is Outcome<ScoutCell>.Fail missing)
        {
            return Outcome<ScoutRun>.Failure(missing.Reason);
        }

        var runId = ((Outcome<ScoutCell>.Ok)cell).Value.RunId;
        var open = (await inputs.Store.CellsAsync(runId, cancellationToken)).Count(c => !c.IsTerminal);

        if (open > 0)
        {
            return Outcome<ScoutRun>.Failure($"scout run {runId} still has {open} open cell(s) — finish it first (bench scouts resume --run {runId}); a re-run measures exactly the generation it appends");
        }

        var next = await inputs.Store.NextGenerationAsync(cellId, Guid.CreateVersion7(), cancellationToken);

        return next is Outcome<ScoutCell>.Fail fail ? Outcome<ScoutRun>.Failure(fail.Reason) : await inputs.Store.LoadAsync(runId, cancellationToken);
    }

    /// <summary>The run a resuming verb is about — its flags, its price table, the store — or the exit code that says why not.</summary>
    private static async Task<int> WithRunAsync(
        CommandLine command, ScoutVerbServices services, string verb, TextWriter error, CancellationToken cancellationToken, Func<ScoutsInputs, ScoutRun, Measuring, Task<int>> body)
    {
        if (!Guid.TryParse(command.Value("run"), out var runId))
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, $"scouts {verb} needs --run <guid>");
        }

        var measuring = await MeasuringAsync(command, services, cancellationToken);
        if (measuring is Outcome<Measuring>.Fail notMeasuring)
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, notMeasuring.Reason);
        }

        var how = ((Outcome<Measuring>.Ok)measuring).Value;

        var (code, inputs) = await ScoutsInputs.LoadAsync(command, artifactsRequired: true, error, cancellationToken);
        if (inputs is null)
        {
            return code;
        }

        await using (inputs)
        {
            return await inputs.Store.LoadAsync(runId, cancellationToken) switch
            {
                Outcome<ScoutRun>.Ok ok => await body(inputs, ok.Value, how),
                Outcome<ScoutRun>.Fail fail => GateRunCommand.Refuse(error, ExitCodes.Configuration, fail.Reason),
                _ => ExitCodes.Configuration,
            };
        }
    }

    /// <summary>The entry step, then the campaign — <c>run</c>, <c>resume</c> and <c>rerun</c>.</summary>
    private static async Task<int> DriveAsync(
        CommandLine command, ScoutsInputs inputs, ScoutRun run, Measuring how, ScoutVerbServices services, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        await PrepareAsync(command, inputs, run, output, cancellationToken);
        var tasks = await inputs.Store.TasksAsync(run.Id, cancellationToken);
        output.WriteLine($"measuring      scout run {run.Id} — {(await inputs.Store.CellsAsync(run.Id, cancellationToken)).Count(c => c.State == CellState.Pending)} pending cell(s), one at a time");

        var options = new ScoutCampaignOptions(services.Owner, how.Executable) { Progress = output.WriteLine };
        var report = await inputs.Campaign(how.Executable, how.Prices, services).RunAsync(run, tasks, options, cancellationToken);

        return await FinishAsync(inputs, run, report, output, error);
    }

    /// <summary>The entry step every claiming verb runs first: the owner-checked sweep, then recovery of every orphaned attempt.</summary>
    internal static async Task<ScoutPrepareReport> PrepareAsync(CommandLine command, ScoutsInputs inputs, ScoutRun run, TextWriter output, CancellationToken cancellationToken)
    {
        var prepared = await inputs.Recovery().PrepareAsync(run.Id, ScoutsInputs.StaleAfter(command), cancellationToken);
        output.WriteLine(
            $"prepared       {prepared.Sweep.Requeued} claim(s) handed back, {prepared.Sweep.Abandoned} abandoned; attempts recovered: "
            + $"{prepared.Released} released, {prepared.Settled} settled from their stream, {prepared.ChargedUnknown} charged as unknown cost");
        return prepared;
    }

    private static async Task<int> FinishAsync(ScoutsInputs inputs, ScoutRun run, ScoutCampaignReport report, TextWriter output, TextWriter error)
    {
        var ledger = (await inputs.Store.LoadAsync(run.Id, CancellationToken.None)).Match(r => r.Ledger, _ => run.Ledger);
        var resume = $"bench scouts resume --run {run.Id}";

        output.WriteLine($"campaign       {report.Settled} cell(s) settled — {report.Stop}: {report.Reason}");
        output.WriteLine(Ledger(ledger));

        return report.Stop switch
        {
            ScoutStop.Drained when report.Settled > 0 => ExitCodes.Pass,
            ScoutStop.Drained => GateRunCommand.Refuse(error, ExitCodes.NoReport, "no cell was produced — nothing was pending"),
            ScoutStop.Cancelled => GateRunCommand.Refuse(error, ExitCodes.NoReport, $"stopped after {report.Settled} cell(s) — the run is resumable ({resume})"),
            _ => GateRunCommand.Refuse(error, ExitCodes.Environment, $"{report.Reason} ({resume} once the cause is fixed)"),
        };
    }

    /// <summary>The ledger identity, spelled out (§3.10): spent = Σ settled + Σ unknown charges, never a bare equality a crash would break.</summary>
    public static string Ledger(SpendLedger ledger) =>
        string.Create(CultureInfo.InvariantCulture,
            $"ledger         ${ledger.SettledUsd} settled + ${ledger.UnknownUsd} unknown = ${ledger.SpentUsd} spent of the ${ledger.CeilingUsd} ceiling (${ledger.ReservedUsd} still reserved)");
}
