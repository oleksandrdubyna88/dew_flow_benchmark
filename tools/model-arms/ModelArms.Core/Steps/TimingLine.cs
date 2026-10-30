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

/// <summary>Reads and writes <c>timing.jsonl</c> lines in the shell's key order (<c>run-arm.sh</c> of the part-6 experiment): UTC as <c>yyyy-MM-ddTHH:mm:ssZ</c>, one compact line, no newline.</summary>
public static class TimingLines
{
    /// <summary>The reason of a wall or an exit the file (a timing line or a <c>.done</c> marker) did not carry.</summary>
    public const string NotRecorded = "the file does not carry it";

    /// <summary>One line of compact JSON, no newline, keys in the shell's order. An uncaptured wall or exit is JSON <c>null</c>; <c>recovered</c> and <c>missing</c> are written only when they say something.</summary>
    public static string Write(TimingLine line) =>
        line switch
        {
            TimingLine.Start start => WriteStart(start),
            TimingLine.End end => WriteEnd(end),
            TimingLine.GateWait wait => WriteGateWait(wait),
            TimingLine.ArmDone done => LineJson.Object(("kind", LineJson.Text("arm-done")), ("arm", LineJson.Text(done.Arm)), ("done_utc", LineJson.Utc(done.DoneUtc))),
            TimingLine.Stopped stopped => WriteStopped(stopped),
            TimingLine.Other other => other.Raw,
            _ => throw new InvalidOperationException("unreachable"),
        };

    /// <summary>Whether <paramref name="arm"/> has finished its scheme and nothing has run since: an <c>arm-done</c> line of this arm after the LAST <c>start</c> or <c>gate-wait</c> line. A <c>stopped</c> line after an <c>arm-done</c> does not make the arm
    /// unfinished — only work does. The one rule <c>run-arm</c> writes <c>arm-done</c> by and the gate step decides a stop needs no line by.</summary>
    public static bool IsArmDone(IReadOnlyList<TimingLine> timing, string arm) =>
        timing.Skip(timing.ToList().FindLastIndex(line => line is TimingLine.Start or TimingLine.GateWait) + 1).Any(line => line is TimingLine.ArmDone done && done.Arm == arm);

    /// <summary>One <see cref="TimingLine"/> per non-empty line of <paramref name="text"/>. A line that is not a JSON object, whose <c>kind</c> is unknown or one of whose required fields is missing or of the wrong
    /// kind is <see cref="TimingLine.Other"/> with its text, so a reader never drops a line and never throws on one. A missing <c>limited</c>, <c>recovered</c> or <c>end</c> reads as false or empty (the shell's lines
    /// and the previous generation's carry none); a missing or null <c>wall_s</c> or <c>exit</c> is <see cref="Bench.Domain.Trace.CapturedCount.Unavailable"/>.</summary>
    public static IReadOnlyList<TimingLine> Read(string text) =>
        [.. text.Split('\n').Select(raw => raw.TrimEnd('\r')).Where(raw => !string.IsNullOrWhiteSpace(raw)).Select(ReadLine)];

    private static TimingLine ReadLine(string raw) =>
        JsonRow.Parse(raw, "a timing line").Match(row => Typed(row) ?? new TimingLine.Other(raw), _ => new TimingLine.Other(raw));

    private static TimingLine? Typed(JsonRow row)
    {
        TimingLine? line = row.Text("kind", required: true) switch
        {
            "start" => ReadStart(row),
            "end" => ReadEnd(row),
            "gate-wait" => ReadGateWait(row),
            "arm-done" => new TimingLine.ArmDone(row.Text("arm", required: true), row.Utc("done_utc")),
            "stopped" => ReadStopped(row),
            _ => null,
        };
        return row.Problem.Length == 0 ? line : null;
    }

    private static TimingLine.Start ReadStart(JsonRow row) =>
        new(row.Text("arm", required: true), row.Text("step", required: true), row.Int("attempt"), row.Text("how"), row.Text("session"), row.Text("model"), row.Utc("start_utc"), row.Int("budget_s"),
            row.Text("deadline"), row.Text("head"), row.Text("stream"));

    private static TimingLine.End ReadEnd(JsonRow row) =>
        new(row.Text("arm", required: true), row.Text("step", required: true), row.Int("attempt"), row.Utc("end_utc"), row.Count("wall_s"), row.Count("exit"), row.Flag("result"), row.Flag("limited"),
            row.Text("head"), row.Text("end"), row.Flag("recovered"))
        {
            Missing = row.Texts("missing"),
        };

    private static TimingLine.GateWait ReadGateWait(JsonRow row) =>
        new(row.Text("arm", required: true), row.Text("step", required: true), row.Int("after"), row.Int("wait"), row.Text("why"), row.Utc("at_utc"));

    private static TimingLine.Stopped ReadStopped(JsonRow row) =>
        new(row.Text("arm", required: true), row.Text("step"), row.Int("exit"), row.Text("why"), row.Utc("at_utc"));

    private static string WriteStart(TimingLine.Start start) =>
        LineJson.Object(
            ("kind", LineJson.Text("start")), ("arm", LineJson.Text(start.Arm)), ("step", LineJson.Text(start.Step)), ("attempt", LineJson.Number(start.Attempt)), ("how", LineJson.Text(start.How)),
            ("session", LineJson.Text(start.Session)), ("model", LineJson.Text(start.Model)), ("start_utc", LineJson.Utc(start.StartUtc)), ("budget_s", LineJson.Number(start.BudgetSeconds)),
            ("deadline", LineJson.Text(start.Deadline)), ("head", LineJson.Text(start.Head)), ("stream", LineJson.Text(start.Stream)));

    private static string WriteEnd(TimingLine.End end) =>
        LineJson.Object(
            [
                ("kind", LineJson.Text("end")), ("arm", LineJson.Text(end.Arm)), ("step", LineJson.Text(end.Step)), ("attempt", LineJson.Number(end.Attempt)), ("end_utc", LineJson.Utc(end.EndUtc)),
                ("wall_s", LineJson.Count(end.WallSeconds)), ("exit", LineJson.Count(end.Exit)), ("result", LineJson.Flag(end.Result)), ("limited", LineJson.Flag(end.Limited)),
                ("head", LineJson.Text(end.Head)), ("end", LineJson.Text(end.State)),
                .. LineJson.Only(end.Recovered, "recovered", () => LineJson.Flag(true)),
                .. LineJson.Only(end.Missing.Count > 0, "missing", () => LineJson.Texts(end.Missing)),
            ]);

    private static string WriteGateWait(TimingLine.GateWait wait) =>
        LineJson.Object(
            ("kind", LineJson.Text("gate-wait")), ("arm", LineJson.Text(wait.Arm)), ("step", LineJson.Text(wait.Step)), ("after", LineJson.Number(wait.After)), ("wait", LineJson.Number(wait.Wait)),
            ("why", LineJson.Text(wait.Why)), ("at_utc", LineJson.Utc(wait.AtUtc)));

    private static string WriteStopped(TimingLine.Stopped stopped) =>
        LineJson.Object(
            ("kind", LineJson.Text("stopped")), ("arm", LineJson.Text(stopped.Arm)), ("step", LineJson.Text(stopped.Step)), ("exit", LineJson.Number(stopped.Exit)), ("why", LineJson.Text(stopped.Why)),
            ("at_utc", LineJson.Utc(stopped.AtUtc)));
}
