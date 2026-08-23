namespace Bench.Delivered;

/// <summary>The scale a weigher prices a step on, and the protocol string every score carries with it.
///
/// <para><b>The zero band is the whole instrument.</b> Its inflation test measured why: ten times the
/// cleaned lines, added as reachable code that cannot change what the change DOES, bought ×1.7 the score.
/// The weigher was not fooled — it priced the padding at 1 and 2 and said so in its own words
/// (<i>"a plain fluent data holder, no logic"</i>, <i>"both branches return the same kind — no effective
/// decision"</i>) — and it had no number to record that judgement with, because the floor was 1 and
/// <b>a floor is a payment</b>. 158 padded steps at 1–2 out-totalled the real work beside them and supplied
/// 46 % of the inflated score. With the zero band the same padding bought ×0.88, and 160 of 160 padded
/// steps landed on zero.</para>
///
/// <para><b>The ten scale lines ship verbatim, and that is deliberate.</b> Their stability is measured:
/// models quote the matching line back and land on its score reproducibly across 2,088 production units
/// and 452 harness steps. Rewriting them for taste would discard exactly that evidence and leave a scale
/// whose only claim is that it reads nicely.</para>
/// </summary>
public static class WeightingProtocol
{
    /// <summary>What every score produced under this scale records, source acknowledged INSIDE it.
    /// <para>
    /// A score is comparable only with scores produced by the same protocol, so the string is part of the
    /// measurement rather than a label on it. It names what was inherited, which is what lets a console
    /// render the <see cref="Inherited.Badge"/> without a second table saying which runs deserve one — and
    /// what makes the badge die by a string change when the recalibration arm finally runs.
    /// </para></summary>
    public const string Protocol =
        "delivered-work-v1 (anchors inherited: scoreMeter diff-weighting-v3 / diff-only-gated-zero-2026-08-13)";

    /// <summary>Zero, deliberately. A floor of 1 is a payment for work that serves nothing.</summary>
    public const int MinScore = 0;

    public const int MaxScore = 10;

    /// <summary>The band no run has ever used — zero of 2,540 scored units, production and harness
    /// together, landed on 10. Named rather than left implicit, because *"the anchors stop at 9"* is a
    /// fact a reader must not have to infer from a list.</summary>
    public const int BandWithoutExample = 10;

    /// <summary>The zero band, kept OUT of <see cref="AnchorScale"/> on purpose so the ten inherited lines
    /// stay byte-identical to the block whose stability was measured.</summary>
    public const string ZeroAnchor =
        "  0  the step serves nothing: a holder, interface, registry or chain that no behavior the\n"
        + "     change delivers reaches, consults or depends on";

    /// <summary>The one rule that keeps zero from eating the bottom of the scale.
    /// <para>
    /// The failure mode to design against is not under-use, it is OVER-use: *"no new logic"* describes a
    /// translation label for a shipped checkbox as accurately as it describes a registry nothing reads, and
    /// the first is a real 1 with thousands of precedents. So the band is defined by <em>what reaches
    /// it</em>, never by how small or how mechanical it looks.
    /// </para></summary>
    public const string ZeroRule =
        "0 is for work that serves nothing, not for work that is small. A one-line label, constant or "
        + "mapping entry that a feature THIS change delivers actually uses is a 1. Score 0 only when "
        + "nothing the change delivers would behave differently if the step were deleted outright — a data "
        + "holder nobody reads, an interface with one implementer and no caller, a registry or chain "
        + "assembled and never consulted, a branch whose arms return the same thing.\n"
        + "Read \"behave differently\" as including the failure paths, not only the successful one. A "
        + "declaration the runtime enforces — a native parameter or return type, a not-null column, a "
        + "narrowed signature — changes what happens when something is wrong, so it is a 1, however "
        + "mechanical it looks. Deleting code is a 1 when the deletion had to be established: removing a "
        + "query, a method or a branch means someone proved nothing reaches it. Deleting a comment, a "
        + "docblock, an unused import or whitespace proves nothing and stays a 0.";

    /// <summary>The ten scale lines, character-for-character as the measured prompt carries them.</summary>
    public const string AnchorScale =
        "  1  a one-line declarative change: add a field to a mapping, a constant to a list, a label\n"
        + "  2  expose an existing value through one more contract or template — no new logic\n"
        + "  3  a new simple field end to end: storage, contract, display, following an existing pattern\n"
        + "  4  a new endpoint or command that follows an existing one closely\n"
        + "  5  a form, popup or report with several fields and ordinary validation\n"
        + "  6  new business logic with branching that must agree with existing behavior\n"
        + "  7  a calculation whose result other parts of the system already depend on\n"
        + "  8  a multi-step algorithm with accumulating state, or a cross-service contract change\n"
        + "  9  a formula or process spelled out over worked examples, with carry-over between steps\n"
        + " 10  reworking a core rule the rest of the system is built on";

    /// <summary>The scale as a weigher is shown it: the zero band above the ten inherited lines, then the
    /// rule that keeps zero in its place.</summary>
    public static string Scale => $"{ZeroAnchor}\n{AnchorScale}\n\n{ZeroRule}";

    /// <summary>Whether a score is on the scale at all. A weigher that answers 11 or −1 has not produced a
    /// low-confidence reading — it has produced something this protocol cannot record, and the parser must
    /// refuse rather than clamp.</summary>
    public static bool IsOnScale(int score) => score is >= MinScore and <= MaxScore;

    /// <summary><b>The nineteen few-shot examples are NOT carried.</b> They quote another repository's code
    /// and name its pull requests, so as few-shots against a .NET target they would teach the shape of a
    /// Symfony diff rather than the meaning of a band. The source's own admission rule is the one to
    /// follow: examples enter only where history agrees, and this project has no history yet. The SCALE
    /// survives without them because what was measured stable is the wording of the ten lines — models
    /// quote the matching line back — not the examples beside it.</summary>
    public const string WhyNoExamples =
        "few-shot examples are not inherited: they quote another repository, and this project's own "
        + "history has not accumulated agreement yet";
}
