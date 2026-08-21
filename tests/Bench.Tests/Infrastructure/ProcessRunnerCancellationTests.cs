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

        // A process that outlives the test unless somebody kills it. ping with a count is the one
        // long-running, argument-driven, always-installed executable Windows offers.
        var launch = ProcessRunner.RunAsync(
            "ping", ["-n", "60", "127.0.0.1"], Path.GetTempPath(), TimeSpan.FromMinutes(5), cancel.Token);

        await Task.Delay(500, TestContext.Current.CancellationToken);
        var before = Process.GetProcessesByName("PING").Concat(Process.GetProcessesByName("ping")).ToArray();
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
}
