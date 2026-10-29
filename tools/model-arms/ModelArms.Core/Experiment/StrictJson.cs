using System.Text.Json.Nodes;

namespace ModelArms.Core.Experiment;

/// <summary>A reader that records a sentence for every problem instead of stopping at the first. The catalog's
/// <c>UnmappedMemberHandling.Disallow</c> names one unknown member and quits; an experiment file wrong in six places must say so in one
/// answer. Each accessor returns a harmless value after recording, so the walk goes on; the caller builds nothing from a file
/// that left a problem.
/// <para>
/// A required member that is absent is reported once, by <see cref="Object"/>; the accessors stay silent about an absent member so
/// the same fact is never said twice.
/// </para></summary>
internal sealed class StrictJson
{
    private readonly List<string> _problems = [];

    public IReadOnlyList<string> Problems => _problems;

    public void Add(string problem) => _problems.Add(problem);

    /// <summary>A dotted path: <c>Join("target", "base")</c> is <c>target.base</c>, and a member of the top level is its bare name.</summary>
    public static string Join(string path, string name) => path.Length == 0 ? name : $"{path}.{name}";

    /// <summary>The node as an object whose keys the caller judges itself. An absent node is the parent's business (it already said so)
    /// and yields an empty object; any other node that is not an object is recorded.</summary>
    public JsonObject Map(JsonNode? node, string path)
    {
        if (node is JsonObject obj)
        {
            return obj;
        }

        if (node is not null)
        {
            Add($"{path} must be a JSON object");
        }

        return new JsonObject();
    }

    /// <summary>The node as an object, after recording every member it has that this build does not know and every required member it lacks.</summary>
    public JsonObject Object(JsonNode? node, string path, string[] known, string[] required)
    {
        var obj = Map(node, path);
        if (node is not JsonObject)
        {
            return obj;
        }

        foreach (var name in obj.Select(member => member.Key).Where(key => !known.Contains(key, StringComparer.Ordinal)))
        {
            Add($"{Join(path, name)} is not a member this build knows (known: {string.Join(", ", known)}) — refused rather than ignored");
        }

        foreach (var name in required.Where(r => !obj.ContainsKey(r)))
        {
            Add($"{Join(path, name)} is missing");
        }

        return obj;
    }

    /// <summary>A non-empty text, or the empty string after recording why not. An absent member is silent.</summary>
    public string Text(JsonObject obj, string path, string name) =>
        obj.ContainsKey(name) ? NonEmptyText(obj[name], Join(path, name)) : string.Empty;

    /// <summary>The text a node holds, which must be non-empty; the empty string after recording why not.</summary>
    public string NonEmptyText(JsonNode? node, string where)
    {
        if (node is not JsonValue value || !value.TryGetValue<string>(out var text))
        {
            Add($"{where} must be text");
            return string.Empty;
        }

        if (text.Trim().Length == 0)
        {
            Add($"{where} is empty — it must be a non-empty text");
        }

        return text;
    }

    /// <summary>A whole number above zero, or zero after recording why not. A fraction or a string is refused, never rounded.</summary>
    public int Positive(JsonObject obj, string path, string name)
    {
        if (!obj.ContainsKey(name))
        {
            return 0;
        }

        var where = Join(path, name);
        if (!TryWhole(obj[name], out var number))
        {
            Add($"{where} must be a whole number");
            return 0;
        }

        if (number <= 0)
        {
            Add($"{where} is {number} — it must be greater than zero");
        }

        return number;
    }

    /// <summary>An array of non-empty texts, or an empty list after recording why not. Every bad element is named by its index.
    /// A command passes <paramref name="atLeastOne"/>: an executable and its arguments cannot be nothing.</summary>
    public IReadOnlyList<string> Texts(JsonObject obj, string path, string name, bool atLeastOne = false)
    {
        if (!obj.ContainsKey(name))
        {
            return [];
        }

        var where = Join(path, name);
        if (obj[name] is not JsonArray array)
        {
            Add($"{where} must be an array of text");
            return [];
        }

        IReadOnlyList<string> texts = [.. array.Select((element, index) => NonEmptyText(element, $"{where}[{index}]"))];
        return atLeastOne ? AtLeastOne(texts, where) : texts;
    }

    /// <summary>One of a closed set of words, mapped to its value; the fallback after recording the legal words.</summary>
    public T Choice<T>(JsonObject obj, string path, string name, IReadOnlyList<(string Key, T Value)> choices, T fallback)
    {
        var text = Text(obj, path, name);
        var hit = choices.Where(choice => string.Equals(choice.Key, text, StringComparison.Ordinal)).ToList();
        if (hit.Count == 0 && text.Length > 0)
        {
            Add($"{Join(path, name)} is '{text}' — legal: {string.Join(", ", choices.Select(choice => choice.Key))}");
        }

        return hit.Count == 0 ? fallback : hit[0].Value;
    }

    private IReadOnlyList<string> AtLeastOne(IReadOnlyList<string> texts, string where)
    {
        if (texts.Count == 0)
        {
            Add($"{where} is empty — it needs at least one element");
        }

        return texts;
    }

    private static bool TryWhole(JsonNode? node, out int number)
    {
        number = 0;
        return node is JsonValue value && value.TryGetValue(out number);
    }
}
