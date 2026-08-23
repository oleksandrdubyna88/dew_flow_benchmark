using Bench.Application.Sessions;
using Bench.Domain;
using Bench.Domain.Sessions;
using Bench.Domain.Trace;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>Agent-session traces, stored so a hook that retried is counted once and a call that never
/// finished is still a record.
/// <para>
/// The adapter ORCHESTRATES the write and decides nothing: which class a tool is, which phase a call
/// belongs to, and whether the working tree moved are all pure domain functions
/// (<see cref="ToolTaxonomy"/>, <see cref="PhaseClassifier"/>, <see cref="WorktreeReading.Against"/>)
/// called from here. That is the same division <c>PostgresTelemetryStore</c> keeps when it calls
/// <c>TelemetryCodec.Fingerprint</c>.
/// </para></summary>
/// <param name="taxonomy">Which names read and which write. Handed in rather than chosen here, so a second
/// runtime is a second value and not a branch in this class.</param>
public sealed class PostgresSessionStore(BenchDbContext db, ToolTaxonomy taxonomy) : ISessionStore
{
    /// <summary>The last line of defence on payload size. The hook client budgets at emit — that is where
    /// it belongs, because the bytes should not travel — and this exists so a client that forgets cannot
    /// put a megabyte of build output into a row nobody will ever read to the end.</summary>
    private const int MaxPayloadChars = 8000;

    public async Task<SessionIngestOutcome> RecordAsync(
        SessionEventRecord record, CancellationToken cancellationToken)
    {
        var session = await ResolveAsync(record, cancellationToken);

        var outcome = record.Kind switch
        {
            SessionEventKind.PreToolUse => await OpenAsync(session, record, cancellationToken),
            SessionEventKind.PostToolUse => await CloseAsync(session, record, cancellationToken),
            _ => new SessionIngestOutcome(session.Id, 0, false, $"{record.Kind} recorded"),
        };

        await db.SaveChangesAsync(cancellationToken);

        return outcome;
    }

    public async Task<IReadOnlyList<SessionSummary>> RecentAsync(int limit, CancellationToken cancellationToken)
    {
        var rows = await db.SessionRuns.AsNoTracking()
            .OrderByDescending(s => s.LastEventAt)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(s => new
            {
                Session = s,
                Calls = s.Calls.Count,
                Research = s.Calls.Count(c => c.Phase == SessionPhase.Research),
                Execution = s.Calls.Count(c => c.Phase == SessionPhase.Execution),
                Verification = s.Calls.Count(c => c.Phase == SessionPhase.Verification),
                Unfinished = s.Calls.Count(c => c.State == CallState.Started),
                Failures = s.Calls.Count(c => c.CompileFailure),
                // The dangerous direction of a taxonomy disagreement, and the one counter on this row that
                // is about THIS SYSTEM rather than about the agent: a call we called harmless that moved
                // tracked files. Computable in SQL, unlike the sequence detectors, which is why it is the
                // one that earns a place in a list that polls.
                Disagreements = s.Calls.Count(c =>
                    c.Mutation == MutationEvidence.Changed
                    && (c.Class == ToolClass.Read || c.Class == ToolClass.Verify)),
                // The phase of the newest call IS the session's current phase. Read here rather than
                // computed after the fact so the list stays one query however many sessions it holds.
                Phase = s.Calls.OrderByDescending(c => c.Ordinal).Select(c => c.Phase).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new SessionSummary(
            r.Session.Id,
            r.Session.Source,
            r.Session.SessionKey,
            r.Session.Runtime,
            new SessionTask(r.Session.TaskId, r.Session.TaskName, r.Session.PlanPath),
            r.Session.WorkspacePath,
            r.Session.Branch,
            r.Session.StartedAt,
            r.Session.LastEventAt,
            r.Phase,
            r.Calls,
            r.Research,
            r.Execution,
            r.Verification,
            r.Unfinished,
            r.Failures,
            r.Disagreements))];
    }

    public async Task<Outcome<SessionRun>> ByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.SessionRuns.AsNoTracking()
            .Include(s => s.Calls)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        return row is null
            ? Outcome<SessionRun>.Failure($"no session {id} in this database")
            : Outcome<SessionRun>.Success(ToDomain(row));
    }

    // ---- writing -------------------------------------------------------------------------------------

    /// <summary>The session this event belongs to, created on first sight.
    /// <para>
    /// Identity is the SOURCE and the key together. The task, workspace and branch are refreshed from
    /// every event rather than frozen at the first: a session that gains a plan link halfway through — the
    /// operator naming what they are doing once they know — must carry it, and re-stating it on every
    /// event is how a hook client with no memory of its own can supply it.
    /// </para></summary>
    private async Task<SessionRunRow> ResolveAsync(
        SessionEventRecord record, CancellationToken cancellationToken)
    {
        var existing = await db.SessionRuns
            .FirstOrDefaultAsync(
                s => s.Source == record.Source && s.SessionKey == record.SessionKey, cancellationToken);

        var session = existing ?? Create(record);

        session.LastEventAt = Later(session.LastEventAt, record.At);
        Describe(session, record);

        return session;
    }

    private SessionRunRow Create(SessionEventRecord record)
    {
        var session = new SessionRunRow
        {
            Id = Guid.CreateVersion7(),
            Source = record.Source,
            SessionKey = record.SessionKey,
            Runtime = record.Runtime,
            StartedAt = record.At,
            LastEventAt = record.At,
        };

        db.SessionRuns.Add(session);

        return session;
    }

    /// <summary>Refreshes what a human told us, and only that. An empty value never overwrites a filled
    /// one: a later event that omits the task name must not erase the name an earlier one carried.</summary>
    private static void Describe(SessionRunRow session, SessionEventRecord record)
    {
        session.TaskId = Prefer(session.TaskId, record.Task.Id);
        session.TaskName = Prefer(session.TaskName, record.Task.Name);
        session.PlanPath = Prefer(session.PlanPath, record.Task.PlanPath);
        session.WorkspacePath = Prefer(session.WorkspacePath, record.WorkspacePath);
        session.Branch = Prefer(session.Branch, record.Branch);
        session.Runtime = Prefer(session.Runtime, record.Runtime);

        if (record.Model.WasCaptured)
        {
            session.ModelCaptured = true;
            session.Model = record.Model.Value;
        }

        session.ModelReason = session.ModelCaptured ? string.Empty : record.Model.Reason;
    }

    private async Task<SessionIngestOutcome> OpenAsync(
        SessionRunRow session, SessionEventRecord record, CancellationToken cancellationToken)
    {
        var known = await db.SessionToolCalls
            .FirstOrDefaultAsync(c => c.OpenFingerprint == record.Fingerprint, cancellationToken);

        if (known is not null)
        {
            return new SessionIngestOutcome(session.Id, known.Ordinal, true, "already recorded");
        }

        var previous = await LatestAsync(session.Id, cancellationToken);
        var call = Opened(session, record, previous);

        db.SessionToolCalls.Add(call);

        return new SessionIngestOutcome(session.Id, call.Ordinal, false, "call opened");
    }

    private SessionToolCallRow Opened(
        SessionRunRow session, SessionEventRecord record, SessionToolCallRow? previous)
    {
        // At open time the tree has been read once and nothing has happened yet, so the mutation evidence
        // is genuinely NotChecked. The close event is what can compare two readings.
        var toolClass = Classify(record);

        return new SessionToolCallRow
        {
            Id = Guid.CreateVersion7(),
            SessionId = session.Id,
            Ordinal = (previous?.Ordinal ?? 0) + 1,
            OpenFingerprint = record.Fingerprint,
            ToolName = record.ToolName,
            Class = toolClass,
            Phase = PhaseClassifier.Resolve(
                toolClass, MutationEvidence.NotChecked, previous?.Phase ?? SessionPhase.Unknown),
            Target = ToolTarget.Of(record.ToolName, record.ArgumentsJson),
            ArgumentsJson = Cap(record.ArgumentsJson),
            At = record.At,
            State = CallState.Started,
            Outcome = CallOutcome.NotCaptured,
            ResponseReason = "the call has not finished",
            DurationReason = "the call has not finished",
            Mutation = MutationEvidence.NotChecked,
            DigestBefore = record.Worktree.Digest,
            DirtyBefore = record.Worktree.Dirty,
        };
    }

    /// <summary>Closes the newest still-open call of the same tool.
    /// <para>
    /// Matched by tool name and recency rather than by an id, because a hook payload carries no call id.
    /// Within one session the calls are strictly serial — an agent does not start a second tool before the
    /// first returns — so the newest open call of that name is the one this event is about.
    /// </para>
    /// <para>
    /// A close with no matching open is not dropped: it becomes a finished call of its own. That is the
    /// shape a configuration with only a post-hook produces, and refusing it would silently record nothing
    /// for an operator whose settings are merely thinner than ours.
    /// </para></summary>
    private async Task<SessionIngestOutcome> CloseAsync(
        SessionRunRow session, SessionEventRecord record, CancellationToken cancellationToken)
    {
        var known = await db.SessionToolCalls
            .FirstOrDefaultAsync(c => c.CloseFingerprint == record.Fingerprint, cancellationToken);

        if (known is not null)
        {
            return new SessionIngestOutcome(session.Id, known.Ordinal, true, "already recorded");
        }

        var open = await db.SessionToolCalls
            .Where(c => c.SessionId == session.Id
                && c.State == CallState.Started
                && c.ToolName == record.ToolName)
            .OrderByDescending(c => c.Ordinal)
            .FirstOrDefaultAsync(cancellationToken);

        var call = open ?? Adopted(session, record, await LatestAsync(session.Id, cancellationToken));

        Close(call, record, timed: open is not null);

        return new SessionIngestOutcome(session.Id, call.Ordinal, false, open is null ? "call adopted" : "call closed");
    }

    /// <summary>A finished call built from a close event that had no open.
    /// <para>
    /// <b>It carries NO before-reading, and clearing it is the whole point of this method.</b> The row is
    /// built through <see cref="Opened"/> for its shape, but <see cref="Opened"/> seeds the before-digest
    /// from the event it is handed — which here is the AFTER reading. Left in place, <see cref="Close"/>
    /// then compares that reading against itself and answers <see cref="MutationEvidence.Unchanged"/>
    /// every single time: one reading taken, and a confident claim that nothing changed across a call
    /// whose tree was never observed beforehand.
    /// </para>
    /// <para>
    /// That defect shipped and was caught by a real Claude Code session traced by this very system on
    /// 2026-08-23 — the first thing it was ever pointed at. Its consequence was visible rather than
    /// theoretical: a false <c>AllowlistCandidate</c> announcing that a command "changed nothing".
    /// <c>Time</c> already refused to invent a zero for exactly this case; mutation had never been given
    /// the same care. <c>PostgresSessionStoreTests</c> pins both halves.
    /// </para></summary>
    private SessionToolCallRow Adopted(
        SessionRunRow session, SessionEventRecord record, SessionToolCallRow? previous)
    {
        var call = Opened(session, record, previous);
        call.ResponseReason = string.Empty;
        call.DigestBefore = string.Empty;
        call.DirtyBefore = 0;
        db.SessionToolCalls.Add(call);

        return call;
    }

    /// <param name="timed">Whether an OPENING event was ever seen for this call. False for an adopted one —
    /// a close with no open — and then the duration is <b>not captured</b> rather than zero. The arithmetic
    /// would happily produce a zero here (the row was created moments ago from this very event), and a zero
    /// is the one answer that cannot be told apart from a genuinely instantaneous call.</param>
    private void Close(SessionToolCallRow call, SessionEventRecord record, bool timed)
    {
        var before = new WorktreeReading(call.DigestBefore.Length > 0, call.DigestBefore, call.DirtyBefore, string.Empty);
        var mutation = record.Worktree.Against(before);

        call.CloseFingerprint = record.Fingerprint;
        call.State = CallState.Finished;
        call.Outcome = record.Outcome;
        call.ResponseCaptured = record.Response.WasCaptured;
        call.Response = Cap(record.Response.Value);
        call.ResponseReason = record.Response.Reason;
        // The emitter's own count, NEVER the stored body's length. Deriving it here from a body that has
        // already been through two byte budgets — the hook client's at emit and Cap() below — made every
        // response longer than the budget report exactly the budget. Caught by reading a real trace where
        // two file reads both claimed precisely 8000 characters.
        call.ResponseChars = record.ResponseChars;
        Time(call, record, timed);
        call.Mutation = mutation;
        call.DigestAfter = record.Worktree.Digest;
        call.DirtyAfter = record.Worktree.Dirty;

        // The class can change at close, and only in one direction that matters: a shell command the
        // allowlist called harmless that moved the tree stays classified as it was, so the disagreement
        // remains visible. The PHASE, however, follows the ground truth — that is what paying for the
        // porcelain read buys.
        call.CompileFailure = CommandClassifier.Failed(call.Class, record.Response.WasCaptured, record.Response.Value);
        call.Phase = PhaseClassifier.Resolve(call.Class, mutation, call.Phase);
    }

    /// <summary>How long the call took, measured HERE when the emitter could not measure it.
    /// <para>
    /// A hook payload carries no duration, so the interval between the opening and the closing event is
    /// the only reading available — and it is a real one: it brackets the tool call plus this system's own
    /// two hook launches. That overhead is a known component rather than an unknown, which is what makes
    /// the number safe to use; it is not the tool's own service time and nothing here claims it is.
    /// </para>
    /// <para>
    /// A close that PREDATES its open is not clamped to zero and called measured — clocks move backwards
    /// on resumed sessions and re-synchronising machines, and a zero would enter every average as a
    /// genuinely instantaneous call.
    /// </para></summary>
    private static void Time(SessionToolCallRow call, SessionEventRecord record, bool timed)
    {
        if (record.DurationMs.WasCaptured)
        {
            call.DurationCaptured = true;
            call.DurationMs = record.DurationMs.Value;
            call.DurationReason = string.Empty;
            return;
        }

        if (!timed)
        {
            call.DurationCaptured = false;
            call.DurationMs = 0;
            call.DurationReason = "no opening event was seen for this call";
            return;
        }

        var interval = record.At - call.At;

        call.DurationCaptured = interval >= TimeSpan.Zero;
        call.DurationMs = (long)Math.Max(0, interval.TotalMilliseconds);
        call.DurationReason = call.DurationCaptured
            ? string.Empty
            : "the closing event is stamped before the opening one";
    }

    private ToolClass Classify(SessionEventRecord record) =>
        taxonomy.Classify(record.ToolName, ToolTarget.Argument(record.ArgumentsJson, "command"));

    private Task<SessionToolCallRow?> LatestAsync(Guid sessionId, CancellationToken cancellationToken) =>
        db.SessionToolCalls
            .Where(c => c.SessionId == sessionId)
            .OrderByDescending(c => c.Ordinal)
            .FirstOrDefaultAsync(cancellationToken);

    // ---- reading -------------------------------------------------------------------------------------

    /// <summary>Reading a session back. Present so no consumer reconstructs the captured/not-captured
    /// split from columns by hand — the one place that could quietly turn an unknown into a value.</summary>
    public static SessionRun ToDomain(SessionRunRow row) => new(
        row.Source,
        row.SessionKey,
        row.Runtime,
        new SessionTask(row.TaskId, row.TaskName, row.PlanPath),
        row.WorkspacePath,
        row.Branch,
        new Captured(row.ModelCaptured, row.Model, row.ModelReason),
        row.StartedAt,
        row.LastEventAt,
        [.. row.Calls.OrderBy(c => c.Ordinal).Select(ToDomain)]);

    private static SessionToolCall ToDomain(SessionToolCallRow row) => new(
        row.Ordinal,
        row.ToolName,
        row.Class,
        row.Phase,
        row.Target,
        row.ArgumentsJson,
        row.At,
        row.State,
        row.Outcome,
        new Captured(row.ResponseCaptured, row.Response, row.ResponseReason),
        row.ResponseChars,
        new CapturedCount(row.DurationCaptured, row.DurationMs, row.DurationReason),
        row.Mutation,
        row.CompileFailure);

    private static string Prefer(string existing, string arriving) =>
        arriving.Length > 0 ? arriving : existing;

    private static DateTimeOffset Later(DateTimeOffset existing, DateTimeOffset arriving) =>
        arriving > existing ? arriving : existing;

    private static string Cap(string payload) =>
        payload.Length <= MaxPayloadChars ? payload : payload[..MaxPayloadChars];
}
