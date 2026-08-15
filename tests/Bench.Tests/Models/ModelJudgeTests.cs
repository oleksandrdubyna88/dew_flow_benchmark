using Bench.Application;
using Bench.Domain;
using Bench.Domain.Models;
using Bench.Domain.Runs;
using Bench.Domain.Trace;
using Bench.Infrastructure.Models;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Models;

/// <summary>Reading an arbiter's answer — where a judged benchmark quietly becomes unreproducible.
/// <para>
/// The parser is the whole risk surface. A model asked for a verdict will sometimes answer in a shape
/// nobody planned for, and the tempting default — treat anything unparseable as a NO — turns an arbiter
/// having a bad day into a subject having a bad day, across every leg, invisibly.
/// </para></summary>
public sealed class ModelJudgeTests
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("YES\nit names the same helper", true)]
    [InlineData("yes\nsame mechanism", true)]
    [InlineData("NO\ndifferent formula", false)]
    [InlineData("No.\nvague", false)]
    public async Task A_verdict_is_read_from_the_first_word_case_and_punctuation_aside(string reply, bool expected)
    {
        var verdict = (await JudgeWith(reply).JudgeAsync("q", "a", "r", Ct)).Ok();

        verdict.Passed.Should().Be(expected);
        verdict.Reason.Should().NotBeEmpty("the arbiter's own words are what makes a verdict auditable");
    }

    [Theory]
    [InlineData("Well, it depends on how you read the question.")]
    [InlineData("MAYBE\nhard to say")]
    [InlineData("")]
    public async Task An_unreadable_verdict_is_a_REFUSAL_and_never_a_NO(string reply)
    {
        var read = await JudgeWith(reply).JudgeAsync("q", "a", "r", Ct);

        read.Failed().Should().BeTrue(
            "defaulting to NO would make a broken arbiter look exactly like a wrong subject, on every leg it touched");
    }

    [Fact]
    public async Task An_arbiter_that_cannot_be_reached_refuses_rather_than_throwing()
    {
        var read = await new ModelJudge(new StubRuntime(string.Empty, "connection refused"), Endpoint, 1)
            .JudgeAsync("q", "a", "r", Ct);

        read.Reason().Should().Contain("could not be reached");
    }

    [Fact]
    public async Task The_arbiter_is_asked_deterministically_and_with_the_evidence_it_needs()
    {
        var runtime = new StubRuntime("YES\nfine");
        await new ModelJudge(runtime, Endpoint, seed: 42).JudgeAsync("how is the delay computed?", "the answer", "the reference", Ct);

        var sent = runtime.Last;
        sent.Sampling.Should().Be(Sampling.Deterministic(42), "an arbiter that samples differently re-scores an old run for no visible reason");
        sent.UserPrompt.Should().Contain("the reference").And.Contain("the answer").And.Contain("how is the delay computed?");
        sent.SystemPrompt.Should().Contain("YES or NO", "asked for a binary, because a number invents a scale and then drifts along it");
    }

    private static ModelEndpoint Endpoint =>
        ModelEndpoint.Parse(ModelRef.Parse("opus", ModelHosting.Cloud).Ok(), "https://example.invalid/v1").Ok();

    private static ModelJudge JudgeWith(string reply) => new(new StubRuntime(reply), Endpoint, 1);

    private sealed class StubRuntime(string reply, string failure = "") : IModelRuntime
    {
        public ModelRequest Last { get; private set; } = null!;

        public ModelHosting Hosting => ModelHosting.Cloud;

        public Task<Outcome<string>> AcceptBudgetAsync(Budget budget, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<string>.Success("stub"));

        public Task<Outcome<ModelAnswer>> AskAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            Last = request;

            return Task.FromResult(failure.Length > 0
                ? Outcome<ModelAnswer>.Failure(failure)
                : Outcome<ModelAnswer>.Success(new ModelAnswer(
                    reply.Length > 0 ? Captured.Text(reply) : Captured.Unavailable("the stream was empty"),
                    CapturedCount.Number(10),
                    CapturedCount.Number(5),
                    TimeSpan.FromMilliseconds(80),
                    SamplingAsSent.From(request.Sampling, "request-body"),
                    StopReason.Completed,
                    "Completed")));
        }
    }
}
