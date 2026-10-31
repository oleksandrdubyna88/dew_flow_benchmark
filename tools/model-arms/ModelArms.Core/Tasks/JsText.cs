using System.Globalization;
using System.Text;
using System.Text.Json;
using ModelArms.Core.Checks;

namespace ModelArms.Core.Tasks;

/// <summary>One value of a parsed <c>tasks.json</c> as JavaScript sees it, or nothing at all — <c>undefined</c>, a member the task does not have. <see cref="Identity"/> numbers the value within one reading, so that an object or an
/// array is equal to itself and to nothing else (<c>===</c> compares them by reference).</summary>
internal sealed record JsValue(JsonElement? Element, int Identity)
{
    /// <summary>What a template literal prints: <c>${value}</c>.</summary>
    public string Text => JsText.Template(Element);

    /// <summary>What <c>Array.prototype.join</c> prints: the same, but <c>null</c> and <c>undefined</c> print nothing.</summary>
    public string Joined => Element is not { ValueKind: not JsonValueKind.Null } element ? string.Empty : JsText.Template(element);

    /// <summary>What <c>${JSON.stringify(value)}</c> prints; nothing at all stringifies as <c>undefined</c>.</summary>
    public string Json => Element is { } element ? JsText.Stringify(element) : "undefined";

    public bool Truthy => Element is { } element && JsText.Truthy(element);

    /// <summary>Strict equality as a string: two values are <c>===</c> exactly when their keys are equal.</summary>
    public string Key => Element switch
    {
        null => "undefined",
        { ValueKind: JsonValueKind.String } text => "s:" + text.GetString(),
        { ValueKind: JsonValueKind.Number } number => "n:" + JsText.Number(number),
        { ValueKind: JsonValueKind.Object or JsonValueKind.Array } => "o:" + Identity.ToString(CultureInfo.InvariantCulture),
        { } other => other.ValueKind.ToString(),
    };

    /// <summary>The text of a JSON string; <c>null</c> for every other value.</summary>
    public string? AsString => Element is { ValueKind: JsonValueKind.String } text ? text.GetString() : null;
}

/// <summary>JavaScript's text of a JSON value (<c>tasks-plan.mjs</c> prints values in template literals): <c>undefined</c> for nothing, a string itself, a number as JavaScript prints it (<c>1e2</c> is <c>100</c>), a boolean and
/// <c>null</c> by their names, an array as its items joined by <c>,</c> (a <c>null</c> item is empty), an object as <c>[object Object]</c>; and <c>JSON.stringify</c>'s text where the shell calls it.
/// <para>
/// A string is escaped by the writer every module file uses (<see cref="ObjectiveJson"/>): the bytes equal <c>JSON.stringify</c>'s for printable BMP text. An object's members keep their order in the file, where JavaScript puts keys that read as array
/// indexes first (not reproduced, and not reachable by a list the checker accepts).
/// </para></summary>
internal static class JsText
{
    public static string Template(JsonElement? value) =>
        value switch
        {
            null => "undefined",
            { ValueKind: JsonValueKind.String } text => text.GetString()!,
            { ValueKind: JsonValueKind.Number } number => Number(number),
            { ValueKind: JsonValueKind.Array } array => string.Join(',', array.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.Null ? string.Empty : Template(item))),
            { ValueKind: JsonValueKind.Object } => "[object Object]",
            { } other => other.GetRawText(),
        };

    public static string Stringify(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => Quoted(value.GetString()!),
            JsonValueKind.Number => Number(value) is "NaN" or "Infinity" or "-Infinity" ? "null" : Number(value),
            JsonValueKind.Array => "[" + string.Join(',', value.EnumerateArray().Select(Stringify)) + "]",
            JsonValueKind.Object => "{" + string.Join(',', Members(value).Select(member => Quoted(member.Key) + ":" + Stringify(member.Value))) + "}",
            _ => value.GetRawText(),
        };

    private static string Quoted(string text) => ObjectiveJson.OneLine(writer => writer.WriteStringValue(text));

    /// <summary>The members of an object as <c>JSON.parse</c> keeps them: a name given twice keeps its first place and its last value.</summary>
    public static IReadOnlyList<KeyValuePair<string, JsonElement>> Members(JsonElement obj)
    {
        var members = new List<KeyValuePair<string, JsonElement>>();
        foreach (var property in obj.EnumerateObject())
        {
            var at = members.FindIndex(member => member.Key == property.Name);
            var pair = new KeyValuePair<string, JsonElement>(property.Name, property.Value);
            if (at < 0)
            {
                members.Add(pair);
            }
            else
            {
                members[at] = pair;
            }
        }

        return members;
    }

    /// <summary>The value of the member <paramref name="name"/> of an object (the last one if the file gives it twice); nothing for any other value or a name it does not have.</summary>
    public static JsonElement? Member(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object && Members(value).Where(member => member.Key == name).Select(member => (JsonElement?)member.Value).FirstOrDefault() is { } found ? found : null;

    public static bool Truthy(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.False => false,
            JsonValueKind.String => value.GetString()!.Length > 0,
            JsonValueKind.Number => Parse(value) != 0,
            _ => true,
        };

    public static string Number(JsonElement number) => Number(Parse(number));

    private static double Parse(JsonElement number) => double.Parse(number.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <summary>Number::toString of the specification: the shortest digits that read back as the same double, in plain or exponent form by the exponent.</summary>
    public static string Number(double value)
    {
        if (double.IsNaN(value))
        {
            return "NaN";
        }

        if (double.IsInfinity(value))
        {
            return value > 0 ? "Infinity" : "-Infinity";
        }

        if (value == 0)
        {
            return "0";
        }

        var (digits, exponent) = Digits(Math.Abs(value));
        return (value < 0 ? "-" : string.Empty) + Layout(digits, exponent);
    }

    /// <summary>The shortest round-trip digits and the position of the decimal point: the value is <c>0.&lt;digits&gt; × 10^exponent</c>.</summary>
    private static (string Digits, int Exponent) Digits(double positive)
    {
        var text = positive.ToString("E16", CultureInfo.InvariantCulture);
        for (var precision = 0; precision <= 16; precision++)
        {
            var candidate = positive.ToString("E" + precision, CultureInfo.InvariantCulture);
            if (double.Parse(candidate, CultureInfo.InvariantCulture) == positive)
            {
                text = candidate;
                break;
            }
        }

        var parts = text.Split('E');
        var digits = parts[0].Replace(".", string.Empty, StringComparison.Ordinal).TrimEnd('0');
        return (digits.Length == 0 ? "0" : digits, int.Parse(parts[1], CultureInfo.InvariantCulture) + 1);
    }

    private static string Layout(string digits, int n)
    {
        var k = digits.Length;
        if (k <= n && n <= 21)
        {
            return digits + new string('0', n - k);
        }

        if (0 < n && n <= 21)
        {
            return digits[..n] + "." + digits[n..];
        }

        if (-6 < n && n <= 0)
        {
            return "0." + new string('0', -n) + digits;
        }

        var exponent = n - 1;
        var sign = exponent < 0 ? "-" : "+";
        var mantissa = k == 1 ? digits : digits[0] + "." + digits[1..];
        return new StringBuilder(mantissa).Append('e').Append(sign).Append(Math.Abs(exponent).ToString(CultureInfo.InvariantCulture)).ToString();
    }
}
