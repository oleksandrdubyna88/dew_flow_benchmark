using Bench.Domain.Runs;
using Bench.Domain.Trace;

namespace Bench.Domain.Models;

/// <summary>Where a model actually lives, and what its tokens cost.
/// <para>
/// Deliberately NOT part of <see cref="ModelRef"/>. A model id is an identity — every aggregate, every
/// discrimination reading and every saturation label is keyed by it — and folding an address into that would
/// make the same model at a different port a different subject. Address and price are configuration; the id
/// is what results are compared by.
/// </para>
/// <para>
/// Prices are operator-entered because no endpoint reports them. Zero is the honest value for a local model
/// and for a flat-fee subscription: per-token figures there would describe a bill nobody receives.
/// </para></summary>
public sealed record ModelEndpoint
{
    private ModelEndpoint(ModelRef model, string baseUrl, decimal inputPerMTok, decimal outputPerMTok)
    {
        Model = model;
        BaseUrl = baseUrl;
        InputCostPerMTok = inputPerMTok;
        OutputCostPerMTok = outputPerMTok;
    }

    public ModelRef Model { get; }

    /// <summary>The OpenAI-compatible base, e.g. <c>http://127.0.0.1:11434/v1</c>.</summary>
    public string BaseUrl { get; }

    public decimal InputCostPerMTok { get; }

    public decimal OutputCostPerMTok { get; }

    /// <summary>An endpoint-shaped identity for a subject that is DRIVEN as a process rather than
    /// answered over HTTP (todo/PLAN_investigate_vs_implement.md §3.6). The base url is the literal
    /// <c>cli</c> — an address would be a lie, and every printout that shows it says what this subject
    /// is. Prices ride as usual: the CLI's own reported cost is the authoritative figure, and these are
    /// the fallback arithmetic.</summary>
    public static ModelEndpoint Cli(ModelRef model, decimal inputPerMTok = 0, decimal outputPerMTok = 0) =>
        new(model, "cli", inputPerMTok, outputPerMTok);

    public static Outcome<ModelEndpoint> Parse(
        ModelRef model, string? baseUrl, decimal inputPerMTok = 0, decimal outputPerMTok = 0)
    {
        var trimmed = (baseUrl ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            return Outcome<ModelEndpoint>.Failure(
                $"model '{model.Id}' has no base url — an unset endpoint is a refusal, exactly like an unset model id");
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return Outcome<ModelEndpoint>.Failure($"'{trimmed}' is not an absolute http(s) url");
        }

        if (inputPerMTok < 0 || outputPerMTok < 0)
        {
            return Outcome<ModelEndpoint>.Failure("a negative price is not a price");
        }

        return Outcome<ModelEndpoint>.Success(
            new ModelEndpoint(model, trimmed.TrimEnd('/'), inputPerMTok, outputPerMTok));
    }

    /// <summary>What a leg cost, from the tokens it actually reported. Unreported tokens produce no cost —
    /// not a zero cost, which would read as free.</summary>
    public Outcome<decimal> CostOf(CapturedCount promptTokens, CapturedCount completionTokens)
    {
        if (!promptTokens.WasCaptured || !completionTokens.WasCaptured)
        {
            return Outcome<decimal>.Failure(
                $"model '{Model.Id}' did not report its tokens, so its cost is unknown rather than zero");
        }

        var cost = (promptTokens.Value * InputCostPerMTok + completionTokens.Value * OutputCostPerMTok) / 1_000_000m;
        return Outcome<decimal>.Success(cost);
    }
}

/// <summary>Why the model stopped talking.
/// <para>
/// The distinction that matters is <see cref="LengthCapped"/>. An answer cut off at a token ceiling and
/// scored as wrong measures the ceiling, not the model — and it looks identical to a bad answer unless the
/// reason is carried.
/// </para></summary>
public enum StopReason
{
    /// <summary>It finished on its own.</summary>
    Completed,

    /// <summary>It ran into a token ceiling. Not a wrong answer.</summary>
    LengthCapped,

    /// <summary>The runtime declined — a content filter, a refusal.</summary>
    Refused,

    /// <summary>The runtime said nothing about why. Not "completed".</summary>
    Unknown,
}

/// <summary>What one call to a model produced.</summary>
/// <param name="Sampling">What the REQUEST carried, captured from the request itself — a value read back
/// from configuration is not evidence that it was applied.</param>
public sealed record ModelAnswer(
    Captured Text,
    CapturedCount PromptTokens,
    CapturedCount CompletionTokens,
    TimeSpan Latency,
    SamplingAsSent Sampling,
    StopReason Stop,
    string StopDetail)
{
    /// <summary>The model's reasoning text, when the runtime returns it separately from the answer.
    /// <para>
    /// <see cref="Captured"/> rather than a string, and that is the whole point: most endpoints return no
    /// thinking at all, and an empty string would make "this model does not expose its reasoning"
    /// indistinguishable from "it thought about nothing". It is stored because a wrong answer with visible
    /// reasoning is the only evidence that says WHERE the run went wrong.
    /// </para>
    /// <para>An <c>init</c> property rather than a positional parameter, so adding it cannot break a caller
    /// that constructs an answer positionally — the shape <see cref="LegTrace.FunnelNote"/> established.</para></summary>
    public Captured Thinking { get; init; } = Captured.Unavailable("the runtime returned no separate reasoning");

    /// <summary>Bytes of the runtime's response payload, as this process received it.</summary>
    public long ResponseBytes { get; init; }

    /// <summary>What the model wants called before it will answer.
    /// <para>An <c>init</c> property with an empty default, like <see cref="Thinking"/> and
    /// <see cref="ResponseBytes"/> beside it, so every existing construction of an answer keeps compiling
    /// and keeps meaning what it meant: no tool calls is what a single-shot completion has always
    /// produced.</para></summary>
    public IReadOnlyList<RequestedToolCall> ToolCalls { get; init; } = [];

    /// <summary>Whether this answer ends the conversation.
    /// <para>Derived rather than stored, because a flag beside the list is a second thing that can disagree
    /// with it — and the loop's whole termination rule is this one question.</para></summary>
    public bool IsFinal => ToolCalls.Count == 0;

    public bool WasCutOff => Stop == StopReason.LengthCapped;

    public string Describe =>
        $"{(Text.WasCaptured ? $"{Text.Value.Length} chars" : "no text")} · "
        + $"{(CompletionTokens.WasCaptured ? $"{CompletionTokens.Value} out" : "tokens unreported")} · "
        + $"{Latency.TotalMilliseconds:0} ms · {Stop}";
}
