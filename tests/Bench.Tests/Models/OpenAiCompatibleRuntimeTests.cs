using System.Net;
using System.Text;
using Bench.Application;
using Bench.Domain.Engines;
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

    // ---- The tool-calling half (2026-08-19). Everything above ran unchanged when it was added, which is
    // the whole proof that this is an addition rather than a new runtime.

    [Fact]
    public async Task A_request_with_no_tools_sends_the_body_it_always_sent()
    {
        // ABSENT, not empty. `tools: []` is a different request from no tools at all at several endpoints,
        // and the no-tools arm is the floor every tool claim is measured against — it has to send exactly
        // what it sent before this existed.
        var handler = new FakeCompletions();

        await Runtime(handler).AskAsync(Request(), Ct);

        handler.LastBody.Should().NotContain("tools");
        handler.LastBody.Should().NotContain("tool_calls");
    }

    [Fact]
    public async Task Tools_are_advertised_as_functions_with_their_schema_as_an_OBJECT()
    {
        var handler = new FakeCompletions();

        await Runtime(handler).AskAsync(WithTools(), Ct);

        handler.LastBody.Should().Contain("\"tools\"").And.Contain("\"function\"");
        handler.LastBody.Should().Contain("rt_read_local_file");
        // Parsed, not passed through as a string: a schema sent as text advertises every tool as taking one
        // text argument, which no model can form a call against.
        handler.LastBody.Should().Contain("\"parameters\":{\"type\":\"object\"");
        handler.LastBody.Should().NotContain("\"parameters\":\"{");
    }

    [Fact]
    public async Task A_tool_whose_schema_is_not_JSON_is_refused_BEFORE_the_call()
    {
        var handler = new FakeCompletions();

        var refused = await Runtime(handler).AskAsync(
            Request() with { Tools = [new EngineTool("broken", "does things", "{not json")] }, Ct);

        // A configuration fault, named — not an exception out of body-building, and not a leg that records
        // an unreachable model for a mistake in its own lane.
        refused.Reason().Should().Contain("broken").And.Contain("not JSON");
        handler.LastBody.Should().BeEmpty("nothing may be sent when the request cannot be built");
    }

    [Theory]
    [InlineData("""{"path":"string","startLine":"int?"}""")]
    [InlineData("""{"properties":{"path":{"type":"string"}}}""")]
    public async Task Argument_JSON_that_is_not_a_SCHEMA_is_refused_even_though_it_parses(string schema)
    {
        // Valid JSON is not the bar, and the first version of this guard set it there. Both engines in this
        // repository described their arguments in the first shape above — perfectly valid JSON, and not a
        // schema at all. It would have reached the wire as `parameters`, no model could have formed a call
        // against it, and the measurement would have read as "the model cannot use tools" when what happened
        // is that we sent it nonsense.
        var handler = new FakeCompletions();

        var refused = await Runtime(handler).AskAsync(
            Request() with { Tools = [new EngineTool("shorthand", "does things", schema)] }, Ct);

        refused.Reason().Should().Contain("shorthand").And.Contain("not a schema");
        handler.LastBody.Should().BeEmpty("nothing may be sent when the request cannot be built");
    }

    [Theory]
    [InlineData("""["path"]""")]
    [InlineData("\"a string\"")]
    [InlineData("5")]
    public async Task A_parameters_value_that_is_not_an_OBJECT_is_refused_by_its_KIND(string schema)
    {
        // A separate theory from the one above, and deliberately so: this refusal names what arrived
        // ("advertises a Array where a JSON Schema object is required") rather than saying "not a schema",
        // and a reader chasing a broken engine is better served by the kind than by the category. Running
        // the first version of these tests is what separated them — one assertion had been written over
        // two different refusals, and only the array case exposed it.
        var handler = new FakeCompletions();

        var refused = await Runtime(handler).AskAsync(
            Request() with { Tools = [new EngineTool("shorthand", "does things", schema)] }, Ct);

        refused.Reason().Should().Contain("shorthand").And.Contain("JSON Schema object is required");
        handler.LastBody.Should().BeEmpty("nothing may be sent when the request cannot be built");
    }

    [Fact]
    public async Task A_tool_that_takes_no_arguments_at_all_is_still_a_valid_schema()
    {
        // An object with no properties is a real shape — "this tool takes nothing" — so requiring
        // `properties` would refuse a legitimate tool. The guard checks the one thing the wire needs.
        var handler = new FakeCompletions();

        await Runtime(handler).AskAsync(
            Request() with { Tools = [new EngineTool("ping", "takes nothing", """{"type":"object"}""")] }, Ct);

        handler.LastBody.Should().Contain("ping");
    }

    [Fact]
    public async Task A_transcript_is_replayed_as_assistant_and_tool_messages()
    {
        var handler = new FakeCompletions();

        await Runtime(handler).AskAsync(
            WithTools() with
            {
                Transcript =
                [
                    new ModelTurn.Assistant("", [new RequestedToolCall("call_1", "rt_read_local_file", """{"path":"a.cs"}""")]),
                    new ModelTurn.ToolResult("call_1", "rt_read_local_file", "lines 1-2 of 2", Refused: false),
                ],
            },
            Ct);

        handler.LastBody.Should().Contain("\"role\":\"assistant\"").And.Contain("\"tool_calls\"");
        handler.LastBody.Should().Contain("\"role\":\"tool\"").And.Contain("\"tool_call_id\":\"call_1\"");
        handler.LastBody.Should().Contain("lines 1-2 of 2");
    }

    [Fact]
    public async Task A_refusal_reaches_the_model_as_content_because_it_has_to_correct_itself()
    {
        var handler = new FakeCompletions();

        await Runtime(handler).AskAsync(
            WithTools() with
            {
                Transcript =
                [
                    new ModelTurn.ToolResult("call_1", "rt_read_local_file", "outside the workspace", Refused: true),
                ],
            },
            Ct);

        // The model sees the reason. That it was a REFUSAL rather than an answer is the harness's record,
        // not a field on the wire — there is nowhere on the wire to put it.
        handler.LastBody.Should().Contain("outside the workspace");
        handler.LastBody.Should().NotContain("refused");
    }

    [Fact]
    public async Task An_answer_that_asks_for_a_tool_is_not_final_and_carries_the_call()
    {
        var answer = (await Runtime(new FakeCompletions(
                content: "",
                toolCalls: """[{"id":"call_9","type":"function","function":{"name":"rag_search_project_context","arguments":"{\"query\":\"where is login\"}"}}]"""))
            .AskAsync(WithTools(), Ct)).Ok();

        answer.IsFinal.Should().BeFalse();
        answer.ToolCalls.Should().ContainSingle();
        answer.ToolCalls[0].Id.Should().Be("call_9");
        answer.ToolCalls[0].Name.Should().Be("rag_search_project_context");
        answer.ToolCalls[0].ArgumentsJson.Should().Contain("where is login");
    }

    [Fact]
    public async Task Broken_argument_JSON_reaches_the_harness_VERBATIM()
    {
        // The load-bearing one. A local model emits broken JSON regularly, and "can it form the arguments"
        // is one of the three questions this benchmark exists to answer. A parse-then-reserialize here would
        // repair the mistake on its way in and make the observation impossible.
        var answer = (await Runtime(new FakeCompletions(
                content: "",
                toolCalls: """[{"id":"c","type":"function","function":{"name":"t","arguments":"{path: 'a.cs'"}}]"""))
            .AskAsync(WithTools(), Ct)).Ok();

        answer.ToolCalls[0].ArgumentsJson.Should().Be("{path: 'a.cs'");
    }

    [Fact]
    public async Task A_turn_that_only_asks_for_a_tool_does_not_record_missing_content_as_a_fault()
    {
        // The normal shape of a loop's middle. Reporting "the response carried no message content" for it
        // would put a fault in the record of every multi-turn leg.
        var answer = (await Runtime(new FakeCompletions(
                content: "",
                toolCalls: """[{"id":"c","type":"function","function":{"name":"t","arguments":"{}"}}]"""))
            .AskAsync(WithTools(), Ct)).Ok();

        answer.Text.WasCaptured.Should().BeFalse();
        answer.Text.Reason.Should().Contain("asked for a tool");
    }

    [Fact]
    public async Task A_call_naming_no_tool_is_dropped_because_nothing_can_invoke_or_score_it()
    {
        var answer = (await Runtime(new FakeCompletions(
                content: "",
                toolCalls: """[{"id":"c","type":"function","function":{"arguments":"{}"}}]"""))
            .AskAsync(WithTools(), Ct)).Ok();

        answer.ToolCalls.Should().BeEmpty();
        answer.IsFinal.Should().BeTrue("a turn whose only call names nothing has asked for nothing");
    }

    [Fact]
    public async Task An_answer_with_no_tool_calls_is_final()
    {
        (await Runtime(new FakeCompletions()).AskAsync(Request(), Ct)).Ok().IsFinal.Should().BeTrue();
    }

    private static ModelRequest WithTools() =>
        Request() with
        {
            Tools =
            [
                new EngineTool(
                    "rt_read_local_file",
                    "Read a file from the workspace, whole or as a line window.",
                    """{"type":"object","properties":{"path":{"type":"string"}},"required":["path"]}"""),
            ],
        };

    private static ModelRequest Request() =>
        ModelRequest.Of(
            ModelEndpoint.Parse(Model(), "http://127.0.0.1:11434/v1").Ok(),
            Sampling.Deterministic(7),
            "where is the order total computed?");

    private static OpenAiCompatibleRuntime Runtime(HttpMessageHandler handler) =>
        new(new SingleHandlerFactory(handler), NullLogger<OpenAiCompatibleRuntime>.Instance);

    /// <param name="toolCalls">The raw <c>tool_calls</c> array to answer with, or empty for a plain
    /// completion. Added rather than copied into a second fake: the subject is the same runtime, and two
    /// handlers producing two slightly different response shapes is how a parser comes to be tested against
    /// a payload no endpoint sends.</param>
    private sealed class FakeCompletions(
        string content = "the total is summed in OrderService.Total",
        string finish = "stop",
        bool withUsage = true,
        HttpStatusCode status = HttpStatusCode.OK,
        string body = "",
        string toolCalls = "") : HttpMessageHandler
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
            var calls = toolCalls.Length > 0 ? $""","tool_calls":{toolCalls}""" : "";
            var payload = $$"""
            {"choices":[{"message":{"role":"assistant","content":"{{content}}"{{calls}}},"finish_reason":"{{finish}}"}]{{usage}}}
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
