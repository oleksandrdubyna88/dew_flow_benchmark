using System.Globalization;
using Bench.Domain;

namespace ModelArms.Core.Experiment;

/// <summary>A git's version as <c>git --version</c> prints it (plan D23, E4.S0): <c>git version &lt;major&gt;.&lt;minor&gt;.&lt;patch&gt;[.&lt;suffix&gt;] [more]</c>. The three numbers and the SUFFIX — whatever follows the third number, kept as text (<c>windows.1</c>, <c>GIT</c>, <c>12.gabcdef0</c>, <c>rc1</c>). A pre-release is a suffix whose first piece starts <c>rc</c>.
/// <para>
/// The grammar is fixed and narrow, and an unreadable text is a refusal, never a guess: the text is trimmed of spaces, CR and LF; it must start <c>git version </c> (ordinal — git's own prefix on every vendor build); the word after it, up to the first space, is split on <c>.</c>; the first three pieces must be decimal digits and nothing else. <c>git version 2.45</c> (two numbers) and <c>2.45.0-rc0</c>
/// (the third piece is not digits) are not git's text. The floor reads a NUMBER: a vendor build carrying it without the fix passes (parent §9).
/// </para></summary>
public sealed record GitVersion(int Major, int Minor, int Patch, string Suffix)
{
    private const string Prefix = "git version ";
    private const int TextLimit = 200;

    /// <summary>Whether the suffix's first <c>.</c>-piece starts <c>rc</c>, whatever its case: a release candidate, whatever its number.</summary>
    public bool IsPreRelease => Suffix.Split('.')[0].StartsWith("rc", StringComparison.OrdinalIgnoreCase);

    /// <summary>The text git printed, read by the grammar above; the failure names the text through <see cref="ExecutableRule.Printable"/>, cut at 200 characters, and says <c>printed nothing</c> for the empty text.</summary>
    public static Outcome<GitVersion> Parse(string text)
    {
        var trimmed = text.Trim(' ', '\r', '\n');
        return trimmed.StartsWith(Prefix, StringComparison.Ordinal) && Numbers(trimmed[Prefix.Length..]) is { } read
            ? Outcome<GitVersion>.Success(read)
            : Outcome<GitVersion>.Failure($"`git --version` printed {Named(trimmed)} — not a version this harness can read");
    }

    /// <summary>The three numbers, or nothing.</summary>
    private static GitVersion? Numbers(string afterPrefix)
    {
        var pieces = afterPrefix.Split(' ')[0].Split('.');
        return pieces.Length >= 3 && pieces.Take(3).Select(Digits).ToArray() is [{ } major, { } minor, { } patch]
            ? new GitVersion(major, minor, patch, string.Join('.', pieces.Skip(3)))
            : null;
    }

    /// <summary>ASCII digits and nothing else: <see cref="int.TryParse(string, NumberStyles, IFormatProvider, out int)"/> ignores trailing NULs (<c>"2\0"</c> parses as 2), which a version text must not be allowed.</summary>
    private static int? Digits(string piece) =>
        piece.Length > 0 && piece.All(char.IsAsciiDigit) && int.TryParse(piece, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;

    private static string Named(string text) =>
        text.Length == 0 ? "nothing" : $"\"{ExecutableRule.Printable(ExecutableRule.Cut(text, TextLimit))}\"";

    /// <summary>The three numbers, <c>2.39.5</c>: the suffix is omitted.</summary>
    public override string ToString() => $"{Major.ToString(CultureInfo.InvariantCulture)}.{Minor.ToString(CultureInfo.InvariantCulture)}.{Patch.ToString(CultureInfo.InvariantCulture)}";
}

/// <summary>The oldest git the harness's guards hold on (plan D23, E4.S0): 2.45.0, or the patched release of its own maintenance line — 2.39.4, 2.40.2, 2.41.1, 2.42.2, 2.43.4, 2.44.1. <c>GIT_NO_LAZY_FETCH</c> is read from these; an older git ignores it, <c>GIT_ALLOW_PROTOCOL</c> and the <c>GIT_CONFIG_COUNT</c> neutralisation in silence, and the harness would claim measures it never had.
/// Decided from the version's NUMBER, not from an observation of the guards: a vendor build carrying the number without the fix passes (parent §9). A release candidate is refused whatever its number — the safe direction.</summary>
public static class GitFloor
{
    /// <summary>The floor in words, the one sentence every refusal and every doc holds.</summary>
    public const string Sentence = "2.45.0, or 2.39.4 / 2.40.2 / 2.41.1 / 2.42.2 / 2.43.4 / 2.44.1 on its own maintenance line";

    private const int FloorMinor = 45;

    /// <summary>The minor lines below 2.45 and the first patch of each that carries the fix.</summary>
    private static readonly IReadOnlyDictionary<int, int> Patched = new Dictionary<int, int> { [39] = 4, [40] = 2, [41] = 1, [42] = 2, [43] = 4, [44] = 1 };

    /// <summary>Parse, then the pre-release refused, then the number held against the floor; <see cref="Outcome{T}.Ok"/> carries the version read.</summary>
    public static Outcome<GitVersion> Admit(string text) => GitVersion.Parse(text).Match(Judged, Outcome<GitVersion>.Failure);

    private static Outcome<GitVersion> Judged(GitVersion version) =>
        version.IsPreRelease ? Outcome<GitVersion>.Failure(Candidate(version)) : Holds(version) ? Outcome<GitVersion>.Success(version) : Outcome<GitVersion>.Failure(Below(version));

    private static string Candidate(GitVersion version) =>
        $"git {version}.{version.Suffix} is a release candidate, not a release — the floor ({Sentence}) is read from releases; install a release";

    private static string Below(GitVersion version) =>
        $"git {version} is below the floor — {Sentence} (D23: GIT_NO_LAZY_FETCH is read from these; an older git ignores the harness's guards in silence) — install a newer git";

    private static bool Holds(GitVersion version) =>
        version.Major > 2 || (version.Major == 2 && (version.Minor >= FloorMinor || (Patched.TryGetValue(version.Minor, out var patch) && version.Patch >= patch)));
}
