namespace Bench.Delivered;

/// <summary>Every number this module accepted on another corpus's evidence, in ONE place.
///
/// <para><b>Why they are gathered rather than declared where they are used.</b> The question a reader has
/// is *"which numbers here are ours?"*, and it must be answerable by reading one file rather than by
/// auditing a module. Each constant below was fitted on a PHP/JS production corpus — 223 scored runs over
/// 1,614 units — and none of them has been re-measured on this project's material. Until the
/// recalibration arm runs, everything derived from them carries an <em>inherited calibration</em> badge,
/// and that badge is a claim about THESE numbers.</para>
///
/// <para>A constant that gets re-measured here moves out of this class. That is the whole mechanism: the
/// file shrinks as the evidence becomes ours.</para>
/// </summary>
public static class Inherited
{
    /// <summary>What a declared repeat of a same-file sibling is worth — the anchor scale's *"expose an
    /// existing value through one more contract, no new logic"* rung.
    /// <para>
    /// <b>Inherited from:</b> run #15105 paid 5 for a unit whose own justification named it a mirror,
    /// where the published anchor for "no new logic" is 2.
    /// </para></summary>
    public const int NearDuplicateCap = 2;

    /// <summary>How many scored units each unit of diff-side evidence admits.
    /// <para>
    /// <b>Inherited from:</b> 29.4 % of all score ever awarded came from adjudicator rescues rather than
    /// the matcher, concentrated in small changes — run #13862 was a ONE-LINE diff that scored 79.0
    /// because 22 of its 23 reference points were rescued. 2× was chosen over 3× and 4× by measurement: it
    /// is the strongest correction that still leaves every large legitimate run within 6 % of its
    /// published score. Effect: #13862 79 → 9, #14847 36.5 → 13, and score-versus-size rank correlation
    /// rose from +0.59 to +0.71.
    /// </para></summary>
    public const int RescueAllowancePerEvidenceUnit = 2;

    /// <summary>The vocabulary by which a unit declares itself a repeat. Taken from the weigher's own
    /// <c>why</c> texts across the 223 measured runs — every phrase appeared verbatim on units whose
    /// reasoning named another unit as the original.
    /// <para>
    /// <b>Inherited from:</b> the same corpus. This is the constant most likely to need re-measuring here,
    /// because it is the only one whose validity depends on how a DIFFERENT model phrases itself.
    /// </para></summary>
    public const string DeclaredMirrorVocabulary =
        @"mirror|same (established )?pattern|following the same|analogous to|duplicate of"
        + @"|variant of|parallel to|\bsibling\b|identical to";

    /// <summary>The base coverage a decomposition must account for, before the size bands widen it.
    /// <para><b>Inherited from:</b> the 161-analysis measurement behind the source's coverage gate.</para></summary>
    public const decimal CoverageThreshold = 0.70m;

    /// <summary>How much of the band one covered line may forgive.
    /// <para>
    /// <b>Inherited from:</b> coverage is QUANTISED — with L cleaned lines it can only take values k/L, so
    /// on a 16-line change the reachable values straddle 70 % at 68.75 % and 75 % and the threshold is not
    /// achievable at all. #14707 hard-failed twice at 68.75 % having improved from 56 % on its re-ask,
    /// rejected for 1.2 points it could not have earned. At 0.20 under the 70 % band this reproduces the
    /// original flat 0.50 floor exactly, so it GENERALISES that rule rather than adding a second one.
    /// </para></summary>
    public const decimal MaxTolerance = 0.20m;

    /// <summary>Slack for the threshold comparison only, because both sides are computed from the same
    /// integers by different routes.
    /// <para>
    /// <b>Inherited from:</b> at 580 cleaned lines the one-line-short coverage (318/580) and the threshold
    /// (0.55 − 1/580) are the same number mathematically and differ in the last bit. A bare comparison
    /// rejected a reply the rule accepts, and did so at only a scattering of sizes — the worst way to be
    /// wrong, because it looks like noise rather than a bug.
    /// </para></summary>
    public const decimal BoundaryEpsilon = 0.000000001m;

    /// <summary>Below this many coverable lines there is no decomposition to regulate.
    /// <para>
    /// <b>Inherited from:</b> #14735 is a 6-line change whose coverable universe is 3 lines — the rest are
    /// braces — and its two steps quoted ALL THREE, 100 % of what can be covered, while a flat ratio failed
    /// it at 50 % three independent times.
    /// </para></summary>
    public const int MinCoverableLines = 5;

    /// <summary>Below this many cleaned lines the tolerance cannot do its job.
    /// <para>
    /// <b>Inherited from:</b> the tolerance forgives exactly one covered line, so for the gate to tell "one
    /// line short" from "materially short", one line must be a small share of the band. At 6 cleaned lines
    /// one line is 16.7 %; at 20 it is 5 %. Across every measured analysis each real shortfall sat at
    /// ≤ 2.4 % per line, and only #14735 was above 5 %.
    /// </para></summary>
    public const int MinCleanLoc = 20;

    /// <summary>The coverage a change of a given size is held to.
    /// <para>
    /// <b>Inherited from:</b> ungated median coverage runs 105 % at 26–100 cleaned lines, 55 % at 101–300,
    /// <b>36 % at 301–800 and 27 % above 800</b>, and the mechanism was measured independently — credited
    /// coverage falls as <c>LOC^-0.286</c>. A flat 70 % asks the large bands for something they do not
    /// produce: #15058 (1032 lines) climbed 26 % → 64 % on a re-ask and was discarded anyway. Under their
    /// own bands both it and #15016 pass, while #13918 at 21 % still fails — the loosening is not an
    /// amnesty.
    /// </para>
    /// <para>
    /// <b>Known limitation, carried across deliberately rather than quietly fixed:</b> the source's own
    /// report says *"the coverage gate still loosens with size"*. A large change is held to less, and
    /// nothing here has established that it should be. Recorded so a recalibration knows what to question.
    /// </para></summary>
    public static IReadOnlyList<(decimal UpToCleanLines, decimal Band)> CoverageBands { get; } =
    [
        (300m, 0.70m),
        (800m, 0.55m),
        (decimal.MaxValue, 0.45m),
    ];

    /// <summary>Causes the cap rule admits: work that genuinely is not decomposable into logical units.
    /// Presence of one is evidence of a real cause; ABSENCE is not disqualifying on its own, because a real
    /// cause outside the list must remain sayable.
    /// <para><b>Inherited from:</b> the vocabulary of the source's own measured cap reasons.</para></summary>
    public static IReadOnlyList<string> AdmissibleCapCauses { get; } =
    [
        "boilerplate", "generated", "autogenerated", "auto-generated", "scaffold",
        "rename", "renaming", "renamed", "import", "imports", "namespace",
        "format", "formatting", "reformat", "whitespace", "indentation",
        "dependency injection", "di ", "constructor", "getter", "setter", "accessor",
        "repetitive", "identical", "uniform", "mechanical", "same pattern", "one per",
        "translation", "fixture", "test data", "config", "mapping",
    ];

    /// <summary>Phrases that RESTATE a verdict instead of explaining it. A reason built only from these is
    /// the formal answer the rule rejects.
    /// <para><b>Inherited from:</b> the same corpus of cap reasons.</para></summary>
    public static IReadOnlyList<string> EmptyCapPhrases { get; } =
    [
        "cannot be decomposed further", "no further decomposition", "coverage cannot be increased",
        "not possible to reach 70", "already decomposed", "the remaining code",
        "nothing more to add", "as required", "n/a", "none",
    ];

    /// <summary>A reason shorter than this is not an explanation.
    /// <para><b>Inherited from:</b> the source's measured floor.</para></summary>
    public const int MinReasonChars = 40;

    /// <summary>…and one with fewer words than this is not either.</summary>
    public const int MinReasonWords = 8;

    /// <summary>The badge every figure derived from the constants above must carry until the
    /// recalibration arm runs on this project's own corpus.</summary>
    public const string Badge = "inherited calibration";
}
