using Bench.Domain;
using Bench.Domain.Trace;

namespace ModelArms.Core.Steps;

/// <summary>The content of <c>out/&lt;step&gt;.done</c> (plan D5, D6): how many attempts it took, the exit of the last one, the stream a review's verdict is read from, the state word (<c>finished</c> or
/// <c>hard-deadline</c>; <c>""</c> for a shell's marker, which carries none) and whether the step was recovered as done rather than finished by the runner that wrote it.</summary>
public sealed record DoneMarker(int Attempts, CapturedCount Exit, string Stream, string State, bool Recovered);

/// <summary>Reads and writes the <c>.done</c> marker.</summary>
public static class DoneMarkers
{
    /// <summary>The previous generation wrote this word where the attempt count goes when it marked a finished stream done without having run it (<c>run-arm.sh</c> of part 5).</summary>
    private const string RecoveredWord = "recovered";

    /// <summary><c>{"attempts":N,"exit":rc|null,"stream":"&lt;path&gt;","end":"finished|hard-deadline"}</c> plus <c>"recovered":true</c> when recovered — the shell's three keys first (<c>review_json</c> and the repair
    /// script read <c>stream</c>), then <c>end</c>. An exit the process never returned is JSON <c>null</c>, not zero.</summary>
    public static string Write(DoneMarker marker) =>
        LineJson.Object(
            [
                ("attempts", LineJson.Number(marker.Attempts)), ("exit", LineJson.Count(marker.Exit)), ("stream", LineJson.Text(marker.Stream)), ("end", LineJson.Text(marker.State)),
                .. LineJson.Only(marker.Recovered, "recovered", () => LineJson.Flag(true)),
            ]);

    /// <summary>The marker <paramref name="json"/> holds: the shell's own (no <c>end</c> reads as <c>State = ""</c>; no or null <c>exit</c> is not captured) or this build's. A non-object, a torn file or an
    /// <c>attempts</c> that is neither a whole number nor the previous generation's word <c>"recovered"</c> is refused naming what was wrong — never a throw.</summary>
    public static Outcome<DoneMarker> Read(string json) =>
        JsonRow.Parse(json, "a done marker").Match(Of, Outcome<DoneMarker>.Failure);

    private static Outcome<DoneMarker> Of(JsonRow row)
    {
        var previous = row.Is("attempts", RecoveredWord);
        var marker = new DoneMarker(previous ? 0 : row.Int("attempts"), row.Count("exit"), row.Text("stream"), row.Text("end"), previous || row.Flag("recovered"));
        return row.Problem.Length == 0 ? Outcome<DoneMarker>.Success(marker) : Outcome<DoneMarker>.Failure("a done marker's " + row.Problem);
    }
}
