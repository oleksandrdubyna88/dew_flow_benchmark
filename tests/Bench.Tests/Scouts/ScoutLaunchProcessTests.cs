using System.Diagnostics;
using System.Text.Json.Nodes;
using Bench.Application;
using Bench.Application.Scouts;
using Bench.Domain.Probes;
using Bench.Domain.Registry;
using Bench.Infrastructure.Models;
using Bench.Infrastructure.Process;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>S2 — the launcher widened with a line-by-line OUTPUT SINK (plan §3.10, "money survives a crash"), and the fake CLI's test-owned
/// home with its per-prompt replay. Every launch here goes to <c>tests/FakeCli</c>; nothing paid is called.</summary>
public sealed class ScoutLaunchProcessTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Stream(int lines) =>
        string.Concat(Enumerable.Range(1, lines).Select(i => $"{{\"type\":\"line\",\"n\":{i}}}\n"));

    [Fact]
    public async Task Each_stdout_line_reaches_the_sink_while_the_child_is_still_running()
    {
        using var fake = new ScoutFake(Home());
        fake.Answer("prompt", Stream(4));
        fake.Script(new JsonObject { ["lineDelayMs"] = 300 });
        var seen = new List<(string Line, bool ChildAlive)>();

        var transcript = await Transcript(fake, "prompt", TimeSpan.FromMinutes(1), AgentOutputSink.Lines(line => seen.Add((line, ChildAlive(fake)))));

        seen.Select(s => s.Line).Should().Equal(Stream(4).TrimEnd('\n').Split('\n'));
        seen.Take(3).Should().OnlyContain(s => s.ChildAlive, "a line reaches the sink as it arrives — not at exit, when a crash would have lost it");
        transcript.Stdout.Should().Be(Stream(4).TrimEnd('\n'), "the whole stdout is still handed back");
    }

    [Fact]
    public async Task At_the_wall_the_child_tree_is_killed_and_the_lines_that_arrived_are_kept()
    {
        using var fake = new ScoutFake(Home());
        fake.Answer("prompt", Stream(5));
        fake.Script(new JsonObject { ["holdAfterLine"] = 2 });
        var seen = new List<string>();

        var transcript = await Transcript(fake, "prompt", TimeSpan.FromSeconds(20), AgentOutputSink.Lines(seen.Add));

        transcript.TimedOut.Should().BeTrue();
        seen.Should().HaveCount(2, "the two lines printed before the hang reached the sink");
        await GoneAsync(fake.Calls().Single().Pid);
    }

    [Fact]
    public async Task A_sink_that_throws_kills_the_child_and_its_exception_reaches_the_caller()
    {
        using var fake = new ScoutFake(Home());
        fake.Answer("prompt", Stream(50));
        fake.Script(new JsonObject { ["lineDelayMs"] = 200 });

        var launch = () => Transcript(fake, "prompt", TimeSpan.FromMinutes(1), AgentOutputSink.Lines(_ => throw new InvalidOperationException("the artefact root refused a line")));

        await launch.Should().ThrowAsync<InvalidOperationException>().WithMessage("*refused a line*");
        await GoneAsync(fake.Calls().Single().Pid);
    }

    /// <summary>§5 S2: two setups in parallel, both launching <c>--model opus</c> with the SAME prompt, each receive their own stream and
    /// their own call count — the fake's home is the test's, not the model's.</summary>
    [Fact]
    public async Task Two_fakes_on_the_same_model_and_prompt_each_replay_their_own_stream_and_count_their_own_calls()
    {
        using var first = new ScoutFake(Home());
        using var second = new ScoutFake(Home());
        first.Answer("the same prompt", "{\"type\":\"first\"}\n");
        second.Answer("the same prompt", "{\"type\":\"second\"}\n");

        var both = await Task.WhenAll(
            Transcript(first, "the same prompt", TimeSpan.FromMinutes(1), AgentOutputSink.None),
            Transcript(second, "the same prompt", TimeSpan.FromMinutes(1), AgentOutputSink.None),
            Transcript(first, "the same prompt", TimeSpan.FromMinutes(1), AgentOutputSink.None));

        both.Select(t => t.Stdout).Should().Equal("{\"type\":\"first\"}", "{\"type\":\"second\"}", "{\"type\":\"first\"}");
        first.Calls().Should().HaveCount(2);
        second.Calls().Should().HaveCount(1);
        first.Calls().Should().OnlyContain(c => c.Argv.SequenceEqual(new[] { "-p", "--model", "opus" }.Concat(c.Argv.Skip(3))) && c.Argv.Contains("opus"));
    }

    [Fact]
    public async Task The_replay_is_chosen_by_the_prompt_hash_and_names_the_tree_it_was_launched_in()
    {
        using var fake = new ScoutFake(Home());
        fake.Answer("prompt — one", "{\"cwd\":\"__CWD__\",\"n\":1}\n");
        fake.Answer("prompt — two", "{\"cwd\":\"__CWD__\",\"n\":2}\n");

        var two = await Transcript(fake, "prompt — two", TimeSpan.FromMinutes(1), AgentOutputSink.Lines(_ => { }));

        var line = JsonNode.Parse(two.Stdout)!;
        line["n"]!.GetValue<int>().Should().Be(2, "the second prompt's own stream, the non-ASCII dash included in the hash");
        line["cwd"]!.GetValue<string>().Should().Be(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), "__CWD__ becomes the launch's working directory");
    }

    private static async Task<AgentTranscript> Transcript(ScoutFake fake, string prompt, TimeSpan wall, AgentOutputSink sink)
    {
        var parent = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>().ToDictionary(e => (string)e.Key, e => e.Value as string ?? string.Empty, StringComparer.Ordinal);
        var child = ProbeChildEnvironment.Of(parent).Variables.Concat(fake.ChildVariables).ToDictionary(v => v.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);
        var ask = new AgentAsk(ModelRuntimeKind.CliClaude, ScoutFake.Executable, prompt, Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), wall, ScoutLaunch.MainModel)
        {
            Environment = AgentEnvironment.Only(child),
            Output = sink,
        };

        return (await new CliAgentRuntime(NullLogger<CliAgentRuntime>.Instance).TranscriptAsync(ask, Ct)).Ok();
    }

    private static bool ChildAlive(ScoutFake fake) =>
        fake.Calls().LastOrDefault() is { } call && WorkerLiveness.ProcessIsAlive(call.Pid);

    private static async Task GoneAsync(int pid)
    {
        for (var i = 0; i < 50 && WorkerLiveness.ProcessIsAlive(pid); i++)
        {
            await Task.Delay(100, Ct);
        }

        WorkerLiveness.ProcessIsAlive(pid).Should().BeFalse($"pid {pid} must not outlive the launch — a hung CLI must not keep spending");
    }

    private static string Home() => Path.Combine(Path.GetTempPath(), "bench-scout-fake", Guid.NewGuid().ToString("N"));
}
