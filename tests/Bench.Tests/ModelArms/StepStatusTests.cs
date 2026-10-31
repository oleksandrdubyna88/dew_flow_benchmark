using Bench.Domain.Trace;
using FluentAssertions;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>Where one step of one arm stands, decided over the files <c>status</c> read (E3.S4): the first row that holds, in the order the story's table gives. Nothing here reads a file — the decision is a function over values, so every row is a row of a theory.
/// <para>
/// What this does NOT prove: that the verb reads those values from the right files (<c>StatusVerbTests</c> does, by name, over a real root), or what a real runner's files hold — the markers and lines are written by the module's own writers (<c>DoneMarkers.Write</c>,
/// <c>TimingLines.Write</c>), the same ones the runner uses.
/// </para></summary>
public sealed class StepStatusTests
{
    private const string Stem = "2-review";

    private static readonly DateTimeOffset At = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private static Read<DoneMarker> Done(string state, int attempts = 1) =>
        new Read<DoneMarker>.Present(new DoneMarker(attempts, CapturedCount.Number(0), "out/2-review.a1.jsonl", state, false));

    private static Read<DoneMarker> NoDone => new Read<DoneMarker>.Absent();

    private static Read<ContinueMarker> Continue(int after, string why = "every provider rate-limited") => new Read<ContinueMarker>.Present(new ContinueMarker(after, why));

    private static Read<ContinueMarker> NoContinue => new Read<ContinueMarker>.Absent();

    private static TimingLine End(string step, int attempt, string state, bool limited = false) =>
        new TimingLine.End("C1", step, attempt, At, CapturedCount.Number(60), CapturedCount.Number(0), !limited, limited, "abc1234", state, false);

    private static TimingLine Stopped(string step, int exit, string why) => new TimingLine.Stopped("C1", step, exit, why, At);

    private static StepEvidence Evidence(Read<DoneMarker>? done = null, Read<ContinueMarker>? marker = null, int streams = 0, params TimingLine[] timing) =>
        new(Stem, done ?? NoDone, marker ?? NoContinue, streams, timing);

    public static TheoryData<string, StepEvidence, StepState> Rows => new()
    {
        { "a refused done marker is unreadable, whatever else is there", Evidence(new Read<DoneMarker>.Refused("2-review.done is a link"), Continue(1), 1, End(Stem, 1, "finished")), new StepState.Unreadable("2-review.done is a link") },
        { "a refused continue marker is unreadable too", Evidence(Done("finished"), new Read<ContinueMarker>.Refused("2-review.continue is bigger than 4096 bytes"), 1), new StepState.Unreadable("2-review.continue is bigger than 4096 bytes") },
        { "a continue marker for the last attempt is continuing", Evidence(NoDone, Continue(1), 1, End(Stem, 1, "finished")), new StepState.Continuing(1, "every provider rate-limited") },
        { "and it outranks a done marker: the gate's recorded intent is to go on", Evidence(Done("finished"), Continue(2), 2), new StepState.Continuing(2, "every provider rate-limited") },
        { "a stale continue marker (an older attempt) decides nothing: no end line for attempt 2 is an interruption", Evidence(NoDone, Continue(1), 2, End(Stem, 1, "finished")), new StepState.Resumable("interrupted (no end line)", 2) },
        { "a stale continue marker beside a done marker leaves the step done", Evidence(Done("finished", 2), Continue(1), 2), new StepState.Done("finished", 2) },
        { "a done marker that says finished", Evidence(Done("finished", 3), NoContinue, 3), new StepState.Done("finished", 3) },
        { "a done marker that says hard-deadline", Evidence(Done("hard-deadline", 1), NoContinue, 1), new StepState.Done("hard-deadline", 1) },
        { "a shell's done marker carries no end word", Evidence(Done(string.Empty, 1), NoContinue, 1), new StepState.Done("not recorded", 1) },
        { "a last end line that is limited, with no stop after it", Evidence(NoDone, NoContinue, 2, End(Stem, 1, "errored"), End(Stem, 2, "limited", limited: true)), new StepState.Limited(2, string.Empty) },
        { "a last end line that is limited, with a later stopped line of the step: its why", Evidence(NoDone, NoContinue, 2, End(Stem, 2, "limited", limited: true), Stopped(Stem, 3, "the account is out")), new StepState.Limited(2, "the account is out") },
        { "a stopped line of ANOTHER step is not this step's why", Evidence(NoDone, NoContinue, 1, End(Stem, 1, "limited", limited: true), Stopped("1-code", 3, "not mine")), new StepState.Limited(1, string.Empty) },
        { "a stopped line BEFORE the end line is not a later one", Evidence(NoDone, NoContinue, 1, Stopped(Stem, 3, "an earlier stop"), End(Stem, 1, "limited", limited: true)), new StepState.Limited(1, string.Empty) },
        { "a last end line in any other state is resumable with that state", Evidence(NoDone, NoContinue, 1, End(Stem, 1, "errored")), new StepState.Resumable("errored", 1) },
        { "the LAST end line of the step decides, not the first", Evidence(NoDone, NoContinue, 2, End(Stem, 1, "limited", limited: true), End(Stem, 2, "interrupted")), new StepState.Resumable("interrupted", 2) },
        { "another step's end lines are not this step's", Evidence(NoDone, NoContinue, 1, End("1-code", 1, "limited", limited: true)), new StepState.Resumable("interrupted (no end line)", 1) },
        { "a stream with no end line is an interruption at that attempt", Evidence(NoDone, NoContinue, 1), new StepState.Resumable("interrupted (no end line)", 1) },
        { "an attempt that began after the last end line has no end of its own", Evidence(NoDone, NoContinue, 3, End(Stem, 2, "limited", limited: true)), new StepState.Resumable("interrupted (no end line)", 3) },
        { "no stream and no line is not started", Evidence(), new StepState.NotStarted() },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void Each_row_of_the_decision_table_gives_its_state(string row, StepEvidence evidence, StepState expected)
    {
        StepStatuses.Of(evidence).Should().Be(expected, row);
    }

    [Fact]
    public void The_rows_cover_every_state_of_the_closed_set()
    {
        var seen = Rows.Select(row => row.Data.Item3.GetType()).Distinct().ToList();

        var states = typeof(StepState).GetNestedTypes().Where(type => type.IsSubclassOf(typeof(StepState))).ToList();
        states.Should().NotBeEmpty("a scan that matches nothing passes forever");
        seen.Should().BeEquivalentTo(states, "a state the table never produces is a state the theory never proved");
    }

    private static string Long => new('x', 300);

    public static TheoryData<StepState, string> Lines => new()
    {
        { new StepState.Done("finished", 1), "2-review: done (finished)" },
        { new StepState.Done("hard-deadline", 1), "2-review: done — ended on its deadline" },
        { new StepState.Done("not recorded", 1), "2-review: done (not recorded)" },
        { new StepState.Continuing(1, "every provider rate-limited"), "2-review: continuing — blocked after attempt 1: every provider rate-limited" },
        { new StepState.Continuing(2, Long), "2-review: continuing — blocked after attempt 2: " + new string('x', 120) + "…" },
        { new StepState.Continuing(2, new string('y', 120)), "2-review: continuing — blocked after attempt 2: " + new string('y', 120) },
        { new StepState.Continuing(1, "two\nlines\0here"), "2-review: continuing — blocked after attempt 1: two\\u000Alines\\0here" },
        { new StepState.Resumable("errored", 2), "2-review: resumable — errored at attempt 2" },
        { new StepState.Limited(2, "the account is out"), "2-review: limited at attempt 2 — the account is out" },
        { new StepState.Limited(2, string.Empty), "2-review: limited at attempt 2" },
        { new StepState.NotStarted(), "2-review: not started" },
        { new StepState.Unreadable("2-review.done is a link — what it names is not read (D17)"), "2-review: unreadable — 2-review.done is a link — what it names is not read (D17)" },
    };

    [Theory]
    [MemberData(nameof(Lines))]
    public void Each_state_has_one_line_that_names_it(StepState state, string line)
    {
        StepStatuses.Line(Stem, state, text => text).Should().Be(line);
    }

    [Fact]
    public void A_why_cut_at_one_hundred_and_twenty_characters_never_splits_a_surrogate_pair()
    {
        var why = new string('a', 119) + "\U0001F600" + "tail";

        var line = StepStatuses.Line(Stem, new StepState.Continuing(1, why), text => text);

        line.Should().EndWith(new string('a', 119) + "…", "the pair starts at the 120th character, so the cut falls before it, never in the middle");
        line.All(c => !char.IsSurrogate(c)).Should().BeTrue("a lone surrogate would print as a replacement character");
    }

    [Theory]
    [InlineData(105)]
    [InlineData(110)]
    [InlineData(117)]
    public void A_token_inside_a_why_is_scrubbed_before_the_cut_so_no_part_of_it_is_printed(int before)
    {
        const string token = "0123456789abcdef";
        var map = ArmMap.Extend(ArmMap.Empty, ["C1"], () => Convert.FromHexString(token));
        var why = new string('x', before) + $"/r/{token}/arm/gate.json is a link";

        var continuing = StepStatuses.Line(Stem, new StepState.Continuing(1, why), map.Scrub);
        var limited = StepStatuses.Line(Stem, new StepState.Limited(1, why), map.Scrub);

        foreach (var line in new[] { continuing, limited })
        {
            for (var length = 4; length <= token.Length; length++)
            {
                line.Should().NotContain(token[..length], $"a cut inside {token} would leave its first {length} characters, and Scrub replaces only a whole token");
            }
        }

        if (before <= 110)
        {
            continuing.Should().Contain("/r/<token>", "the positive case: a token that fits before the cut is shown, scrubbed");
        }
    }
}
