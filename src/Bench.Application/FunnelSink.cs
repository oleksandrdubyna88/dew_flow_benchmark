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
