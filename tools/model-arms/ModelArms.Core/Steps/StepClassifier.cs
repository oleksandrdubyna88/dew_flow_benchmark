using Bench.Domain;
using ModelArms.Core.Launch;

namespace ModelArms.Core.Steps;

/// <summary>The one decision of how an attempt ended (plan C5, D6): its last result line first, its stderr only when there is no result, in a fixed order.
/// Structured fields decide before words — the 2026-10-28 incident was a limit read by its words that the words did not match. Pure: it reads its three inputs and nothing else,
/// in particular no <c>rate_limit_event</c> line.</summary>
public static class StepClassifier
{
    /// <summary>How much of the end of stderr is searched for limit words — the shell's <c>slice(-4000)</c>.</summary>
    public const int StderrTailCharacters = 4000;

    /// <summary>The attempt's end: not started, then a last result (limited by status, by <c>api_error</c>, by words; errored; finished), then no result (limit words on
    /// stderr, the wall, the operator, otherwise interrupted).</summary>
    public static StepEnd Classify(Outcome<ClaudeResultEnvelope> lastResult, string stderr, AttemptFacts attempt) =>
        new StepEnd.Interrupted("stub — not classified yet");
}
