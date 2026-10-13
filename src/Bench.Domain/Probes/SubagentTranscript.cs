using Bench.Domain.Trace;

namespace Bench.Domain.Probes;

/// <summary>Token counts of one message, one child or one session, in the four billed kinds — the cache write split by its TTL,
/// because a 1-hour write and a 5-minute write are priced differently.</summary>
public sealed record TokenUsage(long Input, long Output, long CacheRead, long CacheWrite5m, long CacheWrite1h)
{
    public static TokenUsage Zero { get; } = new(0, 0, 0, 0, 0);

    public long CacheWrite => CacheWrite5m + CacheWrite1h;

    public TokenUsage Plus(TokenUsage other) => new(
        Input + other.Input, Output + other.Output, CacheRead + other.CacheRead, CacheWrite5m + other.CacheWrite5m, CacheWrite1h + other.CacheWrite1h);
}

/// <summary>An event's <c>timestamp</c>, or the fact that it carried none — 34 of 55 events carried one in the 2026-10-12 probe, and
/// "no timestamp" must not read as the epoch.</summary>
public sealed record StreamMoment(bool WasCaptured, DateTimeOffset Value)
{
    public static StreamMoment None { get; } = new(false, default);

    public static StreamMoment At(DateTimeOffset value) => new(true, value);
}

/// <summary>What became of one child tool call. A denial (a <c>permission_denied</c> event) and an error result (<c>is_error</c>) are
/// both STOPPED, and kept apart; a result that is neither RETURNED; a call with no result at all is not captured — never safe.</summary>
public enum ChildToolFate
{
    NotCaptured,
    Denied,
    Errored,
    Returned,
}

/// <summary>One tool call a child made: its name, its input as the CLI printed it, its fate, and the result's TEXT — the path audit
/// needs it (a Glob result listing an outside path is a stop), so unlike <see cref="ClaudeStream"/> this reader keeps it.</summary>
public sealed record ChildToolCall(string Id, string Name, string Input, ChildToolFate Fate, string ResultText);

/// <summary>Everything a child (one Agent call's subagent) did, read from the events whose <c>parent_tool_use_id</c> is the call's id:
/// every <c>message.model</c> it answered with, its usage summed over its messages DEDUPLICATED by message id (the last event of an id
/// wins — usage repeats across a message's content blocks), its tool calls, and its last text.</summary>
public sealed record ChildTrace(IReadOnlyList<string> MessageModels, TokenUsage StreamUsage, IReadOnlyList<ChildToolCall> Tools, string LastText)
{
    public static ChildTrace Nothing { get; } = new([], TokenUsage.Zero, [], string.Empty);
}

/// <summary>The Agent call's result as the main session received it: the <c>tool_result</c> event's timestamp and <c>is_error</c>, and
/// its <c>tool_use_result</c> — status, <c>resolvedModel</c>, <c>totalDurationMs</c>, the FINAL iteration's usage, the child's report text.
/// A failed Agent call carries a string there, not an object: every field below is then absent, never invented.</summary>
public sealed record AgentResult(
    bool Captured,
    bool IsError,
    StreamMoment At,
    string Status,
    string ResolvedModel,
    CapturedCount TotalDurationMs,
    TokenUsage FinalIteration,
    string FinalText)
{
    public static AgentResult NotCaptured { get; } = new(false, false, StreamMoment.None, string.Empty, string.Empty, CapturedCount.Unavailable("no Agent result"), TokenUsage.Zero, string.Empty);
}

/// <summary>One Agent call of the main session: its input (agent type, model alias, prompt and the prompt's SHA-256), when it was made,
/// its result, and its child.</summary>
public sealed record SubagentCall(
    string ToolUseId,
    string SubagentType,
    string InputModel,
    string Prompt,
    string PromptSha256,
    StreamMoment CalledAt,
    AgentResult Result,
    ChildTrace Child);

/// <summary>One tool call of the MAIN session, by name — the read-back voids a cell whose main session called anything but Agent.</summary>
public sealed record MainToolCall(string Id, string Name);

/// <summary>One model's line of the envelope's <c>modelUsage</c> — exact per model, and the only exact source of OUTPUT tokens: the
/// stream's assistant events carry the usage as of the message's start, so their <c>output_tokens</c> is a placeholder for any message
/// that called a tool (2026-10-12: a Sonnet child streamed 16 + 22 output tokens and was billed 118).</summary>
public sealed record ModelUsageEntry(string Model, long Input, long Output, long CacheRead, long CacheCreation, decimal CostUsd);

/// <summary>The envelope's <c>subagent_stats</c> — absent on a stream with no envelope, and then not a zero.</summary>
public sealed record SubagentStats(bool Captured, long Spawned, long Completed, long Failed, long RefusedBudget)
{
    public static SubagentStats NotCaptured { get; } = new(false, 0, 0, 0, 0);
}

/// <summary>The final <c>result</c> envelope: <c>duration_ms</c>, <c>total_cost_usd</c>, the MAIN session's own <c>usage</c> (its split
/// cache writes included), <c>modelUsage</c> and <c>subagent_stats</c>.</summary>
public sealed record ResultEnvelope(
    bool Captured,
    bool IsError,
    string Subtype,
    CapturedCount DurationMs,
    CapturedUsd TotalCostUsd,
    TokenUsage MainUsage,
    IReadOnlyList<ModelUsageEntry> ModelUsage,
    SubagentStats Stats)
{
    public static ResultEnvelope NotCaptured { get; } = new(
        false, false, string.Empty, CapturedCount.Unavailable("no result envelope"), CapturedUsd.Unavailable("no result envelope"), TokenUsage.Zero, [], SubagentStats.NotCaptured);
}

/// <summary>A scout cell's stream, read: what <see cref="ClaudeSubagents.Read"/> returns. Missing pieces are values — an incomplete
/// stream, an absent result, a child with no resolved model — and never an exception.</summary>
public sealed record SubagentTranscript(
    bool Complete,
    string MainModel,
    string CliVersion,
    string WorkingDirectory,
    IReadOnlyList<MainToolCall> MainCalls,
    TokenUsage MainStreamUsage,
    IReadOnlyList<SubagentCall> Agents,
    ResultEnvelope Envelope)
{
    /// <summary>Team time (§3.6): the first Agent <c>tool_use</c> event's timestamp to the last Agent <c>tool_result</c> event's, in ms.
    /// Not captured when there is no Agent call or any of those events carried no timestamp.</summary>
    public CapturedCount TeamMs =>
        Agents.Count > 0 && Agents.All(a => a.CalledAt.WasCaptured && a.Result.At.WasCaptured)
            ? CapturedCount.Number((long)(Agents.Max(a => a.Result.At.Value) - Agents.Min(a => a.CalledAt.Value)).TotalMilliseconds)
            : CapturedCount.Unavailable(Agents.Count == 0 ? "no Agent call" : "an Agent call or its result carried no timestamp");
}
