using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Infrastructure.Models;
using Bench.Infrastructure.Process;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The CLI-subject readings, pure — the four ways a launch ends, asserted without a process,
/// exactly as <c>CliAgentRuntime.Read</c> is tested one port over.</summary>
public sealed class CliSubjectRuntimeTests
{
    private const string Envelope =
        """
        {"is_error":false,"result":"the delay is recomputed","total_cost_usd":0.05,
         "usage":{"input_tokens":10,"output_tokens":5,"cache_read_input_tokens":90,"cache_creation_input_tokens":0}}
        """;

    [Fact]
    public void A_completed_launch_reads_as_an_answer_with_billed_tokens_and_uncaptured_sampling()
    {
        var answer = CliSubjectRuntime.Read(
            new ProcessAttempt.Completed(new ProcessResult(0, "banner\n" + Envelope, Envelope)),
            TimeSpan.FromSeconds(3)).Ok();

        answer.Text.Value.Should().Be("the delay is recomputed");
        answer.PromptTokens.Value.Should().Be(100);
        answer.CompletionTokens.Value.Should().Be(5);
        answer.Sampling.Captured.Should().BeFalse(
            "a CLI exposes no sampling controls, and claiming the requested values were applied is the unpinned-sampler lie");
        answer.StopDetail.Should().Contain("0.05", "the CLI's own cost rides the record until the model plumbing carries it");
    }

    [Fact]
    public void A_nonzero_exit_is_a_refusal_with_the_tail()
    {
        CliSubjectRuntime.Read(
                new ProcessAttempt.Completed(new ProcessResult(2, "banner\nthe real reason")),
                TimeSpan.Zero)
            .Reason().Should().Contain("exited 2").And.Contain("the real reason");
    }

    [Fact]
    public void A_missing_executable_is_a_configuration_fact()
    {
        CliSubjectRuntime.Read(new ProcessAttempt.NotFound("claude"), TimeSpan.Zero)
            .Reason().Should().Contain("not installed");
    }

    [Fact]
    public async Task A_turn_ceiling_is_refused_because_nobody_can_enforce_it()
    {
        var runtime = new CliSubjectRuntime(
            new CliSubjectOptions("claude", "."),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CliSubjectRuntime>.Instance);

        (await runtime.AcceptBudgetAsync(Budget.Of(BudgetKind.Turns, BudgetScope.Question, 25), CancellationToken.None))
            .Reason().Should().Contain("turn-capped");
        (await runtime.AcceptBudgetAsync(Budget.Of(BudgetKind.Wall, BudgetScope.Question, 600), CancellationToken.None))
            .Failed().Should().BeFalse("the wall is the one ceiling a process launch can enforce");
    }
}
