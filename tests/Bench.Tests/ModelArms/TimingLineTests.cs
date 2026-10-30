using Bench.Domain;
using Bench.Domain.Trace;
using FluentAssertions;
using ModelArms.Core.Steps;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The formats E2.S2 writes and reads back: the timing lines in the shell's key order, the <c>.done</c> marker, the gate's <c>.continue</c> marker, the deadline text, the resume
/// notice and the transcript path (plan C6, D5, D6, D7; E2 §8 items 32, 41, 44). The expected literals are written out by hand from the shell (<c>run-arm.sh</c> of the part-6
/// experiment, <c>coai-exp2/run-arm.sh:98-108</c>), so a key renamed in the code cannot quietly agree with itself.
/// <para>
/// What this does NOT prove: that <c>metrics.mjs</c> reads a line this build wrote (E5's compatibility test reads the files through the pinned consumer), that claude puts a transcript at
/// the path <c>SessionTranscripts.PathOf</c> spells when <c>CLAUDE_CONFIG_DIR</c> is set (read, not measured), or that the shell's line a recorded arm wrote is the only shape
/// in the wild — the previous generations' lines are read tolerantly, but only the arm-C file and the 2026-10-28 pair are fixtures.
/// </para></summary>
public sealed class TimingLineTests
{
    private const string Token = "0123456789ab";

    private static readonly DateTimeOffset At = new(2026, 10, 28, 21, 32, 38, TimeSpan.Zero);

    private static readonly DateTimeOffset Later = new(2026, 10, 28, 21, 47, 26, TimeSpan.Zero);

    private static readonly Guid Session = Guid.Parse("a03b4849-62f8-4edd-8042-7fccf7f379ef");

    private static TimingLine.Start Started =>
        new("C1", "2-review", 1, "fresh", Session.ToString("D"), "opus", At, 2400, "22:12 UTC (23:12 +01:00)", "df9634e9", "/e/r/" + Token + "/out/2-review.a1.jsonl");

    private static TimingLine.End Ended =>
        new("C1", "2-review", 1, Later, CapturedCount.Number(888), CapturedCount.Number(1), true, true, "df9634e9", "limited", false);

    private static IReadOnlyList<TimingLine> One(string line) => TimingLines.Read(line);

    public static TheoryData<TimingLine, string> Rows => new()
    {
        {
            Started,
            "{\"kind\":\"start\",\"arm\":\"C1\",\"step\":\"2-review\",\"attempt\":1,\"how\":\"fresh\",\"session\":\"a03b4849-62f8-4edd-8042-7fccf7f379ef\",\"model\":\"opus\","
            + "\"start_utc\":\"2026-10-28T21:32:38Z\",\"budget_s\":2400,\"deadline\":\"22:12 UTC (23:12 +01:00)\",\"head\":\"df9634e9\",\"stream\":\"/e/r/" + Token + "/out/2-review.a1.jsonl\"}"
        },
        {
            Ended,
            "{\"kind\":\"end\",\"arm\":\"C1\",\"step\":\"2-review\",\"attempt\":1,\"end_utc\":\"2026-10-28T21:47:26Z\",\"wall_s\":888,\"exit\":1,\"result\":true,\"limited\":true,\"head\":\"df9634e9\",\"end\":\"limited\"}"
        },
        {
            Ended with { Exit = CapturedCount.Unavailable(TimingLines.NotRecorded), WallSeconds = CapturedCount.Unavailable(TimingLines.NotRecorded), Result = false, Limited = false, State = "hard-deadline", Recovered = true },
            "{\"kind\":\"end\",\"arm\":\"C1\",\"step\":\"2-review\",\"attempt\":1,\"end_utc\":\"2026-10-28T21:47:26Z\",\"wall_s\":null,\"exit\":null,\"result\":false,\"limited\":false,\"head\":\"df9634e9\","
            + "\"end\":\"hard-deadline\",\"recovered\":true}"
        },
        {
            Ended with { Exit = CapturedCount.Number(0), Limited = false, State = "finished", Missing = ["CODE_NOTES"] },
            "{\"kind\":\"end\",\"arm\":\"C1\",\"step\":\"2-review\",\"attempt\":1,\"end_utc\":\"2026-10-28T21:47:26Z\",\"wall_s\":888,\"exit\":0,\"result\":true,\"limited\":false,\"head\":\"df9634e9\","
            + "\"end\":\"finished\",\"missing\":[\"CODE_NOTES\"]}"
        },
        {
            Ended with { Exit = CapturedCount.Number(0), Limited = false, State = "finished", Recovered = true, Missing = ["CODE_NOTES", "HFIX_NOTES"] },
            "{\"kind\":\"end\",\"arm\":\"C1\",\"step\":\"2-review\",\"attempt\":1,\"end_utc\":\"2026-10-28T21:47:26Z\",\"wall_s\":888,\"exit\":0,\"result\":true,\"limited\":false,\"head\":\"df9634e9\","
            + "\"end\":\"finished\",\"recovered\":true,\"missing\":[\"CODE_NOTES\",\"HFIX_NOTES\"]}"
        },
        {
            Ended with { Exit = CapturedCount.Number(0), Limited = false, State = "finished", Missing = [] },
            "{\"kind\":\"end\",\"arm\":\"C1\",\"step\":\"2-review\",\"attempt\":1,\"end_utc\":\"2026-10-28T21:47:26Z\",\"wall_s\":888,\"exit\":0,\"result\":true,\"limited\":false,\"head\":\"df9634e9\",\"end\":\"finished\"}"
        },
        {
            new TimingLine.GateWait("C1", "4-gate", 1, 1, "every provider rate-limited", At),
            "{\"kind\":\"gate-wait\",\"arm\":\"C1\",\"step\":\"4-gate\",\"after\":1,\"wait\":1,\"why\":\"every provider rate-limited\",\"at_utc\":\"2026-10-28T21:32:38Z\"}"
        },
        { new TimingLine.ArmDone("C1", Later), "{\"kind\":\"arm-done\",\"arm\":\"C1\",\"done_utc\":\"2026-10-28T21:47:26Z\"}" },
        {
            new TimingLine.Stopped("C1", "2-review", 5, "ended in an error result: \"x\" — run me again", Later),
            "{\"kind\":\"stopped\",\"arm\":\"C1\",\"step\":\"2-review\",\"exit\":5,\"why\":\"ended in an error result: \\\"x\\\" — run me again\",\"at_utc\":\"2026-10-28T21:47:26Z\"}"
        },
        { new TimingLine.Other("{\"kind\":\"from-the-future\",\"x\":1}"), "{\"kind\":\"from-the-future\",\"x\":1}" },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void Every_line_kind_round_trips_in_the_shells_key_order(TimingLine line, string literal)
    {
        TimingLines.Write(line).Should().Be(literal, "the keys are the shell's, in the shell's order, compact and with no newline");

        One(literal).Should().ContainSingle().Which.Should().Be(line, "a line this build wrote reads back as the value that wrote it");
    }

    [Fact]
    public void A_gate_wait_line_round_trips_and_the_budget_ignores_it()
    {
        var wait = new TimingLine.GateWait("C1", "4-gate", 1, 1, "every provider rate-limited", At);
        const string literal = "{\"kind\":\"gate-wait\",\"arm\":\"C1\",\"step\":\"4-gate\",\"after\":1,\"wait\":1,\"why\":\"every provider rate-limited\",\"at_utc\":\"2026-10-28T21:32:38Z\"}";

        TimingLines.Write(wait).Should().Be(literal);
        One(literal).Should().ContainSingle().Which.Should().Be(wait);

        var timing = new TimingLine[]
        {
            Started with { Step = "4-gate" },
            new TimingLine.End("C1", "4-gate", 1, Later, CapturedCount.Number(600), CapturedCount.Number(0), true, false, "df9634e9", "finished", false),
            wait,
            wait with { After = 2, Wait = 2 },
        };
        StepBudget.UsedSeconds(timing, "4-gate").Should().Be(600, "a gate-wait line spends no budget");

        One("{\"kind\":\"gate-wait\",\"arm\":\"C1\",\"step\":\"4-gate\",\"after\":1,\"wait\":1,\"at_utc\":\"2026-10-28T21:32:38Z\"}")
            .Should().ContainSingle().Which.Should().BeOfType<TimingLine.GateWait>().Which.Why.Should().BeEmpty("an absent why reads as empty text");
        One("{\"kind\":\"gate-wait\",\"arm\":\"C1\",\"step\":\"4-gate\",\"after\":1,\"wait\":\"1\",\"why\":\"x\",\"at_utc\":\"2026-10-28T21:32:38Z\"}")
            .Should().ContainSingle().Which.Should().BeOfType<TimingLine.Other>("a wait number that is text is not a wait number — the line is kept, never counted");
    }

    /// <summary>The two lines are the part-6 experiment's recorded 2026-10-28 attempt of C1's <c>2-review</c> (<c>coai-exp2/r/&lt;token&gt;/out/timing.jsonl</c>), with the arm's token replaced by
    /// <c>0123456789ab</c>: the attempt a 429 ended, recorded <c>"limited":false</c> by the day's detector, no <c>end</c> key.</summary>
    [Fact]
    public void A_shell_end_line_reads_without_an_end_state()
    {
        var start = "{\"kind\":\"start\",\"arm\":\"C1\",\"step\":\"2-review\",\"attempt\":1,\"how\":\"fresh\",\"session\":\"a03b4849-62f8-4edd-8042-7fccf7f379ef\",\"model\":\"opus\",\"start_utc\":\"2026-10-28T21:32:38Z\","
            + "\"budget_s\":2400,\"deadline\":\"22:12 UTC (23:12 CET)\",\"head\":\"df9634e9\",\"stream\":\"/home/jinx/git/coai-exp2/r/" + Token + "/out/2-review.a1.jsonl\"}";
        var end = "{\"kind\":\"end\",\"arm\":\"C1\",\"step\":\"2-review\",\"attempt\":1,\"end_utc\":\"2026-10-28T21:47:26Z\",\"wall_s\":888,\"exit\":1,\"result\":true,\"limited\":false,\"head\":\"df9634e9\"}";

        var lines = TimingLines.Read(start + "\n" + end + "\n");

        lines.Should().HaveCount(2);
        lines[0].Should().BeOfType<TimingLine.Start>().Which.Deadline.Should().Be("22:12 UTC (23:12 CET)", "a deadline is read as text and never re-spelled");
        var read = lines[1].Should().BeOfType<TimingLine.End>().Subject;
        read.Limited.Should().BeFalse("as recorded");
        read.State.Should().BeEmpty("the shell's line carries no end state");
        read.Recovered.Should().BeFalse();
        read.Result.Should().BeTrue();
        read.WallSeconds.Should().Be(CapturedCount.Number(888));
        read.Exit.Should().Be(CapturedCount.Number(1));
        read.Missing.Should().BeEmpty();
    }

    [Fact]
    public void A_line_that_is_not_a_json_object_or_has_no_known_kind_is_kept_as_other()
    {
        string[] raws = ["not json at all", "[1,2]", "{\"kind\":\"end\"}", "{\"kind\":42}", "{\"step\":\"x\"}", "{\"kind\":\"start\",\"arm\":\"C1\",\"step\":\"x\",\"attempt\":\"1\"}"];

        var lines = TimingLines.Read(string.Join("\r\n\r\n", raws) + "\n\n");

        lines.Should().HaveCount(raws.Length, "a blank line is not a line, and every other one is kept");
        lines.Should().AllBeOfType<TimingLine.Other>();
        lines.Cast<TimingLine.Other>().Select(other => other.Raw).Should().Equal(raws, "kept byte for byte, a trailing carriage return dropped");
    }

    [Fact]
    public void A_lone_surrogate_in_a_reason_is_written_as_a_replacement_character_and_a_line_holding_one_is_kept_never_thrown_on()
    {
        var stopped = new TimingLine.Stopped("C1", "2-review", 5, "cut \ud800 here", Later);

        var written = TimingLines.Write(stopped);

        written.Should().Contain("cut \uFFFD here", "a lone surrogate is written as the character U+FFFD, not left to the serializer's escape — the line stays text a reader can grep");
        One(written).Should().ContainSingle().Which.Should().Be(stopped with { Why = "cut \uFFFD here" });
        One("{\"kind\":\"stopped\",\"arm\":\"C1\",\"step\":\"x\",\"exit\":5,\"why\":\"a\\ud800b\",\"at_utc\":\"2026-10-28T21:47:26Z\"}")
            .Should().ContainSingle().Which.Should().BeOfType<TimingLine.Other>("a line that carries a lone surrogate escape is kept as it is, never a throw");
    }

    [Fact]
    public void A_done_marker_round_trips_and_the_shells_marker_reads()
    {
        var marker = new DoneMarker(2, CapturedCount.Number(0), "/e/out/2-review.a2.jsonl", "finished", false);
        const string literal = "{\"attempts\":2,\"exit\":0,\"stream\":\"/e/out/2-review.a2.jsonl\",\"end\":\"finished\"}";

        DoneMarkers.Write(marker).Should().Be(literal, "the shell's three keys first, then end");
        DoneMarkers.Read(literal).Ok().Should().Be(marker);

        var killed = new DoneMarker(1, CapturedCount.Unavailable(TimingLines.NotRecorded), "/e/out/2-review.a1.jsonl", "hard-deadline", true);
        const string killedLiteral = "{\"attempts\":1,\"exit\":null,\"stream\":\"/e/out/2-review.a1.jsonl\",\"end\":\"hard-deadline\",\"recovered\":true}";
        DoneMarkers.Write(killed).Should().Be(killedLiteral);
        DoneMarkers.Read(killedLiteral).Ok().Should().Be(killed);

        var shells = DoneMarkers.Read("{\"attempts\":2,\"exit\":0,\"stream\":\"/e/r/" + Token + "/out/2-review.a2.jsonl\"}").Ok();
        shells.State.Should().BeEmpty("the shell's marker carries no end");
        shells.Stream.Should().EndWith("2-review.a2.jsonl");
        shells.Exit.Should().Be(CapturedCount.Number(0));

        var previous = DoneMarkers.Read("{\"attempts\":\"recovered\",\"stream\":\"/e/out/1-code.a1.jsonl\"}").Ok();
        previous.Recovered.Should().BeTrue("the previous generation's recovered marker is the string 'recovered' where a count goes");
        previous.Exit.WasCaptured.Should().BeFalse("it carries no exit, and absent is not zero");

        DoneMarkers.Read("[]").Reason().Should().Contain("object", "a marker that is not an object is refused, naming what it is");
        DoneMarkers.Read("{\"attempts\":\"two\"}").Reason().Should().Contain("attempts");
        DoneMarkers.Read("not json").Failed().Should().BeTrue("a torn file is an answer, not a throw");
    }

    [Fact]
    public void A_continue_marker_round_trips_and_a_malformed_one_is_refused()
    {
        var marker = new ContinueMarker(2, "gate blocked: x");

        ContinueMarkers.Write(marker).Should().Be("{\"after\":2,\"why\":\"gate blocked: x\"}");
        ContinueMarkers.Read(ContinueMarkers.Write(marker)).Ok().Should().Be(marker);
        ContinueMarkers.Read("{\"after\":2}").Ok().Why.Should().BeEmpty("a marker without a reason still orders the continuation");

        ContinueMarkers.Read("[]").Reason().Should().Contain("object");
        ContinueMarkers.Read("{\"after\":\"2\"}").Reason().Should().Contain("after");
        ContinueMarkers.Read("{\"after\":0}").Reason().Should().Contain("after");
        ContinueMarkers.Read("{\"after\":1.5}").Reason().Should().Contain("after");
        ContinueMarkers.Read("{\"why\":\"x\"}").Reason().Should().Contain("after");
        ContinueMarkers.Read("{\"after\":2,\"why\":7}").Reason().Should().Contain("why");
        ContinueMarkers.Read("{\"after\":2").Failed().Should().BeTrue("a torn file is an answer, not a throw");
    }

    public static TheoryData<string, int, string, string> Deadlines => new()
    {
        { "2026-10-28T21:32:38Z", 2400, "Europe/Paris", "22:12 UTC (23:12 +01:00)" },
        { "2026-07-01T10:00:00Z", 600, "Europe/Paris", "10:10 UTC (12:10 +02:00)" },
        { "2026-10-28T21:32:38Z", 2400, "America/New_York", "22:12 UTC (18:12 -04:00)" },
        { "2026-10-28T21:32:38Z", 2400, "UTC", "22:12 UTC (22:12 +00:00)" },
        { "2026-10-28T23:50:00Z", 1200, "Europe/Paris", "00:10 UTC (01:10 +01:00)" },
        { "2026-10-25T00:30:00Z", 1800, "Europe/Paris", "01:00 UTC (02:00 +01:00)" },
    };

    /// <summary>The last row starts under summer time and ends under winter time (the clocks went back at 01:00 UTC on 2026-10-25): the offset is the zone's at the DEADLINE instant, which is the one the
    /// reader of the brief will see on their clock then.</summary>
    [Theory]
    [MemberData(nameof(Deadlines))]
    public void The_deadline_is_spelled_in_utc_and_as_the_local_offset(string start, int remaining, string zone, string expected)
    {
        var local = zone == "UTC" ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(zone);

        StepBudget.Deadline(DateTimeOffset.Parse(start, System.Globalization.CultureInfo.InvariantCulture), remaining, local).Should().Be(expected);
    }

    [Fact]
    public void The_resume_notice_is_the_shells_text_with_the_base_and_the_deadline()
    {
        const string baseSha = "6786582e1c0a4b3f9d2e5a7b8c1d0e3f4a5b6c7d";

        ResumeNotice.For(baseSha, "09:10 UTC (10:10 +01:00)").Should().Be(
            "This session was interrupted (a usage limit, the machine or the harness stopped it) and is now resumed. Nothing you did\n"
            + "before is lost unless it was not saved: check `git status` and `git log --oneline 6786582e1c0a4b3f9d2e5a7b8c1d0e3f4a5b6c7d..HEAD` first, then continue the\n"
            + "same task from where you stopped. Your new deadline, for the time that is left of your budget, is: **09:10 UTC (10:10 +01:00)**. The rest of\n"
            + "your brief is unchanged.\n");
    }

    [Fact]
    public void The_transcript_path_is_the_working_directory_slug_under_the_claude_home()
    {
        var id = Guid.Parse("1e283c28-3a83-4a3a-a478-279ade916c86");

        SessionTranscripts.PathOf("/h/.claude", "/home/op/git/coai_exp/r/abc/arm", id).Should().Be(
            Path.Combine("/h/.claude", "projects", "-home-op-git-coai-exp-r-abc-arm", "1e283c28-3a83-4a3a-a478-279ade916c86.jsonl"));

        SessionTranscripts.ClaudeHome(new Dictionary<string, string> { ["HOME"] = "/h" }).Should().Be(Path.Combine("/h", ".claude"));
        SessionTranscripts.ClaudeHome(new Dictionary<string, string> { ["HOME"] = "/h", ["CLAUDE_CONFIG_DIR"] = "/c" }).Should().Be("/c", "claude's own relocation of ~/.claude wins");
        SessionTranscripts.ClaudeHome(new Dictionary<string, string> { ["HOME"] = "/h", ["CLAUDE_CONFIG_DIR"] = string.Empty }).Should().Be(Path.Combine("/h", ".claude"), "an empty value is no relocation");
        SessionTranscripts.ClaudeHome(new Dictionary<string, string>()).Should().BeEmpty("with neither, there is no home to name");
        SessionTranscripts.ClaudeHome(new Dictionary<string, string> { ["HOME"] = string.Empty }).Should().BeEmpty();
    }

    public static TheoryData<string, string> LongPaths => new()
    {
        { "/" + new string('a', 199), "-" + new string('a', 199) },
        { "/" + new string('a', 200), "-" + new string('a', 199) + "-b6ymvl" },
        { "/home/" + string.Concat(Enumerable.Repeat("dir.a_b/", 30)), "-home-" + string.Concat(Enumerable.Repeat("dir-a-b-", 24)) + "di-ked71z" },
        { "/" + string.Concat(Enumerable.Repeat("\U0001F600", 101)), new string('-', 200) + "-csymf6" },
    };

    /// <summary>claude's rule for a long working directory, READ from the claude 2.1.310 binary (its function <c>FH</c>), not measured with a real session: the slug is the path with every character outside
    /// <c>[a-zA-Z0-9]</c> replaced by <c>-</c>; when that is longer than 200 it is cut to its first 200 and <c>-</c> plus <c>Math.abs(h).toString(36)</c> is appended, <c>h</c> being the 31-multiplier hash of the ORIGINAL
    /// path over its UTF-16 units (<c>h = (h &lt;&lt; 5) - h + charCodeAt | 0</c>). The expected values were computed with node from the same JS:
    /// <c>node -e 'const h=t=&gt;{let e=0;for(let n=0;n&lt;t.length;n++)e=(e&lt;&lt;5)-e+t.charCodeAt(n)|0;return e};const FH=e=&gt;{const n=e.replace(/[^a-zA-Z0-9]/g,"-");return n.length&lt;=200?n:`${n.slice(0,200)}-${Math.abs(h(e)).toString(36)}`};console.log(FH(process.argv[1]))' "&lt;the path&gt;"</c>.
    /// Rows: 200 characters (kept whole), 201 (cut and hashed), a mixed path that is hashed from its ORIGINAL text, and 101 astral characters (203 UTF-16 units, so 202 dashes before the cut).</summary>
    [Theory]
    [MemberData(nameof(LongPaths))]
    public void A_working_directory_longer_than_200_characters_is_cut_and_hashed_as_claude_does(string cwd, string slug)
    {
        var id = Guid.Parse("d62b4ad4-8535-46c6-8289-1ccf2c45322d");

        SessionTranscripts.PathOf("/h/.claude", cwd, id).Should().Be(Path.Combine("/h/.claude", "projects", slug, "d62b4ad4-8535-46c6-8289-1ccf2c45322d.jsonl"));
    }

    [Fact]
    public void Without_a_claude_home_there_is_no_transcript_path_and_never_a_relative_one()
    {
        var id = Guid.NewGuid();

        SessionTranscripts.PathOf(string.Empty, "/home/op/arm", id).Should().BeEmpty(
            "with neither HOME nor CLAUDE_CONFIG_DIR there is nothing to name; claude may fall back to the passwd home (assumed, not measured), so a run that cannot say starts fresh — the safe side");
        SessionTranscripts.PathOf(SessionTranscripts.ClaudeHome(new Dictionary<string, string>()), "/home/op/arm", id).Should().BeEmpty();
    }

    /// <summary>The part-5 fill steps (<c>2a-fill</c>, <c>2b-fill</c> of the Q arms, 16 recorded start lines) wrote <c>"how":"script"</c> and no <c>budget_s</c>, <c>session</c> or <c>deadline</c>: no claude ran, so no budget, session or
    /// deadline exists. They are a NAMED exception: this reader keeps them as <c>Other</c> with the raw bytes, and an analysis must not count a fill script's start through <c>Read</c>. Two of the recorded lines, byte for byte.</summary>
    [Fact]
    public void The_part_5_fill_script_start_lines_are_kept_as_other_with_their_bytes()
    {
        string[] recorded =
        [
            "{\"kind\":\"start\",\"arm\":\"Q1\",\"step\":\"2a-fill\",\"attempt\":1,\"how\":\"script\",\"model\":\"qwen (vLLM)\",\"start_utc\":\"2026-10-26T09:59:26Z\",\"head\":\"8a683844\",\"stream\":\"\"}",
            "{\"kind\":\"start\",\"arm\":\"Q1\",\"step\":\"2b-fill\",\"attempt\":1,\"how\":\"script\",\"model\":\"qwen (vLLM)\",\"start_utc\":\"2026-10-26T10:01:55Z\",\"head\":\"ee34604c\",\"stream\":\"\"}",
        ];

        var lines = TimingLines.Read(string.Join("\n", recorded) + "\n");

        lines.Should().AllBeOfType<TimingLine.Other>("a fill script's start has no budget_s, so it is not a claude attempt's start");
        lines.Cast<TimingLine.Other>().Select(other => other.Raw).Should().Equal(recorded);
    }

    /// <summary>The short-path slug rule, measured on this host: this session's own working directory <c>/home/jinx/git/dew_flow_conventions</c> is the folder
    /// <c>~/.claude/projects/-home-jinx-git-dew-flow-conventions</c>. The comparison with the file the fake CLI leaves for <c>--session-id g</c> (E2 §8 item 50) is
    /// <c>FakeCliByCallTests.A_launch_with_a_session_id_leaves_the_transcript_claude_would_when_asked</c>, which asserts <c>PathOf(&lt;home&gt;/.claude, &lt;the cwd handed to RunAsync&gt;, g)</c> names it.</summary>
    [Fact]
    public void The_transcript_path_replaces_every_character_outside_letters_and_digits_with_a_dash()
    {
        var id = Guid.Parse("d62b4ad4-8535-46c6-8289-1ccf2c45322d");

        SessionTranscripts.PathOf("/h/.claude", "/home/jinx/git/dew_flow_conventions", id).Should().Be(
            Path.Combine("/h/.claude", "projects", "-home-jinx-git-dew-flow-conventions", "d62b4ad4-8535-46c6-8289-1ccf2c45322d.jsonl"));
        SessionTranscripts.PathOf("/h/.claude", "/tmp/testsToDelete-x_1/arm.d ~y", id).Should().Be(
            Path.Combine("/h/.claude", "projects", "-tmp-testsToDelete-x-1-arm-d--y", "d62b4ad4-8535-46c6-8289-1ccf2c45322d.jsonl"),
            "a dot, an underscore, a space and a tilde are each one dash; letters, digits and an existing dash are kept");
    }
}
