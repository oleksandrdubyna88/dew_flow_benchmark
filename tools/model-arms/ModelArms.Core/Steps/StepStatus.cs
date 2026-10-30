using ModelArms.Core.Experiment;

namespace ModelArms.Core.Steps;

/// <summary>A file read BY NAME, in the three states the reader can tell apart (E3 C16): nothing by that name, its parsed content, or there-but-refused — a link, a FIFO, a file past the limit, text that does not parse. A refusal is NOT
/// absence: a caller that read it as "no file" would call a blocked answer a missing one.</summary>
public abstract record Read<T>
{
    private Read()
    {
    }

    public sealed record Absent : Read<T>;

    public sealed record Present(T Value) : Read<T>;

    public sealed record Refused(string Reason) : Read<T>;
}

/// <summary>Where one step of one arm stands, as <c>status</c> reads it (E3.S4): a closed set, and every row is a fact the files say.</summary>
public abstract record StepState
{
    private StepState()
    {
    }

    public sealed record NotStarted : StepState;

    public sealed record Done(string End, int Attempts) : StepState;

    public sealed record Continuing(int After, string Why) : StepState;

    public sealed record Resumable(string End, int Attempt) : StepState;

    public sealed record Limited(int Attempt, string Stop) : StepState;

    public sealed record Unreadable(string Reason) : StepState;
}

/// <summary>Everything <c>status</c> reads of one step, as values: the step's file stem, its two markers, the number of attempt streams on disk and the arm's timing lines.</summary>
public sealed record StepEvidence(string Stem, Read<DoneMarker> Done, Read<ContinueMarker> Continue, int Streams, IReadOnlyList<TimingLine> Timing);

/// <summary>Where a step stands, decided over the evidence (E3.S4): the FIRST row that holds.
/// <list type="number">
/// <item>a marker the reader refused — <see cref="StepState.Unreadable"/> (a marker nobody can trust is never "done");</item>
/// <item><c>.continue</c> present and its <c>after</c> is the last attempt (<see cref="StepEvidence.Streams"/>) — <see cref="StepState.Continuing"/>; a marker for an older attempt is stale and decides nothing (D7), the same rule <see cref="StepRecovery"/> enters by;</item>
/// <item><c>.done</c> present — <see cref="StepState.Done"/> with its end word (<c>not recorded</c> for a shell's marker, which carries none);</item>
/// <item>the step's last <c>end</c> line, when it is for the last attempt, says <c>limited</c> — <see cref="StepState.Limited"/>, with the <c>why</c> of a LATER <c>stopped</c> line of the step, else <c>""</c>; any other state — <see cref="StepState.Resumable"/>;</item>
/// <item>an attempt on disk with no <c>end</c> line of its own — <see cref="StepState.Resumable"/> as <c>interrupted (no end line)</c>; else <see cref="StepState.NotStarted"/>.</item>
/// </list>
/// An end line for an EARLIER attempt than the last stream is not the step's last word: attempt 3 began after it and has none, which is row 5.</summary>
public static class StepStatuses
{
    private const string NotRecorded = "not recorded";
    private const string NoEndLine = "interrupted (no end line)";

    /// <summary>The longest a model- or runner-written reason is printed; a cut is marked.</summary>
    private const int WhyCharacters = 120;

    private static readonly string DeadlineWord = new StepEnd.HardDeadline().Word;
    private static readonly string LimitedWord = new StepEnd.Limited(string.Empty).Word;

    public static StepState Of(StepEvidence evidence) =>
        Refusal(evidence) ?? Continuing(evidence) ?? Done(evidence) ?? Ended(evidence) ?? Interrupted(evidence);

    private static StepState? Refusal(StepEvidence evidence) =>
        evidence.Done is Read<DoneMarker>.Refused done ? new StepState.Unreadable(done.Reason)
        : evidence.Continue is Read<ContinueMarker>.Refused marker ? new StepState.Unreadable(marker.Reason)
        : null;

    private static StepState? Continuing(StepEvidence evidence) =>
        evidence.Continue is Read<ContinueMarker>.Present { Value: var marker } && marker.After == evidence.Streams ? new StepState.Continuing(marker.After, marker.Why) : null;

    private static StepState? Done(StepEvidence evidence) =>
        evidence.Done is Read<DoneMarker>.Present { Value: var marker } ? new StepState.Done(marker.State.Length == 0 ? NotRecorded : marker.State, marker.Attempts) : null;

    private static StepState? Ended(StepEvidence evidence)
    {
        var ends = evidence.Timing.Select((line, at) => (End: line as TimingLine.End, At: at)).Where(each => each.End is not null && each.End.Step == evidence.Stem).ToList();
        if (ends.Count == 0 || ends[^1].End!.Attempt < evidence.Streams)
        {
            return null;
        }

        var (last, endAt) = (ends[^1].End!, ends[^1].At);
        return last.Limited || last.State == LimitedWord
            ? new StepState.Limited(last.Attempt, StopAfter(evidence, endAt))
            : new StepState.Resumable(last.State.Length == 0 ? NotRecorded : last.State, last.Attempt);
    }

    /// <summary>The <c>why</c> of the last <c>stopped</c> line of the step that comes after the end line at <paramref name="endAt"/>, else <c>""</c>.</summary>
    private static string StopAfter(StepEvidence evidence, int endAt) =>
        evidence.Timing.Skip(endAt + 1).OfType<TimingLine.Stopped>().LastOrDefault(stopped => stopped.Step == evidence.Stem)?.Why ?? string.Empty;

    private static StepState Interrupted(StepEvidence evidence) =>
        evidence.Streams > 0 ? new StepState.Resumable(NoEndLine, evidence.Streams) : new StepState.NotStarted();

    /// <summary>The step's one line: <c>1-code: done (finished)</c> · <c>2-review: done — ended on its deadline</c> · <c>4-gate: continuing — blocked after attempt N: &lt;why&gt;</c> · <c>resumable — &lt;state&gt; at attempt N</c> · <c>limited at attempt N — &lt;why&gt;</c> ·
    /// <c>not started</c> · <c>unreadable — &lt;reason&gt;</c>. A reason or a why another program wrote is printed escaped (<see cref="ExecutableRule.Printable"/>) and cut at <see cref="WhyCharacters"/> characters, the cut marked.</summary>
    public static string Line(string stem, StepState state) =>
        stem + ": " + state switch
        {
            StepState.Done done when done.End == DeadlineWord => "done — ended on its deadline",
            StepState.Done done => $"done ({ExecutableRule.Printable(done.End)})",
            StepState.Continuing continuing => $"continuing — blocked after attempt {continuing.After}: {Cut(continuing.Why)}",
            StepState.Resumable resumable => $"resumable — {ExecutableRule.Printable(resumable.End)} at attempt {resumable.Attempt}",
            StepState.Limited { Stop: "" } limited => $"limited at attempt {limited.Attempt}",
            StepState.Limited limited => $"limited at attempt {limited.Attempt} — {Cut(limited.Stop)}",
            StepState.Unreadable unreadable => "unreadable — " + ExecutableRule.Printable(unreadable.Reason),
            _ => "not started",
        };

    /// <summary>The text escaped and cut at <see cref="WhyCharacters"/> characters of the ORIGINAL, never inside a surrogate pair, <c>…</c> after a cut.</summary>
    private static string Cut(string text)
    {
        if (text.Length <= WhyCharacters)
        {
            return ExecutableRule.Printable(text);
        }

        var keep = char.IsHighSurrogate(text[WhyCharacters - 1]) ? WhyCharacters - 1 : WhyCharacters;
        return ExecutableRule.Printable(text[..keep]) + "…";
    }
}
