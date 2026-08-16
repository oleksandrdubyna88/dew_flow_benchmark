using Bench.Application;
using Bench.Application.Registry;
using Bench.Domain;
using Bench.Domain.Models;
using Bench.Domain.Registry;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Infrastructure.Models;
using Bench.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bench.Cli;

/// <summary>`bench judge` — read a finished run's stored answers with an arbiter, and append its verdicts.
/// <para>
/// It re-scores; it never re-runs. Point a second arbiter at the same run and it judges every leg again
/// from stored evidence, at the price of its own inference and nothing else — which is the only way the
/// question "would a better judge have said something different" is affordable enough to ask.
/// </para></summary>
public static class JudgeCommand
{
    public static async Task<int> RunAsync(
        CommandLine command, TextWriter output, TextWriter error, CancellationToken stopping)
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

        return await JudgeAsync(((Outcome<JudgeInputs>.Ok)inputs).Value, suiteFile, command, output, error, stopping);
    }

    private static async Task<int> JudgeAsync(
        JudgeInputs inputs,
        string suiteFile,
        CommandLine command,
        TextWriter output,
        TextWriter error,
        CancellationToken stopping)
    {
        await using var provider = Services(inputs.ConnectionString);
        await using var scope = provider.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<BenchDbContext>();

        try
        {
            await db.Database.MigrateAsync(stopping);
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

        var arbiters = await ArbitersAsync(scope, inputs, stopping);

        if (arbiters is not Outcome<IReadOnlyList<ModelEndpoint>>.Ok(var endpoints))
        {
            return Fail(error, arbiters.Match(_ => string.Empty, reason => reason), ExitCodes.Configuration);
        }

        return await JudgeEachAsync(
            scope, inputs, endpoints, ((Outcome<Suite>.Ok)suite).Value, command, output, error, stopping);
    }

    /// <summary>Every arbiter of this run, in order.
    /// <para>
    /// The ordered list on the TEST is the source when <c>--judge-model</c> names none: a test chose its
    /// arbiters when it was created, and a re-judge months later must use what it chose rather than
    /// whatever the operator happens to type. The first is the primary — which is a sentence that only
    /// means something because the order is stored.
    /// </para></summary>
    private static async Task<Outcome<IReadOnlyList<ModelEndpoint>>> ArbitersAsync(
        AsyncServiceScope scope, JudgeInputs inputs, CancellationToken stopping)
    {
        if (inputs.AdHoc.Count > 0)
        {
            return Outcome<IReadOnlyList<ModelEndpoint>>.Success(inputs.AdHoc);
        }

        var roles = await scope.ServiceProvider.GetRequiredService<IRunRoleStore>().JudgesAsync(inputs.RunId, stopping);

        if (roles is not Outcome<IReadOnlyList<RunRole>>.Ok(var judges) || judges.Count == 0)
        {
            return Outcome<IReadOnlyList<ModelEndpoint>>.Failure(
                $"run {inputs.RunId} names no arbiters, and none was given — pass --judge-model, or create the test "
                + "with --judges so the choice travels with it");
        }

        var registry = scope.ServiceProvider.GetRequiredService<IModelRegistry>();
        var secrets = scope.ServiceProvider.GetRequiredService<ISecretSource>();
        var endpoints = new List<ModelEndpoint>(judges.Count);

        foreach (var judge in judges)
        {
            var found = await registry.FindAsync(judge.Model.Value, stopping);
            var endpoint = found.Match(model => ModelResolution.Endpoint(model, secrets), Outcome<ModelEndpoint>.Failure);

            if (endpoint is not Outcome<ModelEndpoint>.Ok(var resolved))
            {
                return Outcome<IReadOnlyList<ModelEndpoint>>.Failure(
                    endpoint.Match(_ => string.Empty, reason => reason));
            }

            endpoints.Add(resolved);
        }

        return Outcome<IReadOnlyList<ModelEndpoint>>.Success(endpoints);
    }

    /// <summary>One pass per arbiter, in order. Each writes its own metric series — `Judge verdict · {model}`
    /// — so two arbiters over one run are two readings that cannot collide, and a second pass sees only
    /// what it never finished.</summary>
    private static async Task<int> JudgeEachAsync(
        AsyncServiceScope scope,
        JudgeInputs inputs,
        IReadOnlyList<ModelEndpoint> arbiters,
        Suite suite,
        CommandLine command,
        TextWriter output,
        TextWriter error,
        CancellationToken stopping)
    {
        var runtime = scope.ServiceProvider.GetRequiredService<IModelRuntime>();
        var runner = scope.ServiceProvider.GetRequiredService<JudgeRunner>();
        var code = ExitCodes.NoReport;

        output.WriteLine($"run      {inputs.RunId}");

        foreach (var endpoint in arbiters)
        {
            var judge = new ModelJudge(runtime, endpoint, inputs.Seed);

            output.WriteLine($"arbiter  {judge.Model.Id} @ {endpoint.BaseUrl}");
            output.WriteLine($"metric   {JudgeScoring.MetricName(judge.Model.Id)}");
            output.WriteLine();

            var judged = await runner.JudgeRunAsync(inputs.RunId, suite, judge, stopping);

            if (judged is Outcome<JudgeReport>.Fail failed)
            {
                return Fail(error, failed.Reason, ExitCodes.NoReport);
            }

            // Any arbiter that judged something makes the pass a report. One that judged nothing does not
            // erase what an earlier one said.
            code = Math.Min(code, Write(command, output, ((Outcome<JudgeReport>.Ok)judged).Value));
        }

        return code;
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
        CliContainer.ForJudge(connectionString, CliLogging.Start());

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

        // No --judge-model means "use the arbiters the test chose". The ad-hoc pair stays for pointing a
        // second opinion at a finished run without registering it first.
        if (command.Value("judge-model").Length == 0)
        {
            return Outcome<JudgeInputs>.Success(new JudgeInputs(runId, [], command.Int("seed", 1), connection));
        }

        return ModelRef.Parse(command.Value("judge-model"), Hosting(command)).Match(
            model => ModelEndpoint.Parse(model, command.Value("judge-url")).Match(
                endpoint => Outcome<JudgeInputs>.Success(
                    new JudgeInputs(runId, [endpoint], command.Int("seed", 1), connection)),
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

    /// <param name="AdHoc">The <c>--judge-model</c> pair, or EMPTY when the run's own arbiters are to be
    /// used. Empty rather than nullable, for the same reason the subject roster is.</param>
    private sealed record JudgeInputs(
        Guid RunId, IReadOnlyList<ModelEndpoint> AdHoc, int Seed, string ConnectionString);
}
