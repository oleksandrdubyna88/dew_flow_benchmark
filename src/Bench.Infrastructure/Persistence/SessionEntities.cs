using Bench.Domain.Sessions;

namespace Bench.Infrastructure.Persistence;

/// <summary>One agent session, as one vantage point saw it.
/// <para>
/// Deliberately NOT joined to a run: almost none of these sessions are benchmark legs. Covering the
/// operator's REAL work is the whole reason this vantage point exists — the harness only ever sees its own
/// legs, and a foreign key to a run would have made an ordinary afternoon's session unstorable.
/// </para>
/// <para>
/// Keyed in practice by <see cref="Source"/> + <see cref="SessionKey"/>, never by the key alone: the same
/// session seen by a hook and by a proxy is two rows about one thing, which is the only shape in which the
/// two instruments can be COMPARED rather than silently merged.
/// </para></summary>
public sealed class SessionRunRow
{
    public Guid Id { get; set; }

    public TraceSource Source { get; set; }

    /// <summary>The runtime's own session id.</summary>
    public string SessionKey { get; set; } = string.Empty;

    public string Runtime { get; set; } = string.Empty;

    /// <summary>What a human said this session is for. Empty for a session nobody named — which is
    /// ordinary, and reads as unattributed rather than as anonymous.</summary>
    public string TaskId { get; set; } = string.Empty;

    public string TaskName { get; set; } = string.Empty;

    /// <summary>The plan document this session is working through, linked BY HAND. Never inferred: a
    /// session's subject is a claim only its operator can make, and a heuristic guessing it from the first
    /// file read would attach confident wrong labels to a corpus whose whole value is being trustworthy.</summary>
    public string PlanPath { get; set; } = string.Empty;

    public string WorkspacePath { get; set; } = string.Empty;

    public string Branch { get; set; } = string.Empty;

    /// <summary>Uncaptured for every hook-observed session, and that is the correct reading rather than a
    /// gap to fill — a hook payload does not say which model is driving.</summary>
    public bool ModelCaptured { get; set; }

    public string Model { get; set; } = string.Empty;

    public string ModelReason { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }

    /// <summary>When anything last arrived. What "live" means on the console, and what a stale session is
    /// told apart from a finished one by.</summary>
    public DateTimeOffset LastEventAt { get; set; }

    public List<SessionToolCallRow> Calls { get; set; } = [];
}

/// <summary>One tool call inside one session.
/// <para>
/// A row is INSERTED by the pre-event and UPDATED by the post-event, rather than written once when the
/// call is over. That is the durable-status shape this family already uses for anything that can outlive
/// its writer, and here it buys a specific fact: a call that opened and never closed — a tool the operator
/// denied, an agent interrupted, a session killed — survives as a record instead of as an absence.
/// </para></summary>
public sealed class SessionToolCallRow
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public int Ordinal { get; set; }

    /// <summary>SHA-256 of the canonical opening event. The idempotency key: a hook client that timed out
    /// and retried sends the same event again, and the second one must change nothing.</summary>
    public string OpenFingerprint { get; set; } = string.Empty;

    /// <summary>The same for the closing event, and empty while the call is still open. Empty is not a
    /// sentinel here — it is the literal statement that no close has arrived.</summary>
    public string CloseFingerprint { get; set; } = string.Empty;

    public string ToolName { get; set; } = string.Empty;

    /// <summary>Derived, and stored anyway — the reason <c>CallerKey</c> is: every report groups by it and
    /// a label computed in C# cannot be indexed. Every input it was derived from is stored beside it, so a
    /// better taxonomy can re-label without the sessions being measured again.</summary>
    public ToolClass Class { get; set; }

    public SessionPhase Phase { get; set; }

    /// <summary>The call's normalized subject — a file path, a search pattern, a command head. What the
    /// loop detectors compare.</summary>
    public string Target { get; set; } = string.Empty;

    public string ArgumentsJson { get; set; } = string.Empty;

    public DateTimeOffset At { get; set; }

    public CallState State { get; set; }

    public CallOutcome Outcome { get; set; }

    public bool ResponseCaptured { get; set; }

    public string Response { get; set; } = string.Empty;

    public string ResponseReason { get; set; } = string.Empty;

    /// <summary>Always exact, even when the body above was cut to its budget. A size is cheap, and a
    /// truncated size is a lie about the payload.</summary>
    public int ResponseChars { get; set; }

    public bool DurationCaptured { get; set; }

    /// <summary>The interval between the opening and the closing hook, when the emitter reported no
    /// duration of its own — which for a hook client is always. It brackets the tool call plus this
    /// system's own two process launches, and that overhead is a KNOWN component rather than a hidden one;
    /// it is not the tool's service time and nothing here reads it as such.</summary>
    public long DurationMs { get; set; }

    public string DurationReason { get; set; } = string.Empty;

    /// <summary>What the working tree said across this call, independently of what the tool was called.
    /// <see cref="MutationEvidence.NotChecked"/> is never rendered as <see cref="MutationEvidence.Unchanged"/>.</summary>
    public MutationEvidence Mutation { get; set; }

    /// <summary>Hashes of <c>git status --porcelain</c> either side of the call — never its output, which
    /// would put a developer's uncommitted file list into a database this family publishes unedited.</summary>
    public string DigestBefore { get; set; } = string.Empty;

    public string DigestAfter { get; set; } = string.Empty;

    /// <summary>How many entries the porcelain listed. Not the identity — that is the digest — but the one
    /// number that makes a change legible without it: "3 dirty paths became 4".</summary>
    public int DirtyBefore { get; set; }

    public int DirtyAfter { get; set; }

    public bool CompileFailure { get; set; }

    public SessionRunRow? Session { get; set; }
}
