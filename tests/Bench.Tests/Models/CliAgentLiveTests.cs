using Bench.Application;
using Bench.Domain.Registry;
using Bench.Infrastructure.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bench.Tests.Models;

/// <summary>The Claude CLI, actually launched.
/// <para>
/// <b>The only test that can tell whether <c>claude -p</c> answers on stdin at all.</b> Every stubbed test
/// beside this one asserts what this adapter DOES; none of them can assert that the CLI agrees. That
/// distinction cost a day on 2026-08-17: a whole retrieval lane's tests were green while the live daemon
/// answered 400 to the very request they asserted, because a stub agrees with whatever the caller assumed.
/// </para>
/// <para>
/// Excluded from the default run by its trait, and SKIPPED rather than failed when the executable is not
/// configured — a suite that goes red because somebody has no CLI installed teaches people to ignore red. Run
/// it deliberately:
/// <code>
/// $env:BENCH_CLAUDE_EXE="C:\Users\you\.local\bin\claude.exe"
/// ./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests --filter-trait "Category=Live"
/// </code>
/// </para></summary>
[Trait("Category", "Live")]
public sealed class CliAgentLiveTests
{
    private const string ExecutableVariable = "BENCH_CLAUDE_EXE";

    /// <summary>Generous, and not a guess: an agent's first call pays for process start, authentication and a
    /// model round trip. A ceiling tight enough to be convenient is a ceiling that reports a working agent as
    /// a hang.</summary>
    private static readonly TimeSpan Wall = TimeSpan.FromMinutes(3);

    [Fact]
    public async Task The_agent_answers_a_prompt_it_was_given_on_STDIN()
    {
        var ask = Ask("Reply with exactly the word SEVENTEEN and nothing else.");

        var answer = await Runtime().AskAsync(ask, TestContext.Current.CancellationToken);

        // A known token, so the assertion is about the ANSWER arriving rather than about anything being
        // printed: a CLI that ignored its stdin would still produce output, and a test asserting "not empty"
        // would pass while measuring nothing.
        answer.Failed().Should().BeFalse(answer.Match(_ => string.Empty, reason => reason));
        answer.Ok().Text.Should().Contain("SEVENTEEN");
        answer.Ok().ResponseBytes.Should().BeGreaterThan(0);
        answer.Ok().Elapsed.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task A_prompt_far_larger_than_an_argument_list_still_reaches_it()
    {
        // The reason stdin exists in this path. An authoring prompt carries a task description, a group's
        // conventions and often a file's worth of context; argv caps out around 32 KB on Windows, so this
        // would have failed on the machine with the biggest target repository.
        var padding = string.Join('\n', Enumerable.Range(0, 2_000).Select(i => $"// filler line {i}"));
        var ask = Ask($"{padding}\n\nIgnore the filler above. Reply with exactly the word EIGHTEEN.");

        var answer = await Runtime().AskAsync(ask, TestContext.Current.CancellationToken);

        answer.Failed().Should().BeFalse(answer.Match(_ => string.Empty, reason => reason));
        answer.Ok().Text.Should().Contain("EIGHTEEN");
    }

    [Fact]
    public async Task An_agent_that_outlives_its_wall_is_a_recorded_refusal_rather_than_a_hang()
    {
        // One second against a call that pays for process start alone: the ceiling fires, the child is killed,
        // and the run continues. An authoring batch over six groups cannot end because one agent stalled.
        var ask = Ask("Think carefully and then reply with a very long essay about retry policies.")
            with { Wall = TimeSpan.FromSeconds(1) };

        var refused = await Runtime().AskAsync(ask, TestContext.Current.CancellationToken);

        refused.Failed().Should().BeTrue("a wall that does not fire is a wall that does not exist");
        refused.Reason().Should().Contain("did not answer within");
    }

    private static AgentAsk Ask(string prompt) =>
        new(ModelRuntimeKind.CliClaude, Executable(), prompt, Path.GetTempPath(), Wall);

    /// <summary>The executable, or a skip that says exactly what is missing.</summary>
    private static string Executable()
    {
        var path = Environment.GetEnvironmentVariable(ExecutableVariable) ?? string.Empty;

        Assert.SkipWhen(
            path.Length == 0,
            $"set {ExecutableVariable} to the Claude CLI to run this against a real agent");

        return path;
    }

    private static CliAgentRuntime Runtime() => new(NullLogger<CliAgentRuntime>.Instance);
}
