using Bench.Domain;
using ModelArms.Core.Launch;

namespace ModelArms.Core.Steps;

/// <summary>STUB — E2.S5 is being built test first; this answers nothing yet.</summary>
public static class ReviewAnswer
{
    /// <summary>The text of the review's verdict file, from the last result only.</summary>
    public static Outcome<string> Extract(Outcome<ClaudeResultEnvelope> lastResult) => Outcome<string>.Success(string.Empty);
}
