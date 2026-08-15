using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Bench.Application;
using Bench.Domain;
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

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            return Outcome<ModelAnswer>.Success(Read(payload, request, clock.Elapsed));
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
        var messages = new List<Dictionary<string, string>>();

        if (request.SystemPrompt.Length > 0)
        {
            messages.Add(new() { ["role"] = "system", ["content"] = request.SystemPrompt });
        }

        messages.Add(new() { ["role"] = "user", ["content"] = request.UserPrompt });

        var body = new Dictionary<string, object>
        {
            ["model"] = request.Endpoint.Model.Id,
            ["messages"] = messages,
            ["stream"] = false,
            ["temperature"] = request.Sampling.Temperature,
            ["seed"] = request.Sampling.Seed,
        };

        if (Context(request) is { } cap)
        {
            body["max_tokens"] = cap;
        }

        return body;
    }

    private static ModelAnswer Read(JsonElement payload, ModelRequest request, TimeSpan latency)
    {
        var choice = payload.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array
            ? choices.EnumerateArray().FirstOrDefault()
            : default;

        var text = Text(choice.TryGetProperty("message", out var message) ? message : default, "content");
        var finish = Text(choice, "finish_reason");
        var usage = payload.TryGetProperty("usage", out var u) ? u : default;

        return new ModelAnswer(
            text.Length > 0 ? Captured.Text(text) : Captured.Unavailable("the response carried no message content"),
            Count(usage, "prompt_tokens"),
            Count(usage, "completion_tokens"),
            latency,
            // As SENT: these are the values this method put in the body a moment ago, not values read back
            // from a setting somewhere.
            SamplingAsSent.From(request.Sampling, "request-body"),
            Stop(finish),
            finish);
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
