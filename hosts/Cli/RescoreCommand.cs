using Bench.Application;
using Bench.Application.Delivered;
using Bench.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bench.Cli;

/// <summary><c>bench rescore</c> — recompute the delivered-work policy over stored payloads.
///
/// <para><b>Zero model calls, and that is the whole verb.</b> The payload table is kept permanently so a
/// published score can be re-derived years later; this is where that stops being a claim. Nothing here
/// constructs a runtime, so nothing here can reach one — and a test counts the invocations to prove it,
/// because "we did not call a model" is exactly the kind of promise that decays silently.</para>
///
/// <para>What it recomputes is the POLICY — the near-duplicate cap and the rescue allowance. The gate's
/// verdict is a property of the decomposition accepted at the time and re-deriving it would need the diff's
/// figures, which no payload carries; its stored verdict is the record.</para>
///
/// <para><b>It reports rather than writes.</b> A rescore that overwrote the stored score would destroy the
/// evidence a reader needs to see that the policy changed — the raw and the applied both being kept is the
/// port's own rule, and it applies to the recomputation as much as to the original.</para>
/// </summary>
public static class RescoreCommand
{
    public static async Task<int> RunAsync(
        CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var connection = command.Value("db", Environment.GetEnvironmentVariable("BENCH_DB") ?? string.Empty);

        if (connection.Length == 0)
        {
            error.WriteLine("bench: --db (or BENCH_DB) is required — the payloads live there");
            return ExitCodes.Configuration;
        }

        if (!Guid.TryParse(command.Value("run"), out var runId))
        {
            error.WriteLine("bench: --run <guid> is required — rescoring is per run");
            return ExitCodes.Configuration;
        }

        await using var provider = CliContainer.ForSweep(connection, CliLogging.Start());

        if (!await MigratedAsync(provider, error, cancellationToken))
        {
            return ExitCodes.Environment;
        }

        return await ReportAsync(provider, runId, command.Has("json"), output, cancellationToken);
    }

    private static async Task<int> ReportAsync(
        IServiceProvider provider,
        Guid runId,
        bool asJson,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();

        var results = await scope.ServiceProvider.GetRequiredService<PostgresResultStore>()
            .ForRunAsync(runId, cancellationToken);

        var rescore = new DeliveredRescore(
            new PostgresStagePayloadStore(scope.ServiceProvider.GetRequiredService<BenchDbContext>()));

        var report = await rescore.ForResultsAsync([.. results.Select(r => r.Id)], cancellationToken);

        Render(report, asJson, output);

        // A run with nothing rescorable is not a failure: every result measured before payloads were kept
        // is in that state, and reporting it as an error would make an ordinary history look broken.
        return ExitCodes.Pass;
    }

    private static void Render(RescoreReport report, bool asJson, TextWriter output)
    {
        if (asJson)
        {
            output.WriteLine(System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    rescored = report.Rescored.Select(r => new
                    {
                        result = r.ResultId,
                        total = r.Recomputed.Total,
                        adjustments = r.Recomputed.Adjustments,
                        protocol = r.Protocol,
                    }),
                    skipped = report.Skipped.Select(s => new { result = s.ResultId, reason = s.Reason }),
                },
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

            return;
        }

        output.WriteLine(report.Describe);

        foreach (var rescored in report.Rescored)
        {
            output.WriteLine($"  {rescored.Describe}");
        }

        // Named, never counted silently: "no payloads" and "the payload no longer reads" send a reader to
        // two different places, and only the second is a defect.
        foreach (var skipped in report.Skipped)
        {
            output.WriteLine($"  skipped {skipped.ResultId} — {skipped.Reason}");
        }
    }

    private static async Task<bool> MigratedAsync(
        IServiceProvider provider, TextWriter error, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<BenchDbContext>().Database
                .MigrateAsync(cancellationToken);

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            error.WriteLine($"bench: the database is unreachable — {ex.Message}");
            return false;
        }
    }
}
