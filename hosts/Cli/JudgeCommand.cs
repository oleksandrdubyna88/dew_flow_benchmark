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

/// <summary>`bench judge` — read a finished run's stored answers with an arbiter, and append its verdicts.
/// <para>
/// It re-scores; it never re-runs. Point a second arbiter at the same run and it judges every leg again
/// from stored evidence, at the price of its own inference and nothing else — which is the only way the
/// question "would a better judge have said something different" is affordable enough to ask.
/// </para></summary>
public static class JudgeCommand
{
    public static async Task<int> RunAsync(CommandLine command, TextWriter output, TextWriter error)
    {
        var suiteFile = command.Value("suite-file");

        if (suiteFile.Length == 0)
        {
            return Fail(error, "--suite-file is required — a verdict needs the reference answers", ExitCodes.Configuration);
        }

        if (!File.Exists(suiteFile))
        {
            return Fail(error, $"suite file not found: {suiteFile}", ExitCodes.Environment);
        }

        var inputs = Read(command);

        if (inputs is Outcome<JudgeInputs>.Fail bad)
        {
            return Fail(error, bad.Reason, ExitCodes.Configuration);
        }

        return await JudgeAsync(((Outcome<JudgeInputs>.Ok)inputs).Value, suiteFile, command, output, error);
    }

    private static async Task<int> JudgeAsync(
        JudgeInputs inputs, string suiteFile, CommandLine command, TextWriter output, TextWriter error)
    {
        await using var provider = Services(inputs.ConnectionString);
        await using var scope = provider.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<BenchDbContext>();

        try
        {
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            return Fail(error, $"the store is not reachable — {ex.Message.Split('\n')[0]}", ExitCodes.Environment);
        }

        var suite = SuiteJsonLoader.Load(File.ReadAllText(suiteFile), AnyCommit);

        if (suite is Outcome<Suite>.Fail badSuite)
        {
            return Fail(error, badSuite.Reason, ExitCodes.Configuration);
        }

        var runtime = scope.ServiceProvider.GetRequiredService<IModelRuntime>();
        var judge = new ModelJudge(runtime, inputs.Endpoint, inputs.Seed);
        var runner = scope.ServiceProvider.GetRequiredService<JudgeRunner>();

        output.WriteLine($"run      {inputs.RunId}");
        output.WriteLine($"arbiter  {judge.Model.Id} @ {inputs.Endpoint.BaseUrl}");
        output.WriteLine($"metric   {JudgeScoring.MetricName(judge.Model.Id)}");
        output.WriteLine();

        var judged = await runner.JudgeRunAsync(inputs.RunId, ((Outcome<Suite>.Ok)suite).Value, judge, CancellationToken.None);

        return judged.Match(
            report => Write(command, output, report),
            reason => Fail(error, reason, ExitCodes.NoReport));
    }

    private static int Write(CommandLine command, TextWriter output, JudgeReport report)
    {
        output.WriteLine($"verdicts {report.Describe}");

        if (report.SelfJudged > 0)
        {
            output.WriteLine("warn     the arbiter and the subject are the same model — every verdict here is");
            output.WriteLine("         marked selfJudged so an aggregate can exclude it, but it is not an independent reading");
        }

        if (report.NotJudgeable > 0)
        {
            output.WriteLine($"warn     {report.NotJudgeable} leg(s) had no reference answer to judge against — a gap in the suite, not a verdict");
        }

        if (command.Has("json"))
        {
            output.WriteLine(System.Text.Json.JsonSerializer.Serialize(
                report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }

        // Judged nothing is not a pass. A verdict of NO on every leg IS a completed measurement, and still
        // exits 0 — the arbiter's opinion of the subject is the report's content, never the harness's health.
        return report.Judged == 0 ? ExitCodes.NoReport : ExitCodes.Pass;
    }

    /// <summary>The suite is re-read only for its reference answers, so its anchors are never resolved
    /// against anything. Loading needs A commit; this one says plainly that it is not used.</summary>
    private static CommitSha AnyCommit => CommitSha.Parse(new string('0', 40)).Match(c => c, _ => throw new InvalidOperationException());

    private static ServiceProvider Services(string connectionString) =>
        new ServiceCollection()
            .AddHttpClient()
            .AddDbContext<BenchDbContext>(options => options.UseNpgsql(connectionString))
            .AddScoped<PostgresResultStore>()
            .AddScoped<IResultStore>(s => s.GetRequiredService<PostgresResultStore>())
            .AddScoped<IModelRuntime, OpenAiCompatibleRuntime>()
            .AddScoped<JudgeRunner>()
            .AddLogging()
            .AddSingleton(NullLoggerFactory.Instance)
            .BuildServiceProvider();

    private static Outcome<JudgeInputs> Read(CommandLine command)
    {
        if (!Guid.TryParse(command.Value("run"), out var runId))
        {
            return Outcome<JudgeInputs>.Failure($"--run must be a run id, not '{command.Value("run")}'");
        }

        var connection = command.Value("db", Environment.GetEnvironmentVariable("BENCH_DB") ?? string.Empty);

        if (connection.Length == 0)
        {
            return Outcome<JudgeInputs>.Failure("--db (or BENCH_DB) is required — the answers to judge live there");
        }

        return ModelRef.Parse(command.Value("judge-model"), Hosting(command)).Match(
            model => ModelEndpoint.Parse(model, command.Value("judge-url")).Match(
                endpoint => Outcome<JudgeInputs>.Success(
                    new JudgeInputs(runId, endpoint, command.Int("seed", 1), connection)),
                Outcome<JudgeInputs>.Failure),
            Outcome<JudgeInputs>.Failure);
    }

    private static ModelHosting Hosting(CommandLine command) =>
        command.Value("judge-hosting", "local").Equals("cloud", StringComparison.OrdinalIgnoreCase)
            ? ModelHosting.Cloud
            : ModelHosting.Local;

    private static int Fail(TextWriter error, string reason, int code)
    {
        error.WriteLine($"bench: {reason}");
        return code;
    }

    private sealed record JudgeInputs(Guid RunId, ModelEndpoint Endpoint, int Seed, string ConnectionString);
}
