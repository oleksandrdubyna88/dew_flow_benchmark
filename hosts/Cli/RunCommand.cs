using Bench.Application;
using Bench.Domain;
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

    public static async Task<int> RunAsync(
        CommandLine command, TextWriter output, TextWriter error, CancellationToken stopping)
    {
        // Same two-step as `plan`, and the split is the contract rather than pedantry: an unset flag is the
        // caller's mistake (4), a named file that is not there is the machine's (3).
        var suiteFile = command.Value("suite-file");

        if (suiteFile.Length == 0)
        {
            return Fail(error, "--suite-file is required", ExitCodes.Configuration);
        }

        if (!File.Exists(suiteFile))
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
        var db = scope.ServiceProvider.GetRequiredService<BenchDbContext>();

        try
        {
            await db.Database.MigrateAsync(stopping);
        }
        catch (Exception ex)
        {
            error.WriteLine($"bench: the store is not reachable — {ex.Message.Split('\n')[0]}");
            return null;
        }

        var suite = SuiteJsonLoader.Load(File.ReadAllText(settings.SuiteFile), settings.Target.Commit);

        if (suite is Outcome<Suite>.Fail badSuite)
        {
            error.WriteLine($"bench: {badSuite.Reason}");
            return null;
        }

        var frozen = ((Outcome<Suite>.Ok)suite).Value;
        var run = BenchRun.Planned(settings.Label, settings.Target, EngineRef.Filesystem(), frozen.Stamp, DateTimeOffset.UtcNow);

        var cells = Matrix.Plan(frozen.Questions, settings.Repeats, [settings.Subject], [settings.Lane]);

        if (cells is Outcome<IReadOnlyList<MatrixCell>>.Fail badMatrix)
        {
            error.WriteLine($"bench: {badMatrix.Reason}");
            return null;
        }

        var runs = scope.ServiceProvider.GetRequiredService<PostgresRunStore>();
        var planned = ((Outcome<IReadOnlyList<MatrixCell>>.Ok)cells).Value.Select(c => RunCell.Pending(run.Id, c)).ToList();

        var created = await runs.CreateAsync(run, planned, stopping);

        if (created is Outcome<BenchRun>.Fail badRun)
        {
            error.WriteLine($"bench: {badRun.Reason}");
            return null;
        }

        output.WriteLine($"run      {run.Id}");
        output.WriteLine($"target   {settings.Target.Canonical}");
        output.WriteLine($"suite    {frozen.Stamp}  ({frozen.Questions.Count} question(s))");
        output.WriteLine($"matrix   {planned.Count} cell(s) · {settings.Subject.Model.Id} · lane {settings.Lane.Name}");
        output.WriteLine("warn     the target was not checked out, so its commit is recorded but unverified");

        if (!settings.Lane.Name.Contains("tool", StringComparison.OrdinalIgnoreCase))
        {
            output.WriteLine("warn     this lane surfaces nothing — anchor recall reads 'not applicable', and a correct");
            output.WriteLine("         answer here means the subject answered from its WEIGHTS, which is the memorisation check");
        }

        return (run, LegPlan.Reading(frozen, settings.Endpoint, settings.Sampling));
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
        var scored = await results.ForRunAsync(runId, budget.Token);

        output.WriteLine();
        output.WriteLine($"legs     {progress.Describe}");
        output.WriteLine($"drain    {drained.Describe}");
        output.WriteLine($"scored   {scored.Count(r => r.Passed)} of {scored.Count} passed every expectation");

        if (command.Has("json"))
        {
            output.WriteLine(System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    runId,
                    progress.Settled,
                    progress.Abandoned,
                    scored = scored.Count,
                    passed = scored.Count(r => r.Passed),
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

        if (connection.Length == 0)
        {
            return Outcome<RunInputs>.Failure("--db (or BENCH_DB) is required — a run that is not durable is not a run");
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
                            connection)),
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

    private sealed record RunInputs(
        MeasurementTarget Target,
        string SuiteFile,
        Subject Subject,
        ModelEndpoint Endpoint,
        Lane Lane,
        int Repeats,
        string Label,
        string ConnectionString)
    {
        public Sampling Sampling => Subject.Sampling;
    }
}
