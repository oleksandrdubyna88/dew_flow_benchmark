using Bench.Application;
using Bench.Domain;
using Bench.Domain.Models;
using Bench.Domain.Runs;
using Bench.Domain.Trace;
using Bench.Infrastructure.Models;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The subject router: one runtime for a run whose subjects are driven differently, keyed by
/// the model id every aggregate already uses — and a budget that ANY route cannot honour refuses the
/// run whole, because a ceiling that silently bound only half a run is the context-compaction lie.</summary>
public sealed class SubjectRouterTests
{
    [Fact]
    public async Task A_request_is_routed_by_its_models_id_and_everything_else_falls_back()
    {
        var fallback = new NamedRuntime("http");
        var cli = new NamedRuntime("cli");
        var router = new SubjectRouter(fallback);
        router.Add("claude-sonnet-5", cli);

        (await router.AskAsync(Request("claude-sonnet-5"), CancellationToken.None)).Ok()
            .StopDetail.Should().Be("cli");
        (await router.AskAsync(Request("qwen3-coder:latest"), CancellationToken.None)).Ok()
            .StopDetail.Should().Be("http");
    }

    [Fact]
    public async Task A_budget_one_route_cannot_honour_refuses_the_run_whole()
    {
        var router = new SubjectRouter(new NamedRuntime("http"));
        router.Add("claude-sonnet-5", new RefusingRuntime("no turns here"));

        var refused = await router.AcceptBudgetAsync(
            Budget.Of(BudgetKind.Turns, BudgetScope.Question, 25), CancellationToken.None);

        refused.Reason().Should().Contain("claude-sonnet-5").And.Contain("no turns here",
            "a ceiling that silently bound only the half of the run that understood it is the measured lie");
    }

    private static ModelRequest Request(string modelId) => new(
        ModelEndpoint.Cli(ModelRef.Parse(modelId, ModelHosting.Cloud).Ok()),
        Sampling.Deterministic(1), string.Empty, "prompt", []);

    private sealed class NamedRuntime(string name) : IModelRuntime
    {
        public ModelHosting Hosting => ModelHosting.Local;

        public Task<Outcome<string>> AcceptBudgetAsync(Budget budget, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<string>.Success(name));

        public Task<Outcome<ModelAnswer>> AskAsync(ModelRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<ModelAnswer>.Success(new ModelAnswer(
                Captured.Text("answer"), CapturedCount.Number(1), CapturedCount.Number(1),
                TimeSpan.Zero, SamplingAsSent.NotCaptured("fake"), StopReason.Completed, name)));
    }

    private sealed class RefusingRuntime(string why) : IModelRuntime
    {
        public ModelHosting Hosting => ModelHosting.Cloud;

        public Task<Outcome<string>> AcceptBudgetAsync(Budget budget, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<string>.Failure(why));

        public Task<Outcome<ModelAnswer>> AskAsync(ModelRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException("this fake only refuses budgets");
    }
}
