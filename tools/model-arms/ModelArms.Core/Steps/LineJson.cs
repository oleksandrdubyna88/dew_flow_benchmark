using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bench.Domain;
using Bench.Domain.Trace;

namespace ModelArms.Core.Steps;

/// <summary>The one JSON dialect of the files a step leaves (<c>timing.jsonl</c> lines, <c>.done</c>, <c>.continue</c>): compact, keys in the order the writer names them, and the text the shell
/// wrote — <c>+01:00</c> stays <c>+01:00</c> and an em dash stays an em dash, where the default encoder would write <c>+</c>. Internal: it is the readers' and writers' shared tool.</summary>
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

/// <summary>A JSON object read field by field. Every accessor answers a value and, when the field is of the wrong kind (or required and absent), records the FIRST problem instead of throwing, so one reader
/// can ask for every field and look once at <see cref="Problem"/>. An absent field and a JSON <c>null</c> are the same fact.</summary>
internal sealed class Row
{
    private readonly JsonElement root;

    private Row(JsonElement root)
    {
        this.root = root;
    }

    /// <summary>The first thing wrong with the fields asked for so far, or <c>""</c>.</summary>
    public string Problem { get; private set; } = string.Empty;

    private static readonly JsonDocumentOptions Tolerant = new() { AllowDuplicateProperties = true, AllowTrailingCommas = true };

    /// <summary>The object <paramref name="text"/> holds, or why it is none: <paramref name="what"/> says what was being read.</summary>
    public static Outcome<Row> Parse(string text, string what)
    {
        try
        {
            using var document = JsonDocument.Parse(text, Tolerant);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? Outcome<Row>.Success(new Row(document.RootElement.Clone()))
                : Outcome<Row>.Failure($"{what} is a JSON object, not {Describe(document.RootElement.ValueKind)}");
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            return Outcome<Row>.Failure($"{what} is not JSON: {ex.Message}");
        }
    }

    public string Text(string name, bool required = false) =>
        !Find(name, out var value) ? Absent(name, required, "text", string.Empty)
        : value.ValueKind == JsonValueKind.String && TryText(value, out var text) ? text
        : Wrong(name, "text", string.Empty);

    public int Int(string name) =>
        !Find(name, out var value) ? Absent(name, true, "a whole number", 0)
        : value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number
        : Wrong(name, "a whole number", 0);

    public DateTimeOffset Utc(string name) =>
        !Find(name, out var value) ? Absent(name, true, "a UTC instant", default(DateTimeOffset))
        : value.ValueKind == JsonValueKind.String && TryUtc(value, out var instant) ? instant
        : Wrong(name, "a UTC instant", default(DateTimeOffset));

    public bool Flag(string name) =>
        !Find(name, out var value) ? false
        : value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean()
        : Wrong(name, "true or false", false);

    /// <summary>A number, or <see cref="CapturedCount.Unavailable"/> when the field is absent or null — never zero.</summary>
    public CapturedCount Count(string name) =>
        !Find(name, out var value) ? CapturedCount.Unavailable(TimingLines.NotRecorded)
        : value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) ? CapturedCount.Number(number)
        : Wrong(name, "a whole number or null", CapturedCount.Unavailable(TimingLines.NotRecorded));

    public IReadOnlyList<string> Texts(string name) =>
        !Find(name, out var value) ? []
        : value.ValueKind == JsonValueKind.Array && TryTexts(value, out var items) ? items
        : Wrong<IReadOnlyList<string>>(name, "a list of text", []);

    /// <summary>True when the field is exactly the text <paramref name="expected"/>; any other value, of any kind, is false and no problem.</summary>
    public bool Is(string name, string expected) => Find(name, out var value) && value.ValueKind == JsonValueKind.String && TryText(value, out var text) && text == expected;

    private bool Find(string name, out JsonElement value) => root.TryGetProperty(name, out value) && value.ValueKind != JsonValueKind.Null;

    private T Absent<T>(string name, bool required, string expected, T fallback)
    {
        if (required)
        {
            Note($"'{name}' is missing — it must be {expected}");
        }

        return fallback;
    }

    private T Wrong<T>(string name, string expected, T fallback)
    {
        Note($"'{name}' must be {expected}");
        return fallback;
    }

    private void Note(string problem)
    {
        if (Problem.Length == 0)
        {
            Problem = problem;
        }
    }

    /// <summary>The string, or false for one that is not valid text (an escaped lone surrogate) — never a throw.</summary>
    private static bool TryText(JsonElement element, out string text)
    {
        try
        {
            text = element.GetString() ?? string.Empty;
            return true;
        }
        catch (InvalidOperationException)
        {
            text = string.Empty;
            return false;
        }
    }

    private static bool TryUtc(JsonElement element, out DateTimeOffset instant)
    {
        instant = default;
        return TryText(element, out var text)
            && DateTimeOffset.TryParseExact(text, LineJson.UtcFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out instant);
    }

    private static bool TryTexts(JsonElement array, out IReadOnlyList<string> items)
    {
        var read = new List<string>();
        items = read;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || !TryText(item, out var text))
            {
                return false;
            }

            read.Add(text);
        }

        return true;
    }

    private static string Describe(JsonValueKind kind) =>
        kind switch
        {
            JsonValueKind.Array => "an array",
            JsonValueKind.String => "a string",
            JsonValueKind.Number => "a number",
            JsonValueKind.True or JsonValueKind.False => "a boolean",
            JsonValueKind.Null => "null",
            _ => kind.ToString().ToLowerInvariant(),
        };
}
