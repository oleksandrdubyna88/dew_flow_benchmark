using Bench.Domain;
using ModelArms.Core.Steps;

namespace ModelArms.Core.Launch;

/// <summary>What <c>gate.json</c> said (plan C9): the status word the gate model wrote — one of <see cref="GateStatus.Words"/> — or <c>missing</c> when the file said none, and the reason it gave, <c>""</c> when it gave none. A fourth answer is
/// <see cref="Unjudgeable"/>: the text is THERE and is no status the gate can act on (E2 §8 item 60) — it is not a wait and not a finish, the gate stops.</summary>
public sealed record GateReading(string Status, string Why)
{
    public bool IsBlocked => Status == "blocked";

    /// <summary>Why the text that is there cannot be judged, or <c>""</c>.</summary>
    public string Unjudgeable { get; init; } = string.Empty;

    public bool IsUnjudgeable => Unjudgeable.Length > 0;

    /// <summary>The text is present and is no status; <paramref name="reason"/> says what is wrong with it.</summary>
    public static GateReading Unjudged(string reason) => new("unjudgeable", string.Empty) { Unjudgeable = reason };
}

/// <summary>What the gate step reads of its own step's files, as decisions (plan C9, <c>run-arm.sh:153-156</c>). The file is a model's answer, so it is read as a stranger's input and nothing here throws. Four answers, never fewer: a status word
/// of the gate brief; <c>missing</c> — there is no file, or an object that says no status (a model that chose to say nothing: the shell finishes the gate); <see cref="GateReading.Unjudgeable"/> — text that is present and is not a JSON object with a
/// string <c>status</c>, an empty file, or a word the brief does not allow; and a tolerated trailing comma. <b>This changes the shell's, and E2.S6's, "not JSON reads as missing"</b> (E2 §8 item 60): finishing a gate whose answer cannot be read
/// lets a BLOCKED gate pass, and a model that wraps its JSON in a Markdown fence is a common failure.</summary>
public static class GateStatus
{
    private const string Missing = "missing";

    /// <summary>The words <c>tools/model-arms/briefs/gate.template.md</c> tells the gate model it may write, matched exactly (case-sensitive, as written there). Any other word is unjudgeable: a gate cannot act on what it does not know.</summary>
    public static readonly IReadOnlyList<string> Words = ["in_progress", "proceed", "good_enough", "rounds_spent", "call_human", "blocked"];

    /// <summary>A <c>Failure</c> (no file) reads as <c>("missing", "")</c>; so does an object with no <c>status</c> (absent or <c>null</c>). An empty or blank text, text that is not a JSON object, a <c>status</c> that is not text and a status word outside
    /// <see cref="Words"/> are <see cref="GateReading.Unjudgeable"/> with the reason; else the status and the <c>why</c> as a string, <c>""</c> when it is not one.</summary>
    public static GateReading Read(Outcome<string> gateJson) =>
        gateJson.Match(Present, _ => Nothing());

    /// <summary>Whether the gate waits again (<c>run-arm.sh:154</c>): only a blocked status, and only while the waits already spent are fewer than <paramref name="gateTries"/>.</summary>
    public static bool WaitAgain(GateReading reading, int waitsSoFar, int gateTries) =>
        reading.IsBlocked && waitsSoFar < gateTries;

    /// <summary>How many blocked rounds of <paramref name="step"/> have been waited out: its <c>gate-wait</c> lines in <paramref name="timing"/> (E2 §8 item 44). The count is read back from the durable file, so a restart
    /// does not start again at zero; a start line, a line of another step and a line the reader kept as <c>Other</c> are not waits.</summary>
    public static int WaitsSoFar(IReadOnlyList<TimingLine> timing, string step) =>
        timing.OfType<TimingLine.GateWait>().Count(wait => wait.Step == step);

    private static GateReading Nothing() => new(Missing, string.Empty);

    private static GateReading Present(string text) =>
        string.IsNullOrWhiteSpace(text)
            ? GateReading.Unjudged("the file is empty")
            : JsonRow.Parse(text, "gate.json").Match(Of, GateReading.Unjudged);

    private static GateReading Of(JsonRow row)
    {
        if (!row.Has("status"))
        {
            return Nothing();
        }

        var status = row.Text("status", required: true);
        return row.Problem.Length > 0 ? GateReading.Unjudged($"gate.json's {row.Problem}")
            : !Words.Contains(status) ? GateReading.Unjudged($"the status '{status}' is not one of {string.Join(", ", Words)}")
            : new GateReading(status, row.Text("why"));
    }
}
