using Bench.Application;
using Bench.Domain.Registry;
using Bench.Infrastructure.Models;
using Bench.Infrastructure.Process;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bench.Tests.Models;

/// <summary>The CLI agent adapter: which argv puts each one in headless mode, and how each way of failing
/// reads.
/// <para>
/// Split into two pure halves and one live test on purpose. The argv mapping and the attempt-to-answer
/// reading are the whole logic, and asserting them needs no process; whether <c>claude -p</c> actually answers
/// on stdin is a fact about that CLI, and only a live run can say — the lesson of 2026-08-17, where every
/// stubbed test agreed with an assumption the real daemon rejected.
/// </para></summary>
public sealed class CliAgentRuntimeTests
{
    private static readonly AgentAsk Ask = new(
        ModelRuntimeKind.CliClaude, "claude", "write one question", ".", TimeSpan.FromMinutes(2));

    [Fact]
    public void Claudes_headless_flag_is_the_one_that_answers_once_and_exits() =>
        CliArgv.For(ModelRuntimeKind.CliClaude).Ok().Should().Equal(["-p"]);

    [Theory]
    [InlineData(ModelRuntimeKind.CliCodex)]
    [InlineData(ModelRuntimeKind.CliGemini)]
    public void Every_CLI_kind_has_an_argv(ModelRuntimeKind runtime) =>
        CliArgv.For(runtime).Ok().Should().NotBeEmpty(
            "a kind with no argv would launch an interactive session that waits on a terminal nobody watches, "
            + "and the symptom would be a timeout rather than a message");

    [Theory]
    [InlineData(ModelRuntimeKind.OpenAiEndpoint)]
    [InlineData(ModelRuntimeKind.BridgeLocal)]
    public void A_runtime_that_is_not_a_CLI_is_refused_rather_than_launched(ModelRuntimeKind runtime) =>
        CliArgv.For(runtime).Reason().Should().Contain("not a CLI agent");

    [Fact]
    public async Task An_empty_prompt_never_costs_a_launch()
    {
        var refused = await Runtime().AskAsync(Ask with { Prompt = "   " }, TestContext.Current.CancellationToken);

        refused.Reason().Should().Contain("empty prompt");
    }

    [Fact]
    public async Task An_executable_that_is_not_installed_is_a_configuration_fact_not_an_agents_failure()
    {
        var refused = await Runtime().AskAsync(
            Ask with { Executable = "definitely-not-an-agent-7b21" }, TestContext.Current.CancellationToken);

        // The registry stores a REFERENCE and resolves it on this machine; a reference pointing at nothing is
        // the operator's to fix, and reading it as "the agent failed" would send them hunting the wrong thing.
        refused.Reason().Should().Contain("not installed on this machine").And.Contain("reference resolved");
    }

    [Fact]
    public void An_agent_that_exits_ZERO_and_prints_nothing_is_a_refusal()
    {
        var read = CliAgentRuntime.Read(
            new ProcessAttempt.Completed(new ProcessResult(0, "   \n ")), Ask, TimeSpan.FromSeconds(3));

        // The failure mode that looks like success. An empty answer stored as a candidate would be a question
        // nobody wrote, and it would pass every admission rule that checks SHAPE rather than content.
        read.Reason().Should().Contain("exited 0 and printed nothing");
    }

    [Fact]
    public void A_non_zero_exit_carries_what_the_agent_said_about_it()
    {
        var read = CliAgentRuntime.Read(
            new ProcessAttempt.Completed(new ProcessResult(1, "Invalid API key · Please run /login")),
            Ask,
            TimeSpan.FromSeconds(1));

        read.Reason().Should().Contain("exited 1").And.Contain("Invalid API key");
    }

    [Fact]
    public void A_timeout_says_the_ceiling_AND_whatever_had_been_printed_before_it()
    {
        var read = CliAgentRuntime.Read(
            new ProcessAttempt.TimedOut(TimeSpan.FromSeconds(90), "thinking about the retry helper"),
            Ask,
            TimeSpan.FromSeconds(90));

        // "What did it print before it hung" is the only diagnosis available for a hang, and the launcher
        // already keeps it — dropping it here would waste the one thing it preserved.
        read.Reason().Should().Contain("90s").And.Contain("thinking about the retry helper");
    }

    [Fact]
    public void A_timeout_that_printed_nothing_says_that_too()
    {
        CliAgentRuntime.Read(
            new ProcessAttempt.TimedOut(TimeSpan.FromSeconds(5), string.Empty), Ask, TimeSpan.FromSeconds(5))
            .Reason().Should().Contain("printed nothing");
    }

    [Fact]
    public void An_answer_carries_its_size_and_the_time_it_took()
    {
        var read = CliAgentRuntime.Read(
            new ProcessAttempt.Completed(new ProcessResult(0, "  {\"id\":\"q1\"}  ")),
            Ask,
            TimeSpan.FromSeconds(12)).Ok();

        read.Text.Should().Be("{\"id\":\"q1\"}", "trimmed, because a CLI's trailing newline is not part of an answer");
        read.Elapsed.Should().Be(TimeSpan.FromSeconds(12));
        read.ResponseBytes.Should().BeGreaterThan(0, "the only honest per-call measure of what a batch cost");
    }

    private static CliAgentRuntime Runtime() => new(NullLogger<CliAgentRuntime>.Instance);
}
