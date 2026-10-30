using System.Globalization;
using System.Text;

namespace ModelArms.Core.Launch;

/// <summary>The text of a JSON string that <c>JsonElement.GetString</c> refuses: a well-formed one holding a LONE surrogate escape (<c>"\ud83d"</c>), which a model can print when it cuts an emoji in two. It is read as a JSON parser
/// reads it — node's <c>JSON.parse</c> gives the lone half as a lone surrogate — except that a lone half becomes U+FFFD, because a .NET string with a lone surrogate cannot be written as JSON or transcoded to UTF-8 again. A well-formed pair
/// is kept as the character it is. Nothing here throws on text the JSON reader accepted (its escapes are well-formed: a backslash, one of <c>"\/bfnrt</c> or <c>u</c> and four hex digits).</summary>
internal static class JsonStringText
{
    private const char Replacement = '\uFFFD';

    private const int EscapeLength = 6;

    /// <summary>The decoded text of <paramref name="raw"/>, the characters between a JSON string's quotes, escapes still in it.</summary>
    public static string Decode(string raw)
    {
        var text = new StringBuilder(raw.Length);
        var at = 0;
        while (at < raw.Length)
        {
            at += raw[at] == '\\' ? Escape(raw, at, text) : Plain(raw[at], text);
        }

        return text.ToString();
    }

    private static int Plain(char character, StringBuilder text)
    {
        text.Append(character);
        return 1;
    }

    /// <summary>One escape at <paramref name="at"/>; the number of characters of <paramref name="raw"/> it took.</summary>
    private static int Escape(string raw, int at, StringBuilder text) => raw[at + 1] == 'u' ? Unicode(raw, at, text) : Simple(raw[at + 1], text);

    private static int Simple(char escaped, StringBuilder text)
    {
        text.Append(escaped switch
        {
            'b' => '\b',
            'f' => '\f',
            'n' => '\n',
            'r' => '\r',
            't' => '\t',
            _ => escaped,
        });
        return 2;
    }

    /// <summary>A <c>\uXXXX</c>: a high half followed by an escaped low half is a pair (twelve characters); any other surrogate is lone.</summary>
    private static int Unicode(string raw, int at, StringBuilder text)
    {
        var unit = UnitAt(raw, at);
        if (char.IsHighSurrogate(unit) && IsEscapedLow(raw, at + EscapeLength))
        {
            text.Append(unit).Append(UnitAt(raw, at + EscapeLength));
            return 2 * EscapeLength;
        }

        text.Append(char.IsSurrogate(unit) ? Replacement : unit);
        return EscapeLength;
    }

    private static bool IsEscapedLow(string raw, int at) =>
        at + EscapeLength <= raw.Length && raw[at] == '\\' && raw[at + 1] == 'u' && char.IsLowSurrogate(UnitAt(raw, at));

    private static char UnitAt(string raw, int at) => (char)int.Parse(raw.AsSpan(at + 2, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
}
