using Bench.Domain.Models;
using Bench.Domain.Suites;

namespace Bench.Domain.Runs;

/// <param name="Available">Whether this lane could surface anything at all. A lane with no tools cannot,
/// and that is a property of the ARM rather than a failure of the subject.</param>
/// <param name="SurfacedAnchors">Canonical anchors the leg actually reached.</param>
public sealed record RetrievalObservation(bool Available, IReadOnlyList<string> SurfacedAnchors)
{
    /// <summary>A lane where the subject answers from its own weights and nothing else.</summary>
    public static RetrievalObservation None => new(false, []);

    public static RetrievalObservation Of(params string[] anchors) => new(true, anchors);
}

/// <param name="Available">Whether this lane offered the subject any tool at all. A no-tools arm did not,
/// and that is a property of the ARM rather than a failure of the subject — the same distinction
/// <see cref="RetrievalObservation"/> makes, one axis over, and for the identical reason.</param>
/// <param name="ToolsCalled">Every tool the subject actually invoked, in order, with repeats. Repeats are
/// kept because "it called the search four times" and "it called it once" are different facts about how a
/// model works a surface, and collapsing them here would throw that away before anything could count it.</param>
public sealed record ToolUsageObservation(bool Available, IReadOnlyList<string> ToolsCalled)
{
    /// <summary>A lane that offered nothing. Not "it called nothing" — it could not.</summary>
    public static ToolUsageObservation None => new(false, []);

    public static ToolUsageObservation Of(params string[] names) => new(true, names);
}

/// <summary>Turning one leg's answer into metrics, mechanically.
/// <para>
/// Deterministic on purpose. The first measurement in this system must not depend on a second model: an
/// arbiter is the one component that cannot be checked by reading, so a run that needs one is measuring
/// two things at once before either has been shown to work.
/// </para></summary>
public static class AnswerScoring
{
    public const string AnchorRecall = "Anchor recall";

    public const string ToolUse = "Tool use";

    public static IReadOnlyList<StoredMetric> Score(
        Question question, ModelAnswer answer, RetrievalObservation retrieval) =>
        Score(question, answer, retrieval, ToolUsageObservation.None);

    public static IReadOnlyList<StoredMetric> Score(
        Question question,
        ModelAnswer answer,
        RetrievalObservation retrieval,
        ToolUsageObservation tools) =>
        [.. TextMetrics(question, answer), RecallMetric(question, retrieval), .. ToolMetrics(question, tools)];

    /// <summary>
    /// One metric per tool expectation — or an honest statement that it does not apply.
    ///
    /// <para><b>A <c>ToolUsed</c> expectation in a lane with no tools is NOT a miss.</b> The identical rule
    /// anchor recall already applies, and for the identical reason: the no-tools floor exists to be compared
    /// fairly, and scoring it zero for not calling a tool it never had would make the baseline look worse
    /// than it is and flatter every tool lane by exactly that much. Emitted as TEXT so the numeric aggregate
    /// skips it and reports a smaller denominator rather than a diluted mean.</para>
    ///
    /// <para><c>ToolNotUsed</c> follows the same rule for the same reason, and it is not symmetric with a
    /// pass: a lane that offered the tool is the only place where NOT calling it means anything.</para>
    /// </summary>
    private static IEnumerable<StoredMetric> ToolMetrics(Question question, ToolUsageObservation tools) =>
        question.ToolExpectations.Select(expectation => ToolMetric(expectation, tools));

    private static StoredMetric ToolMetric(Expectation expectation, ToolUsageObservation tools)
    {
        var wanted = expectation.Kind == ExpectationKind.ToolUsed;
        var label = $"{ToolUse}: {expectation.Text}";

        if (!tools.Available)
        {
            return StoredMetric.Text(
                label,
                "not applicable",
                $"this lane offers no tools, so '{expectation.Text}' could not be called — that is the arm, not the subject",
                failed: false,
                "Unknown");
        }

        var called = tools.ToolsCalled.Contains(expectation.Text, StringComparer.Ordinal);
        var met = called == wanted;

        return StoredMetric.Numeric(
            label,
            met ? 1 : 0,
            Describe(expectation.Text, wanted, called),
            failed: !met,
            met ? "Exceptional" : "Unacceptable");
    }

    /// <summary>Why the expectation was met or not, in the words a reader needs.
    /// <para>The unmet <c>ToolNotUsed</c> case is the one worth spelling out: a model that reached for a
    /// tool it should not have is evidence about the DESCRIPTION, not about the model, and a metric that
    /// only said "failed" would send a reader looking in the wrong place.</para></summary>
    private static string Describe(string tool, bool wanted, bool called) =>
        (wanted, called) switch
        {
            (true, true) => $"'{tool}' was called",
            (true, false) => $"'{tool}' was offered and never called",
            (false, false) => $"'{tool}' was correctly left alone",
            _ => $"'{tool}' was called where it should not have been — read this as a defect in its description",
        };

    /// <summary>One metric per answer expectation.
    /// <para>
    /// Case-insensitive, and that is a deliberate loosening rather than an oversight: an answer that writes
    /// a term in prose knows the same thing as one that writes it in the code's casing. It does not weaken a
    /// memorisation trap, because a trap turns on a term that has to be READ — spelling it at all is the
    /// evidence, and a different capitalisation of the same token is still that token.
    /// </para></summary>
    private static IEnumerable<StoredMetric> TextMetrics(Question question, ModelAnswer answer) =>
        question.Expectations
            .Where(e => e.Kind is ExpectationKind.AnswerContains or ExpectationKind.AnswerExcludes)
            .Select(e => TextMetric(e, answer));

    private static StoredMetric TextMetric(Expectation expectation, ModelAnswer answer)
    {
        if (!answer.Text.WasCaptured)
        {
            // No answer is not a wrong answer, and it must not be scored as one — the model may have been
            // cut off, refused, or answered into a stream nobody could read.
            return StoredMetric.Text(
                Name(expectation), "not answered", $"no text was captured: {answer.Text.Reason}",
                failed: false, "Unknown");
        }

        var present = answer.Text.Value.Contains(expectation.Text, StringComparison.OrdinalIgnoreCase);
        var wanted = expectation.Kind == ExpectationKind.AnswerContains;

        return StoredMetric.Boolean(
            Name(expectation),
            present == wanted,
            Reason(expectation.Text, present, wanted),
            failed: present != wanted && expectation.Required,
            present == wanted ? "Good" : "Unacceptable");
    }

    /// <summary>Why the expectation held or did not, said so that one reading is possible.
    /// <para>
    /// This line is read by an agent, so an elision it has to resolve is a defect. The first live run
    /// printed <c>'throughput' was absent, and must not have been</c> for a MISSING required term — which
    /// parses just as easily as "it was there and should not have been", the opposite verdict. The rule
    /// here: never say what must not have been, always say what the answer had to do.
    /// </para></summary>
    private static string Reason(string term, bool present, bool wanted) => (present, wanted) switch
    {
        (true, true) => $"'{term}' was present, as required",
        (false, false) => $"'{term}' was absent, as required",
        (false, true) => $"'{term}' was absent, and the answer had to contain it",
        (true, false) => $"'{term}' was present, and the answer had to avoid it",
    };

    /// <summary>Anchor recall — or an honest statement that it does not apply here.
    /// <para>
    /// A retrieval expectation in a lane with NO retrieval is not a miss. Scoring it zero would make the
    /// no-tools baseline look worse than it is, and that baseline exists precisely to be compared fairly
    /// against the lanes that do have tools. It is emitted as TEXT rather than as a zero, so the numeric
    /// aggregate skips it and reports a smaller denominator instead of a diluted mean.
    /// </para></summary>
    private static StoredMetric RecallMetric(Question question, RetrievalObservation retrieval)
    {
        var expected = question.RetrievalExpectations;

        if (expected.Count == 0)
        {
            return StoredMetric.Text(AnchorRecall, "nothing to find", "the question names no anchors", false, "Unknown");
        }

        if (!retrieval.Available)
        {
            return StoredMetric.Text(
                AnchorRecall,
                "not applicable",
                $"this lane surfaces nothing, so {expected.Count} anchor(s) could not be reached — that is the arm, not the subject",
                failed: false,
                "Unknown");
        }

        var found = expected.Count(e => retrieval.SurfacedAnchors.Contains(e.Anchor.Canonical, StringComparer.Ordinal));
        var recall = (double)found / expected.Count;

        return StoredMetric.Numeric(
            AnchorRecall,
            recall,
            $"{found} of {expected.Count} anchor(s) surfaced",
            failed: found == 0,
            recall >= 1 ? "Exceptional" : recall > 0 ? "Average" : "Unacceptable");
    }

    private static string Name(Expectation expectation) =>
        $"{(expectation.Kind == ExpectationKind.AnswerContains ? "Answer contains" : "Answer excludes")} '{expectation.Text}'";
}
