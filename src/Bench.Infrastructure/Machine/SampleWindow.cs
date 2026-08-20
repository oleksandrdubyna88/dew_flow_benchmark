using System.Collections.Concurrent;

namespace Bench.Infrastructure.Machine;

/// <summary>A bounded, time-ordered buffer of readings.
/// <para>
/// Its own type so the two rules that matter can be checked without a clock, a timer, or a second of waiting:
/// nothing older than the window survives, and asking for an interval does not consume it. Both were learned
/// in this repository rather than anticipated — a collection that only grows is the shape two
/// <c>GetOrAdd</c>-forever maps already had here, and a destructive drain would let one leg eat the evidence
/// of another running beside it.
/// </para></summary>
public sealed class SampleWindow<T>(Func<T, DateTimeOffset> takenAt)
    where T : struct
{
    private readonly ConcurrentQueue<T> _samples = new();

    public int Count => _samples.Count;

    /// <summary>Takes a reading, or nothing when the reader had none to give — a failed probe is an absent
    /// sample rather than a zero, which is what keeps a summary able to say <em>not sampled</em>.</summary>
    public void Add(T? sample)
    {
        if (sample is { } taken)
        {
            _samples.Enqueue(taken);
        }
    }

    /// <summary>Readings in <c>[from, to)</c>. Half-open, matching <c>LegSampling</c>: a reading at the
    /// closing instant belongs to whatever comes next, so two back-to-back legs cannot both claim it.</summary>
    public IReadOnlyList<T> Between(DateTimeOffset from, DateTimeOffset to) =>
        [.. _samples.Where(sample => takenAt(sample) >= from && takenAt(sample) < to)];

    /// <summary>Drops everything older than the cutoff. Called on every tick, so the buffer's size is a
    /// function of the window rather than of how long the process has been alive.</summary>
    public void Retire(DateTimeOffset cutoff)
    {
        while (_samples.TryPeek(out var oldest) && takenAt(oldest) < cutoff)
        {
            _samples.TryDequeue(out _);
        }
    }
}
