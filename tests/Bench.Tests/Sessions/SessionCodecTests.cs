using Bench.Application.Sessions;
using Bench.Contracts;
using Bench.Domain.Sessions;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Sessions;

/// <summary>Reading a session event, and the three ways it can be refused.
/// <para>
/// The version check is the whole point, and it is the telemetry codec's rule one vantage point over: an
/// event stamped with a version this build does not know is refused BY NAME. Reading it as though it were
/// this one, and skipping it silently, produce the same artefact — an ingest that reports success over
/// data it did not understand.
/// </para></summary>
public sealed class SessionCodecTests
{
    [Fact]
    public void A_well_formed_event_is_read()
    {
        SessionCodec.ReadEvent(Event()).Should().BeOfType<SessionVerdict.Read>();
    }

    [Fact]
    public void An_unknown_schema_version_is_refused_by_name_and_is_retryable()
    {
        var verdict = SessionCodec.ReadEvent(Event() with { Schema = "sessions/v9" });

        verdict.Should().BeOfType<SessionVerdict.UnknownVersion>()
            .Which.Reason.Should().Contain("sessions/v9").And.Contain(SessionEventDto.Version);
    }

    [Fact]
    public void An_event_with_no_session_key_is_permanently_unreadable()
    {
        // The shape a payload this client could not parse produces. It must refuse rather than land a call
        // with no session — and the refusal is the signal that the payload reader needs work.
        SessionCodec.ReadEvent(Event() with { SessionKey = string.Empty })
            .Should().BeOfType<SessionVerdict.Unreadable>();
    }

    [Fact]
    public void An_unknown_event_name_is_refused_rather_than_guessed_into_a_tool_call()
    {
        SessionCodec.ReadEvent(Event() with { Event = "SomethingNew" })
            .Should().BeOfType<SessionVerdict.Unreadable>();
    }

    [Fact]
    public void A_malformed_line_costs_that_line_and_names_why()
    {
        SessionCodec.ReadLine("{ not json").Should().BeOfType<SessionVerdict.Unreadable>()
            .Which.Reason.Should().Contain("malformed");
    }

    [Fact]
    public void An_unrecognised_outcome_becomes_an_error_and_never_an_answer()
    {
        var read = SessionCodec.ReadEvent(Event() with { Outcome = "who knows" })
            .Should().BeOfType<SessionVerdict.Read>().Subject;

        read.Record.Outcome.Should().Be(CallOutcome.Error,
            "guessing 'it worked' for a word we do not know would invent a success rate");
    }

    [Fact]
    public void A_pre_tool_event_carries_no_outcome_and_that_is_not_a_success()
    {
        var read = SessionCodec.ReadEvent(Event() with { Outcome = "notcaptured" })
            .Should().BeOfType<SessionVerdict.Read>().Subject;

        read.Record.Outcome.Should().Be(CallOutcome.NotCaptured);
    }

    [Fact]
    public void The_fingerprint_is_stable_across_serialisation_and_differs_per_event()
    {
        var first = SessionCodec.Fingerprint(Event());
        var again = SessionCodec.Fingerprint(Event());
        var other = SessionCodec.Fingerprint(Event() with { Event = "PostToolUse" });

        again.Should().Be(first, "a client that timed out and retried must be counted once");
        other.Should().NotBe(first, "an open and a close are two events about one call, never one row");
    }

    [Fact]
    public void Two_readings_that_agree_are_no_change_and_two_that_differ_are()
    {
        var before = new WorktreeReading(true, "aaa", 2, string.Empty);

        new WorktreeReading(true, "aaa", 2, string.Empty).Against(before)
            .Should().Be(MutationEvidence.Unchanged);

        new WorktreeReading(true, "bbb", 3, string.Empty).Against(before)
            .Should().Be(MutationEvidence.Changed);
    }

    [Fact]
    public void A_reading_that_did_not_run_is_never_read_as_unchanged()
    {
        var before = new WorktreeReading(true, "aaa", 2, string.Empty);

        WorktreeReading.NotChecked("git exceeded its budget").Against(before)
            .Should().Be(MutationEvidence.NotChecked,
                "reading a gap in instrumentation as 'nothing changed' turns it into a claim about the agent");

        new WorktreeReading(true, "aaa", 2, string.Empty).Against(WorktreeReading.NotChecked("no repository"))
            .Should().Be(MutationEvidence.NotChecked);
    }

    private static SessionEventDto Event() => new(
        Schema: SessionEventDto.Version,
        At: DateTimeOffset.UnixEpoch,
        Source: "hook",
        Runtime: "claude-code",
        Event: "PreToolUse",
        SessionKey: "session-1",
        Task: new SessionTaskDto("task-1", "find a bug", "todo/PLAN_x.md"),
        WorkspacePath: "d:/rsd/dew_flow_benchmark",
        Branch: "main",
        Model: CapturedTextDto.Unavailable("a hook payload does not name the model"),
        Tool: "Read",
        ArgumentsJson: """{"file_path":"src/Foo.cs"}""",
        ArgumentsTruncatedBytes: 0,
        Response: CapturedTextDto.Unavailable("this hook event carried no tool response"),
        ResponseChars: 0,
        Outcome: "notcaptured",
        DurationMs: CapturedCountDto.Unavailable("the collector measures this between the two hooks"),
        Worktree: WorktreeDto.NotChecked("Read cannot change tracked files"));
}
