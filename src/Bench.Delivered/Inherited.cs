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

    /// <summary>The badge every figure derived from the constants above must carry until the
    /// recalibration arm runs on this project's own corpus.</summary>
    public const string Badge = "inherited calibration";
}
