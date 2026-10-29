using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Domain;
using Bench.Domain.Targets;

namespace ModelArms.Core.Experiment;

/// <summary>A reader that records a sentence for every problem instead of stopping at the first. The catalog's
/// <c>UnmappedMemberHandling.Disallow</c> names one unknown member and quits; an experiment file wrong in six places must say so in one
/// answer. Each accessor returns a harmless value after recording, so the walk goes on; the caller builds nothing from a file
/// that left a problem.
/// <para>
/// A required member that is absent is reported once, by <see cref="Object"/> or <see cref="ObjectMember"/>; the accessors stay silent about an absent member so
/// the same fact is never said twice.
/// </para></summary>
internal sealed class StrictJson
{
    private readonly List<string> _problems = [];

    public IReadOnlyList<string> Problems => _problems;

    public void Add(string problem) => _problems.Add(problem);

    /// <summary>A stand-in for a sha that did not parse. Never observable: a file that left any problem builds nothing.</summary>
    private static readonly CommitSha Unread =
        CommitSha.Parse(new string('0', CommitSha.Length)) is Outcome<CommitSha>.Ok ok ? ok.Value : throw new InvalidOperationException("forty zeros are a sha");

    /// <summary>The text as a JSON object, or null after recording why not (not JSON, JSON of another shape, a member written twice).</summary>
    public JsonObject? ParseObject(string json, string fileName) => Parse<JsonObject>(json, fileName, "object");

    /// <summary>The text as a JSON array, or null after recording why not.</summary>
    public JsonArray? ParseArray(string json, string fileName) => Parse<JsonArray>(json, fileName, "array");

    /// <summary>Comments are skipped; a trailing comma and a member written twice at any depth are refused. An operator's file being wrong is an
    /// expected answer, not a crash, so a malformed text and a value of the wrong shape (JSON <c>null</c> included) are recorded.</summary>
    private T? Parse<T>(string json, string fileName, string shape)
        where T : JsonNode
    {
        var before = _problems.Count;
        var node = Document(json, fileName);
        if (node is T shaped)
        {
            return shaped;
        }

        if (_problems.Count == before)
        {
            Add($"{fileName} must be a JSON {shape}");
        }

        return null;
    }

    private JsonNode? Document(string json, string fileName)
    {
        try
        {
            var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = false, AllowDuplicateProperties = false };
            return JsonNode.Parse(json, default, options);
        }
        catch (JsonException ex)
        {
            Add($"{fileName} is not JSON: {ex.Message}");
            return null;
        }
    }

    /// <summary>The value built by <paramref name="build"/> when the walk recorded no problem; otherwise every problem, one per line. The builder
    /// runs only on a clean walk, so it never meets a stand-in.</summary>
    public Outcome<T> Result<T>(Func<T> build) =>
        _problems.Count == 0 ? Outcome<T>.Success(build()) : Outcome<T>.Failure(string.Join('\n', _problems));

    /// <summary>A full 40-character commit sha, or a stand-in after recording why not. An absent member is silent.</summary>
    public CommitSha Sha(JsonObject obj, string path, string name)
    {
        var text = Text(obj, path, name);
        return text.Length == 0
            ? Unread
            : CommitSha.Parse(text).Match(sha => sha, reason => Unparsed(Join(path, name), text, reason));
    }

    private CommitSha Unparsed(string where, string text, string reason)
    {
        Add($"{where} is '{text}': {reason}");
        return Unread;
    }

    /// <summary>A dotted path: <c>Join("target", "base")</c> is <c>target.base</c>, and a member of the top level is its bare name.</summary>
    public static string Join(string path, string name) => path.Length == 0 ? name : $"{path}.{name}";

    /// <summary>The member of <paramref name="parent"/> as an object whose keys the caller judges itself, and that has at least one. An
    /// absent member is the parent's business (a required one is reported by <see cref="Object"/>) and yields an empty object; a member
    /// that is present but is not an object, JSON <c>null</c> included, is recorded.</summary>
    public JsonObject MapMember(JsonObject parent, string path, string name)
    {
        if (!parent.ContainsKey(name))
        {
            return new JsonObject();
        }

        var where = Join(path, name);
        var map = Map(parent[name], where);
        if (parent[name] is JsonObject && map.Count == 0)
        {
            Add($"{where} is empty — it needs at least one member");
        }

        return map;
    }

    /// <summary>The member of <paramref name="parent"/> as an object after <see cref="Object"/>'s checks. Absent: empty and silent.</summary>
    public JsonObject ObjectMember(JsonObject parent, string path, string name, string[] known, string[] required) =>
        parent.ContainsKey(name) ? Object(parent[name], Join(path, name), known, required) : new JsonObject();

    /// <summary>The node as an object whose keys the caller judges itself. Anything else, JSON <c>null</c> included, is recorded
    /// and yields an empty object so the walk goes on.</summary>
    public JsonObject Map(JsonNode? node, string where)
    {
        if (node is JsonObject obj)
        {
            return obj;
        }

        Add($"{where} must be a JSON object");
        return new JsonObject();
    }

    /// <summary>The node as an object, after recording every member it has that this build does not know and every required member it lacks.</summary>
    public JsonObject Object(JsonNode? node, string where, string[] known, string[] required)
    {
        var obj = Map(node, where);
        foreach (var name in obj.Select(member => member.Key).Where(key => !known.Contains(key, StringComparer.Ordinal)))
        {
            Add($"{ExecutableRule.Printable(Join(where, name))} is not a member this build knows (known: {string.Join(", ", known)}) — refused rather than ignored");
        }

        foreach (var name in required.Where(r => node is JsonObject && !obj.ContainsKey(r)))
        {
            Add($"{Join(where, name)} is missing");
        }

        return obj;
    }

    /// <summary>A non-empty text, or the empty string after recording why not. An absent member is silent.</summary>
    public string Text(JsonObject obj, string path, string name) =>
        obj.ContainsKey(name) ? NonEmptyText(obj[name], Join(path, name)) : string.Empty;

    /// <summary>The text a node holds, which must be non-empty; the empty string after recording why not.</summary>
    public string NonEmptyText(JsonNode? node, string where) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? Checked(text, where) : MustBeText(where);

    private string Checked(string text, string where)
    {
        if (ExecutableRule.HoldsNul(text))
        {
            return RefuseNul(where);
        }

        if (text.Trim().Length == 0)
        {
            Add($"{where} is empty — it must be a non-empty text");
        }

        return text;
    }

    /// <summary>A NUL character ends a path in the operating system's eyes and makes <c>Path.GetFullPath</c> throw; no text of the experiment file holds one,
    /// so it is refused here, once, for every member and every element, naming the field. The empty string stands in for the refused text.</summary>
    private string RefuseNul(string where)
    {
        Add($"{where} holds a NUL character — a text of this file never does; refused (D17)");
        return string.Empty;
    }

    /// <summary>A text that may be empty, or the empty string after recording that it is not text. An absent member is silent.</summary>
    public string AnyText(JsonObject obj, string path, string name)
    {
        if (!obj.ContainsKey(name))
        {
            return string.Empty;
        }

        var where = Join(path, name);
        return obj[name] is JsonValue value && value.TryGetValue<string>(out var text) ? NoNul(text, where) : MustBeText(where);
    }

    private string NoNul(string text, string where) => ExecutableRule.HoldsNul(text) ? RefuseNul(where) : text;

    private string MustBeText(string where)
    {
        Add($"{where} must be text");
        return string.Empty;
    }

    /// <summary>A whole number of zero or more, or zero after recording why not. A fraction or a string is refused, never rounded.</summary>
    public int Count(JsonObject obj, string path, string name)
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

        if (number < 0)
        {
            Add($"{where} is {number} — it must be zero or more");
        }

        return number;
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
