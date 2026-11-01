using System.Globalization;
using System.Text.Json;
using Bench.Domain;
using Bench.Domain.Trace;

namespace ModelArms.Core.Steps;

/// <summary>A JSON object read field by field. Every accessor answers a value and, when the field is of the wrong kind (or required and absent), records the FIRST problem instead of throwing, so one reader
/// can ask for every field and look once at <see cref="Problem"/>. An absent field and a JSON <c>null</c> are the same fact.</summary>
internal sealed class JsonRow
{
    private readonly JsonElement root;

    private JsonRow(JsonElement root)
    {
        this.root = root;
    }

    /// <summary>The first thing wrong with the fields asked for so far, or <c>""</c>.</summary>
    public string Problem { get; private set; } = string.Empty;

    private static readonly JsonDocumentOptions Tolerant = new() { AllowDuplicateProperties = true, AllowTrailingCommas = true };

    /// <summary>The object <paramref name="text"/> holds, or why it is none: <paramref name="what"/> says what was being read.</summary>
    public static Outcome<JsonRow> Parse(string text, string what)
    {
        try
        {
            using var document = JsonDocument.Parse(text, Tolerant);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? Outcome<JsonRow>.Success(new JsonRow(document.RootElement.Clone()))
                : Outcome<JsonRow>.Failure($"{what} is a JSON object, not {Describe(document.RootElement.ValueKind)}");
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            return Outcome<JsonRow>.Failure($"{what} is not JSON: {ex.Message}");
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

    /// <summary>The objects of an array field, each as a row of its own (the isolation snapshot's <c>modes</c>); an absent field is no rows, and an item that is not an object is the first problem.</summary>
    public IReadOnlyList<JsonRow> Rows(string name) =>
        !Find(name, out var value) ? []
        : value.ValueKind == JsonValueKind.Array && TryRows(value, out var rows) ? rows
        : Wrong<IReadOnlyList<JsonRow>>(name, "a list of objects", []);

    /// <summary>A whole non-negative number, or <c>null</c> when the field is absent; a field of another kind is the first problem (a device and an inode need all 64 bits, so <see cref="Int"/> is no reader of them).</summary>
    public ulong? ULong(string name) =>
        !Find(name, out var value) ? null
        : value.ValueKind == JsonValueKind.Number && value.TryGetUInt64(out var number) ? number
        : Wrong<ulong?>(name, "a whole non-negative number", null);

    /// <summary>True when the field is exactly the text <paramref name="expected"/>; any other value, of any kind, is false and no problem.</summary>
    public bool Is(string name, string expected) => Find(name, out var value) && value.ValueKind == JsonValueKind.String && TryText(value, out var text) && text == expected;

    /// <summary>True when the field is present and not JSON <c>null</c>, of whatever kind.</summary>
    public bool Has(string name) => Find(name, out _);

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

    private static bool TryRows(JsonElement array, out IReadOnlyList<JsonRow> rows)
    {
        var read = new List<JsonRow>();
        rows = read;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            read.Add(new JsonRow(item.Clone()));
        }

        return true;
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
