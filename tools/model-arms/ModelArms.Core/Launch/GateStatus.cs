using Bench.Domain;
using ModelArms.Core.Steps;

namespace ModelArms.Core.Launch;

/// <summary>What <c>gate.json</c> said (plan C9): the status word the gate model wrote (<c>proceed</c>, <c>blocked</c>, or <c>missing</c> when the file said none) and the reason it gave, <c>""</c> when it gave none.</summary>
public sealed record GateReading(string Status, string Why)
{
    public bool IsBlocked => Status == "blocked";

    /// <summary>STUB (red first).</summary>
    public string Unjudgeable { get; init; } = string.Empty;

    public bool IsUnjudgeable => Unjudgeable.Length > 0;
}

/// <summary>What the gate step reads of its own step's files, as decisions (plan C9, <c>run-arm.sh:153-156</c>). The file is a model's answer, so it is read as a stranger's input: whatever it holds reads as a status or as
/// <c>missing</c>, and nothing here throws.</summary>
public static class GateStatus
{
    private const string Missing = "missing";

    /// <summary>A <c>Failure</c> (no file), text that is not a JSON object, or no string <c>status</c> reads as <c>("missing", "")</c>; else the status and the <c>why</c> as a string, <c>""</c> when it is not one.</summary>
    public static GateReading Read(Outcome<string> gateJson) =>
        gateJson.Match(Parsed, _ => Nothing());

    /// <summary>Whether the gate waits again (<c>run-arm.sh:154</c>): only a blocked status, and only while the waits already spent are fewer than <paramref name="gateTries"/>.</summary>
    public static bool WaitAgain(GateReading reading, int waitsSoFar, int gateTries) =>
        reading.IsBlocked && waitsSoFar < gateTries;

    /// <summary>How many blocked rounds of <paramref name="step"/> have been waited out: its <c>gate-wait</c> lines in <paramref name="timing"/> (E2 §8 item 44). The count is read back from the durable file, so a restart
    /// does not start again at zero; a start line, a line of another step and a line the reader kept as <c>Other</c> are not waits.</summary>
    public static int WaitsSoFar(IReadOnlyList<TimingLine> timing, string step) =>
        timing.OfType<TimingLine.GateWait>().Count(wait => wait.Step == step);

    private static GateReading Nothing() => new(Missing, string.Empty);

    private static GateReading Parsed(string text) =>
        JsonRow.Parse(text, "gate.json").Match(Of, _ => Nothing());

    private static GateReading Of(JsonRow row)
    {
        var status = row.Text("status", required: true);
        return row.Problem.Length > 0 ? Nothing() : new GateReading(status, row.Text("why"));
    }
}
