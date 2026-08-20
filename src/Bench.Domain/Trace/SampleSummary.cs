namespace Bench.Domain.Trace;

/// <summary>What a run of samples said — four numbers, never one.
/// <para>
/// <b>The count is not decoration.</b> A maximum over two samples and a maximum over two thousand are
/// different claims, and a report that cannot tell them apart will rank on the first. It is the same rule
/// <c>MetricByDimension.Legs</c> carries for legs, applied to readings.
/// </para>
/// <para>
/// <b>And nothing here can express "unknown" as a zero.</b> On a machine with no vendor tool the normal
/// answer is that nobody read anything, and rendering that as <c>0 %</c> is the defect <see cref="Captured"/>
/// was introduced to prevent — in the one place it had not been applied. <see cref="Nothing"/> is that state,
/// and it is a different value from a summary of samples that all read zero.
/// </para></summary>
/// <param name="Reason">Why nothing was sampled, when nothing was. The text an operator acts on: "no
/// nvidia-smi on this host" and "the sampler was disabled" lead to different next steps.</param>
public readonly record struct SampleSummary(
    bool Sampled,
    double Minimum,
    double Maximum,
    double Mean,
    int Count,
    string Reason)
{
    public static SampleSummary Nothing(string reason) => new(false, 0, 0, 0, 0, reason);

    /// <summary>A summary of real readings. An empty set is <see cref="Nothing"/> rather than a summary of
    /// zeroes: a sampler that ran and collected nothing has still measured nothing.</summary>
    public static SampleSummary Of(IReadOnlyList<double> readings) =>
        readings.Count == 0
            ? Nothing("the sampler produced no readings for this leg")
            : new(true, readings.Min(), readings.Max(), readings.Average(), readings.Count, string.Empty);

    public string Describe =>
        Sampled
            ? $"{Minimum:0.##}–{Maximum:0.##} (mean {Mean:0.##}, {Count} sample(s))"
            : $"not sampled — {Reason}";
}

/// <summary>Whether a VRAM reading may be ATTRIBUTED to this leg, or only observed beside it.
/// <para>
/// The rule is the sidecar's, and it wrote down why: <em>the obvious answer — sample before and after,
/// publish the delta — rests on nothing</em>, so it publishes a figure only when the build was alone
/// (<c>dew_flow_sidecar_rust · src/vram.rs</c>). This benchmark inherits it rather than re-deriving it.
/// </para>
/// <para>
/// The distinction is not pedantry here: concurrent passes once co-loaded a coder and an embedder, 30 GB on
/// a 32 GB card. A bare "VRAM used" cannot tell *we used 20 GB* from *somebody else held 20 GB and we got
/// the rest*, and the two lead to opposite conclusions about the configuration under test.
/// </para></summary>
public enum VramAttribution
{
    /// <summary>Nobody read the card. The <see cref="SampleSummary.Sampled"/> false case, carried here too so
    /// a reader never has to consult two fields to learn there is no number.</summary>
    NotSampled,

    /// <summary>Read while something else also held the accelerator. A real number about the CARD, and not a
    /// statement about this leg — never averaged with the attributed ones.</summary>
    Observed,

    /// <summary>This process held the accelerator alone for the whole leg, so the figure is this leg's. Only
    /// the lease can establish that (<c>todo/PLAN_variant_matrix.md</c> §3.4b); without one, nothing may
    /// claim this state.</summary>
    Attributed,
}

/// <summary>A VRAM reading and what may be said about it.</summary>
public readonly record struct VramReading(SampleSummary Bytes, VramAttribution Attribution, string SharedWith)
{
    public static VramReading NotSampled(string reason) =>
        new(SampleSummary.Nothing(reason), VramAttribution.NotSampled, string.Empty);

    /// <summary>Read while the accelerator was shared. <paramref name="sharedWith"/> names what else was
    /// resident — the answer to "then whose 20 GB was it", which is the only thing that makes an observed
    /// figure worth storing at all.</summary>
    public static VramReading Observed(SampleSummary bytes, string sharedWith) =>
        new(bytes, VramAttribution.Observed, sharedWith);

    public static VramReading Attributed(SampleSummary bytes) =>
        new(bytes, VramAttribution.Attributed, string.Empty);

    public string Describe =>
        Attribution switch
        {
            VramAttribution.Attributed => $"{Bytes.Describe} — this leg alone on the accelerator",
            VramAttribution.Observed =>
                $"{Bytes.Describe} — OBSERVED, the card was shared"
                + (SharedWith.Length > 0 ? $" with {SharedWith}" : string.Empty),
            _ => Bytes.Describe,
        };
}
