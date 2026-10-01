using System.Text.Json;

namespace Bench.Domain.Probes;

/// <summary>How the transcript readers read JSON (S2b, finding 5): through <see cref="JsonDocument"/>, which tolerates a property
/// name repeated in one object, never through <c>JsonNode</c>, which builds a dictionary and threw
/// <c>ArgumentException: An item with the same key has already been added. Key: id</c> on codex-cli 0.156.1's <c>web_search</c>
/// items (<c>{"id":"item_1",…,"id":"exec-…"}</c>) — and the leg faulted with nothing committed. A line that is not JSON is skipped,
/// never fatal: banners and progress lines sit between a stream's events.</summary>
internal static class ProbeJson
{
    private static readonly JsonDocumentOptions Tolerant = new() { AllowDuplicateProperties = true, AllowTrailingCommas = true };

    /// <summary>Every line that parses as a JSON object, as documents the caller owns.</summary>
    public static IReadOnlyList<JsonDocument> Lines(string text) =>
        [.. text.Split('\n').Select(Parse).OfType<JsonDocument>().Where(d => d.RootElement.ValueKind == JsonValueKind.Object)];

    public static JsonDocument? Parse(string text)
    {
        var trimmed = text.Trim();

        if (trimmed.Length == 0 || trimmed[0] is not ('{' or '['))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(trimmed, Tolerant);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A string property, or empty — a missing, null or non-string property is "nothing said".</summary>
    public static string Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>A nested object, or an undefined element when the path is not there.</summary>
    public static JsonElement Object(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object ? value : default;

    public static IEnumerable<JsonElement> Array(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : [];

    /// <summary>A number, when the property is one.</summary>
    public static bool Number(JsonElement element, string name, out long value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var found)
               && found.ValueKind == JsonValueKind.Number && found.TryGetInt64(out value);
    }

    /// <summary>The element's own JSON, for a tool input kept as the CLI printed it; empty for an undefined element.</summary>
    public static string Raw(JsonElement element) => element.ValueKind == JsonValueKind.Undefined ? string.Empty : element.GetRawText();

    public static void Dispose(IEnumerable<JsonDocument> documents)
    {
        foreach (var document in documents)
        {
            document.Dispose();
        }
    }
}
