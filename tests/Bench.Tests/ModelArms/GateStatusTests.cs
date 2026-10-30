using Bench.Domain;
using FluentAssertions;
using ModelArms.Core.Launch;
using ModelArms.Core.Steps;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>What the gate step reads of its own step's files: <c>notes/gate.json</c> (a status word and a reason, <c>run-arm.sh:153</c>, <c>:156</c>), whether another wait is allowed (<c>:154</c>), and how many blocked
/// rounds the arm has already waited out, counted from the durable <c>gate-wait</c> lines of <c>timing.jsonl</c> (E2 §8 item 44) — never from a local that a restart forgets. The file is a model's answer: whatever it holds
/// reads as a status or as <c>missing</c>, and nothing throws.
/// <para>
/// What this does NOT prove: what a real gate model writes into <c>gate.json</c> (the status words are the shell's: <c>proceed</c>, <c>blocked</c>, <c>missing</c> when unreadable), or that the file is read at the right
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
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"status":3}""")]
    [InlineData("""{"status":null}""")]
    [InlineData("{}")]
    [InlineData("")]
    [InlineData("""{"status":"proceed""")]
    public void A_missing_unreadable_or_statusless_file_reads_as_missing(string text)
    {
        Of(text).Should().Be(new GateReading("missing", string.Empty));
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
        Of("""{"status":"\ud83d"}""").Should().Be(new GateReading("missing", string.Empty), "and a status that is not text is no status");
    }
}
