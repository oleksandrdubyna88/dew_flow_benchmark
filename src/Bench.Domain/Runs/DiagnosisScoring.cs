using Bench.Domain.Suites;

namespace Bench.Domain.Runs;

/// <summary>Turning an Investigate phase's reading into metrics, mechanically
/// (todo/PLAN_investigate_vs_implement.md §3.3).
/// <para>
/// The pair recall × precision is the load-bearing part. Recall alone teaches a model to name every
/// file it saw — a shotgun diagnosis scores recall 1.0 and precision 0.1, and only the pair says which
/// subject actually knew. The ground truth is DERIVED from the reference fix's touched members rather
/// than authored, the same principle as seed dates: the author names the change and the repository
/// locates it, so there is no second copy of the diff to drift.
/// </para>
/// <para>
/// Malformed and wrong are DISTINCT states: an unreadable diagnosis scores <see cref="Parses"/> false
/// and NO anchor numbers at all — zeros would drag an average toward a number nothing measured, which
/// is the same rule <see cref="LegOutcome.CapExceeded"/> exists for, one instrument over.
/// </para></summary>
public static class DiagnosisScoring
{
    public const string Parses = "Diagnosis parses";

    public const string AnchorRecall = "Diagnosis anchor recall";

    public const string Precision = "Diagnosis anchor precision";

    public const string SymptomOnly = "Diagnosis symptom-only";

    public static IReadOnlyList<StoredMetric> Score(
        DiagnosisReading reading,
        IReadOnlyList<SourceAnchor> causal,
        IReadOnlyList<SourceAnchor> symptoms) =>
        reading switch
        {
            DiagnosisReading.Parsed parsed =>
                [Followed(), .. AnchorMetrics(parsed.Diagnosis, causal, symptoms)],
            DiagnosisReading.Malformed bad => [StoredMetric.Boolean(
                Parses, false, $"the diagnosis could not be read: {bad.ParseError}", failed: true, "Unacceptable")],
            DiagnosisReading.Absent => [StoredMetric.Boolean(
                Parses, false, "the answer carries no JSON object, and the contract asks for one",
                failed: true, "Unacceptable")],
            _ => [StoredMetric.Boolean(
                Parses, false, "the reading is of a kind this scorer does not know", failed: true, "Unacceptable")],
        };

    private static StoredMetric Followed() =>
        StoredMetric.Boolean(Parses, true, "the answer carries a readable diagnosis", failed: false, "Good");

    private static IEnumerable<StoredMetric> AnchorMetrics(
        Diagnosis diagnosis, IReadOnlyList<SourceAnchor> causal, IReadOnlyList<SourceAnchor> symptoms)
    {
        yield return Recall(diagnosis, causal);
        yield return Aim(diagnosis, causal);

        // The trap exists only where the task authored it: a question with no symptom anchors has no
        // "manifests here, caused there" distinction to assert, and an always-passing metric would read
        // as evidence the trap was survived.
        if (symptoms.Count > 0)
        {
            yield return Trap(diagnosis, causal, symptoms);
        }
    }

    /// <summary>How much of the truth the diagnosis reached — over the anchors the TRUTH names, so a
    /// diagnosis that found one of three causes has not scored a perfect 1.0.</summary>
    private static StoredMetric Recall(Diagnosis diagnosis, IReadOnlyList<SourceAnchor> causal)
    {
        if (causal.Count == 0)
        {
            return StoredMetric.Text(
                AnchorRecall, "nothing to find", "the task names no causal anchors", failed: false, "Unknown");
        }

        var reached = causal.Count(truth => diagnosis.Anchors.Any(named => Reaches(named, truth)));
        var recall = (double)reached / causal.Count;

        return StoredMetric.Numeric(
            AnchorRecall,
            recall,
            $"{reached} of {causal.Count} causal anchor(s) reached",
            failed: reached == 0,
            Rating(recall));
    }

    /// <summary>How much of what the diagnosis NAMED was right — the shotgun guard. Text, not a zero,
    /// when nothing was named: an empty aim cannot be computed, and "0 of 0" would read as arithmetic
    /// where the fact is a refusal to point.</summary>
    private static StoredMetric Aim(Diagnosis diagnosis, IReadOnlyList<SourceAnchor> causal)
    {
        if (diagnosis.Anchors.Count == 0)
        {
            return StoredMetric.Text(
                Precision, "no anchors named",
                "a diagnosis that points at nothing cannot be checked for aim", failed: true, "Unacceptable");
        }

        var correct = diagnosis.Anchors.Count(named => causal.Any(truth => Reaches(named, truth)));
        var precision = (double)correct / diagnosis.Anchors.Count;

        return StoredMetric.Numeric(
            Precision,
            precision,
            $"{correct} of {diagnosis.Anchors.Count} named anchor(s) are causal",
            failed: correct == 0,
            Rating(precision));
    }

    /// <summary>Fired when the diagnosis reached a symptom anchor and no causal one: it pointed at
    /// where the defect MANIFESTS, not where it is caused — the failure the investigate arm exists to
    /// catch, and invisible unless something asserts it.</summary>
    private static StoredMetric Trap(
        Diagnosis diagnosis, IReadOnlyList<SourceAnchor> causal, IReadOnlyList<SourceAnchor> symptoms)
    {
        var reachedSymptom = diagnosis.Anchors.Any(named => symptoms.Any(truth => Reaches(named, truth)));
        var reachedCause = diagnosis.Anchors.Any(named => causal.Any(truth => Reaches(named, truth)));
        var fired = reachedSymptom && !reachedCause;

        return StoredMetric.Boolean(
            SymptomOnly,
            fired,
            fired
                ? "the diagnosis reached only where the defect manifests — the cause was not named"
                : "the diagnosis was not caught pointing at the symptom alone",
            failed: fired,
            fired ? "Unacceptable" : "Good");
    }

    /// <summary>Whether one named place reaches one authored anchor: same path, and then member
    /// identity, overlapping line claims, or a truth that is itself a whole-file claim. A named anchor
    /// with neither member nor lines is a bare file claim and reaches ONLY a whole-file truth —
    /// "somewhere in this file" must not answer for a member.</summary>
    private static bool Reaches(DiagnosisAnchor named, SourceAnchor truth) =>
        AnchorMatching.SamePath(named.Path, truth.FilePath)
        && (AnchorMatching.SameMember(named.Member, truth.MemberKey)
            || (!named.Lines.IsWhole && !truth.Lines.IsWhole && Overlaps(named.Lines, truth.Lines))
            || IsWholeFileClaim(truth));

    /// <summary>A truth that claims the whole file is one with NEITHER a member NOR a line span — a
    /// deleted file, or a genuinely file-level expectation. <see cref="SourceAnchor.IsWholeFile"/> asks
    /// only about the member, which is right for retrieval (a hit returns a region of a file) and wrong
    /// here: FixDiff-derived truth carries a span and no member name, and reading it as whole-file
    /// would let "somewhere in this file" answer for a specific touched region.</summary>
    private static bool IsWholeFileClaim(SourceAnchor truth) =>
        truth.MemberKey.Length == 0 && truth.Lines.IsWhole;

    /// <summary>Overlap, not coverage: a diagnosis pointing into any part of the touched span has found
    /// the place. Coverage is the hit-matching rule because a HIT returns code; a diagnosis only points.</summary>
    private static bool Overlaps(LineSpan named, LineSpan truth) =>
        named.Start <= truth.End && truth.Start <= named.End;

    private static string Rating(double value) =>
        value >= 1 ? "Exceptional" : value > 0 ? "Average" : "Unacceptable";
}
