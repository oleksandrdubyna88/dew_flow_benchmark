using Bench.Domain;
using Bench.Domain.Registry;

namespace Bench.Application;

/// <summary>One question put to a CLI agent.</summary>
/// <param name="Runtime">Which CLI this is, so the adapter can build the argv that puts THAT one in headless
/// mode. <c>claude -p</c> is not <c>codex exec</c> is not <c>gemini -p</c>, and a single flag string in
/// configuration would be a knob nobody can validate.</param>
/// <param name="Executable">Resolved on THIS machine from the registry's reference — the registry stores the
/// name of an environment variable, never a path, because its database is published unedited.</param>
/// <param name="Prompt">Sent on STDIN. A CLI agent's prompt runs to kilobytes and an argument list has a
/// platform maximum, so argv would fail on the machine with the biggest target repository.</param>
/// <param name="Wall">Every wait has a ceiling. An agent that hangs must cost one recorded refusal rather
/// than an authoring run of six groups.</param>
public sealed record AgentAsk(
    ModelRuntimeKind Runtime,
    string Executable,
    string Prompt,
    string WorkingDirectory,
    TimeSpan Wall);

/// <param name="ResponseBytes">What the agent actually printed. Recorded for the same reason a leg records
/// its response size: it is the only honest per-call measure of what a batch cost to produce.</param>
public sealed record AgentAnswer(string Text, TimeSpan Elapsed, long ResponseBytes);

/// <summary>A CLI coding agent, asked once and read once.
/// <para>
/// <b>Not <see cref="IModelRuntime"/>, and not a subject.</b> That port is a completion endpoint measured as
/// a subject; this one launches a process to do WORK FOR the harness — authoring questions, reviewing them.
/// The same executable will later be measured as a subject through a different port with turn ceilings and
/// per-leg telemetry (`todo/PLAN_tool_benchmark.md` step 11), and conflating the two would make the
/// measurement of an agent indistinguishable from the harness's own use of one.
/// </para>
/// <para>
/// Every failure is a VALUE. An executable that is not installed, a CLI that exits non-zero, an answer that
/// outlives its wall: all facts a batch records and continues past. One agent's bad afternoon must not end a
/// run over six groups of a hundred questions.
/// </para></summary>
public interface ICliAgentRuntime
{
    Task<Outcome<AgentAnswer>> AskAsync(AgentAsk ask, CancellationToken cancellationToken);
}
