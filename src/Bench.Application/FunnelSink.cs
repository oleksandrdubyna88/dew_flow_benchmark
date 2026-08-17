using Bench.Domain;
using Bench.Domain.Trace;

namespace Bench.Application;

/// <summary>Where an engine hands over the funnel of a retrieval it just performed.
/// <para>
/// A port rather than a return value, because the funnel does not belong to the tool CALL. A subject
/// asked a question and got hits back; what happened inside the engine on the way is a fact about the
/// leg, and threading it through <see cref="ToolAnswer"/> would put an engine's internals into the
/// answer every tool returns — including the ones that do no retrieval at all.
/// </para>
/// <para>
/// It takes an <see cref="Outcome{T}"/> on purpose. An engine that CLAIMED a trace contract and then
/// sent a payload this build cannot read is a different event from an engine that never claimed one,
/// and the report has to be able to say which — otherwise a contract mismatch renders exactly like a
/// black-box engine, and the mismatch is discovered by nobody.
/// </para></summary>
public interface IFunnelSink
{
    void Retrieved(Outcome<RetrievalFunnel> funnel);
}

/// <summary>The sink of the single-shot lane, where the funnel does NOT need one.
/// <para>
/// In that lane the harness performs the retrieval itself, so the funnel comes back as part of
/// <see cref="Bench.Domain.Retrieval.RetrievedContext"/> and is persisted from there — the sink exists for
/// the tool lane, where a subject makes the call and the funnel has no return path to travel on.
/// </para>
/// <para>
/// Named and documented rather than an anonymous lambda in a container: a sink that drops funnels is
/// exactly the shape of a real defect, and the only thing that separates this from that defect is the
/// sentence above.
/// </para></summary>
public sealed class NoFunnelSink : IFunnelSink
{
    public void Retrieved(Outcome<RetrievalFunnel> funnel)
    {
    }
}
