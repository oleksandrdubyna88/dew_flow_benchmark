using Bench.Domain;

namespace ModelArms.Core.Steps;

/// <summary>The gate's durable intent (plan D7, D10; E2 §8 item 24): "attempt <paramref name="After"/> of this step FINISHED, and I want its session continued". The one thing a finished stream without
/// <c>.done</c> cannot say by itself — D7 reads that as a runner that died after finishing.</summary>
public sealed record ContinueMarker(int After, string Why);

/// <summary>Reads and writes <c>out/&lt;step&gt;.continue</c>.</summary>
public static class ContinueMarkers
{
    /// <summary><c>{"after":N,"why":"&lt;text&gt;"}</c>, compact, no newline.</summary>
    public static string Write(ContinueMarker marker) => LineJson.Object(("after", LineJson.Number(marker.After)), ("why", LineJson.Text(marker.Why)));

    /// <summary>The marker <paramref name="json"/> holds: <c>after</c> a positive whole number, <c>why</c> text or absent (read as <c>""</c>). Anything else — a non-object, a torn file, a missing or non-integral
    /// <c>after</c> — is refused naming what was wrong, never a throw: a marker nobody can read orders nothing.</summary>
    public static Outcome<ContinueMarker> Read(string json) =>
        JsonRow.Parse(json, "a continue marker").Match(Of, Outcome<ContinueMarker>.Failure);

    private static Outcome<ContinueMarker> Of(JsonRow row)
    {
        var marker = new ContinueMarker(row.Int("after"), row.Text("why"));
        return row.Problem.Length > 0 ? Outcome<ContinueMarker>.Failure("a continue marker's " + row.Problem)
            : marker.After < 1 ? Outcome<ContinueMarker>.Failure("a continue marker's 'after' must be a positive whole number — the attempt that finished")
            : Outcome<ContinueMarker>.Success(marker);
    }
}
