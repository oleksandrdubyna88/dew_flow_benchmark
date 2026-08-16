using Bench.Diagnostics;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Bench.Cli;

/// <summary>Installs the process-wide logger for a verb that does real work.
/// <para>
/// Called BEFORE the container is built, deliberately: a verb that fails while wiring itself up is
/// exactly when the log matters, and a logger installed afterwards has nothing to say about it. The
/// caller flushes it in a <c>finally</c> — <see cref="Log.CloseAndFlush"/> — because a file sink that
/// is never closed loses whatever the last two seconds had to say.
/// </para>
/// <para>
/// <b>Only the working verbs call this.</b> <c>help</c> and <c>version</c> touch nothing and produce
/// nothing, and a log file per <c>bench help</c> would be a directory that grows for no reader.
/// </para></summary>
public static class CliLogging
{
    /// <summary>The name in every line and in every file name.</summary>
    public const string AppName = "bench";

    private static readonly Lock Gate = new();
    private static bool _started;

    /// <summary>Idempotent: one process is one run, and one run is one file. Returns the logger the
    /// containers write through.</summary>
    public static Serilog.ILogger Start()
    {
        lock (Gate)
        {
            if (!_started)
            {
                var root = Directory.GetCurrentDirectory();
                Log.Logger = BenchLogging.CreateLogger(Configuration(root), root, AppName);
                _started = true;
            }

            return Log.Logger;
        }
    }

    /// <summary>Levels come from configuration, never from a call site — an optional <c>appsettings.json</c>
    /// beside the invocation, and the environment over it, which is the route an orchestrator has.</summary>
    private static IConfiguration Configuration(string contentRoot) =>
        new ConfigurationBuilder()
            .SetBasePath(contentRoot)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
}
