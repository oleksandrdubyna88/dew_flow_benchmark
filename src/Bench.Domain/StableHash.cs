using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Bench.Domain;

/// <summary>Hashing for values that must be identical across processes, machines and years.
/// <para>
/// <see cref="object.GetHashCode"/> is deliberately never used here. It is randomised per process, and
/// both callers in this domain would be silently wrong with it: a suite version hash that changes on
/// restart is not a version, and a selection/held-out split that re-assigns on re-run destroys the
/// guard it exists for — the second failure would be invisible, because each individual run would
/// still look internally consistent.
/// </para></summary>
public static class StableHash
{
    /// <summary>The canonical digest of an already-canonical string. Lower-case hex, 64 characters.</summary>
    public static string Of(string canonical) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));

    /// <summary>A stable bucket in <c>[0, buckets)</c>. Used for the seed split, where the same
    /// question must land in the same half on every machine, forever.</summary>
    public static Outcome<int> Bucket(string canonical, int buckets)
    {
        if (buckets <= 0)
        {
            return Outcome<int>.Failure($"buckets must be positive, got {buckets}");
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        var head = BinaryPrimitives.ReadUInt32BigEndian(digest);
        return Outcome<int>.Success((int)(head % (uint)buckets));
    }
}
