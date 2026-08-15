using Bench.Domain.Suites;

namespace Bench.Domain.Runs;

/// <summary>What the arbiter said, before it becomes a metric.
/// <para>
/// <paramref name="Reached"/> is separate from <paramref name="Passed"/> on purpose, and it is the whole
/// reason this is a record rather than a bool: a judge that was never reached, or answered something no
/// parser could read, has said NOTHING — and nothing must not arrive as "false". A single boolean cannot
/// carry that distinction, and every place this project lost one it lost it here.
/// </para></summary>
public sealed record JudgeReading(bool Reached, bool Passed, string Reason)
{
    public static JudgeReading Verdict(bool passed, string reason) => new(true, passed, reason);

    /// <summary>The judge could not be asked, or could not be read. Never a failing verdict.</summary>
    public static JudgeReading Silent(string why) => new(false, false, why);
}

/// <summary>Turning an arbiter's verdict into a metric row.
/// <para>
/// A judge metric is <b>appended beside</b> the mechanical ones, never instead of them. Two reasons, and
/// both are the point of the port: a judge is selectable, so two arbiters must be able to disagree about
/// the same stored answer without either erasing the other; and a judge is the one component that cannot
/// be checked by reading, so the deterministic score has to remain visible next to it for comparison.
/// </para></summary>
public static class JudgeScoring
{
    /// <summary>Group-able by arbiter, because that is what the metric-as-row schema is for. Two judges
    /// over one run are two named series, never one column whose meaning depends on when it was written.</summary>
    public static string MetricName(string judgeModelId) => $"Judge verdict · {judgeModelId}";

    /// <param name="subjectModelId">Recorded so an aggregate can exclude self-judging — a model marking
    /// its own homework is a reading, but it is not the same reading, and the schema must be able to
    /// tell them apart after the fact rather than only at the moment somebody notices.</param>
    public static StoredMetric Score(
        Question question, JudgeReading reading, string judgeModelId, string subjectModelId)
    {
        var name = MetricName(judgeModelId);
        var metadata = Provenance(judgeModelId, subjectModelId);

        if (question.ReferenceAnswer.Length == 0)
        {
            // No reference is a gap in the SUITE. Scoring it as a failure would blame the subject for
            // something nobody ever wrote down — the same mistake as scoring anchor recall in a lane that
            // surfaces nothing.
            return StoredMetric
                .Text(name, "not judgeable", "the question carries no reference answer to judge against", false, "Unknown")
                .With(metadata);
        }

        if (!reading.Reached)
        {
            return StoredMetric
                .Text(name, "not judged", $"the arbiter said nothing: {reading.Reason}", false, "Unknown")
                .With(metadata);
        }

        return StoredMetric
            .Boolean(name, reading.Passed, reading.Reason, failed: !reading.Passed,
                reading.Passed ? "Good" : "Unacceptable")
            .With(metadata);
    }

    private static Dictionary<string, string> Provenance(string judgeModelId, string subjectModelId) => new()
    {
        ["judge"] = judgeModelId,
        ["subject"] = subjectModelId,
        ["selfJudged"] = string.Equals(judgeModelId, subjectModelId, StringComparison.OrdinalIgnoreCase) ? "true" : "false",
    };
}
