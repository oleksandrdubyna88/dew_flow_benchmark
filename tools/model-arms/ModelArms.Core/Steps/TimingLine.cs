using Bench.Domain.Trace;

namespace ModelArms.Core.Steps;

/// <summary>One line of <c>out/timing.jsonl</c> (plan C6, D5, D6): the shell's own lines — <c>start</c>, <c>end</c>, <c>arm-done</c> — and the two this build adds, <c>stopped</c> (D16) and
/// <c>gate-wait</c>. The file is append-only; a line this build does not read is <see cref="Other"/>, kept and never dropped. <c>metrics.mjs</c> filters <c>kind === 'start'</c> and
/// <c>'end'</c>, so a kind it does not know passes through it.</summary>
public abstract record TimingLine
{
    private TimingLine()
    {
    }

    /// <summary>An attempt began. <paramref name="Deadline"/> is the text <c>HH:mm UTC (HH:mm ±hh:mm)</c> the brief carried, kept as text and never re-spelled.</summary>
    public sealed record Start(string Arm, string Step, int Attempt, string How, string Session, string Model, DateTimeOffset StartUtc, int BudgetSeconds, string Deadline, string Head, string Stream)
        : TimingLine;

    /// <summary>An attempt ended. <paramref name="State"/> is the classifier's word (<c>StepEnd.Word</c>), or <c>""</c> for a shell line that carried none. An absent wall or exit is
    /// <see cref="CapturedCount.Unavailable"/> — absent is not zero.</summary>
    public sealed record End(string Arm, string Step, int Attempt, DateTimeOffset EndUtc, CapturedCount WallSeconds, CapturedCount Exit, bool Result, bool Limited, string Head, string State, bool Recovered)
        : TimingLine
    {
        /// <summary>The model-written outputs a DONE attempt left unwritten (E2 §8 item 41); written only when there are any.</summary>
        public IReadOnlyList<string> Missing { get; init; } = [];

        public bool Equals(End? other) =>
            other is not null && (Arm, Step, Attempt, EndUtc, WallSeconds, Exit, Result, Limited, Head, State, Recovered) == (other.Arm, other.Step, other.Attempt, other.EndUtc, other.WallSeconds, other.Exit, other.Result, other.Limited, other.Head, other.State, other.Recovered)
            && Missing.SequenceEqual(other.Missing);

        public override int GetHashCode() => HashCode.Combine(Arm, Step, Attempt, EndUtc, WallSeconds, Exit, Missing.Count);
    }

    /// <summary>One DECIDED blocked round of a gate step (E2 §8 item 44): attempt <paramref name="After"/> was blocked and this is wait number <paramref name="Wait"/>, 1-based. It spends no budget.</summary>
    public sealed record GateWait(string Arm, string Step, int After, int Wait, string Why, DateTimeOffset AtUtc) : TimingLine;

    /// <summary>The arm ran its whole scheme.</summary>
    public sealed record ArmDone(string Arm, DateTimeOffset DoneUtc) : TimingLine;

    /// <summary>The runner stopped on purpose (D16): the exit code it returned and why.</summary>
    public sealed record Stopped(string Arm, string Step, int Exit, string Why, DateTimeOffset AtUtc) : TimingLine;

    /// <summary>A line this build does not read, byte for byte.</summary>
    public sealed record Other(string Raw) : TimingLine;
}

/// <summary>Reads and writes <c>timing.jsonl</c> lines in the shell's key order (<c>run-arm.sh</c> of the part-6 experiment).</summary>
public static class TimingLines
{
    /// <summary>The reason of a wall or an exit the file (a timing line or a <c>.done</c> marker) did not carry.</summary>
    public const string NotRecorded = "the file does not carry it";

    /// <summary>One line of compact JSON, no newline.</summary>
    public static string Write(TimingLine line) => string.Empty;

    /// <summary>One <see cref="TimingLine"/> per non-empty line of <paramref name="text"/>.</summary>
    public static IReadOnlyList<TimingLine> Read(string text) => [];
}
