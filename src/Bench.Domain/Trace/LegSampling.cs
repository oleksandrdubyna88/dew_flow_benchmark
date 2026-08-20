namespace Bench.Domain.Trace;

/// <summary>What the machine was doing while one leg ran.
/// <para>
/// Every field can say <em>nobody read this</em>, and on most machines several of them will. That is the
/// point: a leg measured on a host with no accelerator to sample is not a leg whose card was idle.
/// </para></summary>
/// <param name="Window">How long the leg was open. Reported because it is the denominator of every count
/// above it — two samples across ten seconds and two across ten minutes are different evidence.</param>
public readonly record struct LegLoad(
    SampleSummary CpuPercent,
    SampleSummary RamBytesUsed,
    VramReading Vram,
    TimeSpan Window)
{
    public static LegLoad NotSampled(string reason) => new(
        SampleSummary.Nothing(reason),
        SampleSummary.Nothing(reason),
        VramReading.NotSampled(reason),
        TimeSpan.Zero);

    public bool Any => CpuPercent.Sampled || RamBytesUsed.Sampled || Vram.Bytes.Sampled;

    public string Describe =>
        Any
            ? $"cpu {CpuPercent.Describe} · ram {RamBytesUsed.Describe} · vram {Vram.Describe}"
            : $"nothing sampled — {CpuPercent.Reason}";
}

/// <summary>Turning a stream of out-of-band readings into what one leg may claim.
/// <para>
/// Pure, and separate from whatever took the readings, because the judgements are here: which samples belong
/// to this leg, whether there are enough of them to say anything, and — the one that matters most — whether
/// a VRAM figure may be ATTRIBUTED to the leg or only observed beside it.
/// </para>
/// <para>
/// <b>The window is half-open.</b> A sample taken at the instant a leg ended belongs to what came next: legs
/// run back to back, and a boundary that counted both ways would let one reading inflate two legs.
/// </para></summary>
public static class LegSampling
{
    public static LegLoad Over(
        IReadOnlyList<LoadSample> load,
        IReadOnlyList<VramSample> vram,
        DateTimeOffset from,
        DateTimeOffset to,
        bool heldAcceleratorAlone,
        string sharedWith)
    {
        if (to <= from)
        {
            // A leg with no duration cannot own a reading. Not an error — a leg that failed before it began
            // is an ordinary outcome — but it must not inherit the samples of whatever ran before it.
            return LegLoad.NotSampled("the leg had no measurable window");
        }

        var inWindow = load.Where(s => s.TakenAt >= from && s.TakenAt < to).ToList();
        var cardInWindow = vram.Where(s => s.TakenAt >= from && s.TakenAt < to).ToList();

        return new LegLoad(
            Summarise(inWindow.Select(s => s.CpuUtilisationPercent), "no processor reading fell inside this leg"),
            Summarise(inWindow.Select(s => (double)s.RamBytesUsed), "no memory reading fell inside this leg"),
            Vram(cardInWindow, heldAcceleratorAlone, sharedWith),
            to - from);
    }

    /// <summary>The VRAM half, and the only place in this file that decides anything about ownership.
    /// <para>
    /// <b>Attribution is a decision, not a subtraction</b> — the sidecar's own rule
    /// (<c>dew_flow_sidecar_rust · src/vram.rs</c>), inherited rather than re-derived. A figure may be called
    /// this leg's only when nothing else could have put bytes on the card, and the only thing that can
    /// establish that is the accelerator lease. Without one, the number is real and describes the CARD:
    /// <see cref="VramAttribution.Observed"/>, carrying what it was shared with, because "then whose 20 GB
    /// was it" is the only question that makes an observed figure worth keeping.
    /// </para></summary>
    private static VramReading Vram(IReadOnlyList<VramSample> samples, bool alone, string sharedWith)
    {
        if (samples.Count == 0)
        {
            return VramReading.NotSampled(
                "no accelerator reading fell inside this leg — they cost about a second each, so a short leg may catch none");
        }

        var summary = SampleSummary.Of([.. samples.Select(s => (double)s.UsedBytes)]);

        return alone ? VramReading.Attributed(summary) : VramReading.Observed(summary, sharedWith);
    }

    private static SampleSummary Summarise(IEnumerable<double> readings, string whenEmpty)
    {
        var values = readings.ToList();

        return values.Count == 0 ? SampleSummary.Nothing(whenEmpty) : SampleSummary.Of(values);
    }
}
