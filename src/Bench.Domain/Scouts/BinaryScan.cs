using System.Text;

namespace Bench.Domain.Scouts;

/// <summary>What a byte scan found: the deny-list entry's 1-based index and the encoding it was found in, or <see cref="None"/>.</summary>
public sealed record BinaryHit(int Entry, string Encoding)
{
    public static BinaryHit None { get; } = new(0, string.Empty);

    public bool Found => Entry > 0;
}

/// <summary>The deny-list looked for in BYTES (E2 code round): the hygiene scan used to skip the content of a binary blob — a NUL in its first
/// 8 KB — and scan its name only, so an entry inside a .pdb, a zip's directory, a compiled resource or a UTF-16 text file passed. Every entry
/// is searched as its UTF-8 bytes and as its UTF-16LE bytes, ASCII letters case-insensitively; a non-ASCII character matches as it is — a byte
/// scan has no culture to fold it by, and says so rather than guessing. Pure, and named by index only: the value is the thing that must not be
/// repeated.</summary>
public static class BinaryScan
{
    public const string Utf8 = "UTF-8";

    public const string Utf16Le = "UTF-16LE";

    /// <summary>The first entry (lowest index) found anywhere in <paramref name="bytes"/>, in either encoding — UTF-8 looked for first.</summary>
    public static BinaryHit FirstIn(ReadOnlySpan<byte> bytes, IReadOnlyList<string> entries)
    {
        for (var at = 0; at < entries.Count; at++)
        {
            var hit = In(bytes, entries[at], at + 1);

            if (hit.Found)
            {
                return hit;
            }
        }

        return BinaryHit.None;
    }

    private static BinaryHit In(ReadOnlySpan<byte> bytes, string entry, int index) =>
        Contains(bytes, Encoding.UTF8.GetBytes(entry)) ? new BinaryHit(index, Utf8)
            : Contains(bytes, Encoding.Unicode.GetBytes(entry)) ? new BinaryHit(index, Utf16Le)
            : BinaryHit.None;

    /// <summary>Whether <paramref name="needle"/> occurs in <paramref name="haystack"/>, ASCII letters folded: every position holding the needle's
    /// first byte in either case is tried, and a false start moves on one byte.</summary>
    private static bool Contains(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        var from = 0;

        while (Next(haystack, needle, from) is var at and >= 0)
        {
            if (MatchesAt(haystack, at, needle))
            {
                return true;
            }

            from = at + 1;
        }

        return false;
    }

    /// <summary>The next position at or after <paramref name="from"/> holding the needle's first byte in either case, with room for the whole
    /// needle after it; -1 when there is none (or the needle is empty — nothing is looked for).</summary>
    private static int Next(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle, int from)
    {
        var last = haystack.Length - needle.Length;

        if (needle.Length == 0 || from > last)
        {
            return -1;
        }

        var found = haystack[from..(last + 1)].IndexOfAny(Lower(needle[0]), Upper(needle[0]));
        return found < 0 ? -1 : from + found;
    }

    private static bool MatchesAt(ReadOnlySpan<byte> haystack, int at, ReadOnlySpan<byte> needle)
    {
        for (var i = 0; i < needle.Length; i++)
        {
            if (Lower(haystack[at + i]) != Lower(needle[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static byte Lower(byte b) => b is >= (byte)'A' and <= (byte)'Z' ? (byte)(b + ('a' - 'A')) : b;

    private static byte Upper(byte b) => b is >= (byte)'a' and <= (byte)'z' ? (byte)(b - ('a' - 'A')) : b;
}
