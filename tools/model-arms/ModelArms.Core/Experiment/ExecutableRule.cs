using System.Globalization;
namespace ModelArms.Core.Experiment;

/// <summary>D17's static half: which executables a command may name. A harness never runs a file an arm could write, so an executable
/// is a bare name resolved from PATH, or an absolute path outside every forbidden root. A file under the target may only be an
/// argument of an executable resolved elsewhere, which is why the suite runs as <c>dotnet &lt;tests&gt;.dll</c>.</summary>
public static class ExecutableRule
{
    private static readonly char[] PathMarks = ['/', '\\', ':'];

    /// <summary>The verbatim and device spellings of a Windows path. <c>\\?\C:\target\x.exe</c> is the same file as <c>C:\target\x.exe</c> but a text
    /// comparison sees another path, so they are refused as a SPELLING, on every platform (a textual rule, testable anywhere).</summary>
    private static readonly string[] DevicePrefixes = [@"\\?\", @"\\.\", @"\??\", "//?/", "//./", "/??/"];

    /// <summary>Why this executable is refused, or the empty string when it is fine. A name holding a separator or a colon is a PATH:
    /// <c>C:x.exe</c> is drive-relative on Windows, so it is never a bare name.
    /// <para>
    /// This is a static, textual rule. It does not follow symbolic links: a link outside every forbidden root that points inside one
    /// passes here, and <see cref="ExecutableResolution"/> is what asks the kernel for the real path and checks that against the same roots.
    /// </para></summary>
    public static string Refusal(string executable, IReadOnlyList<string> forbiddenRoots) =>
        executable switch
        {
            _ when HoldsNul(executable) => $"the executable '{Printable(executable)}' holds a NUL character — a path never does; refused (D17)",
            _ when executable.Trim().Length == 0 => "the executable is empty — name an executable from PATH or an absolute path outside the target (D17)",
            _ when DevicePrefixes.Any(prefix => executable.StartsWith(prefix, StringComparison.Ordinal)) =>
                $"the executable '{Printable(executable)}' uses a verbatim or device prefix — spell the path plainly (D17)",
            _ when executable.IndexOfAny(PathMarks) < 0 => string.Empty,
            _ when !Path.IsPathFullyQualified(executable) => Relative(executable),
            _ => UnderForbiddenRoot(executable, forbiddenRoots),
        };

    /// <summary>True when the text holds a NUL character. The first thing every comparison asks: <c>Path.GetFullPath</c> throws on one, and a thrown
    /// <c>ArgumentException</c> is a crash where a refusal belongs.</summary>
    public static bool HoldsNul(string text) => text.Contains('\0', StringComparison.Ordinal);

    /// <summary>Why the file this executable REALLY is (the path the kernel opens, every link and every <c>..</c> applied) is refused, or the empty string.
    /// <paramref name="written"/> is what the operator spelled, so the sentence names both.</summary>
    public static string RealRefusal(string written, string real, IReadOnlyList<string> forbiddenRoots)
    {
        var root = forbiddenRoots.FirstOrDefault(r => PathScope.IsEqualOrUnder(real, r));
        return root is null
            ? string.Empty
            : $"the executable '{Printable(written)}' is really '{Printable(real)}', which is under {Under(root)} — a harness never runs a file an arm could write; pass it as an argument of an executable resolved outside (D17)";
    }

    /// <summary>The roots no executable may sit under: the target, and the experiment root itself, which holds the base clone, the arms'
    /// clones and everything else an arm could write. Forbidding the root, not just <c>base</c> and <c>r</c>, fails closed. A root that was not
    /// read (the empty string) is no root. One answer for the experiment file's static check and for the host's resolution.</summary>
    public static IReadOnlyList<string> ForbiddenRoots(string source, string root) =>
        [.. new[] { source, root }.Where(candidate => candidate.Length > 0)];

    /// <summary>The text with every NUL character written as the two characters <c>\0</c>, and every other control or format character as <c>\uXXXX</c>: a raw NUL in a sentence ends the line in many terminals and logs.
    /// Every sentence that echoes input the operator typed goes through this.</summary>
    public static string Printable(string text) => string.Concat(text.Select(Escaped));

    /// <summary>A NUL is <c>\0</c>; any other <see cref="IsInvisible"/> character (a newline, a tab, U+200B, U+2028, U+00A0) is <c>\uXXXX</c>, so a sentence stays one line and shows what it refused.</summary>
    private static string Escaped(char c) =>
        c == '\0' ? "\\0" : IsInvisible(c) ? $"\\u{(int)c:X4}" : c.ToString();

    /// <summary>A character a person cannot see as what it is: a control or format character (a newline, U+200B), a line or paragraph separator (U+2028, U+2029) and every space but U+0020 (U+00A0).
    /// One answer for the sentences that echo it and for the reader that refuses it.</summary>
    internal static bool IsInvisible(char c) =>
        char.IsControl(c) || char.GetUnicodeCategory(c) is UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator || (char.IsWhiteSpace(c) && c != ' ');

    /// <summary>A forbidden root as a sentence names it. A root holding a NUL cannot be compared, so it counts as containing the executable (fail closed)
    /// and the sentence says that, rather than printing a root that looks ordinary.</summary>
    private static string Under(string root) =>
        HoldsNul(root)
            ? $"'{Printable(root)}', which holds a NUL character and cannot be compared (treated as containing it)"
            : $"'{root}'";

    private static string Relative(string executable) =>
        $"the executable '{Printable(executable)}' is a relative path, resolved against the target — use a name from PATH or an absolute path outside the target (D17)";

    private static string UnderForbiddenRoot(string executable, IReadOnlyList<string> forbiddenRoots)
    {
        var root = forbiddenRoots.FirstOrDefault(r => PathScope.IsEqualOrUnder(executable, r));
        return root is null
            ? string.Empty
            : $"the executable '{Printable(executable)}' is under {Under(root)} — a harness never runs a file an arm could write; pass it as an argument of an executable resolved outside (D17)";
    }
}
