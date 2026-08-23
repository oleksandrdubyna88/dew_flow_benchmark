namespace Bench.Contracts;

/// <summary>A value that may not have been obtainable, on the wire.
/// <para>
/// The family's flag-and-reason shape, spelled again here because this project depends on NOTHING — a
/// contract able to reference the domain is a contract that leaks it. "Not captured" and "empty" stay
/// different facts across the wire, which is the whole reason the pair travels instead of a bare string.
/// </para></summary>
public sealed record CapturedTextDto(bool Captured, string Value, string Reason)
{
    public static CapturedTextDto Text(string value) => new(true, value, string.Empty);

    public static CapturedTextDto Unavailable(string reason) => new(false, string.Empty, reason);
}

/// <summary>The same distinction for a count, and the sharper half of it: a zero meaning "none" and a zero
/// meaning "nobody counted" are opposite readings of one number.</summary>
public sealed record CapturedCountDto(bool Captured, long Value, string Reason)
{
    public static CapturedCountDto Number(long value) => new(true, value, string.Empty);

    public static CapturedCountDto Unavailable(string reason) => new(false, 0, reason);
}

/// <summary>What a human said this session is for. Supplied by whoever opened the terminal — never guessed
/// from the work, because a session's subject is a claim only its operator can make.</summary>
/// <param name="PlanPath">The plan document this session is working through, repository-relative
/// (<c>todo/PLAN_corpus_axis_integrity.md</c>). Empty when none was linked, which is ordinary.</param>
public sealed record SessionTaskDto(string Id, string Name, string PlanPath)
{
    public static SessionTaskDto None { get; } = new(string.Empty, string.Empty, string.Empty);
}

/// <summary>What <c>git status --porcelain</c> said at the moment of one event.
/// <para>
/// The ground truth that settles what a tool name cannot: <c>Bash</c> is both <c>git status</c> and
/// <c>rm -rf</c>. Two readings around one call — before and after — decide whether it moved the tree.
/// </para>
/// <para>
/// <paramref name="Checked"/> false is a real state and not a quiet "nothing changed": the workspace may
/// not be a repository, the tool may be one that cannot touch a tree, or git may have exceeded its budget.
/// <paramref name="Reason"/> says which.
/// </para></summary>
/// <param name="Digest">A hash of the porcelain output — never the output itself, which would put a
/// developer's whole uncommitted file list into a published corpus.</param>
public sealed record WorktreeDto(bool Checked, string Digest, int Dirty, string Reason)
{
    public static WorktreeDto NotChecked(string reason) => new(false, string.Empty, 0, reason);
}

/// <summary>One event from an agent session — the ingest body, and the spool line when the collector could
/// not be reached.
/// <para>
/// One shape for both, deliberately, and the difference from the MCP telemetry contract is the reason:
/// that emitter lives in another repository and its JSON must be able to change shape independently, while
/// this emitter is our own hook client in this repository. Two shapes here would be two things to keep in
/// step for no gain.
/// </para></summary>
/// <param name="Schema">Stamped on every event and checked on the way in. An event carrying a version this
/// build does not know is refused BY NAME rather than read as though it were this one.</param>
/// <param name="Event">One of <c>SessionStart</c>, <c>PreToolUse</c>, <c>PostToolUse</c>,
/// <c>SessionEnd</c>. A pre-event opens a call and a post-event closes it, so a call the operator DENIED
/// — pre without post — stays in the ledger as the unfinished record it is.</param>
/// <param name="Source">Which vantage point saw this: <c>hook</c>, <c>proxy</c>, <c>otel</c>,
/// <c>inprocess</c>. Never blended in a report.</param>
public sealed record SessionEventDto(
    string Schema,
    DateTimeOffset At,
    string Source,
    string Runtime,
    string Event,
    string SessionKey,
    SessionTaskDto Task,
    string WorkspacePath,
    string Branch,
    CapturedTextDto Model,
    string Tool,
    string ArgumentsJson,
    int ArgumentsTruncatedBytes,
    CapturedTextDto Response,
    int ResponseChars,
    string Outcome,
    CapturedCountDto DurationMs,
    WorktreeDto Worktree)
{
    /// <summary>The contract this build reads. Published here rather than in the domain so the hook client
    /// and the collector agree on one constant instead of two spellings of it.</summary>
    public const string Version = "sessions/v0";
}

/// <summary>The names the hook client, the installer and the editor extension must all spell the same way.
/// <para>
/// Here rather than in any one of them because all three would otherwise hold their own copy, and a
/// variable name that drifts produces an instrument that records nothing while looking installed. The
/// project every surface already references is the only place they can agree.
/// </para></summary>
public static class SessionCollector
{
    /// <summary>Where the hook posts. PINNED rather than discovered: the address is written into a
    /// <c>.claude/settings.local.json</c> inside every repository being measured, so a port that moved on
    /// restart would silently point all of them at nothing.</summary>
    public const string DefaultUrl = "http://127.0.0.1:5177";

    public const string UrlVariable = "BENCH_COLLECTOR_URL";

    public const string TaskIdVariable = "BENCH_TASK_ID";

    public const string TaskNameVariable = "BENCH_TASK_NAME";

    /// <summary>The plan document a session is working through, linked by hand when its terminal was
    /// opened. Never inferred from the work — see <see cref="SessionTaskDto.PlanPath"/>.</summary>
    public const string PlanVariable = "BENCH_PLAN_PATH";

    public const string SpoolVariable = "BENCH_SESSION_SPOOL";

    /// <summary>The route every vantage point posts to.</summary>
    public const string EventsRoute = "/api/bench/sessions/events";
}

/// <summary>What an ingest took. <paramref name="Duplicate"/> is an ordinary outcome rather than an error:
/// a hook client that retried after a timeout must be able to send the same event twice and mean it
/// once.</summary>
public sealed record SessionIngestResultDto(
    Guid SessionId, int Ordinal, bool Duplicate, int Accepted, int Refused, IReadOnlyList<string> Reasons);

/// <summary>One row of the Math tab's session list.</summary>
/// <param name="Phase">The phase the session is in RIGHT NOW — the label of its last call. A live session's
/// most useful single fact, and the reason the list is worth polling.</param>
/// <param name="Unfinished">Calls that opened and never closed: denied tools, an interrupted agent, a
/// killed session. Shown rather than dropped, because they are among the more interesting things a trace
/// holds.</param>
/// <param name="Disagreements">Calls this system classified as harmless that moved the working tree. A
/// defect in the taxonomy, surfaced by the taxonomy's own ground truth.
/// <para>
/// Every counter on this row is one a database can compute. The DETECTORS — loops, search chains,
/// allowlist candidates — are deliberately absent: they read a session's whole call sequence in order, and
/// a list endpoint that ran them on every poll would load every call of every session to render a summary.
/// They live on <see cref="SessionDetailDto"/>, where the sequence is loaded anyway.
/// </para></param>
public sealed record SessionSummaryDto(
    Guid SessionId,
    string Source,
    string SessionKey,
    string Runtime,
    SessionTaskDto Task,
    string WorkspacePath,
    string Branch,
    DateTimeOffset StartedAt,
    DateTimeOffset LastEventAt,
    string Phase,
    int Calls,
    int Research,
    int Execution,
    int Verification,
    int Unfinished,
    int CompileFailures,
    int Disagreements);

/// <param name="Class">What the tool does to the tree — <c>Read</c>, <c>Write</c>, <c>Verify</c>,
/// <c>Neutral</c>, <c>Unknown</c>. Beside the phase rather than folded into it: a neutral call belongs to
/// whichever phase surrounds it, and rendering only the phase would hide that.</param>
/// <param name="Mutation">What the working tree said — <c>NotChecked</c>, <c>Unchanged</c>,
/// <c>Changed</c>. <c>NotChecked</c> is never rendered as <c>Unchanged</c>.</param>
/// <param name="Disagrees">The dangerous direction of a disagreement: a call classified harmless that moved
/// the tree. A defect in this system's taxonomy, surfaced by its own ground truth.</param>
public sealed record SessionToolCallDto(
    int Ordinal,
    string ToolName,
    string Class,
    string Phase,
    string Target,
    string ArgumentsJson,
    DateTimeOffset At,
    string State,
    string Outcome,
    CapturedTextDto Response,
    int ResponseChars,
    CapturedCountDto DurationMs,
    string Mutation,
    bool Disagrees,
    bool CompileFailure);

/// <param name="Ordinals">The calls that make the case — a finding a reader cannot go and check is an
/// assertion.</param>
public sealed record SessionFindingDto(
    string Kind, string Subject, int Occurrences, IReadOnlyList<int> Ordinals, string Detail);

public sealed record PhaseTotalsDto(string Phase, int Calls, double Seconds);

/// <summary>One session, whole: its calls, its phase economics, and what the detectors found.</summary>
/// <param name="UnknownTools">Calls whose tool this build's taxonomy does not know — the instrument's own
/// error bar, shown beside the numbers it qualifies rather than hidden behind them.</param>
public sealed record SessionDetailDto(
    SessionSummaryDto Summary,
    IReadOnlyList<PhaseTotalsDto> Phases,
    int Switches,
    int UnknownTools,
    IReadOnlyList<SessionFindingDto> Findings,
    IReadOnlyList<SessionToolCallDto> Calls);
