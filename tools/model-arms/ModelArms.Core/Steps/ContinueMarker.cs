using Bench.Domain;

namespace ModelArms.Core.Steps;

/// <summary>The gate's durable intent (plan D7, D10; E2 §8 item 24): "attempt <paramref name="After"/> of this step FINISHED, and I want its session continued". The one thing a finished
/// stream without <c>.done</c> cannot say by itself.</summary>
public sealed record ContinueMarker(int After, string Why);

/// <summary>Reads and writes <c>out/&lt;step&gt;.continue</c>.</summary>
public static class ContinueMarkers
{
    public static string Write(ContinueMarker marker) => string.Empty;

    public static Outcome<ContinueMarker> Read(string json) => Outcome<ContinueMarker>.Failure("stub");
}
