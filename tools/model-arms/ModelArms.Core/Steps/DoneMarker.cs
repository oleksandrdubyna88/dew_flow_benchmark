using Bench.Domain;
using Bench.Domain.Trace;

namespace ModelArms.Core.Steps;

/// <summary>The content of <c>out/&lt;step&gt;.done</c> (plan D5, D6): how many attempts it took, the exit of the last one, the stream a review's verdict is read from, and the state word.</summary>
public sealed record DoneMarker(int Attempts, CapturedCount Exit, string Stream, string State, bool Recovered);

/// <summary>Reads and writes the <c>.done</c> marker — the shell's three keys first (<c>review_json</c> and the repair script read <c>stream</c>), then <c>end</c>.</summary>
public static class DoneMarkers
{
    public static string Write(DoneMarker marker) => string.Empty;

    public static Outcome<DoneMarker> Read(string json) => Outcome<DoneMarker>.Failure("stub");
}
