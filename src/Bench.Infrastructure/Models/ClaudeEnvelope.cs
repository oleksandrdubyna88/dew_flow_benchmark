using System.Text.Json;
using Bench.Application.Bank;
using Bench.Domain;
using Bench.Domain.Trace;

namespace Bench.Infrastructure.Models;

/// <summary>What one `claude -p --output-format json` invocation reported about itself.
/// <para>
/// Cache-read and cache-creation tokens are SUMMED into the input count, so the total reflects
/// everything billed; <see cref="CostUsd"/> is the CLI's own figure and is authoritative when present —
/// never recomputed from a price sheet that may lag the vendor's. Both disciplines are ports of the
/// shape proven in `DewFlow · src/v2/v2.Agents/Services/AiRunnerService.cs` (`TryParseClaudeJson`),
/// cited rather than copied whole per the family's cross-repository rule.
/// </para></summary>
public sealed record ClaudeReading(
    string Text,
    CapturedCount TokensIn,
    CapturedCount TokensOut,
    bool CostCaptured,
    decimal CostUsd);

/// <summary>Reading the envelope off a CLI subject's stdout.
/// <para>
/// Over <see cref="AgentJson"/>, because a CLI's stdout is an agent's answer: the envelope is usually
/// alone, but a stray banner must not defeat the parse (the lesson `CliAgentRuntime` paid for live).
/// An `is_error` envelope is a REFUSAL carrying the CLI's own words — an error stored as an answer
/// would be scored as a wrong one, and those are different facts about a subject.
/// </para></summary>
public static class ClaudeEnvelope
{
    // The envelope is snake_case (`total_cost_usd`, `cache_read_input_tokens`) — the Web default's
    // camelCase would silently read every field as absent, which is the "cost unknown" that looks free.
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static Outcome<ClaudeReading> Read(string stdout)
    {
        var payload = AgentJson.Extract(stdout, '{', '}', Recognisable);

        if (!payload.Json.Contains('{'))
        {
            return Outcome<ClaudeReading>.Failure(
                "the CLI printed no JSON envelope — was it launched without --output-format json?");
        }

        try
        {
            var wire = JsonSerializer.Deserialize<EnvelopeWire>(payload.Json, Options);

            return wire is null
                ? Outcome<ClaudeReading>.Failure("the envelope read as null")
                : ToReading(wire);
        }
        catch (JsonException error)
        {
            return Outcome<ClaudeReading>.Failure($"the envelope could not be read: {error.Message}");
        }
    }

    private static Outcome<ClaudeReading> ToReading(EnvelopeWire wire)
    {
        if (wire.IsError)
        {
            return Outcome<ClaudeReading>.Failure(
                $"the CLI reported an error envelope: {Head(wire.Result ?? wire.Subtype ?? "no detail")}");
        }

        var usage = wire.Usage;

        return Outcome<ClaudeReading>.Success(new ClaudeReading(
            (wire.Result ?? string.Empty).Trim(),
            usage is null
                ? CapturedCount.Unavailable("the envelope carried no usage")
                : CapturedCount.Number(usage.InputTokens + usage.CacheReadInputTokens + usage.CacheCreationInputTokens),
            usage is null
                ? CapturedCount.Unavailable("the envelope carried no usage")
                : CapturedCount.Number(usage.OutputTokens),
            CostCaptured: wire.TotalCostUsd > 0,
            CostUsd: wire.TotalCostUsd));
    }

    /// <summary>Recognisably THIS envelope — the scan's acceptance, so a braced fragment in a banner
    /// cannot steal the read from the real thing further down.</summary>
    private static bool Recognisable(string candidate)
    {
        try
        {
            var wire = JsonSerializer.Deserialize<EnvelopeWire>(candidate, Options);

            return wire is not null && (wire.Result is not null || wire.Usage is not null || wire.IsError);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Head(string text) => text.Length <= 200 ? text : text[..200] + "…";

    private sealed record EnvelopeWire(
        string? Result,
        string? Subtype,
        bool IsError,
        decimal TotalCostUsd,
        UsageWire? Usage);

    private sealed record UsageWire(
        int InputTokens,
        int OutputTokens,
        int CacheReadInputTokens,
        int CacheCreationInputTokens);
}
