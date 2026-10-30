using System.Globalization;
using System.Text.RegularExpressions;
using Bench.Domain.Trace;

namespace ModelArms.Core.Checks;

/// <summary>How the shell's reader (<c>objective-checks.sh:33-36</c>, a Node script) sees a log, kept where the readers port its patterns. A regex is <c>ECMAScript | Multiline</c>, so <c>\d</c>, <c>\w</c> and <c>\b</c> are ASCII as
/// JavaScript's are; and two things .NET's engine does differently are settled here, once, instead of at every pattern:
/// <list type="bullet">
/// <item>A LINE ends at <c>\n</c>, <c>\r</c>, U+2028 or U+2029 in JavaScript (<c>^</c> and <c>.</c> both obey it), and only at <c>\n</c> in .NET. A progress line a runner ends with <c>\r</c> would hide the record after it. So the log is
/// read after <see cref="Lines"/> has made every one of them a <c>\n</c>; a capture never holds a terminator in either engine, so no name changes.</item>
/// <item>JavaScript's <c>\s</c> is the ASCII blanks AND the Unicode spaces (a no-break space, U+FEFF …), and .NET's <c>ECMAScript</c> <c>\s</c> only the ASCII ones. <see cref="Blank"/> and <see cref="NonBlank"/> spell JavaScript's.</item>
/// </list></summary>
internal static class JsLog
{
    /// <summary>JavaScript's whitespace except its line terminators: inside a class of a pattern that runs over <see cref="Lines"/>'s text, so it is what <c>\s</c> is there.</summary>
    public const string Blank = @"\t\v\f \u00a0\u1680\u2000-\u200a\u202f\u205f\u3000\ufeff";

    /// <summary>JavaScript's <c>\S</c> over <see cref="Lines"/>'s text: one character that is neither blank nor a line end.</summary>
    public const string NonBlank = $@"[^{Blank}\n]";

    public const RegexOptions Options = RegexOptions.ECMAScript | RegexOptions.Multiline;

    /// <summary>The log with every JavaScript line terminator (<c>\r\n</c> as the two it is) turned into <c>\n</c>.</summary>
    public static string Lines(string log) => log.Replace('\r', '\n').Replace('\u2028', '\n').Replace('\u2029', '\n');

    /// <summary>The first capture of <paramref name="pattern"/> in <paramref name="text"/> as a count: <c>Unavailable</c> (never zero) when there is no match or the digits are not a count this code holds. The shell's <c>Number(… ?? NaN)</c>, whose
    /// <c>NaN</c> <c>JSON.stringify</c> wrote as <c>null</c>.</summary>
    public static CapturedCount Count(Regex pattern, string text, string what) =>
        pattern.Match(text) is { Success: true } match && long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? CapturedCount.Number(value)
            : CapturedCount.Unavailable($"the log carries no {what}");
}
