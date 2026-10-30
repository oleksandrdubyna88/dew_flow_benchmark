using Bench.Domain;
using FluentAssertions;
using ModelArms.Core.Launch;
using ModelArms.Core.Steps;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>What the gate step reads of its own step's files: <c>notes/gate.json</c> (a status word and a reason, <c>run-arm.sh:153</c>, <c>:156</c>), whether another wait is allowed (<c>:154</c>), and how many blocked
/// rounds the arm has already waited out, counted from the durable <c>gate-wait</c> lines of <c>timing.jsonl</c> (E2 §8 item 44) — never from a local that a restart forgets. The file is a model's answer: whatever it holds
/// reads as a status word of the gate brief, as <c>missing</c> (no file, or an object that says no status) or as UNJUDGEABLE (text that is there and is no status — E2 §8 item 60), and nothing throws.
/// <para>
/// What this does NOT prove: what a real gate model writes into <c>gate.json</c> (the status words are the gate brief's six, matched exactly), or that the file is read at the right
/// moment — that is the gate step's (E2.S7).
/// </para></summary>
public sealed class GateStatusTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);

    private static GateReading Of(string text) => GateStatus.Read(Outcome<string>.Success(text));

    [Fact]
    public void A_blocked_status_and_its_reason_are_read()
    {
        var blocked = Of("""{"status":"blocked","why":"every provider rate-limited"}""");

        blocked.Should().Be(new GateReading("blocked", "every provider rate-limited"));
        blocked.IsBlocked.Should().BeTrue();
        GateStatus.WaitAgain(blocked, 7, 8).Should().BeTrue("seven waits spent of eight allowed: one more");
        GateStatus.WaitAgain(blocked, 8, 8).Should().BeFalse("eight of eight: the gate is finished as it stands (E2 §8 item 10)");

        var proceed = Of("""{"status":"proceed"}""");
        proceed.Should().Be(new GateReading("proceed", string.Empty));
        proceed.IsBlocked.Should().BeFalse();
        GateStatus.WaitAgain(proceed, 0, 8).Should().BeFalse("only a blocked gate waits");

        IReadOnlyList<TimingLine> timing =
        [
            new TimingLine.GateWait("C1", "4-gate", 1, 1, "quota", At),
            new TimingLine.Start("C1", "4-gate", 2, "resume", "s", "m", At, 60, string.Empty, "h", "f"),
            new TimingLine.GateWait("C1", "4-gate", 2, 2, "quota", At),
            new TimingLine.GateWait("C1", "6-gate", 1, 1, "quota", At),
            new TimingLine.Other("{\"kind\":\"gate-wait\"}"),
        ];
        GateStatus.WaitsSoFar(timing, "4-gate").Should().Be(2, "the count is the wait lines of THIS step — a start line, a line of another step and a line the reader did not understand are not waits");
        GateStatus.WaitsSoFar(timing, "6-gate").Should().Be(1);
        GateStatus.WaitsSoFar(timing, "1-code").Should().Be(0);
        GateStatus.WaitsSoFar([], "4-gate").Should().Be(0);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"status":null}""")]
    public void An_object_that_says_nothing_reads_as_missing(string text)
    {
        Of(text).Should().Be(new GateReading("missing", string.Empty), "a model that chose to say nothing wrote no status: the shell finishes the gate");
    }

    [Theory]
    [InlineData("not json", "is not JSON")]
    [InlineData("[]", "JSON object")]
    [InlineData("""{"status":3}""", "'status'")]
    [InlineData("", "empty")]
    [InlineData("   \r\n", "empty")]
    [InlineData("""{"status":"proceed""", "is not JSON")]
    [InlineData("""{"status":""}""", "not one of")]
    [InlineData("""{"status":"Blocked"}""", "not one of")]
    [InlineData("""{"status":"done"}""", "not one of")]
    public void Text_that_is_present_and_is_no_status_is_unjudgeable_with_the_reason_never_missing(string text, string reason)
    {
        var reading = Of(text);

        reading.IsUnjudgeable.Should().BeTrue($"'{text}' is there and says nothing the gate can act on");
        reading.Unjudgeable.Should().Contain(reason);
        reading.IsBlocked.Should().BeFalse();
        GateStatus.WaitAgain(reading, 0, 8).Should().BeFalse("an unjudgeable status is a stop, never a wait");
    }

    [Theory]
    [InlineData("in_progress")]
    [InlineData("proceed")]
    [InlineData("good_enough")]
    [InlineData("rounds_spent")]
    [InlineData("call_human")]
    [InlineData("blocked")]
    public void Every_status_word_the_gate_brief_allows_is_read_as_itself(string word)
    {
        Of("{\"status\":\"" + word + "\"}").Should().Be(new GateReading(word, string.Empty));
    }

    [Fact]
    public void A_trailing_comma_is_tolerated_and_a_blocked_object_stays_blocked()
    {
        Of("""{"status":"blocked","why":"x",}""").Should().Be(new GateReading("blocked", "x"));
    }

    [Fact]
    public void No_file_reads_as_missing()
    {
        GateStatus.Read(Outcome<string>.Failure("no file")).Should().Be(new GateReading("missing", string.Empty));
    }

    [Fact]
    public void A_reason_that_is_not_text_reads_as_empty_and_keeps_the_status()
    {
        Of("""{"status":"blocked","why":3}""").Should().Be(new GateReading("blocked", string.Empty), "the status is read; a reason that is not a string is none");
        Of("""{"status":"blocked","why":"\ud83d"}""").Should().Be(new GateReading("blocked", string.Empty), "a model's lone surrogate (an emoji cut in two) is not text and must not throw");
        Of("""{"status":"\ud83d"}""").IsUnjudgeable.Should().BeTrue("and a status that is there and is not text cannot be judged");
    }
}
