using Bench.Application;
using Bench.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bench.Cli;

/// <summary>`bench prune` — release the source TEXT of retrieved hits older than the retention window.
/// <para>
/// The owner of the one surface in this schema that grows without bound. At a limit of 20 hits, snippets are
/// most of a cell's bytes and the corpus at the pinned commit reproduces every one of them, so they are kept
/// raw for a window and dropped after. Ranks, scores, paths, spans and channels are never touched — every
/// retrieval metric stays recomputable forever, over runs measured years earlier.
/// </para>
/// <para>
/// Two front doors on purpose, exactly like the sweep beside it: an operator verb, AND a call at every
/// <c>bench run</c> startup. A retention policy that only runs when somebody remembers to run it is a
/// policy nobody runs — which is the finding that made <c>SweepAsync</c> unreachable for its first two
/// weeks.
/// </para></summary>
public static class PruneCommand
{
    public static async Task<int> RunAsync(
        CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var connection = command.Value("db", Environment.GetEnvironmentVariable("BENCH_DB") ?? string.Empty);

        if (connection.Length == 0)
        {
            error.WriteLine("bench: --db (or BENCH_DB) is required — the hits live there");
            return ExitCodes.Configuration;
        }

        var days = command.Int("hit-retention-days", DefaultRetentionDays);

        if (days <= 0)
        {
            // Zero means "keep everything" for a RUN, which is a legitimate choice. As an explicit prune it
            // would mean "drop the text of every hit ever retrieved", and a destructive reading of a value
            // whose other reading is "keep everything" is not one to guess at.
            error.WriteLine(
                "bench: --hit-retention-days must be positive here — 0 means 'keep everything' on a run, and "
                + "guessing that it means 'drop everything' on a prune is not a guess this verb makes");
            return ExitCodes.Configuration;
        }

        await using var provider = CliContainer.ForSweep(connection, CliLogging.Start());

        if (!await MigratedAsync(provider, error, cancellationToken))
        {
            return ExitCodes.Environment;
        }

        var released = await ReleaseAsync(provider, days, output, cancellationToken);

        if (command.Has("json"))
        {
            output.WriteLine(System.Text.Json.JsonSerializer.Serialize(
                new { hits = released.Hits, bytesFreed = released.BytesFreed, retentionDays = days },
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }

        // Pruning nothing is a normal, healthy answer: it means nothing is old enough yet.
        return ExitCodes.Pass;
    }

    /// <summary>The retention itself, shared by this verb and by every <c>bench run</c> startup.
    /// <para>
    /// Guarded whole and never fatal, for the same reason the sweep is: a retention pass that fails must not
    /// stop the campaign it precedes. The text it would have released stays where it already was.
    /// </para>
    /// <para>
    /// <paramref name="retentionDays"/> of zero KEEPS everything — the choice an operator makes for a small
    /// run whose database will be published as-is.
    /// </para></summary>
    public static async Task<SnippetPruning> ReleaseAsync(
        IServiceProvider provider, int retentionDays, TextWriter output, CancellationToken cancellationToken)
    {
        if (retentionDays <= 0)
        {
            return new SnippetPruning(0, 0);
        }

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var results = scope.ServiceProvider.GetRequiredService<PostgresResultStore>();

            var released = await results.PruneHitSnippetsAsync(
                DateTimeOffset.UtcNow.AddDays(-retentionDays), cancellationToken);

            output.WriteLine($"pruned   {released.Describe} (hits older than {retentionDays} day(s))");
            return released;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "The hit-snippet retention pass failed; the campaign continues without it");
            output.WriteLine($"warn     the hit-snippet retention pass failed — {ex.Message.Split('\n')[0]}");
            return new SnippetPruning(0, 0);
        }
    }

    /// <summary>How long a retrieved hit keeps its source TEXT, in days.
    /// <para>
    /// Seven, which is the window the sibling project's own size history settled on
    /// (<c>dew_flow_rag_qln · SizeHistoryStore</c>) and which this schema copies rather than re-argues.
    /// </para>
    /// <para>
    /// ONE constant, read by this verb and by <c>bench run</c>'s startup pass. Two defaults for one policy
    /// would mean a database pruned to two different windows depending on which door the pass came through.
    /// </para></summary>
    public const int DefaultRetentionDays = 7;

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
