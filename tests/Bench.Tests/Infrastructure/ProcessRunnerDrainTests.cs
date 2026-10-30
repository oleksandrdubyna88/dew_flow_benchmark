using System.Diagnostics;
using Bench.Infrastructure.Process;
using Bench.Tests.ModelArms;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>Model arms E2.S3 fix round — after the kill, the launcher waits for the two readers only up to <see cref="ProcessRunner.DrainAfterKill"/>. The subject is a
/// descendant that ESCAPES the tree kill: <c>setsid</c> gives it a session of its own, and it holds the stdout and stderr pipes the readers read to the end of for as long
/// as it lives. Posix only (the shell, <c>setsid</c>); every launch is by absolute path.
/// <para>
/// What this does NOT prove: Windows (a job object is how an escaped descendant would be held there); that the abandoned readers end once the descendant dies — they are
/// left to, and their faults are observed by a continuation, which no test here can see being skipped.
/// </para></summary>
public sealed class ProcessRunnerDrainTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string Shell = "/bin/sh";

    /// <summary>The descendant lives this long — well past the bound plus the margin — so a launch that waited for it cannot pass by accident.</summary>
    private static readonly TimeSpan Escaped = TimeSpan.FromSeconds(40);

    private static readonly TimeSpan Margin = TimeSpan.FromSeconds(8);

    /// <summary>The shell starts <c>sleep</c> in a session of its own, from a subshell that exits at once (the sleep's pid written to <c>$1</c>), and then waits itself:
    /// the sleep is reparented to init, so the tree kill — which walks parent to child — never reaches it. (Started from the shell directly it would remain the shell's
    /// child and die with it.)</summary>
    private static string[] Argv(string pidFile) =>
        ["-c", $"(setsid sh -c 'echo $$ > \"$1\"; exec sleep {(int)Escaped.TotalSeconds}' sh \"$1\" &); sleep 60", "sh", pidFile];

    private static void Posix() => Assert.SkipWhen(OperatingSystem.IsWindows() || !File.Exists("/usr/bin/setsid"), "an escaped descendant needs a POSIX shell and setsid");

    private static async Task<int> EscapedPidAsync(string pidFile)
    {
        for (var i = 0; i < 100 && !(File.Exists(pidFile) && new FileInfo(pidFile).Length > 0); i++)
        {
            await Task.Delay(50, Ct);
        }

        return int.Parse((await File.ReadAllTextAsync(pidFile, Ct)).Trim());
    }

    private static void Reap(int pid)
    {
        try
        {
            using var escaped = Process.GetProcessById(pid);
            escaped.Kill();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // already gone
        }
    }

    [Fact]
    public async Task A_cancelled_launch_waits_for_an_escaped_descendant_no_longer_than_the_bound()
    {
        Posix();
        using var temp = new TempFolder();
        var pidFile = temp.Combine("escaped.pid");
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var clock = Stopwatch.StartNew();
        var launch = ProcessRunner.RunAsync(Shell, Argv(pidFile), temp.Path, TimeSpan.FromMinutes(5), caller.Token);
        var pid = await EscapedPidAsync(pidFile);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1), Ct);
            await caller.CancelAsync();
            var thrown = await Record.ExceptionAsync(() => launch);

            thrown.Should().BeAssignableTo<OperationCanceledException>("the caller's cancellation is what surfaces, drained or not");
            clock.Elapsed.Should().BeLessThan(ProcessRunner.DrainAfterKill + Margin, $"the cancelled launch waits for a descendant that escaped the kill only up to the bound — it lived {Escaped.TotalSeconds:0} s");
        }
        finally
        {
            Reap(pid);
        }
    }

    [Fact]
    public async Task A_timed_out_launch_waits_for_an_escaped_descendant_no_longer_than_the_bound()
    {
        Posix();
        using var temp = new TempFolder();
        var pidFile = temp.Combine("escaped.pid");
        var clock = Stopwatch.StartNew();
        var launch = ProcessRunner.RunAsync(Shell, Argv(pidFile), temp.Path, TimeSpan.FromSeconds(2), Ct);
        var pid = await EscapedPidAsync(pidFile);

        try
        {
            var attempt = await launch;

            attempt.Should().BeOfType<ProcessAttempt.TimedOut>("the wall passed: the answer is the timeout, drained or not");
            clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2) + ProcessRunner.DrainAfterKill + Margin, $"the timed-out launch waits for a descendant that escaped the kill only up to the bound — it lived {Escaped.TotalSeconds:0} s");
        }
        finally
        {
            Reap(pid);
        }
    }
}
