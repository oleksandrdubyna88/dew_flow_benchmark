using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bench.Domain.Gate;

/// <summary>One vendor row as the panel's settings file carries it — the shape <c>--from-coai-settings</c>
/// imports. Strings as read; the reviewer row is built from them one step later.</summary>
public sealed record CoaiVendorFields(
    string Id,
    string Runtime,
    string Model,
    string BaseUrl,
    string ExecutablePath,
    string Dialect,
    string Key,
    string Effort,
    string RemoteVendor,
    int ReviewMinutes,
    bool Thinking,
    bool Plan,
    bool Code,
    bool Feature,
    bool Document);

/// <summary>The product's vendor-row VOCABULARY — every field it knows, each with the JSON type it reads it as —
/// and the reader of the panel's own rows. The writer is <see cref="CoaiVendorsSetting.From"/>, the one producer
/// of the vendors string; both enumerate <see cref="KnownFields"/>, so a field the product grows is a red test
/// here rather than a silently-dropped setting.</summary>
public static class CoaiVendorRow
{
    private enum Shape
    {
        Text,
        Flag,
        Integer,
        Object,
    }

    /// <summary>Each field in the product's spelling, with the JSON type the product's reader binds it to.</summary>
    private static readonly IReadOnlyList<(string Name, Shape Shape)> Shapes =
    [
        ("id", Shape.Text), ("runtime", Shape.Text), ("model", Shape.Text), ("baseUrl", Shape.Text),
        ("executablePath", Shape.Text), ("plan", Shape.Flag), ("code", Shape.Flag), ("document", Shape.Flag),
        ("remoteVendor", Shape.Text), ("dialect", Shape.Text), ("feature", Shape.Flag), ("key", Shape.Text),
        ("price", Shape.Object), ("effort", Shape.Text), ("thinking", Shape.Flag), ("reviewMinutes", Shape.Integer),
    ];

    /// <summary>Every field the product's vendor row knows, in the product's own spelling.</summary>
    public static IReadOnlyList<string> KnownFields { get; } = [.. Shapes.Select(s => s.Name)];

    /// <summary>The panel's own rows, read back for import. A row carrying a field this build does not know, or
    /// a known field of the wrong JSON type, is refused naming the field — dropping or defaulting it would import
    /// a reviewer that is not the one the operator runs.</summary>
    public static Outcome<IReadOnlyList<CoaiVendorFields>> Read(string? json)
    {
        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(json ?? string.Empty);
        }
        catch (JsonException e)
        {
            return Outcome<IReadOnlyList<CoaiVendorFields>>.Failure($"the vendor list is not JSON: {e.Message}");
        }

        return parsed is JsonArray array
            ? All(array)
            : Outcome<IReadOnlyList<CoaiVendorFields>>.Failure("the vendor list is a JSON ARRAY of vendor rows");
    }

    private static Outcome<IReadOnlyList<CoaiVendorFields>> All(JsonArray array)
    {
        var rows = new List<CoaiVendorFields>();
        foreach (var read in array.Select(Fields))
        {
            if (read is Outcome<CoaiVendorFields>.Fail fail)
            {
                return Outcome<IReadOnlyList<CoaiVendorFields>>.Failure(fail.Reason);
            }

            rows.Add(((Outcome<CoaiVendorFields>.Ok)read).Value);
        }

        return Outcome<IReadOnlyList<CoaiVendorFields>>.Success(rows);
    }

    private static Outcome<CoaiVendorFields> Fields(JsonNode? node)
    {
        if (node is not JsonObject row)
        {
            return Outcome<CoaiVendorFields>.Failure("a vendor row is a JSON object");
        }

        var refusal = Unknown(row) is { Length: > 0 } unknown ? unknown : Mistyped(row);

        return (refusal.Length, Text(row, "id").Length) switch
        {
            ( > 0, _) => Outcome<CoaiVendorFields>.Failure(refusal),
            (_, 0) => Outcome<CoaiVendorFields>.Failure("a vendor row without an id is not a vendor"),
            _ => Outcome<CoaiVendorFields>.Success(Bound(row)),
        };
    }

    private static CoaiVendorFields Bound(JsonObject row) => new(
        Text(row, "id"), Text(row, "runtime"), Text(row, "model"), Text(row, "baseUrl"), Text(row, "executablePath"),
        Text(row, "dialect"), Text(row, "key"), Text(row, "effort"), Text(row, "remoteVendor"),
        Number(row, "reviewMinutes"), Flag(row, "thinking", true),
        Flag(row, "plan", true), Flag(row, "code", true), Flag(row, "feature", false), Flag(row, "document", false));

    private static string Unknown(JsonObject row)
    {
        var unknown = row.Select(p => p.Key).Where(k => !KnownFields.Contains(k, StringComparer.Ordinal)).ToList();

        return unknown.Count == 0
            ? string.Empty
            : $"vendor row '{Label(row)}' carries a field this build does not know: {string.Join(", ", unknown.Select(u => $"'{u}'"))} — "
              + $"the fields it knows are {string.Join(", ", KnownFields)}; a field dropped on import is a reviewer that is not the one the operator runs";
    }

    /// <summary>ABSENT and WRONG-TYPE are different facts. Absent (or JSON <c>null</c>, which is how the product's
    /// own reader spells absence) folds to the product's default; a present field of another type is refused by
    /// name — <c>"plan":"false"</c> read as absent would tick the plan gate, the opposite of what was written.</summary>
    private static string Mistyped(JsonObject row) =>
        Shapes.Where(s => row[s.Name] is { } value && !Fits(value, s.Shape))
            .Select(s => $"vendor row '{Label(row)}' field '{s.Name}' is {Describe(row[s.Name]!)} — the product reads it as {Expected(s.Shape)}, "
                + "and a mistyped field read as its default is a reviewer that is not the one the operator configured")
            .FirstOrDefault() ?? string.Empty;

    private static bool Fits(JsonNode value, Shape shape) => shape switch
    {
        Shape.Text => value.GetValueKind() == JsonValueKind.String,
        Shape.Flag => value.GetValueKind() is JsonValueKind.True or JsonValueKind.False,
        Shape.Integer => value is JsonValue number && number.GetValueKind() == JsonValueKind.Number && number.TryGetValue<int>(out _),
        _ => value.GetValueKind() == JsonValueKind.Object,
    };

    private static string Expected(Shape shape) => shape switch
    {
        Shape.Text => "a string",
        Shape.Flag => "a boolean",
        Shape.Integer => "an integer",
        _ => "an object",
    };

    private static string Describe(JsonNode value) => $"{value.GetValueKind().ToString().ToLowerInvariant()} {value.ToJsonString()}";

    private static string Label(JsonObject row) => Text(row, "id") is { Length: > 0 } id ? id : "(no id)";

    private static string Text(JsonObject row, string name) =>
        row[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text.Trim() : string.Empty;

    private static int Number(JsonObject row, string name) =>
        row[name] is JsonValue value && value.TryGetValue<int>(out var number) ? number : 0;

    /// <summary>Absent folds to the product's own default for that flag — plan and code absent mean yes,
    /// feature and document absent mean no.</summary>
    private static bool Flag(JsonObject row, string name, bool whenAbsent) =>
        row[name] is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : whenAbsent;
}
