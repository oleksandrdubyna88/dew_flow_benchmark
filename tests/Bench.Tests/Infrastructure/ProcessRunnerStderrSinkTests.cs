using System.Diagnostics;
using System.Text.Json.Nodes;
using Bench.Application;
using Bench.Domain.Registry;
using Bench.Infrastructure.Models;
using Bench.Infrastructure.Process;
using Bench.Tests.Probes.Driver;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>Model arms E1.S5 — the launcher's STDERR line sink beside the stdout one: a CLI that warns while it streams leaves its warnings on
/// disk as they are printed. Every launch goes to <c>tests/FakeCli</c> through the model-keyed handle; nothing paid is called.
/// <c>ChildAlive</c> and <c>GoneAsync</c> exist here and in <c>ScoutLaunchProcessTests</c> (pinned unedited by this story); a later cleanup may
/// extract both.</summary>
public sealed class ProcessRunnerStderrSinkTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Cwd => Path.GetTempPath();

    private static string Stream(int lines) =>
        string.Concat(Enumerable.Range(1, lines).Select(i => $"{{\"type\":\"line\",\"n\":{i}}}\n"));

    private static string[] Warns(int n) => [.. Enumerable.Range(1, n).Select(i => $"warn {i}")];

    private static string[] Argv(FakeCli fake) => ["-p", "--model", fake.Model];

    /// <summary>The fake replays <c>Stream(lines)</c> on stdout and writes <c>Warns(lines)</c> to stderr, the n-th just before the n-th stdout line.</summary>
    private static void Replay(FakeCli fake, int lines, JsonObject extra)
    {
        var stream = Path.Combine(fake.Home, "stream.ndjson");
        File.WriteAllText(stream, Stream(lines));
        extra["stdoutFile"] = stream;
        extra["stderrBeforeLines"] = new JsonArray(Warns(lines).Select(w => (JsonNode)w).ToArray());
        fake.Rewrite(extra);
    }

    [Fact]
    public async Task Stderr_lines_reach_the_sink_as_they_arrive_and_the_whole_text_is_still_returned()
    {
        using var fake = new FakeCli();
        Replay(fake, 4, new JsonObject { ["lineDelayMs"] = 300 });
        var seen = new List<(string Line, bool ChildAlive)>();
        var stdoutSeen = new List<string>();

        var attempt = await ProcessRunner.RunAsync(
            FakeCli.Executable, Argv(fake), Cwd, TimeSpan.FromMinutes(1), "prompt",
            environment: null, stdoutLine: stdoutSeen.Add, stderrLine: line => seen.Add((line, ChildAlive(fake))), Ct);

        seen.Select(s => s.Line).Should().Equal(Warns(4));
        seen.Take(3).Should().OnlyContain(s => s.ChildAlive, "a stderr line reaches the sink as it arrives — not at exit");
        var result = attempt.Should().BeOfType<ProcessAttempt.Completed>().Subject.Result;
        result.ExitCode.Should().Be(0);
        result.StandardError.Should().Be("warn 1\nwarn 2\nwarn 3\nwarn 4");
        result.StandardOutput.Should().Be(Stream(4).TrimEnd('\n'));
        result.Output.Should().Contain("warn 4").And.Contain("\"n\":4");
        stdoutSeen.Should().Equal(Stream(4).TrimEnd('\n').Split('\n'), "both pumps ran side by side");
    }

    [Fact]
    public async Task A_stderr_sink_that_throws_kills_the_tree_like_a_stdout_sink_does()
    {
        using var fake = new FakeCli();
        Replay(fake, 50, new JsonObject { ["lineDelayMs"] = 200, ["holdAfterLine"] = 10 });
        var clock = Stopwatch.StartNew();

        var launch = () => ProcessRunner.RunAsync(
            FakeCli.Executable, Argv(fake), Cwd, TimeSpan.FromSeconds(60), "prompt",
            environment: null, stdoutLine: null, stderrLine: _ => throw new InvalidOperationException("the .err file refused a line"), Ct);

        await launch.Should().ThrowAsync<InvalidOperationException>().WithMessage("*refused a line*");
        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30), "the sink's throw killed the tree — the 60 s wall did not");
        await GoneAsync(fake.Calls().Single().Pid);
    }

    /// <summary>Every earlier overload delegates with no stderr sink and returns what it always returned: the whole stderr in <c>StandardError</c>,
    /// merged into <c>Output</c>, and none of it in the stdout sink.
    /// <para>
    /// The encoding half of E1.S5's criterion 4 (UTF-8 only when a sink is given) is not observable here: on Linux the default encoding is already
    /// UTF-8, so only a Windows run could see it go wrong.
    /// </para></summary>
    [Fact]
    public async Task Every_existing_launch_without_a_stderr_sink_is_unchanged()
    {
        using var fake = new FakeCli();
        Replay(fake, 4, []);
        var stdoutSeen = new List<string>();

        var attempts = new[]
        {
            await ProcessRunner.RunAsync(FakeCli.Executable, Argv(fake), Cwd, TimeSpan.FromMinutes(1), "prompt", Ct),
            await ProcessRunner.RunAsync(FakeCli.Executable, Argv(fake), Cwd, TimeSpan.FromMinutes(1), "prompt", environment: null, Ct),
            await ProcessRunner.RunAsync(FakeCli.Executable, Argv(fake), Cwd, TimeSpan.FromMinutes(1), "prompt", null, stdoutLine: stdoutSeen.Add, Ct),
        };

        foreach (var attempt in attempts)
        {
            var result = attempt.Should().BeOfType<ProcessAttempt.Completed>().Subject.Result;
            result.ExitCode.Should().Be(0);
            result.StandardOutput.Should().Be(Stream(4).TrimEnd('\n'));
            result.StandardError.Should().Be(string.Join('\n', Warns(4)), "the whole stderr still comes back with no sink");
            result.Output.Should().Contain("warn 4").And.Contain("\"n\":4");
        }

        stdoutSeen.Should().Equal(Stream(4).TrimEnd('\n').Split('\n'), "no stderr line strayed into the stdout sink");
        fake.Calls().Should().HaveCount(3);
    }

    [Fact]
    public async Task The_runtime_hands_an_asks_ErrorOutput_to_the_launcher_as_its_stderr_sink()
    {
        using var fake = new FakeCli();
        Replay(fake, 4, new JsonObject { ["lineDelayMs"] = 300 });
        var seen = new List<(string Line, bool ChildAlive)>();
        var ask = new AgentAsk(ModelRuntimeKind.CliClaude, FakeCli.Executable, "prompt", Cwd, TimeSpan.FromMinutes(1), fake.Model)
        {
            ErrorOutput = AgentOutputSink.Lines(line => seen.Add((line, ChildAlive(fake)))),
        };

        var transcript = (await new CliAgentRuntime(NullLogger<CliAgentRuntime>.Instance).TranscriptAsync(ask, Ct)).Ok();

        seen.Select(s => s.Line).Should().Equal(Warns(4));
        seen.Take(3).Should().OnlyContain(s => s.ChildAlive);
        transcript.TimedOut.Should().BeFalse();
        transcript.Stderr.Should().Be(string.Join('\n', Warns(4)));
        transcript.Stdout.Should().Be(Stream(4).TrimEnd('\n'));
        transcript.Argv.Should().Equal(Argv(fake));
    }

    /// <summary>The two reader tasks start together and nothing the launch returns or throws may leave one running (model arms E2.S3, E1 §8 item 15).
    /// A stdout sink that throws kills the child; before the fix the launch then rethrew while the stderr pump's sink was still inside its callback.</summary>
    [Fact]
    public async Task A_launch_never_returns_while_a_sink_is_still_running()
    {
        using var fake = new FakeCli();
        Replay(fake, 20, new JsonObject { ["lineDelayMs"] = 50, ["holdAfterLine"] = 10 });
        var refusal = $"stdout refused {Guid.NewGuid():N}";
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var calls = 0;
        var clock = Stopwatch.StartNew();
        var leftAt = TimeSpan.Zero;

        // The moment is taken INSIDE the launch's own task: the instant the launch left, not the instant the test got round to awaiting it.
        var launch = Task.Run(async () =>
        {
            try
            {
                return await ProcessRunner.RunAsync(
                    FakeCli.Executable, Argv(fake), Cwd, TimeSpan.FromMinutes(1), "prompt",
                    environment: null,
                    stdoutLine: _ => throw new InvalidOperationException(refusal),
                    stderrLine: _ =>
                    {
                        if (Interlocked.Increment(ref calls) == 1)
                        {
                            entered.Set();
                            release.Wait(TimeSpan.FromSeconds(20));
                        }
                    },
                    Ct);
            }
            finally
            {
                leftAt = clock.Elapsed;
            }
        }, Ct);

        entered.Wait(TimeSpan.FromSeconds(10), Ct).Should().BeTrue("the stderr pump reached its sink");
        await Task.Delay(TimeSpan.FromSeconds(1), Ct);
        var releasedAt = clock.Elapsed;
        release.Set();

        var thrown = await Record.ExceptionAsync(() => launch);

        thrown.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be(refusal);
        leftAt.Should().BeGreaterThanOrEqualTo(releasedAt, "a launch never returns while a sink it started is still running");
        await GoneAsync(fake.Calls().Single().Pid);
    }

    /// <summary>The caller's cancellation is still the caller's own <see cref="OperationCanceledException"/> — and it surfaces only after both readers
    /// ended (model arms E2.S3): a sink still inside its callback when the caller got its cancellation back would write after the caller had moved on.</summary>
    [Fact]
    public async Task A_cancelled_launch_ends_both_readers_before_it_throws_the_callers_cancellation()
    {
        using var fake = new FakeCli();
        Replay(fake, 20, new JsonObject { ["lineDelayMs"] = 50, ["holdAfterLine"] = 10 });
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var calls = 0;
        var clock = Stopwatch.StartNew();
        var leftAt = TimeSpan.Zero;

        var launch = Task.Run(async () =>
        {
            try
            {
                return await ProcessRunner.RunAsync(
                    FakeCli.Executable, Argv(fake), Cwd, TimeSpan.FromMinutes(1), "prompt",
                    environment: null,
                    stdoutLine: _ => { },
                    stderrLine: _ =>
                    {
                        if (Interlocked.Increment(ref calls) == 1)
                        {
                            entered.Set();
                            release.Wait(TimeSpan.FromSeconds(20));
                        }
                    },
                    caller.Token);
            }
            finally
            {
                leftAt = clock.Elapsed;
            }
        }, Ct);

        entered.Wait(TimeSpan.FromSeconds(10), Ct).Should().BeTrue("the stderr pump reached its sink");
        await caller.CancelAsync();
        await Task.Delay(TimeSpan.FromSeconds(1), Ct);
        var releasedAt = clock.Elapsed;
        release.Set();

        var thrown = await Record.ExceptionAsync(() => launch);

        thrown.Should().BeAssignableTo<OperationCanceledException>("the caller's cancellation surfaces as a cancellation, not as anything a sink did");
        leftAt.Should().BeGreaterThanOrEqualTo(releasedAt, "a cancelled launch never returns while a sink it started is still running");
        await GoneAsync(fake.Calls().Single().Pid);
    }

    /// <summary>The caller's cancellation outranks a reader's fault: a stderr sink that cancels the caller and THEN throws leaves the launch with the caller's own
    /// cancellation — the token the caller handed in, not the linked one the wait used — and not with the sink's <see cref="IOException"/>.</summary>
    [Fact]
    public async Task A_cancellation_the_caller_asked_for_outranks_the_sinks_fault()
    {
        using var fake = new FakeCli();
        Replay(fake, 20, new JsonObject { ["lineDelayMs"] = 50, ["holdAfterLine"] = 10 });
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        var launch = () => ProcessRunner.RunAsync(
            FakeCli.Executable, Argv(fake), Cwd, TimeSpan.FromMinutes(1), "prompt",
            environment: null,
            stdoutLine: null,
            stderrLine: _ =>
            {
                caller.Cancel();
                throw new IOException("disk full");
            },
            caller.Token);

        var thrown = await Record.ExceptionAsync(launch);

        thrown.Should().BeAssignableTo<OperationCanceledException>("the caller asked to stop, so its cancellation is the answer, not the sink's fault")
            .Which.CancellationToken.Should().Be(caller.Token, "it is the caller's own token");
        await GoneAsync(fake.Calls().Single().Pid);
    }

    /// <summary>One form on every path: the plain cancellation (no sink, no fault) is also a <see cref="OperationCanceledException"/> carrying the caller's own token,
    /// with the wait's own cancellation kept as its inner exception.</summary>
    [Fact]
    public async Task A_plainly_cancelled_launch_throws_the_callers_own_token()
    {
        using var fake = new FakeCli();
        Replay(fake, 20, new JsonObject { ["lineDelayMs"] = 50, ["holdAfterLine"] = 10 });
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var launch = ProcessRunner.RunAsync(FakeCli.Executable, Argv(fake), Cwd, TimeSpan.FromMinutes(1), "prompt", caller.Token);

        for (var i = 0; i < 100 && fake.Calls().Count == 0; i++)
        {
            await Task.Delay(50, Ct);
        }

        await caller.CancelAsync();
        var thrown = await Record.ExceptionAsync(() => launch);

        thrown.Should().BeAssignableTo<OperationCanceledException>().Which.CancellationToken.Should().Be(caller.Token, "the caller gets its own token back on the plain path too");
        thrown!.InnerException.Should().BeAssignableTo<OperationCanceledException>("the wait's own cancellation stays as the inner exception");
        await GoneAsync(fake.Calls().Single().Pid);
    }

    /// <summary>Both readers fault; the one the caller sees is the stdout reader's (task order, never "whichever came first") and the other is OBSERVED
    /// before the launch ends — an unobserved faulted task is raised by the finalizer, long after the test that caused it (common.reliability).</summary>
    [Fact]
    public async Task When_both_sinks_throw_the_stdout_readers_fault_surfaces_and_the_other_is_observed()
    {
        using var fake = new FakeCli();
        Replay(fake, 20, new JsonObject { ["lineDelayMs"] = 50, ["holdAfterLine"] = 10 });
        var id = Guid.NewGuid().ToString("N");
        var unobserved = new List<string>();
        void Record(object? _, UnobservedTaskExceptionEventArgs e)
        {
            lock (unobserved)
            {
                unobserved.AddRange(e.Exception.Flatten().InnerExceptions
                    .Where(x => x.Message.Contains(id, StringComparison.Ordinal) || (x is ObjectDisposedException && x.StackTrace?.Contains("PumpAsync", StringComparison.Ordinal) == true))
                    .Select(x => $"{x.GetType().FullName}: {x.Message}"));
            }
        }

        TaskScheduler.UnobservedTaskException += Record;
        try
        {
            var launch = () => ProcessRunner.RunAsync(
                FakeCli.Executable, Argv(fake), Cwd, TimeSpan.FromMinutes(1), "prompt",
                environment: null,
                stdoutLine: _ => throw new InvalidOperationException($"stdout refused {id}"),
                stderrLine: _ => throw new InvalidOperationException($"stderr refused {id}"),
                Ct);

            await launch.Should().ThrowAsync<InvalidOperationException>().WithMessage($"stdout refused {id}");

            // A pump the launch left behind finishes (and faults) only once the killed child's pipes close; give it that time, then let the finalizers speak.
            await GoneAsync(fake.Calls().Single().Pid);
            await Task.Delay(TimeSpan.FromMilliseconds(500), Ct);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= Record;
        }

        lock (unobserved)
        {
            unobserved.Should().BeEmpty("a reader's fault is observed before the launch returns");
        }

        await GoneAsync(fake.Calls().Single().Pid);
    }

    private static bool ChildAlive(FakeCli fake) =>
        fake.Calls().LastOrDefault() is { } call && WorkerLiveness.ProcessIsAlive(call.Pid);

    private static async Task GoneAsync(int pid)
    {
        for (var i = 0; i < 50 && WorkerLiveness.ProcessIsAlive(pid); i++)
        {
            await Task.Delay(100, Ct);
        }

        WorkerLiveness.ProcessIsAlive(pid).Should().BeFalse($"pid {pid} must not outlive the launch — a hung CLI must not keep spending");
    }
}
