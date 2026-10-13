using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bench.Domain.Scouts;

/// <summary>The pinned settings file of the confined launch (E2 S5, plan §3.4): <c>&lt;artefact root&gt;/scouts/pinned-settings.json</c>, written
/// ONCE from the operator's user settings and passed to every scout launch as <c>--settings</c> while <c>--setting-sources ""</c> loads no
/// settings file at all. It carries ONLY <c>effortLevel</c> and <c>modelSettings</c> — the effort levels are the real condition the rule will
/// run under, and nothing else of the user settings may reach a scout: a <c>permissions</c> block would hand the grant back, a <c>hooks</c>
/// block would run code. Its SHA-256 is the run's settings pin, so a change stops the run as every other pin does.</summary>
public static class ScoutPinnedSettings
{
    public const string FileName = "pinned-settings.json";

    /// <summary>The only keys the file may hold, in the order they are written.</summary>
    public static IReadOnlyList<string> AllowedKeys { get; } = ["effortLevel", "modelSettings"];

    /// <summary>Claude's own settings files may carry comments and trailing commas; the pinned file never does.</summary>
    private static readonly JsonDocumentOptions Lenient = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    private static readonly JsonSerializerOptions Canonical = new() { WriteIndented = true, NewLine = "\n" };

    /// <summary>The pinned text projected from the user settings: the two keys as they stand there (a key the user settings lack is absent),
    /// canonical — indented, LF, a trailing newline — so the same settings always hash the same.</summary>
    public static Outcome<string> Project(string userSettingsJson) =>
        Parse(userSettingsJson, "the user settings").Match(settings => Outcome<string>.Success(Text(settings)), Outcome<string>.Failure);

    /// <summary>Reads a pinned file back: an object whose keys are a subset of <see cref="AllowedKeys"/>; refused naming the first key that is
    /// not — the launch loads EVERY key the file names.</summary>
    public static Outcome<string> Verify(string pinnedJson) =>
        Parse(pinnedJson, "the pinned settings file").Match(
            pinned => pinned.Select(p => p.Key).Where(k => !AllowedKeys.Contains(k, StringComparer.Ordinal)).ToList() is { Count: > 0 } extra
                ? Outcome<string>.Failure($"the pinned settings file carries \"{extra[0]}\" — only {string.Join(", ", AllowedKeys)} may be pinned, because the launch loads every key the file names")
                : Outcome<string>.Success(Text(pinned)),
            Outcome<string>.Failure);

    /// <summary>Why the pinned file no longer matches the user settings it was pinned from — each differing key with both values — or empty.</summary>
    public static string Drift(string pinnedJson, string userSettingsJson)
    {
        var pinned = Parse(pinnedJson, "the pinned settings file");
        var user = Parse(userSettingsJson, "the user settings");

        if (pinned is Outcome<JsonObject>.Fail badPin)
        {
            return badPin.Reason;
        }

        if (user is Outcome<JsonObject>.Fail badUser)
        {
            return badUser.Reason;
        }

        var moved = AllowedKeys
            .Select(key => (Key: key, Pinned: Value(((Outcome<JsonObject>.Ok)pinned).Value, key), Now: Value(((Outcome<JsonObject>.Ok)user).Value, key)))
            .Where(k => !string.Equals(k.Pinned, k.Now, StringComparison.Ordinal))
            .Select(k => $"{k.Key} pinned {k.Pinned}, the user settings now say {k.Now}");

        return string.Join("; ", moved);
    }

    private static Outcome<JsonObject> Parse(string json, string what)
    {
        try
        {
            return JsonNode.Parse(json, documentOptions: Lenient) is JsonObject o
                ? Outcome<JsonObject>.Success(o)
                : Outcome<JsonObject>.Failure($"{what} is not an object — nothing can be pinned from it");
        }
        catch (JsonException ex)
        {
            return Outcome<JsonObject>.Failure($"{what} is not JSON — {ex.Message}");
        }
    }

    private static string Text(JsonObject source)
    {
        var projection = new JsonObject();

        foreach (var key in AllowedKeys.Where(k => source[k] is not null))
        {
            projection[key] = source[key]!.DeepClone();
        }

        return projection.ToJsonString(Canonical) + "\n";
    }

    /// <summary>One key's value as a compact JSON text, or the word <c>absent</c>.</summary>
    private static string Value(JsonObject o, string key) => o[key] is { } node ? node.ToJsonString() : "absent";
}
