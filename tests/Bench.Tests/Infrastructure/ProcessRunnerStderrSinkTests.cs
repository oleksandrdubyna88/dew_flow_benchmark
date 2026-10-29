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
