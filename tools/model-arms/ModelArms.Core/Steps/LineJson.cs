using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bench.Domain;
using Bench.Domain.Trace;

namespace ModelArms.Core.Steps;

/// <summary>The one JSON dialect of the files a step leaves (<c>timing.jsonl</c> lines, <c>.done</c>, <c>.continue</c>): compact, keys in the order the writer names them, and the text the shell
/// wrote — <c>+01:00</c> stays <c>+01:00</c> and an em dash stays an em dash, where the default encoder would escape the plus sign and every non-ASCII character. Internal: it is the readers' and writers' shared tool.</summary>
internal static partial class LineJson
{
    /// <summary>The instants of these files: UTC to the second, as the shell's <c>date -u +%FT%TZ</c> wrote them.</summary>
    public const string UtcFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    private static readonly JsonSerializerOptions Compact = new() { WriteIndented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>One compact line holding <paramref name="members"/> in the order given. A member whose value is <c>null</c> is written as JSON <c>null</c>.</summary>
    public static string Object(params (string Key, JsonNode? Value)[] members)
    {
        var line = new JsonObject();
        foreach (var (key, value) in members)
        {
            line.Add(key, value);
        }

        return line.ToJsonString(Compact);
    }

    /// <summary>The one member <paramref name="key"/>, or none when <paramref name="condition"/> is false — the keys a line carries only when they say something.</summary>
    public static IEnumerable<(string Key, JsonNode? Value)> Only(bool condition, string key, Func<JsonNode> value) => condition ? [(key, value())] : [];

    /// <summary>Text, with a lone surrogate (which no UTF-8 encoding can hold and a model's reason may carry) replaced by U+FFFD, so writing a reason never throws.</summary>
    public static JsonNode Text(string value) => JsonValue.Create(LoneSurrogate().Replace(value, "�"))!;

    public static JsonNode Number(long value) => JsonValue.Create(value);

    public static JsonNode Flag(bool value) => JsonValue.Create(value);

    public static JsonNode Utc(DateTimeOffset value) => JsonValue.Create(value.UtcDateTime.ToString(UtcFormat, CultureInfo.InvariantCulture))!;

    /// <summary>A captured count as its number, an uncaptured one as JSON <c>null</c> — absent is not zero.</summary>
    public static JsonNode? Count(CapturedCount value) => value.WasCaptured ? Number(value.Value) : null;

    public static JsonNode Texts(IReadOnlyList<string> values) => new JsonArray([.. values.Select(Text)]);

    [GeneratedRegex(@"[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LoneSurrogate();
}
