using Bench.Application;
using Bench.Domain;
using Bench.Domain.Models;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Infrastructure.Models;
using Bench.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bench.Cli;

/// <summary>`bench run` — plan a run, then actually execute its legs.
/// <para>
/// <b>It reports; it does not judge.</b> There is no bar yet, so a leg that scored badly is a number rather
/// than a failure, and the exit code says whether the MEASUREMENT happened — not whether the subject did
/// well. Turning scores into a pass before anybody has agreed a threshold is how a harness starts arguing
/// with its operator.
/// </para></summary>
public static class RunCommand
{
    public static async Task<int> RunAsync(CommandLine command, TextWriter output, TextWriter error)
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

        var settings = ((Outcome<RunInputs>.Ok)inputs).Value;
        await using var provider = Services(settings.ConnectionString);

        var prepared = await PrepareAsync(provider, settings, output, error);

        if (prepared is null)
        {
            return ExitCodes.Environment;
        }

        return await ExecuteAsync(provider, prepared.Value.Run, prepared.Value.Plan, command, output);
    }

    private static async Task<(BenchRun Run, LegPlan Plan)?> PrepareAsync(
        ServiceProvider provider, RunInputs settings, TextWriter output, TextWriter error)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BenchDbContext>();

        try
        {
            await db.Database.MigrateAsync();
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

        var created = await runs.CreateAsync(run, planned, CancellationToken.None);

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
        TextWriter output)
    {
        var owner = $"cli-{Environment.ProcessId}";
        var done = 0;

        output.WriteLine();

        while (true)
        {
            await using var scope = provider.CreateAsyncScope();
            var runner = scope.ServiceProvider.GetRequiredService<LegRunner>();

            var leg = await runner.RunNextAsync(run.Id, owner, plan, CancellationToken.None);

            if (leg is Outcome<LegResult>.Fail stop)
            {
                if (stop.Reason.Contains("no pending cell", StringComparison.Ordinal))
                {
                    break;
                }

                output.WriteLine($"  leg    REFUSED — {stop.Reason}");
                continue;
            }

            Report(output, ((Outcome<LegResult>.Ok)leg).Value, ++done);
        }

        return await SummariseAsync(provider, run.Id, done, command, output);
    }

    private static void Report(TextWriter output, LegResult result, int index)
    {
        output.WriteLine($"  leg {index,-3} {(result.Passed ? "pass" : "FAIL")}  {Trim(result.Prompt, 58)}");

        foreach (var metric in result.Metrics)
        {
            output.WriteLine($"         {(metric.Failed ? "✗" : "·")} {metric.Name} = {metric.Value}  ({metric.Reason})");
        }
    }

    private static async Task<int> SummariseAsync(
        ServiceProvider provider, Guid runId, int done, CommandLine command, TextWriter output)
    {
        await using var scope = provider.CreateAsyncScope();
        var results = scope.ServiceProvider.GetRequiredService<PostgresResultStore>();
        var runs = scope.ServiceProvider.GetRequiredService<PostgresRunStore>();

        var progress = await runs.ProgressAsync(runId, CancellationToken.None);
        var scored = await results.ForRunAsync(runId, CancellationToken.None);

        output.WriteLine();
        output.WriteLine($"legs     {progress.Describe}");
        output.WriteLine($"scored   {scored.Count(r => r.Passed)} of {scored.Count} passed every expectation");

        if (command.Has("json"))
        {
            output.WriteLine(System.Text.Json.JsonSerializer.Serialize(
                new { runId, progress.Settled, progress.Abandoned, scored = scored.Count, passed = scored.Count(r => r.Passed) },
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }

        // Nothing measured is NOT a pass. And a low score is not a failure either: without an agreed bar,
        // the exit code answers "did the measurement happen", never "was the subject good".
        return done == 0 ? ExitCodes.NoReport : ExitCodes.Pass;
    }

    private static ServiceProvider Services(string connectionString) =>
        new ServiceCollection()
            .AddHttpClient()
            .AddDbContext<BenchDbContext>(options => options.UseNpgsql(connectionString))
            .AddScoped<PostgresRunStore>()
            .AddScoped<PostgresResultStore>()
            .AddScoped<IRunStore>(s => s.GetRequiredService<PostgresRunStore>())
            .AddScoped<IResultStore>(s => s.GetRequiredService<PostgresResultStore>())
            .AddScoped<IModelRuntime, OpenAiCompatibleRuntime>()
            .AddSingleton(TimeProvider.System)
            .AddScoped<LegRunner>()
            .AddLogging()
            .AddSingleton(NullLoggerFactory.Instance)
            .BuildServiceProvider();

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

    private static string Trim(string text, int max) =>
        text.Length <= max ? text : text[..max].TrimEnd() + "…";

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
