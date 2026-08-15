using System.Net;
using System.Text;
using Bench.Application;
using Bench.Domain.Models;
using Bench.Domain.Runs;
using Bench.Domain.Trace;
using Bench.Infrastructure.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bench.Tests.Models;

/// <summary>The first thing in this repository that actually asks a model something.</summary>
public sealed class OpenAiCompatibleRuntimeTests
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_wall_and_a_context_ceiling_are_accepted_and_name_the_runtime_that_took_them()
    {
        var runtime = Runtime(new FakeCompletions());

        foreach (var kind in new[] { BudgetKind.Wall, BudgetKind.Context })
        {
            var accepted = await runtime.AcceptBudgetAsync(Budget.Of(kind, BudgetScope.Question, 60), Ct);

            accepted.Ok().Should().Be(OpenAiCompatibleRuntime.RuntimeId);
        }
    }

    [Fact]
    public async Task A_cost_ceiling_is_REFUSED_because_a_completion_endpoint_knows_no_prices()
    {
        var refused = await Runtime(new FakeCompletions())
            .AcceptBudgetAsync(Budget.Of(BudgetKind.CostUsd, BudgetScope.Question, 1.25m), Ct);

        refused.Reason().Should().Contain("knows no prices")
            .And.Contain("harness not starting the next leg",
                "a budget nobody can enforce must be visible as such — one was believed for a whole series and reached nothing");
    }

    [Fact]
    public async Task A_turn_ceiling_is_refused_because_one_completion_has_no_turns()
    {
        (await Runtime(new FakeCompletions()).AcceptBudgetAsync(Budget.Of(BudgetKind.Turns, BudgetScope.Question, 8), Ct))
            .Reason().Should().Contain("no turns");
    }

    [Fact]
    public async Task An_answer_carries_its_text_its_tokens_its_latency_and_why_it_stopped()
    {
        var answer = (await Runtime(new FakeCompletions()).AskAsync(Request(), Ct)).Ok();

        answer.Text.Value.Should().Be("the total is summed in OrderService.Total");
        answer.PromptTokens.Value.Should().Be(41);
        answer.CompletionTokens.Value.Should().Be(12);
        answer.Stop.Should().Be(StopReason.Completed);
        answer.Latency.Should().BeGreaterThan(TimeSpan.Zero);
        answer.Describe.Should().Contain("12 out");
    }

    [Fact]
    public async Task Sampling_is_reported_as_SENT_and_matches_what_went_into_the_body()
    {
        var fake = new FakeCompletions();

        var answer = (await Runtime(fake).AskAsync(Request(), Ct)).Ok();

        answer.Sampling.Captured.Should().BeTrue();
        answer.Sampling.Source.Should().Be("request-body");
        answer.Sampling.Matches(Sampling.Deterministic(7)).Should().BeTrue();
        fake.LastBody.Should().Contain("\"seed\":7").And.Contain("\"temperature\":0");
    }

    [Fact]
    public async Task A_context_ceiling_travels_in_the_request_rather_than_being_hoped_for()
    {
        var fake = new FakeCompletions();
        var request = Request() with { Budgets = [Budget.Of(BudgetKind.Context, BudgetScope.Question, 512)] };

        await Runtime(fake).AskAsync(request, Ct);

        fake.LastBody.Should().Contain("\"max_tokens\":512");
    }

    [Fact]
    public async Task An_answer_cut_off_at_a_ceiling_says_so_rather_than_looking_like_a_bad_answer()
    {
        var answer = (await Runtime(new FakeCompletions(finish: "length")).AskAsync(Request(), Ct)).Ok();

        answer.Stop.Should().Be(StopReason.LengthCapped);
        answer.WasCutOff.Should().BeTrue("scoring a truncated answer as wrong measures the ceiling, not the model");
    }

    [Fact]
    public async Task A_response_with_no_usage_reports_tokens_as_unknown_rather_than_zero()
    {
        var answer = (await Runtime(new FakeCompletions(withUsage: false)).AskAsync(Request(), Ct)).Ok();

        answer.PromptTokens.WasCaptured.Should().BeFalse();
        answer.CompletionTokens.WasCaptured.Should().BeFalse();
        answer.Describe.Should().Contain("tokens unreported");
    }

    [Fact]
    public async Task A_response_with_no_content_reports_no_text_rather_than_an_empty_answer()
    {
        var answer = (await Runtime(new FakeCompletions(content: "")).AskAsync(Request(), Ct)).Ok();

        answer.Text.WasCaptured.Should().BeFalse();
        answer.Text.Reason.Should().Contain("no message content");
    }

    [Fact]
    public async Task A_non_success_status_is_an_answer_naming_it_not_an_exception()
    {
        var refused = await Runtime(new FakeCompletions(status: HttpStatusCode.NotFound, body: "model not found"))
            .AskAsync(Request(), Ct);

        refused.Reason().Should().Contain("404").And.Contain("model not found");
    }

    [Fact]
    public async Task An_unreachable_endpoint_is_an_answer_rather_than_an_exception()
    {
        var refused = await Runtime(new ThrowingHandler()).AskAsync(Request(), Ct);

        refused.Reason().Should().Contain("unreachable");
    }

    [Fact]
    public void An_endpoint_with_no_url_is_refused_exactly_like_an_unset_model_id()
    {
        ModelEndpoint.Parse(Model(), "  ").Reason().Should().Contain("unset endpoint is a refusal");
        ModelEndpoint.Parse(Model(), "localhost:11434").Reason().Should().Contain("absolute http(s) url");
        ModelEndpoint.Parse(Model(), "http://x/v1", inputPerMTok: -1).Reason().Should().Contain("not a price");
    }

    [Fact]
    public void A_trailing_slash_on_the_base_url_does_not_produce_a_double_slash()
    {
        ModelEndpoint.Parse(Model(), "http://127.0.0.1:11434/v1/").Ok().BaseUrl.Should().Be("http://127.0.0.1:11434/v1");
    }

    [Fact]
    public void Cost_comes_from_the_tokens_that_were_actually_reported()
    {
        var endpoint = ModelEndpoint.Parse(Model(), "http://x/v1", inputPerMTok: 3m, outputPerMTok: 15m).Ok();

        endpoint.CostOf(CapturedCount.Number(1_000_000), CapturedCount.Number(100_000)).Ok().Should().Be(4.5m);
    }

    [Fact]
    public void Unreported_tokens_make_the_cost_UNKNOWN_rather_than_free()
    {
        var endpoint = ModelEndpoint.Parse(Model(), "http://x/v1", 3m, 15m).Ok();

        endpoint.CostOf(CapturedCount.Unavailable("no usage"), CapturedCount.Number(10)).Reason()
            .Should().Contain("unknown rather than zero");
    }

    [Fact]
    public void A_local_model_costs_nothing_and_that_zero_is_a_real_measurement()
    {
        var local = ModelEndpoint.Parse(Model(), "http://127.0.0.1:11434/v1").Ok();

        local.CostOf(CapturedCount.Number(5000), CapturedCount.Number(500)).Ok().Should().Be(0m);
    }

    private static ModelRef Model() => ModelRef.Parse("qwen3-coder:latest", ModelHosting.Local).Ok();

    private static ModelRequest Request() =>
        ModelRequest.Of(
            ModelEndpoint.Parse(Model(), "http://127.0.0.1:11434/v1").Ok(),
            Sampling.Deterministic(7),
            "where is the order total computed?");

    private static OpenAiCompatibleRuntime Runtime(HttpMessageHandler handler) =>
        new(new SingleHandlerFactory(handler), NullLogger<OpenAiCompatibleRuntime>.Instance);

    private sealed class FakeCompletions(
        string content = "the total is summed in OrderService.Total",
        string finish = "stop",
        bool withUsage = true,
        HttpStatusCode status = HttpStatusCode.OK,
        string body = "") : HttpMessageHandler
    {
        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = await request.Content!.ReadAsStringAsync(cancellationToken);

            if (status != HttpStatusCode.OK)
            {
                return new HttpResponseMessage(status) { Content = new StringContent(body) };
            }

            var usage = withUsage ? ""","usage":{"prompt_tokens":41,"completion_tokens":12,"total_tokens":53}""" : "";
            var payload = $$"""
            {"choices":[{"message":{"role":"assistant","content":"{{content}}"},"finish_reason":"{{finish}}"}]{{usage}}}
            """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused");
    }

    private sealed class SingleHandlerFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
