namespace Bench.Domain;

/// <summary>A reproducible stream of draws from a seed — SplitMix64 (Steele, Lea and Flood, 2014), spelled out here so the sequence is a
/// documented algorithm rather than a runtime's implementation detail.
/// <para>
/// Why not <see cref="Random"/>: a pre-registered analysis (the scouts' bootstrap, plan §3.8) must give the SAME interval for the same
/// seed on any machine and any runtime, and <c>new Random(seed)</c>'s sequence is not a documented contract — the same reason
/// <c>ScoutSchedule</c> orders by <see cref="StableHash"/>. A class with state because a draw advances it; never shared across threads.
/// </para></summary>
public sealed class SeededDraws(ulong seed)
{
    private ulong _state = seed;

    public SeededDraws(int seed)
        : this(unchecked((ulong)(long)seed))
    {
    }

    /// <summary>The next 64 bits.</summary>
    public ulong Next()
    {
        unchecked
        {
            _state += 0x9E3779B97F4A7C15UL;
            var z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    /// <summary>A draw in <c>[0, count)</c>. The modulo's bias is below 2⁻⁵⁰ for any count a bootstrap uses (a few dozen clusters), far
    /// under anything a percentile can see.</summary>
    public int Below(int count) =>
        count > 0 ? (int)(Next() % (ulong)count) : throw new ArgumentOutOfRangeException(nameof(count), count, "a draw needs at least one choice");
}
