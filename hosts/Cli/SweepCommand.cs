using Bench.Application;
using Bench.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bench.Cli;

/// <summary>`bench sweep` — hand back the cells a dead host left claimed.
/// <para>
/// The store has been able to do this since its first commit, and until 2026-08-16 <b>nothing called
/// it</b>: <c>SweepAsync</c> was implemented, tested, and unreachable outside its own test file, so
/// every cell a killed run had claimed stayed claimed forever and no verb existed to free it. That is
/// the audit's most instructive finding — work that is written down but never triggered — and it is why
/// the recovery now runs at every <c>bench run</c> startup as well as being an operator verb.
/// </para></summary>
public static class SweepCommand
{
    /// <summary>How long a claim may go quiet before it is treated as a dead host's.
    /// <para>
    /// Comfortably longer than a leg: the per-leg wall ceiling is ten minutes, and requeuing a cell a
    /// LIVE worker is still running would have two workers measuring the same leg. Configurable, because
    /// a deployment with different ceilings needs a different window rather than a different build.
    /// </para></summary>
    public const int DefaultStaleAfterMinutes = 30;

    public static async Task<int> RunAsync(
        CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var connection = command.Value("db", Environment.GetEnvironmentVariable("BENCH_DB") ?? string.Empty);

        if (connection.Length == 0)
        {
            error.WriteLine("bench: --db (or BENCH_DB) is required — the stranded claims live there");
            return ExitCodes.Configuration;
        }

        await using var provider = CliContainer.ForSweep(connection, CliLogging.Start());

        if (!await MigratedAsync(provider, error, cancellationToken))
        {
            return ExitCodes.Environment;
        }

        var report = await RecoverAsync(provider, StaleAfter(command), output, cancellationToken);

        if (command.Has("json"))
        {
            output.WriteLine(System.Text.Json.JsonSerializer.Serialize(
                new { requeued = report.Requeued, abandoned = report.Abandoned, staleAfterMinutes = StaleAfter(command).TotalMinutes },
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }

        // Sweeping nothing is a normal, healthy answer: it means no host died since the last one.
        return ExitCodes.Pass;
    }

    /// <summary>The recovery itself, shared by this verb and by every <c>bench run</c> startup.
    /// <para>
    /// Guarded whole — scope, resolution and query — and never fatal. A sweep that fails must not stop
    /// the campaign it precedes: the cells it would have freed stay claimed, which is exactly where they
    /// already were.
    /// </para></summary>
    public static async Task<SweepReport> RecoverAsync(
        IServiceProvider provider, TimeSpan staleAfter, TextWriter output, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var runs = scope.ServiceProvider.GetRequiredService<PostgresRunStore>();

            var report = await runs.SweepAsync(staleAfter, cancellationToken);

            output.WriteLine($"swept    {report.Describe} (claims idle for more than {staleAfter.TotalMinutes:0} min)");
            return report;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "The crash-recovery sweep failed; the campaign continues without it");
            output.WriteLine($"warn     the crash-recovery sweep failed — {ex.Message.Split('\n')[0]}");
            return new SweepReport(0, 0);
        }
    }

    public static TimeSpan StaleAfter(CommandLine command) =>
        TimeSpan.FromMinutes(Math.Max(1, command.Int("stale-after-minutes", DefaultStaleAfterMinutes)));

    private static async Task<bool> MigratedAsync(
        IServiceProvider provider, TextWriter error, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();

        try
        {
            await scope.ServiceProvider.GetRequiredService<BenchDbContext>().Database.MigrateAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            error.WriteLine($"bench: the store is not reachable — {ex.Message.Split('\n')[0]}");
            return false;
        }
    }
}
