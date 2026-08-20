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

/// <summary>What a whole run's legs saw, folded from what each of them claimed.
/// <para>
/// <b>Peaks, never averages.</b> A mean across legs would answer nothing: legs differ in length, in what
/// else was running, and in whether anybody sampled them at all, so their means are not commensurable. The
/// highest reading any leg saw IS well defined, and it is the number a capacity question actually asks —
/// <em>did this campaign ever come close to filling the card</em>.
/// </para></summary>
/// <param name="LegsSampled">How many legs carry any reading. Beside <paramref name="Legs"/> because a peak
/// drawn from two legs of two hundred is a different claim from one drawn from all of them, and a reader
/// holding only the peak cannot tell.</param>
public readonly record struct RunLoad(int Legs, int LegsSampled, SampleSummary PeakRamBytes, VramReading PeakVram)
{
    public static RunLoad None { get; } = new(0, 0, SampleSummary.Nothing("no leg of this run was sampled"), VramReading.NotSampled("no leg of this run was sampled"));

    public string Describe =>
        LegsSampled == 0
            ? $"not sampled — {PeakRamBytes.Reason}"
            : $"{LegsSampled} of {Legs} leg(s) sampled · peak ram {PeakRamBytes.Describe} · peak vram {PeakVram.Describe}";
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

    /// <summary>The run's peaks, from the legs that were sampled.
    /// <para>
    /// A VRAM peak inherits the WEAKEST attribution among the legs that contributed: if any of them merely
    /// observed the card, the run's peak is observed too. Taking the strongest would let one leg that held
    /// the accelerator alone lend its authority to a figure the rest of the run cannot support.
    /// </para></summary>
    public static RunLoad Across(IReadOnlyList<LegLoad> legs)
    {
        var sampled = legs.Where(l => l.Any).ToList();

        if (sampled.Count == 0)
        {
            return RunLoad.None with { Legs = legs.Count };
        }

        var ram = sampled.Where(l => l.RamBytesUsed.Sampled).Select(l => l.RamBytesUsed).ToList();
        var vram = sampled.Where(l => l.Vram.Bytes.Sampled).ToList();

        return new RunLoad(
            legs.Count,
            sampled.Count,
            Peak(ram, "no leg of this run carries a memory reading"),
            PeakVram(vram));
    }

    private static SampleSummary Peak(IReadOnlyList<SampleSummary> summaries, string whenEmpty) =>
        summaries.Count == 0
            ? SampleSummary.Nothing(whenEmpty)
            : new SampleSummary(
                true,
                summaries.Min(s => s.Minimum),
                summaries.Max(s => s.Maximum),
                summaries.Sum(s => s.Mean * s.Count) / Math.Max(1, summaries.Sum(s => s.Count)),
                summaries.Sum(s => s.Count),
                string.Empty);

    private static VramReading PeakVram(IReadOnlyList<LegLoad> legs)
    {
        if (legs.Count == 0)
        {
            return VramReading.NotSampled("no leg of this run carries an accelerator reading");
        }

        var peak = Peak([.. legs.Select(l => l.Vram.Bytes)], string.Empty);
        var shared = legs.Where(l => l.Vram.Attribution != VramAttribution.Attributed).ToList();

        return shared.Count == 0
            ? VramReading.Attributed(peak)
            : VramReading.Observed(peak, shared.Select(l => l.Vram.SharedWith).FirstOrDefault(w => w.Length > 0) ?? string.Empty);
    }

    private static SampleSummary Summarise(IEnumerable<double> readings, string whenEmpty)
    {
        var values = readings.ToList();

        return values.Count == 0 ? SampleSummary.Nothing(whenEmpty) : SampleSummary.Of(values);
    }
}
