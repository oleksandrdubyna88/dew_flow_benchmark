using Bench.Application;
using Bench.Application.Bank;
using Bench.Domain;
using Bench.Domain.Bank;
using Bench.Domain.Models;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bench.Cli;

/// <summary>`bench run` — plan a run, recover what a crash stranded, then actually execute its legs.
/// <para>
/// <b>It reports; it does not judge.</b> There is no bar yet, so a leg that scored badly is a number rather
/// than a failure, and the exit code says whether the MEASUREMENT happened — not whether the subject did
/// well. Turning scores into a pass before anybody has agreed a threshold is how a harness starts arguing
/// with its operator.
/// </para>
/// <para>
/// <b>It is built to be stopped and resumed.</b> The startup sweep hands back cells a dead host claimed, the
/// drain survives one leg's failure and ends the campaign when the failures stop being one leg's, and a
/// signal leaves the run resumable instead of stranded.
/// </para></summary>
public static class RunCommand
{
    /// <summary>The summary's own budget. It runs after a stop as well as after a clean drain, so it
    /// cannot use the root token — but every wait has a ceiling, so it does not use none either.</summary>
    private static readonly TimeSpan SummaryBudget = TimeSpan.FromSeconds(30);

    /// <summary>The wall ceiling for a WHOLE leg, in seconds, when the operator names none.
    /// <para>
    /// Ten minutes, which is what the model runtime was already falling back to per completion — so the
    /// default changes no timing, only who owns it. The difference that matters is the scope: this one is
    /// the ceiling for the leg, and a lane that loops cannot multiply it by a turn count nobody bounded.
    /// It is configuration (<c>--leg-wall-seconds</c>), never a constant a call site edits.
    /// </para></summary>
    private const int DefaultLegWallSeconds = 600;

    public static async Task<int> RunAsync(
        CommandLine command, TextWriter output, TextWriter error, CancellationToken stopping)
    {
        // Same two-step as `plan`, and the split is the contract rather than pedantry: an unset flag is the
        // caller's mistake (4), a named file that is not there is the machine's (3).
        var suiteFile = command.Value("suite-file");

        // Two doors to one place: a suite file, or a selection from the bank. Both end as a FROZEN, hashed
        // suite, so a result cannot tell which door its questions came through — the stamp is the identity.
        if (suiteFile.Length == 0 && command.Value("bank-group").Length == 0)
        {
            return Fail(
                error,
                "--suite-file or --bank-group is required — a run measures a frozen question set, from a file or from the bank",
                ExitCodes.Configuration);
        }

        if (suiteFile.Length > 0 && !File.Exists(suiteFile))
        {
            return Fail(error, $"suite file not found: {suiteFile}", ExitCodes.Environment);
        }

        var inputs = Read(command);

        if (inputs is Outcome<RunInputs>.Fail bad)
        {
            return Fail(error, bad.Reason, ExitCodes.Configuration);
        }

        return await StartAsync(((Outcome<RunInputs>.Ok)inputs).Value, command, output, error, stopping);
    }

    private static async Task<int> StartAsync(
        RunInputs settings, CommandLine command, TextWriter output, TextWriter error, CancellationToken stopping)
    {
        await using var provider = Services(settings.ConnectionString);

        var prepared = await PrepareAsync(provider, settings, output, error, stopping);

        if (prepared is null)
        {
            return ExitCodes.Environment;
        }

        // Recovery BEFORE work, always: the cells a crash stranded are the ones nobody is coming back for,
        // and a harness that only ever adds cells to a queue it cannot repair fills that queue with ghosts.
        await SweepCommand.RecoverAsync(provider, SweepCommand.StaleAfter(command), output, stopping);

        return await ExecuteAsync(provider, prepared.Value.Run, prepared.Value.Plan, command, output, stopping);
    }

    private static async Task<(BenchRun Run, LegPlan Plan)?> PrepareAsync(
        ServiceProvider provider, RunInputs settings, TextWriter output, TextWriter error, CancellationToken stopping)
    {
        await using var scope = provider.CreateAsyncScope();

        if (!await MigrateAsync(scope, error, stopping))
        {
            return null;
        }

        var selection = await SelectAsync(scope, settings, stopping);

        return selection is Outcome<BankSelection>.Fail badSuite
            ? Refuse(error, badSuite.Reason)
            : await CreateAsync(scope, settings, ((Outcome<BankSelection>.Ok)selection).Value, output, error, stopping);
    }

    /// <summary>The question set, from a file or from the bank — one frozen suite either way.
    /// <para>
    /// A file-selected test carries no snapshot rows, and that absence is the honest reading: the per-test
    /// snapshot records which GROUP each question was in, and a file has no groups. A test built from the
    /// bank carries one row per question, so a later re-filing cannot move a finished report's numbers into
    /// a different column.
    /// </para></summary>
    private static async Task<Outcome<BankSelection>> SelectAsync(
        AsyncServiceScope scope, RunInputs settings, CancellationToken stopping)
    {
        if (settings.SuiteFile.Length > 0)
        {
            return SuiteJsonLoader.Load(File.ReadAllText(settings.SuiteFile), settings.Target.Commit)
                .Match(suite => Outcome<BankSelection>.Success(new BankSelection(suite, [])), Outcome<BankSelection>.Failure);
        }

        var bank = scope.ServiceProvider.GetRequiredService<IQuestionBank>();
        var found = await bank.QuestionsAsync(settings.Selection, stopping);

        return found.Match(
            entries => BankFreeze.Freeze(settings.SuiteId, entries),
            Outcome<BankSelection>.Failure);
    }

    private static async Task<bool> MigrateAsync(AsyncServiceScope scope, TextWriter error, CancellationToken stopping)
    {
        try
        {
            await scope.ServiceProvider.GetRequiredService<BenchDbContext>().Database.MigrateAsync(stopping);
            return true;
        }
        catch (Exception ex)
        {
            error.WriteLine($"bench: the store is not reachable — {ex.Message.Split('\n')[0]}");
            return false;
        }
    }

    /// <summary>The matrix, the confirmed ceilings, and the cells — in that order, because the order is
    /// the guarantee: a ceiling this runtime cannot impose stops the run BEFORE any cell exists, rather
    /// than being discovered later as a gap in a log the size of a weekend.</summary>
    private static async Task<(BenchRun Run, LegPlan Plan)?> CreateAsync(
        AsyncServiceScope scope,
        RunInputs settings,
        BankSelection selection,
        TextWriter output,
        TextWriter error,
        CancellationToken stopping)
    {
        var frozen = selection.Suite;
        var run = BenchRun.Planned(settings.Label, settings.Target, EngineRef.Filesystem(), frozen.Stamp, DateTimeOffset.UtcNow);
        var cells = Matrix.Plan(frozen.Questions, settings.Repeats, [settings.Subject], [settings.Lane]);

        if (cells is Outcome<IReadOnlyList<MatrixCell>>.Fail badMatrix)
        {
            return Refuse(error, badMatrix.Reason);
        }

        var budgets = await BudgetConfirmation.ConfirmAsync(
            scope.ServiceProvider.GetRequiredService<IModelRuntime>(), settings.Budgets, stopping);

        if (budgets is Outcome<IReadOnlyList<Budget>>.Fail refused)
        {
            return Refuse(error, refused.Reason);
        }

        var planned = ((Outcome<IReadOnlyList<MatrixCell>>.Ok)cells).Value.Select(c => RunCell.Pending(run.Id, c)).ToList();
        var created = await scope.ServiceProvider.GetRequiredService<PostgresRunStore>().CreateAsync(run, planned, stopping);

        if (created is Outcome<BenchRun>.Fail badRun)
        {
            return Refuse(error, badRun.Reason);
        }

        var snapshot = await SnapshotAsync(scope, run.Id, selection, stopping);

        if (snapshot is Outcome<int>.Fail unsnapshotted)
        {
            return Refuse(error, unsnapshotted.Reason);
        }

        var confirmed = ((Outcome<IReadOnlyList<Budget>>.Ok)budgets).Value;
        Announce(output, settings, run, selection, planned.Count, confirmed);

        return (run, LegPlan.Reading(frozen, settings.Endpoint, settings.Sampling) with { Budgets = confirmed });
    }

    /// <summary>Freezes which group each selected question was in. Written once, right after the cells, so
    /// a test that exists always has the snapshot its per-group report will read — and a re-filing next
    /// month cannot move this test's numbers into a different column.</summary>
    private static async Task<Outcome<int>> SnapshotAsync(
        AsyncServiceScope scope, Guid runId, BankSelection selection, CancellationToken stopping) =>
        selection.Questions.Count == 0
            ? Outcome<int>.Success(0)
            : await scope.ServiceProvider.GetRequiredService<IRunQuestionStore>()
                .SaveAsync(runId, selection.Questions, stopping);

    private static void Announce(
        TextWriter output,
        RunInputs settings,
        BenchRun run,
        BankSelection selection,
        int cells,
        IReadOnlyList<Budget> budgets)
    {
        var frozen = selection.Suite;

        output.WriteLine($"run      {run.Id}");
        output.WriteLine($"target   {settings.Target.Canonical}");
        output.WriteLine($"suite    {frozen.Stamp}  ({frozen.Questions.Count} question(s))");
        output.WriteLine($"matrix   {cells} cell(s) · {settings.Subject.Model.Id} · lane {settings.Lane.Name}");

        if (selection.Questions.Count > 0)
        {
            output.WriteLine(
                $"bank     {settings.Selection.Describe} — {selection.Questions.Count} question(s) frozen with their groups");
        }

        // Printed rather than assumed: this ceiling is what stands between a hung endpoint and a campaign
        // that spends days learning what its first leg already said, and it is only real once accepted.
        foreach (var budget in budgets)
        {
            output.WriteLine($"budget   {budget.Describe}");
        }

        output.WriteLine("warn     the target was not checked out, so its commit is recorded but unverified");

        if (!settings.Lane.Name.Contains("tool", StringComparison.OrdinalIgnoreCase))
        {
            output.WriteLine("warn     this lane surfaces nothing — anchor recall reads 'not applicable', and a correct");
            output.WriteLine("         answer here means the subject answered from its WEIGHTS, which is the memorisation check");
        }
    }

    private static (BenchRun Run, LegPlan Plan)? Refuse(TextWriter error, string reason)
    {
        error.WriteLine($"bench: {reason}");
        return null;
    }

    private static async Task<int> ExecuteAsync(
        ServiceProvider provider,
        BenchRun run,
        LegPlan plan,
        CommandLine command,
        TextWriter output,
        CancellationToken stopping)
    {
        // Host AND pid, not a pid alone: a sweep running on another machine would otherwise test this pid
        // against ITS own process table and confidently requeue a cell this process is still measuring.
        var owner = WorkerIdentity.Here("cli");
        var console = new LegConsole(output);

        output.WriteLine();

        var drained = await provider.GetRequiredService<LegDrain>().DrainAsync(
            token => LegAsync(provider, run.Id, owner, plan, token),
            console.Write,
            Limits(command),
            stopping);

        return await SummariseAsync(provider, run.Id, drained, command, output);
    }

    /// <summary>One leg, and the whole of it: the scope, the resolution and the work.
    /// <para>
    /// It is a single delegate on purpose — the drain wraps this in its per-unit <c>try</c>, and setup left
    /// outside that guard is the same crash through a side door.
    /// </para></summary>
    private static async Task<Outcome<LegResult>> LegAsync(
        ServiceProvider provider, Guid runId, WorkerIdentity owner, LegPlan plan, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<LegRunner>();

        return await runner.RunNextAsync(runId, owner, plan, cancellationToken);
    }

    private static DrainLimits Limits(CommandLine command) =>
        DrainLimits.Default with
        {
            ConsecutiveFailureBudget = Math.Max(
                1, command.Int("max-consecutive-failures", DrainLimits.Default.ConsecutiveFailureBudget)),
        };

    private static async Task<int> SummariseAsync(
        ServiceProvider provider, Guid runId, DrainReport drained, CommandLine command, TextWriter output)
    {
        // NOT the root token: this summary is the shutdown report itself, and a Ctrl+C that also cancelled
        // it would leave the operator with a stopped run and no idea what it did. A ceiling instead.
        using var budget = new CancellationTokenSource(SummaryBudget);

        await using var scope = provider.CreateAsyncScope();
        var results = scope.ServiceProvider.GetRequiredService<PostgresResultStore>();
        var runs = scope.ServiceProvider.GetRequiredService<PostgresRunStore>();

        var progress = await runs.ProgressAsync(runId, budget.Token);

        // Counted in the database. This line used to read every result of the run — prompt, answer and
        // every metric with its metadata — to render the two integers below.
        var scored = await results.ScoreboardAsync(runId, budget.Token);

        output.WriteLine();
        output.WriteLine($"legs     {progress.Describe}");
        output.WriteLine($"drain    {drained.Describe}");
        output.WriteLine($"scored   {scored.Passed} of {scored.Scored} passed every expectation");

        if (command.Has("json"))
        {
            output.WriteLine(System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    runId,
                    progress.Settled,
                    progress.Abandoned,
                    scored = scored.Scored,
                    passed = scored.Passed,
                    stop = drained.Stop.ToString(),
                    drained.Reason,
                    faulted = drained.Faulted,
                    refused = drained.Refused,
                },
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }

        return Exit(drained);
    }

    /// <summary>Nothing measured is NOT a pass. And a low score is not a failure either: without an agreed
    /// bar, the exit code answers "did the measurement happen", never "was the subject good".
    /// <para>
    /// The two early exits are separate answers again: a campaign that gave up after N failures in a row is
    /// an ENVIRONMENT verdict, and a campaign an operator stopped has simply not finished — both must stay
    /// distinguishable from "the subject did badly", which is not an exit code at all.
    /// </para></summary>
    private static int Exit(DrainReport drained) =>
        drained.Stop switch
        {
            DrainStop.TooManyFailures => ExitCodes.Environment,
            DrainStop.Cancelled => ExitCodes.NoReport,
            _ => drained.Scored == 0 ? ExitCodes.NoReport : ExitCodes.Pass,
        };

    private static ServiceProvider Services(string connectionString) =>
        CliContainer.ForRun(connectionString, CliLogging.Start());

    private static Outcome<RunInputs> Read(CommandLine command)
    {
        var suiteFile = command.Value("suite-file");
        var connection = command.Value("db", Environment.GetEnvironmentVariable("BENCH_DB") ?? string.Empty);
        var wallSeconds = command.Int("leg-wall-seconds", DefaultLegWallSeconds);

        if (connection.Length == 0)
        {
            return Outcome<RunInputs>.Failure("--db (or BENCH_DB) is required — a run that is not durable is not a run");
        }

        if (wallSeconds <= 0)
        {
            return Outcome<RunInputs>.Failure(
                "--leg-wall-seconds must be positive — an unbounded leg is what turns one hung endpoint into days of wall clock");
        }

        return RepoUrl.Parse(command.Value("repo")).Match(
            repo => CommitSha.Parse(command.Value("commit")).Match(
                commit => Subject(command).Match(
                    subject => ModelEndpoint.Parse(
                        subject.Model,
                        command.Value("model-url"),
                        Money(command, "input-cost"),
                        Money(command, "output-cost")).Match(
                        endpoint => Outcome<RunInputs>.Success(new RunInputs(
                            MeasurementTarget.At(repo, commit).Excluding([.. command.List("exclude")]),
                            suiteFile,
                            subject,
                            endpoint,
                            Lane.Named(command.Value("lane", "no-tools")),
                            command.Int("repeats", 1),
                            command.Value("label", "run"),
                            connection,
                            [Budget.Of(BudgetKind.Wall, BudgetScope.Question, wallSeconds)],
                            Selection(command),
                            command.Value("suite-id", "bank-selection"))),
                        Outcome<RunInputs>.Failure),
                    Outcome<RunInputs>.Failure),
                Outcome<RunInputs>.Failure),
            Outcome<RunInputs>.Failure);
    }

    private static Outcome<Subject> Subject(CommandLine command) =>
        ModelRef.Parse(command.Value("model"), Hosting(command)).Match(
            model => Outcome<Subject>.Success(new Subject(model, Sampling.Deterministic(command.Int("seed", 1)))),
            Outcome<Subject>.Failure);

    private static ModelHosting Hosting(CommandLine command) =>
        command.Value("hosting", "local").Equals("cloud", StringComparison.OrdinalIgnoreCase)
            ? ModelHosting.Cloud
            : ModelHosting.Local;

    private static decimal Money(CommandLine command, string name) =>
        decimal.TryParse(command.Value(name, "0"), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;

    private static int Fail(TextWriter error, string reason, int code)
    {
        error.WriteLine($"bench: {reason}");
        return code;
    }

    /// <summary>The bank selection, always accepted-only. A test may not measure a question nobody vouched
    /// for, and putting the filter here rather than at a call site means no future caller can forget it.</summary>
    private static BankQuery Selection(CommandLine command) =>
        BankQuery.Selection(command.Value("bank-group"), command.Int("bank-from", 0), command.Int("bank-to", 0));

    /// <param name="Budgets">The ceilings this run ASKS for. They reach a leg only after the runtime has
    /// confirmed each one — an unconfirmed budget is a budget that does not exist.</param>
    /// <param name="Selection">Which bank questions this run freezes, when it is not reading a suite file.</param>
    /// <param name="SuiteId">The name the frozen bank selection is minted under. It appears in the stamp
    /// every result carries, so it is an operator's choice rather than a generated string.</param>
    private sealed record RunInputs(
        MeasurementTarget Target,
        string SuiteFile,
        Subject Subject,
        ModelEndpoint Endpoint,
        Lane Lane,
        int Repeats,
        string Label,
        string ConnectionString,
        IReadOnlyList<Budget> Budgets,
        BankQuery Selection,
        string SuiteId)
    {
        public Sampling Sampling => Subject.Sampling;
    }
}
