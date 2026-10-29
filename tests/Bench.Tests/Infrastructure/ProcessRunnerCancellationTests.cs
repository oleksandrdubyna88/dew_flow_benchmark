using System.Diagnostics;
using Bench.Infrastructure.Process;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>What a launch does when the CALLER cancels — the drain's post-grace stop, a Ctrl+C.
/// <para>
/// The child must die with the wait: an orphaned `claude`/`git`/`dotnet` keeps file handles open
/// inside a scratch worktree, which then makes the cleanup fail, which leaves a stale worktree entry
/// nothing sweeps. Found by review: the kill guard fired only for the INTERNAL timeout, so every
/// graceful shutdown leaked the child it was shutting down.
/// </para></summary>
public sealed class ProcessRunnerCancellationTests
{
    [Fact]
    public async Task External_cancellation_kills_the_child_it_was_waiting_on()
    {
        using var cancel = new CancellationTokenSource();

        // A process that outlives the test unless somebody kills it. On Windows, ping with a count is the one
        // long-running, argument-driven, always-installed executable. `-n` is NOT a count on Linux (iputils reads it as
        // "numeric output"), so there ping exited at once and the test was red whenever the 500 ms look came too late —
        // measured 2026-10-29: red on every local WSL run, red once on CI after 8 green main runs. Linux uses `sleep`
        // with a marker duration, so the lookup below finds this test's child and never another `sleep`.
        var launch = ProcessRunner.RunAsync(
            ChildExecutable, ChildArguments, Path.GetTempPath(), TimeSpan.FromMinutes(5), cancel.Token);

        var before = await RunningChildrenAsync();
        before.Should().NotBeEmpty("the child must be running before the cancellation, or this test proves nothing");

        await cancel.CancelAsync();

        var observed = async () => await launch;
        await observed.Should().ThrowAsync<OperationCanceledException>(
            "an external cancellation is the caller's own signal and must still surface as one");

        // The kill is entire-process-tree and synchronous-ish; give the OS a moment, then insist.
        foreach (var survivor in before)
        {
            var gone = false;

            for (var attempt = 0; attempt < 20 && !gone; attempt++)
            {
                survivor.Refresh();
                gone = survivor.HasExited;

                if (!gone)
                {
                    await Task.Delay(100, TestContext.Current.CancellationToken);
                }
            }

            gone.Should().BeTrue(
                $"pid {survivor.Id} must not outlive the wait — an orphan holds worktree handles nothing sweeps");
        }
    }

    private const string LinuxMarker = "617.3";

    private static string ChildExecutable => OperatingSystem.IsWindows() ? "ping" : "sleep";

    private static string[] ChildArguments => OperatingSystem.IsWindows() ? ["-n", "60", "127.0.0.1"] : [LinuxMarker];

    /// <summary>Polls up to 5 s for the child to appear: a loaded runner may start it later than any fixed delay.</summary>
    private static async Task<Process[]> RunningChildrenAsync()
    {
        var found = RunningChildren();

        for (var attempt = 0; attempt < 50 && found.Length == 0; attempt++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
            found = RunningChildren();
        }

        return found;
    }

    private static Process[] RunningChildren() => OperatingSystem.IsWindows()
        ? [.. Process.GetProcessesByName("PING"), .. Process.GetProcessesByName("ping")]
        : [.. Process.GetProcessesByName("sleep").Where(CarriesMarker)];

    /// <summary>True when the process's argv holds the marker as a whole argument (cmdline is NUL-separated).</summary>
    private static bool CarriesMarker(Process process)
    {
        try
        {
            return File.ReadAllText($"/proc/{process.Id}/cmdline").Split('\0').Contains(LinuxMarker, StringComparer.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false; // it exited between the listing and the read, or it is not ours to read: not this child
        }
    }
}
