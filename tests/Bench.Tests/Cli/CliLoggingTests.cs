using Bench.Application;
using Bench.Cli;
using Bench.Diagnostics;
using Bench.Infrastructure.Engines;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>A CLI verb that builds a container is a host, and a host logs.
/// <para>
/// Until 2026-08-16 the live <c>run</c> and <c>judge</c> containers registered a logging stack with no
/// providers plus a <c>NullLoggerFactory</c>, while this repository's correct Serilog setup sat unused
/// one project over. Everything the live path had to say about itself — the leg that was scored but
/// never settled, the verdict that could not be written — was discarded, in exactly the code path whose
/// diagnosability the whole logging rule exists to protect.
/// </para></summary>
public sealed class CliLoggingTests
{
    /// <summary>Never connected to: building a container resolves nothing, which is the point.</summary>
    private const string Connection = "Host=127.0.0.1;Port=1;Database=bench;Username=postgres;Password=x";

    [Fact]
    public void The_live_run_container_writes_its_warnings_to_a_file_per_run_rather_than_discarding_them()
    {
        using var root = new TempLogRoot();
        var logger = BenchLogging.CreateLogger(new ConfigurationBuilder().Build(), root.Path, "bench");

        using (var provider = CliContainer.ForRun(Connection, root.Path, QlnEngineOptions.None, logger))
        {
            provider.GetRequiredService<ILogger<LegRunner>>().LogWarning(
                "Cell {Cell} was scored but not settled: {Reason}", Guid.Empty, "the store refused the settle");
        }

        (logger as IDisposable)?.Dispose();

        var file = root.Files().Should().ContainSingle("a run writes one file, not one per day appended forever").Subject;
        File.ReadAllText(file).Should().Contain("was scored but not settled",
            "a crash-recovery warning nobody can read is the same as no crash-recovery warning");
    }

    [Fact]
    public void A_run_names_its_log_file_for_the_day_the_time_and_the_process()
    {
        using var root = new TempLogRoot();
        var logger = BenchLogging.CreateLogger(new ConfigurationBuilder().Build(), root.Path, "bench");
        (logger as IDisposable)?.Dispose();

        var file = root.Files().Single();

        Path.GetFileName(file).Should().MatchRegex($@"^bench-\d{{2}}-\d{{2}}-\d{{2}}-{Environment.ProcessId}\.log$",
            "the pid disambiguates two hosts started in the same second");
        Path.GetFileName(Path.GetDirectoryName(file)!).Should().Be(
            DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"),
            "UTC everywhere — a local-time .NET host and a UTC sidecar file the same evening under two different days");
    }

    [Fact]
    public void The_judge_container_logs_through_the_same_sinks_as_the_run_container()
    {
        using var root = new TempLogRoot();
        var logger = BenchLogging.CreateLogger(new ConfigurationBuilder().Build(), root.Path, "bench");

        using (var provider = CliContainer.ForJudge(Connection, logger))
        {
            provider.GetRequiredService<ILogger<JudgeRunner>>().LogError(
                "Storing the verdict for cell {Cell} failed: {Reason}", Guid.Empty, "the store was unreachable");
        }

        (logger as IDisposable)?.Dispose();

        File.ReadAllText(root.Files().Single()).Should().Contain("Storing the verdict",
            "two containers with one composition cannot drift apart on this");
    }

    [Fact]
    public void A_day_folder_older_than_the_window_is_pruned_at_startup()
    {
        using var root = new TempLogRoot();
        var now = new DateTimeOffset(2026, 8, 16, 9, 0, 0, TimeSpan.Zero);
        Day(root, "2026-07-01");
        Day(root, "2026-08-10");
        Day(root, "2026-08-16");

        var pruned = BenchLogging.PruneLogFolders(root.Path, BenchLogging.DefaultRetentionDays, now);

        // A file per run with no reaper is a disk that fills, and on a 24/7 machine the "eventually" is
        // a date. Fourteen days back from the 16th keeps the 10th and retires July.
        pruned.Should().Equal("2026-07-01");
        root.Days().Should().BeEquivalentTo(["2026-08-10", "2026-08-16"]);
    }

    [Fact]
    public void A_folder_whose_name_is_not_a_day_is_never_deleted()
    {
        using var root = new TempLogRoot();
        Day(root, "archive-do-not-touch");
        Day(root, "2026-07-01");

        BenchLogging.PruneLogFolders(root.Path, 14, new DateTimeOffset(2026, 8, 16, 9, 0, 0, TimeSpan.Zero));

        // This method deletes directory TREES. Anything it does not positively recognise as a day-folder
        // it has no business removing, whoever put it there.
        root.Days().Should().Contain("archive-do-not-touch");
    }

    [Fact]
    public void A_retention_window_of_zero_keeps_everything_for_the_operator_job_that_owns_it()
    {
        using var root = new TempLogRoot();
        Day(root, "2020-01-01");

        BenchLogging.PruneLogFolders(root.Path, 0, DateTimeOffset.UtcNow).Should().BeEmpty();
        root.Days().Should().ContainSingle("the rule allows exactly two owners, and zero names the other one");
    }

    private static void Day(TempLogRoot root, string name) =>
        Directory.CreateDirectory(Path.Combine(root.Path, BenchLogging.LogFolder, name));

    private sealed class TempLogRoot : IDisposable
    {
        public TempLogRoot() =>
            Path = Directory.CreateDirectory(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bench-logs-{Guid.NewGuid():N}")).FullName;

        public string Path { get; }

        public IReadOnlyList<string> Days() =>
            Directory.Exists(System.IO.Path.Combine(Path, BenchLogging.LogFolder))
                ? [.. Directory.GetDirectories(System.IO.Path.Combine(Path, BenchLogging.LogFolder))
                    .Select(d => System.IO.Path.GetFileName(d)!)]
                : [];

        public IReadOnlyList<string> Files() =>
            Directory.Exists(System.IO.Path.Combine(Path, BenchLogging.LogFolder))
                ? Directory.GetFiles(System.IO.Path.Combine(Path, BenchLogging.LogFolder), "*.log", SearchOption.AllDirectories)
                : [];

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
