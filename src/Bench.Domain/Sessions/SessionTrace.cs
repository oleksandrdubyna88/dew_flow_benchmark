using Bench.Domain.Trace;

namespace Bench.Domain.Sessions;

/// <summary>Which vantage point saw a session.
/// <para>
/// A first-class value rather than a note, for the reason the tool benchmark already states about its two
/// vantage points: observed and reconstructed calls are never blended. A hook sees every native tool call
/// and no tokens; a proxy sees tokens and reconstructs the calls a turn late; the in-process loop sees
/// everything by construction and only for the harness's own legs. Averaging across them would report a
/// number about no instrument.
/// </para></summary>
public enum TraceSource
{
    /// <summary>The agent's own hook fired and told us. Complete for tool calls, blind to tokens.</summary>
    Hook,

    /// <summary>An intercepting proxy read the wire. Sees tokens and sampling; a call's RESULT arrives one
    /// request late, so the last call of a session can be missing one.</summary>
    Proxy,

    /// <summary>The runtime exported OpenTelemetry. Whatever its events carry, and no more.</summary>
    Otel,

    /// <summary>This harness drove the loop itself. Complete by construction — the calibration source every
    /// other row is read against.</summary>
    InProcess,
}

/// <summary>What a tool DOES to the tree, before any phase is inferred from it.
/// <para>
/// Separate from <see cref="SessionPhase"/> deliberately: a class is a property of the tool (and, for a
/// shell, of the command), while a phase is a property of the MOMENT — a neutral call in the middle of an
/// edit run belongs to the edit, and collapsing the two would make every todo update a phase switch.
/// </para></summary>
public enum ToolClass
{
    /// <summary>No taxonomy entry matched. Never silently folded into <see cref="Neutral"/>: an unknown
    /// tool is a gap in the taxonomy, and a report that renders it as "harmless" hides the gap.</summary>
    Unknown,

    /// <summary>Reads state and changes nothing — Read, Grep, Glob, the rag_/graf_/rt_ read tools.</summary>
    Read,

    /// <summary>Changes the tree — Edit, Write, a shell command that is not on the read allowlist.</summary>
    Write,

    /// <summary>Builds or tests. Its own class rather than a shell command like any other, because the
    /// compile-failure count this system reports has nowhere else to come from.</summary>
    Verify,

    /// <summary>Neither, and deliberately not "unknown" — TodoWrite, AskUserQuestion, a delegation. It
    /// belongs to whatever phase surrounds it.</summary>
    Neutral,
}

/// <summary>What the agent was DOING, as a deterministic label over the call sequence.
/// <para>
/// Three, not two. The draft of this system had Research and Execution alone, which made a build after an
/// edit read as research and put the compile-failure count in the same bucket as reading a file. Worse, it
/// made the loop detector wrong by construction: a read after an edit is healthy verification, and a
/// two-phase model can only see it as a relapse.
/// </para></summary>
public enum SessionPhase
{
    Unknown,
    Research,
    Execution,
    Verification,
}

/// <summary>What the working tree said about a call, independently of what the tool was called.
/// <para>
/// The ground truth the operator asked for, and the answer to the one question a name-based taxonomy
/// cannot settle: <c>Bash</c> covers both <c>git status</c> and <c>rm -rf</c>. A digest of
/// <c>git status --porcelain</c> taken before and after the call decides it.
/// </para>
/// <para>
/// <see cref="NotChecked"/> is a real state and never a synonym for <see cref="Unchanged"/>. The check is
/// skipped for tools that cannot touch the tree, refused when the workspace is not a repository, and
/// abandoned when git takes longer than its budget — reading any of those as "nothing changed" would turn
/// a gap in instrumentation into a claim about the agent.
/// </para>
/// <para>
/// Its blind spots are known and recorded rather than papered over: a write OUTSIDE the repository and a
/// write to a path <c>.gitignore</c> excludes are both invisible here. The allowlist still stands for those,
/// which is why both readings are stored and neither overrides the other silently.
/// </para></summary>
public enum MutationEvidence
{
    NotChecked,
    Unchanged,
    Changed,
}

/// <summary>How a tool call ended, from the agent's side.
/// <para>
/// <see cref="NotCaptured"/> heads the list on purpose. Whether a hook payload carries the tool's response
/// at all is undocumented, so the honest default is "nobody told us" — and a schema whose default is
/// <c>Answered</c> would manufacture a success rate out of an unfilled field.
/// </para></summary>
public enum CallOutcome
{
    NotCaptured,
    Answered,
    Refused,
    Error,
}

/// <summary>Whether the call was ever seen to END.
/// <para>
/// A call sits <see cref="Started"/> from its pre-hook until its post-hook arrives. One left
/// <see cref="Started"/> is not a defect in the ledger — it is the record of a tool the operator DENIED, a
/// session that was killed, or an agent interrupted mid-call, and those are among the more interesting
/// things a trace can hold. Discarding unfinished rows would delete exactly that evidence.
/// </para></summary>
public enum CallState
{
    Started,
    Finished,
}

/// <summary>What a human said this session is FOR — supplied by whoever opened the terminal, never guessed.
/// <para>
/// <see cref="PlanPath"/> is the manual link to the plan document a session is working through
/// (<c>research/PLAN_corpus_axis_integrity.md</c>). It is deliberately not inferred: a session's subject is a
/// claim only the operator can make, and a heuristic that guessed it from the first file read would attach
/// confident wrong labels to a corpus whose whole purpose is to be trustworthy.
/// </para></summary>
public sealed record SessionTask(string Id, string Name, string PlanPath)
{
    public static SessionTask None => new(string.Empty, string.Empty, string.Empty);

    /// <summary>Whether a human named this session. An unnamed session is still measured — it is just
    /// unattributed, exactly as an uncorrelated telemetry record is.</summary>
    public bool IsNamed => Id.Length > 0;
}

/// <summary>One tool call inside one agent session.
/// <para>
/// <see cref="Class"/>, <see cref="Phase"/> and <see cref="CompileFailure"/> are DERIVED and stored anyway,
/// the way <c>CallerKey</c> is: every report groups by them, and a label computed in C# cannot be indexed.
/// The inputs they were derived from — the tool name, the arguments, the mutation evidence, the response —
/// are all kept beside them, so a better classifier can recompute the labels rather than needing the
/// sessions measured again.
/// </para></summary>
/// <param name="Target">The call's normalized subject — a file path, a search pattern, a command head.
/// What the loop detectors compare, which is why it is stored rather than re-parsed out of the arguments
/// at query time.</param>
/// <param name="Mutation">What the working tree said, independently of <paramref name="Class"/>. When the
/// two disagree, both are kept: see <see cref="SessionToolCall.Disagrees"/>.</param>
/// <param name="ResponseChars">How much text the tool actually returned — ALWAYS exact, even when
/// <paramref name="Response"/> itself was cut to its byte budget. A size is cheap and a truncated size is a
/// lie about the payload. It is also the input to the question this whole family exists to ask: a call that
/// returned forty thousand characters so that six lines could be edited is a step a line-window read would
/// have done for a fraction of the price.</param>
public sealed record SessionToolCall(
    int Ordinal,
    string ToolName,
    ToolClass Class,
    SessionPhase Phase,
    string Target,
    string ArgumentsJson,
    DateTimeOffset At,
    CallState State,
    CallOutcome Outcome,
    Captured Response,
    int ResponseChars,
    CapturedCount DurationMs,
    MutationEvidence Mutation,
    bool CompileFailure)
{
    /// <summary>The dangerous direction of an allowlist-versus-ground-truth disagreement: a call this
    /// system counted as harmless that actually changed the tree.
    /// <para>
    /// Only this direction is a finding. The opposite — classified as a write, tree unchanged — is ordinary:
    /// an edit that failed, a write outside the repository, a path <c>.gitignore</c> excludes. Reporting
    /// both would bury the one that matters under the one that does not.
    /// </para></summary>
    public bool Disagrees =>
        Mutation == MutationEvidence.Changed && Class is ToolClass.Read or ToolClass.Verify;
}

/// <summary>One agent session, and everything one vantage point saw of it.</summary>
/// <param name="SessionKey">The runtime's OWN session id. Unique per <see cref="Source"/> and never across
/// sources: the same session observed by a hook and by a proxy is two rows about one thing, which is the
/// only shape in which the two can be compared rather than merged.</param>
/// <param name="Model">Absent for a hook-observed session, and absent is the correct reading rather than a
/// gap to be filled — a hook payload does not say which model is driving.</param>
public sealed record SessionRun(
    TraceSource Source,
    string SessionKey,
    string Runtime,
    SessionTask Task,
    string WorkspacePath,
    string Branch,
    Captured Model,
    DateTimeOffset StartedAt,
    DateTimeOffset LastEventAt,
    IReadOnlyList<SessionToolCall> Calls)
{
    /// <summary>The contract version this domain understands. Stamped on every event and checked on the way
    /// in: an event stamped with a version this build does not know is refused BY NAME rather than read as
    /// though it were this one — the telemetry codec's rule, for the same reason.</summary>
    public const string ContractVersion = "sessions/v0";
}
