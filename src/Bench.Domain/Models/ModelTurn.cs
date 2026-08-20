namespace Bench.Domain.Models;

/// <summary>
/// One tool call the model asked for, exactly as it asked for it.
///
/// <para><see cref="ArgumentsJson"/> is the RAW text the model produced, never a parsed object. Two reasons,
/// and the second is the load-bearing one: a local model emits broken JSON regularly, and a shape that could
/// only hold valid arguments would have to throw before the loop could record that it happened — which is
/// precisely the observation the tool benchmark exists to make. Malformed arguments are a fact about the
/// model, and the ledger must be able to hold one.</para>
///
/// <para><see cref="Id"/> is the runtime's own correlation token. It is echoed back on the result message
/// and is what lets a transcript with two calls in one turn be reassembled — an ordinal would work until the
/// first endpoint answered them out of order.</para>
/// </summary>
public sealed record RequestedToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>
/// One entry in a tool-calling conversation, as the harness replays it to the next turn.
///
/// <para>Closed, and only the two shapes a loop actually produces: what the assistant said (with whatever it
/// wanted to call), and what a tool answered. The user's question is not a case here — it lives on
/// <c>ModelRequest.UserPrompt</c> and is sent once, so making it a third case would create two places that
/// could disagree about what was asked.</para>
/// </summary>
public abstract record ModelTurn
{
    private ModelTurn() { }

    /// <summary>What the model produced. <paramref name="Text"/> and <paramref name="ToolCalls"/> are not
    /// exclusive: an endpoint may return prose alongside a call, and dropping either half would replay a
    /// conversation that never happened.</summary>
    public sealed record Assistant(string Text, IReadOnlyList<RequestedToolCall> ToolCalls) : ModelTurn;

    /// <summary>What a tool answered, against the call it answers.
    /// <para><paramref name="Refused"/> is carried separately from the content because "the tool said no"
    /// and "the tool returned this text" are different facts, and a ledger that files both under one flag
    /// can count neither. The model still sees the reason as content — it has to, in order to correct
    /// itself — but the harness records which it was.</para></summary>
    public sealed record ToolResult(string ToolCallId, string ToolName, string Content, bool Refused) : ModelTurn;
}
