using System.Text.RegularExpressions;
using Bench.Domain.Gate;

namespace Bench.Domain.Scouts;

/// <summary>The operator's deny-list (§3.13): private names, one per line, read at run time from a LOCAL file whose path is given then —
/// never committed, because a committed list of private names publishes exactly what it exists to hide. Blank lines and <c>#</c>
/// comments are skipped.</summary>
public sealed record ScrubDenyList(IReadOnlyList<string> Entries)
{
    public static ScrubDenyList None { get; } = new([]);

    public static ScrubDenyList Parse(string fileText) =>
        new([.. fileText.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).Distinct(StringComparer.OrdinalIgnoreCase)]);
}

/// <summary>The public-repository scrubber of §3.13, for every scout text that is committed: drive and home paths, e-mail addresses
/// and every deny-list entry become placeholders. It COMPOSES <see cref="FailureRedaction"/> — the gate's redaction, which already
/// removes urls, machine paths, private names and host names — and adds the e-mail rule that one lacks, so the two never drift.
/// <para>
/// <see cref="Leftovers"/> is the hygiene check a committed record or fixture is held to — narrower than the publication guard on
/// purpose: a fixture's neutral placeholder tree (<c>C:/work/tree</c>) is a drive path BY DESIGN, and what must never be committed is a
/// person's home, a mailbox, or a name on the deny-list.
/// </para></summary>
public static partial class ScoutScrubber
{
    public const string EmailPlaceholder = "<email>";

    public const string HomeRule = "carries a home path";

    public const string EmailRule = "carries an e-mail address";

    /// <summary>A blob with a NUL in this many leading bytes is binary — git's own rule of thumb.</summary>
    public const int BinaryProbeBytes = 8192;

    public static bool IsBinary(ReadOnlySpan<byte> bytes) => bytes[..Math.Min(bytes.Length, BinaryProbeBytes)].Contains((byte)0);

    /// <summary>What a BINARY blob leaves behind (E2 code round): every deny-list entry looked for in its bytes as UTF-8 and as UTF-16LE,
    /// ASCII letters case-insensitively (<see cref="BinaryScan"/>) — a name inside a .pdb, a zip's directory, a compiled resource or a UTF-16
    /// text file is as published as one in a text file. Named by index and encoding, never by value. Home paths and e-mails are text rules
    /// and stay with <see cref="Leftovers"/>.</summary>
    public static IReadOnlyList<string> BinaryLeftovers(ReadOnlySpan<byte> bytes, ScrubDenyList denyList) =>
        BinaryScan.FirstIn(bytes, denyList.Entries) is { Found: true } hit ? [$"carries deny-list entry #{hit.Entry} in its bytes ({hit.Encoding})"] : [];

    public static string Scrub(string text, ScrubDenyList denyList) =>
        FailureRedaction.Redact(Email().Replace(text, EmailPlaceholder), PrivateNames.Of(denyList.Entries));

    /// <summary>What a committed text must not carry, as rule phrases — the value itself is never repeated (it is the thing that must
    /// not be published).</summary>
    public static IReadOnlyList<string> Leftovers(string text, ScrubDenyList denyList)
    {
        var deny = PrivateNames.Of(denyList.Entries).FirstIn(text);

        return
        [
            .. Home().IsMatch(text) ? [HomeRule] : Array.Empty<string>(),
            .. Email().IsMatch(text) ? [EmailRule] : Array.Empty<string>(),
            .. deny > 0 ? [$"carries deny-list entry #{deny}"] : Array.Empty<string>(),
        ];
    }

    /// <summary>A home directory in any of the spellings a stream uses: <c>\Users\name</c>, <c>/Users/name</c>, <c>/home/name</c>, the
    /// JSON-escaped <c>\\Users\\</c>, and the <c>AppData</c> folder that only ever sits inside one.</summary>
    [GeneratedRegex(@"(?i)[\\/]+Users[\\/]+[^\\/\s""']+|/home/[^/\s""']+|AppData")]
    private static partial Regex Home();

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}")]
    private static partial Regex Email();
}
