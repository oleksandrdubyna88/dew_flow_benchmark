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
            LongChild.Executable, LongChild.Arguments, Path.GetTempPath(), TimeSpan.FromMinutes(5), cancel.Token);

        await Task.Delay(500, TestContext.Current.CancellationToken);
        var before = LongChild.Running();
        before.Should().NotBeEmpty("the child must be running before the cancellation, or this test proves nothing");

        cancel.Cancel();

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

    /// <summary>The long-running child per OS, and how to find exactly it among running processes.</summary>
    private static class LongChild
    {
        private const string LinuxMarker = "617.3";

        public static string Executable => OperatingSystem.IsWindows() ? "ping" : "sleep";

        public static string[] Arguments => OperatingSystem.IsWindows() ? ["-n", "60", "127.0.0.1"] : [LinuxMarker];

        public static Process[] Running() => OperatingSystem.IsWindows()
            ? [.. Process.GetProcessesByName("PING"), .. Process.GetProcessesByName("ping")]
            : Process.GetProcessesByName("sleep").Where(CarriesMarker).ToArray();

        private static bool CarriesMarker(Process process)
        {
            try
            {
                return File.ReadAllText($"/proc/{process.Id}/cmdline").Contains(LinuxMarker, StringComparison.Ordinal);
            }
            catch (IOException)
            {
                return false; // it exited between the listing and the read: not a running child
            }
        }
    }
}
