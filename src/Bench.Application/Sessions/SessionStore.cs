using Bench.Domain;
using Bench.Domain.Sessions;
using Bench.Domain.Trace;

namespace Bench.Application.Sessions;

/// <summary>Which moment of a session an event reports.
/// <para>
/// A pre-event OPENS a call and a post-event CLOSES it, rather than one event carrying both. That is not
/// bookkeeping: it is the only shape in which a call that never finished — a tool the operator denied, an
/// agent interrupted, a killed session — survives as a record instead of as an absence.
/// </para></summary>
public enum SessionEventKind
{
    SessionStart,
    PreToolUse,
    PostToolUse,
    SessionEnd,
}

/// <summary>One reading of <c>git status --porcelain</c>.
/// <para>
/// The digest is a HASH of the porcelain output and never the output itself. A benchmark database is
/// published unedited, and the list of a developer's uncommitted files is not ours to publish.
/// </para></summary>
public sealed record WorktreeReading(bool Checked, string Digest, int Dirty, string Reason)
{
    public static WorktreeReading NotChecked(string reason) => new(false, string.Empty, 0, reason);

    /// <summary>What this reading says when compared with the one taken before the call. Two readings that
    /// both ran and disagree are a change; two that both ran and agree are not; anything else was not
    /// measured, and <see cref="MutationEvidence.NotChecked"/> is the honest answer rather than the
    /// convenient one.</summary>
    public MutationEvidence Against(WorktreeReading before) =>
        Checked && before.Checked
            ? Compare(before.Digest, Digest)
            : MutationEvidence.NotChecked;

    private static MutationEvidence Compare(string before, string after) =>
        string.Equals(before, after, StringComparison.Ordinal)
            ? MutationEvidence.Unchanged
            : MutationEvidence.Changed;
}

/// <summary>One event, parsed and validated, on its way to the store.</summary>
/// <param name="Fingerprint">A stable identity for this event, computed over its canonical form. The
/// idempotency key: a hook client that timed out and retried must be able to send the same event twice and
/// have it counted once.</param>
public sealed record SessionEventRecord(
    SessionEventKind Kind,
    TraceSource Source,
    string SessionKey,
    string Runtime,
    SessionTask Task,
    string WorkspacePath,
    string Branch,
    Captured Model,
    DateTimeOffset At,
    string ToolName,
    string ArgumentsJson,
    Captured Response,
    /// <summary>How much the tool ACTUALLY returned, as the emitter counted it before applying its own
    /// byte budget. Carried separately from <paramref name="Response"/> for the reason the schema states
    /// and this record at first failed to honour: a size is cheap, and a size derived from an
    /// already-clipped body is a lie about the payload — every response longer than the budget would
    /// report exactly the budget, which reads as a suspicious coincidence and is in fact a lost
    /// measurement.</summary>
    int ResponseChars,
    CallOutcome Outcome,
    CapturedCount DurationMs,
    WorktreeReading Worktree,
    string Fingerprint);

/// <param name="Ordinal">Where this event landed in its session, or <c>0</c> for an event that opened no
/// call. Returned so the hook client's own log can be joined to the ledger without a second query.</param>
public readonly record struct SessionIngestOutcome(Guid SessionId, int Ordinal, bool Duplicate, string Detail);

/// <summary>One session as a list row: identity, and the counters a reader scans for.</summary>
/// <param name="Phase">The phase of the LAST call — what the session is doing right now. The single most
/// useful fact about a live session, and the reason the list is worth polling at all.</param>
public sealed record SessionSummary(
    Guid Id,
    TraceSource Source,
    string SessionKey,
    string Runtime,
    SessionTask Task,
    string WorkspacePath,
    string Branch,
    DateTimeOffset StartedAt,
    DateTimeOffset LastEventAt,
    SessionPhase Phase,
    int Calls,
    int Research,
    int Execution,
    int Verification,
    int Unfinished,
    int CompileFailures,
    int Disagreements);

/// <summary>Where agent-session traces land.
/// <para>
/// Separate from <see cref="ITelemetryStore"/> because it is a THIRD vantage point, not a second table of
/// the second one: telemetry is what one MCP server saw of its own tools, and this is what an agent's own
/// runtime saw of every tool it called — native ones included, which no server can see. The tool
/// benchmark's rule that vantage points are never averaged together applies, and
/// <see cref="SessionRun.Source"/> is why it can be enforced.
/// </para></summary>
public interface ISessionStore
{
    /// <summary>Records one event, opening or closing a call as its kind requires. Idempotent by
    /// fingerprint: re-sending an event changes nothing and says so.</summary>
    Task<SessionIngestOutcome> RecordAsync(SessionEventRecord record, CancellationToken cancellationToken);

    /// <summary>The sessions, most recently active first — the list the console polls.</summary>
    Task<IReadOnlyList<SessionSummary>> RecentAsync(int limit, CancellationToken cancellationToken);

    /// <summary>One session with every call it holds.
    /// <para>
    /// A refusal rather than an empty session when the id is unknown: "this database has no such session"
    /// and "this session did nothing" are opposite pieces of news, and a caller handed an empty trace for
    /// the first would go looking in the wrong place.
    /// </para></summary>
    Task<Outcome<SessionRun>> ByIdAsync(Guid id, CancellationToken cancellationToken);
}
