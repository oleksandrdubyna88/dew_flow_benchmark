using Bench.Application.Sessions;
using Bench.Contracts;
using Bench.Domain;
using Bench.Domain.Sessions;
using Bench.Domain.Trace;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Sessions;

/// <summary>The session store against a real Postgres — the pairing of a call's open and close, the
/// idempotency the hook client's retries depend on, and the one defect a measured session found.
/// </summary>
[Collection("postgres")]
public sealed class PostgresSessionStoreTests(PostgresFixture postgres)
{
    /// <summary>Found by a real Claude Code session traced by this very system, 2026-08-23 — the first
    /// thing it was ever pointed at, and it came back with a defect in its own recorder.
    /// <para>
    /// A close event with no matching open is ADOPTED as a finished call, which is the shape a runtime
    /// configured with only a post-hook produces. The adoption built the row through <c>Opened</c> using
    /// the CLOSE event, which seeded <c>DigestBefore</c> from the after-reading — so <c>Close</c> then
    /// compared that reading against itself and got <see cref="MutationEvidence.Unchanged"/> every time.
    /// </para>
    /// <para>
    /// One reading was ever taken. "The tree did not change across this call" is a claim the instrument
    /// never measured, and it is exactly the anti-pattern the <see cref="MutationEvidence"/> doc forbids:
    /// reading a gap in instrumentation as a fact about the agent. Duration already refused to invent a
    /// zero for this same case; mutation had simply never been given the same care.
    /// </para></summary>
    [Fact]
    public async Task A_close_with_no_open_never_claims_the_tree_was_unchanged()
    {
        await using var db = postgres.NewContext();
        var store = new PostgresSessionStore(db, ToolTaxonomy.ClaudeCode);
        var key = $"adopted-{Guid.CreateVersion7()}";

        // No PreToolUse: straight to the close, carrying a worktree reading of its own.
        var recorded = await store.RecordAsync(Close(key, "gh pr list", "abc", 2), TestContext.Current.CancellationToken);

        var run = await Read(store, recorded.SessionId);

        run.Calls.Should().ContainSingle();
        run.Calls[0].Mutation.Should().Be(MutationEvidence.NotChecked,
            "only one reading was ever taken, so no change could have been measured — and Unchanged would "
            + "be a claim about the agent made out of a gap in instrumentation");
    }

    /// <summary>The consequence, asserted separately because it is the part a reader would actually see:
    /// a fabricated <c>Unchanged</c> makes the allowlist detector announce a finding nobody measured.</summary>
    [Fact]
    public async Task An_adopted_call_does_not_manufacture_an_allowlist_candidate()
    {
        await using var db = postgres.NewContext();
        var store = new PostgresSessionStore(db, ToolTaxonomy.ClaudeCode);
        var key = $"adopted-finding-{Guid.CreateVersion7()}";

        var recorded = await store.RecordAsync(Close(key, "gh pr list", "abc", 2), TestContext.Current.CancellationToken);
        var run = await Read(store, recorded.SessionId);

        SessionAnalysis.Candidates(run.Calls, ToolTaxonomy.ClaudeCode).Should().BeEmpty(
            "'gh pr list' was counted as a write and the tree was never observed before it ran — "
            + "'it changed nothing' is not something this trace knows");
    }

    [Fact]
    public async Task A_pre_and_a_post_become_one_call_whose_tree_reading_is_a_real_comparison()
    {
        await using var db = postgres.NewContext();
        var store = new PostgresSessionStore(db, ToolTaxonomy.ClaudeCode);
        var key = $"paired-{Guid.CreateVersion7()}";

        await store.RecordAsync(Open(key, "rm -rf out", "before-digest", 1), TestContext.Current.CancellationToken);
        var closed = await store.RecordAsync(Close(key, "rm -rf out", "after-digest", 4), TestContext.Current.CancellationToken);

        var run = await Read(store, closed.SessionId);

        run.Calls.Should().ContainSingle("an open and a close are two events about ONE call");
        run.Calls[0].State.Should().Be(CallState.Finished);
        run.Calls[0].Mutation.Should().Be(MutationEvidence.Changed);
        run.Calls[0].Phase.Should().Be(SessionPhase.Execution);
    }

    /// <summary>Raised by a traced session, 2026-08-23, as a risk rather than an observation — and it was
    /// right. An agent BATCHES tool calls, so two <c>Read</c>s run at once and the hooks arrive
    /// <c>Pre(A) · Pre(B) · Post(B) · Post(A)</c>. Matching a close to "the newest open call of this tool"
    /// then attaches each response, size, duration and digest to the OTHER file.
    /// <para>
    /// Nothing errors and nothing looks wrong: both rows are finished, both have plausible numbers, and the
    /// target of each is the file it was opened with. It is exactly the shape this system exists to catch —
    /// a measurement that is confidently about the wrong thing — and it would have silently poisoned the
    /// window-waste signal, whose whole content is "this size, against that target".
    /// </para></summary>
    [Fact]
    public async Task Two_calls_of_one_tool_in_flight_close_onto_their_own_rows()
    {
        await using var db = postgres.NewContext();
        var store = new PostgresSessionStore(db, ToolTaxonomy.ClaudeCode);
        var key = $"parallel-{Guid.CreateVersion7()}";

        await store.RecordAsync(Read(key, SessionEventKind.PreToolUse, "src/A.cs", 0), TestContext.Current.CancellationToken);
        await store.RecordAsync(Read(key, SessionEventKind.PreToolUse, "src/B.cs", 0), TestContext.Current.CancellationToken);

        // A finishes FIRST — the order the reporting session named, and the one "newest open call of this
        // tool" gets wrong. (B-then-A happens to come out right by accident, which is why the accident had
        // to be excluded from this test rather than relied on.)
        await store.RecordAsync(Read(key, SessionEventKind.PostToolUse, "src/A.cs", 111), TestContext.Current.CancellationToken);
        var last = await store.RecordAsync(Read(key, SessionEventKind.PostToolUse, "src/B.cs", 222), TestContext.Current.CancellationToken);

        var run = await Read(store, last.SessionId);
        var a = run.Calls.Single(c => c.Target == "src/A.cs");
        var b = run.Calls.Single(c => c.Target == "src/B.cs");

        run.Calls.Should().HaveCount(2, "two opens and two closes are two calls, not four");
        a.ResponseChars.Should().Be(111, "A's answer belongs to A");
        b.ResponseChars.Should().Be(222, "B's answer belongs to B");
    }

    [Fact]
    public async Task A_call_that_never_closed_stays_in_the_ledger_as_unfinished()
    {
        await using var db = postgres.NewContext();
        var store = new PostgresSessionStore(db, ToolTaxonomy.ClaudeCode);
        var key = $"denied-{Guid.CreateVersion7()}";

        var opened = await store.RecordAsync(Open(key, "rm -rf out", "d", 1), TestContext.Current.CancellationToken);
        var run = await Read(store, opened.SessionId);

        run.Calls[0].State.Should().Be(CallState.Started,
            "a tool the operator DENIED is among the more interesting things a trace can hold; dropping "
            + "unfinished rows would delete exactly that evidence");
        run.Calls[0].DurationMs.WasCaptured.Should().BeFalse();
    }

    [Fact]
    public async Task Re_sending_the_same_event_is_counted_once()
    {
        await using var db = postgres.NewContext();
        var store = new PostgresSessionStore(db, ToolTaxonomy.ClaudeCode);
        var key = $"retry-{Guid.CreateVersion7()}";
        var wire = Open(key, "dotnet build x.slnx", "d", 0);

        var first = await store.RecordAsync(wire, TestContext.Current.CancellationToken);
        var again = await store.RecordAsync(wire, TestContext.Current.CancellationToken);

        again.Duplicate.Should().BeTrue("a hook client that timed out and retried must be counted once");
        again.Ordinal.Should().Be(first.Ordinal);

        var run = await Read(store, first.SessionId);
        run.Calls.Should().ContainSingle();
    }

    /// <summary>Caught by reading a real trace, 2026-08-23: two file reads both claimed precisely 8000
    /// characters, which is this store's payload cap. The size was being derived from the STORED body
    /// rather than taken from the emitter's own count, so every response longer than the budget reported
    /// exactly the budget — the "a truncated size is a lie about the payload" rule, broken by the one line
    /// that was supposed to honour it.</summary>
    [Fact]
    public async Task A_response_longer_than_the_budget_still_reports_its_true_size()
    {
        await using var db = postgres.NewContext();
        var store = new PostgresSessionStore(db, ToolTaxonomy.ClaudeCode);
        var key = $"big-{Guid.CreateVersion7()}";

        await store.RecordAsync(Open(key, "cat huge.txt", "d", 0), TestContext.Current.CancellationToken);
        var closed = await store.RecordAsync(
            Close(key, "cat huge.txt", "d", 0) with
            {
                // What the emitter clipped to, and what it counted before clipping.
                Response = Captured.Text(new string('x', 8000)),
                ResponseChars = 41_237,
            },
            TestContext.Current.CancellationToken);

        var run = await Read(store, closed.SessionId);

        run.Calls[0].ResponseChars.Should().Be(41_237,
            "a call that returned forty thousand characters is the whole signal behind 'this step did not "
            + "need a model' — collapsed to the cap, it is indistinguishable from every other big read");
        run.Calls[0].Response.Value.Length.Should().Be(8000, "the BODY is still budgeted");
    }

    [Fact]
    public async Task An_adopted_call_reports_no_duration_rather_than_a_zero()
    {
        await using var db = postgres.NewContext();
        var store = new PostgresSessionStore(db, ToolTaxonomy.ClaudeCode);
        var key = $"adopted-time-{Guid.CreateVersion7()}";

        var recorded = await store.RecordAsync(Close(key, "ls", "abc", 0), TestContext.Current.CancellationToken);
        var run = await Read(store, recorded.SessionId);

        run.Calls[0].DurationMs.WasCaptured.Should().BeFalse(
            "the arithmetic would happily produce a zero here, and a zero cannot be told apart from a "
            + "genuinely instantaneous call");
    }

    // ---- scaffolding ---------------------------------------------------------------------------------

    private static async Task<SessionRun> Read(PostgresSessionStore store, Guid id)
    {
        var found = await store.ByIdAsync(id, TestContext.Current.CancellationToken);

        return found.Match(run => run, reason => throw new Xunit.Sdk.XunitException(reason));
    }

    /// <summary>A <c>Read</c> event naming one file — the shape an agent's batched reads take.</summary>
    private static SessionEventRecord Read(string key, SessionEventKind kind, string path, int chars)
    {
        var record = Record(key, kind, "irrelevant", "d", 0, chars > 0 ? CallOutcome.Answered : CallOutcome.NotCaptured);

        return record with
        {
            ToolName = "Read",
            ArgumentsJson = $$"""{"file_path":"{{path}}"}""",
            ResponseChars = chars,
            Worktree = WorktreeReading.NotChecked("Read cannot change tracked files"),
            // Each event needs its own identity, or the second open is absorbed as a retry of the first.
            Fingerprint = $"{key}-{kind}-{path}",
        };
    }

    private static SessionEventRecord Open(string key, string command, string digest, int dirty) =>
        Record(key, SessionEventKind.PreToolUse, command, digest, dirty, CallOutcome.NotCaptured);

    private static SessionEventRecord Close(string key, string command, string digest, int dirty) =>
        Record(key, SessionEventKind.PostToolUse, command, digest, dirty, CallOutcome.Answered);

    private static SessionEventRecord Record(
        string key,
        SessionEventKind kind,
        string command,
        string digest,
        int dirty,
        CallOutcome outcome)
    {
        var wire = new SessionEventDto(
            SessionEventDto.Version,
            DateTimeOffset.UnixEpoch.AddSeconds(kind == SessionEventKind.PreToolUse ? 10 : 12),
            "hook",
            "claude-code",
            kind.ToString(),
            key,
            new SessionTaskDto("t", "a traced task", "todo/ai_math/PLAN_math_over_ai.md"),
            "d:/rsd/dew_flow_benchmark",
            "main",
            CapturedTextDto.Unavailable("a hook payload does not name the model"),
            "Bash",
            $$"""{"command":"{{command}}"}""",
            0,
            outcome == CallOutcome.NotCaptured
                ? CapturedTextDto.Unavailable("this hook event carried no tool response")
                : CapturedTextDto.Text("ok"),
            2,
            outcome == CallOutcome.NotCaptured ? "notcaptured" : "answered",
            CapturedCountDto.Unavailable("the collector measures this between the two hooks"),
            new WorktreeDto(true, digest, dirty, string.Empty));

        return SessionCodec.ReadEvent(wire) switch
        {
            SessionVerdict.Read read => read.Record,
            var other => throw new Xunit.Sdk.XunitException($"the fixture built an unreadable event: {other}"),
        };
    }
}
