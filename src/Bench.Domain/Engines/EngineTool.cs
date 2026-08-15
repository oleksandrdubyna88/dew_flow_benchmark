namespace Bench.Domain.Engines;

/// <summary>One tool an engine offers the subject, as the subject sees it.
/// <para>
/// The description is part of the MEASURED artefact, not documentation around it. The same four tools
/// behind a differently-worded surface scored 4/63 against 37/63 on identical tasks — from the shape of
/// the surface alone — which is why a lane's tool surface is part of the measurement key rather than a
/// detail of the adapter.
/// </para></summary>
public sealed record EngineTool(string Name, string Description, string ArgumentsSchema);

/// <summary>What a tool call came back with. Three states, and the middle one is the point.
/// <para>
/// <see cref="Refused"/> is a tool that understood the request and said no — a path outside the
/// checkout, a missing argument. <see cref="Failed"/> is a tool that tried and could not. A ledger
/// that files both under one flag cannot count either, and that is precisely how a read-only guarantee
/// was asserted for months upstream and was false: the ledger recorded a result's LENGTH.
/// </para>
/// <para>Deliberately the same shape as the tool surface's own contract in <c>dew_flow_mcp</c>, so a
/// call means the same thing on both sides of the wire.</para></summary>
public abstract record ToolAnswer
{
    private ToolAnswer() { }

    public sealed record Ok(string Content) : ToolAnswer;

    public sealed record Refused(string Reason) : ToolAnswer;

    public sealed record Failed(string Message) : ToolAnswer;

    public static ToolAnswer Success(string content) => new Ok(content);

    public static ToolAnswer Refusal(string reason) => new Refused(reason);

    public static ToolAnswer Failure(string message) => new Failed(message);

    public TOut Match<TOut>(Func<string, TOut> ok, Func<string, TOut> refused, Func<string, TOut> failed) =>
        this switch
        {
            Ok o => ok(o.Content),
            Refused r => refused(r.Reason),
            Failed f => failed(f.Message),
            _ => throw new InvalidOperationException("unreachable"),
        };

    /// <summary>The text this answer carries, whichever case it is.</summary>
    public string Text => Match(ok => ok, refused => refused, failed => failed);

    /// <summary>Whether the tool declined. Recorded per call, because "it was denied" and "it returned
    /// nothing" are different facts about a run.</summary>
    public bool WasRefused => this is Refused;
}
