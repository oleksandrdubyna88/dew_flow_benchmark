using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Bench.Application;
using Bench.Domain;
using Bench.Domain.Engines;
using Bench.Domain.Models;
using Bench.Domain.Runs;
using Bench.Domain.Trace;
using Microsoft.Extensions.Logging;

namespace Bench.Infrastructure.Models;

/// <summary>A local model behind an OpenAI-compatible endpoint — Ollama's <c>/v1</c>, and anything that
/// speaks the same shape.
/// <para>
/// Local first, and deliberately: it is free, it is deterministic when seeded, and the budget-confirmation
/// path can be got right without a bill arriving for every mistake along the way.
/// </para></summary>
public sealed class OpenAiCompatibleRuntime(IHttpClientFactory factory, ILogger<OpenAiCompatibleRuntime> logger)
    : IModelRuntime
{
    public const string RuntimeId = "openai-compatible-http";

    public ModelHosting Hosting => ModelHosting.Local;

    /// <summary>Which ceilings this runtime can actually impose, and which it must refuse.
    /// <para>
    /// This is the whole point of the method rather than a formality. A context ceiling was once configured,
    /// believed and reasoned from for an entire measurement series while reaching nothing at all, and a real
    /// degradation was attributed to a flooded window that never happened. So a budget this runtime cannot
    /// enforce comes back as a REFUSAL naming why, and the run is marked instead of scored.
    /// </para>
    /// <para>
    /// <b>Cost is refused on purpose, and it is not an oversight.</b> Nothing at a completion endpoint knows
    /// prices; cost is computed afterwards from the tokens it reported. A cost ceiling is therefore enforced
    /// by the HARNESS declining to start the next leg, never by the endpoint stopping mid-answer.
    /// </para></summary>
    public Task<Outcome<string>> AcceptBudgetAsync(Budget budget, CancellationToken cancellationToken) =>
        Task.FromResult(budget.Kind switch
        {
            BudgetKind.Wall => Outcome<string>.Success(RuntimeId),
            BudgetKind.Context => Outcome<string>.Success(RuntimeId),
            BudgetKind.CostUsd => Outcome<string>.Failure(
                "a completion endpoint knows no prices — a cost ceiling is enforced by the harness not starting the next leg"),
            BudgetKind.Turns => Outcome<string>.Failure(
                "one completion has no turns — a turn ceiling belongs to an agentic loop, not to this runtime"),
            _ => Outcome<string>.Failure($"unknown budget kind {budget.Kind}"),
        });

    public async Task<Outcome<ModelAnswer>> AskAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        if (RefuseUnusableTools(request) is { Length: > 0 } unusable)
        {
            return Outcome<ModelAnswer>.Failure(unusable);
        }

        var body = Body(request);
        var clock = Stopwatch.StartNew();

        try
        {
            using var http = factory.CreateClient("model-runtime");
            http.BaseAddress = new Uri(request.Endpoint.BaseUrl + "/");
            http.Timeout = Wall(request);

            using var response = await http.PostAsJsonAsync("chat/completions", body, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(cancellationToken);
                return Outcome<ModelAnswer>.Failure(
                    $"{request.Endpoint.Model.Id} answered {(int)response.StatusCode}: {Short(detail)}");
            }

            // Text first, then parse: the response's SIZE is part of what a cell records, and a
            // deserializer that consumed the stream leaves nothing left to measure.
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            return Outcome<ModelAnswer>.Success(
                Read(JsonDocument.Parse(payload).RootElement, request, clock.Elapsed) with
                {
                    ResponseBytes = System.Text.Encoding.UTF8.GetByteCount(payload),
                });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Our own wall budget, not the caller giving up. A value, so the leg records a ceiling rather
            // than the run unwinding over one slow answer.
            return Outcome<ModelAnswer>.Failure(
                $"{request.Endpoint.Model.Id} did not answer within {Wall(request).TotalSeconds:0.#}s");
        }
        catch (HttpRequestException ex)
        {
            logger.LogDebug(ex, "{Model} at {Url} did not answer", request.Endpoint.Model.Id, request.Endpoint.BaseUrl);
            return Outcome<ModelAnswer>.Failure($"{request.Endpoint.Model.Id} is unreachable: {Short(ex.Message)}");
        }
    }

    /// <summary>The request body — and the reason sampling travels IN it rather than being configured
    /// anywhere: at least one runtime's OpenAI-compatible route substitutes its own defaults over whatever a
    /// model file declared, which makes every "deterministic" measurement through it unreproducible while
    /// looking configured.</summary>
    private static Dictionary<string, object> Body(ModelRequest request)
    {
        var messages = new List<Dictionary<string, object>>();

        if (request.SystemPrompt.Length > 0)
        {
            messages.Add(new() { ["role"] = "system", ["content"] = request.SystemPrompt });
        }

        messages.Add(new() { ["role"] = "user", ["content"] = request.UserPrompt });
        messages.AddRange(request.Transcript.Select(Message));

        var body = new Dictionary<string, object>
        {
            ["model"] = request.Endpoint.Model.Id,
            ["messages"] = messages,
            ["stream"] = false,
            ["temperature"] = request.Sampling.Temperature,
            ["seed"] = request.Sampling.Seed,
        };

        // Absent, not empty, when the lane offers nothing. An empty `tools: []` is a different request from
        // one with no tools at all at several endpoints, and the no-tools arm is the floor every tool claim
        // is measured against — it must send exactly the body it always sent.
        if (request.Tools.Count > 0)
        {
            body["tools"] = request.Tools.Select(Tool).ToList();
        }

        if (Context(request) is { } cap)
        {
            body["max_tokens"] = cap;
        }

        return body;
    }

    /// <summary>One transcript entry as the wire wants it.
    /// <para>An assistant turn carries <c>content</c> even when it is empty, because an endpoint that
    /// receives a message with neither content nor tool calls has been handed a turn that says nothing —
    /// and the one that produced it said something, or asked for a call.</para></summary>
    private static Dictionary<string, object> Message(ModelTurn turn) =>
        turn switch
        {
            ModelTurn.Assistant assistant when assistant.ToolCalls.Count > 0 => new()
            {
                ["role"] = "assistant",
                ["content"] = assistant.Text,
                ["tool_calls"] = assistant.ToolCalls.Select(Call).ToList(),
            },
            ModelTurn.Assistant assistant => new()
            {
                ["role"] = "assistant",
                ["content"] = assistant.Text,
            },
            ModelTurn.ToolResult result => new()
            {
                ["role"] = "tool",
                ["tool_call_id"] = result.ToolCallId,
                // The model sees the refusal REASON as ordinary content — it has to, in order to correct
                // itself. That the tool refused rather than answered is recorded by the harness, not sent.
                ["content"] = result.Content,
            },
            _ => throw new InvalidOperationException("unreachable"),
        };

    private static Dictionary<string, object> Call(RequestedToolCall call) => new()
    {
        ["id"] = call.Id,
        ["type"] = "function",
        ["function"] = new Dictionary<string, object>
        {
            ["name"] = call.Name,
            // Verbatim, as the model produced it. Re-serializing would repair broken JSON on the way back
            // in, and a loop that quietly fixes the model's mistakes cannot measure that it makes them.
            ["arguments"] = call.ArgumentsJson,
        },
    };

    /// <summary>One tool as the request advertises it.
    /// <para>The schema is parsed rather than passed through as a string: the wire wants a JSON OBJECT
    /// there, and a string would advertise every tool as taking one text argument. A schema that will not
    /// parse is refused before the call — see <see cref="RefuseUnusableTools"/>.</para></summary>
    private static Dictionary<string, object> Tool(EngineTool tool) => new()
    {
        ["type"] = "function",
        ["function"] = new Dictionary<string, object>
        {
            ["name"] = tool.Name,
            ["description"] = tool.Description,
            ["parameters"] = JsonDocument.Parse(tool.ArgumentsSchema).RootElement.Clone(),
        },
    };

    /// <summary>Why this request cannot be sent, or empty when it can.
    /// <para>Checked BEFORE the HTTP call so a broken schema is a named refusal rather than an exception out
    /// of body-building — the leg then records a configuration fault, which is what it is, instead of an
    /// unreachable model.</para></summary>
    private static string RefuseUnusableTools(ModelRequest request)
    {
        foreach (var tool in request.Tools)
        {
            try
            {
                using var _ = JsonDocument.Parse(tool.ArgumentsSchema);
            }
            catch (JsonException ex)
            {
                return $"tool '{tool.Name}' advertises an argument schema that is not JSON: {Short(ex.Message)}";
            }
        }

        return string.Empty;
    }

    private static ModelAnswer Read(JsonElement payload, ModelRequest request, TimeSpan latency)
    {
        var choice = payload.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array
            ? choices.EnumerateArray().FirstOrDefault()
            : default;

        var text = Text(choice.TryGetProperty("message", out var message) ? message : default, "content");
        var finish = Text(choice, "finish_reason");
        var usage = payload.TryGetProperty("usage", out var u) ? u : default;

        var calls = ToolCalls(choice.TryGetProperty("message", out var asked) ? asked : default);

        return new ModelAnswer(
            // A turn that asks for a tool and says nothing else is NOT a missing answer — it is the normal
            // shape of a loop's middle. Reporting "no message content" for it would put a fault in every
            // multi-turn leg's record.
            text.Length > 0
                ? Captured.Text(text)
                : Captured.Unavailable(
                    calls.Count > 0
                        ? "this turn asked for a tool rather than answering"
                        : "the response carried no message content"),
            Count(usage, "prompt_tokens"),
            Count(usage, "completion_tokens"),
            latency,
            // As SENT: these are the values this method put in the body a moment ago, not values read back
            // from a setting somewhere.
            SamplingAsSent.From(request.Sampling, "request-body"),
            Stop(finish),
            finish)
        {
            Thinking = Thinking(choice.TryGetProperty("message", out var thought) ? thought : default),
            ToolCalls = calls,
        };
    }

    /// <summary>What the model asked to call, if anything.
    /// <para>The arguments are taken as RAW TEXT, never re-serialized. A local model emits broken JSON
    /// regularly, and a parse-then-write here would silently repair it — which would make the one
    /// observation this whole benchmark exists to record, "can it form the arguments", unobservable.</para>
    /// <para>A call with no name is dropped rather than carried: it names no tool, so nothing can invoke it
    /// and nothing can score it. A call with no id keeps an empty one — the id is the endpoint's own
    /// correlation token, and inventing one would put a value on the wire the endpoint never issued.</para></summary>
    private static IReadOnlyList<RequestedToolCall> ToolCalls(JsonElement message)
    {
        if (!message.TryGetProperty("tool_calls", out var calls) || calls.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return
        [
            .. calls.EnumerateArray()
                .Select(call => new RequestedToolCall(
                    Text(call, "id"),
                    Text(call.TryGetProperty("function", out var fn) ? fn : default, "name"),
                    Text(call.TryGetProperty("function", out var args) ? args : default, "arguments")))
                .Where(call => call.Name.Length > 0),
        ];
    }

    /// <summary>The model's reasoning, when the endpoint separates it from the answer.
    /// <para>
    /// Two field names because two conventions exist for the same thing and neither is standard:
    /// <c>reasoning_content</c> (Ollama, vLLM, DeepSeek's own API) and <c>reasoning</c>. An endpoint that
    /// sends neither reports that it sends neither — an empty string here would make a model that hides its
    /// reasoning indistinguishable from one that did none.
    /// </para></summary>
    private static Captured Thinking(JsonElement message)
    {
        var reasoning = Text(message, "reasoning_content");
        var fallback = reasoning.Length > 0 ? reasoning : Text(message, "reasoning");

        return fallback.Length > 0
            ? Captured.Text(fallback)
            : Captured.Unavailable("the response carried no reasoning field");
    }

    private static StopReason Stop(string finishReason) =>
        finishReason switch
        {
            "stop" => StopReason.Completed,
            "length" => StopReason.LengthCapped,
            "content_filter" => StopReason.Refused,
            _ => StopReason.Unknown,
        };

    private static TimeSpan Wall(ModelRequest request) =>
        request.Budgets.FirstOrDefault(b => b.Kind == BudgetKind.Wall) is { } wall && wall.Limit > 0
            ? TimeSpan.FromSeconds((double)wall.Limit)
            : TimeSpan.FromMinutes(10);

    private static int? Context(ModelRequest request) =>
        request.Budgets.FirstOrDefault(b => b.Kind == BudgetKind.Context) is { } context && context.Limit > 0
            ? (int)context.Limit
            : null;

    private static CapturedCount Count(JsonElement usage, string property) =>
        usage.ValueKind == JsonValueKind.Object
        && usage.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.Number
            ? CapturedCount.Number(value.GetInt64())
            : CapturedCount.Unavailable($"the response reported no {property}");

    private static string Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string Short(string text) =>
        text.Length <= 200 ? text.Trim() : text[..200].Trim() + "…";
}
