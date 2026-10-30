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

    /// <summary>The shell prints a line on each pipe, starts a descendant in a session of its own from a subshell that exits at once (the descendant's pid written to <c>$1</c>), and
    /// then waits itself: the descendant is reparented to init, so the tree kill — which walks parent to child — never reaches it. (Started from the shell directly it would remain
    /// the shell's child and die with it.) The descendant runs <paramref name="descendant"/>, which holds the stdout and stderr pipes for as long as it lives.</summary>
    private static string[] Argv(string pidFile, string descendant) =>
        ["-c", $"echo started; echo err-started >&2; (setsid sh -c 'echo $$ > \"$1\"; {descendant}' sh \"$1\" &); sleep 60", "sh", pidFile];

    private static string[] Argv(string pidFile) => Argv(pidFile, $"exec sleep {(int)Escaped.TotalSeconds}");

    private static void Posix() => Assert.SkipWhen(OperatingSystem.IsWindows() || !File.Exists("/usr/bin/setsid"), "an escaped descendant needs a POSIX shell and setsid");

    /// <summary>The escaped descendant's pid, once it has written it; the launch is already running, so a pid that never comes fails the test instead of hanging it.</summary>
    private static async Task<int> EscapedPidAsync(string pidFile)
    {
        for (var i = 0; i < 100 && !(File.Exists(pidFile) && new FileInfo(pidFile).Length > 0); i++)
        {
            await Task.Delay(50, Ct);
        }

        File.Exists(pidFile).Should().BeTrue("the shell started the escaped descendant and it wrote its pid");
        return int.Parse((await File.ReadAllTextAsync(pidFile, Ct)).Trim());
    }

    /// <summary>Kills the escaped descendant, when a pid was read for it; its own sleep ends it otherwise.</summary>
    private static void Reap(int pid)
    {
        if (pid <= 0)
        {
            return;
        }

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
        var pid = 0;

        try
        {
            pid = await EscapedPidAsync(pidFile);
            await Task.Delay(TimeSpan.FromSeconds(1), Ct);
            await caller.CancelAsync();
            var thrown = await Record.ExceptionAsync(() => launch);

            thrown.Should().BeAssignableTo<OperationCanceledException>("the caller's cancellation is what surfaces, drained or not");
            clock.Elapsed.Should().BeLessThan(ProcessRunner.DrainAfterKill + Margin, $"the cancelled launch waits for a descendant that escaped the kill only up to the bound — it lived {Escaped.TotalSeconds:0} s");
        }
        finally
        {
            await caller.CancelAsync();
            Reap(pid);
        }
    }

    [Fact]
    public async Task A_timed_out_launch_waits_for_an_escaped_descendant_no_longer_than_the_bound_and_says_its_output_is_partial()
    {
        Posix();
        using var temp = new TempFolder();
        var pidFile = temp.Combine("escaped.pid");
        var clock = Stopwatch.StartNew();
        var launch = ProcessRunner.RunAsync(Shell, Argv(pidFile), temp.Path, TimeSpan.FromSeconds(2), Ct);
        var pid = 0;

        try
        {
            pid = await EscapedPidAsync(pidFile);
            var attempt = await launch;

            var timedOut = attempt.Should().BeOfType<ProcessAttempt.TimedOut>("the wall passed: the answer is the timeout, drained or not").Subject;
            clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2) + ProcessRunner.DrainAfterKill + Margin, $"the timed-out launch waits for a descendant that escaped the kill only up to the bound — it lived {Escaped.TotalSeconds:0} s");
            timedOut.StandardOutput.Should().Be("started", "what was read before the bound is not lost with the abandoned reader");
            timedOut.StandardError.Should().Be("err-started");
            timedOut.Output.Should().Contain("started").And.Contain("err-started");
            timedOut.ReadersAbandoned.Should().BeTrue("a pipe was still open at the bound, so the text is not known to be whole — an empty text would otherwise read as 'printed nothing'");
            timedOut.Describe.Should().Contain("missing");
        }
        finally
        {
            Reap(pid);
        }
    }

    [Fact]
    public async Task A_timed_out_launch_whose_pipes_closed_is_not_marked_partial()
    {
        Posix();
        using var temp = new TempFolder();

        var attempt = await ProcessRunner.RunAsync(Shell, ["-c", "echo started; echo err-started >&2; sleep 60"], temp.Path, TimeSpan.FromSeconds(1), Ct);

        var timedOut = attempt.Should().BeOfType<ProcessAttempt.TimedOut>().Subject;
        timedOut.StandardOutput.Should().Be("started");
        timedOut.StandardError.Should().Be("err-started");
        timedOut.ReadersAbandoned.Should().BeFalse("the pipes closed with the kill: the text is the whole of what the child printed");
    }

    /// <summary>The abandoned readers' work happens after the launch has returned, so it is watched from the outside: the descendant writes a line at 14 s (the bound is 10 s, the
    /// cancellation comes at 1 s) and the abandoned stdout pump's sink throws on it. Once the descendant has gone and the finalizers have run, that fault must have been OBSERVED (by the
    /// continuation that also disposes the budget — an unobserved faulted task is raised by the finalizer) and the pump's catch must never have cancelled a DISPOSED budget (a first-chance
    /// <see cref="ObjectDisposedException"/> out of <c>PumpAsync</c>). Both hooks are process-wide, so they are filtered to this test's own id and to the pump's own frame.</summary>
    [Fact]
    public async Task An_abandoned_readers_fault_is_observed_and_the_budget_outlives_it()
    {
        Posix();
        using var temp = new TempFolder();
        var id = Guid.NewGuid().ToString("N");
        var pidFile = temp.Combine("escaped.pid");
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        using var lateSeen = new ManualResetEventSlim();
        var seen = new List<string>();

        void Unobserved(object? _, UnobservedTaskExceptionEventArgs e)
        {
            lock (seen)
            {
                seen.AddRange(e.Exception.Flatten().InnerExceptions.Where(x => x.Message.Contains(id, StringComparison.Ordinal)).Select(x => $"unobserved {x.GetType().Name}: {x.Message}"));
            }
        }

        void FirstChance(object? _, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            if (e.Exception is ObjectDisposedException && e.Exception.StackTrace?.Contains("PumpAsync", StringComparison.Ordinal) == true)
            {
                lock (seen)
                {
                    seen.Add($"first-chance {e.Exception.GetType().Name}: {e.Exception.Message}");
                }
            }
        }

        TaskScheduler.UnobservedTaskException += Unobserved;
        AppDomain.CurrentDomain.FirstChanceException += FirstChance;
        var pid = 0;

        try
        {
            var launch = ProcessRunner.RunAsync(
                Shell, Argv(pidFile, "sleep 14; echo late; exec sleep 1"), temp.Path, TimeSpan.FromMinutes(5), string.Empty,
                environment: null,
                stdoutLine: line =>
                {
                    if (line == "late")
                    {
                        lateSeen.Set();
                        throw new IOException($"late refused {id}");
                    }
                },
                stderrLine: null,
                caller.Token);
            pid = await EscapedPidAsync(pidFile);
            await Task.Delay(TimeSpan.FromSeconds(1), Ct);
            await caller.CancelAsync();

            (await Record.ExceptionAsync(() => launch)).Should().BeAssignableTo<OperationCanceledException>("the launch left at the bound, long before the late line");
            lateSeen.Wait(TimeSpan.FromSeconds(30), Ct).Should().BeTrue("the abandoned reader was left running and its sink saw the descendant's late line");

            // The descendant's last second, the readers' ends, the continuation, the finalizers.
            await Task.Delay(TimeSpan.FromSeconds(3), Ct);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= Unobserved;
            AppDomain.CurrentDomain.FirstChanceException -= FirstChance;
            Reap(pid);
        }

        lock (seen)
        {
            seen.Should().BeEmpty("the abandoned reader's fault is observed and its pump never met a disposed budget");
        }
    }
}
