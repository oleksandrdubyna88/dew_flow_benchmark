using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Bench.Domain.Gate;

/// <summary>The id of an imported campaign or cell, DERIVED from where it came from — a name-based UUID over (harness,
/// source key). Derived rather than minted, so importing the same source twice finds the same ids: idempotence is a
/// property of the key, not of a lookup somebody has to remember to make.</summary>
public static class ImportIds
{
    public static Guid Of(string harness, string key)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"gate-import\n{harness}\n{key}"));
        var bytes = digest.AsSpan(0, 16).ToArray();

        // Version 8 (a custom, name-based layout) and the RFC variant, so the value is a well-formed UUID and never
        // collides with a random version-4 or a time-ordered version-7 id a native plan mints.
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes);
    }
}

/// <summary>A reviewer or label slug built from a name somebody else chose — a model id such as <c>grok-4.7</c> or a
/// table's first column such as <c>**GPT-5.5**</c>: lower case, every run of characters outside <c>[a-z0-9]</c> one
/// hyphen, trimmed, cut to fit.</summary>
public static partial class ImportSlug
{
    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex Outside { get; }

    public static string Of(string? text, int maxLength = 64)
    {
        var slug = Outside.Replace((text ?? string.Empty).ToLowerInvariant(), "-").Trim('-');
        return slug.Length <= maxLength ? slug : slug[..maxLength].TrimEnd('-');
    }
}
