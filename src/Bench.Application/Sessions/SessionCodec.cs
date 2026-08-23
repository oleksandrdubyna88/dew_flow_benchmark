using System.Text.Json;
using Bench.Contracts;
using Bench.Domain;
using Bench.Domain.Sessions;
using Bench.Domain.Trace;

namespace Bench.Application.Sessions;

/// <summary>What one session event turned out to be. A closed union rather than a success-or-reason pair,
/// because the two ways of failing have OPPOSITE consequences for whoever holds the event.
/// <para>
/// A malformed event is permanent — nothing will ever read it. An unknown schema version is the opposite:
/// the event is fine and this build is simply older than the emitter, so a spool holding it must survive
/// until a build that understands it runs. Retiring that file would break the promise the refusal makes.
/// </para></summary>
public abstract record SessionVerdict
{
    private SessionVerdict() { }

    public sealed record Read(SessionEventRecord Record) : SessionVerdict;

    /// <summary>Permanent. Nothing will ever read this event.</summary>
    public sealed record Unreadable(string Reason) : SessionVerdict;

    /// <summary>Retryable. A LATER build reads this event; this one must not consume it.</summary>
    public sealed record UnknownVersion(string Version, string Reason) : SessionVerdict;
}

/// <summary>Reads the <c>sessions/v0</c> events a hook client emits, and renders a stored session back out
/// to the console.
/// <para>
/// The version check is the point, and it is the telemetry codec's rule applied one vantage point over: an
/// event stamped with a version this build does not know is refused BY NAME — never read as though it were
/// this one, and never silently skipped. Both alternatives produce the same artefact: an ingest that
/// reports success over data it did not understand.
/// </para></summary>
public static class SessionCodec
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static SessionVerdict ReadEvent(SessionEventDto? wire)
    {
        if (wire is null)
        {
            return new SessionVerdict.Unreadable("empty session event");
        }

        return wire.Schema == SessionEventDto.Version
            ? Parse(wire)
            : new SessionVerdict.UnknownVersion(
                wire.Schema,
                $"unknown session schema '{wire.Schema}' — this build reads {SessionEventDto.Version}");
    }

    /// <summary>One spool line. The hook client writes the same shape it would have posted, so a collector
    /// that was down costs a round trip and nothing else.</summary>
    public static SessionVerdict ReadLine(string line)
    {
        try
        {
            return ReadEvent(JsonSerializer.Deserialize<SessionEventDto>(line, Json));
        }
        catch (JsonException ex)
        {
            return new SessionVerdict.Unreadable($"malformed session line: {ex.Message}");
        }
    }

    /// <summary>A stable identity for one event, over its CANONICAL form rather than the raw bytes, so
    /// re-serialisation cannot invent a second identity for one call.</summary>
    public static string Fingerprint(SessionEventDto wire) => StableHash.Of(JsonSerializer.Serialize(wire, Json));

    private static SessionVerdict Parse(SessionEventDto wire)
    {
        if (wire.SessionKey.Length == 0)
        {
            return new SessionVerdict.Unreadable("session event carries no sessionKey");
        }

        var kind = Kind(wire.Event);
        var source = Source(wire.Source);

        return kind is null || source is null
            ? new SessionVerdict.Unreadable($"unknown event '{wire.Event}' from source '{wire.Source}'")
            : new SessionVerdict.Read(Record(wire, kind.Value, source.Value));
    }

    private static SessionEventRecord Record(SessionEventDto wire, SessionEventKind kind, TraceSource source) => new(
        kind,
        source,
        wire.SessionKey,
        wire.Runtime,
        new SessionTask(wire.Task.Id, wire.Task.Name, wire.Task.PlanPath),
        wire.WorkspacePath,
        wire.Branch,
        ToDomain(wire.Model),
        wire.At,
        wire.Tool,
        wire.ArgumentsJson,
        ToDomain(wire.Response),
        wire.ResponseChars,
        Outcome(wire.Outcome),
        ToDomain(wire.DurationMs),
        new WorktreeReading(wire.Worktree.Checked, wire.Worktree.Digest, wire.Worktree.Dirty, wire.Worktree.Reason),
        Fingerprint(wire));

    /// <summary>Named states, matched exactly. An unrecognised outcome becomes <see cref="CallOutcome.Error"/>
    /// rather than <see cref="CallOutcome.Answered"/>: guessing "it worked" for a word we do not know would
    /// invent a success rate.</summary>
    private static CallOutcome Outcome(string outcome) => outcome.ToLowerInvariant() switch
    {
        "answered" => CallOutcome.Answered,
        "refused" => CallOutcome.Refused,
        "" or "notcaptured" => CallOutcome.NotCaptured,
        _ => CallOutcome.Error,
    };

    /// <summary>Which moment an event name reports.
    /// <para>
    /// <b><c>PostToolUseFailure</c> closes a call exactly as <c>PostToolUse</c> does</b>, and it has to.
    /// A tool that FAILED fires the failure event and not the success one, so without this line every
    /// failed call stays open forever in the ledger — and failed calls are precisely the work a
    /// replacement map is hunting. Measured on a real session, 2026-08-23: an agent guessed a path that
    /// did not exist, the read failed, and the call sat unfinished with no duration and no response while
    /// looking like an interrupted session.
    /// </para>
    /// <para>
    /// It is the same KIND rather than a fourth one because the store's job is identical — find the open
    /// call and settle it. What differs is the outcome, and the emitter already says so.
    /// </para></summary>
    private static SessionEventKind? Kind(string name) => name switch
    {
        "SessionStart" => SessionEventKind.SessionStart,
        "PreToolUse" => SessionEventKind.PreToolUse,
        "PostToolUse" or "PostToolUseFailure" => SessionEventKind.PostToolUse,
        "SessionEnd" => SessionEventKind.SessionEnd,
        _ => null,
    };

    private static TraceSource? Source(string name) => name.ToLowerInvariant() switch
    {
        "hook" => TraceSource.Hook,
        "proxy" => TraceSource.Proxy,
        "otel" => TraceSource.Otel,
        "inprocess" => TraceSource.InProcess,
        _ => null,
    };

    private static Captured ToDomain(CapturedTextDto value) => new(value.Captured, value.Value, value.Reason);

    private static CapturedCount ToDomain(CapturedCountDto value) => new(value.Captured, value.Value, value.Reason);
}
