namespace Bench.Delivered;

/// <summary>What the gate decided to do with a decomposition.</summary>
public enum CoverageAction
{
    /// <summary>Score it.</summary>
    Accept,

    /// <summary>Re-ask ONCE, naming the shortfall.</summary>
    Retry,

    /// <summary>Refuse it. Recorded, never silently scored.</summary>
    Fail,
}

/// <summary>Why a reply was accepted, so a CAPPED run is never indistinguishable from a clean one.
/// <para>
/// Six names rather than a boolean, and that is the point of the whole type: "accepted" covers a reply that
/// met its band, one that fell short with a good reason, one that fell short with an unrecognised reason,
/// and one the arithmetic could not judge at all. Collapsing those into <c>true</c> would publish four
/// different claims as one.
/// </para></summary>
public static class CoverageStatus
{
    /// <summary>Met the band for its size.</summary>
    public const string Passed = "passed";

    /// <summary>Under the band, but the cap was justified by a cause the rule admits.</summary>
    public const string CappedSubstantive = "capped-substantive";

    /// <summary>Under the band, cap justified by a cause outside the admissible list — needs a human read.</summary>
    public const string CappedBorderline = "capped-borderline";

    /// <summary>Below the gate's RESOLUTION: neither passed nor failed, because both would overclaim.</summary>
    public const string TooThinToGate = "too-thin-to-gate";

    /// <summary>Under the band after a re-ask, with no usable reason.</summary>
    public const string HardFailure = "hard-failure";

    /// <summary>Under the band on the first attempt; a re-ask follows.</summary>
    public const string UnderThreshold = "under-threshold";
}

/// <summary>How a cap reason reads to the checks a machine can actually apply.</summary>
public enum CapReason
{
    /// <summary>No cap was claimed.</summary>
    NotClaimed,

    /// <summary>Names a cause the rule admits.</summary>
    Substantive,

    /// <summary>Names a cause outside the admissible list — accepted, FLAGGED for review. Sincerity is not
    /// machine-decidable, and a real cause nobody listed must stay sayable.</summary>
    Borderline,

    /// <summary>Absent, too short, or a restatement of the verdict.</summary>
    Empty,
}

/// <param name="Coverage">What the decomposition's evidence accounted for, as a share.</param>
/// <param name="Threshold">What it was actually judged against — the band, less the one-line slack.</param>
/// <param name="Band">The band for this size, BEFORE the slack. Both are carried because a reply that met
/// the band and one that only met the quantisation-tolerant threshold are different readings.</param>
public sealed record CoverageVerdict(
    CoverageAction Action,
    string Status,
    decimal Coverage,
    decimal Threshold,
    decimal Band,
    bool Capped,
    CapReason Reason,
    string Note);

/// <summary>The coverage gate: a decomposition is only as good as the share of the change its own evidence
/// accounts for.
///
/// <para><b>The measurement that forced it.</b> Two changes of exactly 116 cleaned lines were decomposed
/// into 16 steps accounting for 75 % of the code and 3 steps accounting for 16 % — a 5.9× score gap for no
/// reason a reader would accept. Removing the surrounding ticket did not fix decomposition granularity, so
/// the gate makes coverage a CONDITION of the reply rather than an invisible property of it.</para>
///
/// <para><b>What feeds the numerator is this project's decision, not an inherited one.</b> The source
/// divided Σ grain by cleaned LOC. Grain itself is deliberately NOT ported — its own report says *"Σ grain
/// still cannot tell padding from work"* — so what this gate takes is a neutral <em>accounted</em> figure,
/// and choosing how the stage computes it is an open question that belongs with the stage. The gate's
/// arithmetic, its bands and its six statuses are what came across.</para>
///
/// <para>Every constant lives in <see cref="Inherited"/> with the measurement behind it, including the
/// known limitation carried across rather than quietly fixed: the gate still loosens with size.</para>
/// </summary>
public static class CoverageDecision
{
    /// <summary>The share of the coverable universe a decomposition accounts for. A zero denominator
    /// answers 0 rather than throwing: a change with nothing coverable cannot be covered, and must not pass
    /// by accident.</summary>
    public static decimal CoverageOf(decimal accounted, decimal denominator) =>
        denominator <= 0 ? 0m : accounted / denominator;

    /// <summary>The coverage a change of this size is held to, before the one-line slack.</summary>
    public static decimal BandFor(decimal cleanLoc)
    {
        foreach (var (limit, band) in Inherited.CoverageBands)
        {
            if (cleanLoc <= limit)
            {
                return band;
            }
        }

        return Inherited.CoverageBands[^1].Band;
    }

    /// <summary>What a reply is actually judged against: the band for its size, less one covered line,
    /// capped at <see cref="Inherited.MaxTolerance"/>. Two adjustments, both measured — the band answers
    /// "how much of a change this size does a good decomposition account for", the slack answers "the band
    /// is often not a reachable number".</summary>
    public static decimal EffectiveThreshold(decimal cleanLoc)
    {
        if (cleanLoc <= 0)
        {
            return Inherited.CoverageThreshold;
        }

        var band = BandFor(cleanLoc);

        return Math.Max(band - Inherited.MaxTolerance, band - (1m / cleanLoc));
    }

    /// <summary>Why this change is below the gate's resolution, or empty if it is gateable. Checked BEFORE
    /// the threshold: a decomposition the arithmetic cannot judge must be neither passed nor failed,
    /// because both verdicts would be claims the numbers do not support.</summary>
    public static string TooThinToGate(decimal cleanLoc, int? coverableLines)
    {
        if (coverableLines is { } coverable && coverable < Inherited.MinCoverableLines)
        {
            return $"only {coverable} coverable line(s), under {Inherited.MinCoverableLines} — "
                + "there is no decomposition to regulate";
        }

        return cleanLoc > 0 && cleanLoc < Inherited.MinCleanLoc
            ? $"{cleanLoc:0} cleaned lines, so one covered line is {1m / cleanLoc:P0} of coverage — "
                + "the gate cannot tell one line short from materially short"
            : string.Empty;
    }

    /// <summary>How a cap reason reads to the checks a machine can apply: present, long enough, and naming
    /// a cause rather than restating the number.</summary>
    public static (CapReason Reason, string Why) JudgeReason(string? reason)
    {
        var text = (reason ?? string.Empty).Trim();

        return Refuse(text) is { Length: > 0 } refusal
            ? (CapReason.Empty, refusal)
            : Classify(text.ToLowerInvariant());
    }

    private static string Refuse(string text) =>
        text.Length switch
        {
            0 => "no reason given",
            < Inherited.MinReasonChars => $"reason is {text.Length} chars, under the {Inherited.MinReasonChars} minimum",
            _ => Thin(text.ToLowerInvariant()),
        };

    /// <summary>A reason with too few words, or one left with too few once the verdict-restating phrases are
    /// removed. The second check is the one that matters: a fluent paragraph saying "this cannot be
    /// decomposed further, as required" is long, well-formed and explains nothing.</summary>
    private static string Thin(string lowered)
    {
        if (WordsIn(lowered) < Inherited.MinReasonWords)
        {
            return $"reason is {WordsIn(lowered)} words, under the {Inherited.MinReasonWords} minimum";
        }

        var residue = Inherited.EmptyCapPhrases.Aggregate(
            lowered, (text, phrase) => text.Replace(phrase, " ", StringComparison.Ordinal));

        return WordsIn(residue) < Inherited.MinReasonWords
            ? "reason only restates the verdict without naming a cause"
            : string.Empty;
    }

    private static (CapReason, string) Classify(string lowered) =>
        Inherited.AdmissibleCapCauses.Any(cause => lowered.Contains(cause, StringComparison.Ordinal))
            ? (CapReason.Substantive, "names an admissible cause")
            : (CapReason.Borderline, "names a cause outside the admissible list — needs a human read");

    /// <summary>The gate's decision for one decomposition.</summary>
    /// <param name="accounted">What the decomposition's evidence accounts for — see the type note on why
    /// this is not inherited.</param>
    /// <param name="cleanLoc">The cleaned churn figure. Dividing by the decomposition's OWN universe was
    /// tried and rejected on measurement: it scores a 3-step decomposition of a 116-line change at 106 %
    /// and waves it through, because only 17 of those lines are coverable.</param>
    /// <param name="coverableLines">How many lines are coverable at all. Used only by the size floor;
    /// <c>null</c> skips that check.</param>
    public static CoverageVerdict Evaluate(
        decimal accounted,
        decimal cleanLoc,
        bool capped,
        string? reason,
        int attempt,
        int maxAttempts = 2,
        int? coverableLines = null)
    {
        var coverage = CoverageOf(accounted, cleanLoc);
        var threshold = EffectiveThreshold(cleanLoc);
        var band = BandFor(cleanLoc);
        var (judged, why) = capped ? JudgeReason(reason) : (CapReason.NotClaimed, "no cap claimed");

        CoverageVerdict Verdict(CoverageAction action, string status, string note) =>
            new(action, status, coverage, threshold, band, capped, judged, note);

        if (TooThinToGate(cleanLoc, coverableLines) is { Length: > 0 } thin)
        {
            return Verdict(CoverageAction.Accept, CoverageStatus.TooThinToGate, thin);
        }

        if (coverage >= threshold - Inherited.BoundaryEpsilon)
        {
            // A cap claimed on a reply that already meets the threshold is not an error, but it is noise
            // worth recording: the model apologised for work it had in fact done.
            return Verdict(
                CoverageAction.Accept,
                CoverageStatus.Passed,
                (coverage >= band ? "met the threshold" : "met the quantisation-tolerant threshold")
                    + (capped ? ", cap claim ignored" : string.Empty));
        }

        if (capped && judged is CapReason.Substantive or CapReason.Borderline)
        {
            return Verdict(
                CoverageAction.Accept,
                judged == CapReason.Substantive ? CoverageStatus.CappedSubstantive : CoverageStatus.CappedBorderline,
                why);
        }

        return attempt < maxAttempts
            ? Verdict(
                CoverageAction.Retry,
                CoverageStatus.UnderThreshold,
                capped ? why : $"coverage {coverage:P1} below {threshold:P1}")
            : Verdict(
                CoverageAction.Fail,
                CoverageStatus.HardFailure,
                $"coverage {coverage:P1} below {threshold:P1} after {maxAttempts} attempts"
                    + (capped ? $"; cap reason rejected: {why}" : "; no cap claimed"));
    }

    private static int WordsIn(string text)
    {
        var count = 0;
        var inWord = false;

        foreach (var c in text)
        {
            var isWordChar = char.IsLetterOrDigit(c) || c is '\'' or '-' or '_';
            count += isWordChar && !inWord ? 1 : 0;
            inWord = isWordChar;
        }

        return count;
    }
}
