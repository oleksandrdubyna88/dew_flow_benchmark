using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Bench.Diagnostics;

/// <summary>
/// The one place this repository configures logging: coloured to the console, and on disk with a new file per
/// run. Every host calls it; nothing else touches a sink.
///
/// <para>Mirrored from the family rule (<c>.claude/rules/common/logging-serilog.md</c>), which is deliberately
/// copied into each repository rather than shared as a package — a repository that only reads its own rules is
/// a repository whose logging drifts silently.</para>
/// </summary>
public static class BenchLogging
{
    /// <summary>Where a run's file lands, relative to the content root.</summary>
    public const string LogFolder = "logs";

    /// <summary>
    /// The template. Short enough to read in a terminal, structured enough to grep: the level is fixed-width
    /// so columns line up, and <c>SourceContext</c> is the type that logged — without it, a dashboard showing
    /// several hosts at once is a wall of sentences with no attribution.
    /// </summary>
    private const string Template =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {App}/{Pid} {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Configures Serilog and installs it as the host's logging provider.
    ///
    /// <para>Called BEFORE <c>Build()</c>, deliberately: a host that fails while wiring itself up is exactly
    /// when the log matters, and a logger installed afterwards has nothing to say about it.</para>
    /// </summary>
    public static void AddDewFlowLogging(
        this IHostApplicationBuilder builder, string appName, bool consoleToStdErr = false)
    {
        Log.Logger = CreateLogger(
            builder.Configuration, builder.Environment.ContentRootPath, appName, consoleToStdErr);
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(Log.Logger, dispose: true);
    }

    /// <summary>
    /// The logger itself, without a host.
    ///
    /// <para>Separate from the extension above because an orchestrator's builder is not an
    /// <see cref="IHostApplicationBuilder"/> — it has its own type — and the alternative was either a second
    /// copy of every decision here or an Aspire reference inside a project that has no business carrying one.</para>
    /// </summary>
    public static Serilog.ILogger CreateLogger(
        IConfiguration appConfiguration, string contentRoot, string appName, bool consoleToStdErr = false)
    {
        var configuration = new LoggerConfiguration()
            .ReadFrom.Configuration(appConfiguration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("App", appName)
            .Enrich.WithProperty("Pid", Environment.ProcessId);

        ApplyDefaultLevels(configuration, appConfiguration);

        // Our own sink rather than Serilog's console theme, and the reason is measured rather than stylistic —
        // see AnsiConsoleSink: on Serilog.Sinks.Console 6.1.1 the theme emits NO escapes once stdout is
        // redirected, which is exactly the case an orchestrator creates.
        configuration.WriteTo.Sink(new AnsiConsoleSink(formatProvider: null, toStandardError: consoleToStdErr));

        // No theme on the file: escape codes in a file are noise to every reader, grep included.
        configuration.WriteTo.File(
            RunFilePath(contentRoot, appName),
            outputTemplate: Template,
            shared: false,
            flushToDiskInterval: TimeSpan.FromSeconds(2));

        return configuration.CreateLogger();
    }

    /// <summary>
    /// This RUN's file: a folder per day, a file per run.
    ///
    /// <para>Not a rolling sink, and that is the requirement rather than a preference. Rolling by day appends
    /// every run into one file, while the question actually asked is almost always "what did THAT run do".</para>
    /// </summary>
    public static string RunFilePath(string contentRoot, string appName)
    {
        // UTC everywhere: this family's Rust sidecar names its folder from a unix timestamp, so a local-time
        // .NET host and a UTC sidecar put the same evening's logs in two different day folders — and the one
        // time anyone correlates them is while chasing a failure across both.
        var now = DateTimeOffset.UtcNow;
        var folder = Path.Combine(contentRoot, LogFolder, now.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, $"{appName}-{now:HH-mm-ss}-{Environment.ProcessId}.log");
    }

    /// <summary>
    /// The floor, and the sources that drown everything else at Information.
    ///
    /// <para>Applied only when the configuration file says nothing, so an operator's <c>Serilog:</c> section
    /// always wins — verbosity is a config edit, never an edited call site.</para>
    /// </summary>
    private static void ApplyDefaultLevels(LoggerConfiguration logger, IConfiguration configuration)
    {
        if (configuration.GetSection("Serilog:MinimumLevel").Exists())
        {
            return;
        }

        logger.MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning);
    }
}
